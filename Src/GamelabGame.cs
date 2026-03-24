using System;
using System.Collections.Generic;
using System.IO;
using DreamWardUtils.Graphics;
using FontStashSharp;
using Gamelab.Managers;
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
    
    public static Dictionary<string, TextureAtlas> textureAtlas;

    public static GamelabGame Instance { get; private set; }

    public CardScreen CardScreenInstance;
    public MapScreen MapScreenInstance;
    public StartScreen StartScreenInstance;
    public float MasterVolume { get; set; } = 0.5f;

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

    public static CardManager CardManager {get; private set;}
    public static CombatManager CombatManager {get; private set;}
    public static GamePhaseManager GamePhaseManager {get; private set;}
    public static MapManager MapManager {get; private set;}
    public static PathFindingManager PathFindingManager {get; private set;}
    public static SanityManager SanityManager {get; private set;}

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
        CardManager = new();
        CombatManager = new();
        GamePhaseManager = new();
        SanityManager = new();
        Components.Add(screenManager);
    }

    protected override void Initialize()
    {
        base.Initialize();

        //TODO: Replace with actual game name
        Window.Title = "DreamWard";
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

        //to see the changes
        StartScreenInstance = new StartScreen(this);
        screenManager.ShowScreen(StartScreenInstance);
        //screenManager.ShowScreen(new HexMapScreen(this));
        //uncomment the next line to test the sfx with the map (pressing space bar results in a "shuffle sound") -> try it :D
        //SoundTestScreen soundtest = new SoundTestScreen(this);
        //soundtest.SetManager(CardScreenInstance.SanityManager, CardScreenInstance.CardManager);
        //screenManager.ShowScreen(new SoundTestScreen(this));
        //screenManager.ShowScreen(new CardScreen(this));
        logger.Info("Game initialized");
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        
        textureAtlas = new();
        TextureAtlas monsters = TextureAtlas.FromJsonFile(Content, "Sprites/monsters.json");
        textureAtlas.Add("monsters", monsters);
        TextureAtlas night_top = TextureAtlas.FromJsonFile(Content, "Sprites/night_top.json");
        textureAtlas.Add("night_top", night_top);
        TextureAtlas day_top = TextureAtlas.FromJsonFile(Content, "Sprites/day_top.json");
        textureAtlas.Add("day_top", day_top);
        
        //load tilesets
        Texture2D textureDay = Content.Load<Texture2D>("Sprites/day_tiles");
        TextureRegion regionDay = new(textureDay, 0, 0, textureDay.Width, textureDay.Height);
        TileSet setDay = new(regionDay, 64, 32);

        Texture2D textureNight = Content.Load<Texture2D>("Sprites/night_tiles");
        TextureRegion regionNight = new(textureNight, 0, 0, textureNight.Width, textureNight.Height);
        TileSet setNight = new(regionNight, 64, 32);


        MapManager = new MapManager(setDay, setNight, 20, 20, TimeSpan.FromMilliseconds(500));
        MapManager.FillRandomly();
        MapManager.Scale = new Vector2(4, 4);
        MapUtils.Setup(20,20);
        
        //pathfindingManager need the MapManager on init
        PathFindingManager = new();
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
    /// Resets all game managers to their initial state for a new game.
    /// </summary>
    public void ResetGame()
    {
        CardManager = new();
        CombatManager = new();
        GamePhaseManager = new();
        SanityManager = new();

        // Recreate MapManager with fresh tile data
        var setDay = MapManager._tileSets["day"];
        var setNight = MapManager._tileSets["night"];
        MapManager = new MapManager(setDay, setNight, 20, 20, TimeSpan.FromMilliseconds(500));
        MapManager.FillRandomly();
        MapManager.Scale = new Vector2(4, 4);

        PathFindingManager = new();
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
