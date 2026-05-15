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
#region Managers and Renderers

    private MapManager mapManager => GamelabGame.MapManager;
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CardManager cardManager => GamelabGame.CardManager;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    private readonly MapRenderer mapRenderer;
    private readonly EntityRenderer entityRenderer;
    private readonly CardRenderer cardRenderer;

#endregion
    
    private RenderTarget2D _nightBackgroundCache;
    private Point _nightBackgroundCacheSize;
    
    private enum BtnId { Inventory, TowerRange, Speed, Move, Hammer, StartNight, Count }

    private Rectangle _speedBtn;

    // Cards
    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private TextureAtlas InterfaceAtlas => GamelabGame.TextureAtlas[gamePhaseManager.IsDay ? "day_interface" : "interface"];
    private Rectangle Region(string name) => InterfaceAtlas.GetRegion(name + "0").SourceRectangle;
    private Dictionary<string, Rectangle> _cardRegions;

    // Navigation button
    private bool _showInventory = true;
    private Rectangle _inventoryBtn;
    private bool _showTowerRange = false;
    private Rectangle _towerRangeBtn;
    private bool _offsetInitialized = false;
    private bool _moveActive  = false;
    private bool _hammerActive = false;
    private readonly bool[]         _btnClickedInside = new bool[(int)BtnId.Count];
    private readonly ButtonResult[] _btnResults       = new ButtonResult[(int)BtnId.Count];
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
    private float _mapDragDistance;
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
    private static readonly Rectangle ArrowBtn        = new(112, 176, 32, 32);
    private static readonly Rectangle ArrowBtnPrs     = new(112, 208, 32, 32);
    private static readonly Rectangle HintC     = new(80, 176, 32, 16);
    private static readonly Rectangle HintS     = new(80, 192, 32, 16);
    private static readonly Rectangle HintSpace = new(80, 208, 32, 16);
    private static readonly Rectangle HintR     = new(80, 224, 32, 16);
    private readonly Color COLOR_D = new(0x29, 0x24, 0x10);    
    private readonly Color COLOR_L = new Color(0xc7, 0xb8, 0x9c);

    private Point screenSize => viewportAdapter.PointToScreen(new Point(GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth, GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight));


    public MapScreen(GamelabGame game, bool init = false) : base(game)
    {
        // Map setup
        mapRenderer = new MapRenderer();
        entityRenderer = new EntityRenderer();
        cardRenderer = new CardRenderer(); 
        if(init)
        {
            mapManager.FillMap();
        }
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

        // 1x1 white texture used by EntityRenderer to draw HP bars
        entityRenderer.SetHealthBarTexture(_dummy);
        _interfaceTexture = Content.Load<Texture2D>("Sprites/interface");
        _dayInterfaceTexture = Content.Load<Texture2D>("Sprites/day_interface");

        // Card textures
        _cardTexture = Content.Load<Texture2D>("Sprites/cards");
        _cardRegions = TextureAtlas.LoadRegions(Content, "Sprites/cards.json");
        
        if (!_offsetInitialized)
        {
            mapManager.InitOffset(screenSize);
            _offsetInitialized = true;
        }
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
        _speedBtn = new Rectangle(vw - 260, 20, 240, 96);
        cardRenderer.UpdateUIButtonLayout(vw, vh, _showInventory, ref _inventoryBtn, ref _towerRangeBtn, ref _startNightBtn, ref _hammerBtn, ref _moveBtn);
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
        TutorialManager.Update(gameTime, viewportAdapter.BoundingRectangle);
        cardRenderer.SetMousePos(mousePos);
        RebuildButtonLayout();
        var rc = cardRenderer.RightmostCardRightCenter;
        if (rc != Vector2.Zero)
        {
            float sc = SpriteScale;
            int bW = (int)(ArrowBtn.Width  * sc);
            int bH = (int)(ArrowBtn.Height * sc);
            _inventoryBtn = new Rectangle((int)rc.X, (int)(rc.Y - bH / 2f), bW, bH);
        }
        bool isDay         = gamePhaseManager.IsDay;
        bool notTransition = !gamePhaseManager.IsInTransition;
        Rectangle[] btns   = { _inventoryBtn, _towerRangeBtn, _speedBtn, _moveBtn, _hammerBtn, _startNightBtn };
        bool[] btnActive   = {
            notTransition && isDay,             // Inventory
            !isDay || (notTransition && isDay), // TowerRange: night block OR day block
            !isDay,                             // Speed (night only)
            notTransition && isDay,             // Move
            notTransition && isDay,             // Hammer
            notTransition && isDay,             // StartNight
        };
        for (int i = 0; i < btns.Length; i++)
            _btnResults[i] = UpdateButton(btns[i], mouse, mousePos, ref _btnClickedInside[i], btnActive[i]);
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
            {
                if (cardManager._activeMapCard != null || _moveActive || _hammerActive)
                {
                    CancelMoveMode();
                    cardManager._activeMapCard = null;
                    cardManager._previewStructure = null;
                    cardManager._selectedTile = (-1, -1);
                    _isDraggingCard = false;
                    _dragCard = null;
                    _hammerActive = false;
                }
                else
                {
                    Game.SwitchToScreen(Game.StartScreenInstance);
                }
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
            if (keyboard.WasKeyReleased(Keys.B))  cardManager.ForceSpawnInto("buff",         cardManager.BuildSlots);
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
            if (keyboard.IsKeyDown(Keys.S))
                _btnResults[(int)BtnId.Speed] = ButtonResult.Pressed;
            if (keyboard.WasKeyReleased(Keys.S))
                GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 3f : 1f;
            //Slowdown
#if DEBUG
            if(keyboard.WasKeyReleased(Keys.D))
            {
                GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 0.25f : 1f;
            }
#endif
            if (keyboard.IsKeyDown(Keys.R))
                _btnResults[(int)BtnId.TowerRange] = ButtonResult.Pressed;
            if (keyboard.WasKeyReleased(Keys.R))
                _showTowerRange = !_showTowerRange;
            //set cursor state
            if(!gamePhaseManager.IsInTransition){
                if (_showInventory)
                {
                    foreach (var (rect, card) in cardManager.CardRects)
                    {
                        if (card != null && rect.Contains(mousePos) && !cardRenderer.IsCardUnaffordable(card, cardManager))
                        {
                            GamelabGame.IsHovering = true;
                            break;
                        }
                    }
                }
            }
            if (!gamePhaseManager.IsInTransition && gamePhaseManager.IsDay)
            {
                if (keyboard.IsKeyDown(Keys.C))
                    _btnResults[(int)BtnId.Inventory] = ButtonResult.Pressed;
                if (keyboard.WasKeyReleased(Keys.C))
                {
                    _showInventory = !_showInventory;
                    if (!_showInventory) cardRenderer.CollapseExpanded();
                    if (_hoveredStructure is Meadow) GamelabGame.IsHovering = true;
                    return;
                }
            }
            
            if(keyboard.IsKeyDown(Keys.Space) && !gamePhaseManager.IsInTransition)
            {
                _btnResults[(int)BtnId.StartNight] = ButtonResult.Pressed;
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
                    CancelMoveMode();
                    _hammerActive = false;
                    cardManager._selectedTile = (-1, -1);
                    cardManager._previewStructure = null;
                    cardManager._activeMapCard = null;  
                }
            }

            if(keyboard.WasKeyReleased(Keys.Space) && !gamePhaseManager.IsInTransition)
            {
                TutorialManager.HandleTutorialAction(TutorialAction.Click);
            }
                       
            // DEBUG: right click triggers a sanity-gain burst above the sheep
#if DEBUG
            if (mouse.WasButtonPressed(MouseButton.Right))
            {
                SpawnSanityGainOverSheep(20);
            }
#endif
            // Right-click to purchase a hovered build card
            if (mouse.WasButtonReleased(MouseButton.Right) && !gamePhaseManager.IsInTransition
                && gamePhaseManager.IsDay && _showInventory)
            {
                var (hoveredCard, hoveredSlot) = cardRenderer.HandleClick(mousePos, cardManager);
                if (hoveredCard != null && hoveredSlot != null && !hoveredSlot.Purchased
                    && (hoveredCard.Type == CardType.Build || hoveredCard.Type == CardType.MapAction))
                {
                    if (cardManager.TryPurchaseBuildCard(hoveredSlot, sanityManager))
                    {
                        AudioManager.Play("coinsplash");
                        _hammerActive = false;
                        CancelMoveMode();
                    }
                }
            }

            // Drag
            if (mouse.WasButtonPressed(MouseButton.Left))
            {
                var pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                _oldPos          = pos;
                _clickStarted    = true;
                _mapDragDistance = 0f;

                // Begin possible card drag
                _dragCard       = null;
                _isDraggingCard = false;
                if (!gamePhaseManager.IsInTransition && gamePhaseManager.IsDay && _showInventory)
                {
                    var (dragCandidate, _) = cardRenderer.HandleClick(pos, cardManager);
                    if ((dragCandidate?.Type == CardType.Build || dragCandidate?.Type == CardType.MapAction)
                        && dragCandidate?.Effect != CardEffect.Polymorph)
                    {
                        _dragCard     = dragCandidate;
                        _dragStartPos = pos;
                    }
                }

                // trigger unaffordable flash
                if(!gamePhaseManager.IsInTransition && _hoveredStructure is Meadow cantAffordSpot && !cantAffordSpot.HasSheep)
                {
                    int moveCost = (int)MathF.Round(MapUtils.CalculateTileDistance(mapManager.SheepPosition, cantAffordSpot.TilePosition) * MapManager.MoveCostMultiplier);
                    if(GamelabGame.SanityManager.Sanity <= moveCost)
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
                                    _hammerActive = false;
                                    CancelMoveMode();
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
                if (!_isDraggingCard && !_btnClickedInside.Any(b => b) &&
                    (!_showInventory || !cardRenderer.CardStripBounds.Contains(mousePos)))
                {
                    Vector2 deltaPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _oldPos;
                    if (deltaPos.Length() > 0)
                    {
                        mapManager.MoveMap(deltaPos, screenSize);
                        _mapDragDistance += deltaPos.Length();
                        if (_mapDragDistance > DRAG_THRESHOLD)
                            _isDrag = true;
                        _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                    }
                }

                // ── General hold ────────────────────────────────────────
                if (!_isDrag && !_isDraggingCard)
                {
                    // ── Sheep Hold grazing hold ────────────────────────────────────────
                    if(!gamePhaseManager.IsInTransition
                    && _hoveredStructure is Meadow grazingSpot
                    && !grazingSpot.HasSheep && !grazingSpot.IsGrazed)
                    {
                        int moveCost = (int)MathF.Round(
                            MapUtils.CalculateTileDistance(mapManager.SheepPosition, grazingSpot.TilePosition)
                            * MapManager.MoveCostMultiplier);
                        if (GamelabGame.SanityManager.Sanity > moveCost && grazingSpot.AddProgress(gameTime.ElapsedGameTime))
                        {
                            mapManager.TryMoveSheep(mousePos);
                        }
                    }
                    if(!gamePhaseManager.IsDay && _hoveredStructure is Bomb bomb)
                    {
                       if(bomb.AddProgress(gameTime.ElapsedGameTime))
                        {
                            bomb.ShouldBlowUp = true;
                        }
                    }
                }
            }

            //set the hovered tile
            bool mouseOverUI = (!gamePhaseManager.IsInTransition &&
                ((_showInventory && cardRenderer.CardStripBounds.Contains(mousePos))
                 || (_showInventory && cardRenderer.ExpandedPanelBounds != Rectangle.Empty && cardRenderer.ExpandedPanelBounds.Contains(mousePos))
                 || _inventoryBtn.Contains(mousePos)
                 || _towerRangeBtn.Contains(mousePos)
                 || _startNightBtn.Contains(mousePos)
                 || _moveBtn.Contains(mousePos)
                 || _hammerBtn.Contains(mousePos)))
                || (!gamePhaseManager.IsDay && _speedBtn.Contains(mousePos));
            
            _hoverTile = mouseOverUI ? (-1, -1) : mapManager.GetTileFromScreenPosition(mousePos);
            // BLOCK HOVER IF ON UI
            if (!mouseOverUI && _hoverTile.col >= 0)
            {
                _hoveredStructure = mapManager.GetStructure(_hoverTile);
            }
            else
            {
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
                if(!_isDrag)
                {
                    // ── Drop dragged card onto map ────────────────────────────────
                    if (_isDraggingCard && _dragCard != null)
                    {
                        bool overCardUI = cardRenderer.CardStripBounds.Contains(mousePos)
                            || cardRenderer.ExpandedPanelBounds.Contains(mousePos);
                        if (_hoverTile.col >= 0 && !overCardUI)
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
                        if (Btn(BtnId.Speed) == ButtonResult.Clicked)
                        {
                            GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 3f : 1f;
                            AudioManager.Play("rustle01");
                            return;
                        }
                        if (Btn(BtnId.TowerRange) == ButtonResult.Clicked)
                        {
                            _showTowerRange = !_showTowerRange;
                            _clickStarted = false;
                            return;
                        }
                        mapManager.HandleMapClick(mousePos);
                    }
                    if(!gamePhaseManager.IsInTransition && GraphicsDevice.PresentationParameters.Bounds.Contains(mouse.Position) && !_isDrag)
                    {
                        bool overCardStrip = _showInventory && cardRenderer.CardStripBounds.Contains(mousePos);
                        //start night
                        if (!overCardStrip && Btn(BtnId.StartNight) == ButtonResult.Clicked)
                        {
    #if DEBUG
                            gamePhaseManager.SwitchToNight();
    #else
                            if (gamePhaseManager.IsDay){
                                    mapManager.TriggerVoidEnclosureFall();
                                    gamePhaseManager.SwitchToNight();
                                }
    #endif
                            CancelMoveMode();
                            _hammerActive = false;
                            cardManager._selectedTile = (-1, -1);
                            cardManager._previewStructure = null;
                            cardManager._activeMapCard = null;  
                            _clickStarted = false;
                            return;
                        }
                        // ── Card strip clicks ─────────────────────────────────────
                        if (_showInventory && cardRenderer.CardStripBounds.Contains(mousePos))
                        {
                            var (clickedCard, clickedSlot) = cardRenderer.HandleClick(mousePos, cardManager);
    
                            if (clickedCard == null)
                                return;
    
                            bool isDouble = IsDoubleClick(mouse, gameTime);
    
                            // ── BUILD / MAP ACTION ─────────────────────────────
                            if (clickedCard.Type == CardType.Build || clickedCard.Type == CardType.MapAction)
                            {
                                // ── POLYMORPH: double-click to auto-target sheep ──
                                if (clickedCard.Effect == CardEffect.Polymorph)
                                {
                                    if (isDouble)
                                    {
                                        Meadow sheep = mapManager.GetSheep();
                                        if (sheep != null && !sheep.IsPolymorphed)
                                        {
                                            if (!clickedSlot.Purchased)
                                            {
                                                if (!cardManager.TryPurchaseBuildCard(clickedSlot, sanityManager))
                                                {
                                                    _clickStarted = false;
                                                    return;
                                                }
                                                AudioManager.Play("coinsplash");
                                            }
                                            if (cardManager.UsePolymorphCard(clickedCard, mapManager))
                                            {
                                                AudioManager.Play("card-place");
                                                cardRenderer.CollapseExpanded();
                                            }
                                        }
                                    }
                                    _clickStarted = false;
                                    return;
                                }

                                if (clickedSlot != null && !clickedSlot.Purchased)
                                {
                                    if (isDouble)
                                        if (cardManager.TryPurchaseBuildCard(clickedSlot, sanityManager))
                                        {
                                            AudioManager.Play("coinsplash");
                                            _hammerActive = false;
                                            CancelMoveMode();
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
                                if (clickedCard.Effect == CardEffect.Shuffle ||
                                    clickedCard.Effect == CardEffect.Discount ||
                                    clickedCard.Effect == CardEffect.Refill)
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

                        if (Btn(BtnId.Inventory) == ButtonResult.Clicked)
                        {
                            _showInventory = !_showInventory;
                            if (!_showInventory) cardRenderer.CollapseExpanded();
                            _clickStarted = false;
                            return;
                        }
                        if (Btn(BtnId.TowerRange) == ButtonResult.Clicked)
                        {
                            _showTowerRange = !_showTowerRange;
                            _clickStarted = false;
                            return;
                        }
                        if (Btn(BtnId.Move) == ButtonResult.Clicked)
                        {
                            if (_moveActive)
                            {
                                CancelMoveMode();
                            }
                            else
                            {
                                _hammerActive = false;
                                if (GamelabGame.SanityManager.Sanity <= 2)
                                {
                                    _clickStarted = false;
                                    return;
                                }
                                _moveActive = true;
                                cardManager._activeMapCard = null;
                                cardManager._previewStructure = null;
                                cardManager._selectedTile = (-1, -1);
                            }

                            _clickStarted = false;
                            return;
                        }
                        if (Btn(BtnId.Hammer) == ButtonResult.Clicked)
                        {
                            _hammerActive = !_hammerActive;
                            if (_hammerActive)
                            {
                                CancelMoveMode();
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
                                bool overCardUI = cardRenderer.CardStripBounds.Contains(mousePos)
                                    || cardRenderer.ExpandedPanelBounds.Contains(mousePos);
                                if (!overCardUI)
                                {
                                    if (cardManager.HandleMapCardUse(mapManager, mousePos))
                                        cardRenderer.CollapseExpanded();
                                }
                                _clickStarted = false;
                                _isDrag = false;
                                return;
                            }
                        }
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
        float fallProgress = (float)mapManager.TotalFallTime.TotalSeconds < 1.0f ? 0 : (float)mapManager.TotalFallTime.TotalSeconds / (float)mapManager.MaxTotalFallTime.TotalSeconds;
        GraphicsDevice.Clear(Color.Lerp(Color.CornflowerBlue, Color.Black, fallProgress));
        // this still does not account for pixel tearing, but I think I prefer that over blurriness or choppy zoom
        float viewportScale = viewportAdapter.GetScaleMatrix().M11;
        SamplerState sampler = mapManager.Scale.X * viewportScale < 1f ? SamplerState.LinearClamp : SamplerState.PointClamp;

        DrawNightBackgroundIfNight();

        GamelabGame.RenderTransform = viewportAdapter.GetScaleMatrix();
        GamelabGame.RenderSampler = sampler;
        spriteBatch.Begin(samplerState: sampler, transformMatrix: viewportAdapter.GetScaleMatrix());
        mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle, gameTime);
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

        GamelabGame.RenderTransform = viewportAdapter.GetScaleMatrix();
        GamelabGame.RenderSampler = sampler;
        spriteBatch.Begin(samplerState: sampler,
                        transformMatrix: viewportAdapter.GetScaleMatrix());

        // ✅ Map        
        if (cardManager._activeMapCard != null &&
            (cardManager._activeMapCard.Effect == CardEffect.Move     ||
             cardManager._activeMapCard.Effect == CardEffect.Destroy  ||
             cardManager._activeMapCard.Effect == CardEffect.Polymorph) &&
            _hoverTile.col >= 0)
        {
            mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle, gameTime, _hoverTile, _showTowerRange, cardRenderer);
        }
        else
        {
            mapRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle, gameTime, drawRange: _showTowerRange, cardRenderer: cardRenderer);
        }
        if(!gamePhaseManager.IsInTransition)
        {
            if (cardRenderer.ExpandedCard == null)
                cardRenderer.DrawPreview(spriteBatch, cardManager, mapManager, _hoverTile);
            if (!gamePhaseManager.IsInTransition &&
                (cardManager._activeMapCard != null || _isDraggingCard))
            {
                cardRenderer.DrawAllTowerRanges(spriteBatch, mapManager);
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
                if (cardManager._selectedTile == (-1, -1))
                {
                    // Pre-pick: highlight all movable structures in yellow
                    for (int col = 0; col < mapManager.Columns; col++)
                        for (int row = 0; row < mapManager.Rows; row++)
                        {
                            var s = mapManager.GetStructure((col, row));
                            if (s != null && s.CanMove)
                                cardRenderer.DrawTileHighlightPublic(spriteBatch, mapManager, (col, row), Color.Yellow);
                        }
                }
                else if (_hoverTile.col >= 0)
                {
                    // Post-pick: only highlight the hovered tile red if it's blocked
                    var hovered = mapManager.GetStructure(_hoverTile);
                    if (hovered != null)
                        cardRenderer.DrawTileHighlightPublic(spriteBatch, mapManager, _hoverTile, Color.Red);
                }
            }
            // ── Move-mode placement preview ───────────────────────────────────────
            if (_moveActive && cardManager._selectedTile != (-1, -1)
                && cardManager._previewStructure != null)
            {
                var moving = cardManager._previewStructure;
                bool atOrigin = _hoverTile.col < 0;
                var targetTile = atOrigin ? cardManager._selectedTile : _hoverTile;

                Color ghost;
                if (atOrigin)
                    ghost = Color.White;
                else
                {
                    bool valid = mapManager.GetStructure(_hoverTile) == null;
                    ghost = valid ? new Color(0x11, 0x7a, 0x15) : new Color(220, 60, 60);
                }

                var oldPos = moving.TilePosition;
                moving.TilePosition = targetTile;

                if (!atOrigin)
                {
                    if (moving is Tower movingTower)
                    {
                        cardRenderer.DrawTowerRangePreview(spriteBatch, mapManager, movingTower, ghost);
                        cardRenderer.DrawAllTowerRanges(spriteBatch, mapManager);
                    }
                    else
                    {
                        cardRenderer.DrawTileHighlightPublic(spriteBatch, mapManager, targetTile, ghost);
                    }
                }
                moving.PreviewTint = ghost;
                moving.DrawBase(spriteBatch, alpha: 0.5f);
                moving.Draw(spriteBatch, alpha: 0.5f);
                moving.PreviewTint = null;
                moving.TilePosition = oldPos;
            }
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
        // Structure._screenTransform = viewportAdapter.GetScaleMatrix();
        PopUpRenderer.ScreenTransform = viewportAdapter.GetScaleMatrix();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        PopUpRenderer.Draw(spriteBatch, viewportAdapter.BoundingRectangle);
        spriteBatch.End();

        // UI always uses point clamp — cards are pixel art at fixed size, no zoom applied
        spriteBatch.Begin(samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());
        if (!gamePhaseManager.IsDay)
        {
            string speedLabel = GamelabGame.GameSpeed > 1f ? "Speed: 3x" : "Speed: 1x";
            DrawButton(_speedBtn,      speedLabel,                                                Btn(BtnId.Speed)      == ButtonResult.Pressed, keyHint: HintS);
            DrawButton(_towerRangeBtn, _showTowerRange ? "Hide Range" : "Tower Range",           Btn(BtnId.TowerRange) == ButtonResult.Pressed, keyHint: HintR);
        }
        int vw = (int)viewportAdapter.VirtualWidth;
        int vh = (int)viewportAdapter.VirtualHeight;

        DrawSanityPanel();

        if (!gamePhaseManager.IsInTransition && gamePhaseManager.IsDay)
        {
            bool invPrs = Btn(BtnId.Inventory) == ButtonResult.Pressed;
            Rectangle arrowSrc = invPrs ? ArrowBtnPrs : ArrowBtn;
            float arrowSc = SpriteScale;
            int arrowW = (int)(arrowSrc.Width  * arrowSc);
            int arrowH = (int)(arrowSrc.Height * arrowSc);
            spriteBatch.Draw(InterfaceTexture, new Rectangle(_inventoryBtn.X, _inventoryBtn.Y, arrowW, arrowH), arrowSrc, Color.White);
            DrawButton(_towerRangeBtn, _showTowerRange ? "Hide Range"    : "Tower Range",  Btn(BtnId.TowerRange) == ButtonResult.Pressed, keyHint: HintR);
            DrawButton(_startNightBtn, "Start Night", Btn(BtnId.StartNight) == ButtonResult.Pressed, (float)(_elapsedStartNightTime/_startNightTime), forceLong: true, keyHint: HintSpace);
            DrawButton(_moveBtn,    _moveActive    ? "Move (ON)"    : "Move (2 Sanity)", Btn(BtnId.Move)   == ButtonResult.Pressed || _moveActive);
            DrawButton(_hammerBtn,  _hammerActive  ? "Destroy (ON)" : "Destroy",         Btn(BtnId.Hammer) == ButtonResult.Pressed || _hammerActive);
            if (_showInventory)
            {
                cardRenderer.DrawCardStrip(spriteBatch, _dummy, _cardTexture, _cardRegions, _font,cardManager, vw, vh);
                cardRenderer.DrawExpandedPanel(spriteBatch, _dummy, _font, cardManager, vw, vh);
            }
        }
        DrawDayPopup(vw, vh);
        ParticleManager.Draw(spriteBatch, viewportAdapter.BoundingRectangle);
        spriteBatch.End();
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

        int iconSize = (int)(16 * SpriteScale);
        int iconGap = 8;

        spriteBatch.Draw(InterfaceTexture,
            new Rectangle((int)pos1.X, (int)pos1.Y + 6, iconSize, iconSize),
            Region("sanityIcon"), Color.White);
        spriteBatch.Draw(InterfaceTexture,
            new Rectangle((int)pos1.X, (int)pos2.Y + 6, iconSize, iconSize),
            Region("timeIcon"), Color.White);

        int textX = (int)pos1.X + iconSize + iconGap;
        DrawValueBar(textX, (int)pos1.Y, sanityBarWidth, barHeight,
            Math.Clamp(sanityManager.Sanity, 0, MaxSanity), MaxSanity, gamePhaseManager.IsDay ? new Color(104, 201, 255) : new Color(138, 198, 204));
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



    private Rectangle PickFloatSrc(Rectangle dest, bool pressed)
    {
        bool wide = (float)dest.Width / dest.Height > FloatBtn.Width / (float)FloatBtn.Height;
        return wide ? (pressed ? LongFloatBtnPrs : LongFloatBtn) : (pressed ? FloatBtnPrs : FloatBtn);
    }

    private ButtonResult Btn(BtnId id) => _btnResults[(int)id];

    private void DrawButton(Rectangle rect, string text, bool pressed = false, float progress = 0f, Texture2D overrideTexture = null, bool forceLong = false, Rectangle? keyHint = null)
    {
        Texture2D tex = overrideTexture ?? InterfaceTexture;
        Rectangle src = forceLong ? (pressed ? LongFloatBtnPrs : LongFloatBtn) : PickFloatSrc(rect, pressed);
        float scale = SpriteScale;
        int drawW = (int)(src.Width  * scale);
        int drawH = (int)(src.Height * scale);
        int drawX = forceLong ? rect.Right - drawW : rect.Center.X - drawW / 2;
        Rectangle drawDest = new Rectangle(drawX, rect.Center.Y - drawH / 2, drawW, drawH);

        if(progress > 0)
        {
            spriteBatch.End();
            float progressWidthInPixels = src.X + (src.Width * progress);
            float uvThreshold = progressWidthInPixels / tex.Width;

            GamelabGame.Instance.LoadingShader.Parameters["ProgressThreshold"].SetValue(uvThreshold);
            GamelabGame.Instance.LoadingShader.Parameters["OverlayColor"].SetValue(new Vector4(0.5f, 0.5f, 0.5f, 1.0f));

            spriteBatch.Begin(effect: GamelabGame.Instance.LoadingShader,
                        samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());
            spriteBatch.Draw(tex, drawDest, src, Color.White);
            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewportAdapter.GetScaleMatrix());
        }
        else
        {
            spriteBatch.Draw(tex, drawDest, src, Color.White);
        }
        var size = _font.MeasureString(text);
        float sc = Math.Min(drawW / size.X * 0.6f, drawH / size.Y * 0.6f);
        float pressOffset = pressed ? scale : 0f;
        spriteBatch.DrawString(_font, text,
            new Vector2(drawDest.Center.X - size.X * sc * 0.5f,
                        drawDest.Center.Y - size.Y * sc * 0.5f + pressOffset),
            COLOR_D, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);

        if (keyHint.HasValue)
        {
            var h = keyHint.Value;
            int hintW = (int)(h.Width  * scale);
            int hintH = (int)(h.Height * scale);
            Rectangle hintDest = new(drawDest.Right - hintW, drawDest.Bottom - hintH, hintW, hintH);
            spriteBatch.Draw(tex, hintDest, h, Color.White);
        }
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

    private void CancelMoveMode()
    {
        // If a structure was picked up but not placed, put it back
        if (_moveActive && cardManager._selectedTile != (-1, -1) && cardManager._previewStructure != null)
        {
            cardManager._previewStructure.TilePosition = cardManager._selectedTile;
            mapManager.PlaceEntity(cardManager._previewStructure, cardManager._selectedTile);
        }
        _moveActive = false;
        cardManager._selectedTile = (-1, -1);
        cardManager._previewStructure = null;
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
            // Restore any in-progress move before switching cards
            if (cm._previewStructure != null && cm._selectedTile != (-1, -1))
                GamelabGame.MapManager.PlaceEntity(cm._previewStructure, cm._selectedTile);
            CancelMoveMode();
            cm._activeMapCard    = card;
            cm._selectedTile     = (-1, -1);
            cm._previewStructure = null;
        }
    }

}