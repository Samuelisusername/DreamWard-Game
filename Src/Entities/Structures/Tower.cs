using System.Collections.Generic;
using Gamelab;
using Gamelab.Managers;
using MonoGame.Extended.Animations;

/// <summary>
/// Tower with depth-1 hex range (own tile + immediate neighbors).
/// Deals damage to monsters on tiles in range.
/// </summary>
public abstract class Tower : Structure
{
    protected virtual float damagePerSecond => GamelabGame.Instance.Balance.TowerDamage[this.GetType()];
    protected virtual int rangeDepth => GamelabGame.Instance.Balance.TowerRanges[this.GetType()];
    private float soundTimer = 0.0f;

    public Tower(string animName, (int,int) pos)
        : base(animName, pos)
    {
    }

    public virtual void Attack(float dt)
    {
        var tilesInRange = MapUtils.GetSurroundingCircle(TilePosition, 0, rangeDepth + 1);
        bool dealtDamageThisFrame = false;
        foreach (var tile in tilesInRange)
        {
            // Target the new Container!
            var targetContainer = mapManager.GetMonsterContainer(tile);
            if (targetContainer != null && !targetContainer.IsEmpty)
            {
                // Pass that info into the container
                targetContainer.TakeDamage(damagePerSecond * dt, this is AreaTower, this is Spike); 
                dealtDamageThisFrame = true;
                if(this is SniperTower)
                {
                    break;
                }
            }
        }
        
        if (dealtDamageThisFrame)
        {
            soundTimer -= dt;

            if (soundTimer <= 0f)
            {
                string soundToPlay = this is AreaTower ? "AOE-tower_stomp" : "shoot_01"; 
                float cooldown = this is AreaTower ? 1.5f : 1.0f;

                AudioManager.Play(soundToPlay);
                soundTimer = cooldown; 
            }
        }
    }

    /// <summary>
    /// Returns all hex tiles within this tower's range (including its own tile).
    /// </summary>
    public List<(int,int)> GetTilesInRange()
    {
        return MapUtils.GetSurroundingCircle(TilePosition, 0, rangeDepth+1);
    }
}
