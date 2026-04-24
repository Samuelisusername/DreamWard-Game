using System;
using Microsoft.Xna.Framework;

public class SniperTower : Tower
{
    private Monster target = null;

    private TimeSpan attackCooldown = TimeSpan.FromSeconds(1);
    private TimeSpan elapsedAttackCooldown = TimeSpan.Zero;
    public SniperTower((int, int) pos) : base("towerSniper_idle", pos)
    {
    }

    // protected override float damagePerSecond => 8f;

    // protected override int rangeDepth => 2;

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];

    public override void Attack(GameTime gameTime)
    {
        //TODO: potentially move this responsability to the monsters.
        // the monsters tell the tower if they get in/out of range
        if(target != null && MapUtils.CalculateTileDistance(TilePosition, target.TilePosition) > rangeDepth)
        {
            //target is out of range
            target = null;
        }
        if(target == null)
        {
            //find new target
            elapsedAttackCooldown = TimeSpan.Zero;
            var tilesInRange = MapUtils.GetSurroundingCircle(TilePosition, 0, rangeDepth + 1);
            foreach (var item in tilesInRange)
            {
                MonsterGroupContainer container = mapManager.GetMonsterContainer(item);
                if(container != null && !container.IsEmpty)
                {
                    target = container.GetFirstMonster();
                    break;
                }
            }
        }
        if(target != null)
        {
            elapsedAttackCooldown += gameTime.ElapsedGameTime;
            if(elapsedAttackCooldown >= attackCooldown)
            {
                //tower should attack
                elapsedAttackCooldown -= attackCooldown;
                target.TakeDamage(damagePerSecond);
                if (target.IsDestroyed)
                {
                    target = null;
                }
                //TODO: play anim here
            } 
        }
    }
}