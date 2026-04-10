using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using Gamelab.Managers;

namespace Gamelab.Screens;

public class MapScreen : GamelabGameScreen
{
    
    private MapManager mapManager => GamelabGame.MapManager;
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CardManager cardManager => GamelabGame.CardManager;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    private readonly MapRenderer mapRenderer;
    private readonly EntityRenderer entityRenderer;
    private readonly CardRenderer cardRenderer;


    // Cards
    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private Dictionary<string, Rectangle> _cardRegions;

    // Navigation button
    private Rectangle _toCardScreenBtn;
    private bool _showInventory = true;
    private Rectangle _inventoryBtn;
    private bool _showTowerRange = false;
    private Rectangle _towerRangeBtn;
    private int _cardStartY = 0;

    // Drag
    private bool _isDrag;
    private bool _clickStarted;
    private Vector2 _oldPos;
    private (int col, int row) _hoverTile = (-1,-1);
    private bool _showDayPopup = false;
    private string _dayPopupText = "";
    private float _popupTimer = 0f;
    private Structure _hoveredStructure;
 
    private readonly Color BTN_COLOR = new Color(0xbc, 0xb6, 0x9d);  
    private readonly Color BTN_TEXT_COLOR = new Color(0x74, 0x70, 0x5f);    
    private readonly Color TEXT_COLOR = new Color(0xd8, 0xd4, 0xc1);

    private Point screenSize => viewportAdapter.PointToScreen(new Point(GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth, GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight));


    public MapScreen(GamelabGame game) : base(game)
    {
        // Map setup
        mapRenderer = new MapRenderer();
        entityRenderer = new EntityRenderer();
        cardRenderer = new CardRenderer(); 
        SpawnDefaultEntities();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        AudioManager.LoadSound(Game.Content, "sheep_baa", "Sounds/sheep_baa");
        AudioManager.LoadSound(Game.Content, "dragonflap", "Sounds/dragonflap");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/action_cards");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/buildings");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/demons");

        AudioManager.LoadSound(Game.Content, "day_music", "Sounds/preferred_option_day");
        AudioManager.LoadSound(Game.Content, "night_music", "Sounds/creepy_demon_night_forest") ;
        AudioManager.PlayMusic("day_music");

        // Fonts and dummy texture
        _font = Content.Load<SpriteFont>("font");
        _dummy = new Texture2D(GraphicsDevice, 1, 1);
        _dummy.SetData(new[] { Color.White });

        // 1x1 white texture used by EntityRenderer to draw HP bars
        entityRenderer.SetHealthBarTexture(_dummy);

        // Card textures
        _cardTexture = Content.Load<Texture2D>("Sprites/cards");
        _cardRegions = new()
        {
            { "sniper", new(0,0,128,128) },
            { "aoe", new(0,128,128,128) },
            { "dreamcatcher", new(0,256,128,128) },
            { "fence", new(0,384,128,128) },
            { "meadow", new(0,512,128,128) },
            { "polymorph", new(0,1280,128,128) },
            { "move", new(0,1408,128,128) },
            { "destroy", new(0,1536,128,128) }
        };


        // Navigation button
        _toCardScreenBtn = new Rectangle((int)viewportAdapter.VirtualWidth - 150,20,130,50);
        
        mapManager.InitOffset(screenSize);
    }

    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        // Exit
        if (keyboard.WasKeyReleased(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
            Game.SwitchToScreen(Game.StartScreenInstance);
        if(keyboard.WasKeyReleased(Keys.C))
        {
            Game.SwitchToScreen(Game.CardScreenInstance);
            AudioManager.Play("dragonflap");
            return;
        }
        // Spawn monster at edge heading toward tent
        if (keyboard.WasKeyPressed(Keys.Space))
        {
            int centerX = mapManager.Columns / 2;
            int centerY = mapManager.Rows / 2;
#if DEBUG
            AudioManager.Play("grunt_03");
            gamePhaseManager.SwitchToNight();
#else
            if(gamePhaseManager.IsDay){
                AudioManager.Play("grunt_03");
                gamePhaseManager.SwitchToNight();
            }
#endif

        }

        if (keyboard.WasKeyPressed(Keys.P))
        {
            Game.StartScreenInstance ??= new StartScreen(Game);
            Game.SwitchToScreen(Game.StartScreenInstance);
            return;
        }

        if(Game.IsActive)
        {
            // Zoom before mapupdates so entity scale is synced
            if (mouse.DeltaScrollWheelValue != 0)
            {
                float delta = mouse.DeltaScrollWheelValue * (-0.01f);
                mapManager.Zoom(delta, viewportAdapter.PointToScreen(mouse.Position).ToVector2(), screenSize);
            }
        }

        if (_showDayPopup)
        {
            _popupTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_popupTimer <= 0f)
            {
                _showDayPopup = false;
            }
        }
        

        // Map update
        mapManager.Update(gameTime);

        if(Game.IsActive)
        {
            if (mapManager.TentDestroyed)
            {
                Game.SwitchToScreen(new GameOverScreen(Game));
                return;
            }


            // Drag
            if (mouse.WasButtonPressed(MouseButton.Left))
            {
                _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                _clickStarted = true;
            }

            if (mouse.IsButtonDown(MouseButton.Left) && _clickStarted)
            {
                Vector2 deltaPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _oldPos;
                if (deltaPos.Length() > 0)
                {
                    mapManager.MoveMap(deltaPos, screenSize);
                    _isDrag = true;
                    _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                }
            }

            if (mouse.WasButtonReleased(MouseButton.Left) && _clickStarted)
            {
                if(GamelabGame.GamePhaseManager.IsDay && GraphicsDevice.PresentationParameters.Bounds.Contains(mouse.Position) && !_isDrag)
                {
                    Vector2 pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                    // Navigation to CardScreen
                    if (_toCardScreenBtn.Contains(pos))
                    {
                        Game.SwitchToScreen(Game.CardScreenInstance);
                        AudioManager.Play("dragonflap");
                        return;
                    }
                    if (_inventoryBtn.Contains(pos))
                    {
                        _showInventory = !_showInventory;
                        return;
                    }

                    if (_towerRangeBtn.Contains(pos))
                    {
                        _showTowerRange = !_showTowerRange;
                        return;
                    }
                    // Check USE buttons on cards
                    if (cardManager.HandleCardUseClicks(pos, (int)viewportAdapter.VirtualWidth, (int)viewportAdapter.VirtualHeight))
                        return;

                    if (cardManager.HandleMapCardUse(mapManager, pos))
                    {
                        return;
                    }
                    mapManager.TryMoveSheep(pos);
                }
                _clickStarted = false;
                _isDrag = false;
            }
            Vector2 mouseWorld = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            _hoverTile = mapManager.GetTileFromScreenPosition(mouseWorld);
            if (_hoverTile.col >= 0 && cardManager._activeMapCard != null &&
                (cardManager._activeMapCard.Effect == CardEffect.Move ||
                 cardManager._activeMapCard.Effect == CardEffect.Destroy ||
                 cardManager._activeMapCard.Effect == CardEffect.Polymorph))
            {
                _hoveredStructure = mapManager.GetStructure(_hoverTile);
            }
            else
            {
                _hoveredStructure = null;
            }
        }        
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GraphicsDevice.Clear(GamelabGame.GamePhaseManager.IsDay ? Color.CornflowerBlue : Color.Black);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());

        // ✅ Map
        mapRenderer.Draw(spriteBatch);
        if (cardManager._activeMapCard != null &&
            (cardManager._activeMapCard.Effect == CardEffect.Move ||
            cardManager._activeMapCard.Effect == CardEffect.Destroy ||
            cardManager._activeMapCard.Effect == CardEffect.Polymorph) &&
            _hoverTile.col >= 0)
        {
            entityRenderer.Draw(spriteBatch, _hoverTile);
        }
        else
        {
            entityRenderer.Draw(spriteBatch);
        }
        if (_showTowerRange && GamelabGame.GamePhaseManager.IsDay)
        {
            cardRenderer.DrawAllTowerRanges(spriteBatch, mapManager);
        }
        cardRenderer.DrawPreview(spriteBatch, cardManager, mapManager, _hoverTile);
        
        // draw sanity
        if (sanityManager != null)
            {
                string text = $"Sanity: {sanityManager.Sanity}";
                spriteBatch.DrawString(_font, text,new Vector2(20, 20),TEXT_COLOR);

                string dayText = $"Day: {gamePhaseManager.DayCount}/10";
                spriteBatch.DrawString(_font, dayText,new Vector2(20, 50),TEXT_COLOR);

                string phaseText = gamePhaseManager.IsDay ? "Phase: Day" : "Phase: Night";
                spriteBatch.DrawString(_font, phaseText,new Vector2(20, 80),TEXT_COLOR);
            }

        if(GamelabGame.GamePhaseManager.IsDay)
        {
            // ✅ UI (now also scaled)
            DrawButton(_toCardScreenBtn, "Cards");
            cardRenderer.UpdateUIButtonLayout((int)viewportAdapter.VirtualWidth, (int)viewportAdapter.VirtualHeight, _showInventory, _font, ref _cardStartY, ref _inventoryBtn, ref _towerRangeBtn);
            DrawButton(_inventoryBtn, _showInventory ? "Hide Cards" : "Show Cards");
            DrawButton(_towerRangeBtn, _showTowerRange ? "Hide Range" : "Tower Range");

            if (_showInventory)
            {
                cardRenderer.DrawCardSlots(spriteBatch, _dummy, _cardTexture, _cardRegions, _font, cardManager, _cardStartY, (int)viewportAdapter.VirtualWidth, (int)viewportAdapter.VirtualHeight);
            }
        }
        if (_showDayPopup)
        {
            DrawDayPopup();
        }
        spriteBatch.End();
    }

    // ------------------------
    // Draw Helpers
    // ------------------------

    private void DrawDayPopup()
    {
        int w = (int)viewportAdapter.VirtualWidth;
        int h = (int)viewportAdapter.VirtualHeight;

        // Make the popup cover at least half the screen
        int boxWidth = w / 2;
        int boxHeight = h / 2;
        Rectangle box = new(
            w / 2 - boxWidth / 2,
            h / 2 - boxHeight / 2,
            boxWidth,
            boxHeight
        );

        // Faint white background (semi-transparent)
        spriteBatch.Draw(_dummy, box, Color.White * 0.6f);

        // Bigger text
        float scale = 1.5f; // scale up font
        Vector2 textSize = _font.MeasureString(_dayPopupText) * scale;
        Vector2 textPos = new Vector2(box.Center.X - textSize.X / 2, box.Center.Y - textSize.Y / 2);

        spriteBatch.DrawString(_font, _dayPopupText, textPos, BTN_TEXT_COLOR, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
    private void DrawButton(Rectangle rect, string text)
    {
        spriteBatch.Draw(_dummy, rect, BTN_COLOR);
        var size = _font.MeasureString(text);
        spriteBatch.DrawString(_font, text,
            new Vector2(rect.Center.X - (size.X * 0.6f) / 2,
                        rect.Center.Y - (size.Y * 0.6f) / 2),
            BTN_TEXT_COLOR, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    public void ShowDayPopup(string text)
    {
        _dayPopupText = text;
        _showDayPopup = true;
        _popupTimer = 3f; // seconds
    }
    
    private void SpawnDefaultEntities()
    {
        var atlas = GamelabGame.textureAtlas["night_top"];

        int centerX = mapManager.Columns / 2;
        int centerY = mapManager.Rows / 2;

        // 🏕️ Tent at center
        mapManager.PlaceEntity(new Tent("tent_idle",
            (centerX, centerY)
        ), (centerX, centerY));

        Meadow starterMeadow = new Meadow("meadow_idle",
            (centerX + 1, centerY), 5
        );

        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX, centerY + 1)), (centerX, centerY + 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX, centerY - 1)), (centerX, centerY - 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX-1, centerY - 1)), (centerX-1, centerY - 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX-1, centerY )), (centerX-1, centerY ));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 1, centerY + 1)), (centerX + 1, centerY + 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 1, centerY -1)), (centerX + 1, centerY -1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 2, centerY)), (centerX + 2, centerY ));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 2, centerY + 1)), (centerX + 2, centerY + 1));


        // 🐑 Sheep next to tent
        mapManager.PlaceSheep(starterMeadow ,(centerX + 1, centerY));

        mapManager.PlaceEntity(new Meadow("meadow_idle",
            (8, 7), 10
        ) ,(8, 7));

        
        mapManager.PlaceEntity(new Meadow("meadow_idle",
            (15, 9), 10
        ) ,(15, 9));


        // 🌲 Random trees
        SpawnTrees(25, centerX, centerY);
    }

    private void SpawnTrees(int count, int centerX, int centerY)
    {
        mapManager.AddTrees(new List<(int col, int row)>());
        // string[] treeAnims = { "treeBig_idle", "treeSmall_idle" };
        // for (int row = 0; row < mapManager.Rows; row++)
        // {
        //     for (int col = 0; col < mapManager.Columns; col++)
        //     {
        //         if (mapManager.GetStructure((col, row)) != null) continue;

        //         string anim = treeAnims[System.Random.Shared.Next(treeAnims.Length)];
        //         var tree = new TreeTile(anim, (col, row));
        //         mapManager.PlaceEntity(tree, (col, row));
        //     }
        // }
        // int cols = mapManager.Columns;
        // int rows = mapManager.Rows;
        // int placed = 0;
        // int attempts = 0;

        // while (placed < count && attempts < 200)
        // {
        //     attempts++;
        //     int col = System.Random.Shared.Next(cols);
        //     int row = System.Random.Shared.Next(rows);

        //     // Don't place near the tent (keep a 3-tile radius clear)
        //     int dx = System.Math.Abs(col - centerX);
        //     int dy = System.Math.Abs(row - centerY);
        //     if (dx + dy <= 3) continue;

        //     // Don't place on edges (leave room for monster spawns)
        //     if (col == 0 || col == cols - 1 || row == 0 || row == rows - 1) continue;

        //     // Don't place on occupied tiles
        //     if (mapManager.GetStructure((col, row)) != null) continue;

        //     string anim = treeAnims[System.Random.Shared.Next(treeAnims.Length)];
        //     var tree = new TreeTile(anim, (col, row));
        //     mapManager.PlaceEntity(tree, (col, row));
        //     placed++;
        // }
    }
}