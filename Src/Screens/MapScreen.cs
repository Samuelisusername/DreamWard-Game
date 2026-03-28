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
    private readonly MapRenderer mapRenderer;
    private readonly EntityRenderer entityRenderer;


    // Cards
    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private Dictionary<string, Rectangle> _cardRegions;

    // Navigation button
    private Rectangle _toCardScreenBtn;

    // Drag
    private bool _isDrag;
    private bool _clickStarted;
    private Vector2 _oldPos;
    private Card _activeMapCard = null;
    private (int,int) _selectedTile = (-1,-1);

    private readonly Color SLOT_COLOR = new Color(0xbc, 0xb6, 0x9d);  
    private readonly Color BTN_COLOR = new Color(0xbc, 0xb6, 0x9d);  
    private readonly Color BTN_TEXT_COLOR = new Color(0x74, 0x70, 0x5f);    
    private readonly Color SLOT_BORDER_COLOR = new Color(0xd8, 0xd4, 0xc1); 
    private readonly Color TEXT_COLOR = new Color(0xd8, 0xd4, 0xc1);


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
        AudioManager.PlayMusic("day_music", Game.MasterVolume);

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
    }

    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        // Exit
        if (keyboard.IsKeyDown(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
            Game.Exit();

        // Spawn monster at edge heading toward tent
        if (keyboard.WasKeyPressed(Keys.Space))
        {
            int centerX = mapManager.Columns / 2;
            int centerY = mapManager.Rows / 2;
            AudioManager.Play("grunt_03");
            GamelabGame.GamePhaseManager.SwitchToNight();
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
                mapManager.Zoom(delta, viewportAdapter.PointToScreen(mouse.Position).ToVector2());
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
                mapManager.MoveMap(deltaPos);
                if (deltaPos.Length() > 0)
                {
                    _isDrag = true;
                    _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                }
            }

            if (mouse.WasButtonReleased(MouseButton.Left) && _clickStarted)
            {
                _clickStarted = false;
                _isDrag = false;
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

                    // Check USE buttons on cards
                    if (HandleCardUseClicks(pos))
                        return;

                    if (HandleMapCardUse(pos))
                    {
                        return;
                    }
                    mapManager.TryMoveSheep(pos);
                }
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
        entityRenderer.Draw(spriteBatch);
        
        // draw sanity
        if (sanityManager != null)
            {
                string text = $"Sanity: {sanityManager.Sanity}";
                spriteBatch.DrawString(_font, text,
                    new Vector2(20, 20),
                    TEXT_COLOR);
            }

        if(GamelabGame.GamePhaseManager.IsDay)
        {
            // ✅ UI (now also scaled)
            DrawButton(_toCardScreenBtn, "Cards");

            

            DrawCardSlots();
        }

        spriteBatch.End();
    }

    // ------------------------
    // Draw Helpers
    // ------------------------
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

        int cardW = 80;
        int cardH = 110;
        int spacing = 15;

        int screenW = (int)viewportAdapter.VirtualWidth;
        int screenH = (int)viewportAdapter.VirtualHeight;

        int totalWidth = slots.Count * cardW + (slots.Count - 1) * spacing;
        int startX = screenW - totalWidth - 20;
        int startY = screenH - cardH - 60;

        // Draw rectangle surrounding all slots + buttons
        Rectangle borderRect = new(
            startX - 10,
            startY - 10,
            totalWidth + 20,
            cardH + 25 + 20 // card height + button height + padding
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

            spriteBatch.Draw(_dummy, rect, SLOT_COLOR);

            if (slot.Card != null)
            {
                spriteBatch.Draw(_cardTexture, rect,
                    _cardRegions[slot.Card.SpriteName], Color.White);
            }

            Rectangle btn = new(rect.X, rect.Bottom + 5, rect.Width, 25);
            DrawButton(btn, "USE");
        }
    }

    private bool HandleCardUseClicks(Vector2 mousePos)
    {
        var slots = cardManager.MapSlots.Concat(cardManager.BuildSlots).ToList();

        int cardW = 80;
        int cardH = 110;
        int spacing = 15;

        int screenW = (int)viewportAdapter.VirtualWidth;
        int screenH = (int)viewportAdapter.VirtualHeight;

        int totalWidth = slots.Count * cardW + (slots.Count - 1) * spacing;

        int startX = screenW - totalWidth - 20;
        int startY = screenH - cardH - 60;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            Rectangle rect = new(
                startX + i * (cardW + spacing),
                startY,
                cardW,
                cardH
            );

            Rectangle btn = new(rect.X, rect.Bottom + 5, rect.Width, 25);

            if (slot.Card != null && btn.Contains(mousePos))
            {
                var card = slot.Card;

                if (card.Type == CardType.Build || card.Type == CardType.MapAction)
                {
                    _activeMapCard = card; // ✅ enter placement mode
                }
                else
                {
                    cardManager.UseCard(card); // stack cards still instant
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
        int cols = mapManager.Columns;
        int rows = mapManager.Rows;
        string[] treeAnims = { "treeBig_idle", "treeSmall_idle" };
        int placed = 0;
        int attempts = 0;

        while (placed < count && attempts < 200)
        {
            attempts++;
            int col = System.Random.Shared.Next(cols);
            int row = System.Random.Shared.Next(rows);

            // Don't place near the tent (keep a 3-tile radius clear)
            int dx = System.Math.Abs(col - centerX);
            int dy = System.Math.Abs(row - centerY);
            if (dx + dy <= 3) continue;

            // Don't place on edges (leave room for monster spawns)
            if (col == 0 || col == cols - 1 || row == 0 || row == rows - 1) continue;

            // Don't place on occupied tiles
            if (mapManager.GetStructure((col, row)) != null) continue;

            string anim = treeAnims[System.Random.Shared.Next(treeAnims.Length)];
            var tree = new TreeTile(anim, (col, row));
            mapManager.PlaceEntity(tree, (col, row));
            placed++;
        }
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
            }

            return success;
        }

        if (_activeMapCard.Type == CardType.MapAction)
        {
            bool success = ApplyMapAction(_activeMapCard, pos);

            if (success)
            {
                cardManager.UseCard(_activeMapCard); // ✅ NOW works for ALL actions
                _activeMapCard = null;
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
                // ✅ Keep as-is
                if (target != null && target.IsBuildEntity)
                {
                    mapManager.RemoveStructure((col,row));
                    AudioManager.Play("tower_breaking");
                    return true; // action succeeded
                }
                return false;
            case CardEffect.Move:
                {
                    if (_selectedTile == (-1,-1))
                    {
                        if(mapManager.GetStructure((col,row)) != null)
                        {
                            _selectedTile = (col,row);
                            return false; // do not consume card yet
                        }
                        return false;
                    }
                    else
                    {
                        Entity enitityGoal = mapManager.GetStructure((col,row));
                        if (enitityGoal == null)
                        {
                            Structure entityToMove = mapManager.GetStructure(_selectedTile);
                            entityToMove.TilePosition = (col,row);
                            mapManager.RemoveStructure(_selectedTile);
                            mapManager.PlaceEntity(entityToMove,(col,row));
                            _selectedTile = (-1,-1);
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

            case CardEffect.Polymorph:
                // ✅ Keep as-is
                if (target is Meadow meadow && meadow.HasSheep)
                {
                    PolymorphEntity(target);
                    AudioManager.Play("qubodupRatAttack"); //probably change this audio later
                    return true; // polymorph succeeded
                }
                return false;

            default:
                return false;
        }
    }

    private void PolymorphEntity(Entity target)
    {
        if (target is Meadow meadow && meadow.HasSheep)
        {
            var tile = target.TilePosition;

            // Replace in entities list
            mapManager.GetSheep().PolymorphSheep();
        }
    }
}