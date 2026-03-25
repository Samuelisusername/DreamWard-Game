using System;
using System.Collections.Generic;

public enum MonsterType
{
    Regular,
    Flying
}

public class Monster : Entity
{
    // Path the monster follows from spawn to target
    private List<(int,int)> _path = new List<(int,int)>();

    // Damage this monster deals per second when attacking
    public float DamagePerSecond { get; set; }

    // The type of this monster (Regular or Flying)
    public MonsterType Type { get; set; }

    // Display name of this monster
    public string Name { get; set; }

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
                if (item.Item1 < 0 || item.Item1 >= _mapManager.Columns || item.Item2 < 0 || item.Item2 >= _mapManager.Rows)
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

    public Monster(string animName, MapManager mapManager, (int,int) startTile, (int,int) goal,
                   MonsterType type = MonsterType.Regular, float damagePerSecond = 3f, int health = 30)
        : base(animName, mapManager, startTile, health)
    {
        Type = type;
        DamagePerSecond = type == MonsterType.Flying ? damagePerSecond * 0.5f : damagePerSecond;
        Name = type == MonsterType.Regular ? "Regular" : "Flying";
        GenerateHexPath(startTile, goal);
    }

    /// <summary>
    /// Returns whether this monster can target/attack the given entity.
    /// Regular: can hit all except VoidTile.
    /// Flying: can hit all except VoidTile and WallTile (flies over walls).
    /// </summary>
    public bool CanTarget(Entity entity)
    {
        if (entity is VoidTile) return false;
        if (entity is TreeTile) return false;
        if (entity is Meadow meadow && (!meadow.HasSheep || meadow.IsPolymorphed)) return false;
        if (entity is PlacableMeadow) return false; //TODO: when can they attack meadows
        if (Type == MonsterType.Flying && entity is WallTile) return false;
        return true;
    }

    /// <summary>
    /// Returns whether this monster can step on a tile occupied by the given entity.
    /// VoidTile: only flying monsters can step on it.
    /// WallTile/Fence/Tower: regular monsters cannot step on it (must attack first), flying ignores it.
    /// </summary>
    public bool CanStepOn(Entity entity)
    {
        if (entity is VoidTile && Type != MonsterType.Flying) return false;
        if (entity is TreeTile) return false;
        if (entity is WallTile && Type != MonsterType.Flying) return false;
        if (entity is Fence && Type != MonsterType.Flying) return false;
        if (entity is Tower && Type != MonsterType.Flying) return false;
        return true;
    }

    /// <summary>
    /// Steps the monster forward on its path by one tile.
    /// Called by MonsterGroup when movement timing triggers.
    /// </summary>
    public void Move()
    {
        if (GoalReached) return;
        _path.RemoveAt(0);
        TilePosition = _path[0];
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

    /// <summary>
    /// Generates a shortest hex-grid path from start to goal using greedy neighbor stepping
    /// </summary>
    public void GenerateHexPath((int,int) start, (int,int) goal)
    {
        _path.Clear();

        if (goal == (-1,-1))
        {
            do
            {
                goal = (start.Item1 + (int)Random.Shared.NextInt64(-5,6),
                        start.Item2 + (int)Random.Shared.NextInt64(-5,6));
            } while (goal.Item1 < 0 || goal.Item1 >= _mapManager.Rows ||
                     goal.Item2 < 0 || goal.Item2 >= _mapManager.Columns);
        }

        _path.Add(start);
        (int,int) current = start;

        while (current != goal)
        {
            (int,int)[] neighbors;
            if (current.Item1 % 2 == 0)
            {
                neighbors = new[]
                {
                    (current.Item1 - 1, current.Item2 - 1),
                    (current.Item1 - 1, current.Item2),
                    (current.Item1,     current.Item2 - 1),
                    (current.Item1,     current.Item2 + 1),
                    (current.Item1 + 1, current.Item2 - 1),
                    (current.Item1 + 1, current.Item2),
                };
            }
            else
            {
                neighbors = new[]
                {
                    (current.Item1 - 1, current.Item2),
                    (current.Item1 - 1, current.Item2 + 1),
                    (current.Item1,     current.Item2 - 1),
                    (current.Item1,     current.Item2 + 1),
                    (current.Item1 + 1, current.Item2),
                    (current.Item1 + 1, current.Item2 + 1),
                };
            }

            (int,int) best = current;
            int bestDist = int.MaxValue;
            foreach (var n in neighbors)
            {
                if (n.Item1 < 0 || n.Item1 >= _mapManager.Columns || n.Item2 < 0 || n.Item2 >= _mapManager.Rows)
                    continue;
                int dist = HexDistance(n, goal);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = n;
                }
            }

            current = best;
            _path.Add(current);
        }

        Path = _path;
    }

    private static int HexDistance((int,int) a, (int,int) b)
    {
        int q1 = a.Item1, q2 = b.Item1;
        int r1 = a.Item2 - (int)Math.Floor(a.Item1 / 2.0);
        int r2 = b.Item2 - (int)Math.Floor(b.Item1 / 2.0);
        int s1 = -q1 - r1, s2 = -q2 - r2;
        return Math.Max(Math.Abs(q1 - q2), Math.Max(Math.Abs(r1 - r2), Math.Abs(s1 - s2)));
    }
}
