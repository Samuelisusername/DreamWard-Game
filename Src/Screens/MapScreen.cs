using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using Gamelab.Managers;
using Microsoft.Xna.Framework.Audio;

namespace Gamelab.Screens;

public class MapScreen : GamelabGameScreen
{
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
    private bool _dragStarted;
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
        mapRenderer = new MapRenderer(GamelabGame.MapManager);

        entityRenderer = new EntityRenderer(GamelabGame.MapManager);
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
            int centerX = GamelabGame.MapManager.Columns / 2;
            int centerY = GamelabGame.MapManager.Rows / 2;
            AudioManager.Play("grunt_03");
            GamelabGame.GamePhaseManager.SwitchToNight();
        }

        if (keyboard.WasKeyPressed(Keys.P))
        {
            Game.StartScreenInstance ??= new StartScreen(Game);
            Game.SwitchToScreen(Game.StartScreenInstance);
            return;
        }

        // Map update
        GamelabGame.MapManager.Update(gameTime);

        if (GamelabGame.MapManager.TentDestroyed)
        {
            Game.SwitchToScreen(new GameOverScreen(Game));
            return;
        }

        // Zoom
        if (mouse.DeltaScrollWheelValue != 0)
        {
            float delta = mouse.DeltaScrollWheelValue * (-0.01f);
            GamelabGame.MapManager.Zoom(delta, viewportAdapter.PointToScreen(mouse.Position).ToVector2());
        }

        // Drag
        if (mouse.WasButtonPressed(MouseButton.Left))
        {
            _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            _dragStarted = true;
        }

        if (mouse.IsButtonDown(MouseButton.Left) && _dragStarted)
        {
            Vector2 deltaPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _oldPos;
            GamelabGame.MapManager.MoveMap(deltaPos);
            if (deltaPos.Length() > 0)
            {
                _isDrag = true;
                _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            }
        }

        if (mouse.WasButtonReleased(MouseButton.Left))
        {
            _dragStarted = false;
            if(GamelabGame.GamePhaseManager.IsDay)
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

                if (!_isDrag)
                {
                    if (_activeMapCard != null)
                    {
                        HandleMapCardUse(pos);
                    }
                }
            }
            _isDrag = false;
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
        if (GamelabGame.SanityManager != null)
            {
                string text = $"Sanity: {GamelabGame.SanityManager.Sanity}";
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
        if (GamelabGame.CardManager == null) return;

        var slots = GamelabGame.CardManager.MapSlots.Concat(GamelabGame.CardManager.BuildSlots).ToList();

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
        var slots = GamelabGame.CardManager.MapSlots.Concat(GamelabGame.CardManager.BuildSlots).ToList();

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
                    GamelabGame.CardManager.UseCard(card); // stack cards still instant
                }
                 return true;
            }
        }
        return false;
    }

    private void SpawnDefaultEntities()
    {
        var atlas = GamelabGame.textureAtlas["night_top"];

        int centerX = GamelabGame.MapManager.Columns / 2;
        int centerY = GamelabGame.MapManager.Rows / 2;

        // 🏕️ Tent at center
        GamelabGame.MapManager.PlaceEntity(new Tent("tent_idle",
            GamelabGame.MapManager,
            (centerX, centerY)
        ), centerX, centerY);

        // 🐑 Sheep next to tent
        GamelabGame.MapManager.PlaceEntity(new Sheep("sheepSmall_idle",
            GamelabGame.MapManager,
            (centerX + 1, centerY)
        ),centerX + 1, centerY);

        // 🌲 Random trees
        SpawnTrees(25, centerX, centerY);
    }

    private void SpawnTrees(int count, int centerX, int centerY)
    {
        int cols = GamelabGame.MapManager.Columns;
        int rows = GamelabGame.MapManager.Rows;
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
            if (GamelabGame.MapManager.GetStructure(col, row) != null) continue;

            string anim = treeAnims[System.Random.Shared.Next(treeAnims.Length)];
            var tree = new TreeTile(anim, GamelabGame.MapManager, (col, row));
            GamelabGame.MapManager.PlaceEntity(tree, col, row);
            placed++;
        }
    }

    private void HandleMapCardUse(Vector2 pos)
    {
        (int col, int row) = GamelabGame.MapManager.GetTileFromScreenPosition(pos);
        if (col < 0) return;

        if (_activeMapCard.Type == CardType.Build)
        {
            bool success = GamelabGame.MapManager.PlaceEntity(_activeMapCard, col, row);

            if (success)
            {
                GamelabGame.CardManager.UseCard(_activeMapCard);
                AudioManager.Play("slam_02");
                _activeMapCard = null;
            }

            return;
        }

        if (_activeMapCard.Type == CardType.MapAction)
        {
            bool success = ApplyMapAction(_activeMapCard, pos);

            if (success)
            {
                GamelabGame.CardManager.UseCard(_activeMapCard); // ✅ NOW works for ALL actions
                _activeMapCard = null;
            }
        }
    }


    private bool ApplyMapAction(Card card, Vector2 mousePos)
    {
        (int col, int row) = GamelabGame.MapManager.GetTileFromScreenPosition(mousePos);
        if (col < 0) return false;

        Entity target = GamelabGame.MapManager.GetStructure(col, row);

        switch (card.Effect)
        {
            case CardEffect.Destroy:
                // ✅ Keep as-is
                if (target != null && target.IsBuildEntity)
                {
                    GamelabGame.MapManager.RemoveStructure(col,row);
                    AudioManager.Play("tower_breaking");
                    return true; // action succeeded
                }
                return false;
            case CardEffect.Move:
                {
                    if (_selectedTile == (-1,-1))
                    {
                        if(GamelabGame.MapManager.GetStructure(col,row) != null)
                        {
                            _selectedTile = (col,row);
                            return false; // do not consume card yet
                        }
                        return false;
                    }
                    else{
                        Entity enitityGoal = GamelabGame.MapManager.GetStructure(col,row);
                        if (enitityGoal == null){
                            Entity entityToMove = GamelabGame.MapManager.GetStructure(_selectedTile.Item1, _selectedTile.Item2);
                            entityToMove.SetTilePosition((col,row));
                            GamelabGame.MapManager.RemoveStructure(_selectedTile.Item1, _selectedTile.Item2);
                            GamelabGame.MapManager.PlaceEntity(entityToMove,col,row);
                            _selectedTile = (-1,-1);
                            return true;
                        }
                        else{
                            return false;
                        }
                    }
                }

            case CardEffect.Polymorph:
                // ✅ Keep as-is
                if (target is Sheep)
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
        if (target is Sheep)
        {
            var tile = target.TilePosition;
            var rat = new Rat("rats_idle", GamelabGame.MapManager, tile);

            // Replace in entities list
            GamelabGame.MapManager.RemoveStructure(tile.Item1, tile.Item2);
            GamelabGame.MapManager.PlaceEntity(rat,tile.Item1, tile.Item2);

            GamelabGame.MapManager.RegisterPolymorph(target, rat);
        }
    }
}