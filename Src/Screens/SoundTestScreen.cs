using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio; // Crucial for SFX
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;    // Needed for your team's GamePad dictionary

namespace Gamelab.Screens;

public class SoundTestScreen : GamelabGameScreen
{
    private SoundEffect _testSound;
    private KeyboardState _previousKeyboardState;

    public SoundTestScreen(GamelabGame game) : base(game)
    {
    }

    public override void LoadContent()
    {
        base.LoadContent();
        
        // 1. Make sure "myTestSound" matches your actual file name exactly!
        // 2. Do not include the ".wav" extension here.
        _testSound = Game.Content.Load<SoundEffect>("Sounds/myTestSound");
    }

    // NOTICE: We are using your team's custom Update method here!
    protected override void Update(GameTime gameTime, KeyboardState keyboard, Dictionary<int, GamePadState> gamePads)
    {
        // Check if Spacebar was just pressed this exact frame
        if (keyboard.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space))
        {
            _testSound.Play(); 
        }

        _previousKeyboardState = keyboard;
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime); // Keeps your team's frame counter working
        
        // Turn the screen green so you know you are in the Sound Test Sandbox
        Game.GraphicsDevice.Clear(Color.DarkGreen); 
    }
}