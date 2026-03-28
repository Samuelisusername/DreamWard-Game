using System;
using System.Collections.Generic;
using Gamelab;
using Microsoft.Xna.Framework;

public enum MonsterType
{
    Regular,
    Flying
}

public abstract class Monster : Entity
{
    protected MapManager mapManager => GamelabGame.MapManager;
    
    // Path the monster follows from spawn to target
    private List<(int,int)> _path = new List<(int,int)>();

    // Damage this monster deals per second when attacking
    public abstract float DamagePerSecond { get; }

    // The type of this monster (Regular or Flying)
    public abstract MonsterType Type { get; } 

    // Display name of this monster
    public abstract string Name { get; }

    // Has the monster reached the end of its path
    public bool GoalReached => _path.Count <= 1;

    // Flag set by MonsterGroup when it's time to step forward
    public bool ShouldMove { get; set; }

    // The tile this monster is trying to attack (set when blocked or when reaching goal)
    public (int,int)? AttackTarget { get; set; }

    public string Facing {get; private set;}

    public List<(int,int)> Path
    {
        get => _path;
        set
        {
            foreach (var item in value)
            {
                if (item.Item1 < 0 || item.Item1 >= mapManager.Columns || item.Item2 < 0 || item.Item2 >= mapManager.Rows)
                {
                    _path = new List<(int,int)> { value[0] };
                    Facing = "S";
                    return;
                }
            }
            _path = value;
            if(_path.Count > 1)
            {
                Facing = MapUtils.GetDirection(_path[0],_path[1]);
            }
            else
            {
                Facing = Facing ?? "S";
            }
        }
    }

    public override bool IsBuildEntity => false;

    protected override Vector2 _localOffset => throw new NotImplementedException();

    public Monster((int,int) startTile, int health = 30)
        : base(startTile, health)
    {
        // DamagePerSecond = type == MonsterType.Flying ? damagePerSecond * 0.5f : damagePerSecond;
        // Name = type == MonsterType.Regular ? "Regular" : "Flying";
        _path = GamelabGame.PathFindingManager.CalculatePath(this);
    }

    /// <summary>
    /// Returns whether this monster can target/attack the given entity.
    /// Regular: can hit all except VoidTile.
    /// Flying: can hit all except VoidTile and WallTile (flies over walls).
    /// </summary>
    public abstract bool CanTarget(Entity entity);

    /// <summary>
    /// Returns whether this monster can step on a tile occupied by the given entity.
    /// VoidTile: only flying monsters can step on it.
    /// WallTile/Fence/Tower: regular monsters cannot step on it (must attack first), flying ignores it.
    /// </summary>
    public abstract bool CanStepOn(Entity entity);

    /// <summary>
    /// Steps the monster forward on its path by one tile.
    /// Called by MonsterGroup when movement timing triggers.
    /// </summary>
    public void Move()
    {
        if (GoalReached) return;
        _path.RemoveAt(0);
        TilePosition = _path[0];        
        mapManager.AddExistingMonster(this, _path[0]);
        if(!GoalReached)
        {
            Facing = MapUtils.GetDirection(_path[0], _path[1]);
        }
    }

    /// <summary>
    /// Deals damage to entities on the same tile based on DPS and elapsed time.
    /// Only attacks when the monster has reached its goal.
    /// </summary>
    public void Attack(Entity target, float deltaTime)
    {
        if (target == null || target.IsDestroyed) return;
        target.TakeDamage(DamagePerSecond * deltaTime);
    }

    /// <summary>
    /// Returns the next tile the monster is heading toward
    /// </summary>
    public (int,int) GetCurrentGoal()
    {
        if (GoalReached)
            return _path[0];
        return _path[1];
    }
    

    public override void Update(GameTime gameTime)
    {
        throw new NotImplementedException();
    }
}
