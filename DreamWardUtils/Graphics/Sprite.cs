using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DreamWardUtils.Graphics;

public class Sprite 
{
    public TextureRegion Region {get; set;}

    public Color Color {get; set;} = Color.White;

    public float Rotation {get; set;} = 0.0f;

    public Vector2 Origin {get; set;} = Vector2.Zero;

    public Vector2 Scale {get; set;} = Vector2.One;

    public SpriteEffects SpriteEffects {get; set;} = SpriteEffects.None;

    public float layerDepth {get; set;} = 0.0f;

    public float Width => Region.Width * Scale.X;
    public float Height => Region.Height * Scale.Y;

    public Sprite() { }

    public Sprite(TextureRegion region)
    {
        Region = region;
    }
    
    public void CenterOrigin()
    {
        Origin = new Vector2(Region.Width, Region.Height) * 0.5f;
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 pos)
    {
        Region.Draw(spriteBatch, pos, Color, Rotation, Origin, Scale, SpriteEffects, layerDepth);
    }
}
