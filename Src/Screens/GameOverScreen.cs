using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gamelab.Screens;

public class GameOverScreen : GamelabGameScreen
{
    private SpriteFont _font;
    private Texture2D _pixel;
    private TimeSpan _elapsed;
    private static readonly TimeSpan ReturnDelay = TimeSpan.FromSeconds(4);

    public GameOverScreen(GamelabGame game) : base(game) { }

    public override void LoadContent()
    {
        base.LoadContent();
        _font = Content.Load<SpriteFont>("font");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        _elapsed += gameTime.ElapsedGameTime;
        if (_elapsed >= ReturnDelay)
        {
            Game.SwitchToScreen(new StartScreen(Game));
        }
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GraphicsDevice.Clear(new Color(8, 48, 43));

        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix());

        string text = "Game Over";
        Vector2 textSize = _font.MeasureString(text);
        float scale = 3f;
        Vector2 pos = new Vector2(
            virtualScreenSize.X / 2f - textSize.X * scale / 2f,
            virtualScreenSize.Y / 2f - textSize.Y * scale / 2f
        );
        spriteBatch.DrawString(_font, text, pos, new Color(188, 182, 157),
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        spriteBatch.End();
    }
}
