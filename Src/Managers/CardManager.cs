using System;
using System.Collections.Generic;
using System.Linq;
using Gamelab;
using Microsoft.Xna.Framework;

public enum CardType { MapAction, StackAction, Build }
public enum CardEffect { None, Shuffle, Discard, Discount, Polymorph, Move, Destroy, SaveCard }

public record Card
{
    public string Name;
    public CardType Type;
    public CardEffect Effect;
    public int Cost;
    public string SpriteName;
    public int Weight;
}

public class CardSlot
{
    public Card Card;
    public bool Purchased;
    public bool IsFilled  => Card != null;
    public bool IsUnlocked => Purchased;
    public bool IsSaved;
}

public class CardManager
{
    private Random _random => GamelabGame.Rng;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    private SanityManager    sanityManager    => GamelabGame.SanityManager;

    // Slots
    public List<CardSlot> MapSlots   { get; private set; } = new();
    public List<CardSlot> StackSlots { get; private set; } = new();
    public List<CardSlot> BuildSlots { get; private set; } = new();
    public CardEffect? PendingStackAction = null;

    // Powers
    public bool DiscountActive { get; private set; }
    public Card SavedCard      { get; private set; }
    public int  SavedSlotIndex { get; private set; } = -1;
    public int SavedCardExpiresInDays = 0;

    public void SaveCardFromSlot(CardSlot slot)
    {
        int index = BuildSlots.IndexOf(slot);
        if (index == -1 || slot.Card == null)
            return;

        SavedCard = slot.Card with {};
        SavedSlotIndex = index;
        slot.IsSaved = true;
        SavedCardExpiresInDays = 1;
        RemovePendingStackCard();
    }
    public void DiscardCardFromSlot(CardSlot slot, int day)
    {
        int index = BuildSlots.IndexOf(slot);
        if (index == -1) return;
        if (slot.Purchased || slot.IsSaved)
            return;
        slot.Card = GetRandomBuildCardForSlot(index, day, BuildSlots[index].Card);
        RemovePendingStackCard();
    }

    public Card      _activeMapCard    = null;
    public (int,int) _selectedTile     = (-1,-1);
    public Structure _previewStructure = null;
    public List<(Rectangle rect, Card card)> CardRects = new();

    private static readonly List<Card> TowerTemplates = new()
    {
        new Card { Name = "sniper", Type = CardType.Build, Cost = 1, SpriteName = "sniper", Weight = 3 },
        new Card { Name = "aoe",    Type = CardType.Build, Cost = 2, SpriteName = "aoe",    Weight = 2 },
    };

    private static readonly List<Card> BarrierTemplates = new()
    {
        new Card { Name = "fence",        Type = CardType.Build, Cost = 1, SpriteName = "fence",        Weight = 3 },
        new Card { Name = "dreamcatcher", Type = CardType.Build, Cost = 1, SpriteName = "dreamcatcher", Weight = 2 },
        new Card { Name = "void",         Type = CardType.Build, Cost = 2, SpriteName = "void",         Weight = 2 },
        new Card { Name = "spikes",       Type = CardType.Build, Cost = 1, SpriteName = "spikes",       Weight = 3 },
    };

    private static readonly List<Card> SupportTemplates = new()
    {
        new Card { Name = "meadow", Type = CardType.Build, Cost = 2, SpriteName = "meadow", Weight = 1 },
        new Card { Name = "buff",   Type = CardType.Build, Cost = 2, SpriteName = "buff",   Weight = 2 },
        new Card { Name = "decoy",  Type = CardType.Build, Cost = 2, SpriteName = "decoy",  Weight = 2 },
    };

    private static readonly List<Card> MapACTemplates = new()
    {
        new Card { Name = "Move",      Type = CardType.MapAction, Effect = CardEffect.Move,      SpriteName = "move",      Weight = 2 },
        new Card { Name = "Destroy",   Type = CardType.MapAction, Effect = CardEffect.Destroy,   SpriteName = "destroy",   Weight = 2 },
        new Card { Name = "Polymorph", Type = CardType.MapAction, Effect = CardEffect.Polymorph, SpriteName = "polymorph", Weight = 1 },
    };

    private static readonly List<Card> StackACTemplates = new()
    {
        new Card { Name = "Discount", Type = CardType.StackAction, Effect = CardEffect.Discount, SpriteName = "discount", Weight = 1 },
        new Card { Name = "Shuffle",  Type = CardType.StackAction, Effect = CardEffect.Shuffle,  SpriteName = "shuffle",  Weight = 1 },
        new Card { Name = "SaveCard", Type = CardType.StackAction, Effect = CardEffect.SaveCard, SpriteName = "top3",     Weight = 1 },
        new Card { Name = "Discard", Type = CardType.StackAction, Effect = CardEffect.Discard, SpriteName = "discard",   Weight = 1 },
    };

    public CardManager()
    {
        InitSlots();
        GenerateDailyCards(1);
    }

    private void InitSlots()
    {
        MapSlots.Add(new CardSlot());
        StackSlots.Add(new CardSlot());
        for (int i = 0; i < 4; i++) BuildSlots.Add(new CardSlot());
    }

    public void Update(GameTime gameTime) { }

    public void GenerateDailyCards(int day)
    {
        // Reset all slots
        MapSlots[0].Card = null;
        MapSlots[0].Purchased = true;

        StackSlots[0].Card = null;
        StackSlots[0].Purchased = true;

        foreach (var s in BuildSlots)
        {
            if (s.IsSaved)
                continue;
            s.Card = null;
            s.Purchased = false;
        }

        int savedIndex = SavedSlotIndex;

        if (SavedCard != null && savedIndex >= 0 && savedIndex < BuildSlots.Count)
        {
                var slot = BuildSlots[SavedSlotIndex];
                slot.Card = SavedCard with {};
        }

        for (int i = 0; i < BuildSlots.Count; i++)
        {
            if (BuildSlots[i].Card != null) continue; // skip saved slot

            BuildSlots[i].Card = GetRandomBuildCardForSlot(i, day, BuildSlots[i].Card);
        }

        MapSlots[0].Card = GetWeightedRandom(MapACTemplates) with {};
        StackSlots[0].Card = GetWeightedRandom(StackACTemplates) with {};

        DiscountActive = false;
        _activeMapCard = null;
        _selectedTile = (-1, -1);
        _previewStructure = null;

        if (SavedCardExpiresInDays > 0)
            SavedCardExpiresInDays--;

        if (SavedCardExpiresInDays == 0)
        {
            SavedCard = null;
            SavedSlotIndex = -1;

            foreach (var s in BuildSlots)
                s.IsSaved = false;
        }
    }
    public bool TryPurchaseBuildCard(CardSlot slot, SanityManager sanity)
    {
        if (slot == null || slot.Card == null || slot.Purchased) return false;
        int cost = DiscountActive ? Math.Max(slot.Card.Cost - 1, 0) : slot.Card.Cost;
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

                foreach (var slot in BuildSlots.Where(s => s.IsFilled && !s.Purchased && !s.IsSaved))
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

            case CardEffect.SaveCard:
                PendingStackAction = CardEffect.SaveCard;
                PendingStackCard = card;
                return;

            case CardEffect.Discard:
                PendingStackAction = CardEffect.Discard;
                PendingStackCard = card;
                return;
        }

        if (usedSuccessfully)
        {
            RemoveFromSlots(card);
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
                RemoveFromSlots(_activeMapCard);
                _activeMapCard    = null;
                _previewStructure = null;
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

            case CardEffect.Discard:
                if (target is Structure toDiscard && toDiscard.CanMove)
                { mapManager.RemoveStructure((col, row)); return true; }
                return false;

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
                        moving.TilePosition = (col, row);
                        mapManager.PlaceEntity(moving, (col, row));
                        _selectedTile     = (-1, -1);
                        _previewStructure = null;
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
        foreach (var slot in MapSlots.Concat(StackSlots).Concat(BuildSlots))
            if (ReferenceEquals(slot.Card, card)) { slot.Card = null; slot.Purchased = false; return; }
    }

    public CardSlot GetSlotForCard(Card card)
    {
        foreach (var slot in MapSlots.Concat(StackSlots).Concat(BuildSlots))
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
        var all = TowerTemplates.Concat(BarrierTemplates).Concat(SupportTemplates).Concat(MapACTemplates).Concat(StackACTemplates);
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
        {
            return previousCard == null || c.SpriteName != previousCard.SpriteName;
        }

        if (slotIndex == 0)
        {
            do
            {
                newCard = GetWeightedRandom(TowerTemplates) with {};
            }
            while (!isDifferent(newCard));
            return newCard;
        }

        if (slotIndex == 1)
        {
            do
            {
                newCard = GetWeightedRandom(BarrierTemplates) with {};
            }
            while (!isDifferent(newCard));
            return newCard;
        }

        if (slotIndex == 2)
        {
            do
            {
                newCard = GetWeightedRandom(BarrierTemplates) with {};
            }
            while (
                (BuildSlots[1].Card != null && newCard.SpriteName == BuildSlots[1].Card.SpriteName)
                || !isDifferent(newCard)
            );
            return newCard;
        }

        if (slotIndex == 3)
        {
            bool oddDay = (day % 2 == 1);
            do
            {
                newCard = oddDay
                    ? GetWeightedRandom(TowerTemplates) with {}
                    : GetWeightedRandom(SupportTemplates) with {};
            }
            while (!isDifferent(newCard));
            return newCard;
        }
        return null;
    }

    public Card PendingStackCard { get; set; }
    public void RemovePendingStackCard()
    {
        if (PendingStackCard != null)
        {
            RemoveFromSlots(PendingStackCard);
            PendingStackCard = null;
        }
    }
}