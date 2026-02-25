using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;


namespace Gamelab.Screens;

public class MainMenuScreen(GamelabGame game) : GamelabGameScreen(game)
{
    protected Texture2D bgTexture;

    class ExampleData
    {
        public int size;
        public Vector2 position;
    }

    private ExampleData exampleData;
    public override void LoadContent()
    {
        base.LoadContent();

        bgTexture = new Texture2D(GraphicsDevice, 1, 1);
        bgTexture.SetData([Color.White]);

        LoadExampleData();
    }

    protected override void Update(GameTime gameTime, KeyboardState keyboard, Dictionary<int, GamePadState> gamePads)
    {
        if (keyboard.IsKeyDown(Keys.Escape) || gamePads.Values.Any(gp => gp.Buttons.Back == ButtonState.Pressed))
        {
            Game.Exit();
        }

        if (Game.IsDebug && keyboard.IsKeyDown(Keys.F5))
        {
            LoadExampleData();
        }

        if (Game.IsDebug && keyboard.IsKeyDown(Keys.F12))
        {
            SaveExampleData();
        }
    }

    public override void Draw(GameTime gameTime)
    {
        // Scale to fit the virtual screen size to the actual window size
        spriteBatch.Begin(transformMatrix: viewportAdapter.GetScaleMatrix());

        // Rendering is done in the virtual screen space
        spriteBatch.Draw(bgTexture, new Rectangle(0, 0, virtualScreenSize.X, virtualScreenSize.Y), Color.White);

        SpriteFontBase font = Game.fontSystem.GetFont(exampleData.size);
        spriteBatch.DrawString(font, "Press \u242F to Exit", exampleData.position, Color.Black);

        spriteBatch.End();

        base.Draw(gameTime);
    }

    protected void LoadExampleData()
    {
        exampleData = Game.jsonLoader.LoadJson<ExampleData>("example.json");
    }

    protected void SaveExampleData()
    {
        var newExampleData = new ExampleData {
            size = 42,
            position = new Vector2(100, 100),
        };

        Game.jsonLoader.SaveJson("example2.json", newExampleData);
    }
}
