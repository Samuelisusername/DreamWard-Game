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
    
    public static Dictionary<string, TextureAtlas> TextureAtlas;

    public static GamelabGame Instance { get; private set; }

    public MapScreen MapScreenInstance;
    public StartScreen StartScreenInstance;
    public float MasterVolume { get; set; } = 0.5f;
    public bool IsGameInProgress { get; set; } = false;

    private readonly Logger logger = new("Game");
    public readonly GraphicsDeviceManager graphics;
    public readonly ScreenManager screenManager;
    public readonly FontSystem fontSystem = new();

    public static Texture2D DummyText;
    public static Texture2D NightBackground;
    public static SpriteFont Font;
    public static SpriteFont HintFont;

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
    private MouseCursor dayCursorArrow;
    private MouseCursor dayCursorHover;
    private MouseCursor dayCursorClick;
    private MouseCursor CursorArrow => GamePhaseManager?.IsDay == true ? dayCursorArrow : cursorArrow;
    private MouseCursor CursorHover => GamePhaseManager?.IsDay == true ? dayCursorHover : cursorHover;
    private MouseCursor CursorClick => GamePhaseManager?.IsDay == true ? dayCursorClick : cursorClick;
    private MouseCursor hammerCursor;
    private MouseCursor hammerClickCursor;

    /// <summary>
    /// Set to true during Update to show the hover cursor. Reset automatically each frame.
    /// </summary>
    public static bool IsHovering { get; set; }

    /// <summary>
    /// When true, the click cursor is suppressed even while hovering with a held button (e.g. drag from outside a button onto it).
    /// </summary>
    public static bool SuppressClickCursor { get; set; }

    /// <summary>
    /// When true, the cursor is replaced by the hammer (destroy mode).
    /// </summary>
    public static bool UseHammerCursor { get; set; }

    public static Random Rng { get; private set; }
    public static int Seed { get; private set; }

    public static Matrix RenderTransform { get; set; } = Matrix.Identity;
    public static SamplerState RenderSampler { get; set; } = SamplerState.PointClamp;
    
    private int setSeed = 0; //1870306913; //1512335036;//1029531291; //1330672853; //1036804964; //set this to override seed

    public BalanceParams Balance;
    public Effect LoadingShader;
    public static Effect WhiteFlashShader;

    public GamelabGame(RunMode runMode, int? seed = null)
    {
        Instance = this;

        setSeed = seed ?? setSeed;
        Seed = setSeed == 0 ? new Random().Next(): setSeed;
        Rng = new Random(Seed);

        this.runMode = runMode;

        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";

        contentDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Content.RootDirectory);
        uncompiledContentDir = IsDebug ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", Content.RootDirectory) : null;

        jsonLoader = new(this, "Data");

        Balance = jsonLoader.LoadJson<BalanceParams>("Balance.json");

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
            graphics.HardwareModeSwitch = false; // borderless windowed fullscreen — prevents tabbing out on screenshots
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
        
        TextureAtlas = new();
        TextureAtlas monsters = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/monsters.json");
        TextureAtlas.Add("monsters", monsters);
        TextureAtlas night_top = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/night_top.json");
        TextureAtlas.Add("night_top", night_top);
        TextureAtlas night_top_base = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/night_top_base.json");
        TextureAtlas.Add("night_top_base", night_top_base);
        TextureAtlas day_top = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/day_top.json");
        TextureAtlas.Add("day_top", day_top);
        TextureAtlas day_top_base = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/day_top_base.json");
        TextureAtlas.Add("day_top_base", day_top_base);
        TextureAtlas day_player = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/day_player.json");
        TextureAtlas.Add("day_player", day_player);
        
        DummyText = new Texture2D(GraphicsDevice, 1, 1);
        DummyText.SetData(new[] { Color.White });

        NightBackground = Content.Load<Texture2D>("Sprites/night_background");

        Font = Content.Load<SpriteFont>("infoFont");
        HintFont = Content.Load<SpriteFont>("hintFont");
        LoadingShader = Content.Load<Effect>("ColorOverlay");
        WhiteFlashShader = Content.Load<Effect>("WhiteFlash");
        
        var dayTilesAtlas = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/day_tiles.json");
        dayTiles = new(dayTilesAtlas, "var", "idle");

        var nightTilesAtlas = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/night_tiles.json");
        nightTiles = new(nightTilesAtlas, "var", "idle");


        TextureAtlas interfaceAtlas = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/interface.json");
        TextureAtlas.Add("interface", interfaceAtlas);
        TextureAtlas dayInterfaceAtlas = DreamWardUtils.Graphics.TextureAtlas.FromJsonFile(Content, "Sprites/day_interface.json");
        TextureAtlas.Add("day_interface", dayInterfaceAtlas);

        var interfaceTexture = interfaceAtlas.Texture;
        cursorArrow = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("cursor_arrow0").SourceRectangle, 0, 0, 2);
        cursorHover = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("cursor_hover0").SourceRectangle, 0, 0, 2);
        cursorClick = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("cursor_click0").SourceRectangle, 0, 0, 2);
        var dayInterfaceTexture = dayInterfaceAtlas.Texture;
        dayCursorArrow = CursorFromRegion(dayInterfaceTexture, dayInterfaceAtlas.GetRegion("cursor_arrow0").SourceRectangle, 0, 0, 2);
        dayCursorHover = CursorFromRegion(dayInterfaceTexture, dayInterfaceAtlas.GetRegion("cursor_hover0").SourceRectangle, 0, 0, 2);
        dayCursorClick = CursorFromRegion(dayInterfaceTexture, dayInterfaceAtlas.GetRegion("cursor_click0").SourceRectangle, 0, 0, 2);

        hammerCursor      = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("hammer0").SourceRectangle,       6, 8, 2);
        hammerClickCursor = CursorFromRegion(interfaceTexture, interfaceAtlas.GetRegion("hammer_click0").SourceRectangle, 6, 8, 2);

        Mouse.SetCursor(CursorArrow);

        MapManager = new MapManager(dayTiles, nightTiles, mapSize.x, mapSize.y);
        //pathfindingManager need the MapManager on init
        PathFindingManager = new();

        // Pre-create the map so it can be drawn behind the start screen
        MapScreenInstance = new MapScreen(this, true);
        MapScreenInstance.Initialize();
        MapScreenInstance.LoadContent();
    }
    public static float GameSpeed = 1f;

    public static TimeSpan InGameTime = TimeSpan.Zero;

    protected override void Update(GameTime gameTime)
    {
        GameTime time = new GameTime(gameTime.TotalGameTime, gameTime.ElapsedGameTime * GameSpeed);
        if (nextScreen != null)
        {
            SwitchToScreenImmediately(nextScreen);
            nextScreen = null;
        }
        if(screenManager.ActiveScreen == MapScreenInstance)
        {
            InGameTime += time.ElapsedGameTime;
        }

        IsHovering = false;
        SuppressClickCursor = false;
        base.Update(time);

        var mouseState = Mouse.GetState();
        if (UseHammerCursor)
        {
            Mouse.SetCursor(mouseState.LeftButton == ButtonState.Pressed ? hammerClickCursor : hammerCursor);
        }
        else if (IsHovering)
        {
            Mouse.SetCursor(CursorHover);
            if (mouseState.LeftButton == ButtonState.Pressed && !SuppressClickCursor)
                Mouse.SetCursor(CursorClick);
        }
        else
            Mouse.SetCursor(CursorArrow);
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
        GameSpeed = 1.0f;
        InGameTime = TimeSpan.Zero;
        Seed = setSeed == 0 ? new Random().Next(): setSeed;
        Rng = new Random(Seed);
        logger.Info($"Using seed: {Seed}");
        CardManager = new();
        CombatManager = new();
        GamePhaseManager = new();
        SanityManager = new();

        // Recreate MapManager with fresh tile data
        MapManager = new MapManager(dayTiles, nightTiles, mapSize.x, mapSize.y);
        
        MapManager.FillMap();

        PathFindingManager = new();
        TutorialManager.Reset();
        PopUpRenderer.Reset();
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
