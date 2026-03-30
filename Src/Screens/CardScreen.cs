using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;

using Gamelab.Managers;

namespace Gamelab.Screens;

public class CardScreen : GamelabGameScreen
{
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CardManager cardManager => GamelabGame.CardManager;
    private Rectangle _toMapScreenBtn;

    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private Dictionary<string, Rectangle> _cardRegions;

    // Colors
    private readonly Color BG = new(0x18, 0x3A, 0x37);    
    private readonly Color SLOT = new(0x08, 0x30, 0x2B); 
    private readonly Color TEXT = new(0xBC, 0xB6, 0x9D); 
    private readonly Color BTN = new(0x08, 0x30, 0x2B);   
    private readonly Color BTN_TEXT = new(0xBC, 0xB6, 0x9D);

    public CardScreen(GamelabGame game) : base(game)
    {
    }

    // -----------------------
    // LOAD
    // -----------------------
    public override void LoadContent()
    {
        AudioManager.LoadAllSounds(Game.Content, "Sounds/action_cards");
        AudioManager.LoadSound(Game.Content, "coinsplash", "Sounds/action_cards/coinsplash");
        AudioManager.LoadSound(Game.Content, "dragonflap", "Sounds/dragonflap");
        _font = Content.Load<SpriteFont>("font");

        _dummy = new Texture2D(GraphicsDevice, 1, 1);
        _dummy.SetData(new[] { Color.White });
        _toMapScreenBtn = new Rectangle(20, 20, 130, 50);

        _cardTexture = Content.Load<Texture2D>("Sprites/cards");

        _cardRegions = new()
        {
            { "sniper", new(0,0,128,128) }, { "aoe", new(0,128,128,128) },
            { "dreamcatcher", new(0,256,128,128) }, { "fence", new(0,384,128,128) },
            { "meadow", new(0,512,128,128) }, { "shuffle", new(0,640,128,128) },
            { "discount", new(0,768,128,128) }, { "top3", new(0,896,128,128) },
            { "discard", new(0,1024,128,128) }, { "deck", new(0,1152,128,128) },
            { "polymorph", new(0,1280,128,128) }, { "move", new(0,1408,128,128) },
            { "destroy", new(0,1536,128,128) }
        };
    }

    // -----------------------
    // UPDATE
    // -----------------------
    protected override void Update(GameTime gameTime,
        KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads,
        MouseStateExtended mouse)
    {
        cardManager.Update(gameTime);

        if (mouse.WasButtonReleased(MouseButton.Left))
        {
            var pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            HandleClick(pos);
        }

        if (keyboard.WasKeyPressed(Keys.P))
        {
            Game.StartScreenInstance ??= new StartScreen(Game);
            Game.SwitchToScreen(Game.StartScreenInstance);
            return;
        }
        #if DEBUG
        // 1-3: Spawn Build Cards
        if (keyboard.WasKeyPressed(Keys.D1)) ForceSpawnCard("sniper", cardManager.BuildSlots);
        if (keyboard.WasKeyPressed(Keys.D2)) ForceSpawnCard("aoe", cardManager.BuildSlots);
        if (keyboard.WasKeyPressed(Keys.D3)) ForceSpawnCard("meadow", cardManager.BuildSlots);
        
        // 4-5: Spawn Map Action Cards
        if (keyboard.WasKeyPressed(Keys.D4)) ForceSpawnCard("polymorph", cardManager.MapSlots);
        if (keyboard.WasKeyPressed(Keys.D5)) ForceSpawnCard("destroy", cardManager.MapSlots);

        // 6-7: Spawn Stack Action Cards
        if (keyboard.WasKeyPressed(Keys.D6)) ForceSpawnCard("shuffle", cardManager.StackSlots);
        if (keyboard.WasKeyPressed(Keys.D7)) ForceSpawnCard("discount", cardManager.StackSlots);
        #endif
    }
    private void ForceSpawnCard(string spriteName, List<CardSlot> targetSlots)
    {
        // Find the first slot in the target list that doesn't have a card yet
        var emptySlot = targetSlots.FirstOrDefault(s => s.Card == null);
        
        if (emptySlot != null)
        {
            // Inject a new card directly into the UI slot.
            // NOTE: You may need to adjust this instantiation based on the required 
            // arguments in your actual Card.cs constructor (e.g., setting Cost or CardType).
            Card debugCard = spriteName switch
            {
                "sniper" => new Card { Name = "sniper", Type = CardType.Build, Cost = 1, SpriteName = "sniper" },
                "aoe" => new Card { Name = "aoe", Type = CardType.Build, Cost = 2, SpriteName = "aoe" },
                "meadow" => new Card { Name = "meadow", Type = CardType.Build, Cost = 1, SpriteName = "meadow" },
                
                // Map Action Cards
                "polymorph" => new Card { Name = "Polymorph", Type = CardType.MapAction, Effect = CardEffect.Polymorph, SpriteName = "polymorph" },
                "destroy" => new Card { Name = "Destroy", Type = CardType.MapAction, Effect = CardEffect.Destroy, SpriteName = "destroy" },

                // Stack Action Cards
                "shuffle" => new Card { Name = "Shuffle", Type = CardType.StackAction, Effect = CardEffect.Shuffle, SpriteName = "shuffle" },
                "discount" => new Card { Name = "Discount", Type = CardType.StackAction, Effect = CardEffect.Discount, SpriteName = "discount" },
                
                // Fallback just in case
                _ => new Card { Name = spriteName, SpriteName = spriteName }
            };

            emptySlot.Card = debugCard;
        }
    }

    // -----------------------
    // CLICK HANDLING
    // -----------------------
    private void HandleClick(Vector2 pos)
    {
        var l = GetLayout();

        if (l.MapDeck.Contains(pos)) { cardManager.DrawMapAC(l.MapDeck.Location.ToVector2()); AudioManager.Play("card-place"); return; }
        if (l.StackDeck.Contains(pos)) { cardManager.DrawStackAC(l.StackDeck.Location.ToVector2()); AudioManager.Play("card-place"); return; }

        if (_toMapScreenBtn.Contains(pos))
        {
            Game.SwitchToScreen(Game.MapScreenInstance);
            AudioManager.Play("dragonflap");
            return;
        }

        for (int i = 0; i < l.Top3Rects.Count; i++)
        {
            if (l.BuyButtons[i].Contains(pos) &&
                (cardManager.DrawTop3Free || i == 0) &&
                !cardManager.BuildSlots.All(s => s.IsFilled))
            {
                cardManager.BuyTopCard(sanityManager, i);
                AudioManager.Play("coinsplash");
                return;
            }
        }

        HandleSlotButtons(cardManager.MapSlots, l.MapSlots, pos, CardType.MapAction);
        HandleSlotButtons(cardManager.StackSlots, l.StackSlots, pos, CardType.StackAction);
        HandleSlotButtons(cardManager.BuildSlots, l.BuildSlots, pos, CardType.Build);
    }

    // -----------------------
    // DRAW
    // -----------------------
    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GraphicsDevice.Clear(BG);
        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix());
        DrawButton(_toMapScreenBtn, "To Map");

        var l = GetLayout();

        // Titles
        spriteBatch.DrawString(_font, "Action & Build Cards", l.TitlePos, TEXT);
        spriteBatch.DrawString(_font, "Map Action Cards", l.MapLabel, TEXT);
        spriteBatch.DrawString(_font, "Stack Action Cards", l.StackLabel, TEXT);
        spriteBatch.DrawString(_font, "Build Cards", l.BuildLabel, TEXT);

        // Sanity
        string text = $"Sanity: {sanityManager.Sanity}";
        var size = _font.MeasureString(text);
        spriteBatch.DrawString( _font, text,new Vector2(viewportAdapter.VirtualWidth - size.X - 20, 20),TEXT);

        // Decks
        DrawSprite("deck", l.MapDeck);
        DrawSprite("deck", l.StackDeck);
        DrawSprite("deck", l.BuildDeck);

        // Active card animation
        if (cardManager.ActiveCard != null)
        {
            DrawCard(cardManager.ActiveCard, new Rectangle(
                (int)cardManager.ActiveCardStartPos.X + 20,
                (int)cardManager.ActiveCardStartPos.Y,
                l.ActiveCard.Width, l.ActiveCard.Height
            ));
        }

        // TOP 3 CARDS 
        for (int i = 0; i < cardManager.Top3Cards.Count; i++)
        {
            var rect = l.Top3Rects[i];
            var card = cardManager.Top3Cards[i];
            DrawCard(card, rect);

            int cost = cardManager.DiscountActive ? Math.Max(card.Cost - 1, 0) : card.Cost;

            // Cost text
            string costText = $"Cost: {cost}";
            var size1 = _font.MeasureString(costText);
            int costY = rect.Bottom + 5;
            spriteBatch.DrawString(_font, costText,
                new Vector2(rect.Center.X - size1.X / 2, costY), TEXT);

            // Buy button
            if (cardManager.DrawTop3Free || i == 0)
            {
                var btn = l.BuyButtons[i];
                btn.Y = costY + 40;
                DrawButton(btn, "BUY");
            }
        }

        // Slots
        DrawSlots(cardManager.MapSlots, l.MapSlots, CardType.MapAction);
        DrawSlots(cardManager.StackSlots, l.StackSlots, CardType.StackAction);
        DrawSlots(cardManager.BuildSlots, l.BuildSlots, CardType.Build);

        spriteBatch.End();
    }

    // -----------------------
    // DRAW HELPERS
    // -----------------------
    private void DrawSlots(List<CardSlot> slots, List<Rectangle> rects, CardType type)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            spriteBatch.Draw(_dummy, rects[i], SLOT);

            if (slots[i].Card != null)
            {
                DrawCard(slots[i].Card, rects[i]);

                if (type == CardType.StackAction)
                    DrawButton(GetBottom(rects[i], 0), "USE");

                DrawButton(GetBottom(rects[i], 1), "DISCARD");
            }
        }
    }

    private void DrawCard(Card card, Rectangle rect)
        => spriteBatch.Draw(_cardTexture, rect, _cardRegions[card.SpriteName], Color.White);

    private void DrawSprite(string name, Rectangle rect)
        => spriteBatch.Draw(_cardTexture, rect, _cardRegions[name], Color.White);
    private void DrawButton(Rectangle rect, string text)
    {
        spriteBatch.Draw(_dummy, rect, BTN);

        var size = _font.MeasureString(text);
        float scale = Math.Min(rect.Width / size.X * 0.7f, rect.Height / size.Y * 0.7f);

        var pos = new Vector2(rect.Center.X - (size.X * scale) / 2,
                              rect.Center.Y - (size.Y * scale) / 2);

        spriteBatch.DrawString(_font, text, pos, BTN_TEXT, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void HandleSlotButtons(List<CardSlot> slots, List<Rectangle> rects, Vector2 mouse, CardType type)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            if (slots[i].Card == null) continue;

            if (type == CardType.StackAction &&
                GetBottom(rects[i], 0).Contains(mouse))
            {
                cardManager.UseCard(slots[i].Card);
                return;
            }

            if (GetBottom(rects[i], 1).Contains(mouse))
            {
                slots[i].Card = null;
                return;
            }
        }
    }

    // -----------------------
    // RESPONSIVE LAYOUT
    // -----------------------
    private dynamic GetLayout()
    {
        float w = viewportAdapter.VirtualWidth;
        float h = viewportAdapter.VirtualHeight;

        int cardW = (int)(w * 0.09f);
        int cardH = (int)(h * 0.2f);

        int spacingX = (int)(w * 0.04f);
        int spacingY = (int)(h * 0.06f); // vertical distance between deck rows
        int startY = (int)(h * 0.18f);
        int leftX = spacingX;

        Rectangle mapDeck = new(leftX, startY, cardW, cardH);
        Rectangle stackDeck = new(leftX, startY + cardH + spacingY, cardW, cardH);
        Rectangle buildDeck = new(leftX, startY + 2 * (cardH + spacingY), cardW, cardH);

        int rightX = (int)(w * 0.75f);

        var mapSlots = CreateSlotRow(rightX, mapDeck.Y, cardW, cardH);
        var stackSlots = CreateSlotRow(rightX, stackDeck.Y, cardW, cardH);
        var buildSlots = CreateSlotRow(rightX, buildDeck.Y, cardW, cardH);

        int midStartX = buildDeck.Right + spacingX;

        List<Rectangle> top3 = new();
        List<Rectangle> buyBtns = new();

        for (int i = 0; i < 3; i++)
        {
            var r = new Rectangle(midStartX + i * (cardW + spacingX), buildDeck.Y, cardW, cardH);
            top3.Add(r);
            buyBtns.Add(new Rectangle(r.X, r.Bottom + 50, cardW, 35));
        }

        return new
        {
            TitlePos = new Vector2(w * 0.35f, h * 0.05f),
            MapLabel = new Vector2(mapDeck.X, mapDeck.Y - 35),
            StackLabel = new Vector2(stackDeck.X, stackDeck.Y - 35),
            BuildLabel = new Vector2(buildDeck.X, buildDeck.Y - 35),

            MapDeck = mapDeck,
            StackDeck = stackDeck,
            BuildDeck = buildDeck,

            ActiveCard = new Rectangle(mapDeck.Right + 20, mapDeck.Y, cardW, cardH),

            Top3Rects = top3,
            BuyButtons = buyBtns,

            MapSlots = mapSlots,
            StackSlots = stackSlots,
            BuildSlots = buildSlots
        };
    }

    private List<Rectangle> CreateSlotRow(int x, int y, int w, int h)
    {
        return new()
        {
            new Rectangle(x, y, w, h),
            new Rectangle(x + w + 40, y, w, h)
        };
    }

    private Rectangle GetBottom(Rectangle r, int i)
        => new(r.X + i * (r.Width / 2 + 10), r.Bottom + 5, r.Width / 2 - 10, 30);
}