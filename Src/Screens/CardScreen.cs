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
    private SanityManager       sanityManager   => GamelabGame.SanityManager;
    private CardManager         cardManager     => GamelabGame.CardManager;
    private GamePhaseManager    GamePhaseManager=> GamelabGame.GamePhaseManager;

    private SpriteFont  _font;
    private Texture2D   _dummy, _cardTexture;

    private readonly Rectangle _toMapBtn = new(15, 15, 163, 63);
    private int  _selectedTop3Index = -1;
    private int  _selectedSlotIndex = -1;
    private DateTime _lastStackClick = DateTime.MinValue;
    private Vector2 _mouseDownPos = new Vector2(-1, -1);
    private const double DoubleClickMs = 350;
    private Rectangle _buyBtnRect;

    // Layout cache — rebuilt each frame inside BuildLayout()
    private Layout _l;
    private Texture2D _interfaceTexture;
    private Texture2D _dayInterfaceTexture;
    private Texture2D InterfaceTexture => GamePhaseManager.IsDay ? _dayInterfaceTexture : _interfaceTexture;
    private static readonly Rectangle BtnNormal  = new(0, 112, 80, 32);
    private static readonly Rectangle BtnPressed = new(0, 144, 80, 32);
    private bool _buyBtnPressed = false;
    private bool _toMapBtnPressed = false;

    private readonly Color BG   = new(0x85, 0x8f, 0x60);
    private readonly Color TEXT = new(0x29, 0x24, 0x10);
    private readonly Color BTN  = new(0x73, 0x7c, 0x50);
    private readonly Color HINT  = new(0x19,0x42,0x36);

    // All positioned rects live here so Draw and HandleClick share them
    private struct Layout
    {
        public Rectangle MapCard, MapInfo, StackCard, StackInfo, BuildDeck;
        public List<Rectangle> Top3, Top3Info, BuyBtns, BuildSlots, SlotInfo;
        public int DividerY, Row2DividerY, Row3DividerY;
        public float TopBarTextY, Row1LabelY, Row2LabelY, Row3LabelY;
    }

    private static readonly Dictionary<string, Rectangle> CardRegions = new()
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
        { "void",         new(0, 1664, 128, 128) },
        { "spikes",       new(0, 1792, 128, 128) },
        { "decoy",        new(0, 1920, 128, 128) },
        { "buff",         new(0, 2048, 128, 128) },
    };

    private static readonly Dictionary<string, string> CardDescriptions = new()
    {
        { "sniper",       "Sniper Tower\nTargets 1 enemy at a time\nShort range but high damage" },
        { "aoe",          "Area of Effect Tower\nTargets all the enemies in its range\nLong range but less damage" },
        { "dreamcatcher", "Dreamcatcher\nActs as a wall to blocks the enemies\nIncluding flying ones" },
        { "fence",        "Fence\nBlock all ground enemies" },
        { "meadow",       "Meadow\nCan move Sheeps here\nHelps increase sanity" },
        { "void",         "Void\nBlock all ground enemies.\nOnce placed cannot\nbe removed." },
        { "spikes",       "Spikes\nInflicts damage on ground enemies." },
        { "decoy",        "Decoy\nActs like a tile with Sheep.\nLures enemies towards it." },
        { "buff",         "Buff\nRepels enemies." },
        { "shuffle",      "Shuffle\nShuffles the Build Deck" },
        { "discount",     "Discount\nNext Card to purchase gets\na discount of 1 Sanity" },
        { "top3",         "Top3\nBuy any of the Top 3 cards" },
        { "discard",      "Discard\nDiscard the top card" },
        { "polymorph",    "Polymorph\nTransforms the sheep into rats\nMonsters don't attack the sheep" },
        { "move",         "Move\nRelocate any Build Structure" },
        { "destroy",      "Destroy\nDestroy any Build Structure" },
    };

    public CardScreen(GamelabGame game) : base(game) { }

    public override void LoadContent()
    {
        AudioManager.LoadAllSounds(Game.Content, "Sounds/action_cards");
        AudioManager.LoadSound(Game.Content, "coinsplash", "Sounds/action_cards/coinsplash");
        AudioManager.LoadSound(Game.Content, "dragonflap", "Sounds/dragonflap");
        _font        = Content.Load<SpriteFont>("font");
        _cardTexture = Content.Load<Texture2D>("Sprites/cards");
        _interfaceTexture = Content.Load<Texture2D>("Sprites/interface");
        _dayInterfaceTexture = Content.Load<Texture2D>("Sprites/day_interface");
        _dummy       = new Texture2D(GraphicsDevice, 1, 1);
        _dummy.SetData(new[] { Color.White });
    }

    // ── Build layout once per frame ──────────────────────────────────────────
    private void BuildLayout()
    {
        float W  = viewportAdapter.VirtualWidth;
        int pad  = 8;
        int cW   = 128*2;
        int cH   = cW;
        int infW = (int)(W * 0.12f);

        float topBarTextY  = 10f;
        int   dividerY     = 100;
        float row1LabelY   = dividerY + 7f;
        float row1Y        = row1LabelY + 13f + 40f; // label + offset

        var mapCard    = new Rectangle(pad,           (int)row1Y, cW, cH);
        var mapInfo    = new Rectangle(mapCard.Right + pad, (int)row1Y, infW, cH);
        int stackX     = (int)(W * 0.52f);
        var stackCard  = new Rectangle(stackX,        (int)row1Y, cW, cH);
        var stackInfo  = new Rectangle(stackCard.Right + pad, (int)row1Y, infW, cH);

        int   row2DividerY = (int)(row1Y + cH + pad);
        float row2LabelY   = row2DividerY + 7f;
        float row2Y        = row2LabelY + 13f + 40f;

        var buildDeck  = new Rectangle(pad, (int)row2Y, cW, cH);
        var top3       = new List<Rectangle>();
        int top3StartX = buildDeck.Right + pad * 2;
        for (int i = 0; i < 3; i++)
            top3.Add(new Rectangle(top3StartX + i * (cW + pad), (int)row2Y, cW, cH));

        var top3Info  = new List<Rectangle> { new(top3[2].Right + pad, (int)row2Y, infW, cH) };
        int buyW = (top3Info[0].Width - 12);   // half the panel width instead of full
        int buyH = 48;                               // taller (was 24)
        var buyBtns = new List<Rectangle> { new(top3Info[0].X + 6, top3Info[0].Bottom - buyH - 6, buyW, buyH) };

        int   row3DividerY = (int)(row2Y + cH + pad);
        float row3LabelY   = row3DividerY + 7f;
        float row3Y        = row3LabelY + 13f + 40f;

        int slotCount  = cardManager.BuildSlots.Count;
        int slotStride = (int)((W - pad) / slotCount);
        var buildSlots = new List<Rectangle>();
        var slotInfo   = new List<Rectangle>();
        for (int i = 0; i < slotCount; i++)
        {
            var slot = new Rectangle(pad + i * slotStride, (int)row3Y, cW, cH);
            buildSlots.Add(slot);
            slotInfo.Add(new Rectangle(slot.Right + pad / 2, (int)row3Y, infW, cH));
        }

        _l = new Layout
        {
            TopBarTextY  = topBarTextY,
            DividerY     = dividerY,
            Row1LabelY   = row1LabelY,
            Row2DividerY = row2DividerY,
            Row2LabelY   = row2LabelY,
            Row3DividerY = row3DividerY,
            Row3LabelY   = row3LabelY,
            MapCard      = mapCard,   MapInfo   = mapInfo,
            StackCard    = stackCard, StackInfo = stackInfo,
            BuildDeck    = buildDeck,
            Top3         = top3,      Top3Info  = top3Info, BuyBtns = buyBtns,
            BuildSlots   = buildSlots, SlotInfo = slotInfo,
        };
    }

    // ── Update ───────────────────────────────────────────────────────────────
    protected override void Update(GameTime gameTime,
        KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads,
        MouseStateExtended mouse)
    {
        cardManager.Update(gameTime);
        BuildLayout();

        if (keyboard.WasKeyReleased(Keys.Escape))
        {
            Game.SwitchToScreen(Game.MapScreenInstance);
            return;
        }

        Vector2 mp = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
        GamelabGame.IsHovering =
            _toMapBtn.Contains(mp) ||
            (cardManager.StackSlots[0].Card != null && _l.StackCard.Contains(mp)) ||
            (_selectedTop3Index >= 0 && _buyBtnRect.Contains(mp)) ||
            _l.Top3.Any(r => r.Contains(mp));

        if (mouse.WasButtonPressed(MouseButton.Left))
        {

            var pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            _mouseDownPos = pos;
            _toMapBtnPressed = _toMapBtn.Contains(pos);
            _buyBtnPressed = _buyBtnRect.Contains(pos);

        }
        if (mouse.WasButtonReleased(MouseButton.Left))
        {

            _toMapBtnPressed = false; 
            _buyBtnPressed = false;
            HandleClick(viewportAdapter.PointToScreen(mouse.Position).ToVector2(), _mouseDownPos);
            _mouseDownPos = new Vector2(-1, -1);
        }
        if (keyboard.WasKeyReleased(Keys.P))
        {
            Game.StartScreenInstance ??= new StartScreen(Game);
            Game.SwitchToScreen(Game.StartScreenInstance);
            return;
        }

#if DEBUG
        // Build cards
        if (keyboard.WasKeyReleased(Keys.D1)) ForceSpawn("sniper",       cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D2)) ForceSpawn("aoe",          cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D3)) ForceSpawn("meadow",       cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D4)) ForceSpawn("dreamcatcher", cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.D5)) ForceSpawn("fence",        cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.E))  ForceSpawn("void",         cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.R))  ForceSpawn("spikes",       cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.T))  ForceSpawn("decoy",        cardManager.BuildSlots);
        if (keyboard.WasKeyReleased(Keys.Y))  ForceSpawn("buff",         cardManager.BuildSlots);
        // Map action cards
        if (keyboard.WasKeyReleased(Keys.D6)) ForceSpawn("polymorph",    cardManager.MapSlots);
        if (keyboard.WasKeyReleased(Keys.D7)) ForceSpawn("destroy",      cardManager.MapSlots);
        if (keyboard.WasKeyReleased(Keys.D8)) ForceSpawn("move",         cardManager.MapSlots);
        // Stack action cards
        if (keyboard.WasKeyReleased(Keys.D9)) ForceSpawn("shuffle",      cardManager.StackSlots);
        if (keyboard.WasKeyReleased(Keys.D0)) ForceSpawn("discount",     cardManager.StackSlots);
        if (keyboard.WasKeyReleased(Keys.Q))  ForceSpawn("top3",         cardManager.StackSlots);
        if (keyboard.WasKeyReleased(Keys.W))  ForceSpawn("discard",      cardManager.StackSlots);
#endif
    }

    private void ForceSpawn(string sprite, List<CardSlot> slots)
    {
        var slot = slots.FirstOrDefault(s => s.Card == null);
        if (slot == null) return;
        var card = cardManager.GetCardBySprite(sprite);
        if (card == null) return;
        if (card.Cost > 0 && !sanityManager.TryDeductSanity(card.Cost)) return;
        slot.Card = card;
    }

    // ── Click handling ────────────────────────────────────────────────────────
    private void HandleClick(Vector2 pos, Vector2 downpos)
    {
        if (_toMapBtn.Contains(pos) && _toMapBtn.Contains(downpos))
        {
            Game.SwitchToScreen(Game.MapScreenInstance);
            AudioManager.Play("dragonflap");
            return;
        }

        // Stack card double-click
        if (cardManager.StackSlots[0].Card != null && _l.StackCard.Contains(pos) && _l.StackCard.Contains(downpos))
        {
            var now = DateTime.UtcNow;
            bool isDouble = (now - _lastStackClick).TotalMilliseconds <= DoubleClickMs;
            _lastStackClick = isDouble ? DateTime.MinValue : now;
            if (isDouble) cardManager.UseCard(cardManager.StackSlots[0].Card);
            return;
        }

        if (_l.MapCard.Contains(pos)&&_l.MapCard.Contains(downpos)) return;

        // Buy button
        if (_selectedTop3Index >= 0 && (_selectedTop3Index == 0 || cardManager.DrawTop3Free)
            && _buyBtnRect.Contains(pos) && _buyBtnRect.Contains(downpos))
        {
            if (!cardManager.BuildSlots.All(s => s.IsFilled))
            {
                cardManager.BuyTopCard(sanityManager, _selectedTop3Index);
                AudioManager.Play("coinsplash");
                _selectedTop3Index = -1;
            }
            return;
        }
        // Top-3 selection
        for (int i = 0; i < _l.Top3.Count; i++)
        {
            if (_l.Top3[i].Contains(pos))
            {
                int logical = 2 - i;
                _selectedTop3Index = _selectedTop3Index == logical ? -1 : logical;
                _selectedSlotIndex = -1;
                return;
            }
        }

        // Build slot selection
        for (int i = 0; i < _l.BuildSlots.Count; i++)
        {
            if (_l.BuildSlots[i].Contains(pos) && _l.BuildSlots[i].Contains(downpos))
            {
                _selectedSlotIndex = _selectedSlotIndex == i ? -1 : i;
                _selectedTop3Index = -1;
                return;
            }
        }

        _selectedTop3Index = _selectedSlotIndex = -1;
    }

    // ── Draw ──────────────────────────────────────────────────────────────────
    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GraphicsDevice.Clear(BG);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewportAdapter.GetScaleMatrix());


        float W = viewportAdapter.VirtualWidth;
        var l = _l;

        // Top bar
        DrawButton(_toMapBtn, "< Map", _toMapBtnPressed);
        DrawCenteredText("CARD INVENTORY", W / 2f, l.TopBarTextY);
        DrawRightText($"Sanity: {sanityManager.Sanity}", W - 20, l.TopBarTextY);
        DrawRightText($"Day: {GamePhaseManager.DayCount}/10", W - 20, l.TopBarTextY + 40);
        Divider(l.DividerY, W);

        // Row 1
        DrawLabel("Map Action Card",   l.MapCard.X,   l.Row1LabelY);
        DrawLabel("Stack Action Card", l.StackCard.X, l.Row1LabelY);
        DrawCardOrEmpty(cardManager.MapSlots[0].Card,   l.MapCard,   l.MapInfo,   "Used on the map screen");
        DrawCardOrEmpty(cardManager.StackSlots[0].Card, l.StackCard, l.StackInfo, "Double click to apply");
        Divider(l.Row2DividerY, W);

        // Row 2
        DrawLabel("Build Cards", l.BuildDeck.X, l.Row2LabelY);
        DrawSprite("deck", l.BuildDeck);
        for (int i = 0; i < cardManager.Top3Cards.Count; i++)
        {
            var card = cardManager.Top3Cards[i];
            var rect = l.Top3[2 - i];
            DrawCard(card, rect);
            if (_selectedTop3Index == i)
                DrawTop3Panel(card, l.Top3Info[0], l.BuyBtns[0]);
        }
        Divider(l.Row3DividerY, W);

        // Row 3
        DrawLabel("Build Slots", l.BuildSlots[0].X, l.Row3LabelY);
        for (int i = 0; i < cardManager.BuildSlots.Count; i++)
        {
            var slot = cardManager.BuildSlots[i];
            spriteBatch.Draw(_dummy, l.BuildSlots[i], BG);
            if (slot.Card != null) { DrawCard(slot.Card, l.BuildSlots[i]); DrawInfoPanel(slot.Card, l.SlotInfo[i]); }
            else DrawEmptyCardSlot(l.BuildSlots[i], "Empty Slot");
        }

        spriteBatch.End();
    }

    // ── Draw helpers ──────────────────────────────────────────────────────────
    private void DrawCardOrEmpty(Card card, Rectangle cardRect, Rectangle infoRect, string hint)
    {
        if (card != null) { DrawCard(card, cardRect); DrawInfoPanel(card, infoRect, hint); }
        else DrawEmptyCardSlot(cardRect, "Card Used");
    }

    private void DrawInfoPanel(Card card, Rectangle panel, string hint = null)
    {
        spriteBatch.Draw(_dummy, panel, BG);
        float ty = panel.Y + 6, lx = panel.X + 6, ls = 25f, sc = 0.65f;
        if (CardDescriptions.TryGetValue(card.SpriteName, out var desc))
            foreach (var line in desc.Split('\n'))
            { spriteBatch.DrawString(_font, SafeStr(line), new(lx, ty), TEXT, 0, Vector2.Zero, sc, SpriteEffects.None, 0); ty += ls; }
        if (hint != null)
            spriteBatch.DrawString(_font, SafeStr(hint), new(lx, ty), HINT, 0, Vector2.Zero, 0.6f, SpriteEffects.None, 0);
    }
    private void DrawTop3Panel(Card card, Rectangle panel, Rectangle buyBtn)
    {
        spriteBatch.Draw(_dummy, panel, BG);
        float ty = panel.Y + 6, lx = panel.X + 6, ls = 25f, sc = 0.65f;
        if (CardDescriptions.TryGetValue(card.SpriteName, out var desc))
            foreach (var line in desc.Split('\n'))
            { spriteBatch.DrawString(_font, SafeStr(line), new(lx, ty), TEXT, 0, Vector2.Zero, sc, SpriteEffects.None, 0); ty += ls; }

        int cost = cardManager.DiscountActive ? Math.Max(card.Cost - 1, 0) : card.Cost;
        string costLabel = cardManager.DiscountActive ? $"Cost: {cost} (discount)" : $"Cost: {cost}";
        spriteBatch.DrawString(_font, SafeStr(costLabel), new(lx, ty), TEXT, 0, Vector2.Zero, sc, SpriteEffects.None, 0);
        ty += ls + 8;

        if (_selectedTop3Index == 0 || cardManager.DrawTop3Free)
        {
            _buyBtnRect = new Rectangle(buyBtn.X, (int)ty, buyBtn.Width, buyBtn.Height);
            DrawButton(_buyBtnRect, "BUY", _buyBtnPressed);
        }
    }

    private void DrawCard(Card card, Rectangle rect)
        => spriteBatch.Draw(_cardTexture, rect, CardRegions[card.SpriteName], Color.White);

    private void DrawSprite(string name, Rectangle rect)
        => spriteBatch.Draw(_cardTexture, rect, CardRegions[name], Color.White);

    private void DrawEmptyCardSlot(Rectangle rect, string text)
    {
        spriteBatch.Draw(_dummy, rect, BTN);
        float ty = rect.Y + 8;
        foreach (var line in text.Split('\n'))
        { spriteBatch.DrawString(_font, line, new(rect.X + 8, ty), TEXT, 0, Vector2.Zero, 0.7f, SpriteEffects.None, 0); ty += 20; }
    }

    private void DrawButton(Rectangle rect, string label, bool pressed = false)
    {
        var srcRect = pressed ? BtnPressed : BtnNormal;
        spriteBatch.Draw(InterfaceTexture, rect, srcRect, Color.White);

        var sz  = _font.MeasureString(label);
        float sc = Math.Min(rect.Width / sz.X * 0.6f, rect.Height / sz.Y * 0.6f);
        spriteBatch.DrawString(_font, label,
            new(rect.Center.X - sz.X * sc * 0.5f, rect.Center.Y - sz.Y * sc * 0.5f),
            TEXT, 0, Vector2.Zero, sc, SpriteEffects.None, 0);
    }

    private void DrawLabel(string text, float x, float y)
        => spriteBatch.DrawString(_font, SafeStr(text), new(x, y), TEXT);

    private void DrawCenteredText(string text, float cx, float y)
    {
        var sz = _font.MeasureString(text);
        spriteBatch.DrawString(_font, text, new(cx - sz.X * 0.5f, y), TEXT);
    }

    private void DrawRightText(string text, float right, float y)
    {
        var sz = _font.MeasureString(text);
        spriteBatch.DrawString(_font, text, new(right - sz.X, y), TEXT);
    }

    private void Divider(int y, float w)
        => spriteBatch.Draw(_dummy, new Rectangle(0, y, (int)w, 2), BTN);

    private string SafeStr(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            try { _font.MeasureString(c.ToString()); sb.Append(c); } catch { }
        return sb.ToString();
    }
}