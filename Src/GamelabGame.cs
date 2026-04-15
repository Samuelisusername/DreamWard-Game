using System;
using System.Collections.Generic;
using System.IO;
using DreamWardUtils.Graphics;
using FontStashSharp;
using Gamelab.Screens;
using Gamelab.Utils;
using Gamelab.Utils.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    public bool IsGameInProgress { get; set; } = false;

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

    private (int x, int y) mapSize = (30,30);

    private TileSet dayTiles;
    private TileSet nightTiles;
    private MouseCursor cursorArrow;
    private MouseCursor cursorHover;
    private MouseCursor cursorClick;

    /// <summary>
    /// Set to true during Update to show the hover cursor. Reset automatically each frame.
    /// </summary>
    public static bool IsHovering { get; set; }

    public GamelabGame(RunMode runMode)
    {
        Instance = this;

        this.runMode = runMode;

        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";

        contentDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Content.RootDirectory);
        uncompiledContentDir = IsDebug ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", Content.RootDirectory) : null;

        jsonLoader = new(this, "Data");

        screenManager = new();
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
        
        //load tilesets — read frame duration from the JSON atlas so it stays in sync with entity animations
        var dayTilesAtlas = TextureAtlas.FromJsonFile(Content, "Sprites/day_tiles.json");
        TextureRegion regionDay = new(dayTilesAtlas.Texture, 0, 0, dayTilesAtlas.Texture.Width, dayTilesAtlas.Texture.Height);
        dayTiles = new(regionDay, 64, 32, dayTilesAtlas.GetAnimation("var0_idle").Delay);

        var nightTilesAtlas = TextureAtlas.FromJsonFile(Content, "Sprites/night_tiles.json");
        TextureRegion regionNight = new(nightTilesAtlas.Texture, 0, 0, nightTilesAtlas.Texture.Width, nightTilesAtlas.Texture.Height);
        nightTiles = new(regionNight, 64, 32, nightTilesAtlas.GetAnimation("var0_idle").Delay);


        TextureAtlas interfaceAtlas = TextureAtlas.FromJsonFile(Content, "Sprites/interface.json");
        textureAtlas.Add("interface", interfaceAtlas);

        var interfaceTexture = interfaceAtlas.Texture;
        cursorArrow = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("cursor_arrow0").SourceRectangle, 0, 0, 2);
        cursorHover = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("cursor_hover0").SourceRectangle, 0, 0, 2);
        cursorClick = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("cursor_click0").SourceRectangle, 0, 0, 2);
        Mouse.SetCursor(cursorArrow);

        MapManager = new MapManager(dayTiles, nightTiles, mapSize.x, mapSize.y);
        //pathfindingManager need the MapManager on init
        PathFindingManager = new();

        // Pre-create the map so it can be drawn behind the start screen
        MapScreenInstance = new MapScreen(this);
        MapScreenInstance.Initialize();
        MapScreenInstance.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (nextScreen != null)
        {
            SwitchToScreenImmediately(nextScreen);
            nextScreen = null;
        }

        IsHovering = false;
        base.Update(gameTime);

        var mouseState = Mouse.GetState();
        if (IsHovering){
            Mouse.SetCursor(cursorHover);
            if (mouseState.LeftButton == ButtonState.Pressed)
                Mouse.SetCursor(cursorClick);
        }
        else
            Mouse.SetCursor(cursorArrow);
    }

    private MouseCursor CursorFromRegion(Texture2D source, Rectangle region, int originX, int originY, int scale = 1)
    {
        var src = new Color[region.Width * region.Height];
        source.GetData(0, region, src, 0, src.Length);

        int w = region.Width * scale;
        int h = region.Height * scale;
        var dst = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dst[y * w + x] = src[(y / scale) * region.Width + (x / scale)];

        var texture = new Texture2D(GraphicsDevice, w, h);
        texture.SetData(dst);
        return MouseCursor.FromTexture2D(texture, (originX + 2) * scale, originY * scale);
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
        MapManager = new MapManager(dayTiles, nightTiles, mapSize.x, mapSize.y);

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
