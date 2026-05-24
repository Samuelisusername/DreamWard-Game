using System.Collections.Generic;
using Gamelab;
using Gamelab.Managers;
using Microsoft.Xna.Framework;

/// <summary>
/// Tower with depth-1 hex range (own tile + immediate neighbors).
/// Deals damage to monsters on tiles in range.
/// </summary>
public abstract class Tower : Structure
{
    protected virtual float damagePerSecond => GamelabGame.Instance.Balance.TowerDamage[this.GetType().Name];
    protected virtual int rangeDepth => GamelabGame.Instance.Balance.TowerRanges[this.GetType().Name];
    protected float soundTimer = 0.0f;
    public float TotalDamage {get; protected set;} = 0;

    public float DamagePerSecond => damagePerSecond;

    public Tower(string animName, (int,int) pos)
        : base(animName, pos)
    {
    }

    public abstract void Attack(GameTime gameTime);

    /// <summary>
    /// Returns all hex tiles within this tower's range (including its own tile).
    /// </summary>
    public List<(int,int)> GetTilesInRange()
    {
        return MapUtils.GetSurroundingCircle(TilePosition, 0, rangeDepth+1);
    }
}
