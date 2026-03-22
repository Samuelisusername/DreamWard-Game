using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;

namespace Gamelab.Screens;

public class StartScreen : GamelabGameScreen
{
    private SpriteFont _font;
    private Texture2D _bgTexture;
    private Texture2D _pixel;

    // Colors
    private readonly Color _bgColor = new Color(8, 48, 43);
    private readonly Color _titleColor = new Color(188, 182, 157);
    private readonly Color _buttonColor = new Color(72, 93, 70);
    private readonly Color _buttonBorderColor = new Color(188, 182, 157);
    private readonly Color _buttonTextColor = new Color(188, 182, 157);
    private Rectangle[] _buttons;
    private string[] _buttonLabels = { "Start New Game", "Continue Game", "How to Play", "Settings", "Quit Game" };

    private enum MenuState { MainMenu, HowToPlay, Settings }
    private MenuState _currentState = MenuState.MainMenu;

    private string _howToPlayText =
    @"Welcome to DreamWard!
    - Protect yourself and your sheep.
    - Let your sheep graze on meadows to increase your sanity.
    - Use sanity to buy and play build cards.
    - Build towers and fences to defend your territory.
    - Use action cards strategically to gain advantages.
    - During the night, watch how your defenses protect you from monsters.";

    private float _volume = 0.5f;
    private Rectangle _volumeBar;

    public StartScreen(GamelabGame game) : base(game) { }

    public override void LoadContent()
    {
        base.LoadContent();
        _font = Content.Load<SpriteFont>("font");
        _bgTexture = Content.Load<Texture2D>("Sprites/Start");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;

        if (_currentState == MenuState.MainMenu)
        {
            _buttons = new Rectangle[_buttonLabels.Length];
            int btnWidth = w / 4;
            int btnHeight = h / 12;
            int spacing = btnHeight / 2;
            int startY = h / 3;

            for (int i = 0; i < _buttons.Length; i++)
                _buttons[i] = new Rectangle(w / 2 - btnWidth / 2, startY + i * (btnHeight + spacing), btnWidth, btnHeight);
        }
        else
        {
            _buttons = new Rectangle[]
            {
                new Rectangle(50, h - 100, 150, 50)
            };
        }

        if (_currentState == MenuState.Settings)
            _volumeBar = new Rectangle(w / 2 - 200, h / 3, 400, 30);
    }

    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        if (keyboard.IsKeyDown(Keys.Escape))
            Game.Exit();

        UpdateLayout();
        Vector2 pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();

        switch (_currentState)
        {
            case MenuState.MainMenu:
                if (mouse.WasButtonPressed(MouseButton.Left))
                {
                    for (int i = 0; i < _buttons.Length; i++)
                    {
                        if (_buttons[i].Contains(pos))
                        {
                            string label = _buttonLabels[i];
                            if (label == "Continue Game" && Game.MapScreenInstance == null)
                                return;
                            HandleButtonClick(label);
                            break;
                        }
                    }
                }
                break;

            case MenuState.HowToPlay:
            case MenuState.Settings:
                if (mouse.WasButtonPressed(MouseButton.Left))
                {
                    if (_buttons[0].Contains(pos))
                        _currentState = MenuState.MainMenu;

                    if (_volumeBar.Contains(pos))
                    {
                        _volume = (pos.X - _volumeBar.X) / (float)_volumeBar.Width;
                        _volume = MathHelper.Clamp(_volume, 0f, 1f);
                        Game.MasterVolume = _volume;
                        Game.MapScreenInstance?.UpdateVolume();
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
                {
                    Game.SwitchToScreen(Game.MapScreenInstance);
                }
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

        // Draw background scaled to fill virtual screen
        int vw = virtualScreenSize.X;
        int vh = virtualScreenSize.Y;
        float scaleX = (float)vw / _bgTexture.Width;
        float scaleY = (float)vh / _bgTexture.Height;
        float bgScale = Math.Max(scaleX, scaleY);
        Vector2 bgSize = new Vector2(_bgTexture.Width * bgScale, _bgTexture.Height * bgScale);
        Vector2 bgPos = new Vector2((vw - bgSize.X) / 2, (vh - bgSize.Y) / 2);
        spriteBatch.Draw(_bgTexture, bgPos, null, Color.White, 0f, Vector2.Zero, bgScale, SpriteEffects.None, 0f);

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
        DrawScaledText("DreamWard", _titleColor, virtualScreenSize.X / 2, 50, 3f);

        foreach (var (btnRect, idx) in _buttons.WithIndex())
        {
            string label = _buttonLabels[idx];

            // Disable Continue if no game exists
            if (label == "Continue Game" && Game.MapScreenInstance == null)
            {
                DrawButtonDisabled(btnRect, label);
            }
            else
            {
                DrawButton(btnRect, label);
            }
        }
    }

    private void DrawButtonDisabled(Rectangle rect, string text)
    {
        Color disabledColor = new Color(58, 74, 57);
        Color disabledText = new Color(143, 138, 119);

        DrawRoundedRectangle(rect, 10, disabledColor, disabledText);

        Vector2 textSize = _font.MeasureString(text);
        float scale = MathF.Min(rect.Width / textSize.X * 0.9f, rect.Height / textSize.Y * 0.9f);

        Vector2 textPos = new Vector2(
            rect.Center.X - textSize.X * scale / 2,
            rect.Center.Y - textSize.Y * scale / 2
        );

        spriteBatch.DrawString(_font, text, textPos, disabledText, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
    private void DrawHowToPlay()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;
        DrawScaledText("How to Play", _titleColor, w / 2, 50, 2f);

        Rectangle paragraphArea = new Rectangle(w / 10, h / 5, w * 8 / 10, h * 3 / 5 - 60);
        DrawScaledParagraph(_howToPlayText, paragraphArea, _titleColor);

        DrawButton(_buttons[0], "Back");
    }

    private void DrawSettings()
    {
        int w = virtualScreenSize.X;
        int h = virtualScreenSize.Y;
        DrawScaledText("Settings", _titleColor, w / 2, 50, 2f);

        Color barBackgroundColor = new Color(188, 182, 157);
        Color barFillColor = new Color(72, 93, 70);

        DrawRoundedRectangle(_volumeBar, 5, barBackgroundColor, _buttonBorderColor);

        Rectangle fill = new Rectangle(_volumeBar.X, _volumeBar.Y, (int)(_volumeBar.Width * _volume), _volumeBar.Height);
        DrawRoundedRectangle(fill, 5, barFillColor, Color.Transparent);

        string volumeLabel = "Volume";
        Vector2 labelSize = _font.MeasureString(volumeLabel);
        Vector2 labelPos = new Vector2(_volumeBar.Center.X - labelSize.X / 2, _volumeBar.Y - labelSize.Y - 5);
        spriteBatch.DrawString(_font, volumeLabel, labelPos, _buttonTextColor);

        DrawButton(_buttons[0], "Back");
    }

    private void DrawScaledParagraph(string text, Rectangle area, Color color)
    {
        Vector2 size = _font.MeasureString(text);
        float scaleX = area.Width / size.X;
        float scaleY = area.Height / size.Y;
        float scale = Math.Min(scaleX, scaleY);
        Vector2 pos = new Vector2(
            area.X + area.Width / 2 - size.X * scale / 2,
            area.Y + area.Height / 2 - size.Y * scale / 2
        );
        spriteBatch.DrawString(_font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawButton(Rectangle rect, string text)
    {
        DrawRoundedRectangle(rect, 10, _buttonColor, _buttonBorderColor);
        Vector2 textSize = _font.MeasureString(text);
        float scale = MathF.Min(rect.Width / textSize.X * 0.9f, rect.Height / textSize.Y * 0.9f);
        Vector2 textPos = new Vector2(rect.Center.X - textSize.X * scale / 2, rect.Center.Y - textSize.Y * scale / 2);
        spriteBatch.DrawString(_font, text, textPos, _buttonTextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawRoundedRectangle(Rectangle rect, int radius, Color fillColor, Color borderColor)
    {
        spriteBatch.Draw(_pixel, rect, fillColor);

        int thickness = 3;
        if (borderColor != Color.Transparent)
        {
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
}

// Extension to iterate with index
public static class EnumerableExtensions
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
    {
        int i = 0;
        foreach (var item in self)
            yield return (item, i++);
    }
}