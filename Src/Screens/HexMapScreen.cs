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

public class HexMapScreen(GamelabGame game) : GamelabGameScreen(game)
{
    public static Dictionary<string, TextureAtlas> textureAtlas;
    MapManager mapManager;
    MapRenderer mapRenderer;

    EntityRenderer entityRenderer;

    Vector2 _oldPos;

    //Potentially something for an inputManager
    private bool _isDrag;

    public override void LoadContent()
    {
        base.LoadContent();
        textureAtlas = new();
        TextureAtlas monsters = TextureAtlas.FromFile(Content, "Sprites/atlas-definition.xml");
        textureAtlas.Add("monsters", monsters);
        Texture2D texture = Content.Load<Texture2D>("Sprites/day_tiles_alternative");
        TextureRegion region = new TextureRegion(texture,0,0,128,64);
        TileSet set = new TileSet(region,64,32);
        mapManager = new MapManager(set,30,30, TimeSpan.FromMilliseconds(500));
        mapManager.FillRandomly();
        mapManager.Scale = new Vector2(4,4);
        mapRenderer = new MapRenderer(mapManager);
        entityRenderer = new EntityRenderer(mapManager);

    }

    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard, Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        if (keyboard.IsKeyDown(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
        {
            Game.Exit();
        }
        mapManager.Update(gameTime);

        if(mouse.DeltaScrollWheelValue != 0)
        {
            float delta = mouse.DeltaScrollWheelValue * 0.01f;
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
