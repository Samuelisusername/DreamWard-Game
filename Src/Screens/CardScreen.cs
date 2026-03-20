using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.ViewportAdapters;
using MonoGame.Extended.Input;

namespace Gamelab.Screens;

public class CardScreen : GamelabGameScreen
{
    private CardManager _manager;
    private SanityManger _sanity;

    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private Dictionary<string, Rectangle> _cardRegions;

    // 🎨 Colors
    private readonly Color BG = new(0x10, 0x52, 0x18);
    private readonly Color SLOT = new(0x0F, 0x4A, 0x18);
    private readonly Color TEXT = new(0xD6, 0xC9, 0x90);
    private readonly Color BTN = new(0x1F, 0x42, 0x21);
    private readonly Color BTN_TEXT = new(0xEB, 0xE0, 0xBE);

    public CardScreen(GamelabGame game) : base(game)
    {
        _manager = new CardManager();
        _sanity = new SanityManger(null, null);
    }

    // -----------------------
    // LOAD
    // -----------------------
    public override void LoadContent()
    {
        _font = Content.Load<SpriteFont>("font");

        _dummy = new Texture2D(GraphicsDevice, 1, 1);
        _dummy.SetData(new[] { Color.White });

        _cardTexture = Content.Load<Texture2D>("Sprites/cards1");

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
        _manager.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (mouse.WasButtonPressed(MouseButton.Left))
        {
            var pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            HandleClick(pos);
        }
    }

    // -----------------------
    // CLICK HANDLING
    // -----------------------
    private void HandleClick(Vector2 pos)
    {
        var l = GetLayout();

        if (l.MapDeck.Contains(pos)) { _manager.DrawMapAC(l.MapDeck.Location.ToVector2()); return; }
        if (l.StackDeck.Contains(pos)) { _manager.DrawStackAC(l.StackDeck.Location.ToVector2()); return; }

        for (int i = 0; i < l.Top3Rects.Count; i++)
        {
            if (l.BuyButtons[i].Contains(pos) &&
                (_manager.DrawTop3Free || i == 0) &&
                !_manager.BuildSlots.All(s => s.IsFilled))
            {
                _manager.BuyTopCard(_sanity, i);
                return;
            }
        }

        HandleSlotButtons(_manager.MapSlots, l.MapSlots, pos);
        HandleSlotButtons(_manager.StackSlots, l.StackSlots, pos);
        HandleSlotButtons(_manager.BuildSlots, l.BuildSlots, pos);
    }

    // -----------------------
    // DRAW
    // -----------------------
    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BG);
        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix());

        var l = GetLayout();

        // Titles
        spriteBatch.DrawString(_font, "Action & Build Cards", l.TitlePos, TEXT);
        spriteBatch.DrawString(_font, "Map Action Cards", l.MapLabel, TEXT);
        spriteBatch.DrawString(_font, "Stack Action Cards", l.StackLabel, TEXT);
        spriteBatch.DrawString(_font, "Build Cards", l.BuildLabel, TEXT);

        // Sanity
        spriteBatch.DrawString(_font, $"Sanity: {_sanity.Sanity}", new Vector2(20, 20), TEXT);

        // Decks
        DrawSprite("deck", l.MapDeck);
        DrawSprite("deck", l.StackDeck);
        DrawSprite("deck", l.BuildDeck);

        // Active card animation
        if (_manager.ActiveCard != null)
        {
            DrawCard(_manager.ActiveCard, new Rectangle(
                (int)_manager.ActiveCardStartPos.X + 20,
                (int)_manager.ActiveCardStartPos.Y,
                l.ActiveCard.Width, l.ActiveCard.Height
            ));
        }

        // 🔥 TOP 3 CARDS (with cost spacing)
        for (int i = 0; i < _manager.Top3Cards.Count; i++)
        {
            var rect = l.Top3Rects[i];
            var card = _manager.Top3Cards[i];
            DrawCard(card, rect);

            int cost = _manager.DiscountActive ? Math.Max(card.Cost - 1, 0) : card.Cost;

            // Cost text (closer to card)
            string costText = $"Cost: {cost}";
            var size = _font.MeasureString(costText);
            int costY = rect.Bottom + 5;
            spriteBatch.DrawString(_font, costText,
                new Vector2(rect.Center.X - size.X / 2, costY), TEXT);

            // Buy button (far from cost)
            if (_manager.DrawTop3Free || i == 0)
            {
                var btn = l.BuyButtons[i];
                btn.Y = costY + 40;
                DrawButton(btn, "BUY");
            }
        }

        // Slots
        DrawSlots(_manager.MapSlots, l.MapSlots);
        DrawSlots(_manager.StackSlots, l.StackSlots);
        DrawSlots(_manager.BuildSlots, l.BuildSlots);

        spriteBatch.End();
    }

    // -----------------------
    // DRAW HELPERS
    // -----------------------
    private void DrawSlots(List<CardSlot> slots, List<Rectangle> rects)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            spriteBatch.Draw(_dummy, rects[i], SLOT);

            if (slots[i].Card != null)
            {
                DrawCard(slots[i].Card, rects[i]);
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

    private void HandleSlotButtons(List<CardSlot> slots, List<Rectangle> rects, Vector2 mouse)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            if (slots[i].Card == null) continue;

            if (GetBottom(rects[i], 0).Contains(mouse)) { _manager.UseCard(slots[i].Card); return; }
            if (GetBottom(rects[i], 1).Contains(mouse)) { slots[i].Card = null; return; }
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