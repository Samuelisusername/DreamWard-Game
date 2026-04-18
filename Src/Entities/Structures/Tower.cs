using System.Collections.Generic;
using Gamelab.Managers;
using MonoGame.Extended.Animations;

/// <summary>
/// Tower with depth-1 hex range (own tile + immediate neighbors).
/// Deals damage to monsters on tiles in range.
/// </summary>
public abstract class Tower : Structure
{
    protected abstract float damagePerSecond {get;}
    protected abstract int rangeDepth {get;}
    private float soundTimer = 0.0f;

    public Tower(string animName, (int,int) pos, int health)
        : base(animName, pos, health)
    {
    }

    public virtual void Attack(float dt)
    {
        var tilesInRange = MapUtils.GetSurroundingCircle(TilePosition, 0, rangeDepth);
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
