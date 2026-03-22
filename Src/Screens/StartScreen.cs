using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Gamelab.Screens;

public class StartScreen : GamelabGameScreen
{
    private readonly GamelabGame _game;
    private SpriteFont _font;
    private MouseState _prevMouse;

    // Colors
    private readonly Color _bgColor = new Color(8, 48, 43);      
    private readonly Color _titleColor = new Color(188, 182, 157); 
    private readonly Color _buttonColor = new Color(72, 93, 70); 
    private readonly Color _buttonBorderColor = new Color(188, 182, 157); 
    private readonly Color _buttonTextColor = new Color(188, 182, 157); 
    private Rectangle[] _buttons;
    private string[] _buttonLabels = { "Start Game", "How to Play", "Settings", "Quit Game" };

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

    private float _volume = 0.5f; // 0.0 to 1.0
    private Rectangle _volumeBar;

    public StartScreen(GamelabGame game) : base(game) => _game = game;
    private Texture2D _bgTexture;

    public override void LoadContent()
    {
        _font = Content.Load<SpriteFont>("font");
        _bgTexture = Content.Load<Texture2D>("Sprites/Start");
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        int w = _game.GraphicsDevice.Viewport.Width;
        int h = _game.GraphicsDevice.Viewport.Height;

        if (_currentState == MenuState.MainMenu)
        {
            _buttons = new Rectangle[_buttonLabels.Length];
            int btnWidth = w / 4;
            int btnHeight = h / 12;
            int spacing = btnHeight / 2;
            int startY = h / 3;

            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i] = new Rectangle(w / 2 - btnWidth / 2, startY + i * (btnHeight + spacing), btnWidth, btnHeight);
            }
        }
        else
        {
            // Single Back button for HowToPlay and Settings
            _buttons = new Rectangle[]
            {
                new Rectangle(50, _game.GraphicsDevice.Viewport.Height - 100, 150, 50)
            };
        }

        // Volume bar position relative to screen
        if (_currentState == MenuState.Settings)
        {
            _volumeBar = new Rectangle(w / 2 - 200, h / 3, 400, 30);
        }
    }

    public override void Update(GameTime gameTime)
    {
        var m = Mouse.GetState();
        UpdateLayout();

        switch (_currentState)
        {
            case MenuState.MainMenu:
                for (int i = 0; i < _buttons.Length; i++)
                {
                    if (m.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released &&
                        _buttons[i].Contains(m.Position))
                    {
                        HandleButtonClick(_buttonLabels[i]);
                    }
                }
                break;

            case MenuState.HowToPlay:
            case MenuState.Settings:
                if (m.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
                {
                    // Back button
                    if (_buttons[0].Contains(m.Position))
                        _currentState = MenuState.MainMenu;

                    // Only respond if clicking **inside the volume bar**
                    if (_volumeBar.Contains(m.Position))
                    {
                        _volume = (m.X - _volumeBar.X) / (float)_volumeBar.Width;
                        _volume = MathHelper.Clamp(_volume, 0f, 1f);

                        // Store globally so all screens can use it
                        _game.MasterVolume = _volume; // <-- add this in your GamelabGame class
                    }
                }
                break;
        }

        _prevMouse = m;
    }

    private void HandleButtonClick(string label)
    {
        switch (label)
        {
            case "Start Game":
                _game.MapScreenInstance = new MapScreen(_game);
                _game.CardScreenInstance = new CardScreen(_game);
                _game.MapScreenInstance.SetManager(_game.CardScreenInstance.SanityManager, _game.CardScreenInstance.CardManager);
                _game.SwitchToScreen(_game.MapScreenInstance);
                break;
            case "How to Play":
                _currentState = MenuState.HowToPlay;
                break;
            case "Settings":
                _currentState = MenuState.Settings;
                break;
            case "Quit Game":
                _game.Exit();
                break;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        spriteBatch.Begin();
        //spriteBatch.GraphicsDevice.Clear(_bgColor);
        // Draw background image scaled to full screen
        var vp = _game.GraphicsDevice.Viewport;
        float scaleX = (float)vp.Width / _bgTexture.Width;
        float scaleY = (float)vp.Height / _bgTexture.Height;
        float scale = Math.Max(scaleX, scaleY); // fill screen

        Vector2 size = new Vector2(_bgTexture.Width * scale, _bgTexture.Height * scale);
        Vector2 pos = new Vector2((vp.Width - size.X) / 2, (vp.Height - size.Y) / 2);

        spriteBatch.Draw(_bgTexture, pos, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);


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
        // Title
        DrawScaledText("DreamWard", _titleColor, _game.GraphicsDevice.Viewport.Width / 2, 50);

        // Buttons
        foreach (var (btnRect, idx) in _buttons.WithIndex())
        {
            DrawButton(btnRect, _buttonLabels[idx]);
        }
    }

    private void DrawHowToPlay()
    {
        // Title
        DrawScaledText("How to Play", _titleColor, _game.GraphicsDevice.Viewport.Width / 2, 50);

        int w = _game.GraphicsDevice.Viewport.Width;
        int h = _game.GraphicsDevice.Viewport.Height;

        // Paragraph area (80% width, 60% height, leave space for Back button)
        Rectangle paragraphArea = new Rectangle(w / 10, h / 5, w * 8 / 10, h * 3 / 5 - 60);

        // Draw the scaled paragraph
        DrawScaledParagraph(_howToPlayText, paragraphArea, _titleColor);

        // Back button
        DrawButton(_buttons[0], "Back");
    }

    private void DrawScaledParagraph(string text, Rectangle area, Color color)
    {
        // Measure the full text without scaling
        Vector2 size = _font.MeasureString(text);

        // Compute scale to fit width and height
        float scaleX = area.Width / size.X;
        float scaleY = area.Height / size.Y;
        float scale = Math.Min(scaleX, scaleY);

        // Draw text centered in rectangle
        Vector2 pos = new Vector2(
            area.X + area.Width / 2 - size.X * scale / 2,
            area.Y + area.Height / 2 - size.Y * scale / 2
        );

        spriteBatch.DrawString(_font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawSettings()
    {
        // Title
        DrawScaledText("Settings", _titleColor, _game.GraphicsDevice.Viewport.Width / 2, 50);

        // Volume bar position (responsive)
        int w = _game.GraphicsDevice.Viewport.Width;
        int h = _game.GraphicsDevice.Viewport.Height;
        _volumeBar = new Rectangle(w / 2 - 200, h / 3, 400, 30);

        // Colors
        Color barBackgroundColor = new Color(188, 182, 157); 
        Color barFillColor = new Color(72, 93, 70);           

        // Draw volume bar background
        DrawRoundedRectangle(_volumeBar, 5, barBackgroundColor, _buttonBorderColor);

        // Draw filled volume inside bar
        Rectangle fill = new Rectangle(_volumeBar.X, _volumeBar.Y, (int)(_volumeBar.Width * _volume), _volumeBar.Height);
        DrawRoundedRectangle(fill, 5, barFillColor, Color.Transparent);

        // Draw label above the bar (centered)
        string volumeLabel = "Volume";
        Vector2 labelSize = _font.MeasureString(volumeLabel);
        Vector2 labelPos = new Vector2(_volumeBar.Center.X - labelSize.X / 2, _volumeBar.Y - labelSize.Y - 5);
        spriteBatch.DrawString(_font, volumeLabel, labelPos, _buttonTextColor);

        // Back button
        DrawButton(_buttons[0], "Back");
    }

    // Draw button with scaled text to fit
    private void DrawButton(Rectangle rect, string text)
    {
        DrawRoundedRectangle(rect, 10, _buttonColor, _buttonBorderColor);

        Vector2 textSize = _font.MeasureString(text);
        float scale = MathF.Min(rect.Width / textSize.X * 0.9f, rect.Height / textSize.Y * 0.9f); // padding 0.9
        Vector2 textPos = new Vector2(rect.Center.X - textSize.X * scale / 2, rect.Center.Y - textSize.Y * scale / 2);
        spriteBatch.DrawString(_font, text, textPos, _buttonTextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // Draw simple filled rectangle with optional border
    private void DrawRoundedRectangle(Rectangle rect, int radius, Color fillColor, Color borderColor)
    {
        Texture2D pixel = new Texture2D(_game.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });

        spriteBatch.Draw(pixel, rect, fillColor);

        int thickness = 3;
        if (borderColor != Color.Transparent)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), borderColor); // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), borderColor); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), borderColor); // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), borderColor); // Right
        }
    }

    // Draw scaled text centered at (centerX, y)
    private void DrawScaledText(string text, Color color, float centerX, float y)
    {
        Vector2 textSize = _font.MeasureString(text);
        float scale = 1f;
        spriteBatch.DrawString(_font, text, new Vector2(centerX - textSize.X * scale / 2, y), color);
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