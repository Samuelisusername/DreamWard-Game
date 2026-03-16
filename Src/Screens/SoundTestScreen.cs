using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio; // Crucial for SFX
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using System.Collections.Generic;    // Needed for your team's GamePad dictionary

namespace Gamelab.Screens;

public class SoundTestScreen : GamelabGameScreen
{
    private SoundEffect _testSound;

    public SoundTestScreen(GamelabGame game) : base(game)
    {
    }

    public override void LoadContent()
    {
        base.LoadContent();
        // 2. Do not include the ".wav" extension here.
        _testSound = Game.Content.Load<SoundEffect>("Sounds/shuffle");
    }

    // NOTICE: We are using your team's custom Update method here!
        protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard, Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)    {
        // Check if Spacebar was just pressed this exact frame
        if (keyboard.WasKeyPressed(Keys.Space))
        {
            _testSound.Play(); 
        }
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime); // Keeps your team's frame counter working
        
        // Turn the screen green so you know you are in the Sound Test Sandbox
        Game.GraphicsDevice.Clear(Color.DarkGreen); 
    }
}