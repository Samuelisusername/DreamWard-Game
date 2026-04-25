using System;
using System.Diagnostics;
using System.Runtime;
using Gamelab.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class SniperTower : Tower
{
    private Monster target = null;
    private MonsterGroup targetGroup = null;
    private Vector2 targetPos;

    private TimeSpan attackCooldown = TimeSpan.FromSeconds(1);
    private TimeSpan elapsedAttackCooldown = TimeSpan.Zero;
    public SniperTower((int, int) pos) : base("towerSniper_idle", pos)
    {
    }

    // protected override float damagePerSecond => 8f;

    // protected override int rangeDepth => 2;

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}"), ("DPS:", $"{damagePerSecond}"), ("Total Damage Dealt:", $"{(int)totalDamage}")];

    private TimeSpan attackCD = TimeSpan.Zero;

    private static readonly TimeSpan projectileTravelTime = TimeSpan.FromMilliseconds(125);
    private TimeSpan pendingHitTimer = TimeSpan.Zero;
    private Monster pendingHitTarget = null;
    private float pendingHitDamage;

    public override void Attack(GameTime gameTime)
    {
        //TODO: potentially move this responsability to the monsters.
        // the monsters tell the tower if they get in/out of range
        if(target != null && (MapUtils.CalculateTileDistance(TilePosition, target.TilePosition) > rangeDepth ||  target.IsDestroyed))
        {
            //target is out of range
            target = null;
        }
        if(target == null)
        {
            //find new target
            bool reset = true;
            var tilesInRange = MapUtils.GetSurroundingCircle(TilePosition, 0, rangeDepth + 1);
            foreach (var item in tilesInRange)
            {
                MonsterGroupContainer container = mapManager.GetMonsterContainer(item);
                if(container != null && !container.IsEmpty)
                {
                    target = container.GetFirstMonster();
                    if(target != null)
                    {
                        targetGroup = container.GetFirstMonsterGroup();
                        reset = false;
                        break;
                    }
                }
            }            
            elapsedAttackCooldown = reset ? TimeSpan.Zero : elapsedAttackCooldown;
        }
        if(target != null)
        {
            elapsedAttackCooldown += gameTime.ElapsedGameTime;
            if(elapsedAttackCooldown >= attackCooldown)
            {
                //tower should attack
                elapsedAttackCooldown -= attackCooldown;
                pendingHitTarget = target;
                pendingHitDamage = damagePerSecond;
                pendingHitTimer = projectileTravelTime;
                totalDamage += damagePerSecond;
                var hitTile = target.TilePosition;
                DamageByTile.TryGetValue(hitTile, out float prevDmg);
                DamageByTile[hitTile] = prevDmg + damagePerSecond;
                targetPos = targetGroup.GetInterpolatedWorldPosition();
                //TODO: play anim here
                attackCD = projectileTravelTime;
                Vector2 startPos = mapManager.GetWorldPositionOfTile(TilePosition) + (_localOffset * _spriteTop.Scale) + (new Vector2(64, 55) * _spriteTop.Scale);
                Vector2 endPos =  targetPos +  (new Vector2(45, 5) * mapManager.Scale);
                Vector2 dir = endPos - startPos;
                for (int i = 0; i < 50; i++)
                {
                    ParticleManager.AddParticle(new Particle
                    {
                        WorldPosition = ((startPos + (float)Random.Shared.NextDouble() * dir) - mapManager.Offset)/mapManager.Scale,
                        Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 200, (float)(Random.Shared.NextDouble() -0.5f) * 200),
                        LifeSpan = TimeSpan.FromSeconds(0.2),
                        Color = Color.LightSkyBlue
                    });
                }

                AudioManager.Play("shoot_01");
            } 
        }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if(attackCD > TimeSpan.Zero)
        {
            attackCD -= gameTime.ElapsedGameTime;
        }
        if (pendingHitTarget != null)
        {
            pendingHitTimer -= gameTime.ElapsedGameTime;
            if (pendingHitTimer <= TimeSpan.Zero)
            {
                if (!pendingHitTarget.IsDestroyed)
                {
                    pendingHitTarget.TakeDamage(pendingHitDamage);
                    if (pendingHitTarget.IsDestroyed && target == pendingHitTarget)
                    {
                        target = null;
                    }
                }
                pendingHitTarget = null;
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Vector2 offset = default, float alpha = 1)
    {
        base.Draw(spriteBatch, offset, alpha);
        
        if (_spriteTop == null || attackCD <= TimeSpan.Zero || gamePhaseManager.IsDay) return;

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        spriteBatch.DrawLine(worldPos + (_localOffset * _spriteTop.Scale + offset) + (new Vector2(64, 55) * _spriteTop.Scale), targetPos +  (new Vector2(45, 5) * mapManager.Scale), Color.LightSkyBlue, 3);
    }
}