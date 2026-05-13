using System;
using System.Linq;
using Gamelab;
using Gamelab.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class Bomb : Tower
{
    public bool ShouldBlowUp {get; set;}

    private bool resetBlowProgress = true;
    public Bomb((int, int) pos) : base("bomb_idle", pos)
    {
    }

    public override (string label, string info)[] Infos => [("Health:", $"{CurrentHealth}"), ("DPS:", $"{damagePerSecond}")];

    public override string BottomText => !gamePhaseManager.IsDay? "Click and hold blow up" : "Can't blow up during the day";

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (resetBlowProgress)
        { 
            BarProgress = TimeSpan.Zero;
        }
        resetBlowProgress = true;
    }

    public bool AddProgress(TimeSpan delta)
    {
        if (gamePhaseManager.IsDay) return false;
        resetBlowProgress = false;
        BarProgress += delta;
        if (BarProgress > BarComplete)
        {
            return true;
        }
        return false;
    }

    public override void Attack(GameTime gameTime)
    {
        if(ShouldBlowUp)
        {
            var groupsInRage = mapManager.GetMonsterGroupsInRange(TilePosition, rangeDepth);

            foreach (MonsterGroup group in groupsInRage)
            {
                group.TakeDamageAOE(damagePerSecond);
                string soundToPlay = "AOE-tower_stomp";

                AudioManager.Play(soundToPlay);
            }
            CurrentHealth = 0;
            Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
            for(int i = 0; i < 40; i++)
            {
                ParticleManager.AddParticle(new Particle
                {
                    WorldPosition = (worldPos + (_localOffset * _spriteTop.Scale) + (new Vector2(64, 100) * _spriteTop.Scale) - mapManager.Offset)/mapManager.Scale,
                    Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 150, (float)(Random.Shared.NextDouble() -0.5f) * 150),
                    LifeSpan = TimeSpan.FromSeconds(0.5),
                    Color = Color.DarkRed,
                    Updater = (particle, gameTime) =>
                    {            
                        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                        particle.Color  = Color.Lerp(particle.Color, Color.Black , 3f * dt);
                        particle.Scale += 0.5f * dt;
                    }
                });
            }
            mapManager.InitScreenShake(TimeSpan.FromSeconds(0.3f), 7);

        }
    }
}