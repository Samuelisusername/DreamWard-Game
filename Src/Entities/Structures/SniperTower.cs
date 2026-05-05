using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using Gamelab;
using Gamelab.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class SniperTower : Tower
{
    private CombatManager combatManager => GamelabGame.CombatManager;
    private Monster _target = null;
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
        if(_target != null && (!mapManager.GetMonsterGroupsInRange(TilePosition, rangeDepth).SelectMany(i => i.Monsters).Contains(_target) ||  _target.IsDestroyed))
        {
            //target is out of range
            _target = null;
        }
        if(_target == null || (combatManager.PriorityGroup != null && !combatManager.PriorityGroup.Monsters.Contains(_target)))
        {
            //find new target
            bool reset = true;
            var groupsInRange = mapManager.GetMonsterGroupsInRange(TilePosition, rangeDepth);
            foreach (var group in groupsInRange)
            {
                if(group == combatManager.PriorityGroup)
                {
                    _target = group.Monsters.MinBy(i => i.CurrentHealth);
                    reset = false;
                    break;
                }
                if(_target != null && !group.IsEmpty)
                {
                    _target = group.Monsters.MinBy(i => i.CurrentHealth);
                    reset = false;
                }
            }    
            elapsedAttackCooldown = reset ?  TimeSpan.Zero : elapsedAttackCooldown;  
        }
        if(_target != null)
        {   
            targetGroup = mapManager.GetMonsterContainer(_target.TilePosition).GetMonsterGroup(_target);
            elapsedAttackCooldown += gameTime.ElapsedGameTime;
            if(elapsedAttackCooldown >= attackCooldown)
            {
                //tower should attack
                elapsedAttackCooldown -= attackCooldown;
                pendingHitTarget = _target;
                pendingHitDamage = damagePerSecond;
                pendingHitTimer = projectileTravelTime;
                totalDamage += damagePerSecond;
                targetPos = targetGroup.GetInterpolatedWorldPosition();
                switch (_target.Type)
                {
                    case MonsterType.Regular:
                        targetPos = (targetPos +  (new Vector2(45, 11) * mapManager.Scale )- mapManager.Offset)/mapManager.Scale;
                        break;
                    case MonsterType.Flying:
                        targetPos = (targetPos +  (new Vector2(21, 8) * mapManager.Scale )- mapManager.Offset)/mapManager.Scale;
                        break;
                    case MonsterType.Small:
                        targetPos = (targetPos +  (new Vector2(33, 28) * mapManager.Scale )- mapManager.Offset)/mapManager.Scale;
                        break;
                    case MonsterType.Big:
                        targetPos = (targetPos +  (new Vector2(33, 5) * mapManager.Scale )- mapManager.Offset)/mapManager.Scale;
                        break;
                }
                //TODO: play anim here
                attackCD = projectileTravelTime;
                Vector2 startPos = mapManager.GetWorldPositionOfTile(TilePosition) + (_localOffset * _spriteTop.Scale) + (new Vector2(64, 55) * _spriteTop.Scale);
                Vector2 endPos =  (targetPos * mapManager.Scale) + mapManager.Offset;
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
                    if (pendingHitTarget.IsDestroyed && _target == pendingHitTarget)
                    {
                        _target = null;
                    }
                }
                pendingHitTarget = null;
            }
        }
    }

    public void SetTarget(Monster target)
    {
        if(mapManager.GetMonsterGroupsInRange(TilePosition, rangeDepth).Any(i => i.Monsters.Contains(target)))
        {
            _target = target;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Vector2 offset = default, float alpha = 1)
    {
        base.Draw(spriteBatch, offset, alpha);
        
        if (_spriteTop == null || attackCD <= TimeSpan.Zero || gamePhaseManager.IsDay) return;

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        spriteBatch.DrawLine(worldPos + (_localOffset * _spriteTop.Scale + offset) + (new Vector2(64, 55) * _spriteTop.Scale), (targetPos * mapManager.Scale) + mapManager.Offset, Color.LightSkyBlue, 3);
    }
}