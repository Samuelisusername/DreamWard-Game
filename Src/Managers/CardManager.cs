using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

public enum CardType { MapAction, StackAction, Build }
public enum CardEffect { None, Shuffle, Discard, Discount, DrawTop3, Polymorph, Move, Destroy }

public class Card
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
    private float _timer;

    // Top 3 build cards
    public List<Card> Top3Cards { get; private set; } = new List<Card>();

    // Powers
    private bool _discountActive;
    public bool DiscountActive => _discountActive;
    private bool _drawTop3Free;
    public bool DrawTop3Free => _drawTop3Free;

    public CardManager()
    {
        InitSlots();
        InitDecks();
        ShuffleAll();
        RefillBuildTop3();
    }

    // -----------------------
    // Initialization
    // -----------------------
    private void InitSlots()
    {
        for (int i = 0; i < 2; i++)
        {
            MapSlots.Add(new CardSlot());
            StackSlots.Add(new CardSlot());
            BuildSlots.Add(new CardSlot());
        }
    }

    private void InitDecks()
    {
        // Build deck (other cards commented out for combat testing)
        _buildDeck.AddRange(new[]
        {   
            //rest commented out for testing
            new Card { Name = "sniper", Type = CardType.Build, Cost = 1, SpriteName = "sniper" },
            new Card { Name = "aoe", Type = CardType.Build, Cost = 2, SpriteName = "aoe" },
           // new Card { Name = "dreamcatcher", Type = CardType.Build, Cost = 1, SpriteName = "dreamcatcher" },
            new Card { Name = "fence", Type = CardType.Build, Cost = 1, SpriteName = "fence" },
            new Card { Name = "meadow", Type = CardType.Build, Cost = 2, SpriteName = "meadow" }
        });

        // Map action cards
        _mapACDeck.AddRange(new[]
        {
            new Card { Name = "Polymorph", Type = CardType.MapAction, Effect = CardEffect.Polymorph, SpriteName = "polymorph" },
            new Card { Name = "Move", Type = CardType.MapAction, Effect = CardEffect.Move, SpriteName = "move" },
            new Card { Name = "Destroy", Type = CardType.MapAction, Effect = CardEffect.Destroy, SpriteName = "destroy" }
        });

        // Stack action cards
        _stackACDeck.AddRange(new[]
        {
            new Card { Name = "Shuffle", Type = CardType.StackAction, Effect = CardEffect.Shuffle, SpriteName = "shuffle"},
            new Card { Name = "Discard", Type = CardType.StackAction, Effect = CardEffect.Discard, SpriteName = "discard"},
            new Card { Name = "Discount", Type = CardType.StackAction, Effect = CardEffect.Discount, SpriteName = "discount"},
            new Card { Name = "DrawTop3", Type = CardType.StackAction, Effect = CardEffect.DrawTop3, SpriteName = "top3"}
        });
    }

    // -----------------------
    // Update (for ActiveCard timer)
    // -----------------------
    public void Update(float dt)
    {
        if (ActiveCard != null)
        {
            _timer -= dt;
            if (_timer <= 0)
            {
                PlaceCardInSlot(ActiveCard);
                ActiveCard = null;
            }
        }
    }

    // -----------------------
    // Draw a card from deck
    // -----------------------
    public void DrawMapAC(Vector2 pos) => DrawFromDeck(_mapACDeck, pos);
    public void DrawStackAC(Vector2 pos) => DrawFromDeck(_stackACDeck, pos);

    private void DrawFromDeck(List<Card> deck, Vector2 deckPos)
    {
        if (ActiveCard != null || IsSlotsFull(deck)) return;
        ActiveCard = deck[_random.Next(deck.Count)]; // infinite deck, no removal
        ActiveCardStartPos = deckPos;
        _timer = 1f;
    }

    // -----------------------
    // Buy build card from Top3
    // -----------------------
    public Card BuyTopCard(SanityManager sanity, int index = 0)
    {
        if (Top3Cards.Count == 0 || BuildSlots.All(s => s.IsFilled)) return null;

        index = Math.Clamp(index, 0, Top3Cards.Count - 1);
        Card card = Top3Cards[index];

        int cost = _discountActive ? card.Cost - 1 : card.Cost;
        if (!sanity.TryDeductSanity(Math.Max(cost, 0))) return null;

        // Apply card
        _discountActive = false;
        _drawTop3Free = false;
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
            case CardEffect.Discount: _discountActive = true; break;
            case CardEffect.Discard:
                if (Top3Cards.Count > 0) Top3Cards.RemoveAt(0);
                RefillBuildTop3();
                break;
            case CardEffect.DrawTop3: _drawTop3Free = true; break;
        }

        RemoveFromSlots(card);
    }

    // -----------------------
    // Helpers
    // -----------------------
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

    private bool IsSlotsFull(List<Card> deck)
        => deck == _mapACDeck ? MapSlots.All(s => s.IsFilled) :
           deck == _stackACDeck ? StackSlots.All(s => s.IsFilled) : false;

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
            int j = _random.Next(i + 1);
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
            Top3Cards.Add(new Card
            {
                Name = template.Name,
                Type = template.Type,
                Effect = template.Effect,
                Cost = template.Cost,
                SpriteName = template.SpriteName
            });
        }
    }
}