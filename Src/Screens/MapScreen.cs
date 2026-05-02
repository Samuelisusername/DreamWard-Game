using System;
using System.Collections.Generic;
using System.Linq;
using DreamWardUtils.Graphics;
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
    private Texture2D _dayTopTexture;
    private Texture2D _dayPlayerTexture;
    private RenderTarget2D _nightBackgroundCache;
    private Point _nightBackgroundCacheSize;
    
    private enum TutorialHighlight { None, CardStrip, BuildSlots, MapSlots, StackSlots, Expanded, MeadowTiles }
    private TutorialHighlight _tutorialHighlight = TutorialHighlight.None;

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
    private bool _tutorialTextComplete = false;

    // Cards
    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture, _sanityIconTexture, _dayIconTexture;
    private Texture2D _hammerTexture;
    private Dictionary<string, Rectangle> _cardRegions;

    // Navigation button
    private bool _showInventory = true;
    private Rectangle _inventoryBtn;
    private bool _showTowerRange = false;
    private Rectangle _towerRangeBtn;
    private bool _offsetInitialized = false;
    private bool _inventoryBtnPressed  = false;
    private bool _towerRangeBtnPressed = false;

    private bool _moveActive    = false;
    private bool _hammerActive = false;
    private bool _movePressed   = false;
    private bool _hammerBtnPressed = false;
    private Rectangle _moveBtn;
    private Rectangle _hammerBtn;
    
    private Rectangle _startNightBtn;
    private TimeSpan _startNightTime = TimeSpan.FromSeconds(1);
    private TimeSpan _elapsedStartNightTime = TimeSpan.Zero;
    private bool _resetStartNightTimer = true;

    // Drag
    private bool _isDrag;
    private bool _clickStarted;
    private Vector2 _oldPos;
    private (int col, int row) _hoverTile = (-1,-1);
    private string _dayPopupText = "";
    private float _popupTimer = 0f;
    private const float DayPopupDuration = 2f;
    private const float DayPopupFadeStart = 1f;
    private Structure _hoveredStructure;
    private Texture2D _interfaceTexture;
    private Texture2D _dayInterfaceTexture;
    private double _lastClickTime = 0;
    private const double DOUBLE_CLICK_TIME = 300; // ms

    //  — card drag-to-buy state ──────────────────────────────────────
    private Card    _dragCard         = null;
    private bool    _isDraggingCard   = false;
    private Vector2 _dragStartPos;
    private const float DRAG_THRESHOLD = 10f;   // virtual pixels before drag is confirmed

    private Texture2D InterfaceTexture => gamePhaseManager.IsDay ? _dayInterfaceTexture : _interfaceTexture;
    private float SpriteScale => viewportAdapter.VirtualWidth / 640f;
    private static readonly Rectangle FloatBtn        = new(0, 176, 80,  32);
    private static readonly Rectangle FloatBtnPrs     = new(0, 208, 80,  32);
    private static readonly Rectangle LongFloatBtn    = new(0, 240, 105, 32);
    private static readonly Rectangle LongFloatBtnPrs = new(0, 272, 105, 32);
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
        _sanityIconTexture = Content.Load<Texture2D>("Sprites/sanity_icon_big");
        _dayIconTexture = Content.Load<Texture2D>("Sprites/day-and-night_icon");
        _hammerTexture = GamelabGame.Instance.HammerTexture;

        // Card textures
        _cardTexture = Content.Load<Texture2D>("Sprites/cards");
        _cardRegions = TextureAtlas.LoadRegions(Content, "Sprites/cards.json");
        
        if (!_offsetInitialized)
        {
            mapManager.InitOffset(screenSize);
            _offsetInitialized = true;
        }
    }

    public void StartNewDay(int day)
    {
        cardManager.GenerateDailyCards(day);
        cardRenderer.CollapseExpanded();
    }

    public void ShowDayPopup(int day)
    {
        _dayPopupText = $"Day {day}";
        _popupTimer = 0f;
    }

    private void RebuildButtonLayout()
    {
        int vw = viewportAdapter.VirtualWidth;
        int vh = viewportAdapter.VirtualHeight;
        _tutorial_btn = new Rectangle(vw - 260, 20, 240, 96);
        _speedBtn = new Rectangle(vw - 260, 20, 240, 96);
        if (!gamePhaseManager.IsInTransition)
        {
            cardRenderer.UpdateUIButtonLayout(vw, vh, _showInventory, ref _inventoryBtn, ref _towerRangeBtn, ref _startNightBtn, ref _hammerBtn, ref _moveBtn);
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
        // The hammer cursor takes over while destroy mode is active and the mouse is over the map.
        Vector2 mousePosForCursor = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
        GamelabGame.UseHammerCursor = _hammerActive && !_hammerBtn.Contains(mousePosForCursor);
        Vector2 mousePos = mousePosForCursor;
        // ParticleManager.AddParticle(new Particle
        // {
        //     WorldPosition = (mousePos - mapManager.Offset)/mapManager.Scale,
        //     Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 200, (float)(Random.Shared.NextDouble() -0.5f) * 200),
        //     LifeSpan = TimeSpan.FromSeconds(3),
        //     Color = Color.Orange
        // });
        cardRenderer.SetMousePos(mousePos);
        RebuildButtonLayout();
        entityRenderer.HoveredTile = _hoverTile.col >= 0 ? _hoverTile : null;

        if (cardManager._activeMapCard?.Type == CardType.Build && _hoverTile.col >= 0)
        {
            var existing = mapManager.GetStructure(_hoverTile);
            var preview  = cardManager._previewStructure;
            entityRenderer.HealFlashTile = (existing != null && preview != null
                && existing.GetType() == preview.GetType()
                && existing.CurrentHealth < existing.MaxHealth)
                ? _hoverTile : null;
        }
        else
        {
            entityRenderer.HealFlashTile = null;
        }

        entityRenderer.Update(gameTime);
        

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
                if(_tutorial_step == 22)
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
            if (keyboard.WasKeyReleased(Keys.D1)) cardManager.ForceSpawnInto("sniper",       cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D2)) cardManager.ForceSpawnInto("aoe",          cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D3)) cardManager.ForceSpawnInto("meadow",       cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D4)) cardManager.ForceSpawnInto("dreamcatcher", cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D5)) cardManager.ForceSpawnInto("fence",        cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.Q))  cardManager.ForceSpawnInto("void",         cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.W))  cardManager.ForceSpawnInto("spikes",       cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.E))  cardManager.ForceSpawnInto("decoy",        cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.R))  cardManager.ForceSpawnInto("buff",         cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D6)) cardManager.ForceSpawnInto("polymorph",    cardManager.BuildSlots);
            if (keyboard.WasKeyReleased(Keys.D7)) cardManager.ForceSpawnInto("shuffle",      cardManager.StackSlots);
            if (keyboard.WasKeyReleased(Keys.D8)) cardManager.ForceSpawnInto("discount",     cardManager.StackSlots);
            if (keyboard.WasKeyReleased(Keys.D9)) cardManager.ForceSpawnInto("top3",         cardManager.StackSlots);
            if (keyboard.WasKeyReleased(Keys.T))  cardManager.AddSlots(1);
            if (keyboard.WasKeyReleased(Keys.Y))  cardManager.AddSlots(2);
            if (keyboard.WasKeyReleased(Keys.U))  cardManager.AddSlots(3);
#endif
        }



        if (!string.IsNullOrEmpty(_dayPopupText) && _popupTimer < DayPopupDuration)
            _popupTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

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
                GamelabGame.GameSpeed = 1f;
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
            if(!gamePhaseManager.IsInTransition){
                if (_inventoryBtn.Contains(mousePos) || _towerRangeBtn.Contains(mousePos) || _tutorial_btn.Contains(mousePos) || _startNightBtn.Contains(mousePos) || _hammerBtn.Contains(mousePos))
                    GamelabGame.IsHovering = true;
                    
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
            }
            else if(!gamePhaseManager.IsDay && _speedBtn.Contains(mousePos))
            {
                GamelabGame.IsHovering = true;
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
                    mapManager.TriggerVoidEnclosureFall();
                    gamePhaseManager.SwitchToNight();
#else
                    if(gamePhaseManager.IsDay){
                        mapManager.TriggerVoidEnclosureFall();
                        gamePhaseManager.SwitchToNight();
                    }
#endif
                }
            }
                       
            // DEBUG: right click triggers a sanity-gain burst above the sheep
#if DEBUG
            if (mouse.WasButtonPressed(MouseButton.Right))
            {
                SpawnSanityGainOverSheep(20);
            }
#endif

            // Drag
            if (mouse.WasButtonPressed(MouseButton.Left))
            {
                var pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                _inventoryBtnPressed  = _inventoryBtn.Contains(pos);
                _towerRangeBtnPressed = _towerRangeBtn.Contains(pos);
                _tutorial_btn_pressed = _tutorial_btn.Contains(pos);
                _speedBtnPressed      = _speedBtn.Contains(pos);
                _movePressed          = _moveBtn.Contains(pos);
                _hammerBtnPressed     = _hammerBtn.Contains(pos);
                _oldPos       = pos;
                _clickStarted = true;

                // Begin possible card drag
                _dragCard       = null;
                _isDraggingCard = false;
                if (!gamePhaseManager.IsInTransition && gamePhaseManager.IsDay && _showInventory)
                {
                    foreach (var (rect, card) in cardManager.CardRects)
                    {
                        if ((card?.Type == CardType.Build || card?.Type == CardType.MapAction) && rect.Contains(pos))
                        {
                            _dragCard     = card;
                            _dragStartPos = pos;
                            break;
                        }
                    }
                }

                // trigger unaffordable flash
                if(!gamePhaseManager.IsInTransition && _hoveredStructure is Meadow cantAffordSpot && !cantAffordSpot.HasSheep)
                {
                    int moveCost = (int)MathF.Round(MapUtils.CalculateTileDistance(mapManager.SheepPosition, cantAffordSpot.TilePosition) * MapManager.MoveCostMultiplier);
                    if(GamelabGame.SanityManager.Sanity < moveCost)
                        cantAffordSpot.TriggerCantAffordFlash();
                }
            }

            // ── Mouse held ───────────────────────────────────────────────────
            if (mouse.IsButtonDown(MouseButton.Left) && _clickStarted)
            {
                // ── Card drag: promote to dragging once threshold crossed ──────
                if (_dragCard != null && !_isDraggingCard)
                {
                    Vector2 dragDelta = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _dragStartPos;
                    if (dragDelta.Length() > DRAG_THRESHOLD)
                    {
                        CardSlot slot = cardManager.GetSlotForCard(_dragCard);
                        if (slot != null)
                        {
                            if (!slot.Purchased)
                            {
                                // Try to purchase on drag-start
                                if (cardManager.TryPurchaseBuildCard(slot, sanityManager))
                                {
                                    AudioManager.Play("coinsplash");
                                    _isDraggingCard = true;
                                    cardManager._activeMapCard    = _dragCard;
                                    cardManager._selectedTile     = (-1, -1);
                                    cardManager._previewStructure = null;
                                }
                                else
                                {
                                    // Can't afford — abort drag
                                    _dragCard = null;
                                }
                            }
                            else
                            {
                                // Already purchased — drag directly into placement
                                _isDraggingCard = true;
                                cardManager._activeMapCard    = _dragCard;
                                cardManager._selectedTile     = (-1, -1);
                                cardManager._previewStructure = null;
                            }
                        }
                    }
                }

                // ── Map drag (only when not dragging a card and not over card strip) ──
                if (!_isDraggingCard &&
                    (!_showInventory || !cardRenderer.CardStripBounds.Contains(mousePos)))
                {
                    Vector2 deltaPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _oldPos;
                    if (deltaPos.Length() > 0)
                    {
                        mapManager.MoveMap(deltaPos, screenSize);
                        _isDrag = true;
                        _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                    }
                }

                // ── Sheep grazing hold ────────────────────────────────────────
                if (!_isDrag && !_isDraggingCard && !gamePhaseManager.IsInTransition
                    && _hoveredStructure is Meadow grazingSpot
                    && !grazingSpot.HasSheep && !grazingSpot.IsGrazed)
                {
                    int moveCost = (int)MathF.Round(
                        MapUtils.CalculateTileDistance(mapManager.SheepPosition, grazingSpot.TilePosition)
                        * MapManager.MoveCostMultiplier);
                    if (GamelabGame.SanityManager.Sanity >= moveCost
                        && grazingSpot.AddProgress(gameTime.ElapsedGameTime))
                    {
                        mapManager.TryMoveSheep(mousePos);
                    }
                }
            }

            //set the hovered tile
            bool mouseOverUI = (!gamePhaseManager.IsInTransition &&
                ((_showInventory && cardRenderer.CardStripBounds.Contains(mousePos))
                 || _inventoryBtn.Contains(mousePos)
                 || _towerRangeBtn.Contains(mousePos)
                 || _tutorial_btn.Contains(mousePos)
                 || _startNightBtn.Contains(mousePos)
                 || _moveBtn.Contains(mousePos)
                 || _hammerBtn.Contains(mousePos)))
                || (!gamePhaseManager.IsDay && _speedBtn.Contains(mousePos));
            
            _hoverTile = mapManager.GetTileFromScreenPosition(mousePos);
            // BLOCK HOVER IF ON UI
            if (!mouseOverUI && _hoverTile.col >= 0)
            {
                if (_hoveredStructure != null)
                    _hoveredStructure.ShouldDrawCard = false;

                _hoveredStructure = mapManager.GetStructure(_hoverTile);

                if (_hoveredStructure != null && _hoveredStructure is not VoidTile)
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
                _tutorial_btn_pressed = _inventoryBtnPressed = _towerRangeBtnPressed = _speedBtnPressed = _movePressed = _hammerBtnPressed = false;

                // ── Drop dragged card onto map ────────────────────────────────
                if (_isDraggingCard && _dragCard != null)
                {
                    if (_hoverTile.col >= 0)
                    {
                        if (cardManager.HandleMapCardUse(mapManager, mousePos))
                            cardRenderer.CollapseExpanded();
                    }
                    _isDraggingCard = false;
                    _dragCard       = null;
                    _clickStarted   = false;
                    _isDrag         = false;
                    return;
                }
                _dragCard       = null;
                _isDraggingCard = false;

                if(!gamePhaseManager.IsDay) // Night specific clicks
                {
                    mapManager.HandleMapClick(mousePos);
                    if (_speedBtn.Contains(mousePos))
                    {
                        // Toggle speed: 1x -> 3x -> 1x
                        GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 3f : 1f;
                        AudioManager.Play("rustle01");
                        return;
                    }
                    if (_towerRangeBtn.Contains(mousePos))
                    {
                        _showTowerRange = !_showTowerRange;
                        _clickStarted = false;
                        return;
                    }
                }
                if(!gamePhaseManager.IsInTransition && GraphicsDevice.PresentationParameters.Bounds.Contains(mouse.Position) && !_isDrag)
                {
                    bool overCardStrip = _showInventory && cardRenderer.CardStripBounds.Contains(mousePos);
                    //start night
                    if (!overCardStrip && _startNightBtn.Contains(mousePos))
                    {
#if DEBUG
                        gamePhaseManager.SwitchToNight();
#else
                        if (gamePhaseManager.IsDay){
                                mapManager.TriggerVoidEnclosureFall();
                                gamePhaseManager.SwitchToNight();
                            }
#endif
                        _clickStarted = false;
                        return;
                    }
                    //start tutorial
                    if (!overCardStrip && _tutorial_btn.Contains(mousePos))
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
                        if (!_tutorialTextComplete)
                        {
                            // Complete the text instantly
                            _charIndex = int.MaxValue;
                        }
                        else
                        {
                            _tutorial_step++;
                            //reached tutorial end
                            if(_tutorial_step == 22)
                            {
                               _is_active_tutorial = false;
                            }
                        }
                        _clickStarted = false;
                        return;
                    }
                    // ── Card strip clicks ─────────────────────────────────────
                    if (_showInventory && cardRenderer.CardStripBounds.Contains(mousePos))
                    {
                        Card clickedCard = null;
                        CardSlot clickedSlot = null;

                        // ── SAME logic as hover: topmost wins ──
                        for (int i = cardRenderer.TransformCount - 1; i >= 0; i--)
                        {
                            var t = cardRenderer.GetTransform(i);

                            if (t.HalfW == 0 || t.HalfH == 0)
                                continue;

                            float rad = MathHelper.ToRadians(t.AngleDeg);
                            Vector2 local = CardRenderer.Rot(mousePos - t.Center, -rad);

                            if (MathF.Abs(local.X) <= t.HalfW &&
                                MathF.Abs(local.Y) <= t.HalfH)
                            {
                                if (i < cardManager.CardRects.Count)
                                {
                                    var (_, card) = cardManager.CardRects[i];
                                    if (card == null) continue;

                                    clickedCard = card;
                                    clickedSlot = cardManager.GetSlotForCard(card);
                                    break;
                                }
                            }
                        }

                        if (clickedCard == null)
                            return;

                        bool isDouble = IsDoubleClick(mouse, gameTime);

                        // ── BUILD / MAP ACTION ─────────────────────────────
                        if (clickedCard.Type == CardType.Build || clickedCard.Type == CardType.MapAction)
                        {
                            if (clickedSlot != null && !clickedSlot.Purchased)
                            {
                                if (isDouble)
                                {
                                    if (cardManager.TryPurchaseBuildCard(clickedSlot, sanityManager))
                                        AudioManager.Play("coinsplash");
                                }

                                _clickStarted = false;
                                return;
                            }

                            if (clickedSlot != null && clickedSlot.Purchased)
                            {
                                ToggleActiveCard(cardManager, clickedCard);
                                _clickStarted = false;
                                return;
                            }
                        }

                        // ── STACK ACTION ───────────────────────────────────
                        if (clickedCard.Type == CardType.StackAction)
                        {
                            if (clickedCard.Effect == CardEffect.Shuffle || clickedCard.Effect == CardEffect.Discount || clickedCard.Effect == CardEffect.Refill)
                            {
                                if (isDouble)
                                {
                                    cardManager.UseCard(clickedCard);
                                    AudioManager.Play("card-place");
                                }
                            }
                            _clickStarted = false;
                            return;
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
                    if (_moveBtn.Contains(mousePos))
                    {
                        _moveActive = !_moveActive;
                        _hammerActive = false;

                        if (_moveActive)
                        {
                            if (GamelabGame.SanityManager.Sanity <= 2)
                            {
                                _moveActive = false;
                                _clickStarted = false;
                                return;
                            }

                            cardManager._activeMapCard = null;
                            cardManager._previewStructure = null;
                            cardManager._selectedTile = (-1, -1);
                        }
                        else
                        {
                            cardManager._selectedTile = (-1, -1);
                        }

                        _clickStarted = false;
                        return;
                    }
                    if (_hammerBtn.Contains(mousePos))
                    {
                        _hammerActive = !_hammerActive;
                        if (_hammerActive)
                        {
                            // Cancel any active map card so the two modes don't conflict
                            cardManager._activeMapCard = null;
                            cardManager._previewStructure = null;
                            cardManager._selectedTile = (-1, -1);
                        }
                        _clickStarted = false;
                        return;
                    }

                    if (!mouseOverUI)
                    {
                        if (_hammerActive)
                        {
                            (int col, int row) tile = mapManager.GetTileFromScreenPosition(mousePos);
                            if (tile.col >= 0 && IsHammerDestroyable(mapManager.GetStructure(tile)))
                            {
                                mapManager.RemoveStructure(tile);
                            }
                            _clickStarted = false;
                            _isDrag = false;
                            return;
                        }         
                        if (_moveActive)
                        {
                            (int col, int row) tile = mapManager.GetTileFromScreenPosition(mousePos);

                            if (tile.col >= 0)
                            {
                                cardManager.TryMoveStructure(mapManager, tile);
                            }

                            _clickStarted = false;
                            _isDrag = false;
                            return;
                        }
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

    private void DrawNightBackgroundIfNight()
    {
        if (GamelabGame.NightBackground == null) return;

        bool isNight = !gamePhaseManager.IsDay;
        bool isFallingToNight = gamePhaseManager.IsInTransition && gamePhaseManager.IsDay;
        if (!isNight && !isFallingToNight) return;

        float opacity = 1f;
        if (isFallingToNight)
        {
            float fallProgress = (float)mapManager.TotalFallTime.TotalSeconds < 1.0f
                ? 0f
                : (float)mapManager.TotalFallTime.TotalSeconds / (float)mapManager.MaxTotalFallTime.TotalSeconds;
            opacity = MathHelper.Clamp(fallProgress, 0f, 1f);
        }

        int vw = viewportAdapter.VirtualWidth;
        int vh = viewportAdapter.VirtualHeight;

        if (_nightBackgroundCache == null || _nightBackgroundCacheSize.X != vw || _nightBackgroundCacheSize.Y != vh)
        {
            _nightBackgroundCache?.Dispose();
            _nightBackgroundCache = new RenderTarget2D(GraphicsDevice, vw, vh);
            _nightBackgroundCacheSize = new Point(vw, vh);

            var prevTargets = GraphicsDevice.GetRenderTargets();
            GraphicsDevice.SetRenderTarget(_nightBackgroundCache);
            GraphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(GamelabGame.NightBackground, new Rectangle(0, 0, vw, vh), Color.White);
            spriteBatch.End();
            GraphicsDevice.SetRenderTargets(prevTargets);
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewportAdapter.GetScaleMatrix());
        spriteBatch.Draw(_nightBackgroundCache, new Rectangle(0, 0, vw, vh), Color.White * opacity);
        spriteBatch.End();
    }

    /// <summary>
    /// Draws only the map and entities without any UI. Used to render the map as a background behind other screens.
    /// </summary>
    public void DrawMapOnly(GameTime gameTime)
    {
        if (spriteBatch == null) return; // not yet initialized (screen switch pending)
        GraphicsDevice.Clear(gamePhaseManager.IsDay ? new Color(80, 137, 71) : new Color(20, 47, 44));
        // this still does not account for pixel tearing, but I think I prefer that over blurriness or choppy zoom
        float viewportScale = viewportAdapter.GetScaleMatrix().M11;
        SamplerState sampler = mapManager.Scale.X * viewportScale < 1f ? SamplerState.LinearClamp : SamplerState.PointClamp;

        DrawNightBackgroundIfNight();

        spriteBatch.Begin(samplerState: sampler, transformMatrix: viewportAdapter.GetScaleMatrix());
        mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle);
        spriteBatch.End();
        SamplerState hpSampler = mapManager.Scale.X < 2f ? SamplerState.PointClamp : SamplerState.PointClamp;
        spriteBatch.Begin(samplerState: hpSampler, transformMatrix: viewportAdapter.GetScaleMatrix());
        entityRenderer.Draw(spriteBatch);
        spriteBatch.End();
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        float currentMin = (float)mapManager.TotalFallTime.TotalSeconds < 1.0f ? 0 : (float)mapManager.TotalFallTime.TotalSeconds / (float)mapManager.MaxTotalFallTime.TotalSeconds;
        GraphicsDevice.Clear(Color.Lerp(Color.CornflowerBlue, Color.Black, currentMin));

        float viewportScale = viewportAdapter.GetScaleMatrix().M11;
        SamplerState sampler = mapManager.Scale.X * viewportScale < 1f ? SamplerState.LinearClamp : SamplerState.PointClamp;

        DrawNightBackgroundIfNight();

        spriteBatch.Begin(samplerState: sampler,
                        transformMatrix: viewportAdapter.GetScaleMatrix());

        // ✅ Map        
        if (cardManager._activeMapCard != null &&
            (cardManager._activeMapCard.Effect == CardEffect.Move     ||
             cardManager._activeMapCard.Effect == CardEffect.Destroy  ||
             cardManager._activeMapCard.Effect == CardEffect.Polymorph) &&
            _hoverTile.col >= 0)
        {
            mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle, _hoverTile, _showTowerRange, cardRenderer);
        }
        else
        {
            mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle, drawRange: _showTowerRange, cardRenderer: cardRenderer);
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
        if(!gamePhaseManager.IsInTransition)
        {
            cardRenderer.DrawPreview(spriteBatch, cardManager, mapManager, _hoverTile);
        }

        // Hammer (destroy mode): highlight every player-placed structure tile in yellow
        if (_hammerActive)
        {
            Color hammerHighlight = Color.Yellow;
            for (int col = 0; col < mapManager.Columns; col++)
            {
                for (int row = 0; row < mapManager.Rows; row++)
                {
                    if (IsHammerDestroyable(mapManager.GetStructure((col, row))))
                    {
                        cardRenderer.DrawTileHighlightPublic(spriteBatch, mapManager, (col, row), hammerHighlight);
                    }
                }
            }
        }
        if (_moveActive)
        {
            Color moveHighlight = Color.Yellow;

            for (int col = 0; col < mapManager.Columns; col++)
            {
                for (int row = 0; row < mapManager.Rows; row++)
                {
                    var s = mapManager.GetStructure((col, row));

                    if (cardManager._selectedTile == (-1, -1))
                    {
                        // show pickable tiles
                        if (s != null && s.CanMove)
                            cardRenderer.DrawTileHighlightPublic(spriteBatch, mapManager, (col, row), moveHighlight);
                    }
                    else
                    {
                        // show valid drop tiles
                        if (s != null)
                            cardRenderer.DrawTileHighlightPublic(spriteBatch, mapManager, (col, row), Color.Red);
                    }
                }
            }
        }

        // ── Move-mode placement preview ───────────────────────────────────────
        // When the player has picked up a structure (_selectedTile is set) and is
        // hovering a valid drop tile, show the ghost of the structure there.
        // ── Move-mode placement preview ───────────────────────────────────────
        if (_moveActive && cardManager._selectedTile != (-1, -1)
            && cardManager._previewStructure != null && _hoverTile.col >= 0)
        {
            var moving  = cardManager._previewStructure;
            bool valid  = mapManager.GetStructure(_hoverTile) == null;
            Color ghost = valid ? new Color(0x11, 0x7a, 0x15) : new Color(220, 60, 60);

            var oldPos = moving.TilePosition;
            moving.TilePosition = _hoverTile;
            cardRenderer.DrawTileHighlightPublic(spriteBatch, mapManager, _hoverTile, ghost);

            // ── NEW: show tower range while moving a tower ──────────────────
            if (moving is Tower movingTower)
                cardRenderer.DrawTowerRangePreview(spriteBatch, mapManager, movingTower, ghost);

            var prev = moving.PreviewTint;
            moving.PreviewTint = ghost;
            moving.DrawBase(spriteBatch, alpha: 0.5f);
            moving.Draw(spriteBatch, alpha: 0.5f);
            moving.PreviewTint = prev;
            moving.TilePosition = oldPos;
        }

        spriteBatch.End();

        SamplerState hpSampler = mapManager.Scale.X < 2f ? SamplerState.LinearClamp : SamplerState.PointClamp;
        spriteBatch.Begin(samplerState: hpSampler, transformMatrix: viewportAdapter.GetScaleMatrix());
        (int col, int row) skipOrHover = (
            (cardManager._activeMapCard != null &&
             (cardManager._activeMapCard.Effect == CardEffect.Move ||
              cardManager._activeMapCard.Effect == CardEffect.Destroy ||
              cardManager._activeMapCard.Effect == CardEffect.Polymorph) &&
             _hoverTile.col >= 0)
            || (_moveActive && cardManager._selectedTile != (-1, -1) && _hoverTile.col >= 0)
            ) ? _hoverTile : (-1, -1);
        entityRenderer.Draw(spriteBatch, skipOrHover.col >= 0 ? skipOrHover : null);
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
            DrawButton(_towerRangeBtn, _showTowerRange ? "Hide Range" : "Tower Range", _towerRangeBtnPressed);
        }
        int vw = (int)viewportAdapter.VirtualWidth;
        int vh = (int)viewportAdapter.VirtualHeight;

        DrawSanityPanel();

        if (!gamePhaseManager.IsInTransition)
        {
            DrawButton(_inventoryBtn,  _showInventory  ? "Hide Cards"    : "Show Cards",   _inventoryBtnPressed);
            DrawButton(_towerRangeBtn, _showTowerRange ? "Hide Range"    : "Tower Range",  _towerRangeBtnPressed);
            DrawButton(_tutorial_btn,  _is_active_tutorial ? "Close Tutorial" : "Tutorial", _tutorial_btn_pressed);
            DrawButton(_startNightBtn, "Start night", false, (float)(_elapsedStartNightTime/_startNightTime));
            DrawButton(_moveBtn, _moveActive ? "Move (ON)" : "Move (2 Sanity)", _moveActive);
            DrawButton(_hammerBtn, _hammerActive ? "Destroy (ON)" : "Destroy", _hammerActive);
            if (_showInventory)
            {
                cardRenderer.DrawCardStrip(spriteBatch, _dummy, _cardTexture, _cardRegions, _font,cardManager, vw, vh, _is_active_tutorial);
                cardRenderer.DrawExpandedPanel(spriteBatch, _dummy, _cardTexture, _cardRegions, _font,cardManager,vw, vh); 
            }
        }

        if (_is_active_tutorial) DrawTutorialState(gameTime);
        DrawDayPopup(vw, vh);
        ParticleManager.Draw(spriteBatch, viewportAdapter.BoundingRectangle);
        spriteBatch.End();
    }

    private void DrawDayPopup(int vw, int vh)
    {
        if (string.IsNullOrEmpty(_dayPopupText) || _popupTimer >= DayPopupDuration) return;

        float fade = _popupTimer < DayPopupFadeStart
            ? 1f
            : 1f - (_popupTimer - DayPopupFadeStart) / (DayPopupDuration - DayPopupFadeStart);
        float rise = _popupTimer < DayPopupFadeStart
            ? 0f
            : (_popupTimer - DayPopupFadeStart) / (DayPopupDuration - DayPopupFadeStart) * 60f;

        float scale = 2.5f;
        Vector2 size = _font.MeasureString(_dayPopupText) * scale;
        Vector2 pos = new Vector2((vw - size.X) / 2f, vh * 0.25f - rise);

        Color shadow = Color.Black * fade;
        Color text = Color.White * fade;
        spriteBatch.DrawString(_font, _dayPopupText, pos + new Vector2(2, 2), shadow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, _dayPopupText, pos, text, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // ------------------------
    // Draw Helpers
    // ------------------------

    private const int MaxSanity = 100;

    private const int MaxDays = 10;

    public void SpawnSanityGainOverSheep(int amount)
    {
        if (amount <= 0) return;
        var sheep = mapManager.GetSheep();
        if (sheep == null) return;

        Vector2 sheepScreen = mapManager.GetWorldPositionOfTile(mapManager.SheepPosition);
        Vector2 tileCenterScreen = sheepScreen + new Vector2(mapManager.TileWidth / 2f, mapManager.TileHeight / 2f);
        Vector2 spawnWorld = (tileCenterScreen - mapManager.Offset) / mapManager.Scale;
        ParticleManager.SpawnSanityGain(amount, spawnWorld, _font);
    }

    private void DrawSanityPanel()
    {
        if (sanityManager == null) return;
        Vector2 pos1 = new Vector2(20, 20);
        Vector2 pos2 = new Vector2(20, 70);
        int sanityBarWidth = 280;
        int dayBarWidth = 200;
        int barHeight = (int)_font.MeasureString("X").Y;

        int panelHeight = (int)(barHeight + barHeight + (pos2.Y - pos1.Y - barHeight));
        int iconSize = panelHeight / 3;
        int iconGap = 8;

        if (_sanityIconTexture != null)
        {
            spriteBatch.Draw(_sanityIconTexture,
                new Rectangle((int)pos1.X, (int)pos1.Y + 6, iconSize, iconSize),
                Color.White);
        }
        if (_dayIconTexture != null)
        {
            spriteBatch.Draw(_dayIconTexture,
                new Rectangle((int)pos1.X, (int)pos2.Y + 6, iconSize, iconSize),
                Color.White);
        }

        int textX = (int)pos1.X + iconSize + iconGap;
        DrawValueBar(textX, (int)pos1.Y, sanityBarWidth, barHeight,
            Math.Clamp(sanityManager.Sanity, 0, MaxSanity), MaxSanity, new Color(37, 150, 190));
        DrawValueBar(textX, (int)pos2.Y, dayBarWidth, barHeight,
            Math.Clamp(gamePhaseManager.DayCount, 0, MaxDays), MaxDays, new Color(218, 165, 32));
    }

    private void DrawValueBar(int x, int y, int width, int height, int value, int max, Color fillColor)
    {
        float pct = max <= 0 ? 0f : value / (float)max;

        spriteBatch.Draw(_dummy, new Rectangle(x, y, width, height), new Color(40, 40, 40));
        spriteBatch.Draw(_dummy, new Rectangle(x, y, (int)(width * pct), height), fillColor);

        Color borderColor = new(59, 79, 70);
        int border = 3;
        spriteBatch.Draw(_dummy, new Rectangle(x, y, width, border), borderColor);
        spriteBatch.Draw(_dummy, new Rectangle(x, y + height - border, width, border), borderColor);
        spriteBatch.Draw(_dummy, new Rectangle(x, y, border, height), borderColor);
        spriteBatch.Draw(_dummy, new Rectangle(x + width - border, y, border, height), borderColor);

        string overlay = $"{value}/{max}";
        Vector2 overlaySize = _font.MeasureString(overlay);
        Vector2 overlayPos = new Vector2(x + (width - overlaySize.X) / 2f, y + (height - overlaySize.Y) / 2f);
        spriteBatch.DrawString(_font, overlay, overlayPos + new Vector2(1, 1), Color.Black);
        spriteBatch.DrawString(_font, overlay, overlayPos, Color.White);
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
        _tutorialHighlight = TutorialHighlight.None;

        // --- 2. TUTORIAL STEPS ---
        switch (_tutorial_step)
        {
            case 0:  speaker = "Human"; text = "Huh... where am I? What is happening?"; _targetZoom = 1.7f; _showInventory = false; break;
            case 1:  speaker = "Sheep"; text = "Oh good, you're awake! Last night when you were sleeping, nightmares came and attacked us."; _targetZoom = 1.7f; _showInventory = false; break;
            case 2:  speaker = "Human"; text = "Did they do something to us?"; _targetZoom = 1.7f; _showInventory = false; break;
            case 3:
                speaker = "Sheep";
                text = "The nightmares come from the forest at night and wanted to your tent and sanity. They also tried to eat me.";
                _targetZoom = 1.0f; _showInventory = false;
                // Calculate Sanity UI Highlight
                int barW = 240;
                int barH = (int)_font.MeasureString("X").Y;
                int panelH = barH + barH + (70 - 20 - barH);
                int iconCol = panelH / 3 + 8;
                int boxWidth = iconCol + barW + 71;
                int boxHeight = panelH + 20;
                highlightBox = new Rectangle(10, 10, boxWidth, boxHeight);
                break;

            case 4:  speaker = "Human"; text = "Oh no, I hope you are okay. Why did they attack you?"; _targetZoom = 1.7f; _showInventory = false; break;
            case 5:  speaker = "Sheep"; text = "The nightmares attack your sanity and I produce sanity each morning, if I survive the night."; _targetZoom = 1.0f; _showInventory = false; break;
            case 6:  speaker = "Sheep"; text = "I like to graze on meadows far away. The further away the more sanity I produce for you each morning! However there are some downsides too. Firstly, moving me costs sanity per tile I have to traverse and secondly the nightmares spawn near the forest close to the good grazing spots"; _targetZoom = 1.0f; _showInventory = false; break;
            case 7:  speaker = "Sheep"; text = "Bonus tip: Place a Build-Card Meadow next to me and you'll be EVEN MORE sane!"; _targetZoom = 1.0f; _showInventory = false; _tutorialHighlight = TutorialHighlight.MeadowTiles; break;
            case 8:  speaker = "Human"; text = "Okay, this is a lot. You said something about cards?"; _targetZoom = 1.0f; _showInventory = false; break;
            case 9:  speaker = "Sheep"; text = "Yes! Look at the bottom of the screen - that's your hand of cards, arranged in a fan. There are 2 types: Build cards and Stack Action cards."; _targetZoom = 1.0f; _showInventory = true; _tutorialHighlight = TutorialHighlight.CardStrip; break;
            case 10: speaker = "Sheep"; text = "Build Cards are your defenses. Dark ones are locked - DOUBLE-CLICK to buy with Sanity, then click once to select and click a tile to place! You can also DRAG a build card onto the map to buy and place in one motion."; _targetZoom = 1.0f; _showInventory = true; _tutorialHighlight = TutorialHighlight.BuildSlots; break;
            case 11: speaker = "Human"; text = "Double-click to buy, single click to select, then click the map tile... or just drag it. Got it."; _targetZoom = 1.0f; _showInventory = false; break;
            case 12: speaker = "Sheep"; text = "Hover any card to see its details in the panel on the left. Towers, fence, spikes, dream-catcher, you name it! Go ahead, try it now."; _targetZoom = 1.0f; _showInventory = true; _tutorialHighlight = TutorialHighlight.Expanded; break;
            case 13: speaker = "Human"; text = "Okay, what about the action cards?"; _targetZoom = 1.0f; _showInventory = false; break;
            case 14: speaker = "Sheep"; text = "Stack Action cards appear in the two slots on the right of your hand. You EARN them by moving me to a new meadow. Double-click to use them!"; _targetZoom = 1.0f; _showInventory = true; _tutorialHighlight = TutorialHighlight.StackSlots; break;
            case 15: speaker = "Sheep"; text = "Shuffle reshuffles your build cards. Discount makes the next card cheaper. Refill fills your hand cards."; _targetZoom = 1.0f; _showInventory = false; break;
            case 16: speaker = "Human"; text = "So I move my sheep to earn stack cards? Neat!"; _targetZoom = 1.0f; _showInventory = false; break;
            case 17: speaker = "Sheep"; text = "Exactly. And you can hover any structure on the map to see its info card with Move and Destroy buttons."; _targetZoom = 1.0f; _showInventory = false; break;
            case 18: speaker = "Sheep"; text = "Moving a structure costs 2 sanity. Destroying is free. Both need a double-click to confirm."; _targetZoom = 1.0f; _showInventory = false; break;
            case 19: speaker = "Human"; text = "Okay, okay I'm bored of explanations, I wanna learn by doing!"; break;
            case 20: speaker = "Sheep"; text = "Okay cool! Survive 10 nights to win the game! Good Luck!"; break;
            default: speaker = (_tutorial_step % 2 == 0) ? "Sheep" : "Human"; text = "DREAMWARD!!!!"; break;
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

        // ── 4. DRAW CARD-STRIP REGION HIGHLIGHTS────────────────────────────────
        if (_tutorialHighlight != TutorialHighlight.None && _tutorialHighlight != TutorialHighlight.MeadowTiles)
        {
            DrawCardRegionHighlight(gameTime, _tutorialHighlight, vw, vh);
        }    

        // --- 5. DIALOG BOX UI ---
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
        _tutorialTextComplete = !isTyping;
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

    // ── Draws a yellow highlight box + arrow around a card-strip region ─────────
    private void DrawCardRegionHighlight(GameTime gameTime, TutorialHighlight region, int vw, int vh)
    {
        Rectangle stripBounds = cardRenderer.CardStripBounds;
        Rectangle targetRect;
        string    regionLabel;
        int slotW = stripBounds.Width / 6;

        switch (region)
        {
            case TutorialHighlight.CardStrip:
                targetRect  = stripBounds;
                regionLabel = "Your Cards";
                break;
            case TutorialHighlight.MapSlots:
                targetRect  = new Rectangle(stripBounds.X, stripBounds.Y, slotW, stripBounds.Height);
                regionLabel = "Map Action Cards";
                break;
            case TutorialHighlight.StackSlots:
                // Stack slots are the last 2 in the 6-card fan
                targetRect  = new Rectangle(stripBounds.X + 4 * slotW, stripBounds.Y, 2 * slotW, stripBounds.Height);
                regionLabel = "Stack Action Cards";
                break;
            case TutorialHighlight.BuildSlots:
                // Build slots are the first 4
                targetRect  = new Rectangle(stripBounds.X, stripBounds.Y, 4 * slotW, stripBounds.Height);
                regionLabel = "Build Cards";
                break;
            case TutorialHighlight.Expanded:
                targetRect  = cardRenderer.ExpandedPanelBounds;
                regionLabel = "Card Info Panel";
                break;
            default:
                return;
        }

        int border = 4;
        Color hc   = Color.Yellow;
        spriteBatch.Draw(_dummy, new Rectangle(targetRect.X - border, targetRect.Y - border, targetRect.Width + border * 2, border), hc);
        spriteBatch.Draw(_dummy, new Rectangle(targetRect.X - border, targetRect.Bottom, targetRect.Width + border * 2, border), hc);
        spriteBatch.Draw(_dummy, new Rectangle(targetRect.X - border, targetRect.Y, border, targetRect.Height), hc);
        spriteBatch.Draw(_dummy, new Rectangle(targetRect.Right, targetRect.Y, border, targetRect.Height), hc);

        if (!string.IsNullOrEmpty(regionLabel))
        {
            float labelScale = 0.75f;
            Vector2 labelSize = _font.MeasureString(regionLabel) * labelScale;
            spriteBatch.DrawString(_font, regionLabel,
                new Vector2(targetRect.X, targetRect.Y - labelSize.Y - 6f),
                Color.Yellow, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        }

        float bounce   = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 8) * 6;
        Vector2 arrowTip = new Vector2(targetRect.Center.X, targetRect.Y - border - 60 - bounce);
        DrawPointerArrowDown(arrowTip, hc);
    }

    /// <summary>
    /// Draws a simple pixel-art arrow pointing left ( <--- )    /// </summary>
    private void DrawPointerArrow(Vector2 tipPos, Color color)
    {
        // Shaft
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X, (int)tipPos.Y - 4, 30, 8), color);
        // Head (Triangle-ish)
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 6, (int)tipPos.Y - 12, 6, 24), color);
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 12, (int)tipPos.Y - 8, 6, 16), color);
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 18, (int)tipPos.Y - 4, 6, 8), color);
    }

    private void DrawPointerArrowDown(Vector2 tipPos, Color color)
    {
        // Shaft (vertical)
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 4, (int)tipPos.Y - 30, 8, 30), color);
        // Head (Triangle-ish, pointing down)
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 12, (int)tipPos.Y,      24, 6), color);
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 8,  (int)tipPos.Y + 6,  16, 6), color);
        spriteBatch.Draw(_dummy, new Rectangle((int)tipPos.X - 4,  (int)tipPos.Y + 12,  8, 6), color);
    }



    private Rectangle PickFloatSrc(Rectangle dest, bool pressed)
    {
        bool wide = (float)dest.Width / dest.Height > FloatBtn.Width / (float)FloatBtn.Height;
        return wide ? (pressed ? LongFloatBtnPrs : LongFloatBtn) : (pressed ? FloatBtnPrs : FloatBtn);
    }

    private Rectangle UniformDest(Rectangle rect, Rectangle src)
    {
        float scale = rect.Width / (float)src.Width;
        int h = (int)(src.Height * scale);
        return new Rectangle(rect.X, rect.Center.Y - h / 2, rect.Width, h);
    }

    private void DrawButton(Rectangle rect, string text, bool pressed = false, float progress = 0f)
    {
        Rectangle src = PickFloatSrc(rect, pressed);
        float scale = SpriteScale;
        int drawW = (int)(src.Width  * scale);
        int drawH = (int)(src.Height * scale);
        Rectangle drawDest = new Rectangle(rect.Center.X - drawW / 2, rect.Center.Y - drawH / 2, drawW, drawH);

        if(progress > 0)
        {
            spriteBatch.End();
            float progressWidthInPixels = src.X + (src.Width * progress);
            float uvThreshold = progressWidthInPixels / InterfaceTexture.Width;

            GamelabGame.Instance.LoadingShader.Parameters["ProgressThreshold"].SetValue(uvThreshold);
            GamelabGame.Instance.LoadingShader.Parameters["OverlayColor"].SetValue(new Vector4(0.5f, 0.5f, 0.5f, 1.0f));

            spriteBatch.Begin(effect: GamelabGame.Instance.LoadingShader,
                        samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());
            spriteBatch.Draw(InterfaceTexture, drawDest, src, Color.White);
            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewportAdapter.GetScaleMatrix());
        }
        else
        {
            spriteBatch.Draw(InterfaceTexture, drawDest, src, Color.White);
        }
        var size = _font.MeasureString(text);
        float sc = Math.Min(drawW / size.X * 0.6f, drawH / size.Y * 0.6f);
        float pressOffset = pressed ? scale : 0f;
        spriteBatch.DrawString(_font, text,
            new Vector2(drawDest.Center.X - size.X * sc * 0.5f,
                        drawDest.Center.Y - size.Y * sc * 0.5f + pressOffset),
            COLOR_D, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
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

    /// <summary>
    /// True if the hammer (destroy mode) can remove this structure.
    /// Mirrors the existing destroy card rule: structure must be CanMove and not a VoidTile.
    /// Excludes Sheep tile, Tent, natural Meadow, and trees.
    /// </summary>
    private static bool IsHammerDestroyable(Structure s)
    {
        if (s == null) return false;
        if (s is VoidTile) return false;
        return s.CanMove;
    }

    private void DrawHammerButton()
    {
        // Background plate: pressed when held or active (selected = indented + grayed)
        var src = (_hammerActive || _hammerBtnPressed) ? FloatBtnPrs : FloatBtn;
        Rectangle hammerDest = UniformDest(_hammerBtn, src);
        spriteBatch.Draw(InterfaceTexture, hammerDest, src, Color.White);

        // Hammer overlay, centered with a small inset
        if (_hammerTexture != null)
        {
            int inset = Math.Max(6, _hammerBtn.Width / 8);
            Rectangle overlay = new Rectangle(
                _hammerBtn.X + inset,
                _hammerBtn.Y + inset,
                _hammerBtn.Width  - inset * 2,
                _hammerBtn.Height - inset * 2);
            // Gray out when active (selected); full color otherwise
            Color tint = _hammerActive ? new Color(140, 140, 140) : Color.White;
            spriteBatch.Draw(_hammerTexture, overlay, null, tint);
        }
    }

    private void ToggleActiveCard(CardManager cm, Card card)
    {
        if (ReferenceEquals(cm._activeMapCard, card))
        {
            if (cm._previewStructure != null && cm._selectedTile != (-1, -1))
                GamelabGame.MapManager.PlaceEntity(cm._previewStructure, cm._selectedTile);
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