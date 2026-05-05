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

public abstract class GamelabGameScreen(GamelabGame game) : GameScreen(game)
{
    protected ViewportAdapter viewportAdapter;
    protected SpriteBatch spriteBatch;
    protected Point virtualScreenSize = new(1920, 1080);
    public int NumFramesDrawn { get; private set; }

    public new GamelabGame Game => (GamelabGame)base.Game;

    public override void Initialize()
    {
        base.Initialize();

        RebuildViewportAdapter();
        Game.Window.ClientSizeChanged += (_, _) => RebuildViewportAdapter();

        spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    private void RebuildViewportAdapter()
    {
        var pp = GraphicsDevice.PresentationParameters;
        float screenAspect = (float)pp.BackBufferWidth / pp.BackBufferHeight;
        int virtualHeight = 1080;
        int virtualWidth = (int)Math.Round(virtualHeight * screenAspect);
        virtualScreenSize = new Point(virtualWidth, virtualHeight);

        viewportAdapter = new ScalingViewportAdapter(GraphicsDevice, virtualWidth, virtualHeight);
        viewportAdapter.Reset();
    }

    public override void Update(GameTime gameTime)
    {
        // Get keyboard and all gamepad states for input handling
        MouseExtended.Update();
        KeyboardExtended.Update();
        var mouse = MouseExtended.GetState();
        var keyboard = KeyboardExtended.GetState();
        var gamePads = new Dictionary<int, GamePadState>();
        for (int i = 0; i < GamePad.MaximumGamePadCount; i++)
        {
            var playerIndex = (PlayerIndex)i;
            var capabilities = GamePad.GetCapabilities(playerIndex);
            if (!capabilities.IsConnected)
            {
                continue;
            }

            var gamePad = GamePad.GetState(playerIndex);
            gamePads.Add(i, gamePad);
        }

        Update(gameTime, keyboard, gamePads, mouse);
    }

    protected virtual void Update(GameTime gameTime, KeyboardStateExtended keyboard, Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
    }

    public override void Draw(GameTime gameTime)
    {
        NumFramesDrawn++;
    }

    protected enum ButtonResult { Normal, Hovered, Pressed, Clicked }

    protected ButtonResult UpdateButton(Rectangle rect, MouseStateExtended mouse, Vector2 mousePos, ref bool clickStartedInside, bool active = true)
    {
        if (!active)
        {
            if (mouse.WasButtonPressed(MouseButton.Left) || mouse.WasButtonReleased(MouseButton.Left))
                clickStartedInside = false;
            return ButtonResult.Normal;
        }
        bool over = rect.Contains(mousePos);
        bool held = mouse.IsButtonDown(MouseButton.Left);

        if (mouse.WasButtonPressed(MouseButton.Left))
            clickStartedInside = over;

        bool externalHold    = held && !clickStartedInside;
        bool visuallyPressed = clickStartedInside && held && over;
        bool clicked         = mouse.WasButtonReleased(MouseButton.Left) && clickStartedInside && over;

        if (mouse.WasButtonReleased(MouseButton.Left))
            clickStartedInside = false;

        if (over)
        {
            GamelabGame.IsHovering = true;
            if (externalHold)
                GamelabGame.SuppressClickCursor = true;
        }

        if (clicked)         return ButtonResult.Clicked;
        if (visuallyPressed) return ButtonResult.Pressed;
        if (over)            return ButtonResult.Hovered;
        return ButtonResult.Normal;
    }

    public class Factory
    {
        public string name;
        private readonly Func<GamelabGame, GamelabGameScreen> factory;

        public Factory(string name, Func<GamelabGame, GamelabGameScreen> factory)
        {
            this.name = name;
            this.factory = factory;
        }

        public GamelabGameScreen Instantiate(GamelabGame game) => factory(game);

        public override string ToString() => name;
    }

    /// <summary>
    /// Returns a list of factory functions that create instances of all screens in the game.
    /// The instances created by these factories will be tested in the CICD pipeline.
    /// If you have parameterized screens or have to exclude unused screens, modify this logic.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<Factory> GetScreenFactories()
    {
        var screenTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(GamelabGameScreen)) && !t.IsAbstract)
            .ToList();

        return screenTypes.Select(t => new Factory(t.Name, game => {
            return (GamelabGameScreen)Activator.CreateInstance(t, game);
        }));
    }
}