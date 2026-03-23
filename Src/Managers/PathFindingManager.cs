using System;
using System.Collections.Generic;
using Gamelab;

public class PathFindingManager
{
    private const int Step4InitialWaitTurns = 2;

    // Keep track of all tiles that are going to be attacked and by who
    private readonly List<(int col, int row, int timeToBeAttacked, int monsterCount)> _activeTargets;
    private readonly Dictionary<(int col, int row), HashSet<Monster>> _monstersTargeting;
    private readonly Dictionary<Monster, (int col, int row)> _monsterToTarget;

    private readonly int[,] _defenceStrength;
    private readonly Dictionary<(int col, int row), List<Entity>> _defenders;

    /// <summary>
    /// Initializes a pathfinding manager bound to a map.
    /// </summary>
    public PathFindingManager()
    {
        _activeTargets = new List<(int, int, int, int)>();
        _monstersTargeting = new Dictionary<(int, int), HashSet<Monster>>();
        _monsterToTarget = new Dictionary<Monster, (int, int)>();
        _defenceStrength = new int[GamelabGame.MapManager.Columns, GamelabGame.MapManager.Rows];
        _defenders = new Dictionary<(int, int), List<Entity>>();
    }

    /// <summary>
    /// Finds the cheapest path to the tent (primary target) for the given monster.
    /// If the tent can be reached without crossing blocking structures, the monster
    /// walks straight there.  If blocking structures are in the way, the path goes
    /// through them — the monster will stop adjacent to the first blocker, attack it,
    /// and recalculate once it is destroyed.
    /// </summary>
    public List<(int,int)> CalculatePath(Monster monster)
    {
        UnregisterMonsterTarget(monster);
        UpdateDefenceStrengths();

        (int col, int row) startTile = monster.TilePosition;
        int cols = GamelabGame.MapManager.Columns;
        int rows = GamelabGame.MapManager.Rows;
        var allTargets = GetAllTargetStructures(monster);

        // Phase 1: try to reach a primary target (tent/sheep) without crossing blocking structures.
        int[,] minMaxDamage1 = new int[cols, rows];
        (int col, int row)?[,] prev1 = new (int col, int row)?[cols, rows];
        int[,] time1 = new int[cols, rows];
        CalculateWeakestPaths(startTile, minMaxDamage1, prev1, time1, monster, allowThroughStructures: false);

        var primary1 = SelectBestTarget(allTargets, minMaxDamage1, time1, IsPrimaryTarget, requireZeroDamage: false);
        if (primary1 != null)
        {
            var path = ReconstructPath(startTile, primary1.Value.Tile, prev1, time1);
            if (path.Count > 0)
                return path;
        }

        // Phase 2: tent is blocked — allow pathfinding through blocking structures.
        // The monster will stop adjacent to the first structure it can't step on and attack it.
        // When that structure is destroyed, CombatManager triggers a full recalculation.
        int[,] minMaxDamage2 = new int[cols, rows];
        (int col, int row)?[,] prev2 = new (int col, int row)?[cols, rows];
        int[,] time2 = new int[cols, rows];
        CalculateWeakestPaths(startTile, minMaxDamage2, prev2, time2, monster, allowThroughStructures: true);

        var primary2 = SelectBestTarget(allTargets, minMaxDamage2, time2, IsPrimaryTarget, requireZeroDamage: false);
        if (primary2 != null)
        {
            var path = ReconstructPath(startTile, primary2.Value.Tile, prev2, time2);
            if (path.Count > 0)
                return path;
        }

        return new List<(int, int)> { startTile };
    }

    // --- OLD 4-STEP STRATEGY (commented out) ---
    // public List<(int,int)> CalculatePath_Old(Monster monster)
    // {
    //     // --- GENERAL PATHFINDING STRATEGY ---
    //     // We define a 'primary target' as a target that reduces the player's sanity when destroyed.
    //     // All other targets are secondary targets.
    //     // We define the 'weakest path' to some target as a path that minimizes the maximum damage
    //     // taken at any tile along the path to the target.
    //     // A monster has the following preferences:
    //     // 1. A primary target that can be reached without getting damaged at any point.
    //     //      --> tent, sheep
    //     // 2. A secondary target that can be reached without getting damaged at any point.
    //     //      --> wall
    //     // 3. A target that another monster is going to and that the first monster can reach at the
    //     //    same time as the other monster without getting damaged *before* arriving in the
    //     //    target's attack range.
    //     //      --> tower
    //     // 4. The first secondary target on the 'weakest path' to any of the primary targets. The
    //     //    monster will wait a little bit to give the other monsters a chance to join in.
    //     // ------------------------------------
    //     // Remove stale assignment for this monster before recalculating.
    //     UnregisterMonsterTarget(monster);
    //     UpdateDefenceStrengths();
    //     (int col, int row) startTile = monster.TilePosition;
    //     int[,] minMaxDamage = new int[GamelabGame.MapManager.Columns, GamelabGame.MapManager.Rows];
    //     (int col, int row)?[,] previousTile = new (int col, int row)?[GamelabGame.MapManager.Columns, GamelabGame.MapManager.Rows];
    //     int[,] timeToReach = new int[GamelabGame.MapManager.Columns, GamelabGame.MapManager.Rows];
    //     CalculateWeakestPaths(startTile, minMaxDamage, previousTile, timeToReach, monster, allowThroughStructures: false);
    //     var allTargets = GetAllTargetStructures(monster);
    //     // 1-2: zero-damage primary/secondary
    //     var primaryZero = SelectBestTarget(allTargets, minMaxDamage, timeToReach, IsPrimaryTarget, requireZeroDamage: true);
    //     if (primaryZero != null) { var p = ReconstructPath(startTile, primaryZero.Value.Tile, previousTile, timeToReach); if (p.Count > 0) { RegisterTarget(monster, primaryZero.Value.Tile, timeToReach[primaryZero.Value.Tile.Item1, primaryZero.Value.Tile.Item2]); return p; } }
    //     var secondaryZero = SelectBestTarget(allTargets, minMaxDamage, timeToReach, t => IsSecondaryTarget(monster, t), requireZeroDamage: true);
    //     if (secondaryZero != null) { var p = ReconstructPath(startTile, secondaryZero.Value.Tile, previousTile, timeToReach); if (p.Count > 0) { RegisterTarget(monster, secondaryZero.Value.Tile, timeToReach[secondaryZero.Value.Tile.Item1, secondaryZero.Value.Tile.Item2]); return p; } }
    //     // 3: join active targets
    //     foreach (var activeTarget in _activeTargets) { /* ... coordination logic ... */ }
    //     // 4: weakest path to primary, attack first blocker
    //     var weakestPrimary = SelectBestTarget(allTargets, minMaxDamage, timeToReach, IsPrimaryTarget, requireZeroDamage: false);
    //     if (weakestPrimary != null) { /* ... find first secondary on path ... */ }
    //     return new List<(int, int)> { startTile };
    // }

    /// <summary>
    /// Removes tracking information for a monster that left the map or died.
    /// </summary>
    public void DeleteMonster(Monster monster)
    {
        UnregisterMonsterTarget(monster);
    }

    /// <summary>
    /// Clears all target-intent tracking state, e.g. when a night ends.
    /// </summary>
    public void ClearEverythingWhenNightEnds()
    {
        _activeTargets.Clear();
        _monstersTargeting.Clear();
        _monsterToTarget.Clear();
    }

    /// <summary>
    /// Recomputes how many defenders cover each map tile.
    /// </summary>
    private void UpdateDefenceStrengths()
    {
        Array.Clear(_defenceStrength, 0, _defenceStrength.Length);
        _defenders.Clear();

        for (int col = 0; col < GamelabGame.MapManager.Columns; col++)
        {
            for (int row = 0; row < GamelabGame.MapManager.Rows; row++)
            {
                Entity structure = GamelabGame.MapManager.GetStructure(col, row);
                if (structure is not Tower tower)
                {
                    continue;
                }

                foreach (var defendedTile in tower.GetTilesInRange())
                {
                    _defenceStrength[defendedTile.Item1, defendedTile.Item2]++;
                    if (!_defenders.TryGetValue(defendedTile, out var defenders))
                    {
                        defenders = new List<Entity>();
                        _defenders[defendedTile] = defenders;
                    }

                    defenders.Add(structure);
                }
            }
        }
    }

    /// <summary>
    /// Computes weakest paths from the start tile by minimizing maximum defense encountered.
    /// Also tracks predecessor and arrival time for each tile.
    /// </summary>
    private void CalculateWeakestPaths((int col, int row) startTile, int[,] minMaxDamage,
        (int col, int row)?[,] previousTile, int[,] timeToReach, Monster monster, bool allowThroughStructures)
    {
        for (int col = 0; col < GamelabGame.MapManager.Columns; col++)
        {
            for (int row = 0; row < GamelabGame.MapManager.Rows; row++)
            {
                minMaxDamage[col, row] = int.MaxValue;
                previousTile[col, row] = null;
                timeToReach[col, row] = int.MaxValue;
            }
        }

        minMaxDamage[startTile.col, startTile.row] = _defenceStrength[startTile.col, startTile.row];
        timeToReach[startTile.col, startTile.row] = 0;

        var queue = new PriorityQueue<(int col, int row), (int damage, int time)>();
        queue.Enqueue(startTile, (minMaxDamage[startTile.col, startTile.row], 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentDamage = minMaxDamage[current.col, current.row];
            int currentTime = timeToReach[current.col, current.row];

            Entity currentStructure = GamelabGame.MapManager.GetStructure(current.col, current.row);

            if (allowThroughStructures)
            {
                // Only block truly impassable tiles (VoidTile for non-flying monsters)
                if (currentStructure is VoidTile && monster.Type != MonsterType.Flying)
                    continue;
            }
            else
            {
                // Block at structures the monster can't step on
                if (currentStructure != null && !monster.CanStepOn(currentStructure))
                    continue;
            }

            foreach (var neighbor in Tower.GetHexNeighbors(current))
            {
                if (!IsInBounds(neighbor))
                {
                    continue;
                }

                int nextDamage = Math.Max(currentDamage, _defenceStrength[neighbor.Item1, neighbor.Item2]);
                int nextTime = currentTime + 1;

                if (nextDamage < minMaxDamage[neighbor.Item1, neighbor.Item2] ||
                    (nextDamage == minMaxDamage[neighbor.Item1, neighbor.Item2] &&
                     nextTime < timeToReach[neighbor.Item1, neighbor.Item2]))
                {
                    minMaxDamage[neighbor.Item1, neighbor.Item2] = nextDamage;
                    timeToReach[neighbor.Item1, neighbor.Item2] = nextTime;
                    previousTile[neighbor.Item1, neighbor.Item2] = current;
                    queue.Enqueue(neighbor, (nextDamage, nextTime));
                }
            }
        }
    }

    /// <summary>
    /// Returns whether a target is primary (reduces sanity when destroyed).
    /// </summary>
    private static bool IsPrimaryTarget(Entity target)
    {
        return target is Tent || target is Sheep;
    }

    /// <summary>
    /// Returns whether a target is secondary for a given monster.
    /// </summary>
    private static bool IsSecondaryTarget(Monster monster, Entity target)
    {
        return monster.CanTarget(target) && !IsPrimaryTarget(target);
    }

    /// <summary>
    /// Collects all structures on the map that can be targeted by the given monster.
    /// </summary>
    private List<((int, int) Tile, Entity Target)> GetAllTargetStructures(Monster monster)
    {
        var targets = new List<((int, int), Entity)>();
        for (int col = 0; col < GamelabGame.MapManager.Columns; col++)
        {
            for (int row = 0; row < GamelabGame.MapManager.Rows; row++)
            {
                Entity structure = GamelabGame.MapManager.GetStructure(col, row);
                if (structure == null || structure.IsDestroyed || !monster.CanTarget(structure))
                {
                    continue;
                }

                targets.Add(((col, row), structure));
            }
        }

        return targets;
    }

    /// <summary>
    /// Selects the best target candidate according to predicate, damage, and arrival time.
    /// </summary>
    private ((int, int) Tile, Entity Target)? SelectBestTarget(
        List<((int, int) Tile, Entity Target)> allTargets,
        int[,] minMaxDamage,
        int[,] timeToReach,
        Func<Entity, bool> selector,
        bool requireZeroDamage)
    {
        ((int, int) Tile, Entity Target)? best = null;
        foreach (var candidate in allTargets)
        {
            if (!selector(candidate.Target))
            {
                continue;
            }

            int col = candidate.Tile.Item1;
            int row = candidate.Tile.Item2;
            int damage = minMaxDamage[col, row];
            int time = timeToReach[col, row];
            if (damage == int.MaxValue || time == int.MaxValue)
            {
                continue;
            }

            if (requireZeroDamage && damage != 0)
            {
                continue;
            }

            if (best == null)
            {
                best = candidate;
                continue;
            }

            int bestDamage = minMaxDamage[best.Value.Tile.Item1, best.Value.Tile.Item2];
            int bestTime = timeToReach[best.Value.Tile.Item1, best.Value.Tile.Item2];

            if (damage < bestDamage || (damage == bestDamage && time < bestTime))
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Registers monster intent to attack a target tile and updates tracking collections.
    /// </summary>
    private void RegisterTarget(Monster monster, (int col, int row) targetTile, int timeToBeAttacked)
    {
        UnregisterMonsterTarget(monster);

        if (!_monstersTargeting.TryGetValue(targetTile, out var monsters))
        {
            monsters = new HashSet<Monster>();
            _monstersTargeting[targetTile] = monsters;
        }

        monsters.Add(monster);
        _monsterToTarget[monster] = targetTile;

        int targetIndex = _activeTargets.FindIndex(t => t.col == targetTile.col && t.row == targetTile.row);
        if (targetIndex >= 0)
        {
            var existing = _activeTargets[targetIndex];
            _activeTargets[targetIndex] =
                (existing.col, existing.row, Math.Max(existing.timeToBeAttacked, timeToBeAttacked), monsters.Count);
        }
        else
        {
            _activeTargets.Add((targetTile.col, targetTile.row, timeToBeAttacked, monsters.Count));
        }
    }

    private void UnregisterMonsterTarget(Monster monster)
    {
        if (!_monsterToTarget.TryGetValue(monster, out var previousTile))
        {
            return;
        }

        _monsterToTarget.Remove(monster);

        if (!_monstersTargeting.TryGetValue(previousTile, out var monsters))
        {
            return;
        }

        monsters.Remove(monster);

        int targetIndex = _activeTargets.FindIndex(t => t.col == previousTile.col && t.row == previousTile.row);
        if (monsters.Count == 0)
        {
            _monstersTargeting.Remove(previousTile);
            if (targetIndex >= 0)
            {
                _activeTargets.RemoveAt(targetIndex);
            }
            return;
        }

        if (targetIndex >= 0)
        {
            var existing = _activeTargets[targetIndex];
            _activeTargets[targetIndex] = (existing.col, existing.row, existing.timeToBeAttacked, monsters.Count);
        }
    }

    /// <summary>
    /// Reconstructs a path from start to end using predecessor data.
    /// </summary>
    private List<(int, int)> ReconstructPath((int col, int row) startTile, (int col, int row) endTile, (int col, int row)?[,] previousTile, int[,] timeToReach)
    {
        if (!IsInBounds(endTile) || timeToReach[endTile.col, endTile.row] == int.MaxValue)
        {
            return new List<(int, int)>();
        }

        var path = new List<(int, int)>();
        var current = endTile;
        int maxSteps = GamelabGame.MapManager.Columns * GamelabGame.MapManager.Rows;

        for (int steps = 0; steps <= maxSteps; steps++)
        {
            path.Add(current);
            if (current == startTile)
            {
                path.Reverse();
                return path;
            }

            var previous = previousTile[current.col, current.row];
            if (previous == null)
            {
                return new List<(int, int)>();
            }

            current = previous.Value;
        }

        return new List<(int, int)>();
    }

    /// <summary>
    /// Prepends repeated start-tile entries to make the monster wait before moving.
    /// </summary>
    private static void AddInitialWait(List<(int, int)> path, (int col, int row) startTile, int waitTurns)
    {
        for (int i = 0; i < waitTurns; i++)
        {
            path.Insert(0, startTile);
        }
    }

    private bool IsInBounds((int col, int row) tile)
    {
        return tile.col >= 0 && tile.col < GamelabGame.MapManager.Columns && tile.row >= 0 && tile.row < GamelabGame.MapManager.Rows;
    }

    /// <summary>
    /// calculates the shortest distance (in number of tiles) between two tiles on the map.
    /// does not take into account possible obstacles on the map.
    /// </summary>
    /// <param name="col1">the column of the first tile</param>
    /// <param name="col2">the column of the second tile</param>
    /// <param name="row1">the row of the first tile</param>
    /// <param name="row2">the row of the second tile</param>
    public int CalculateTileDistance(int col1, int row1, int col2, int row2)
    {
        // Map to cube coordinates
        int q1 = col1;
        int q2 = col2;
        int r1 = row1 - (int) Math.Floor(col1 / 2.0);
        int r2 = row2 - (int) Math.Floor(col2 / 2.0);
        int s1 = -q1 - r1;
        int s2 = -q2 - r2;

        // The distance is the maximum absolute difference among the three axes
        int delta_q = Math.Abs(q1 - q2);
        int delta_r = Math.Abs(r1 - r2);
        int delta_s = Math.Abs(s1 - s2);
        return Math.Max(delta_q, Math.Max(delta_r, delta_s));
    }

}