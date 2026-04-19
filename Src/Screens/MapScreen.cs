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
    private bool _is_active_tutorial = false;
    private Rectangle _tutorial_btn;
    private int _tutorial_step = 0;
    private bool _tutorial_btn_pressed = false; 
    private int _lastAudioStep = -1;
    private float _textTimer = 0f;
    private int _charIndex = 0;
    private int _previousTutorialStep = -1;


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
    private bool _toCardsBtnPressed   = false;
    private bool _inventoryBtnPressed  = false;
    private bool _towerRangeBtnPressed = false;

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
            //new
            { "void",      new(0, 1664, 128, 128) },
            { "spikes",      new(0, 1792, 128, 128) },
            { "decoy",      new(0, 1920, 128, 128) },
            { "buff",      new(0, 2048, 128, 128) }

        };
        
        mapManager.InitOffset(screenSize);
    }
    private void update_card_button()
    {
        int vw = viewportAdapter.VirtualWidth;
        int vh = viewportAdapter.VirtualHeight;
        _toCardScreenBtn = new Rectangle(vw-183, 20, 163, 63);
        _tutorial_btn = new Rectangle(vw-183, 80, 163, 63);
        if (gamePhaseManager.IsDay)
        {
            cardRenderer.UpdateUIButtonLayout(vw, vh, _showInventory,_font, ref _cardStartY, ref _inventoryBtn, ref _towerRangeBtn);
        }
    }
    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        AudioManager.PlayMusic(gamePhaseManager.IsDay ? "day_music" : "night_music");
                
        GamelabGame.IsHovering = false;
        Vector2 mousePos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
        update_card_button();
        

        if(Game.IsActive)
        {
            // Exit
            if (keyboard.WasKeyReleased(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
            Game.SwitchToScreen(Game.StartScreenInstance);

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
            cardManager._previewStructure?.Update(gameTime);
            if(keyboard.WasKeyReleased(Keys.S))
            {
                GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 3f : 1f;
            }
            if(keyboard.WasKeyReleased(Keys.D))
            {
                GamelabGame.GameSpeed = GamelabGame.GameSpeed == 1f ? 0.25f : 1f;
            }
            if (mapManager.TentDestroyed)
            {
                Game.SwitchToScreen(new GameOverScreen(Game));
                return;
            }
            if (_toCardScreenBtn.Contains(mousePos) ||
                _inventoryBtn.Contains(mousePos) ||
                _towerRangeBtn.Contains(mousePos))
            {
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

            if(gamePhaseManager.IsDay && keyboard.WasKeyReleased(Keys.C))
            {
                Game.SwitchToScreen(Game.CardScreenInstance);
                AudioManager.Play("dragonflap");
                return;
            }

            // Drag
            if (mouse.WasButtonPressed(MouseButton.Left))
            {
                var pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                _toCardsBtnPressed    = _toCardScreenBtn.Contains(pos);
                _inventoryBtnPressed  = _inventoryBtn.Contains(pos);
                _towerRangeBtnPressed = _towerRangeBtn.Contains(pos);
                _oldPos = pos;
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
                _toCardsBtnPressed = _inventoryBtnPressed = _towerRangeBtnPressed = false;
                if(gamePhaseManager.IsDay && GraphicsDevice.PresentationParameters.Bounds.Contains(mouse.Position) && !_isDrag)
                {
                    if (_tutorial_btn.Contains(mousePos))
                    {
                        _is_active_tutorial = !_is_active_tutorial;
                        _tutorial_step = 0; 
                        AudioManager.Play("dragonflap");
                        _clickStarted = false;
                        return;
                    }
                    if (_is_active_tutorial)
                    {
                        _tutorial_step++;
                        //reached tutorial end
                        if(_tutorial_step == 4) //4 tutorial steps for the moment, change this later based on tutorial
                        {  
                           _is_active_tutorial = false; 
                        }
                        _clickStarted = false; 
                        return;
                    }
                    // Navigation to CardScreen
                    if (_toCardScreenBtn.Contains(mousePos))
                    {
                        Game.SwitchToScreen(Game.CardScreenInstance);
                        AudioManager.Play("dragonflap");
                        return;
                    }
                    if (_inventoryBtn.Contains(mousePos))
                    {
                        _showInventory = !_showInventory;
                        return;
                    }

                    if (_towerRangeBtn.Contains(mousePos))
                    {
                        _showTowerRange = !_showTowerRange;
                        return;
                    }
                    // Check USE buttons on cards
                    if (cardManager.HandleCardUseClicks(mousePos, (int)viewportAdapter.VirtualWidth, (int)viewportAdapter.VirtualHeight))
                        return;

                    if (cardManager._activeMapCard != null)
                    {
                        if (cardManager.HandleMapCardUse(mapManager, mousePos))
                            return;
                        return;
                    }
                    mapManager.HandleMapClick(mousePos);
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
        }
        if (_showTowerRange && gamePhaseManager.IsDay)
        {
            cardRenderer.DrawAllTowerRanges(spriteBatch, mapManager);
        }
        cardRenderer.DrawPreview(spriteBatch, cardManager, mapManager, _hoverTile);

        spriteBatch.End();

        // UI always uses point clamp — cards are pixel art at fixed size, no zoom applied
        spriteBatch.Begin(samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());

        // draw sanity
        if (sanityManager != null)
        {
            string text = $"Sanity: {sanityManager.Sanity}";
            string dayText = $"Day: {gamePhaseManager.DayCount}/10";

            Vector2 pos1 = new Vector2(20, 20);
            Vector2 pos2 = new Vector2(20, 70);

            int pad = 10;

            Vector2 size1 = _font.MeasureString(text);
            Vector2 size2 = _font.MeasureString(dayText);

            int boxWidth = (int)Math.Max(size1.X, size2.X) + pad * 2;
            int boxHeight = (int)(size1.Y + size2.Y + (pos2.Y - pos1.Y - size1.Y)) + pad * 2;

            Color rectColor = gamePhaseManager.IsDay
                ? COLOR_L
                : new Color(33, 28, 11);      // #211c0b

            Color textColor = gamePhaseManager.IsDay
                ? COLOR_D
                : new Color(214, 214, 214);   // #d6d6d6

            spriteBatch.Draw(_dummy, new Rectangle((int)pos1.X - pad, (int)pos1.Y - pad, boxWidth, boxHeight), rectColor);
            spriteBatch.DrawString(_font, text, pos1, textColor);
            spriteBatch.DrawString(_font, dayText, pos2, textColor);
        }

        if(gamePhaseManager.IsDay)
        {
            DrawButton(_toCardScreenBtn, "Cards", _toCardsBtnPressed);
            //cardRenderer.UpdateUIButtonLayout((int)viewportAdapter.VirtualWidth, (int)viewportAdapter.VirtualHeight, _showInventory, _font, ref _cardStartY, ref _inventoryBtn, ref _towerRangeBtn);
            DrawButton(_inventoryBtn,    _showInventory ? "Hide Cards" : "Show Cards", _inventoryBtnPressed);
            DrawButton(_towerRangeBtn,   _showTowerRange ? "Hide Range" : "Tower Range", _towerRangeBtnPressed);
            DrawButton(_tutorial_btn, _is_active_tutorial? "close tutorial" : "Tutorial", _tutorial_btn_pressed);

            if (_showInventory)
            {
                cardRenderer.DrawCardSlots(spriteBatch, _dummy, _cardTexture, _cardRegions, _font, cardManager, _cardStartY, (int)viewportAdapter.VirtualWidth, (int)viewportAdapter.VirtualHeight);
            }
        }
        if (_is_active_tutorial)
        {

            DrawTutorialState(gameTime);
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
private void DrawTutorialState(GameTime gameTime)
{
    int vw = (int)viewportAdapter.VirtualWidth;
    int vh = (int)viewportAdapter.VirtualHeight;

    // Darken the background slightly to focus on the tutorial
    spriteBatch.Draw(_dummy, new Rectangle(0, 0, vw, vh), Color.Black * 0.4f);

    string speaker = "";
    string text = "";
    Rectangle? highlightBox = null;

    // Load the dialogue based on the current step
    switch (_tutorial_step)
    {
        case 0:
            speaker = "Human";
            text = "Huh... where am I? What is happening?";
            break;
        case 1:
            speaker = "Sheep";
            text = "Oh good, you're awake! Last night when you were sleeping, demons came and attacked me and you. Please build some defenses before the next night starts.";
            break;
        case 2:
            speaker = "Human";
            text = "Did they do something to us?";
            break;
        case 3:
            speaker = "Sheep";
            text = "Your sleep monsters went for your sanity and they also tried to eat me. The monsters want to attack sanity points. To do that they need to reach your tent. And they attack me too because I produce sanity each morning for you, if I survive the night.";
            
            // EXACT math to match your Sanity UI box perfectly
            string sanityText = $"Sanity: {sanityManager.Sanity}";
            string dayText = $"Day: {gamePhaseManager.DayCount}/10";
            Vector2 s1 = _font.MeasureString(sanityText);
            Vector2 s2 = _font.MeasureString(dayText);
            int boxWidth = (int)Math.Max(s1.X, s2.X) + 20; 
            int boxHeight = (int)(s1.Y + s2.Y + (70 - 20 - s1.Y)) + 20;
            
            highlightBox = new Rectangle(10, 10, boxWidth, boxHeight); 
            break;
    }

    // Draw the Highlight Border (if any)
    if (highlightBox.HasValue)
    {
        var h = highlightBox.Value;
        int border = 4;
        Color hc = Color.Yellow;
        spriteBatch.Draw(_dummy, new Rectangle(h.X - border, h.Y - border, h.Width + border * 2, border), hc); 
        spriteBatch.Draw(_dummy, new Rectangle(h.X - border, h.Bottom, h.Width + border * 2, border), hc);     
        spriteBatch.Draw(_dummy, new Rectangle(h.X - border, h.Y, border, h.Height), hc);                      
        spriteBatch.Draw(_dummy, new Rectangle(h.Right, h.Y, border, h.Height), hc);                           
    }

    // --- Draw the clean Dialog Box ---
    int dialogHeight = 140;
    Rectangle dialogRect = new Rectangle(50, vh - dialogHeight - 30, vw - 100, dialogHeight);
    
    spriteBatch.Draw(_dummy, dialogRect, COLOR_L); 
    spriteBatch.Draw(_dummy, new Rectangle(dialogRect.X + 2, dialogRect.Y + 2, dialogRect.Width - 4, dialogRect.Height - 4), COLOR_D);
    spriteBatch.Draw(_dummy, new Rectangle(dialogRect.X + 4, dialogRect.Y + 4, dialogRect.Width - 8, dialogRect.Height - 8), COLOR_L);

    // --- Determine Speaker Portrait ---
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

    // --- Typewriter & Audio Logic ---
    float textScale = 0.5f; 
    float maxTextWidth = (speaker == "Sheep") 
        ? portraitX - (dialogRect.X + textOffsetX) - 20 
        : dialogRect.Right - (dialogRect.X + textOffsetX) - 20;

    string wrappedText = WrapText(_font, text, maxTextWidth, textScale);

    // If the dialog step changed, reset the typewriter
    if (_previousTutorialStep != _tutorial_step)
    {
        _charIndex = 0;
        _textTimer = 0f;
        _previousTutorialStep = _tutorial_step;
    }

    bool isTyping = _charIndex < wrappedText.Length;

    if (isTyping)
    {
        _textTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        float timePerChar = 0.03f; // <-- Change this to make typing faster or slower

        while (_textTimer >= timePerChar && _charIndex < wrappedText.Length)
        {
            _charIndex++;
            _textTimer -= timePerChar;
            int frequency = speaker == "Human" ? 10:30; //frequency of soundeffects for human and sheep

            // Audio Blips! Play sound every 4 letters (and don't play on spaces/newlines)
            if (_charIndex % frequency == 4 && wrappedText[_charIndex - 1] != ' ' && wrappedText[_charIndex - 1] != '\n')
            {
                if (speaker == "Sheep") AudioManager.Play("sheep_baa");
                else if (speaker == "Human") AudioManager.Play("grunt_03");
            }
        }
    }

    // Only draw the characters we have revealed so far!
    string textToDraw = wrappedText.Substring(0, _charIndex);

    // --- Portrait Bouncing Logic ---
    // If typing, bounce up and down using a Sine wave. If done typing, sit still (0).
    int bounceOffset = isTyping ? (int)(Math.Sin(gameTime.TotalGameTime.TotalSeconds * 20) * 4) : 0;

    if (currentPortraitTex != null)
    {
        spriteBatch.Draw(
            currentPortraitTex, 
            new Vector2(portraitX, portraitY + bounceOffset), // Adding the bounce here!
            currentSourceRect, 
            Color.White, 
            0f, 
            Vector2.Zero, 
            portraitScale, 
            SpriteEffects.None, 
            0f
        );
    }

    // --- Draw the Text ---
    spriteBatch.DrawString(_font, speaker + ":", new Vector2(dialogRect.X + textOffsetX, dialogRect.Y + 15), Color.DarkRed, 0f, Vector2.Zero, textScale * 1.2f, SpriteEffects.None, 0f);
    spriteBatch.DrawString(_font, textToDraw, new Vector2(dialogRect.X + textOffsetX, dialogRect.Y + 45), COLOR_D, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

    // Draw "Click to continue" hint
    string hint = "(Click anywhere to continue)";
    Vector2 hintSize = _font.MeasureString(hint) * 0.4f;
    int hintX = speaker == "Sheep" ? portraitX - (int)hintSize.X - 20 : dialogRect.Right - (int)hintSize.X - 20;
    
    // Optional extra flair: make the "Click to continue" blink when typing is done!
    if (!isTyping)
    {
        float pulse = (float)Math.Abs(Math.Sin(gameTime.TotalGameTime.TotalSeconds * 3));
        spriteBatch.DrawString(_font, hint, new Vector2(hintX, dialogRect.Bottom - 20), Color.DarkOliveGreen * pulse, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
    }
}
  private void DrawDayPopup()
    {
        int w = (int)viewportAdapter.VirtualWidth;
        int h = (int)viewportAdapter.VirtualHeight;

        // Make the popup cover at least half the screen
        int boxWidth = w / 2;
        int boxHeight = h / 2;
        Rectangle box = new(w / 2 - boxWidth / 2, h / 2 - boxHeight / 2, boxWidth, boxHeight);

        // Faint white background (semi-transparent)
        spriteBatch.Draw(_dummy, box, Color.Beige * 0.6f);

        // Bigger text
        float scale = 1.25f; // scale up font
        Vector2 textSize = _font.MeasureString(_dayPopupText) * scale;
        Vector2 textPos = new Vector2(box.Center.X - textSize.X / 2, box.Center.Y - textSize.Y / 2);

        spriteBatch.DrawString(_font, _dayPopupText, textPos, COLOR_D, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
    private void DrawButton(Rectangle rect, string text, bool pressed = false)
    {
        var src = pressed ? BtnPressed : BtnNormal;
        spriteBatch.Draw(_interfaceTexture, rect, src, Color.White);
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
    
}