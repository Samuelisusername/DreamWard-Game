using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gamelab.Managers;
using MonoGame.Extended.Input;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace Gamelab.Screens;

public enum GameOverType
{
    Win,
    RegularMonster,
    FlyingMonster,
    SmallMonster,
    BigMonster,
    SpawnFail,
    TentIsolated,

}

public class GameOverScreen : GamelabGameScreen
{
    private SpriteFont _font;
    private Texture2D _pixel;
    private TimeSpan _elapsed;
    public static bool DidWin { get; set; } = false;
    private static readonly TimeSpan ReturnDelay = TimeSpan.FromSeconds(4);

    private GameOverType _killedBy;
    private Structure bestStructure;
    private TimeSpan runTime;
    public GameOverScreen(GamelabGame game, GameOverType killedBy) : base(game)
    {
        _killedBy = killedBy; 
        bestStructure = GamelabGame.MapManager.GetBestStructure();
        runTime = GamelabGame.InGameTime;
    }

    public override void LoadContent()
    {
        base.LoadContent();
        _font = Content.Load<SpriteFont>("font");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        if (DidWin)
        {
            // AudioManager.LoadSound(Content, "win_voice", "Sounds/win");
            // AudioManager.Play("win_voice");
        }
        else
        {
            AudioManager.LoadSound(Content, "gameover_voice", "Sounds/random_sounds/universfield-game-over-deep-male-voice-clip-352695");
            AudioManager.Play("gameover_voice");
        }
    }
    protected override void Update(GameTime gameTime, KeyboardStateExtended keyboard,
        Dictionary<int, GamePadState> gamePads, MouseStateExtended mouse)
    {
        if(Game.IsActive)
        {
            if(DidWin)
            {
                if (keyboard.WasKeyReleased(Keys.Enter))
                {
                    Game.IsGameInProgress = false;
                    Game.ResetGame();
                    Game.MapScreenInstance = new MapScreen(Game);
                    Game.MapScreenInstance.Initialize();
                    Game.MapScreenInstance.LoadContent();
                    Game.StartScreenInstance ??= new StartScreen(Game);
                    Game.SwitchToScreen(Game.StartScreenInstance);
                }
                else if(keyboard.WasKeyReleased(Keys.Space))
                {
                    Game.SwitchToScreen(Game.MapScreenInstance);
                }
            }
            else
            {
                if (keyboard.WasKeyReleased(Keys.Enter))
                {
                    Game.IsGameInProgress = false;
                    Game.ResetGame();
                    Game.MapScreenInstance = new MapScreen(Game);
                    Game.MapScreenInstance.Initialize();
                    Game.MapScreenInstance.LoadContent();
                    Game.StartScreenInstance ??= new StartScreen(Game);
                    Game.SwitchToScreen(Game.StartScreenInstance);
                }
            }
        }
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GraphicsDevice.Clear(new Color(8, 48, 43));

        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix(), samplerState: SamplerState.PointClamp);
        
        string text;
        if(DidWin)
        {
            text = bestStructure is Tower t ? $"You Survived! \nbest strcutre was: {t.GetType()} \nit dealt {t.TotalDamage} \nyour Run took {(int)runTime.TotalMinutes}:{runTime.Seconds}.{runTime.Milliseconds} \n\nPress Space to enter infinite mode \nEress Enter to get back to menu" :
            $"You Survived! \nyour Run took {(int)runTime.TotalMinutes}:{runTime.Seconds}.{runTime.Milliseconds}\n\nPress Space to enter infinite mode \npress enter to get back to menu";
        }
        else
        {
            text = bestStructure is Tower t ? 
                $"Game Over \n because of {_killedBy.ToString()} \n best strcutre was: {t.GetType()} \n it dealt {t.TotalDamage}\nyour Run took {(int)runTime.TotalMinutes}:{runTime.Seconds}.{runTime.Milliseconds}\n\nPress Enter to get back to menu" :
                $"Game Over \n because of {_killedBy.ToString()}\nyour Run took {(int)runTime.TotalMinutes}:{runTime.Seconds}.{runTime.Milliseconds}\n\nPress Enter to get back to menu";
        }
        Color color = DidWin ? new Color(120, 220, 160) : new Color(188, 182, 157);
        Vector2 textSize = _font.MeasureString(text);
        float scale = 3f;
        Vector2 pos = new Vector2(
            virtualScreenSize.X / 2f - textSize.X * scale / 2f,
            virtualScreenSize.Y / 2f - textSize.Y * scale / 2f
        );
        spriteBatch.DrawString(_font, text, pos, color,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        spriteBatch.End();
    }
}
