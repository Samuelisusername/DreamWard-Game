using System;
using System.IO;
using FontStashSharp;
using Gamelab.Screens;
using Gamelab.Utils;
using Gamelab.Utils.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Screens;

namespace Gamelab;

public class GamelabGame : Game
{
    public enum RunMode
    {
        Release,
        Debug,
    }

    public static GamelabGame Instance { get; private set; }

    private readonly Logger logger = new("Game");
    public readonly GraphicsDeviceManager graphics;
    public readonly ScreenManager screenManager;
    public readonly FontSystem fontSystem = new();

    /// <summary>
    /// The directory where the compiled content files are available during runtime
    /// </summary>
    public readonly string contentDir;

    /// <summary>
    /// ONLY FOR DEBUG: The directory where the uncompiled content files are available.
    /// </summary>
    public readonly string uncompiledContentDir;
    public readonly JsonLoader jsonLoader;

    public RunMode runMode { get; private set; }
    public bool IsDebug => runMode == RunMode.Debug;
    public bool IsRelease => runMode == RunMode.Release;

    private GamelabGameScreen nextScreen;
    private string screenshotPath;

    public bool IsRunning => screenManager.ActiveScreen != null;

    public GamelabGame(RunMode runMode)
    {
        Instance = this;

        this.runMode = runMode;

        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";

        contentDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Content.RootDirectory);
        uncompiledContentDir = IsDebug ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", Content.RootDirectory) : null;

        jsonLoader = new(this, "Data");

        screenManager = new ScreenManager();
        Components.Add(screenManager);
    }

    protected override void Initialize()
    {
        base.Initialize();

        //TODO: Replace with actual game name
        Window.Title = "Gamelab Game";
        IsMouseVisible = true;

        if (IsDebug)
        {
            // For debugging, show a smaller window that is resizable
            Window.AllowUserResizing = true;
            graphics.PreferredBackBufferWidth = 854;
            graphics.PreferredBackBufferHeight = 480;
        }
        else
        {
            // For release, render in fullscreen
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            graphics.PreferredBackBufferWidth = displayMode.Width;
            graphics.PreferredBackBufferHeight = displayMode.Height;
            graphics.IsFullScreen = true;
        }
        graphics.ApplyChanges();

        var fontPath = Path.Combine(contentDir, "promptfont.ttf");
        var fontBytes = File.ReadAllBytes(fontPath);
        fontSystem.AddFont(fontBytes);

        screenManager.ShowScreen(new MainMenuScreen(this));
        logger.Info("Game initialized");
    }

    protected override void Update(GameTime gameTime)
    {
        if (nextScreen != null)
        {
            SwitchToScreenImmediately(nextScreen);
            nextScreen = null;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        base.Draw(gameTime);

        if (!string.IsNullOrEmpty(screenshotPath))
        {
            SaveScreenshotImmediately(screenshotPath);
            screenshotPath = null;
        }
    }

    /// <summary>
    /// Switch to the specified screen on the next update. Not transitioning immediately allows the current screen to finish its update and draw cycle, which can help avoid issues with switching screens in the middle of their logic.
    /// </summary>
    /// <param name="screen">The screen to switch to.</param>
    public void SwitchToScreen(GamelabGameScreen screen)
    {
        nextScreen = screen;
    }

    protected void SwitchToScreenImmediately(GamelabGameScreen screen)
    {
        screenManager.ReplaceScreen(screen);
    }

    /// <summary>
    /// Save a screenshot at the end of the next draw cycle.
    /// </summary>
    /// <param name="path">The path where the screenshot will be saved.</param>
    public void SaveScreenshot(string path)
    {
        screenshotPath = path;
    }

    protected void SaveScreenshotImmediately(string path)
    {
        var pp = GraphicsDevice.PresentationParameters;
        var width = pp.BackBufferWidth;
        var height = pp.BackBufferHeight;

        var data = new byte[width * height * 4];
        GraphicsDevice.GetBackBufferData(data);

        var texture = new Texture2D(GraphicsDevice, width, height, false, pp.BackBufferFormat);
        texture.SetData(data);

        var parentDirPath = Path.GetDirectoryName(path);
        Directory.CreateDirectory(parentDirPath);

        using (var stream = File.Create(path))
        {
            texture.SaveAsPng(stream, texture.Width, texture.Height);
        }

        texture.Dispose();
    }
}
