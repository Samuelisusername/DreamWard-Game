using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

public enum CardType { MapAction, StackAction, Build }
public enum CardEffect { None, Shuffle, Discard, Discount, DrawTop3, Polymorph, Move, Destroy }

public record Card
{
    public string Name;
    public CardType Type;
    public CardEffect Effect;
    public int Cost;
    public string SpriteName;
}

public class CardSlot
{
    public Card Card;
    public bool IsFilled => Card != null;
}

public class CardManager
{
    private Random _random = new Random();

    // Decks
    private List<Card> _mapACDeck = new();
    private List<Card> _stackACDeck = new();
    private List<Card> _buildDeck = new();

    // Slots (2 each)
    public List<CardSlot> MapSlots { get; private set; } = new();
    public List<CardSlot> StackSlots { get; private set; } = new();
    public List<CardSlot> BuildSlots { get; private set; } = new();

    // Active card display
    public Card ActiveCard { get; private set; }
    public Vector2 ActiveCardStartPos { get; private set; }
    private TimeSpan _timer;

    // Top 3 build cards
    public List<Card> Top3Cards { get; private set; } = new List<Card>();

    // Powers
    public bool DiscountActive {get; private set;}
    public bool DrawTop3Free {get; private set;}
    public Card _activeMapCard = null;
    public (int,int) _selectedTile = (-1,-1);
    public Structure _previewStructure = null;

    public CardManager()
    {
        InitSlots();
        InitDecks();
        ShuffleAll();
        RefillBuildTop3();
        GenerateDailyCards(1);
    }

    // -----------------------
    // Initialization
    // -----------------------
    private void InitSlots()
    {
        MapSlots.Add(new CardSlot());
        StackSlots.Add(new CardSlot());
        BuildSlots.Add(new CardSlot());
        BuildSlots.Add(new CardSlot());
    }

    private void InitDecks()
    {
        // Build deck (other cards commented out for combat testing)
        _buildDeck.AddRange(
        [
            //rest commented out for testing
            new Card { Name = "sniper", Type = CardType.Build, Cost = 1, SpriteName = "sniper" },
            new Card { Name = "aoe", Type = CardType.Build, Cost = 2, SpriteName = "aoe" },
           // new Card { Name = "dreamcatcher", Type = CardType.Build, Cost = 1, SpriteName = "dreamcatcher" },
            new Card { Name = "fence", Type = CardType.Build, Cost = 1, SpriteName = "fence" },
            new Card { Name = "meadow", Type = CardType.Build, Cost = 2, SpriteName = "meadow" }
        ]);

        // Map action cards
        _mapACDeck.AddRange(
        [
            new Card { Name = "Polymorph", Type = CardType.MapAction, Effect = CardEffect.Polymorph, SpriteName = "polymorph" },
            new Card { Name = "Move", Type = CardType.MapAction, Effect = CardEffect.Move, SpriteName = "move" },
            new Card { Name = "Destroy", Type = CardType.MapAction, Effect = CardEffect.Destroy, SpriteName = "destroy" }
        ]);

        // Stack action cards
        _stackACDeck.AddRange(
        [
            new Card { Name = "Shuffle", Type = CardType.StackAction, Effect = CardEffect.Shuffle, SpriteName = "shuffle"},
            new Card { Name = "Discard", Type = CardType.StackAction, Effect = CardEffect.Discard, SpriteName = "discard"},
            new Card { Name = "Discount", Type = CardType.StackAction, Effect = CardEffect.Discount, SpriteName = "discount"},
            new Card { Name = "DrawTop3", Type = CardType.StackAction, Effect = CardEffect.DrawTop3, SpriteName = "top3"}
        ]);
    }

    // -----------------------
    // Update (for ActiveCard timer)
    // -----------------------
    public void Update(GameTime gameTime)
    {
        if (ActiveCard != null)
        {
            _timer -= gameTime.ElapsedGameTime;
            if (_timer <= TimeSpan.Zero)
            {
                PlaceCardInSlot(ActiveCard);
                ActiveCard = null;
            }
        }
    }

    // -----------------------
    // Draw a card from deck
    // -----------------------
    public void DrawMapAC(Vector2 pos) => DrawFromDeck(CardType.MapAction, pos);
    public void DrawStackAC(Vector2 pos) => DrawFromDeck(CardType.StackAction, pos);

    private void DrawFromDeck(CardType cardType, Vector2 deckPos)
    {
        if (ActiveCard != null || IsSlotsFull(cardType)) return;
        switch (cardType)
        {
            case CardType.MapAction:
                ActiveCard = _mapACDeck[_random.Next(_mapACDeck.Count)]; // infinite deck, no removal
                ActiveCardStartPos = deckPos;
                _timer = TimeSpan.FromSeconds(1);
                break;
            case CardType.StackAction:                
                ActiveCard = _stackACDeck[_random.Next(_stackACDeck.Count)]; // infinite deck, no removal
                ActiveCardStartPos = deckPos;
                _timer = TimeSpan.FromSeconds(1);
                break;
            default:
                throw new ArgumentException("Cannot draw card of type Build?");
        }
        
    }

    // -----------------------
    // Buy build card from Top3
    // -----------------------
    public Card BuyTopCard(SanityManager sanity, int index = 0)
    {
        if (Top3Cards.Count == 0 || IsSlotsFull(CardType.Build)) return null;

        index = Math.Clamp(index, 0, Top3Cards.Count - 1);
        Card card = Top3Cards[index];

        int cost = DiscountActive ? card.Cost - 1 : card.Cost;
        if (!sanity.TryDeductSanity(Math.Max(cost, 0))) return null;

        // Apply card
        DiscountActive = false;
        DrawTop3Free = false;
        PlaceCardInSlot(card);
        Top3Cards.RemoveAt(index);
        RefillBuildTop3();
        return card;
    }

    // -----------------------
    // Use stack card effects
    // -----------------------
    public void UseCard(Card card)
    {
        switch (card.Effect)
        {
            case CardEffect.Shuffle: ShuffleBuildDeck(); break;
            case CardEffect.Discount: DiscountActive = true; break;
            case CardEffect.Discard:
                if (Top3Cards.Count > 0) Top3Cards.RemoveAt(0);
                RefillBuildTop3();
                break;
            case CardEffect.DrawTop3: DrawTop3Free = true; break;
        }

        RemoveFromSlots(card);
    }

    // -----------------------
    // Helpers
    // -----------------------
    public void GenerateDailyCards(int day)
    {
        // Clear old cards
        MapSlots[0].Card = null;
        StackSlots[0].Card = null;

        // Number of cards increases with day (but only 1 slot → pick stronger/random logic)
        int mapDraws = Math.Min(day, _mapACDeck.Count);
        int stackDraws = Math.Min(day, _stackACDeck.Count);

        // Pick final card from multiple draws (simulate "better odds")
        Card mapCard = null;
        for (int i = 0; i < mapDraws; i++)
            mapCard = _mapACDeck[_random.Next(_mapACDeck.Count)];

        Card stackCard = null;
        for (int i = 0; i < stackDraws; i++)
            stackCard = _stackACDeck[_random.Next(_stackACDeck.Count)];

        MapSlots[0].Card = mapCard;
        StackSlots[0].Card = stackCard;
    }

    public Card GetCardBySprite(string spriteName)
    {
        var allCards = _buildDeck.Concat(_mapACDeck).Concat(_stackACDeck);
        var found = allCards.FirstOrDefault(c => c.SpriteName == spriteName);
        if (found == null) return null;
        return new Card{Name = found.Name, Type = found.Type, Cost = found.Cost, Effect = found.Effect, SpriteName = found.SpriteName};
    }
    
    private void PlaceCardInSlot(Card card)
    {
        foreach (var slot in GetSlots(card.Type))
        {
            if (!slot.IsFilled) { slot.Card = card; break; }
        }
    }

    private void RemoveFromSlots(Card card)
    {
        foreach (var slot in MapSlots.Concat(StackSlots).Concat(BuildSlots))
        {
            if (slot.Card == card) { slot.Card = null; return; }
        }
    }

    private bool IsSlotsFull(CardType cardType)
        => cardType == CardType.MapAction ? MapSlots.All(s => s.IsFilled) :
           cardType == CardType.StackAction ? StackSlots.All(s => s.IsFilled) :
           cardType == CardType.Build ? BuildSlots.All(s => s.IsFilled) : false;

    private List<CardSlot> GetSlots(CardType type)
        => type switch
        {
            CardType.MapAction => MapSlots,
            CardType.StackAction => StackSlots,
            _ => BuildSlots
        };

    public void ShuffleAll()
    {
        Shuffle(_mapACDeck);
        Shuffle(_stackACDeck);
        Shuffle(_buildDeck);
    }

    private void Shuffle(List<Card> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void ShuffleBuildDeck()
    {
        Top3Cards.Clear();
        RefillBuildTop3();
    }

    // Fill Top3Cards until 3 cards are visible
    public void RefillBuildTop3()
    {
        while (Top3Cards.Count < 3)
        {
            var template = _buildDeck[_random.Next(_buildDeck.Count)];
            Top3Cards.Add(template with {});
        }
    }

    public Structure GetStructureFromCard(Card card, (int col, int row) pos)
    {
        
        string animKey = card.SpriteName switch
        {
            "dreamcatcher" => "wall_idle", // map dreamcatcher to wall
            "sniper" => "towerSniper_idle",
            "aoe" => "towerAOE_idle",
            "fence" => "fence_idle",
            "meadow" => "meadow_idle",
            _ => null
        };

        if (animKey == null) return null; // no matching animation

        Structure structure = card.Name switch
        {
            "sniper" => new Tower(animKey, pos, damagePerSecond: 8f, health: 100),
            "aoe" => new AreaTower(animKey, pos, damagePerSecond: 5f, health: 80),
            "fence" => new Fence(animKey, pos),
            "meadow" => new PlacableMeadow(animKey, pos,2),
            "dreamcatcher" => new Wall(animKey, pos),
            _ => null
        };

        return structure;
    }
    public bool HandleCardUseClicks(Vector2 mousePos, int screenW, int screenH, int startY)
    {
        var slots = MapSlots.Concat(BuildSlots).ToList();

        int cardW = 128 * 2;
        int cardH = 128 * 2;
        int spacing = 5;

        int totalWidth = slots.Count * cardW + (slots.Count - 1) * spacing;

        int startX = screenW - totalWidth - 10;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            Rectangle rect = new(
                startX + i * (cardW + spacing),
                startY,
                cardW,
                cardH
            );

            if (slot.Card != null && rect.Contains(mousePos))
            {
                var card = slot.Card;

                if (card.Type == CardType.Build || card.Type == CardType.MapAction)
                {
                    if (_activeMapCard == card)
                    {
                        _activeMapCard = null;
                        _previewStructure = null;
                        _selectedTile = (-1,-1);
                    }
                    else
                    {
                        _activeMapCard = card;
                        _selectedTile = (-1,-1);
                        _previewStructure = null;
                    }
                }
                else
                {
                    UseCard(card);
                }

                return true;
            }
        }
        return false;
    }
    public bool HandleMapCardUse(MapManager mapManager, Vector2 pos)
    {
        if(_activeMapCard == null) return false;

        (int col, int row) = mapManager.GetTileFromScreenPosition(pos);
        if (col < 0) return false;

        if (_activeMapCard.Type == CardType.Build)
        {
            bool success = mapManager.PlaceEntity(_activeMapCard, (col, row));

            if (success)
            {
                UseCard(_activeMapCard);
                _activeMapCard = null;
                _previewStructure = null;
            }

            return success;
        }

        if (_activeMapCard.Type == CardType.MapAction)
        {
            bool success = ApplyMapAction(mapManager, _activeMapCard, pos);

            if (success && _selectedTile == (-1,-1))
            {
                UseCard(_activeMapCard);
                _activeMapCard = null;
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
                if (target is Structure toDestroy && toDestroy.CanMove)
                {
                    mapManager.RemoveStructure((col, row));
                    return true;
                }
                return false;

            case CardEffect.Move:
            {
                if (target is Structure toMove && toMove.CanMove)
                {
                    mapManager.RemoveStructure((col, row));
                    _selectedTile = (col, row);
                    _previewStructure = toMove;
                    return false;
                }

                if (_selectedTile != (-1, -1) && mapManager.GetStructure((col, row)) == null)
                {
                    var movingEntity = _previewStructure;
                    if (movingEntity != null)
                    {
                        movingEntity.TilePosition = (col, row);
                        mapManager.PlaceEntity(movingEntity, (col, row));

                        _selectedTile = (-1, -1);
                        _previewStructure = null;
                        return true;
                    }
                }

                return false;
            }

            case CardEffect.Polymorph:
                if (target is Meadow meadow && meadow.HasSheep && !meadow.IsPolymorphed)
                {
                    meadow.PolymorphSheep();
                    return true;
                }
                return false;

            default:
                return false;
        }
    }
    
}