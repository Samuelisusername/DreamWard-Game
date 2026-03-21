using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using DreamWardUtils.Graphics;
using System;
using MonoGame.Extended.Input;


namespace Gamelab.Screens;

public class HexMapScreen : GamelabGameScreen
{
    private readonly MapManager mapManager;
    private readonly MapRenderer mapRenderer;
    private readonly EntityRenderer entityRenderer;
    private List<Entity> _entities;


    //Potentially something for an inputManager
    private bool _isDrag;
    Vector2 _oldPos;

    public HexMapScreen(GamelabGame game) : base(game)
    {
        Texture2D texture = Content.Load<Texture2D>("Sprites/day_tiles");
        TextureRegion region = new TextureRegion(texture,0,0,128,320);
        TileSet set = new TileSet(region,64,32);
        mapManager = new MapManager(set,30,30, TimeSpan.FromMilliseconds(500));
        mapManager.FillRandomly();
        mapManager.Scale = new Vector2(4,4);
        mapRenderer = new MapRenderer(mapManager);
        _entities = new List<Entity>();
        entityRenderer = new EntityRenderer(mapManager, _entities);
    }

    public override void LoadContent()
    {
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard, Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        if (keyboard.IsKeyDown(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
        {
            Game.Exit();
        }

        if (keyboard.WasKeyPressed(Keys.Space))
        {
            mapManager.SpawnRandomMonsters();
        }

        if (keyboard.WasKeyPressed(Keys.R))
        {
            mapManager.RandomizeGoal();
        }

        mapManager.Update(gameTime);

        if(mouse.DeltaScrollWheelValue != 0)
        {
            float delta = mouse.DeltaScrollWheelValue * (-0.01f);
            //Move this into the MapManager as a function
            mapManager.Zoom(delta, viewportAdapter.PointToScreen(mouse.Position).ToVector2());
        }

        if(mouse.WasButtonPressed(MouseButton.Left))
        {
            _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
        }

        if(mouse.IsButtonDown(MouseButton.Left))
        {
            Vector2 deltaPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2() - _oldPos;
            mapManager.MoveMap(deltaPos);
            if(deltaPos.Length() > 0)
            {                
                _isDrag = true;
                _oldPos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
            }
        }        

        if(mouse.WasButtonReleased(MouseButton.Left))
        {
            if(!_isDrag)
            {
                Vector2 pos = viewportAdapter.PointToScreen(mouse.Position).ToVector2();
                mapManager.HandleClick(pos);
            }
            _isDrag = false;
        }
        
    }

    public override void Draw(GameTime gameTime)
    {
        // Scale to fit the virtual screen size to the actual window size
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewportAdapter.GetScaleMatrix());

        mapRenderer.Draw(spriteBatch);

        entityRenderer.Draw(spriteBatch);

        spriteBatch.End();

        base.Draw(gameTime);
    }

}
