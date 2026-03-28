using System.Collections.Generic;    // Needed for your team's GamePad dictionary
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio; // Crucial for SFX
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using DreamWardUtils.Graphics;
using Gamelab.Managers;


namespace Gamelab.Screens;

public class SoundTestScreen : GamelabGameScreen
{
    private MapManager mapManager => GamelabGame.MapManager;
    private SoundEffect _testSound;
    private SoundEffect _day_BackgroundSound;
    MapRenderer mapRenderer;

    EntityRenderer entityRenderer;
    private List<Entity> _entities = new();

    Vector2 _oldPos;

    //Potentially something for an inputManager
    private bool _isDrag;

    public SoundTestScreen(GamelabGame game) : base(game)
    {
    }

    public override void LoadContent()
    {
        base.LoadContent();
        // 2. Do not include the ".wav" extension here.
        AudioManager.LoadSound(Game.Content, "sheep_baa", "Sounds/sheep_baa");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/action_cards");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/buildings");
        AudioManager.LoadAllSounds(Game.Content, "Sounds/demons");
        
        _day_BackgroundSound = Game.Content.Load<SoundEffect>("Sounds/preferred_option_day");
        SoundEffectInstance dayMusic = _day_BackgroundSound.CreateInstance();
        dayMusic.IsLooped = true; // Tell it to repeat forever
        dayMusic.Play();          // Press play EXACTLY ONCE
        Texture2D texture = Content.Load<Texture2D>("Sprites/day_tiles");
        TextureRegion region = new TextureRegion(texture,0,0,128,320);
        TileSet set = new TileSet(region,64,32);
        mapRenderer = new MapRenderer();     
        entityRenderer = new EntityRenderer();
    }

    // NOTICE: We are using your team's custom Update method here!
        protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard, Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)    {
        //continuously play the day soundtrack

        // Check if Spacebar was just pressed this exact frame
        if (keyboard.WasKeyPressed(Keys.Space))
        {
            AudioManager.Play("sheep_baa"); 
        }
        if (keyboard.IsKeyDown(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
        {
            Game.Exit();
        }
        mapManager.Update(gameTime);

        if(mouse.DeltaScrollWheelValue != 0)
        {
            float delta = mouse.DeltaScrollWheelValue * 0.01f;
            //Move this into the mapManager as a function
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
                //mapManager.HandleClick(pos);
            }
            _isDrag = false;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewportAdapter.GetScaleMatrix());

        mapRenderer.Draw(spriteBatch);

        entityRenderer.Draw(spriteBatch);

        spriteBatch.End();

        base.Draw(gameTime);
        
    }
}