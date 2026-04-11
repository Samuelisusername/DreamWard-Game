using System;
using System.Collections.Generic;
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
    private Rectangle[] _buttons;
    private Rectangle _volumeBar;

    // Colors
    private readonly Color _bgColor = new(0x85, 0x8f, 0x60);  //new Color(113, 137, 92);
    private readonly Color _titleColor = new(0x29, 0x24, 0x10); //new Color(41, 36, 16);
    private readonly Color _buttonTextColor = new(0x29, 0x24, 0x10); //new Color(41, 36, 16);
    private Color _buttonTextColor1;

    private string[] _buttonLabels = { "Start New Game", "Continue Game", "How to Play", "Settings", "Quit Game" };
    private string _howToPlayText =
    @"Welcome to DreamWard!
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

    public StartScreen(GamelabGame game) : base(game) { }

    public override void LoadContent()
    {
        base.LoadContent();
        _font = Content.Load<SpriteFont>("font");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _interfaceTexture = Content.Load<Texture2D>("Sprites/interface");

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

            int btnWidth = w / 5;    // width of normal buttons
            int btnHeight = h / 12;  // height of normal buttons
            int spacingX = w / 50 - 50;   // horizontal spacing
            int spacingY = h / 20;   // vertical spacing
            int startX = (w - (btnWidth * 2 + spacingX)) / 2 + 10;
            int startY = h / 3 + 50;

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
        if (keyboard.IsKeyDown(Keys.Escape))
            Game.Exit();

        UpdateLayout();
        Vector2 mousePos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();

        GamelabGame.IsHovering = false; // reset hover flag

        switch (_currentState)
        {
            case MenuState.MainMenu:
                // Check hover and pressed states for all buttons
                for (int i = 0; i < _buttons.Length; i++)
                {
                    Rectangle btnRect = _buttons[i];
                    string label = _buttonLabels[i];
                    bool disabled = label == "Continue Game" && Game.MapScreenInstance == null;

                    if (btnRect.Contains(mousePos))
                    {
                        GamelabGame.IsHovering = true;

                        if (mouse.WasButtonReleased(MouseButton.Left) && !disabled)
                        {
                            HandleButtonClick(label);
                            break; // exit loop after click
                        }
                    }
                }
                break;

            case MenuState.HowToPlay:
            case MenuState.Settings:
                // Back button
                if (_buttons[0].Contains(mousePos))
                {
                    GamelabGame.IsHovering = true;
                    if (mouse.WasButtonReleased(MouseButton.Left))
                        _currentState = MenuState.MainMenu;
                }

                // Volume bar interaction in Settings
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
            case "Continue Game":
                if (Game.MapScreenInstance != null)
                    Game.SwitchToScreen(Game.MapScreenInstance);
                break;
            case "Start New Game":
                Game.ResetGame();
                Game.MapScreenInstance = new MapScreen(Game);
                Game.CardScreenInstance = new CardScreen(Game);
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
        GraphicsDevice.Clear(_bgColor);
        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix());

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

        Vector2 mousePos = viewportAdapter.PointToScreen(Mouse.GetState().Position).ToVector2();

        foreach (var (btnRect, idx) in _buttons.WithIndex())
        {
            string label = _buttonLabels[idx];

            bool disabled = label == "Continue Game" && Game.MapScreenInstance == null;
            bool pressed = Mouse.GetState().LeftButton == ButtonState.Pressed && btnRect.Contains(mousePos);

            if (disabled)
                DrawButtonDisabled(btnRect, label);
            else
                DrawButton(btnRect, label, pressed);
        }
    }

    private float GetMenuButtonTextScale(Rectangle rect)
    {
        // Use the longest button text so every button has the same text size
        string referenceText = "Start New Game";

        Vector2 textSize = _font.MeasureString(referenceText);

        float scaleX = rect.Width / textSize.X * 0.7f;
        float scaleY = rect.Height / textSize.Y * 0.7f;

        return MathF.Min(scaleX, scaleY);
    }

    private void DrawButton(Rectangle rect, string text, bool pressed = false)
    {
        Rectangle sourceRect = pressed ? new Rectangle(0, 80, 80, 32) : new Rectangle(0, 48, 80, 32);

        float scaleX = rect.Width / (float)sourceRect.Width;
        float scaleY = rect.Height / (float)sourceRect.Height;

        spriteBatch.Draw(_interfaceTexture, new Vector2(rect.X, rect.Y), sourceRect, Color.White, 0f, Vector2.Zero, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);

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
            _buttonTextColor1 = _titleColor;
        }
        Vector2 textSizeFinal = _font.MeasureString(text);
        Vector2 textPos = new Vector2(rect.Center.X - textSizeFinal.X * textScale / 2, rect.Center.Y - textSizeFinal.Y * textScale / 2);
        spriteBatch.DrawString(_font, text, textPos, _buttonTextColor1, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    private void DrawButtonDisabled(Rectangle rect, string text)
    {
        Rectangle sourceRect = new Rectangle(0, 48, 80, 32);
        float scaleX = rect.Width / (float)sourceRect.Width;
        float scaleY = rect.Height / (float)sourceRect.Height;

        spriteBatch.Draw(_interfaceTexture, new Vector2(rect.X, rect.Y), sourceRect, Color.Gray * 0.6f, 0f, Vector2.Zero, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);

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

        float scaleX = (vw * 1f) / sourceRect.Width;
        float scaleY = (vh * 1f) / sourceRect.Height;
        float scale = Math.Min(scaleX, scaleY);

        Vector2 size = new Vector2(sourceRect.Width * scale, sourceRect.Height * scale);
        Vector2 pos = new Vector2((vw - size.X) / 2, (vh - size.Y) / 2);
        Rectangle destRect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);

        spriteBatch.Draw(_interfaceTexture, destRect, sourceRect, Color.White);
    }

    private void DrawHowToPlay()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;
        DrawScaledText("How to Play", _titleColor, w / 2, 50, 2f);

        Rectangle paragraphArea = new Rectangle(w / 10, h / 5, w * 8 / 10, h * 3 / 5 - 60);
        DrawScaledParagraph(_howToPlayText, paragraphArea, _titleColor);

        Vector2 mousePos = viewportAdapter.PointToScreen(Mouse.GetState().Position).ToVector2();
        bool pressed = Mouse.GetState().LeftButton == ButtonState.Pressed && _backButton.Contains(mousePos);
        DrawButton(_backButton, "Back", pressed);
    }

    private void DrawSettings()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;
        DrawScaledText("Settings", _titleColor, w / 2, 50, 2f);

        Color barBackgroundColor = new (0x5c, 0x63, 0x40);
        Color barFillColor = new (0x29, 0x24, 0x10);

        DrawRoundedRectangle(_volumeBar, 5, barBackgroundColor, Color.Transparent);

        Rectangle fill = new Rectangle(_volumeBar.X, _volumeBar.Y, (int)(_volumeBar.Width * _volume), _volumeBar.Height);
        DrawRoundedRectangle(fill, 5, barFillColor, Color.Transparent);

        string volumeLabel = "Volume";
        Vector2 labelSize = _font.MeasureString(volumeLabel);
        Vector2 labelPos = new Vector2(_volumeBar.Center.X - labelSize.X / 2, _volumeBar.Y - labelSize.Y - 5);
        spriteBatch.DrawString(_font, volumeLabel, labelPos, _titleColor);

        Vector2 mousePos = viewportAdapter.PointToScreen(Mouse.GetState().Position).ToVector2();
        bool pressed = Mouse.GetState().LeftButton == ButtonState.Pressed && _backButton.Contains(mousePos);
        DrawButton(_backButton, "Back", pressed);
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
