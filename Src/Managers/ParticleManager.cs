using System.Collections.Generic;
using System.Reflection.Metadata;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public static class ParticleManager
{
    private static LinkedList<Particle> particles = new LinkedList<Particle>();

    public static void Update(GameTime gameTime)
    {
        var part = particles.First;
        while(part != null)
        {
            var nextPart = part.Next;

            part.Value.LivedFor += gameTime.ElapsedGameTime;
            if(part.Value.LivedFor >= part.Value.LifeSpan)
            {
                particles.Remove(part);
            }
            part.Value.WorldPosition += part.Value.Velocity * (float)gameTime.ElapsedGameTime.TotalSeconds;
            part = nextPart;
        }
    }

    public static void Draw(SpriteBatch spriteBatch, Rectangle screen)
    {
        var part = particles.First;
        while(part != null)
        {
            if(!screen.Contains((part.Value.WorldPosition * GamelabGame.MapManager.Scale) + GamelabGame.MapManager.Offset))
            {
                part = part.Next;
                continue;
            }
            // spriteBatch.Draw(GamelabGame.DummyText, part.Value.WorldPosition, part.Value.Color);
            spriteBatch.Draw(GamelabGame.DummyText,
            (part.Value.WorldPosition * GamelabGame.MapManager.Scale) + GamelabGame.MapManager.Offset,
            new Rectangle(0,0,1,1),
            part.Value.Color,
            0,
            Vector2.One,
            GamelabGame.MapManager.Scale * 2,
            SpriteEffects.None,
            0);
            part = part.Next;
        }
    }

    public static void AddParticle(Particle particle)
    {
        particles.AddLast(particle);
    }
}