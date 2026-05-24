using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gamelab.Managers;
using MonoGame.Extended.Input;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using DreamWardUtils.Graphics;

namespace Gamelab.Screens;

public enum GameOverType
{
    Win,
    RegularMonster,
    FlyingMonster,
    SmallMonster,
    BigMonster,
    SpawnFail,
    TentIsolated,
}

public class GameOverScreen : GamelabGameScreen
{
    // ── Data ──────────────────────────────────────────────────────────────
    private SpriteFont _font;
    private Texture2D  _pixel;

    public static bool DidWin { get; set; } = false;

    private readonly GameOverType _killedBy;
    private readonly Structure    _bestStructure;
    private readonly TimeSpan     _runTime;

    // ── Sprite sheets ─────────────────────────────────────────────────────
    private Texture2D _dayTopSheet;
    private Texture2D _dayTopBaseSheet;
    private Texture2D _nightTopSheet;
    private Texture2D _nightTopBaseSheet;
    private Texture2D _interfaceSheet;
    private Texture2D _dayInterfaceSheet;
    private Texture2D _monsterSheet;

    // Convenience: pick the right interface sheet based on win/lose
    private Texture2D IfSheet => DidWin ? _dayInterfaceSheet : _interfaceSheet;

    // ── Interface button slices (from interface.json / day_interface.json) ─
    // float_button       (0, 176, 80, 32)
    // float_button_pressed (0, 208, 80, 32)
    // long_float_button  (0, 240, 105, 32)
    // long_float_button_pressed (0, 272, 105, 32)
    private static readonly Rectangle SRC_FLOAT_BTN     = new(0, 176,  80, 32);
    private static readonly Rectangle SRC_FLOAT_BTN_PRS = new(0, 208,  80, 32);

    // Backdrop slice (same coords in both sheets)
    private static readonly Rectangle SRC_BACKDROP = new(204, 35, 282, 308);

    // ── Sprite-sheet source rows (day_top / night_top, frame 0 at x=0) ────
    private const int SPR_TOWER_AOE  = 896;
    private const int SPR_SNIPER     = 1152;    
    private const int SPR_BOMB       = 1920;
    private const int SPR_SPIKE      = 2304;

    // monsters.png: horizontal frames, monster type stacked vertically, each 128×128
    private const int MON_FLY    = 0;
    private const int MON_NORMAL = 128;
    private const int MON_SMOL   = 256;
    private const int MON_STRONG = 384;

    // ── Scale (mirrors StartScreen exactly) ───────────────────────────────
    private float SpriteScale => virtualScreenSize.X / 640f;
    private int VW => virtualScreenSize.X;
    private int VH => virtualScreenSize.Y;
    private int S(float refPx) => (int)(refPx * SpriteScale);

    // ── Button click state ─────────────────────────────────────────────────
    private bool _menuBtnDown     = false;
    private bool _infinityBtnDown = false;

    // ── Computed rects (derived fresh each frame so always correct) ────────

    // Backdrop destination — centred, scaled from SRC_BACKDROP dimensions
    private Rectangle BackdropDest
    {
        get
        {
            int w = S(SRC_BACKDROP.Width);
            int h = S(SRC_BACKDROP.Height);
            return new Rectangle((VW - w) / 2, (VH - h) / 2, w, h);
        }
    }

    // Content area: the writable area inside the scroll.
    private Rectangle ContentArea
    {
        get
        {
            var bd = BackdropDest;
            int ix = S(38);
            int it = S(22);
            int ib = S(20);
            return new Rectangle(bd.X + ix, bd.Y + it,
                                 bd.Width - ix * 2, bd.Height - it - ib);
        }
    }

    private Rectangle MenuBtnDest
    {
        get
        {
            int w = S(SRC_FLOAT_BTN.Width);
            int h = S(SRC_FLOAT_BTN.Height);

            return new Rectangle(
                S(10),
                VH - h - S(10),
                w,
                h
            );
        }
    }

    private Rectangle InfinityBtnDest
    {
        get
        {
            int w = S(SRC_FLOAT_BTN.Width);
            int h = S(SRC_FLOAT_BTN.Height);

            return new Rectangle(
                VW - w - S(10),
                VH - h - S(10),
                w,
                h
            );
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────
    public GameOverScreen(GamelabGame game, GameOverType killedBy = GameOverType.RegularMonster) : base(game)
    {
        _killedBy      = killedBy;
        _bestStructure = GamelabGame.MapManager.GetBestStructure();
        _runTime       = GamelabGame.InGameTime;
    }

    // ── LoadContent ───────────────────────────────────────────────────────
    public override void LoadContent()
    {
        base.LoadContent();

        _font  = Content.Load<SpriteFont>("font");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _dayTopSheet       = Content.Load<Texture2D>("Sprites/day_top");
        _dayTopBaseSheet   = Content.Load<Texture2D>("Sprites/day_top_base");
        _nightTopSheet     = Content.Load<Texture2D>("Sprites/night_top");
        _nightTopBaseSheet = Content.Load<Texture2D>("Sprites/night_top_base");
        _monsterSheet      = Content.Load<Texture2D>("Sprites/monsters");
        _interfaceSheet    = Content.Load<Texture2D>("Sprites/interface");
        _dayInterfaceSheet = Content.Load<Texture2D>("Sprites/day_interface");

        if (!DidWin)
        {
            AudioManager.LoadSound(Content, "gameover_voice",
                "Sounds/random_sounds/universfield-game-over-deep-male-voice-clip-352695");
            AudioManager.Play("gameover_voice");
        }
    }

    // ── Update ────────────────────────────────────────────────────────────
    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        if (!Game.IsActive) return;

        // Keyboard shortcuts
        if (keyboard.WasKeyReleased(Keys.Enter))
        {
            GoToMenu();
            return;
        }
        if (DidWin && keyboard.WasKeyReleased(Keys.Space))
        {
            Game.SwitchToScreen(Game.MapScreenInstance);
            return;
        }

        // Mouse — convert to virtual space the same way StartScreen does
        Vector2 mousePos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();

        GamelabGame.IsHovering = false;

        // hover detection
        if (MenuBtnDest.Contains(mousePos))
            GamelabGame.IsHovering = true;

        if (DidWin && InfinityBtnDest.Contains(mousePos))
            GamelabGame.IsHovering = true;

        // Track pressed state for visual feedback, then fire on release
        _menuBtnDown     = mouse.IsButtonDown(MouseButton.Left) && MenuBtnDest.Contains(mousePos);
        _infinityBtnDown = mouse.IsButtonDown(MouseButton.Left) && InfinityBtnDest.Contains(mousePos);

        if (mouse.WasButtonReleased(MouseButton.Left))
        {
            if (MenuBtnDest.Contains(mousePos))
            {
                GoToMenu();
                return;
            }
            if (DidWin && InfinityBtnDest.Contains(mousePos))
            {
                Game.SwitchToScreen(Game.MapScreenInstance);
                return;
            }
        }
    }

    private void GoToMenu()
    {
        Game.IsGameInProgress = false;
        Game.ResetGame();
        Game.MapScreenInstance = new MapScreen(Game);
        Game.MapScreenInstance.Initialize();
        Game.MapScreenInstance.LoadContent();
        Game.StartScreenInstance ??= new StartScreen(Game);
        Game.SwitchToScreen(Game.StartScreenInstance);
    }

    // ── Draw ──────────────────────────────────────────────────────────────
    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        if (Game.MapScreenInstance != null)
            Game.MapScreenInstance.DrawMapOnly(gameTime);
        else
            GraphicsDevice.Clear(DidWin ? new Color(134, 175, 80) : new Color(18, 30, 40));

        spriteBatch.Begin(
            transformMatrix: viewportAdapter.GetScaleMatrix(),
            samplerState: SamplerState.PointClamp);

        DrawBackdrop();
        DrawScrollContent();
        DrawButtons();

        spriteBatch.End();
    }

    // ── Backdrop ──────────────────────────────────────────────────────────
    private void DrawBackdrop()
        => spriteBatch.Draw(IfSheet, BackdropDest, SRC_BACKDROP, Color.White);

    // ── Content ───────────────────────────────────────────────────────────

    Color TITLE_COL = DidWin
        ? new Color(30, 173, 16)
        : new Color(191, 15, 15);
    Color INF_TEXT     = new Color(214, 143, 11);
    private void DrawScrollContent()
    {
        var ca = ContentArea;

        Color TXT = Color.Black;

        int leftX = ca.X + S(8);
        int cy = ca.Y + S(70);

        string title = DidWin ? "YOU SURVIVED!" : "GAME OVER";

        float titleScale = 0.5f * SpriteScale;
        Vector2 titleSize = _font.MeasureString(title) * titleScale;

        spriteBatch.DrawString(_font, title,
            new Vector2(ca.Center.X - titleSize.X / 2, cy),TITLE_COL,
            0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        cy += S(25);

        DrawRect(ca.X, cy, ca.Width, Math.Max(1, S(1)), TXT);

        cy += S(4);

        if (_killedBy == GameOverType.TentIsolated || _killedBy == GameOverType.SpawnFail)
        {
            string msg = _killedBy == GameOverType.TentIsolated
                ? "You fell out of the world"
                : "Couldn't Spawn Monsters";

            spriteBatch.DrawString(_font, msg,
                new Vector2(leftX, cy + S(8)),
                TITLE_COL, 0f, Vector2.Zero,
                0.3f * SpriteScale,
                SpriteEffects.None, 0f);

            cy += S(28);

            DrawRect(ca.X, cy, ca.Width, Math.Max(1, S(1)), TXT);

            cy += S(4);
        }

        if (!DidWin &&
            _killedBy != GameOverType.SpawnFail &&
            _killedBy != GameOverType.TentIsolated)
        {
            var (monsterLabel, monsterSheet, monsterSrc) = GetMonsterInfo();

            int spriteSize = S(64);
            int spriteX = ca.Right - spriteSize;
            int spriteY = cy;

            spriteBatch.DrawString(_font, "Killed By:",
                new Vector2(leftX, cy),
                TXT, 0f, Vector2.Zero,
                0.3f * SpriteScale,
                SpriteEffects.None, 0f);

            spriteBatch.DrawString(_font, monsterLabel,
                new Vector2(leftX + S(8), cy + S(10)),
                TITLE_COL, 0f, Vector2.Zero,
                0.3f * SpriteScale,
                SpriteEffects.None, 0f);

            DrawRect(spriteX, spriteY, spriteSize, spriteSize, new Color(0, 0, 0, 25));

            DrawOutlineRect(
                new Rectangle(spriteX, spriteY, spriteSize, spriteSize),
                TITLE_COL);

            spriteBatch.Draw(monsterSheet,
                new Rectangle(spriteX, spriteY, spriteSize, spriteSize),
                monsterSrc, Color.White);

            cy += S(64);

            DrawRect(ca.X, cy + S(4), ca.Width, Math.Max(1, S(1)), TXT);

            cy += S(8);
        }

        if (_bestStructure is Tower)
        {
            var (towerLabel, towerSrc) = GetTowerInfo();

            bool isSpike = towerLabel.Contains("Spike");

            Texture2D topSheet = DidWin ? (isSpike ? _dayTopBaseSheet : _dayTopSheet) : (isSpike ? _nightTopBaseSheet : _nightTopSheet);

            int spriteSize = S(64);
            int spriteX = ca.Right - spriteSize;
            int spriteY = cy;

            spriteBatch.DrawString(_font, "Best Structure:",
                new Vector2(leftX, cy),
                TXT, 0f, Vector2.Zero,
                0.3f * SpriteScale,
                SpriteEffects.None, 0f);

            spriteBatch.DrawString(_font, towerLabel,
                new Vector2(leftX + S(8), cy + S(10)),
                TITLE_COL, 0f, Vector2.Zero,
                0.3f * SpriteScale,
                SpriteEffects.None, 0f);

            DrawRect(spriteX, spriteY, spriteSize, spriteSize,
                DidWin
                    ? new Color(0, 0, 0, 10)
                    : new Color(0, 0, 0, 25));

            DrawOutlineRect(
                new Rectangle(spriteX, spriteY, spriteSize, spriteSize),
                TITLE_COL);

            spriteBatch.Draw(topSheet,
                new Rectangle(spriteX, spriteY, spriteSize, spriteSize),
                towerSrc, Color.White);

            spriteBatch.DrawString(_font,
                "Damage Caused:",
                new Vector2(leftX, cy + S(25)),
                TXT, 0f, Vector2.Zero,
                0.3f * SpriteScale,
                SpriteEffects.None, 0f);

            spriteBatch.DrawString(_font,
                (_bestStructure is Tower t ? t.TotalDamage.ToString("F0") : "0"),
                new Vector2(leftX + S(105), cy + S(25)),
                TITLE_COL, 0f, Vector2.Zero,
                0.3f * SpriteScale,
                SpriteEffects.None, 0f);

            cy += S(64);

            DrawRect(ca.X, cy + S(4), ca.Width, Math.Max(1, S(1)), TXT);

            cy += S(8);
        }

        int totalSec = (int)_runTime.TotalSeconds;
        int m = totalSec / 60;
        int s = totalSec % 60;
        int ms = _runTime.Milliseconds;

        spriteBatch.DrawString(_font,
            "Run Time:",
            new Vector2(leftX, cy),
            TXT, 0f, Vector2.Zero,
            0.3f * SpriteScale,
            SpriteEffects.None, 0f);

        spriteBatch.DrawString(_font,
            $"{m:D2}:{s:D2}.{ms:D3}",
            new Vector2(leftX + S(65), cy),
            TITLE_COL, 0f, Vector2.Zero,
            0.3f * SpriteScale,
            SpriteEffects.None, 0f);

        cy += S(14);

        if (DidWin)
        {
            float scale = 0.27f * SpriteScale;

            string line1 = "Think you can survive longer?";
            Vector2 sz1 = _font.MeasureString(line1) * scale;

            spriteBatch.DrawString(_font, line1,
                new Vector2(ca.Center.X - sz1.X / 2, cy),
                INF_TEXT, 0f, Vector2.Zero,
                scale, SpriteEffects.None, 0f);

            cy += S(10);

            string line2 = "Try Infinity Mode!";
            Vector2 sz2 = _font.MeasureString(line2) * scale;

            spriteBatch.DrawString(_font, line2,
                new Vector2(ca.Center.X - sz2.X / 2, cy),
                INF_TEXT, 0f, Vector2.Zero,
                scale, SpriteEffects.None, 0f);

            cy += S(12);
        }
    }

    private void DrawOutlineRect(Rectangle rect, Color color)
    {
        int t = Math.Max(1, S(1));

        DrawRect(rect.X, rect.Y, rect.Width, t, color);
        DrawRect(rect.X, rect.Bottom - t, rect.Width, t, color);
        DrawRect(rect.X, rect.Y, t, rect.Height, color);
        DrawRect(rect.Right - t, rect.Y, t, rect.Height, color);
    }

    // ── Buttons (interface sprite buttons) ────────────────────────────────
    private void DrawButtons()
    {
        Texture2D menuSheet = DidWin ? _dayInterfaceSheet : _interfaceSheet;

        Rectangle menuSrc = _menuBtnDown ? SRC_FLOAT_BTN_PRS : SRC_FLOAT_BTN;
        spriteBatch.Draw(menuSheet, MenuBtnDest, menuSrc, Color.White);
        DrawButtonLabel(MenuBtnDest, "Menu", _menuBtnDown);

        if (DidWin)
        {
            Rectangle infSrc = _infinityBtnDown ? SRC_FLOAT_BTN_PRS : SRC_FLOAT_BTN;
            spriteBatch.Draw(_dayInterfaceSheet, InfinityBtnDest, infSrc, Color.White);
            DrawButtonLabel(InfinityBtnDest, "Infinity Mode", _infinityBtnDown);
        }
    }

    private void DrawButtonLabel(Rectangle btnRect, string text, bool pressed)
    {
        Vector2 sz    = _font.MeasureString(text);
        float   scale = Math.Min(btnRect.Width / sz.X * 0.70f,
                                 btnRect.Height / sz.Y * 0.60f);
        float   pressOff = pressed ? SpriteScale : 0f;
        Vector2 pos   = new Vector2(
            btnRect.Center.X - sz.X * scale / 2f,
            btnRect.Center.Y - sz.Y * scale / 2f + pressOff);
        spriteBatch.DrawString(_font, text, pos, Color.Black,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }


    // ── Primitives ────────────────────────────────────────────────────────
    private void DrawRect(int x, int y, int w, int h, Color c)
        => spriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), c);

    // ── Info lookups ──────────────────────────────────────────────────────
    private (string label, Texture2D sheet, Rectangle src) GetMonsterInfo() =>
        _killedBy switch
        {
            GameOverType.RegularMonster => ("Regular Monster", _monsterSheet, new Rectangle(0, MON_NORMAL, 128, 128)),
            GameOverType.FlyingMonster  => ("Flying Monster",  _monsterSheet, new Rectangle(0, MON_FLY,    128, 128)),
            GameOverType.SmallMonster   => ("Small Monster",   _monsterSheet, new Rectangle(0, MON_SMOL,   128, 128)),
            GameOverType.BigMonster     => ("Big Monster",     _monsterSheet, new Rectangle(0, MON_STRONG, 128, 128)),
            _                           => ("Unknown",         _monsterSheet, new Rectangle(0, MON_NORMAL, 128, 128)),
        };

    private (string label, Rectangle src) GetTowerInfo()
    {
        if (_bestStructure is Tower t)
        {
            string n = t.GetType().Name;
            if (n.Contains("Sniper"))                             return ("Sniper Tower", new Rectangle(0, SPR_SNIPER,    128, 128));
            if (n.Contains("Area"))                               return ("AOE Tower",    new Rectangle(0, SPR_TOWER_AOE, 128, 128));
            if (n.Contains("Bomb"))                               return ("Bomb",         new Rectangle(0, SPR_BOMB,      128, 128));
            if (n.Contains("Spike"))                              return ("Spikes",       new Rectangle(0, SPR_SPIKE,     128, 128));
            return (n, new Rectangle(0, SPR_SNIPER, 128, 128));
        }
        return ("None", new Rectangle(0, SPR_SNIPER, 128, 128));
    }
}