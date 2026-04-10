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
    private GamePhaseManager GamePhaseManager => GamelabGame.GamePhaseManager;

    private Rectangle _toMapScreenBtn;
    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private Dictionary<string, Rectangle> _cardRegions;
    private Dictionary<string, string> _cardDescriptions;
    private Rectangle _currentBuyBtnRect;
    private int _selectedTop3Index = -1;
    private int _selectedSlotIndex = -1;
    private DateTime _lastStackClickTime = DateTime.MinValue;
    private const double DoubleClickMs = 350;
    private readonly Color BG          = new(0x18, 0x3A, 0x37);
    private readonly Color SLOT_EMPTY  = new(0x08, 0x30, 0x2B);
    private readonly Color SLOT_SEL    = new(0x1E, 0x56, 0x4E);
    private readonly Color PANEL       = new(0x0C, 0x22, 0x1E);
    private readonly Color TEXT        = new(0xBC, 0xB6, 0x9D);
    private readonly Color HEADING     = new(0xD4, 0xAA, 0x40);
    private readonly Color HINT        = new(0x7A, 0xB8, 0xA8);
    private readonly Color BTN         = new(0x08, 0x30, 0x2B);
    private readonly Color BTN_BUY     = new(0x14, 0x5E, 0x30);
    private readonly Color BTN_DISCARD = new(0x6A, 0x1A, 0x1A);
    private readonly Color BTN_TEXT    = new(0xBC, 0xB6, 0x9D);
    private readonly Color OUTLINE_SEL = new(0xD4, 0xA8, 0x17);
    private readonly Color OUTLINE_DBL = new(0x60, 0xC8, 0xFF);
    public CardScreen(GamelabGame game) : base(game) { }

    public override void LoadContent()
    {
        AudioManager.LoadAllSounds(Game.Content, "Sounds/action_cards");
        AudioManager.LoadSound(Game.Content, "coinsplash", "Sounds/action_cards/coinsplash");
        AudioManager.LoadSound(Game.Content, "dragonflap", "Sounds/dragonflap");

        _font = Content.Load<SpriteFont>("font");

        _dummy = new Texture2D(GraphicsDevice, 1, 1);
        _dummy.SetData(new[] { Color.White });

        _toMapScreenBtn = new Rectangle(15, 15, 130, 44);

        _cardTexture = Content.Load<Texture2D>("Sprites/cards");

        _cardRegions = new()
        {
            { "sniper",       new(0,    0, 128, 128) },
            { "aoe",          new(0,  128, 128, 128) },
            { "dreamcatcher", new(0,  256, 128, 128) },
            { "fence",        new(0,  384, 128, 128) },
            { "meadow",       new(0,  512, 128, 128) },
            { "shuffle",      new(0,  640, 128, 128) },
            { "discount",     new(0,  768, 128, 128) },
            { "top3",         new(0,  896, 128, 128) },
            { "discard",      new(0, 1024, 128, 128) },
            { "deck",         new(0, 1152, 128, 128) },
            { "polymorph",    new(0, 1280, 128, 128) },
            { "move",         new(0, 1408, 128, 128) },
            { "destroy",      new(0, 1536, 128, 128) },
        };

        _cardDescriptions = new()
        {
            // Build cards
            { "sniper",       "Sniper Tower\nTargets 1 enemy at a time\nShort range but high damage" },
            { "aoe",          "Area of Effect Tower\nTargets all the enemies in its range\nLong range but less damage" },
            { "dreamcatcher", "Dreamcatcher\nActs as a wall to blocks the enemies\nIncluding flying ones" },
            { "fence",        "Fence\nBlock all ground enemies" },
            { "meadow",       "Meadow\nCan move Sheeps here\nHelps increase sanity" },
            // Stack action cards
            { "shuffle",      "Shuffle\nShuffles the Build Deck" },
            { "discount",     "Discount\nNext Card to purchase gets\na discount of 1 Sanity" },
            { "top3",         "Top3\nBuy any of the Top 3 cards" },
            { "discard",      "Discard\nDiscard the top card" },
            // Map action cards
            { "polymorph",    "Polymorph\nTransforms the sheep into rats\nMonsters don't attack the sheep" },
            { "move",         "Move\nRelocate any Build Structure" },
            { "destroy",      "Destroy\nDestroy any Build Structure" },
        };
    }

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

        if (keyboard.WasKeyReleased(Keys.P))
        {
            Game.StartScreenInstance ??= new StartScreen(Game);
            Game.SwitchToScreen(Game.StartScreenInstance);
            return;
        }
        if(keyboard.WasKeyReleased(Keys.Escape))
        {
            Game.SwitchToScreen(Game.MapScreenInstance);
            AudioManager.Play("dragonflap");
            return;
        }

#if DEBUG
        // Build cards
        if (keyboard.WasKeyReleased(Keys.D1)) ForceSpawnCard("sniper",       cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D2)) ForceSpawnCard("aoe",          cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D3)) ForceSpawnCard("meadow",       cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D4)) ForceSpawnCard("dreamcatcher", cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D5)) ForceSpawnCard("fence",        cardManager.BuildSlots);
        // Map action cards
        if (keyboard.WasKeyReleased(Keys.D6)) ForceSpawnCard("polymorph",    cardManager.MapSlots);
        if (keyboard.WasKeyReleased(Keys.D7)) ForceSpawnCard("destroy",      cardManager.MapSlots);
        if (keyboard.WasKeyReleased(Keys.D8)) ForceSpawnCard("move",         cardManager.MapSlots);
        // Stack action cards
        if (keyboard.WasKeyReleased(Keys.D9)) ForceSpawnCard("shuffle",      cardManager.StackSlots);
        if (keyboard.WasKeyReleased(Keys.D0)) ForceSpawnCard("discount",     cardManager.StackSlots);
        if (keyboard.WasKeyReleased(Keys.Q))  ForceSpawnCard("top3",         cardManager.StackSlots);
        if (keyboard.WasKeyReleased(Keys.W))  ForceSpawnCard("discard",      cardManager.StackSlots);
#endif
    }

    private void ForceSpawnCard(string spriteName, List<CardSlot> targetSlots)
    {
        var emptySlot = targetSlots.FirstOrDefault(s => s.Card == null);
        if (emptySlot == null) return;
        var card = cardManager.GetCardBySprite(spriteName);
        if (card == null) return;
        if (card.Cost > 0 && !sanityManager.TryDeductSanity(card.Cost)) return;
        emptySlot.Card = card;
    }

    // CLICK HANDLING
    private void HandleClick(Vector2 pos)
    {
        var l = GetLayout();

        // ── To Map button ───────────────────────────────────────
        if (_toMapScreenBtn.Contains(pos))
        {
            Game.SwitchToScreen(Game.MapScreenInstance);
            AudioManager.Play("dragonflap");
            return;
        }

        // ── Compute SAME Y positions as Draw() ──────────────────
        float row1Y = l.Row1LabelY + 80;
        float row2Y = l.Row2LabelY + 80;
        float row3Y = l.Row3LabelY + 70;

        // Adjusted rects click
        Rectangle mapCardRect   = l.MapCard;        mapCardRect.Y   = (int)row1Y;
        Rectangle stackCardRect = l.StackCard;      stackCardRect.Y = (int)row1Y;

        // ── Stack card: double-click ────────────────────────────
        if (cardManager.StackSlots[0].Card != null && stackCardRect.Contains(pos))
        {
            var now = DateTime.UtcNow;
            bool isDouble = (now - _lastStackClickTime).TotalMilliseconds <= DoubleClickMs;
            _lastStackClickTime = isDouble ? DateTime.MinValue : now;

            if (isDouble)
                cardManager.UseCard(cardManager.StackSlots[0].Card);

            return;
        }

        // ── Map card ────────────────────────────────────
        if (mapCardRect.Contains(pos)) return;

        // ── BUY BUTTON ──────────────────────────────
        if (_selectedTop3Index >= 0)
        {
            bool canBuy =
                (_selectedTop3Index == 0) ||
                cardManager.DrawTop3Free;

            if (canBuy && _currentBuyBtnRect.Contains(pos))
            {
                if (!cardManager.BuildSlots.All(s => s.IsFilled))
                {
                    cardManager.BuyTopCard(sanityManager, _selectedTop3Index);
                    AudioManager.Play("coinsplash");
                    _selectedTop3Index = -1;
                }
                return;
            }
        }

        // ── Top-3 cards: select ─────────────────────────────────
        for (int i = 0; i < l.Top3Rects.Count; i++)
        {
            var rect = l.Top3Rects[i];
            rect.Y = (int)row2Y; 

            if (rect.Contains(pos))
            {
                int logicalIndex = 2 - i; 

                _selectedTop3Index = (_selectedTop3Index == logicalIndex) ? -1 : logicalIndex;
                _selectedSlotIndex = -1;
                return;
            }
        }

        // ── Build slots: select ─────────────────────────────────
        for (int i = 0; i < l.BuildSlots.Count; i++)
        {
            var slotRect = l.BuildSlots[i];
            slotRect.Y = (int)row3Y;  

            if (slotRect.Contains(pos))
            {
                _selectedSlotIndex = (_selectedSlotIndex == i) ? -1 : i;
                _selectedTop3Index = -1;
                return;
            }
        }

        // ── Reset ───────────────────────────────────────────────
        _selectedTop3Index = -1;
        _selectedSlotIndex = -1;
    }

    // DRAW
    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GraphicsDevice.Clear(BG);
        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix());

        var l = GetLayout();
        float W = viewportAdapter.VirtualWidth;

        // ── Top bar ───────────────────────────────────────────────────────────
        DrawButton(_toMapScreenBtn, "< Map");

        float topTitleY = l.TopBarTextY;
        DrawCenteredText("CARDS", W / 2f, topTitleY, TEXT);

        float topInfoY = topTitleY;

        string sanityStr = $"Sanity: {sanityManager.Sanity}";
        string dayStr    = $"Day: {GamePhaseManager.DayCount}/10";
        float rightX     = W - 20;

        DrawRightText(sanityStr, rightX, topInfoY, TEXT);
        DrawRightText(dayStr, rightX, topInfoY + 32, TEXT);

        // Divider
        spriteBatch.Draw(_dummy, new Rectangle(0, l.DividerY, (int)W, 2), new Color(0x2A, 0x5A, 0x50));

        // ── ROW 1 ─────────────────────────────────────────────────────────────
        DrawLabel("Map Action Card", l.MapCard.X, l.Row1LabelY);
        DrawLabel("Stack Action Card", l.StackCard.X, l.Row1LabelY);

        float row1Y = l.Row1LabelY + 80;

        var mapCardRect  = l.MapCard;        mapCardRect.Y  = (int)row1Y;
        var mapInfoRect  = l.MapInfoPanel;   mapInfoRect.Y  = (int)row1Y;
        var stackCardRect= l.StackCard;      stackCardRect.Y= (int)row1Y;
        var stackInfoRect= l.StackInfoPanel; stackInfoRect.Y= (int)row1Y;

        // Map card
        if (cardManager.MapSlots[0].Card != null)
        {
            DrawCard(cardManager.MapSlots[0].Card, mapCardRect);
            DrawInfoPanel(cardManager.MapSlots[0].Card, mapInfoRect, "Used on the map screen");
        }
        else
            DrawEmptyCardSlot(mapCardRect, "Card Used");

        // Stack card
        if (cardManager.StackSlots[0].Card != null)
        {
            DrawCard(cardManager.StackSlots[0].Card, stackCardRect);
            DrawInfoPanel(cardManager.StackSlots[0].Card, stackInfoRect, "Double click to apply");
        }
        else
            DrawEmptyCardSlot(stackCardRect, "Card Used");

        // Divider
        spriteBatch.Draw(_dummy, new Rectangle(0, l.Row2DividerY, (int)W, 2), new Color(0x2A, 0x5A, 0x50));

        // ── ROW 2 ─────────────────────────────────────────────────────────────
        DrawLabel("Build Cards", l.BuildDeck.X, l.Row2LabelY);

        float row2Y = l.Row2LabelY + 80;

        var deckRect = l.BuildDeck;
        deckRect.Y = (int)row2Y;
        DrawSprite("deck", deckRect);

        // Top 3
        if (cardManager.Top3Cards.Count > 0)
        {
            for (int i = 0; i < cardManager.Top3Cards.Count; i++)
            {
                var card = cardManager.Top3Cards[i];

                var rect = l.Top3Rects[2 - i];
                rect.Y = (int)row2Y;

                bool isSel = (_selectedTop3Index == i);

                DrawCard(card, rect);

                if (isSel)
                {
                    var panelRect = l.Top3InfoPanels[0];
                    panelRect.Y = (int)row2Y;

                    var buyBtn = l.BuyButtons[0];

                    DrawTop3Panel(card, panelRect, buyBtn);
                }
            }
        }

        // Divider
        spriteBatch.Draw(_dummy, new Rectangle(0, l.Row3DividerY, (int)W, 2), new Color(0x2A, 0x5A, 0x50));

        // ── ROW 3 ─────────────────────────────────────────────────────────────
        DrawLabel("Build Slots", l.BuildSlots[0].X, l.Row3LabelY);

        float row3Y = l.Row3LabelY + 70;

        for (int i = 0; i < cardManager.BuildSlots.Count; i++)
        {
            var slotRect = l.BuildSlots[i];
            slotRect.Y = (int)row3Y;

            var infoRect = l.SlotInfoPanels[i];
            infoRect.Y = (int)row3Y;

            bool isSel = (_selectedSlotIndex == i);
            var slot   = cardManager.BuildSlots[i];

            spriteBatch.Draw(_dummy, slotRect, isSel ? SLOT_SEL : SLOT_EMPTY);

            if (slot.Card != null)
            {
                DrawCard(slot.Card, slotRect);  
                DrawInfoPanel(slot.Card, infoRect);
            }
            else
            {
                DrawEmptyCardSlot(slotRect, "Empty\nSlot");
            }
        }

        spriteBatch.End();
    }

    // DRAW HELPERS
    private Rectangle GetInfoPanelRect(Card card, int x, int y, int width)
    {
        float lineSpacing = 20;
        //float textScale = 0.65f;
        int topPadding = 6;
        int bottomPadding = 6;

        int lineCount = 0;
        if (_cardDescriptions.TryGetValue(card.SpriteName, out var desc))
            lineCount = desc.Split('\n').Length;

        // Add 1 extra line for hint
        int panelHeight = (int)(topPadding + lineCount * lineSpacing + lineSpacing + bottomPadding);

        return new Rectangle(x, y, width, panelHeight);
    }

    private void DrawInfoPanel(Card card, Rectangle panel, string hint = null)
    {
        spriteBatch.Draw(_dummy, panel, BG);

        float ty = panel.Y + 6;
        float lx = panel.X + 6;
        float lineSpacing = 20; 
        float textScale = 0.65f;

        if (_cardDescriptions.TryGetValue(card.SpriteName, out var desc))
        {
            foreach (var line in desc.Split('\n'))
            {
                spriteBatch.DrawString(_font, SafeString(line), new Vector2(lx, ty), TEXT, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                ty += lineSpacing;
            }
        }

        if (hint != null)
            spriteBatch.DrawString(_font, SafeString(hint), new Vector2(lx, ty), HINT, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawTop3Panel(Card card, Rectangle panel, Rectangle buyBtn)
    {
        spriteBatch.Draw(_dummy, panel, BG);

        float ty = panel.Y + 6;
        float lx = panel.X + 6;
        float lineSpacing = 20;
        float textScale = 0.65f;

        // Description
        if (_cardDescriptions.TryGetValue(card.SpriteName, out var desc))
        {
            foreach (var line in desc.Split('\n'))
            {
                spriteBatch.DrawString(_font, SafeString(line),
                    new Vector2(lx, ty), TEXT,
                    0f, Vector2.Zero, textScale,
                    SpriteEffects.None, 0f);
                ty += lineSpacing;
            }
        }

        int cost = cardManager.DiscountActive
            ? Math.Max(card.Cost - 1, 0)
            : card.Cost;

        string costLabel = cardManager.DiscountActive
            ? $"Cost: {cost} (discount)"
            : $"Cost: {cost}";

        spriteBatch.DrawString(_font, SafeString(costLabel),
            new Vector2(lx, ty), TEXT,
            0f, Vector2.Zero, textScale,
            SpriteEffects.None, 0f);

        ty += lineSpacing + 8;

        bool canBuy =
            (_selectedTop3Index == 0) || 
            cardManager.DrawTop3Free;       

        if (canBuy)
        {
            _currentBuyBtnRect = new Rectangle(
            buyBtn.X,
            (int)ty,
            buyBtn.Width,
            buyBtn.Height
        );

        DrawButtonColored(_currentBuyBtnRect, "BUY", BTN_BUY);
        }
    }

    private void DrawCard(Card card, Rectangle rect)
        => spriteBatch.Draw(_cardTexture, rect, _cardRegions[card.SpriteName], Color.White);

    private void DrawSprite(string name, Rectangle rect)
        => spriteBatch.Draw(_cardTexture, rect, _cardRegions[name], Color.White);

    private void DrawEmptyCardSlot(Rectangle rect, string text)
    {
        spriteBatch.Draw(_dummy, rect, SLOT_EMPTY);
        float ty = rect.Y + 8;
        foreach (var line in text.Split('\n'))
        {
            spriteBatch.DrawString(_font, line, new Vector2(rect.X + 8, ty),
                new Color(0x50, 0x80, 0x70), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            ty += 20;
        }
    }

    private void DrawButton(Rectangle rect, string label) => DrawButtonColored(rect, label, BTN);

    private void DrawButtonColored(Rectangle rect, string label, Color bg)
    {
        spriteBatch.Draw(_dummy, rect, bg);
        var safe = SafeString(label);
        var sz   = _font.MeasureString(safe);
        float sc = Math.Min(rect.Width / sz.X * 0.76f, rect.Height / sz.Y * 0.76f);
        var pos  = new Vector2(rect.Center.X - sz.X * sc * 0.5f,
                               rect.Center.Y - sz.Y * sc * 0.5f);
        spriteBatch.DrawString(_font, safe, pos, BTN_TEXT,
            0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
    }

    private void DrawLabel(string text, float x, float y)
        => spriteBatch.DrawString(_font, SafeString(text), new Vector2(x, y), TEXT);

    private void DrawCenteredText(string text, float cx, float y, Color col)
    {
        var safe = SafeString(text);
        var sz = _font.MeasureString(safe);
        spriteBatch.DrawString(_font, safe, new Vector2(cx - sz.X * 0.5f, y), col);
    }

    private void DrawRightText(string text, float rightEdge, float y, Color col)
    {
        var safe = SafeString(text);
        var sz = _font.MeasureString(safe);
        spriteBatch.DrawString(_font, safe, new Vector2(rightEdge - sz.X, y), col);
    }

    /// Strips any character the SpriteFont cannot render so we never crash on
    /// unexpected glyphs (unicode arrows, em-dashes, etc.).
    private string SafeString(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            try { _font.MeasureString(c.ToString()); sb.Append(c); }
            catch { /* skip unsupported glyph */ }
        }
        return sb.ToString();
    }

    // LAYOUT
    private dynamic GetLayout()
    {
        float W = viewportAdapter.VirtualWidth;
        float H = viewportAdapter.VirtualHeight;

        int pad     = 16;
        int cW      = (int)(W * 0.08f * 1.5f);
        int cH      = cW;
        int infW    = (int)(W * 0.12f);       
        int infH    = cH;                      

        int top3IH  = cH / 2 + 20; 

        // Top bar
        float topBarTextY = 20f;
        int dividerY      = 100;

        // Row 1: Map + Stack
        float row1LabelY = dividerY + 12f;
        float row1Y      = row1LabelY + 22f;

        Rectangle mapCard       = new(pad, (int)row1Y, cW, cH);
        Rectangle mapInfoPanel  = new(mapCard.Right + pad, (int)row1Y, infW, infH);

        int stackX              = (int)(W * 0.52f);
        Rectangle stackCard     = new(stackX, (int)row1Y, cW, cH);
        Rectangle stackInfoPanel= new(stackCard.Right + pad, (int)row1Y, infW, infH);

        int row2DividerY = (int)(row1Y + cH + pad + 50);

        // Row 2: Build deck + top3
        float row2LabelY = row2DividerY + 8f;
        float row2Y      = row2LabelY + 18f;

        Rectangle buildDeck = new(pad, (int)row2Y, cW, cH);

        var top3Rects      = new List<Rectangle>();
        int top3StartX     = buildDeck.Right + pad * 2;
        for (int i = 0; i < 3; i++)
        {
            top3Rects.Add(new Rectangle(top3StartX + i * (cW + pad), (int)row2Y, cW, cH));
        }

        var top3InfoPanels = new List<Rectangle> { new(top3Rects[2].Right + pad, (int)row2Y, infW, cH) };
        var buyButtons     = new List<Rectangle> { new(top3InfoPanels[0].X + 6, top3InfoPanels[0].Bottom - 26, top3InfoPanels[0].Width - 12, 24) };

        int row3DividerY = (int)(row2Y + cH + pad + 60);

        // Row 3: Build slots
        float row3LabelY = row3DividerY + 6f;
        float row3Y      = row3LabelY + 18f;

        int slotCount = cardManager.BuildSlots.Count;
        int slotStride = (int)((W - pad) / slotCount);

        var buildSlots     = new List<Rectangle>();
        var slotInfoPanels = new List<Rectangle>();

        for (int i = 0; i < slotCount; i++)
        {
            int sx = pad + i * slotStride;
            var slot = new Rectangle(sx, (int)row3Y, cW, cH);
            buildSlots.Add(slot);
            slotInfoPanels.Add(new Rectangle(slot.Right + pad / 2, (int)row3Y, infW, infH));
        }

        return new
        {
            TopBarTextY  = topBarTextY,
            DividerY     = dividerY,
            Row1LabelY   = row1LabelY,
            Row2DividerY = row2DividerY,
            Row2LabelY   = row2LabelY,
            Row3DividerY = row3DividerY,
            Row3LabelY   = row3LabelY,

            MapCard       = mapCard,
            MapInfoPanel  = mapInfoPanel,
            StackCard     = stackCard,
            StackInfoPanel= stackInfoPanel,

            BuildDeck      = buildDeck,
            Top3Rects      = top3Rects,
            Top3InfoPanels = top3InfoPanels,
            BuyButtons     = buyButtons,

            BuildSlots     = buildSlots,
            SlotInfoPanels = slotInfoPanels,
        };
    }
}