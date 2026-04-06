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
    private GamePhaseManager GamePhaseManager => GamelabGame.GamePhaseManager;
    private readonly MapRenderer mapRenderer;
    private readonly EntityRenderer entityRenderer;


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
    private Card _activeMapCard = null;
    private (int,int) _selectedTile = (-1,-1);
    private (int col, int row) _hoverTile = (-1,-1);
    private bool _showDayPopup = false;
    private string _dayPopupText = "";
    private float _popupTimer = 0f;
    private Structure _previewStructure = null;
    private Structure _hoveredStructure;

    private readonly Color SLOT_COLOR = new Color(0xbc, 0xb6, 0x9d);  
    private readonly Color BTN_COLOR = new Color(0xbc, 0xb6, 0x9d);  
    private readonly Color BTN_TEXT_COLOR = new Color(0x74, 0x70, 0x5f);    
    private readonly Color SLOT_BORDER_COLOR = new Color(0xd8, 0xd4, 0xc1); 
    private readonly Color TEXT_COLOR = new Color(0xd8, 0xd4, 0xc1);

    private Point screenSize => viewportAdapter.PointToScreen(new Point(GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth, GamelabGame.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight));


    public MapScreen(GamelabGame game) : base(game)
    {
        // Map setup
        mapRenderer = new MapRenderer();
        entityRenderer = new EntityRenderer();
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

        // Spawn monster at edge heading toward tent
        if (keyboard.WasKeyPressed(Keys.Space))
        {
            int centerX = mapManager.Columns / 2;
            int centerY = mapManager.Rows / 2;
#if DEBUG
            AudioManager.Play("grunt_03");
            GamelabGame.GamePhaseManager.SwitchToNight();
#else
            if(GamelabGame.GamePhaseManager.IsDay){
                AudioManager.Play("grunt_03");
                GamelabGame.GamePhaseManager.SwitchToNight();
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
                    if (HandleCardUseClicks(pos))
                        return;

                    if (HandleMapCardUse(pos))
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
            if (_activeMapCard != null &&
                (_activeMapCard.Effect == CardEffect.Move ||
                _activeMapCard.Effect == CardEffect.Destroy ||
                _activeMapCard.Effect == CardEffect.Polymorph))
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
        if (_activeMapCard != null &&
            (_activeMapCard.Effect == CardEffect.Move ||
            _activeMapCard.Effect == CardEffect.Destroy ||
            _activeMapCard.Effect == CardEffect.Polymorph) &&
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
            DrawAllTowerRanges();
        }
        DrawPreview();
        
        // draw sanity
        if (sanityManager != null)
            {
                string text = $"Sanity: {sanityManager.Sanity}";
                spriteBatch.DrawString(_font, text,new Vector2(20, 20),TEXT_COLOR);

                string dayText = $"Day: {GamePhaseManager.DayCount}/10";
                spriteBatch.DrawString(_font, dayText,new Vector2(20, 50),TEXT_COLOR);

                string phaseText = GamePhaseManager.IsDay ? "Phase: Day" : "Phase: Night";
                spriteBatch.DrawString(_font, phaseText,new Vector2(20, 80),TEXT_COLOR);
            }

        if(GamelabGame.GamePhaseManager.IsDay)
        {
            // ✅ UI (now also scaled)
            DrawButton(_toCardScreenBtn, "Cards");
            UpdateUIButtonLayout();
            DrawButton(_inventoryBtn, _showInventory ? "Hide Cards" : "Show Cards");
            DrawButton(_towerRangeBtn, _showTowerRange ? "Hide Range" : "Tower Range");

            if (_showInventory)
            {
                DrawCardSlots();
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

    private void DrawCardSlots()
    {
        if (cardManager == null) return;

        var slots = cardManager.MapSlots.Concat(cardManager.BuildSlots).ToList();

        int cardW = 160;
        int cardH = 220;
        int spacing = 5; 

        int screenW = (int)viewportAdapter.VirtualWidth;
        int screenH = (int)viewportAdapter.VirtualHeight;

        int totalWidth = slots.Count * cardW + (slots.Count - 1) * spacing;

        int startX = screenW - totalWidth - 10; 
        //int startY = screenH - cardH - 40;    
        int startY = _cardStartY;

        // ✅ Thin border (almost hugging cards)
        Rectangle borderRect = new(
            startX - 4,
            startY - 4,
            totalWidth + 8,
            cardH + 8
        );

        spriteBatch.Draw(_dummy, borderRect, SLOT_BORDER_COLOR);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            Rectangle rect = new(
                startX + i * (cardW + spacing),
                startY,
                cardW,
                cardH
            );

            Color bgColor = (slot.Card != null && slot.Card == _activeMapCard)
                ? BTN_TEXT_COLOR
                : SLOT_COLOR;

            spriteBatch.Draw(_dummy, rect, bgColor);

            if (slot.Card != null)
            {
                spriteBatch.Draw(_cardTexture, rect,
                    _cardRegions[slot.Card.SpriteName], Color.White);
            }
        }
    }

    private bool HandleCardUseClicks(Vector2 mousePos)
    {
        var slots = cardManager.MapSlots.Concat(cardManager.BuildSlots).ToList();

        int cardW = 160; 
        int cardH = 220;
        int spacing = 5;

        int screenW = (int)viewportAdapter.VirtualWidth;
        int screenH = (int)viewportAdapter.VirtualHeight;

        int totalWidth = slots.Count * cardW + (slots.Count - 1) * spacing;

        int startX = screenW - totalWidth - 10;
        int startY = screenH - cardH - 40;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            Rectangle rect = new(
                startX + i * (cardW + spacing),
                startY,
                cardW,
                cardH
            );

            if (slot.Card != null && rect.Contains(mousePos))
            {
                var card = slot.Card;

                if (card.Type == CardType.Build || card.Type == CardType.MapAction)
                {
                    if (_activeMapCard == card)
                    {
                        if (_previewStructure != null && _selectedTile != (-1,-1))
                        {
                            mapManager.PlaceEntity(_previewStructure, _selectedTile);
                            _previewStructure = null;
                            _selectedTile = (-1,-1);
                        }
                        _activeMapCard = null;
                    }
                    else
                    {
                        _activeMapCard = card;
                        _selectedTile = (-1, -1);
                        _previewStructure = null;
                    }
                }
                else
                {
                    cardManager.UseCard(card);
                }

                return true;
            }
        }
        return false;
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

    private bool HandleMapCardUse(Vector2 pos)
    {
        if(_activeMapCard == null) return false;
        (int col, int row) = mapManager.GetTileFromScreenPosition(pos);
        if (col < 0) return false;

        if (_activeMapCard.Type == CardType.Build)
        {
            bool success = mapManager.PlaceEntity(_activeMapCard, (col, row));

            if (success)
            {
                cardManager.UseCard(_activeMapCard);
                AudioManager.Play("slam_02");
                _activeMapCard = null;
                _previewStructure = null; 
            }

            return success;
        }

        if (_activeMapCard.Type == CardType.MapAction)
        {
            bool success = ApplyMapAction(_activeMapCard, pos);

            if (success && _selectedTile == (-1,-1))
            {
                cardManager.UseCard(_activeMapCard); // ✅ NOW works for ALL actions
                _activeMapCard = null;
                _previewStructure = null;
            }
            return success;
        }

        return false;
    }


    private bool ApplyMapAction(Card card, Vector2 mousePos)
    {
        (int col, int row) = mapManager.GetTileFromScreenPosition(mousePos);
        if (col < 0) return false;

        Entity target = mapManager.GetStructure((col, row));

        switch (card.Effect)
        {
            case CardEffect.Destroy:
                if (mapManager.GetStructure((col, row)) is Structure toDestroy && toDestroy.CanMove)
                {
                    mapManager.RemoveStructure((col, row));
                    AudioManager.Play("tower_breaking");
                    return true; // action succeeded
                }
                return false;
            case CardEffect.Move:
                {
                    if (mapManager.GetStructure((col, row)) is Structure toMove && toMove.CanMove)
                    {
                        mapManager.RemoveStructure((col, row));
                        _selectedTile = (col, row);
                        _previewStructure = toMove;
                        return false; 
                    }

                    if (_selectedTile != (-1, -1) && mapManager.GetStructure((col, row)) == null)
                    {
                        var movingEntity = _previewStructure;
                        if (movingEntity != null)
                        {
                            movingEntity.TilePosition = (col, row);
                            mapManager.PlaceEntity(movingEntity, (col, row));

                            _selectedTile = (-1, -1);
                            _previewStructure = null;
                            return true;
                        }
                    }

                    return false;
                }
            case CardEffect.Polymorph:
                if (target is Meadow meadow && meadow.HasSheep && !meadow.IsPolymorphed)
                {
                    meadow.PolymorphSheep();
                    AudioManager.Play("qubodupRatAttack"); // probably change later
                    return true; // card is consumed
                }
                return false;
            default:
                return false;
        }
    }

    private void DrawPreview()
    {
        if (_activeMapCard == null || _hoverTile.col < 0) return;

        if (_activeMapCard.Type == CardType.Build)
        {
            if (_previewStructure == null || _previewStructure.TilePosition != _hoverTile)
            {
                _previewStructure = cardManager.GetStructureFromCard(_activeMapCard, _hoverTile);
            }

            if (_previewStructure != null)
                DrawTransparentStructure(_previewStructure);
                if (_previewStructure is Tower tower)
                {
                    DrawTowerRangePreview(tower);
                }
            return;
        }

        Structure target = mapManager.GetStructure(_hoverTile) as Structure;

        switch (_activeMapCard.Effect)
        {
            case CardEffect.Destroy:
                if (target != null)
                    DrawTransparentStructure(target);
                break;

            case CardEffect.Move:
                // Always draw the entity at hover tile first
                if (target != null)
                    DrawTransparentStructure(target);

                // Draw the moving entity preview if selected and tile is empty
                if (_selectedTile != (-1, -1))
                {
                    Structure movingEntity = _previewStructure ?? mapManager.GetStructure(_selectedTile) as Structure;
                    if (movingEntity != null && target == null)
                    {
                        // Temporarily move the entity to hover tile for drawing
                        var oldPos = movingEntity.TilePosition;
                        movingEntity.TilePosition = _hoverTile;
                        DrawTransparentStructure(movingEntity);
                        if (movingEntity is Tower tower)
                            DrawTowerRangePreview(tower);
                        movingEntity.TilePosition = oldPos;
                    }
                }
                break;

            case CardEffect.Polymorph:
                if (target != null)
                    DrawTransparentStructure(target);
                break;
        }
    }
    public void ShowDayPopup(string text)
    {
        _dayPopupText = text;
        _showDayPopup = true;
        _popupTimer = 3f; // seconds
    }
    private void DrawTransparentStructure(Structure structure)
    {
        if (structure == null) return;

        structure.DrawTransparent(spriteBatch, 0.5f); 
    }

    private void DrawTowerRangePreview(Tower tower)
    {
        if (tower == null) return;

        float alpha = tower is AreaTower ? 0.25f : 0.4f;
        Color rangeColor = (tower is AreaTower ? Color.Blue : Color.Red) * alpha;

        foreach (var tile in tower.GetTilesInRange())
        {
            if (tile.Item1 < 0 || tile.Item1 >= mapManager.Columns ||
                tile.Item2 < 0 || tile.Item2 >= mapManager.Rows)
                continue;

            var tileTexture = mapManager.GetTile(tile);
            Vector2 pos = mapManager.GetWorldPositionOfTile(tile);

            tileTexture.Draw(spriteBatch,pos,rangeColor,0f,Vector2.Zero,mapManager.Scale,SpriteEffects.None,0.8f);
        }
    }
    private void DrawAllTowerRanges()
    {
        for (int i = 0; i < mapManager.Columns; i++)
        {
            for (int j = 0; j < mapManager.Rows; j++)
            {
                var structure = mapManager.GetStructure((i, j));

                if (structure is Tower tower)
                {
                    DrawTowerRangePreview(tower);
                }
            }
        }
    }
    private void UpdateUIButtonLayout()
    {
        int screenW = (int)viewportAdapter.VirtualWidth;
        int screenH = (int)viewportAdapter.VirtualHeight;
        int margin = 20, spacing = 10;

        int currentY = screenH - margin;

        // -------- Cards (if visible) --------
        int cardH = 220; // card height
        _cardStartY = _showInventory ? currentY - cardH : currentY;
        currentY = _cardStartY - (_showInventory ? spacing : 0);

        // -------- Inventory Button --------
        string trText = "Tower Range";
        Vector2 trSize = _font.MeasureString(trText);
        int invW = (int)trSize.X + 20, invH = (int)trSize.Y + 10;

        currentY -= invH;
        _inventoryBtn = new Rectangle(screenW - invW - margin, currentY, invW, invH);
        currentY -= spacing;

        // -------- Tower Range Button --------
        int trW = (int)trSize.X + 20, trH = (int)trSize.Y + 10;

        currentY -= trH;
        _towerRangeBtn = new Rectangle(screenW - trW - margin, currentY, trW, trH);
    }
}