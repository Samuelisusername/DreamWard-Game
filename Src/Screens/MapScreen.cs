using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using DreamWardUtils.Graphics;
using MonoGame.Extended.Input;

namespace Gamelab.Screens;

public class MapScreen : GamelabGameScreen
{
    private readonly MapManager mapManager;
    private readonly MapRenderer mapRenderer;
    private readonly EntityRenderer entityRenderer;
    private List<Entity> _entities;
    private SanityManager _sanityManager;
    private CardManager _cardManager;

    public void SetManager(SanityManager sanityManager, CardManager manager)
    {
        _sanityManager = sanityManager;
        _cardManager = manager;
    }

    // Cards
    private SpriteFont _font;
    private Texture2D _dummy, _cardTexture;
    private Dictionary<string, Rectangle> _cardRegions;

    // Navigation button
    private Rectangle _toCardScreenBtn;

    // Drag
    private bool _isDrag;
    private Vector2 _oldPos;
    private Card _activeMapCard = null;
    private Entity _selectedEntity = null;

    public MapScreen(GamelabGame game) : base(game)
    {
        // Map setup
        Texture2D texture = Content.Load<Texture2D>("Sprites/day_tiles");
        TextureRegion region = new(texture, 0, 0, 128, 64);
        TileSet set = new(region, 64, 32);
        mapManager = new MapManager(set, 30, 30, TimeSpan.FromMilliseconds(500));
        mapManager.FillRandomly();
        mapManager.Scale = new Vector2(4, 4);
        mapRenderer = new MapRenderer(mapManager);

        _entities = new List<Entity>();
        entityRenderer = new EntityRenderer(mapManager, _entities);
        SpawnDefaultEntities();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        // Fonts and dummy texture
        _font = Content.Load<SpriteFont>("font");
        _dummy = new Texture2D(GraphicsDevice, 1, 1);
        _dummy.SetData(new[] { Color.White });

        // Card textures
        _cardTexture = Content.Load<Texture2D>("Sprites/cards1");
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
            mapManager.SpawnMonsterAtEdge(centerX, centerY);
        }

        // Map update
        mapManager.Update(gameTime);
        //Entity Update
        entityRenderer.Update(gameTime);

        // Zoom
        if (mouse.DeltaScrollWheelValue != 0)
        {
            float delta = mouse.DeltaScrollWheelValue * (-0.01f);
            mapManager.Zoom(delta, viewportAdapter.PointToScreen(mouse.Position).ToVector2());
        }

        // Drag
        if (mouse.WasButtonPressed(MouseButton.Left))
            _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();

        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Vector2 deltaPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _oldPos;
            mapManager.MoveMap(deltaPos);
            if (deltaPos.Length() > 0)
            {
                _isDrag = true;
                _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            }
        }

        if (mouse.WasButtonReleased(MouseButton.Left))
        {
            Vector2 pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();

            // Navigation to CardScreen
            if (_toCardScreenBtn.Contains(pos))
            {
                Game.screenManager.ShowScreen(Game.CardScreenInstance);
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
            _isDrag = false;
        }
        
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp,
                        transformMatrix: viewportAdapter.GetScaleMatrix());

        // ✅ Map
        mapRenderer.Draw(spriteBatch);
        entityRenderer.Draw(spriteBatch);

        // ✅ UI (now also scaled)
        DrawButton(_toCardScreenBtn, "Cards");

        if (_sanityManager != null)
        {
            string text = $"Sanity: {_sanityManager.Sanity}";
            spriteBatch.DrawString(_font, text,
                new Vector2(20, 20),
                Color.White);
        }

        DrawCardSlots();

        spriteBatch.End();
    }

    // ------------------------
    // Draw Helpers
    // ------------------------
    private void DrawButton(Rectangle rect, string text)
    {
        spriteBatch.Draw(_dummy, rect, Color.DarkGray);
        var size = _font.MeasureString(text);
        spriteBatch.DrawString(_font, text, new Vector2(rect.Center.X - (size.X * 0.6f) / 2, rect.Center.Y - (size.Y * 0.6f) / 2), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawCardSlots()
    {
        if (_cardManager == null)
        {
            Console.WriteLine("CardManager is NULL");
            return;
        }

        var slots = _cardManager.MapSlots.Concat(_cardManager.BuildSlots).ToList();


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

            spriteBatch.Draw(_dummy, rect, Color.DarkGreen);

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
        var slots = _cardManager.MapSlots.Concat(_cardManager.BuildSlots).ToList();

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
                    _cardManager.UseCard(card); // stack cards still instant
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
        _entities.Add(new Tent(
            atlas.CreateAnimatedSprite("tent-animation"),
            mapManager,
            (centerX, centerY)
        ));

        // 🐑 Sheep next to tent
        _entities.Add(new Sheep(
            atlas.CreateAnimatedSprite("sheep-animation"),
            mapManager,
            (centerX + 1, centerY)
        ));
    }

    private void HandleMapCardUse(Vector2 pos)
    {
        (int col, int row) = mapManager.GetTileFromScreenPosition(pos);
        if (col < 0) return;

        if (_activeMapCard.Type == CardType.Build)
        {
            bool success = PlaceEntity(_activeMapCard, col, row);

            if (success)
            {
                _cardManager.UseCard(_activeMapCard);
                _activeMapCard = null;
            }

            return;
        }

        if (_activeMapCard.Type == CardType.MapAction)
        {
            bool success = ApplyMapAction(_activeMapCard, pos);

            if (success)
            {
                _cardManager.UseCard(_activeMapCard); // ✅ NOW works for ALL actions
                _activeMapCard = null;
            }
        }
    }

    private bool PlaceEntity(Card card, int col, int row)
    {
        bool occupied = _entities.Any(e => e.TilePosition == (col, row));
        if (occupied)
        {
            Console.WriteLine("Tile already occupied!");
            return false;
        }
        var atlas = GamelabGame.textureAtlas["night_top"];

        string animKey = card.SpriteName switch
        {
            "dreamcatcher" => "wall-animation", // map dreamcatcher to wall
            "sniper" => "sniper-animation",
            "aoe" => "aoe-animation",
            "fence" => "fence-animation",
            "meadow" => "meadow-animation",
            _ => null
        };

        if (animKey == null) return false; // no matching animation

        var sprite = atlas.CreateAnimatedSprite(animKey);

        Entity entity = card.Name switch
        {
            "sniper" => new SniperTower(sprite, mapManager, (col,row)),
            "aoe" => new AOETower(sprite, mapManager, (col,row)),
            "fence" => new Fence(sprite, mapManager, (col,row)),
            "meadow" => new Meadow(sprite, mapManager, (col,row)),
            "dreamcatcher" => new Wall(sprite, mapManager, (col,row)), // create Wall entity
            _ => null
        };

        if (entity != null){
            entity.SetAsBuildEntity();
            _entities.Add(entity);
            return true;
        }
        return false;
    }

    private bool ApplyMapAction(Card card, Vector2 mousePos)
    {
        (int col, int row) = mapManager.GetTileFromScreenPosition(mousePos);
        if (col < 0) return false;

        Entity target = _entities.FirstOrDefault(e => e.TilePosition == (col, row));

        switch (card.Effect)
        {
            case CardEffect.Destroy:
                // ✅ Keep as-is
                if (target != null && target.IsBuildEntity)
                {
                    _entities.Remove(target);
                    return true; // action succeeded
                }
                return false;
            case CardEffect.Move:
                {
                    if (_selectedEntity == null)
                    {
                        Entity targetEntity = _entities.FirstOrDefault(e => e.TilePosition == (col, row) && e.IsBuildEntity);
                        if (targetEntity != null){
                            _selectedEntity = targetEntity;
                        }
                        return false; // do not consume card yet
                    }
                    else{
                        bool tileOccupied = _entities.Any(e => e.TilePosition == (col, row) && e.IsBuildEntity);
                        if (!tileOccupied){
                            _selectedEntity.SetTilePosition((col, row));
                            _selectedEntity = null;
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
            var sprite = GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite("rat-animation");
            var rat = new Rat(sprite, mapManager, tile);

            // Replace in entities list
            int index = _entities.IndexOf(target);
            if (index >= 0)
            {
                _entities[index] = rat;
            }
        }
    }
}