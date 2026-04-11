using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gamelab.Managers;

namespace Gamelab.Screens;

public class GameOverScreen : GamelabGameScreen
{
    private SpriteFont _font;
    private Texture2D _pixel;
    private TimeSpan _elapsed;
    private readonly bool _won;
    private static readonly TimeSpan ReturnDelay = TimeSpan.FromSeconds(4);
    public GameOverScreen(GamelabGame game, bool won) : base(game)
    {
        _won = won;
    }

    public override void LoadContent()
    {
        base.LoadContent();
        _font = Content.Load<SpriteFont>("font");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        if (_won)
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
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        _elapsed += gameTime.ElapsedGameTime;
        if (_elapsed >= ReturnDelay)
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

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GraphicsDevice.Clear(new Color(8, 48, 43));

        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix(), samplerState: SamplerState.PointClamp);

        string text = _won ? "You Survived!" : "Game Over";
        Color color = _won ? new Color(120, 220, 160) : new Color(188, 182, 157);
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
