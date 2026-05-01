using System;
using System.Collections.Generic;
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
                part = nextPart;
                continue;
            }

            if (part.Value.Updater != null)
                part.Value.Updater(part.Value, gameTime);

            part.Value.WorldPosition += part.Value.Velocity * (float)gameTime.ElapsedGameTime.TotalSeconds;
            part = nextPart;
        }
    }

    public static void Draw(SpriteBatch spriteBatch, Rectangle screen)
    {
        // Pass 1: non-text (sparkles, dots) so text always sits on top
        var part = particles.First;
        while(part != null)
        {
            if (part.Value.Text == null)
                DrawParticle(spriteBatch, screen, part.Value);
            part = part.Next;
        }

        // Pass 2: text particles
        part = particles.First;
        while(part != null)
        {
            if (part.Value.Text != null)
                DrawParticle(spriteBatch, screen, part.Value);
            part = part.Next;
        }
    }

    private static void DrawParticle(SpriteBatch spriteBatch, Rectangle screen, Particle p)
    {
        Vector2 screenPos = (p.WorldPosition * GamelabGame.MapManager.Scale) + GamelabGame.MapManager.Offset;
        if (!screen.Contains(screenPos)) return;

        if (p.Text != null && p.Font != null)
        {
            Vector2 size = p.Font.MeasureString(p.Text);
            Vector2 origin = size / 2f;
            float scale = p.Scale * GamelabGame.MapManager.Scale.X;
            spriteBatch.DrawString(p.Font, p.Text,
                screenPos + new Vector2(2, 2), new Color((byte)0,(byte)0,(byte)0,p.Color.A),
                0f, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(p.Font, p.Text,
                screenPos, p.Color,
                0f, origin, scale, SpriteEffects.None, 0f);
        }
        else
        {
            spriteBatch.Draw(GamelabGame.DummyText,
                screenPos,
                new Rectangle(0, 0, 1, 1),
                p.Color,
                0,
                Vector2.One,
                GamelabGame.MapManager.Scale * 2 * p.Scale,
                SpriteEffects.None,
                0);
        }
    }

    public static void AddParticle(Particle particle)
    {
        particles.AddLast(particle);
    }

    public static void SpawnSanityGain(int amount, Vector2 worldPos, SpriteFont font)
    {
        Color blue = new Color(37, 150, 190);
        Color lightBlue = new Color(135, 206, 235);

        AddParticle(new Particle
        {
            WorldPosition = worldPos + Vector2.One,
            Velocity = new Vector2(0, -40f),
            Color = Color.Black,
            LifeSpan = TimeSpan.FromSeconds(2.0),
            Text = $"+{amount}",
            Font = font,
            Scale = 0.5f,
        });
        AddParticle(new Particle
        {
            WorldPosition = worldPos,
            Velocity = new Vector2(0, -40f),
            Color = lightBlue,
            LifeSpan = TimeSpan.FromSeconds(2.0),
            Text = $"+{amount}",
            Font = font,
            Scale = 0.5f,
        });

        Color[] glitterPalette = {
            blue,
            new Color(180, 230, 255),
            Color.White,
            new Color(120, 220, 255),
        };
        int glitterCount = Math.Clamp(amount * 6, 60, 200);
        for (int i = 0; i < glitterCount; i++)
        {
            Color baseColor = glitterPalette[Random.Shared.Next(glitterPalette.Length)];
            float life = 1.6f + (float)Random.Shared.NextDouble() * 1.6f;
            float lifeF = life;
            float twinkleFreq = 8f + (float)Random.Shared.NextDouble() * 8f;
            float twinklePhase = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            Vector2 spawn = worldPos + new Vector2(
                (float)(Random.Shared.NextDouble() - 0.5) * 70f,
                (float)(Random.Shared.NextDouble() - 0.5) * 30f);
            Vector2 vel = new Vector2(
                (float)(Random.Shared.NextDouble() - 0.5) * 25f,
                -18f - (float)Random.Shared.NextDouble() * 25f);

            AddParticle(new Particle
            {
                WorldPosition = spawn,
                Velocity = vel,
                Color = baseColor,
                LifeSpan = TimeSpan.FromSeconds(life),
                Scale = 0.6f + (float)Random.Shared.NextDouble() * 0.8f,
                Updater = (p, gt) =>
                {
                    float t = (float)p.LivedFor.TotalSeconds;
                    float lifePct = 1f - (t / lifeF);
                    float twinkle = 0.4f + 0.6f * MathF.Abs(MathF.Sin(t * twinkleFreq + twinklePhase));
                    float a = MathF.Max(0f, lifePct) * twinkle;
                    p.Color = baseColor * a;
                }
            });
        }
    }
}
