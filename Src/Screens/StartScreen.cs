using System;
using System.Collections.Generic;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using Gamelab.Managers;

namespace Gamelab.Screens;
public class StartScreen : GamelabGameScreen
{
    private SpriteFont _font;
    private Texture2D _pixel;
    private Texture2D _interfaceTexture;
    private Texture2D _dayInterfaceTexture;
    private Texture2D InterfaceTexture => IsDay ? _dayInterfaceTexture : _interfaceTexture;
    private float SpriteScale => virtualScreenSize.X / 640f;
    private TextureAtlas InterfaceAtlas => GamelabGame.textureAtlas[IsDay ? "day_interface" : "interface"];
    private Rectangle Region(string name) => InterfaceAtlas.GetRegion(name + "0").SourceRectangle;
    private Rectangle[] _buttons;
    private Rectangle _volumeBar;

    // Colors
    private readonly Color _bgColor = new(0x85, 0x8f, 0x60);  //new Color(113, 137, 92);
    private readonly Color _titleColor = new(0x29, 0x24, 0x10); //new Color(41, 36, 16);
    private readonly Color _buttonTextColor = new(0x29, 0x24, 0x10); //new Color(41, 36, 16);
    private Color _buttonTextColor1;

    private bool IsDay => GamelabGame.GamePhaseManager?.IsDay ?? true;
    private Color PanelColor => IsDay
        ? new(0xc7, 0xb8, 0x9c)   // light transparent (day)  // #03050b
        : new(0x03,0x05,0x0b);     // dark transparent (night)
    private Color TextColor => IsDay
        ? new(0x29, 0x24, 0x10)           // dark text (day)
        : new Color(0xd6, 0xd6, 0xd6);       // light text (night)

    private string[] _buttonLabels = { "New Game", "Continue", "How to Play", "Settings", "Quit Game" };
    private string _howToPlayText =
    @" Welcome to DreamWard!
    - Protect yourself and your sheep.
    - Let your sheep graze on meadows to increase your sanity.
    - Use sanity to buy and play build cards.
    - Build towers and fences to defend your territory.
    - Use action cards strategically to gain advantages.
    - During the night, watch how your defenses protect you from monsters.";

    private float _volume = AudioManager.SavedVolume;

    private enum MenuState { MainMenu, HowToPlay, Settings }
    private MenuState _currentState = MenuState.MainMenu;
    private MenuState _previousState;

    private Rectangle _backButton;
    private readonly bool[]         _btnClickedInside = new bool[5];
    private readonly ButtonResult[] _btnResults       = new ButtonResult[5];

    public StartScreen(GamelabGame game) : base(game) { }

    public override void LoadContent()
    {
        base.LoadContent();
        _font = Content.Load<SpriteFont>("font");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _interfaceTexture = Content.Load<Texture2D>("Sprites/interface");
        _dayInterfaceTexture = Content.Load<Texture2D>("Sprites/day_interface");

        UpdateLayout();
        _previousState = _currentState;
        AudioManager.LoadSound(Game.Content, "start_music", "Sounds/start_screen");
    }

    private void UpdateLayout()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;

        if (_currentState == MenuState.MainMenu)
        {
            _buttons = new Rectangle[_buttonLabels.Length];

            var btnSrc    = Region("button_normal");
            int btnWidth  = (int)(btnSrc.Width  * SpriteScale);
            int btnHeight = (int)(btnSrc.Height * SpriteScale);
            int spacingX  = (int)(10 * SpriteScale);
            int spacingY  = (int)(8  * SpriteScale);
            int startX = (w - (btnWidth * 2 + spacingX)) / 2;
            int startY = h / 2 - 2 * btnHeight / 2 - spacingY;

            // Top row: Start, Continue
            _buttons[0] = new Rectangle(startX, startY, btnWidth, btnHeight);                 // Start
            _buttons[1] = new Rectangle(startX + btnWidth + spacingX, startY, btnWidth, btnHeight); // Continue

            // Middle row: How to Play (centered)
            _buttons[2] = new Rectangle((w - btnWidth) / 2, startY + btnHeight + spacingY, btnWidth, btnHeight); // How to Play

            // Bottom row: Settings, Quit
            _buttons[3] = new Rectangle(startX, startY + 2 * (btnHeight + spacingY), btnWidth, btnHeight); // Settings
            _buttons[4] = new Rectangle(startX + btnWidth + spacingX, startY + 2 * (btnHeight + spacingY), btnWidth, btnHeight); // Quit
        }
        else
        {
            // Single Back button for HowToPlay & Settings
            _buttons = new Rectangle[] { new Rectangle(50, h - 100, 200, 75) };
            _backButton = _buttons[0];
        }

        if (_currentState == MenuState.Settings)
            _volumeBar = new Rectangle(w / 2 - 200, h / 3, 400, 30);
    }

    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        AudioManager.PlayMusic("start_music");

        if (keyboard.WasKeyReleased(Keys.Escape) && Game.IsGameInProgress)
            Game.SwitchToScreen(Game.MapScreenInstance);

        UpdateLayout();
        Vector2 mousePos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();

        GamelabGame.IsHovering = false; // reset hover flag

        switch (_currentState)
        {
            case MenuState.MainMenu:
                for (int i = 0; i < _buttons.Length; i++)
                {
                    string label = _buttonLabels[i];
                    bool disabled = label == "Continue" && !Game.IsGameInProgress;
                    if (disabled) { _btnResults[i] = ButtonResult.Normal; continue; }

                    _btnResults[i] = UpdateButton(_buttons[i], mouse, mousePos, ref _btnClickedInside[i]);
                    if (_btnResults[i] == ButtonResult.Clicked)
                    {
                        HandleButtonClick(label);
                        break;
                    }
                }
                if (keyboard.WasKeyReleased(Keys.F5))
                    GamelabGame.Instance.Balance = GamelabGame.Instance.jsonLoader.LoadJson<BalanceParams>("Balance.json");
                break;

            case MenuState.HowToPlay:
            case MenuState.Settings:
                _btnResults[0] = UpdateButton(_buttons[0], mouse, mousePos, ref _btnClickedInside[0]);
                if (_btnResults[0] == ButtonResult.Clicked)
                    _currentState = MenuState.MainMenu;

                if (_currentState == MenuState.Settings && _volumeBar.Contains(mousePos))
                {
                    GamelabGame.IsHovering = true;
                    if (mouse.LeftButton == ButtonState.Pressed)
                    {
                        _volume = (mousePos.X - _volumeBar.X) / (float)_volumeBar.Width;
                        _volume = MathHelper.Clamp(_volume, 0f, 1f);
                        AudioManager.UpdateMusicVolume(_volume);
                    }
                }
                break;
        }
    }

    private void HandleButtonClick(string label)
    {
        switch (label)
        {
            case "Continue":
                if (Game.MapScreenInstance != null)
                    Game.SwitchToScreen(Game.MapScreenInstance);
                break;
            case "New Game":
                Game.ResetGame();
                Game.MapScreenInstance = new MapScreen(Game);
                Game.IsGameInProgress = true;
                Game.SwitchToScreen(Game.MapScreenInstance);
                break;
            case "How to Play":
                _currentState = MenuState.HowToPlay;
                break;
            case "Settings":
                _currentState = MenuState.Settings;
                break;
            case "Quit Game":
                Game.Exit();
                break;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);

        if (Game.MapScreenInstance != null)
            Game.MapScreenInstance.DrawMapOnly(gameTime);
        else
            GraphicsDevice.Clear(_bgColor);

        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix(), samplerState: SamplerState.PointClamp);

        switch (_currentState)
        {
            case MenuState.MainMenu:
                DrawMainMenu();
                break;
            case MenuState.HowToPlay:
                DrawHowToPlay();
                break;
            case MenuState.Settings:
                DrawSettings();
                break;
        }

        spriteBatch.End();
    }

    private void DrawMainMenu()
    {
        DrawBackdrop();

        foreach (var (btnRect, idx) in _buttons.WithIndex())
        {
            string label = _buttonLabels[idx];
            bool disabled = label == "Continue" && !Game.IsGameInProgress;
            if (disabled)
                DrawButtonDisabled(btnRect, label);
            else
                DrawButton(btnRect, label, _btnResults[idx] == ButtonResult.Pressed);
        }
    }

    private float GetMenuButtonTextScale(Rectangle rect)
    {
        // Use the longest button text so every button has the same text size
        string referenceText = "Start New Game";

        Vector2 textSize = _font.MeasureString(referenceText);

        float scaleX = rect.Width / textSize.X * 1;
        float scaleY = rect.Height / textSize.Y * 1;

        return MathF.Min(scaleX, scaleY);
    }

    private Rectangle PickMenuSrc(Rectangle dest, bool pressed, string normalName = null, string pressedName = null)
    {
        if (normalName != null && pressedName != null)
            return Region(pressed ? pressedName : normalName);
        var normal = Region("button_normal");
        bool wide = (float)dest.Width / dest.Height > normal.Width / (float)normal.Height;
        wide = false;
        if (pressed) return wide ? Region("long_button_pressed") : Region("button_pressed");
        return wide ? Region("long_button") : Region("button_normal");
    }

    private void DrawButton(Rectangle rect, string text, bool pressed = false, string normalRegion = null, string pressedRegion = null)
    {
        Rectangle sourceRect = PickMenuSrc(rect, pressed, normalRegion, pressedRegion);
        float scale = SpriteScale;
        int drawW = (int)(sourceRect.Width  * scale);
        int drawH = (int)(sourceRect.Height * scale);
        Vector2 drawPos = new Vector2(rect.Center.X - drawW / 2f, rect.Center.Y - drawH / 2f);

        spriteBatch.Draw(InterfaceTexture, drawPos, sourceRect, Color.White, 0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);

        float textScale;

        if (_currentState == MenuState.MainMenu)
        {
            textScale = GetMenuButtonTextScale(rect);
            _buttonTextColor1 = _buttonTextColor;
        }
        else
        {
            Vector2 textSize = _font.MeasureString(text);
            textScale = MathF.Min(rect.Width / textSize.X * 0.6f, rect.Height / textSize.Y * 0.6f);
            _buttonTextColor1 = _buttonTextColor;
        }
        Vector2 textSizeFinal = _font.MeasureString(text);
        float pressOffset = pressed ? SpriteScale : 0f;
        Vector2 textPos = new Vector2(rect.Center.X - textSizeFinal.X * textScale / 2, rect.Center.Y - textSizeFinal.Y * textScale / 2 + pressOffset);
        spriteBatch.DrawString(_font, text, textPos, _buttonTextColor1, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    private void DrawButtonDisabled(Rectangle rect, string text)
    {
        Rectangle sourceRect = PickMenuSrc(rect, false);
        float scale = SpriteScale;
        int drawW = (int)(sourceRect.Width  * scale);
        int drawH = (int)(sourceRect.Height * scale);
        Vector2 drawPos = new Vector2(rect.Center.X - drawW / 2f, rect.Center.Y - drawH / 2f);

        spriteBatch.Draw(InterfaceTexture, drawPos, sourceRect, Color.Gray * 0.6f, 0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);

        float textScale = GetMenuButtonTextScale(rect);
        Vector2 textSize = _font.MeasureString(text);
        Vector2 textPos = new Vector2(rect.Center.X - textSize.X * textScale / 2, rect.Center.Y - textSize.Y * textScale / 2);
        spriteBatch.DrawString(_font, text, textPos, Color.Gray * 0.6f, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    private void DrawBackdrop()
    {
        int vw = virtualScreenSize.X;
        int vh = virtualScreenSize.Y;

        Rectangle sourceRect = new Rectangle(204, 35, 282, 308);

        float scale = SpriteScale;
        Vector2 size = new Vector2(sourceRect.Width * scale, sourceRect.Height * scale);
        Vector2 pos  = new Vector2((vw - size.X) / 2, (vh - size.Y) / 2);
        Rectangle destRect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);

        spriteBatch.Draw(InterfaceTexture, destRect, sourceRect, Color.White);
    }

    private void DrawHowToPlay()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;
        
        Rectangle panel = new Rectangle(w / 10, h / 6, w * 8 / 10, h * 4 / 6);
        DrawTransparentPanel(panel);
        
        DrawScaledText("How to Play", TextColor, w / 2, 50, 2f);

        Rectangle paragraphArea = new Rectangle(w / 10, h / 5, w * 8 / 10, h * 3 / 5 - 60);
        DrawScaledParagraph(_howToPlayText, paragraphArea, TextColor);

        DrawButton(_backButton, "Back", _btnResults[0] == ButtonResult.Pressed, "float_button", "float_button_pressed");
    }

    private void DrawSettings()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;

        Rectangle panel = new Rectangle(w / 10, h / 6, w * 8 / 10, h * 4 / 6);
        DrawTransparentPanel(panel);

        DrawScaledText("Settings", TextColor, w / 2, 50, 2f);

        Color barBackgroundColor = IsDay
            ? new Color(150, 140, 100)
            : new Color(0x03,0x05,0x0b);

        Color barFillColor = IsDay
            ? new Color(41, 36, 16)
            : new Color(200, 200, 200);

        DrawRoundedRectangle(_volumeBar, 5, barBackgroundColor, Color.Transparent);

        Rectangle fill = new Rectangle(_volumeBar.X, _volumeBar.Y, (int)(_volumeBar.Width * _volume), _volumeBar.Height);
        DrawRoundedRectangle(fill, 5, barFillColor, Color.Transparent);

        string volumeLabel = "Volume";
        Vector2 labelSize = _font.MeasureString(volumeLabel);
        Vector2 labelPos = new Vector2(_volumeBar.Center.X - labelSize.X / 2, _volumeBar.Y - labelSize.Y - 5);
        spriteBatch.DrawString(_font, volumeLabel, labelPos, TextColor);

        DrawButton(_backButton, "Back", _btnResults[0] == ButtonResult.Pressed, "float_button", "float_button_pressed");
    }

    private void DrawRoundedRectangle(Rectangle rect, int radius, Color fillColor, Color borderColor)
    {
        spriteBatch.Draw(_pixel, rect, fillColor);
        if (borderColor != Color.Transparent)
        {
            int thickness = 3;
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), borderColor);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), borderColor);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), borderColor);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), borderColor);
        }
    }

    private void DrawScaledText(string text, Color color, float centerX, float y, float scale = 1f)
    {
        Vector2 textSize = _font.MeasureString(text) * scale;
        Vector2 pos = new Vector2(centerX - textSize.X / 2, y);
        spriteBatch.DrawString(_font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawScaledParagraph(string text, Rectangle area, Color color)
    {
        Vector2 size = _font.MeasureString(text);
        float scaleX = area.Width / size.X;
        float scaleY = area.Height / size.Y;
        float scale = Math.Min(scaleX, scaleY);
        Vector2 pos = new Vector2(area.X + area.Width / 2 - size.X * scale / 2, area.Y + area.Height / 2 - size.Y * scale / 2);
        spriteBatch.DrawString(_font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
    private void DrawTransparentPanel(Rectangle rect)
    {
        spriteBatch.Draw(_pixel, rect, PanelColor * 0.5f);
    }
}

public static class EnumerableExtensions
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
    {
        int i = 0;
        foreach (var item in self)
            yield return (item, i++);
    }
}
