using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DreamWardUtils.Graphics;

/// <summary>
/// Represents a rectangular region within a texture.
/// </summary>
public class TextureRegion 
{
    public Texture2D Texture {get; private set;}
    public Rectangle SourceRectangle {get; private set;}

    public int Width => SourceRectangle.Width;

    public int Height => SourceRectangle.Height;

    public TextureRegion(){}

    public TextureRegion(Texture2D text, int x, int y, int width, int height)
    {
        Texture = text;
        SourceRectangle = new Rectangle(x,y,width,height);
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 pos, Color color)
    {
        Draw(spriteBatch, pos, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
    }

    public void DrawCentered(SpriteBatch spriteBatch, Vector2 pos, Color color)
    {
        Draw(spriteBatch, pos, color, 0f, SourceRectangle.Center.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
    }
    
    public void Draw(SpriteBatch spriteBatch, Vector2 pos, Color color, float rotation, Vector2 origin, float scale, SpriteEffects spriteEffect, float layerDepth)
    {
        spriteBatch.Draw(Texture,
        pos,
        SourceRectangle,
        color,
        rotation,
        origin,
        new Vector2(scale, scale),
        spriteEffect,
        layerDepth);
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 pos, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects spriteEffect, float layerDepth)
    {
        spriteBatch.Draw(Texture,
        pos,
        SourceRectangle,
        color,
        rotation,
        origin,
        scale,
        spriteEffect,
        layerDepth);
    }
}
