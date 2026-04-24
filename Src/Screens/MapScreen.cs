using System;
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
    private CombatManager combatManager => GamelabGame.CombatManager;
    private readonly MapRenderer mapRenderer;
    private readonly EntityRenderer entityRenderer;
    private readonly CardRenderer cardRenderer;
    private Texture2D _dayTopTexture;
    private Texture2D _dayPlayerTexture;

    // The exact coordinates from your JSON files
    private readonly Rectangle _sheepPortraitRect = new Rectangle(0, 1408, 128, 128); 
    private readonly Rectangle _humanPortraitRect = new Rectangle(0, 0, 16, 32);
    private Rectangle _speedBtn;
    private bool _speedBtnPressed = false;
    private bool _is_active_tutorial = false;
    private Rectangle _tutorial_btn;
    private int _tutorial_step = 0;
    private bool _tutorial_btn_pressed = false; 
    private float _textTimer = 0f;
    private float _zoomTimer = 0f;
    private int _charIndex = 0;
    private int _previousTutorialStep = -1;


    // Cards
    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private Dictionary<string, Rectangle> _cardRegions;

    // Navigation button
    private bool _showInventory = true;
    private Rectangle _inventoryBtn;
    private bool _showTowerRange = false;
    private Rectangle _towerRangeBtn;
    private bool _offsetInitialized = false;
    private int _cardStartY = 0;
    private bool _inventoryBtnPressed  = false;
    private bool _towerRangeBtnPressed = false;
    
    private Rectangle _startNightBtn;
    private TimeSpan _startNightTime = TimeSpan.FromSeconds(1);
    private TimeSpan _elapsedStartNightTime = TimeSpan.Zero;
    private bool _resetStartNightTimer = true;

    // Drag
    private bool _isDrag;
    private bool _clickStarted;
    private Vector2 _oldPos;
    private (int col, int row) _hoverTile = (-1,-1);
    private bool _showDayPopup = false;
    private string _dayPopupText = "";
    private float _popupTimer = 0f;
    private Structure _hoveredStructure;
    private Texture2D _interfaceTexture;
    private Texture2D _dayInterfaceTexture;
    private double _lastClickTime = 0;
    private const double DOUBLE_CLICK_TIME = 300; // ms
    private int _restoredSavedSlotThisDay = -1;
    private Texture2D InterfaceTexture => gamePhaseManager.IsDay ? _dayInterfaceTexture : _interfaceTexture;
    private static readonly Rectangle BtnNormal  = new(0, 176, 80, 32); // button_normal_map
    private static readonly Rectangle BtnPressed = new(0, 208, 80, 32); // button_pressed_map
    private readonly Color COLOR_D = new(0x29, 0x24, 0x10);    
    private readonly Color COLOR_L = new Color(0xc7, 0xb8, 0x9c);

    private Point screenSize => viewportAdapter.PointToScreen(new Point(GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth, GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight));


    public MapScreen(GamelabGame game) : base(game)
    {
        // Map setup
        mapRenderer = new MapRenderer();
        entityRenderer = new EntityRenderer();
        cardRenderer = new CardRenderer(); 
        mapManager.fillMap();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        AudioManager.LoadSound(Game.Content, "sheep_baa", "Sounds/sheep_baa");
        AudioManager.LoadSound(Game.Content, "dragonflap", "Sounds/dragonflap");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/action_cards");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/buildings");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/demons");
        AudioManager.LoadAllSounds(Game.Content,"Sounds/rustle");

        AudioManager.LoadSound(Game.Content, "day_music", "Sounds/preferred_option_day");
        AudioManager.LoadSound(Game.Content, "night_music", "Sounds/creepy_demon_night_forest") ;
        

        // Fonts and dummy texture
        _font = Content.Load<SpriteFont>("font");
        _dummy = new Texture2D(GraphicsDevice, 1, 1);
        _dummy.SetData(new[] { Color.White });
        // texture for tutorial 
        _dayTopTexture = Content.Load<Texture2D>("Sprites/day_top");
        _dayPlayerTexture = Content.Load<Texture2D>("Sprites/day_player");

        // 1x1 white texture used by EntityRenderer to draw HP bars
        entityRenderer.SetHealthBarTexture(_dummy);
        _interfaceTexture = Content.Load<Texture2D>("Sprites/interface");
        _dayInterfaceTexture = Content.Load<Texture2D>("Sprites/day_interface");

        // Card textures
        _cardTexture = Content.Load<Texture2D>("Sprites/cards");
        _cardRegions = new()
        {
            { "sniper",       new(0,    0, 128, 128) },
            { "aoe",          new(0,  128, 128, 128) },
            { "dreamcatcher", new(0,  256, 128, 128) },
            { "fence",        new(0,  384, 128, 128) },
            { "meadow",       new(0,  512, 128, 128) },
            { "shuffle",      new(0,  640, 128, 128) },
            { "discount",     new(0,  768, 128, 128) },
            { "top3",         new(0,  896, 128, 128) },
            { "discard",      new(0, 1024, 128, 128) },
            { "deck",         new(0, 1152, 128, 128) },
            { "polymorph",    new(0, 1280, 128, 128) },
            { "move",         new(0, 1408, 128, 128) },
            { "destroy",      new(0, 1536, 128, 128) },
            { "void",         new(0, 1664, 128, 128) },
            { "spikes",       new(0, 1792, 128, 128) },
            { "decoy",        new(0, 1920, 128, 128) },
            { "buff",         new(0, 2048, 128, 128) }

        };
        
        if (!_offsetInitialized)
        {
            mapManager.InitOffset(screenSize);
            _offsetInitialized = true;
        }
    }

    public void StartNewDay(int day)
    {
        _restoredSavedSlotThisDay = cardManager.SavedSlotIndex;
        cardManager.GenerateDailyCards(day);
        cardRenderer.SetRestoredSavedSlot(_restoredSavedSlotThisDay);
        cardRenderer.CollapseExpanded();
    }

    private void RebuildButtonLayout()
    {
        int vw = viewportAdapter.VirtualWidth;
        int vh = viewportAdapter.VirtualHeight;
        _tutorial_btn = new Rectangle(vw - 240, 20, 220, 70);
        _speedBtn = new Rectangle(vw - 240, 20, 220, 70);
        if (!gamePhaseManager.IsInTransition)
        {
            cardRenderer.UpdateUIButtonLayout(vw, vh, _showInventory,_font, ref _cardStartY, ref _inventoryBtn, ref _towerRangeBtn, ref _startNightBtn);
        }
    }
    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        if(_resetStartNightTimer)
        {
            _elapsedStartNightTime = TimeSpan.Zero;
        }
        _resetStartNightTimer = true;
        AudioManager.PlayMusic(gamePhaseManager.IsDay ? "day_music" : "night_music");
                
        GamelabGame.IsHovering = false;
        Vector2 mousePos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
        // ParticleManager.AddParticle(new Particle
        // {
        //     WorldPosition = (mousePos - mapManager.Offset)/mapManager.Scale,
        //     Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 20, (float)(Random.Shared.NextDouble() -0.5f) * 20),
        //     LifeSpan = TimeSpan.FromSeconds(3),
        //     Color = Color.Orange
        // });
        cardRenderer.SetMousePos(mousePos);
        RebuildButtonLayout();
        

        if(Game.IsActive)
        {
            // Exit
            if (keyboard.WasKeyReleased(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
            Game.SwitchToScreen(Game.StartScreenInstance);
            
            // Spawn monster at edge heading toward tent
            
            if (keyboard.WasKeyPressed(Keys.Space) && _is_active_tutorial)
            {
                _tutorial_step++;
                //reached tutorial end
                if(_tutorial_step == 25)
                {  
                    _is_active_tutorial = false; 
                }
                _clickStarted = false;
            }

            if (keyboard.WasKeyPressed(Keys.P))
            {
                Game.StartScreenInstance ??= new StartScreen(Game);
                Game.SwitchToScreen(Game.StartScreenInstance);
                return;
            }
            // Zoom before mapupdates so entity scale is synced
            if (mouse.DeltaScrollWheelValue != 0)
            {
                float delta = mouse.DeltaScrollWheelValue * (-0.01f);
                mapManager.Zoom(delta, viewportAdapter.PointToScreen(mouse.Position).ToVector2(), screenSize);
            }
            if (gamePhaseManager.IsDay && _showInventory)
            {
                cardRenderer.HandleHover(mousePos, cardManager);
            }
            else
            {
                cardRenderer.CollapseExpanded();
            }
#if DEBUG
            // Build cards 
            if (keyboard.WasKeyReleased(Keys.D1)) cardManager.ForceSpawnInto("sniper",        cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D2)) cardManager.ForceSpawnInto("aoe",           cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D3)) cardManager.ForceSpawnInto("meadow",        cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D4)) cardManager.ForceSpawnInto("dreamcatcher",  cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D5)) cardManager.ForceSpawnInto("fence",         cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.E))  cardManager.ForceSpawnInto("void",          cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.R))  cardManager.ForceSpawnInto("spikes",        cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.T))  cardManager.ForceSpawnInto("decoy",         cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.Y))  cardManager.ForceSpawnInto("buff",          cardManager.BuildSlots);
            // Map action cards
            if (keyboard.WasKeyReleased(Keys.D6)) cardManager.ForceSpawnInto("polymorph",     cardManager.MapSlots);
            if (keyboard.WasKeyReleased(Keys.D7)) cardManager.ForceSpawnInto("destroy",       cardManager.MapSlots);
            if (keyboard.WasKeyReleased(Keys.D8)) cardManager.ForceSpawnInto("move",          cardManager.MapSlots);
            // Stack action cards
            if (keyboard.WasKeyReleased(Keys.D9)) cardManager.ForceSpawnInto("shuffle",       cardManager.StackSlots);
            if (keyboard.WasKeyReleased(Keys.D0)) cardManager.ForceSpawnInto("discount",      cardManager.StackSlots);
            if (keyboard.WasKeyReleased(Keys.Q))  cardManager.ForceSpawnInto("top3",          cardManager.StackSlots);
            if (keyboard.WasKeyReleased(Keys.W))  cardManager.ForceSpawnInto("discard",       cardManager.StackSlots);
#endif
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
        TimeSpan scaledElapsed = TimeSpan.FromTicks((long)(gameTime.ElapsedGameTime.Ticks * GamelabGame.GameSpeed));
        GameTime scaledGameTime = new GameTime(gameTime.TotalGameTime, scaledElapsed);
        mapManager.Update(scaledGameTime);
        ParticleManager.Update(scaledGameTime);

        if(Game.IsActive)
        {
            //Gameover
            if (mapManager.TentDestroyed)
            {
                Game.SwitchToScreen(new GameOverScreen(Game));
                return;
            }
            cardManager._previewStructure?.Update(gameTime);
            //Speedup
            if(keyboard.WasKeyReleased(Keys.S))
            {
                GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 3f : 1f;
            }
            //Slowdown
            if(keyboard.WasKeyReleased(Keys.D))
            {
                GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 0.25f : 1f;
            }
            //set cursor state
            if(gamePhaseManager.IsDay){
                if (_inventoryBtn.Contains(mousePos) || _towerRangeBtn.Contains(mousePos) || _tutorial_btn.Contains(mousePos) || _startNightBtn.Contains(mousePos))
                    GamelabGame.IsHovering = true;
            }
            if (_showInventory)
            {
                foreach (var (rect, card) in cardManager.CardRects)
                {
                    if (card != null && rect.Contains(mousePos))
                    {
                        GamelabGame.IsHovering = true;
                        break;
                    }
                }
            }
            if (!gamePhaseManager.IsInTransition && keyboard.WasKeyReleased(Keys.C))
            {
                _showInventory = !_showInventory;
                if (!_showInventory) cardRenderer.CollapseExpanded();
                return;
            }
            
            if(keyboard.IsKeyDown(Keys.Space) && !_is_active_tutorial && !gamePhaseManager.IsInTransition)
            {
                _resetStartNightTimer = false;
                _elapsedStartNightTime += gameTime.ElapsedGameTime;
                if(_elapsedStartNightTime > _startNightTime)
                {
                    _elapsedStartNightTime = TimeSpan.Zero;
#if DEBUG
                    gamePhaseManager.SwitchToNight();
#else
                    if(gamePhaseManager.IsDay){
                        gamePhaseManager.SwitchToNight();
                    }
#endif
                }
            }
                       
            // Drag
            if (mouse.WasButtonPressed(MouseButton.Left))
            {
                var pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                _inventoryBtnPressed  = _inventoryBtn.Contains(pos);
                _towerRangeBtnPressed = _towerRangeBtn.Contains(pos);
                _tutorial_btn_pressed = _tutorial_btn.Contains(pos);
                _speedBtnPressed      = _speedBtn.Contains(pos);
                _oldPos       = pos;
                _clickStarted = true;

                // trigger unaffordable flash
                if(!gamePhaseManager.IsInTransition && _hoveredStructure is Meadow cantAffordSpot && !cantAffordSpot.HasSheep)
                {
                    int moveCost = (int)MathF.Round(MapUtils.CalculateTileDistance(mapManager.SheepPosition, cantAffordSpot.TilePosition) * MapManager.MoveCostMultiplier);
                    if(GamelabGame.SanityManager.Sanity < moveCost)
                        cantAffordSpot.TriggerCantAffordFlash();
                }
            }

            if (mouse.IsButtonDown(MouseButton.Left) && _clickStarted &&
                (!_showInventory || !cardRenderer.CardStripBounds.Contains(mousePos)))
            {
                Vector2 deltaPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _oldPos;

                if (deltaPos.Length() > 0)
                {
                    mapManager.MoveMap(deltaPos, screenSize);
                    _isDrag = true;
                    _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                }

                if (!_isDrag && !gamePhaseManager.IsInTransition && _hoveredStructure is Meadow grazingSpot)
                {
                    int moveCost = (int)MathF.Round(
                        MapUtils.CalculateTileDistance(mapManager.SheepPosition, grazingSpot.TilePosition)
                        * MapManager.MoveCostMultiplier);

                    if (GamelabGame.SanityManager.Sanity >= moveCost &&
                        grazingSpot.AddProgress(gameTime.ElapsedGameTime))
                    {
                        mapManager.TryMoveSheep(mousePos);
                    }
                }
                else if (!_isDrag && !gamePhaseManager.IsInTransition && _startNightBtn.Contains(mousePos))
                {
                    _elapsedStartNightTime += gameTime.ElapsedGameTime;
                    _resetStartNightTimer = false;

                    if (_elapsedStartNightTime > _startNightTime)
                    {
                        _elapsedStartNightTime = TimeSpan.Zero;

            #if DEBUG
                        gamePhaseManager.SwitchToNight();
            #else
                        if (gamePhaseManager.IsDay)
                            gamePhaseManager.SwitchToNight();
            #endif
                    }
                }
            }

            //set the hovered tile
            bool mouseOverUI = _showInventory && cardRenderer.CardStripBounds.Contains(mousePos);
            _hoverTile = mapManager.GetTileFromScreenPosition(mousePos);
            // BLOCK HOVER IF ON UI
            if (!mouseOverUI && _hoverTile.col >= 0)
            {
                if (_hoveredStructure != null)
                    _hoveredStructure.ShouldDrawCard = false;

                _hoveredStructure = mapManager.GetStructure(_hoverTile);

                if (_hoveredStructure != null)
                    _hoveredStructure.ShouldDrawCard = true;
            }
            else
            {
                if (_hoveredStructure != null)
                    _hoveredStructure.ShouldDrawCard = false;

                _hoveredStructure = null;
            }

            // ── Mouse sprites for meadow hover ─────────────────────
            if (!mouseOverUI && _hoveredStructure is Meadow)
            {
                GamelabGame.IsHovering = true;
            }

            //handle click
            if (mouse.WasButtonReleased(MouseButton.Left) && _clickStarted)
            {
                _tutorial_btn_pressed = _inventoryBtnPressed = _towerRangeBtnPressed = _speedBtnPressed = false;
                if(!gamePhaseManager.IsDay) // Night specific clicks
                {
                    if (_speedBtn.Contains(mousePos))
                    {
                        // Toggle speed: 1x -> 3x -> 1x
                        GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 3f : 1f;
                        AudioManager.Play("rustle01");
                        return;
                    }
                }
                if(!gamePhaseManager.IsInTransition && GraphicsDevice.PresentationParameters.Bounds.Contains(mouse.Position) && !_isDrag)
                {
                    //start tutorial
                    if (_tutorial_btn.Contains(mousePos))
                    {
                        _is_active_tutorial = !_is_active_tutorial;
                        _tutorial_step = 0; 
                        if (_is_active_tutorial)
                        {
                            _showInventory = false;
                            _showTowerRange = false;
                            cardRenderer.CollapseExpanded(); 
                        }
                        AudioManager.Play("dragonflap");
                        _clickStarted = false;
                        return;
                    }
                    //progress tutorial
                    if (_is_active_tutorial)
                    {
                        _tutorial_step++;
                        //reached tutorial end
                        if(_tutorial_step == 25)
                        {  
                           _is_active_tutorial = false; 
                        }
                        _clickStarted = false; 
                        return;
                    }
                    // Card strip takes priority over any overlapping buttons
                    if (_showInventory && cardRenderer.CardStripBounds.Contains(mousePos))
                    {
                        foreach (var (rect, card) in cardManager.CardRects)
                        {
                            if (card == null || !rect.Contains(mousePos)) continue;
                            CardSlot slot = cardManager.GetSlotForCard(card);
                            bool isDouble = IsDoubleClick(mouse, gameTime);
                            if (card.Type == CardType.Build)
                            {
                                if (cardManager.PendingStackAction != null && slot != null)
                                {
                                    if (cardManager.PendingStackAction == CardEffect.SaveCard)
                                    {
                                        cardManager.SaveCardFromSlot(slot);
                                    }
                                    else if (cardManager.PendingStackAction == CardEffect.Discard)
                                    {
                                        cardManager.DiscardCardFromSlot(slot, gamePhaseManager.DayCount);
                                    }
                                    cardManager.PendingStackAction = null;
                                    _clickStarted = false;
                                    return;
                                }
                                if (slot != null && !slot.Purchased)
                                {
                                    if (isDouble)
                                    {
                                        bool bought = cardManager.TryPurchaseBuildCard(slot, sanityManager);
                                        if (bought)
                                            AudioManager.Play("coinsplash");
                                    }
                                    _clickStarted = false;
                                    return;
                                }
                                if (slot != null && slot.Purchased)
                                {
                                    ToggleActiveCard(cardManager, card);
                                    _clickStarted = false;
                                    return;
                                }
                            }
                            if (card.Type == CardType.StackAction)
                            {
                                if (card.Effect == CardEffect.Shuffle || card.Effect == CardEffect.Discount)
                                {
                                    if (isDouble)
                                    {
                                        cardManager.UseCard(card);
                                        AudioManager.Play("card-place");
                                    }
                                }
                                else if (card.Effect == CardEffect.SaveCard || card.Effect == CardEffect.Discard)
                                {
                                    if (isDouble)
                                    {
                                        cardManager.UseCard(card);
                                        AudioManager.Play("card-place");
                                    }
                                }
                                _clickStarted = false;
                                return;
                            }
                            if (card.Type == CardType.MapAction)
                            {
                                ToggleActiveCard(cardManager, card);
                                _clickStarted = false;
                                return;
                            }
                        }
                    }

                    if (_inventoryBtn.Contains(mousePos))
                    {
                        _showInventory = !_showInventory;
                        if (!_showInventory) cardRenderer.CollapseExpanded();
                        _clickStarted = false;
                        return;
                    }
                    if (_towerRangeBtn.Contains(mousePos))
                    {
                        _showTowerRange = !_showTowerRange;
                        _clickStarted = false;
                        return;
                    }

                    if (!mouseOverUI)
                    {
                        if (cardManager._activeMapCard != null)
                        {
                            if (cardManager.HandleMapCardUse(mapManager, mousePos))
                                cardRenderer.CollapseExpanded();
                            _clickStarted = false;
                            _isDrag = false;
                            return;
                        }
                        mapManager.HandleMapClick(mousePos);
                    }
                    mapManager.HandleMapClick(mousePos);
                }
                _clickStarted = false;
                _isDrag = false;
            }
        }        
    }

    /// <summary>
    /// Draws only the map and entities without any UI. Used to render the map as a background behind other screens.
    /// </summary>
    public void DrawMapOnly(GameTime gameTime)
    {
        if (spriteBatch == null) return; // not yet initialized (screen switch pending)
        GraphicsDevice.Clear(gamePhaseManager.IsDay ? new Color(80, 137, 71) : new Color(20, 47, 44));
        SamplerState sampler = mapManager.Scale.X < 2f ? SamplerState.LinearClamp : SamplerState.PointClamp;
        spriteBatch.Begin(samplerState: sampler, transformMatrix: viewportAdapter.GetScaleMatrix());
        mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle);
        entityRenderer.Draw(spriteBatch);
        spriteBatch.End();
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        float currentMin = (float)mapManager.TotalFallTime.TotalSeconds < 1.0f ? 0 : (float)mapManager.TotalFallTime.TotalSeconds / (float)mapManager.MaxTotalFallTime.TotalSeconds;
        GraphicsDevice.Clear(Color.Lerp(Color.CornflowerBlue, Color.Black, currentMin));

        SamplerState sampler = mapManager.Scale.X < 2f ? SamplerState.LinearClamp : SamplerState.PointClamp;
        spriteBatch.Begin(samplerState: sampler,
                        transformMatrix: viewportAdapter.GetScaleMatrix());

        // ✅ Map        
        if (cardManager._activeMapCard != null &&
            (cardManager._activeMapCard.Effect == CardEffect.Move ||
            cardManager._activeMapCard.Effect == CardEffect.Destroy ||
            cardManager._activeMapCard.Effect == CardEffect.Polymorph) &&
            _hoverTile.col >= 0)
        {
            mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle, _hoverTile);
            entityRenderer.Draw(spriteBatch, _hoverTile);
        }
        else
        {
            mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle);
            entityRenderer.Draw(spriteBatch);
            if (_is_active_tutorial && (_tutorial_step == 6))
            {
                float pulse = (float)(Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5) + 1) / 2f;
                Color highlightColor = Color.Yellow * (0.3f + pulse * 0.4f);

                for (int col = 0; col < mapManager.Columns; col++)
                {
                    for (int row = 0; row < mapManager.Rows; row++)
                    {
                        // 1. Check if the structure on this tile is a Meadow
                        // Use GetStructure with the (col, row) tuple
                        var structure = mapManager.GetStructure((col, row));

                        if (structure is Meadow) 
                        {
                            // 2. Get the actual screen/world position for the hex
                            Vector2 tilePos = mapManager.GetWorldPositionOfTile((col, row));
                            
                            // 3. Create a rectangle based on the tile's calculated position and size
                            Rectangle tileRect = new Rectangle(
                                (int)tilePos.X, 
                                (int)tilePos.Y, 
                                (int)mapManager.TileWidth, 
                                (int)mapManager.TileHeight
                            );

                            // 4. Draw the highlight
                            spriteBatch.Draw(_dummy, tileRect, highlightColor);
                        }
                    }
                }
            }


            
        }
        if (_showTowerRange && gamePhaseManager.IsDay)
        {
            cardRenderer.DrawAllTowerRanges(spriteBatch, mapManager);
        }
        cardRenderer.DrawPreview(spriteBatch, cardManager, mapManager, _hoverTile);

        spriteBatch.End();

        // Info card: no transform so all positions are in screen pixels, eliminating sub-pixel glyph shifts.
        Structure._screenTransform = viewportAdapter.GetScaleMatrix();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _hoveredStructure?.DrawCard(spriteBatch);
        spriteBatch.End();

        // UI always uses point clamp — cards are pixel art at fixed size, no zoom applied
        spriteBatch.Begin(samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());
        if (!gamePhaseManager.IsDay)
        {
            // Label showing current speed
            string speedLabel = GamelabGame.GameSpeed > 1f ? "Speed: 3x" : "Speed: 1x";
            DrawButton(_speedBtn, speedLabel, _speedBtnPressed);
        }
        int vw = (int)viewportAdapter.VirtualWidth;
        int vh = (int)viewportAdapter.VirtualHeight;

        DrawSanityPanel();

        if (!gamePhaseManager.IsInTransition)
        {
            DrawButton(_inventoryBtn,  _showInventory  ? "Hide Cards"    : "Show Cards",   _inventoryBtnPressed);
            DrawButton(_towerRangeBtn, _showTowerRange ? "Hide Range"    : "Tower Range",  _towerRangeBtnPressed);
            DrawButton(_tutorial_btn,  _is_active_tutorial ? "Close Tutorial" : "Tutorial", _tutorial_btn_pressed);
            DrawButton(_startNightBtn, "start night (Space)", false, (float)(_elapsedStartNightTime/_startNightTime));
            if (_showInventory)
            {
                cardRenderer.DrawCardStrip(spriteBatch, _dummy, _cardTexture, _cardRegions, _font,cardManager, vw, vh);
                cardRenderer.DrawExpandedPanel(spriteBatch, _dummy, _cardTexture, _cardRegions, _font,cardManager,vw, vh); 
            }
        }

        if (_is_active_tutorial) DrawTutorialState(gameTime);
        if (_showDayPopup)       DrawDayPopup();
        ParticleManager.Draw(spriteBatch, viewportAdapter.BoundingRectangle);
        spriteBatch.End();
    }

    // ------------------------
    // Draw Helpers
    // ------------------------

    private void DrawSanityPanel()
    {
        if (sanityManager == null) return;
        string text    = $"Sanity: {sanityManager.Sanity}";
        string dayText = $"Day: {gamePhaseManager.DayCount}/10";
        int pad = 10;
        Vector2 pos1  = new Vector2(20, 20);
        Vector2 pos2  = new Vector2(20, 70);
        Vector2 size1 = _font.MeasureString(text);
        Vector2 size2 = _font.MeasureString(dayText);
        int boxW = (int)Math.Max(size1.X, size2.X) + pad * 2;
        int boxH = (int)(size1.Y + size2.Y + (pos2.Y - pos1.Y - size1.Y)) + pad * 2;
        Color rectColor = gamePhaseManager.IsDay ? COLOR_L : new Color(33, 28, 11);
        Color textColor = gamePhaseManager.IsDay ? COLOR_D : new Color(214, 214, 214);
        spriteBatch.Draw(_dummy, new Rectangle((int)pos1.X - pad, (int)pos1.Y - pad, boxW, boxH), rectColor);
        spriteBatch.DrawString(_font, text,    pos1, textColor);
        spriteBatch.DrawString(_font, dayText, pos2, textColor);
    }
    
    private string WrapText(SpriteFont font, string text, float maxLineWidth, float scale)
    {
        // Remove any hardcoded newlines you might have left in your strings
        text = text.Replace("\n", " "); 
        
        string[] words = text.Split(' ');
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        float lineWidth = 0f;
        float spaceWidth = font.MeasureString(" ").X * scale;

        foreach (string word in words)
        {
            Vector2 size = font.MeasureString(word) * scale;

            if (lineWidth + size.X < maxLineWidth)
            {
                sb.Append(word + " ");
                lineWidth += size.X + spaceWidth;
            }
            else
            {
                // The word is too long, push it to the next line!
                sb.Append("\n" + word + " ");
                lineWidth = size.X + spaceWidth;
            }
        }

        return sb.ToString();
    }
private float _targetZoom = 1.0f; // Add this variable to your class fields at the top

private void DrawTutorialState(GameTime gameTime)
{
    int vw = (int)viewportAdapter.VirtualWidth;
    int vh = (int)viewportAdapter.VirtualHeight;
    float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

    // --- 1. SMOOTH ZOOM LOGIC ---
    // Instead of a loop, we check if we are at the target. If not, we zoom slightly.
    // Adjust 0.05f to change the speed (smaller = slower).
// --- 1. SMOOTH ZOOM LOGIC ---
// --- 1. SMOOTH ZOOM LOGIC ---
float zoomSpeed = 1.5f; // Adjust this to make the overall transition faster or slower
if (Math.Abs(mapManager.Scale.X - _targetZoom) > 0.05f)
{
    // 1. Calculate how much we WANT to zoom this frame
    float amountToMove = zoomSpeed * deltaTime;
    
    // 2. Add it to our timer (acting as an accumulator)
    _zoomTimer += amountToMove;

    // 3. Only call the expensive/jumpy Zoom method when the accumulator 
    // reaches a 'threshold' (0.1). This creates a rhythmic, consistent pulse.
    if (_zoomTimer >= 0.1f)
    {
        Vector2 screenCenter = new Vector2(vw / 2f, vh / 2f);
        float direction = mapManager.Scale.X < _targetZoom ? 1f : -1f;
        
        mapManager.Zoom(direction, screenCenter, screenSize);
        
        // 4. Subtract the threshold rather than setting to 0 
        // to keep the remainder for the next frame (prevents stutter)
        _zoomTimer -= 0.1f; 
    }
}
else 
{
    _zoomTimer = 0f; // Reset when we are at the target
}

    // Darken the background
    spriteBatch.Draw(_dummy, new Rectangle(0, 0, vw, vh), Color.Black * 0.4f);

    string speaker = "";
    string text = "";
    Rectangle? highlightBox = null;

    // --- 2. TUTORIAL STEPS ---
    switch (_tutorial_step)
    {
        case 0:
            speaker = "Human";
            text = "Huh... where am I? What is happening?";
            _targetZoom = 1.7f; // Zoom in close to the player
            break;
        case 1:
            speaker = "Sheep";
            text = "Oh good, you're awake! Last night when you were sleeping, demons came and attacked me and you.";
            break;
        case 2:
            speaker = "Human";
            text = "Did they do something to us?";
            break;
        case 3:
            speaker = "Sheep";
            text = "The sleep monsters come from the forest at night and wanted to attack sanity points at your tent. They also tried to eat me.";
            _targetZoom = 1.0f; // Zoom back out to see the UI

            // Calculate Sanity UI Highlight
            string sanityText = $"Sanity: {sanityManager.Sanity}";
            string dayText = $"Day: {gamePhaseManager.DayCount}/10";
            Vector2 s1 = _font.MeasureString(sanityText);
            Vector2 s2 = _font.MeasureString(dayText);
            int boxWidth = (int)Math.Max(s1.X, s2.X) + 20; 
            int boxHeight = (int)(s1.Y + s2.Y + 30) + 20;
            highlightBox = new Rectangle(10, 10, boxWidth, boxHeight); 
            break;
        case 4: 
            speaker = "Human";
            text = "Oh no, I hope you are okay. Why did they attack you?";
            _targetZoom = 1.7f;
            break;
        case 5: 
            speaker = "Sheep";
            text = "The monsters attack your sanity and I produce sanity each morning, if I survive the night";
            break;
        case 6:
            speaker = "Sheep";
            
            _targetZoom = 1.0f;
            text = "I like to graze on meadows far away. The further away the more sanity I produce for you each morning! However there are some downsides too. Firstly, moving me costs 1 sanity point per tile i have to traverse and secondly the monsters spawn nearby the forests close to the good grazing spots";
            break;
        case 7: 
            speaker = "Sheep";
            text = "you should also place some cards from your hand to defend us";
            float pulse = (float)(Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5) + 1) / 2f;
            Color highlightColor = Color.Yellow * (0.3f + pulse * 0.4f);
            foreach (var (rect, card) in cardManager.CardRects)
    {
        // Draw a glow behind or over each card slot
        spriteBatch.Draw(_dummy, rect, highlightColor);
    }
            break;
        // case 8: 
        //     speaker = "Human";
        //     text = "Okay, okay I'm bored of explanations, I wanna learn by doing :D";
        //     break;
        // case 9: 
        //     speaker = "Sheep"; 
        //     text = "Okay cool! For any more tipps and infos you can hover over tiles like meadows, cards and defense towers. Survive 10 nights to win the game! Good Luck!";
        //     break;

        case 8:  speaker = "Sheep";  text = "Now Look at the bottom-left of the screen your cards are there."; break;
        case 9:  speaker = "Human";  text = "Cards? My life depends on cards now?"; break;
        case 10: speaker = "Sheep";  text = "There are 3 types - starting with Build Cards."; break;
        case 11: speaker = "Sheep";  text = "Dark ones are locked - double click to buy, then click again to place."; break;
        case 12: speaker = "Human";  text = "Buy first, then place... got it."; break;
        case 13: speaker = "Sheep";  text = "Build cards protect the tent and stop monsters at night."; break;
        case 14: speaker = "Sheep";  text = "Hover over any card to see its info before using it."; break;
        case 15: speaker = "Sheep";  text = "Map Action cards let you move, destroy structures, or polymorph me."; break;
        case 16: speaker = "Human";  text = "How does that help against monsters?"; break;
        case 17: speaker = "Sheep";  text = "You can reposition defenses as u move me and also once polymorphed monsters won't attack me."; break;
        case 18: speaker = "Sheep";  text = "Stack Action cards modify your build cards like shuffle or discount."; break;
        case 19: speaker = "Human";  text = "So I need strategy..."; break;
        case 20: speaker = "Sheep";  text = "Exactly. Plan smart to survive the night."; break;
        case 21: speaker = "Human";  text = "Alright... I'll try not to mess this up."; break;

        default:
            speaker = (_tutorial_step%2==0)?"Sheep" : "Human";
            text = "DREAMWARD!!!!";
            break;

    }

    // --- 3. DRAW HIGHLIGHT & ARROW ---
    if (highlightBox.HasValue)
    {
        Rectangle h = highlightBox.Value;
        int border = 4;
        Color hc = Color.Yellow;

        // Draw Border
        spriteBatch.Draw(_dummy, new Rectangle(h.X - border, h.Y - border, h.Width + border * 2, border), hc); 
        spriteBatch.Draw(_dummy, new Rectangle(h.X - border, h.Bottom, h.Width + border * 2, border), hc);     
        spriteBatch.Draw(_dummy, new Rectangle(h.X - border, h.Y, border, h.Height), hc);                      
        spriteBatch.Draw(_dummy, new Rectangle(h.Right, h.Y, border, h.Height), hc);

        // Draw Bouncing Arrow pointing at the box
        // We place it to the right of the box and make it bob left/right
        float bounce = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 12) * 8;
        Vector2 arrowPos = new Vector2(h.Right + 30 + bounce, h.Center.Y);
        DrawPointerArrow(arrowPos, hc);
    }

    // --- 4. DIALOG BOX UI ---
    int dialogHeight = 140;
    Rectangle dialogRect = new Rectangle(50, vh - dialogHeight - 30, vw - 100, dialogHeight);
    
    spriteBatch.Draw(_dummy, dialogRect, COLOR_L); 
    spriteBatch.Draw(_dummy, new Rectangle(dialogRect.X + 2, dialogRect.Y + 2, dialogRect.Width - 4, dialogRect.Height - 4), COLOR_D);
    spriteBatch.Draw(_dummy, new Rectangle(dialogRect.X + 4, dialogRect.Y + 4, dialogRect.Width - 8, dialogRect.Height - 8), COLOR_L);

    // Portrait Logic
    Texture2D currentPortraitTex = null;
    Rectangle currentSourceRect = Rectangle.Empty;
    float portraitScale = 1f;
    int portraitX = 0;
    int portraitY = 0;
    int textOffsetX = 20; 

    if (speaker == "Human")
    {
        currentPortraitTex = _dayPlayerTexture;
        currentSourceRect = _humanPortraitRect;
        portraitScale = 3f; 
        portraitX = dialogRect.X + 20;
        portraitY = dialogRect.Y + 20;
        textOffsetX = 140; 
    }
    else if (speaker == "Sheep")
    {
        currentPortraitTex = _dayTopTexture;
        currentSourceRect = _sheepPortraitRect;
        portraitScale = 4f; 
        float scaledWidth = currentSourceRect.Width * portraitScale;
        float scaledHeight = currentSourceRect.Height * portraitScale;
        portraitX = dialogRect.Right - (int)scaledWidth + 201; 
        portraitY = dialogRect.Bottom - (int)scaledHeight; 
        textOffsetX = 20; 
    }

    // Typewriter Logic
    float textScale = 0.5f; 
    float maxTextWidth = (speaker == "Sheep") 
        ? portraitX - (dialogRect.X + textOffsetX) - 20 
        : dialogRect.Right - (dialogRect.X + textOffsetX) - 20;

    string wrappedText = WrapText(_font, text, maxTextWidth, textScale);

    if (_previousTutorialStep != _tutorial_step)
    {
        _charIndex = 0;
        _textTimer = 0f;
        _previousTutorialStep = _tutorial_step;
    }

    bool isTyping = _charIndex < wrappedText.Length;
    if (isTyping)
    {
        _textTimer += deltaTime;
        float timePerChar = 0.03f;
        while (_textTimer >= timePerChar && _charIndex < wrappedText.Length)
        {
            _charIndex++;
            _textTimer -= timePerChar;
            int frequency = speaker == "Human" ? 10 : 30;
            if (_charIndex % frequency == 4 && wrappedText[_charIndex - 1] != ' ' && wrappedText[_charIndex - 1] != '\n')
            {
                if (speaker == "Sheep") AudioManager.Play("sheep_baa");
                else if (speaker == "Human") AudioManager.Play("grunt_03");
            }
        }
    }
    // wrappedText is recomputed each frame and can change length if layout shifts, so clamp _charIndex to avoid Substring overflow
    _charIndex = Math.Min(_charIndex, wrappedText.Length);
    string textToDraw = wrappedText.Substring(0, _charIndex);
    int bounceOffset = isTyping ? (int)(Math.Sin(gameTime.TotalGameTime.TotalSeconds * 20) * 4) : 0;

    if (currentPortraitTex != null)
    {
        spriteBatch.Draw(currentPortraitTex, new Vector2(portraitX, portraitY + bounceOffset), 
            currentSourceRect, Color.White, 0f, Vector2.Zero, portraitScale, SpriteEffects.None, 0f);
    }

    spriteBatch.DrawString(_font, speaker + ":", new Vector2(dialogRect.X + textOffsetX, dialogRect.Y + 15), Color.DarkRed, 0f, Vector2.Zero, textScale * 1.2f, SpriteEffects.None, 0f);
    spriteBatch.DrawString(_font, textToDraw, new Vector2(dialogRect.X + textOffsetX, dialogRect.Y + 45), COLOR_D, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

    // "Click to continue"
    if (!isTyping)
    {
        string hint = "(Click anywhere to continue)";
        Vector2 hintSize = _font.MeasureString(hint) * 0.4f;
        int hintX = speaker == "Sheep" ? portraitX - (int)hintSize.X +160  : dialogRect.Right - (int)hintSize.X - 80;
        float pulse = (float)Math.Abs(Math.Sin(gameTime.TotalGameTime.TotalSeconds * 3));
        spriteBatch.DrawString(_font, hint, new Vector2(hintX, dialogRect.Bottom - 80), Color.DarkOliveGreen * pulse, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
    }
}

/// <summary>
/// Draws a simple pixel-art arrow pointing left ( <--- )
/// </summary>
private void DrawPointerArrow(Vector2 tipPos, Color color)
{
    // Shaft
    spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X, (int)tipPos.Y - 4, 30, 8), color);
    // Head (Triangle-ish)
    spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 6, (int)tipPos.Y - 12, 6, 24), color);
    spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 12, (int)tipPos.Y - 8, 6, 16), color);
    spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 18, (int)tipPos.Y - 4, 6, 8), color);
}
    private void DrawDayPopup()
    {
        int w = (int)viewportAdapter.VirtualWidth;
        int h = (int)viewportAdapter.VirtualHeight;
        int bW = w / 2, bH = h / 2;
        Rectangle box = new(w / 2 - bW / 2, h / 2 - bH / 2, bW, bH);
        spriteBatch.Draw(_dummy, box, Color.Beige * 0.6f);
        float scale      = 1.25f;
        Vector2 textSize = _font.MeasureString(_dayPopupText) * scale;
        Vector2 textPos  = new Vector2(box.Center.X - textSize.X / 2, box.Center.Y - textSize.Y / 2);
        spriteBatch.DrawString(_font, _dayPopupText, textPos, COLOR_D, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawButton(Rectangle rect, string text, bool pressed = false, float progress = 0f)
    {
        var src = pressed ? BtnPressed : BtnNormal;
        if(progress > 0)
        {   
            spriteBatch.End();
            // 1. Calculate where the progress "line" is in the texture atlas
            // We find the X position in pixels, then divide by texture width to get 0.0 - 1.0
            float progressWidthInPixels = src.X + (src.Width * progress);
            float uvThreshold = progressWidthInPixels / (float)InterfaceTexture.Width;

            // 2. Set the Shader Parameters
            GamelabGame.Instance.LoadingShader.Parameters["ProgressThreshold"].SetValue(uvThreshold);
            GamelabGame.Instance.LoadingShader.Parameters["OverlayColor"].SetValue(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)); // 50% dark

            // 3. Draw using the effect

            spriteBatch.Begin(effect: GamelabGame.Instance.LoadingShader,
                        samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());
            spriteBatch.Draw(InterfaceTexture, rect, src, Color.White);
            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewportAdapter.GetScaleMatrix());

            // Rectangle progressSrc = new Rectangle(src.X, src.Y, (int)(src.Width * progress), src.Height);
            // Rectangle progressDst = new Rectangle(rect.X, rect.Y, (int)(rect.Width * progress), rect.Height);
            // spriteBatch.Draw(InterfaceTexture, progressDst, progressSrc, Color.Black * 0.4f);
        }
        else
        {
            spriteBatch.Draw(InterfaceTexture, rect, src, Color.White);
        }
        var size = _font.MeasureString(text);
        float sc = Math.Min(rect.Width / size.X * 0.6f, rect.Height / size.Y * 0.6f);
        spriteBatch.DrawString(_font, text,
            new Vector2(rect.Center.X - size.X * sc * 0.5f,
                        rect.Center.Y - size.Y * sc * 0.5f),
            COLOR_D, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
    }

    public void ShowDayPopup(string text)
    {
        _dayPopupText = text;
        _showDayPopup = true;
        _popupTimer = 4f; // seconds
    }
    private bool IsDoubleClick(MouseStateExtended mouse, GameTime gameTime)
    {
        double now = gameTime.TotalGameTime.TotalMilliseconds;
        if (mouse.WasButtonReleased(MouseButton.Left))
        {
            if (now - _lastClickTime < DOUBLE_CLICK_TIME)
            {
                _lastClickTime = 0;
                return true;
            }
            _lastClickTime = now;
        }
        return false;
    }

    private void ToggleActiveCard(CardManager cm, Card card)
    {
        if (ReferenceEquals(cm._activeMapCard, card))
        {
            cm._activeMapCard    = null;
            cm._previewStructure = null;
            cm._selectedTile     = (-1, -1);
        }
        else
        {
            cm._activeMapCard    = card;
            cm._selectedTile     = (-1, -1);
            cm._previewStructure = null;
        }
    }
}