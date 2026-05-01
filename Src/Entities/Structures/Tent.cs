using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class Tent : CardEntity
{
    private SanityManager sanityManager => GamelabGame.SanityManager;

    public override bool CanMove => false;

    private TimeSpan zTimer = TimeSpan.FromSeconds(0.5f);
    private int zCycle = 0;

    public Tent(string animName, (int,int) pos)
        : base(animName, pos)
    { 
    }
    
    public override float CurrentHealth { get => sanityManager?.Sanity ?? 0; protected set => base.CurrentHealth = value; }
    public override float MaxHealth
    { 
        get => (sanityManager?.Sanity ?? 0) > base.MaxHealth ? sanityManager?.Sanity ?? 0 : base.MaxHealth;
        protected set => base.MaxHealth = value; 
    }

    protected override (string, string)[] infos => [("Sanity:", $"{CurrentHealth}")];

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        zTimer -= gameTime.ElapsedGameTime;
        if(zTimer <= TimeSpan.Zero)
        {
            zCycle += 1;
            zCycle %= 3;
            if(zCycle == 0)
            {
                zTimer = TimeSpan.FromSeconds(1);
            }
            else
            {
                zTimer = TimeSpan.FromSeconds(0.3f);
            }
            Vector2 worldPos = (mapManager.GetWorldPositionOfTile(TilePosition) + (new Vector2(40, 5) * mapManager.Scale) - mapManager.Offset) / mapManager.Scale;
            
            if(!gamePhaseManager.IsDay)
            {
                ParticleManager.AddParticle(new Particle
                {
                    WorldPosition = worldPos ,
                    Velocity = new Vector2(10f, -15f),
                    Color = Color.LightGray,
                    LifeSpan = TimeSpan.FromSeconds(1.0),
                    Text = $"z",
                    Font = _font,
                    Scale = 0.3f,
                    Updater = (particle, gameTime) =>
                    {            
                        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                        particle.Color  = Color.Lerp(particle.Color, Color.FromNonPremultiplied(particle.Color.R,particle.Color.G,particle.Color.B,(byte)0) , 1.0f * dt);
                        particle.Scale += 0.5f * dt;
                        particle.Velocity += new Vector2(5,7.5f) * dt;
                    }
                });
            }
        }
    }

    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);
        mapManager.InitScreenShake(TimeSpan.FromSeconds(0.3f), 7);
    }
}