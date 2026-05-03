using System;
using System.Collections.Generic;
using System.Linq;
using Gamelab;
using Microsoft.Xna.Framework;
using Gamelab.Managers;
using Gamelab.Screens;

public enum CardType { MapAction, StackAction, Build }
public enum CardEffect { None, Shuffle, Discount, Polymorph, Move, Destroy, Refill }

public record Card
{
    public string Name;
    public CardType Type;
    public CardEffect Effect;
    public int Cost;
    public string SpriteName;
    public int Weight;
    public int StackCount = 1;
}

public class CardSlot
{
    public Card Card;
    public bool Purchased;
    public bool IsFilled  => Card != null;
    public bool IsUnlocked => Purchased;
}

public class CardManager
{
    // In CardManager — add this field:
    private (int col, int row)? _voidWarningTile = null; // the tile that last triggered the warning
    private Random _random => GamelabGame.Rng;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    private SanityManager    sanityManager    => GamelabGame.SanityManager;

    // Slots
    public List<CardSlot> StackSlots { get; private set; } = new();
    public List<CardSlot> BuildSlots { get; private set; } = new();
    public enum BuildSlotType {Tower, Barrier, Support}
    private readonly List<BuildSlotType> _buildSlotTypes = new();
    private void InitSlots()
    {
        // Slot 0 → Tower
        BuildSlots.Add(new CardSlot());
        _buildSlotTypes.Add(BuildSlotType.Tower);

        // Slot 1 → Barrier
        BuildSlots.Add(new CardSlot());
        _buildSlotTypes.Add(BuildSlotType.Barrier);

        // Slot 2 → Barrier
        BuildSlots.Add(new CardSlot());
        _buildSlotTypes.Add(BuildSlotType.Barrier);

        // Slot 3 → Support
        BuildSlots.Add(new CardSlot());
        _buildSlotTypes.Add(BuildSlotType.Support);

        // Stack slots
        for (int i = 0; i < 2; i++)
            StackSlots.Add(new CardSlot());
    }
    private bool IsTentEnclosed()
{
    var mapManager = GamelabGame.MapManager;
    bool[,] visited = new bool[mapManager.Columns, mapManager.Rows];
    var tentPos = mapManager.GetTent();
    if (tentPos.col == -1) return false;

    Queue<(int, int)> queue = new();
    queue.Enqueue(tentPos);
    visited[tentPos.col, tentPos.row] = true;

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        foreach (var neighbor in MapUtils.GetSurroundingTiles(current, true))
        {
            if (!MapUtils.IsInBounds(neighbor)) return false; // reached map border → not enclosed
            int c = neighbor.Item1, r = neighbor.Item2;
            if (visited[c, r]) continue;
            // Block on void tiles (structure) OR missing ground
            if (mapManager.GetStructure(neighbor) is VoidTile) continue;
            if (mapManager.GetTileIndex(neighbor) == -1) continue;
            visited[c, r] = true;
            queue.Enqueue(neighbor);
        }
    }
    return true;
}
    public void AddSlots(int type)
    {
        BuildSlotType slotType = type switch
        {
            1 => BuildSlotType.Tower,
            2 => BuildSlotType.Barrier,
            3 => BuildSlotType.Support,
            _ => BuildSlotType.Tower
        };

        BuildSlots.Add(new CardSlot());
        _buildSlotTypes.Add(slotType);
        
        int index = BuildSlots.Count - 1;

        BuildSlots[index].Card = GetRandomBuildCardForSlot(
            index,
            gamePhaseManager.DayCount,
            null
        );
    }

    // Powers
    public bool DiscountActive { get; private set; }
    public Card      _activeMapCard    = null;
    public (int,int) _selectedTile     = (-1,-1);
    public Structure _previewStructure = null;
    public List<(Rectangle rect, Card card)> CardRects = new();

    public HashSet<string> UnlockedCards { get; private set; } = new() {"sniper","fence"};
    public bool IsUnlocked(string spriteName) => UnlockedCards.Contains(spriteName);
    public bool UnlockCard(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return false;
        return UnlockedCards.Add(spriteName);
    }

    private static readonly List<Card> TowerTemplates = new()
    {
        new Card { Name = "sniper", Type = CardType.Build, Cost = 7, SpriteName = "sniper", Weight = 3 },
        new Card { Name = "aoe",    Type = CardType.Build, Cost = 6, SpriteName = "aoe",    Weight = 2 },
    };

    public static readonly List<Card> BarrierTemplates = new()
    {
        new Card { Name = "fence",        Type = CardType.Build, Cost = 1, SpriteName = "fence",        Weight = 3, StackCount = 10 },
        new Card { Name = "dreamcatcher", Type = CardType.Build, Cost = 2, SpriteName = "dreamcatcher", Weight = 2, StackCount = 10 },
        new Card { Name = "void",         Type = CardType.Build, Cost = 2, SpriteName = "void",         Weight = 2, StackCount = 10 },
        new Card { Name = "spikes",       Type = CardType.Build, Cost = 2, SpriteName = "spikes",       Weight = 3, StackCount = 3 },
    };

    private static readonly List<Card> SupportTemplates = new()
    {
        new Card { Name = "meadow", Type = CardType.Build, Cost = 5, SpriteName = "meadow", Weight = 1 },
        new Card { Name = "buff",   Type = CardType.Build, Cost = 3, SpriteName = "buff",   Weight = 2 },
        new Card { Name = "decoy",  Type = CardType.Build, Cost = 3, SpriteName = "decoy",  Weight = 2 },
        new Card { Name = "Polymorph", Type = CardType.MapAction, Cost = 2, SpriteName = "polymorph", Weight = 1, Effect = CardEffect.Polymorph },
    };

    private static readonly List<Card> StackACTemplates = new()
    {
        new Card { Name = "Discount", Type = CardType.StackAction, Effect = CardEffect.Discount, SpriteName = "discount", Weight = 1 },
        new Card { Name = "Shuffle",  Type = CardType.StackAction, Effect = CardEffect.Shuffle,  SpriteName = "shuffle",  Weight = 1 },
        new Card { Name = "Refill",   Type = CardType.StackAction, Effect = CardEffect.Refill,   SpriteName = "top3",   Weight = 1 },
    };

    public CardManager()
    {
        InitSlots();
        GenerateDailyCards(1);
    }

    public void Update(GameTime gameTime) { }

    public void GenerateDailyCards(int day)
    {
        foreach (var s in BuildSlots)
        {
            s.Card = null;
            s.Purchased = false;
        }

        for (int i = 0; i < BuildSlots.Count; i++)
        {
            BuildSlots[i].Card = GetRandomBuildCardForSlot(i, day, BuildSlots[i].Card);
        }

        DiscountActive = false;
        _activeMapCard = null;
        _selectedTile = (-1, -1);
        _previewStructure = null;
    }

    public bool TryPurchaseBuildCard(CardSlot slot, SanityManager sanity)
    {
        if (slot == null || slot.Card == null || slot.Purchased) return false;
        int cost = DiscountActive ? Math.Max(slot.Card.Cost - 2, 0) : slot.Card.Cost;
        if (!sanity.TryDeductSanity(cost)) return false;
        slot.Purchased = true;
        DiscountActive = false;
        return true;
    }

    public void UseCard(Card card)
    {
        bool usedSuccessfully = false;

        switch (card.Effect)
        {
            case CardEffect.Shuffle:
            {
                bool anyValid = false;

                foreach (var slot in BuildSlots.Where(s => s.IsFilled && !s.Purchased))
                {
                    anyValid = true;

                    int index = BuildSlots.IndexOf(slot);
                    if (index == -1) continue;

                    slot.Card = GetRandomBuildCardForSlot(
                        index,
                        gamePhaseManager.DayCount,
                        slot.Card
                    );
                }

                usedSuccessfully = anyValid;
                break;
            }

            case CardEffect.Discount:
            {
                bool anyValid = BuildSlots.Any(s => s.IsFilled && !s.Purchased);

                if (anyValid)
                {
                    DiscountActive = true;
                    usedSuccessfully = true;
                }

                break;
            }

            case CardEffect.Refill:
            {
                bool anyEmpty = BuildSlots.Any(s => !s.IsFilled);

                if (anyEmpty)
                {
                    RefillEmptySlots(gamePhaseManager.DayCount);
                    usedSuccessfully = true;
                }

                break;
            }
        }

        if (usedSuccessfully)
        {
            RemoveFromSlots(card);
        }
    }

    /// <summary>
    /// Fills any empty (unpurchased, unfilled) build slots with a new random card.
    /// </summary>
    public void RefillEmptySlots(int day)
    {
        for (int i = 0; i < BuildSlots.Count; i++)
        {
            var slot = BuildSlots[i];
            if (slot.IsFilled) continue;
            slot.Card = GetRandomBuildCardForSlot(i, day, null);
            slot.Purchased = false;
        }
    }

    public bool HandleMapCardUse(MapManager mapManager, Vector2 pos)
    {
        if (_activeMapCard == null) return false;

        (int col, int row) = mapManager.GetTileFromScreenPosition(pos);
        if (col < 0) return false;

        if (_activeMapCard.Type == CardType.Build)
        {
            bool success = mapManager.PlaceEntity(_activeMapCard, (col, row));
            if (success)
            {

                       if (_activeMapCard.SpriteName == "void" && IsTentEnclosed())
        {
            if (_voidWarningTile == (col, row))
            {
                // Warned already, placed again → game over
                GameOverScreen.DidWin = false;
                GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
                return true;
            }
            else
            {
                // First time: undo, refund, show sheep warning
                _voidWarningTile = (col, row);
                mapManager.RemoveStructure((col, row));
                sanityManager.AddSanity(_activeMapCard.Cost);
                GamelabGame.Instance.MapScreenInstance?.ShowVoidWarning(
                    "Careful! Placing a void here will cut off our tent from the outside world. Are you sure?");
                return false;
            }
        }

        _voidWarningTile = null;
                _activeMapCard.StackCount--;
                if (_activeMapCard.StackCount <= 0)
                {
                    RemoveFromSlots(_activeMapCard);
                    _activeMapCard    = null;
                    _previewStructure = null;
                }
                else
                {
                    _previewStructure = null;
                }
            }
            return success;
        }

        if (_activeMapCard.Type == CardType.MapAction)
        {
            bool success = ApplyMapAction(mapManager, _activeMapCard, pos);
            if (success && _selectedTile == (-1, -1))
            {
                RemoveFromSlots(_activeMapCard);
                _activeMapCard    = null;
                _previewStructure = null;
            }
            return success;
        }

        return false;
    }

    private bool ApplyMapAction(MapManager mapManager, Card card, Vector2 mousePos)
    {
        (int col, int row) = mapManager.GetTileFromScreenPosition(mousePos);
        if (col < 0) return false;

        Entity target = mapManager.GetStructure((col, row));

        switch (card.Effect)
        {
           case CardEffect.Destroy:
            {
                if (target is not Structure toDestroy)
                    return false;
                if (toDestroy is VoidTile)
                    return false;
                if (!toDestroy.CanMove)
                    return false;
                mapManager.RemoveStructure((col, row));
                return true;
            }

            case CardEffect.Move:
            {
                if (target is Structure toMove && toMove.CanMove)
                {
                    mapManager.RemoveStructure((col, row));
                    _selectedTile     = (col, row);
                    _previewStructure = toMove;
                    return false;
                }
                if (_selectedTile != (-1, -1) && mapManager.GetStructure((col, row)) == null)
                {
                    var moving = _previewStructure;
                    if (moving != null)
                    {
                        bool isSameTile = _selectedTile.Item1 == col && _selectedTile.Item2 == row;
                        moving.TilePosition = (col, row);
                        mapManager.PlaceEntity(moving, (col, row));
                        _selectedTile     = (-1, -1);
                        _previewStructure = null;
                        if (!isSameTile)
                        {
                            GamelabGame.SanityManager.DeductSanity(2);
                            
                        }
                        return true;
                    }
                }
                return false;
            }

            case CardEffect.Polymorph:
                if (target is Meadow meadow && meadow.HasSheep && !meadow.IsPolymorphed)
                { meadow.PolymorphSheep(); return true; }
                return false;

            default: return false;
        }
    }

    private void RemoveFromSlots(Card card)
    {
        foreach (var slot in StackSlots.Concat(BuildSlots))
        {
            if (!ReferenceEquals(slot.Card, card)) continue;
            slot.Card = null;
            slot.Purchased = false;
            return;
        }
    }

    public CardSlot GetSlotForCard(Card card)
    {
        foreach (var slot in StackSlots.Concat(BuildSlots))
            if (ReferenceEquals(slot.Card, card)) return slot;
        return null;
    }

    private Card GetWeightedRandom(List<Card> pool)
    {
        int total = pool.Sum(c => c.Weight);
        int roll  = _random.Next(total);
        int cum   = 0;
        foreach (var c in pool) { cum += c.Weight; if (roll < cum) return c; }
        return pool[^1];
    }

    public Structure GetStructureFromCard(Card card, (int col, int row) pos)
    {
        return card.Name switch
        {
            "sniper"       => new SniperTower(pos),
            "aoe"          => new AreaTower(pos),
            "fence"        => new Fence(pos),
            "meadow"       => new PlacableMeadow(pos, 2),
            "dreamcatcher" => new Wall(pos),
            "void"         => new VoidTile(pos),
            "spikes"       => new Spike(pos),
            "decoy"        => new Decoy(pos),
            "buff"         => new Buff(pos),
            _              => null
        };
    }

    public Card GetCardBySprite(string spriteName)
    {
        var all = TowerTemplates.Concat(BarrierTemplates).Concat(SupportTemplates).Concat(StackACTemplates);
        var found = all.FirstOrDefault(c => c.SpriteName == spriteName);
        return found == null ? null : found with {};
    }

    public void ForceSpawnInto(string spriteName, List<CardSlot> slots)
    {
        var slot = slots.FirstOrDefault(s => s.Card == null);
        if (slot == null) return;
        var card = GetCardBySprite(spriteName);
        if (card == null) return;
        if (card.Cost > 0 && !sanityManager.TryDeductSanity(card.Cost)) return;
        slot.Card      = card;
        slot.Purchased = true;
    }

    public Card GetRandomBuildCardForSlot(int slotIndex, int day, Card previousCard = null)
    {
        Card newCard = null;

        bool isDifferent(Card c)
            => previousCard == null || c.SpriteName != previousCard.SpriteName;

        BuildSlotType type = _buildSlotTypes[slotIndex];

        List<Card> pool = type switch
        {
            BuildSlotType.Tower   => TowerTemplates,
            BuildSlotType.Barrier => BarrierTemplates,
            BuildSlotType.Support => SupportTemplates,
            _ => TowerTemplates
        };

        // Meadow Bonus Unlock
        var filteredPool = pool
            .Where(c => (c.Type == CardType.Build || c.Type == CardType.MapAction)  && IsUnlocked(c.SpriteName))
            .ToList();

        if (filteredPool.Count == 0)
            return null;

        do
        {
            newCard = GetWeightedRandom(filteredPool) with {};
        }
        while (!isDifferent(newCard) && filteredPool.Count > 1); // 🔥 prevent infinite loop

        return newCard;
    }

    /// <summary>
    /// Called when the sheep successfully moves to a new meadow.
    /// Gives the player a random stack action card.
    /// </summary>
    public void GrantStackCard()
    {
        Card newCard = GetWeightedRandom(StackACTemplates) with {};

        // Find an empty slot first
        for (int i = 0; i < StackSlots.Count; i++)
        {
            if (StackSlots[i].Card == null)
            {
                StackSlots[i].Card      = newCard;
                StackSlots[i].Purchased = true;
                return;
            }
        }

        // Both slots full — evict slot 0, shift slot 1 → slot 0, put new in slot 1
        StackSlots[0].Card      = StackSlots[1].Card;
        StackSlots[0].Purchased = StackSlots[1].Purchased;
        StackSlots[1].Card      = newCard;
        StackSlots[1].Purchased = true;
    }

    public bool TryMoveStructure(MapManager mapManager, (int col, int row) tile)
    {
        var target = mapManager.GetStructure(tile);

        // STEP 1: pick up
        if (_selectedTile == (-1, -1))
        {
            if (target is Structure toMove && toMove.CanMove)
            {
                mapManager.RemoveStructure(tile);
                _selectedTile = tile;
                _previewStructure = toMove;
            }
            return false;
        }

        // STEP 2: place
        if (mapManager.GetStructure(tile) == null)
        {
            var moving = _previewStructure;
            if (moving == null) return false;

            moving.TilePosition = tile;
            mapManager.PlaceEntity(moving, tile);

            bool isSameTile = _selectedTile == tile;

            _selectedTile = (-1, -1);
            _previewStructure = null;

            if (!isSameTile)
                GamelabGame.SanityManager.DeductSanity(2);

            return true;
        }

        return false;
    }
}