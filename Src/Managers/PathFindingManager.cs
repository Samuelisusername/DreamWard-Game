using System;
using System.Collections.Generic;
using Gamelab;

public class PathFindingManager
{
    private MapManager mapManager => GamelabGame.MapManager;

    private const int RegularMonsterVisionRange = 5;
    private const int FlyingMonsterVisionRange = 8;
    private const int SimplePathMinDefenceStrength = 20; // higher number encourages taking a shorter route when discovering targets
    private const int MinDefenceStrength = 1; // higher number encourages taking a shorter route when going to a target
    private const int VisitedPenalty = 10; // per-occurrence penalty added to local cost for tiles in a monster's recent path
    private const int RecentPathLength = 5;
    private const int NormalDefenceStrength = 10;
    private const int BuffPenalty = 50;
    private const int FencePenalty = 4;
    private const int RandomTileMaxDistanceFromCenter = 7;

    // Keep track of all tiles that are going to be attacked and by who
    private readonly List<(int col, int row, int timeToBeAttacked, int monsterCount)> _activeTargets;
    private readonly Dictionary<(int col, int row), HashSet<Monster>> _monstersTargeting;
    private readonly Dictionary<Monster, (int col, int row)> _monsterToTarget;
    private readonly Dictionary<Monster, int> _monsterToTargetDist;
    private readonly Dictionary<Monster, Queue<(int col, int row)>> _recentPath;

    private readonly int[,] _defenceStrength;
    private readonly int[,] _simplePathDefenceStrength;
    private readonly Dictionary<(int col, int row), List<Entity>> _defenders;

    /// <summary>
    /// Initializes a pathfinding manager bound to a map.
    /// </summary>
    public PathFindingManager()
    {
        _activeTargets = new List<(int, int, int, int)>();
        _monstersTargeting = new Dictionary<(int, int), HashSet<Monster>>();
        _monsterToTarget = new Dictionary<Monster, (int, int)>();
        _monsterToTargetDist = new Dictionary<Monster, int>();
        _recentPath = new Dictionary<Monster, Queue<(int col, int row)>>();
        _defenceStrength = new int[mapManager.Columns, mapManager.Rows];
        _simplePathDefenceStrength = new int[mapManager.Columns, mapManager.Rows];
        _defenders = new Dictionary<(int, int), List<Entity>>();
    }

    /// <summary>
    /// Gives the monster's actual target.
    /// </summary>
    public (int, int)? GetTarget(Monster monster)
    {
        return _monsterToTarget.TryGetValue(monster, out var target) ? target : null;
    }

    public (int, int) GetNextPositionIfSafe(Monster monster, (int, int) currentTile, (int, int) candidateTile)
    {
        var endStructure = mapManager.GetStructure(candidateTile);
        var isIntendedTarget = _monsterToTarget.TryGetValue(monster, out var target) && target == candidateTile;
        if (endStructure != null && !monster.CanStepOn(endStructure))
        {
            _monsterToTargetDist[monster] = 1;
            _monsterToTarget[monster] = candidateTile;
            PushToRecentPath(monster, currentTile);
            return currentTile;
        }
        else if (endStructure != null && monster is FlyingMonster && isIntendedTarget)
        {
            _monsterToTargetDist[monster] = 0;
        }
        PushToRecentPath(monster, candidateTile);
        return candidateTile;
    }

    private void PushToRecentPath(Monster monster, (int col, int row) tile)
    {
        if (!_recentPath.TryGetValue(monster, out var queue))
        {
            queue = new Queue<(int col, int row)>();
            _recentPath[monster] = queue;
        }
        queue.Enqueue(tile);
        if (queue.Count > RecentPathLength)
            queue.Dequeue();
    }

    /// <summary>
    /// Gives the monster's next move.
    /// </summary>
    public (int, int) GetNextPosition(Monster monster)
    {
        // Each monster type has an important property, vision range, that defines how far it can see on the map. The monster will target a structure
        // only if it can see the structure and each tile within 2 tiles of the structure.

        // --- TARGET FINDING  ---
        // A new target is selected according to the following preferences:
        // 1. A structure less than or equal to 'visionRange' tiles away (in simple airwise distance) that the monster can target
        //    a. In case of a tie, the monster will prefer primary targets over secondary targets.
        //    b. If there are multiple targets of the same type, the monster will pick the one with the lowest 'costToGetThere'.
        // 2. One tile towards some randomly chosen tile near the center

        // --- PATH FINDING ---
        // The sum of squared defense strength along the path is used as a measure of path cost, where defense strength is the sum of strengths of the
        // towers defending the tile.

        // ------------------------------------

        UnregisterMonsterTarget(monster);
        var localPenalty = UpdateDefenceStrengths(monster);

        // Find out at what time can each tile, that is reachable without damage, be reached.
        (int col, int row) startTile = monster.TilePosition;
        int[,] costToGetThere = new int[mapManager.Columns, mapManager.Rows];
        (int col, int row)?[,] previousTile = new (int col, int row)?[mapManager.Columns, mapManager.Rows]; // For path reconstruction.
        int[,] timeToReach = new int[mapManager.Columns, mapManager.Rows];
        CalculateWeakestPaths(startTile, monster, _defenceStrength, localPenalty, costToGetThere, previousTile, timeToReach, allowThroughBreakableStructures: true);

        var allTargets = GetAllTargetStructures(monster);
        int visionRange = GetVisionRange(monster);
        int maxTargetDistance = visionRange;

        var visibleTargets = allTargets.FindAll(target =>
            MapUtils.CalculateTileDistance(startTile, target.Tile) <= maxTargetDistance &&
            CanSeeStructureAndSurroundings(startTile, target.Tile, visionRange));

        // prototype: monsters share knowledge of all targets
        foreach (var activeTarget in _activeTargets)
        {
            (int col, int row, int timeToBeAttacked, int monsterCount) = activeTarget;
            visibleTargets.Add(((col, row), mapManager.GetStructure((row, col))));
        }

        // if the monster can see a decoy, it will ignore all other targets
        foreach (var target in visibleTargets)
        {
            var (location, entity) = target;
            if (entity is Decoy decoy)
            {
                visibleTargets.Clear();
                visibleTargets.Add(target);
                break;
            }
        }

        var bestPrimary = SelectBestTarget(visibleTargets, costToGetThere, timeToReach, IsPrimaryTarget, requireZeroDamage: false);
        if (bestPrimary != null)
        {
            var primaryPath = ReconstructPath(startTile, bestPrimary.Value.Tile, previousTile, timeToReach, monster);
            if (primaryPath.Count > 0)
            {
                RegisterTarget(monster, bestPrimary.Value.Tile, timeToReach[bestPrimary.Value.Tile.Item1, bestPrimary.Value.Tile.Item2]);
                var nextTile = primaryPath[Math.Min(1, primaryPath.Count - 1)];
                return GetNextPositionIfSafe(monster, startTile, nextTile);
            }
        }

        var bestSecondary = SelectBestTarget(visibleTargets, costToGetThere, timeToReach, t => IsSecondaryTarget(monster, t), requireZeroDamage: false);
        if (bestSecondary != null)
        {
            var secondaryPath = ReconstructPath(startTile, bestSecondary.Value.Tile, previousTile, timeToReach, monster);
            if (secondaryPath.Count > 0)
            {
                RegisterTarget(monster, bestSecondary.Value.Tile, timeToReach[bestSecondary.Value.Tile.Item1, bestSecondary.Value.Tile.Item2]);
                var nextTile = secondaryPath[Math.Min(1, secondaryPath.Count - 1)];
                return GetNextPositionIfSafe(monster, startTile, nextTile);
            }
        }

        for (int i = RandomTileMaxDistanceFromCenter; i >= 0; --i)
        {
            var randomCenterTile = GetRandomTileNearCenter(i);
            if (localPenalty[randomCenterTile.col, randomCenterTile.row] > 0)
            {
                continue;
            }
            CalculateWeakestPaths(startTile, monster, _simplePathDefenceStrength, localPenalty, costToGetThere, previousTile, timeToReach, allowThroughBreakableStructures: false);
            var centerPath = ReconstructPath(startTile, randomCenterTile, previousTile, timeToReach, monster);
            if (centerPath.Count > 1)
            {
                var nextTile = centerPath[1];
                return GetNextPositionIfSafe(monster, startTile, nextTile);
            }
        }

        var next = GetRandomNeighborAsLastResort(startTile, monster);
        
        return GetNextPositionIfSafe(monster, startTile, next);
    }

    /// <summary>
    /// Removes tracking information for a monster that left the map or died.
    /// </summary>
    public void DeleteMonster(Monster monster)
    {
        UnregisterMonsterTarget(monster);
        _recentPath.Remove(monster);
    }

    /// <summary>
    /// returns the length of the monsters path to their next target
    /// </summary>
    /// <param name="monster"></param>
    /// <returns></returns>
    public int PathLength(Monster monster)
    {
        if (_monsterToTargetDist.TryGetValue(monster, out var dist))
        {
            return dist;
        }
        return 100;
    }

    /// <summary>
    /// Clears all target-intent tracking state, e.g. when a night ends.
    /// </summary>
    public void ClearEverythingWhenNightEnds()
    {
        _activeTargets.Clear();
        _monstersTargeting.Clear();
        _monsterToTarget.Clear();
        _monsterToTargetDist.Clear();
        _defenders.Clear();
        _recentPath.Clear();
    }

    /// <summary>
    /// Recomputes how many defenders cover each map tile.
    /// </summary>
    private int[,] UpdateDefenceStrengths(Monster monster)
    {
        // reset data structures
        _defenders.Clear();
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                _defenceStrength[col, row] = MinDefenceStrength;
                _simplePathDefenceStrength[col, row] = SimplePathMinDefenceStrength;
            }
        }

        // update based on current structures
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                Entity structure = mapManager.GetStructure((col, row));
                List<(int, int)> defendedTiles;
                int strength = 0;
                if (structure is Tower tower)
                {
                    defendedTiles = tower.GetTilesInRange();
                    strength = NormalDefenceStrength;
                }
                else if (structure is Buff buff)
                {
                    defendedTiles = MapUtils.GetSurroundingTiles(buff.TilePosition);
                    strength = BuffPenalty; // make buffed tiles more powerful
                }
                else if (monster is not FlyingMonster && (structure is Fence fence || structure is WallTile || structure is Wall))
                {
                    defendedTiles = new List<(int, int)> { (col, row) };
                    strength = FencePenalty;
                }
                else if (monster is FlyingMonster && structure is Wall)
                {
                    defendedTiles = new List<(int, int)> { (col, row) };
                    strength = FencePenalty;
                }
                else
                {
                    continue;
                }
                foreach (var defendedTile in defendedTiles)
                {
                    _defenceStrength[defendedTile.Item1, defendedTile.Item2] += strength;
                    _simplePathDefenceStrength[defendedTile.Item1, defendedTile.Item2] += strength;
                    if (!_defenders.TryGetValue(defendedTile, out var defenders))
                    {
                        defenders = new List<Entity>();
                        _defenders[defendedTile] = defenders;
                    }

                    defenders.Add(structure);
                }

            }
        }

        var localPenalty = new int[mapManager.Columns, mapManager.Rows];
        if (_recentPath.TryGetValue(monster, out var recentQueue))
        {
            foreach (var (col, row) in recentQueue)
                localPenalty[col, row] += VisitedPenalty;
        }
        return localPenalty;
    }

    /// <summary>
    /// Computes weakest paths from the start tile by minimizing maximum defense encountered.
    /// Also tracks predecessor and arrival time for each tile.
    /// </summary>
    private void CalculateWeakestPaths((int col, int row) startTile, Monster monster, int[,] defenseStrength, int[,] localPenalty, int[,] costToGetThere, (int col, int row)?[,] previousTile, int[,] timeToReach, bool allowThroughBreakableStructures)
    {
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                costToGetThere[col, row] = int.MaxValue;
                previousTile[col, row] = null;
                timeToReach[col, row] = int.MaxValue;
            }
        }

        int startDefence = defenseStrength[startTile.col, startTile.row] + localPenalty[startTile.col, startTile.row];
        costToGetThere[startTile.col, startTile.row] = startDefence * startDefence;
        timeToReach[startTile.col, startTile.row] = 0;

        var queue = new PriorityQueue<(int col, int row), (int damage, int time)>();
        queue.Enqueue(startTile, (costToGetThere[startTile.col, startTile.row], 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentDamage = costToGetThere[current.col, current.row];
            int currentTime = timeToReach[current.col, current.row];

            foreach (var neighbor in MapUtils.GetSurroundingTiles(current))
            {
                if (!MapUtils.IsInBounds(neighbor))
                {
                    continue;
                }

                Entity structureOnNeighbor = mapManager.GetStructure(neighbor);
                if (structureOnNeighbor != null && !monster.CanStepOn(structureOnNeighbor))
                {
                    bool isTarget = IsPrimaryTarget(structureOnNeighbor) || IsSecondaryTarget(monster, structureOnNeighbor);
                    bool isBreakableStructure = structureOnNeighbor is WallTile || structureOnNeighbor is Fence || structureOnNeighbor is Tower || structureOnNeighbor is Meadow || structureOnNeighbor is Decoy || structureOnNeighbor is Tent;
                    if (!isTarget && (!allowThroughBreakableStructures || !isBreakableStructure))
                    {
                        continue;
                    }
                }

                int neighborDefence = defenseStrength[neighbor.Item1, neighbor.Item2] + localPenalty[neighbor.Item1, neighbor.Item2];
                int nextDamage = currentDamage + neighborDefence;
                int nextTime = currentTime + 1;

                if (nextDamage < costToGetThere[neighbor.Item1, neighbor.Item2] ||
                    (nextDamage == costToGetThere[neighbor.Item1, neighbor.Item2] &&
                     nextTime < timeToReach[neighbor.Item1, neighbor.Item2]))
                {
                    costToGetThere[neighbor.Item1, neighbor.Item2] = nextDamage;
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
        return target is Tent || (target is Meadow meadow && meadow.HasSheep && !meadow.IsPolymorphed);
    }

    /// <summary>
    /// Returns whether a target is secondary for a given monster.
    /// </summary>
    private static bool IsSecondaryTarget(Monster monster, Entity target)
    {
        return monster.CanTarget(target) && !IsPrimaryTarget(target);
    }

    private bool CanSeeStructureAndSurroundings((int col, int row) monsterTile, (int col, int row) structureTile, int visionRange)
    {
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                if (MapUtils.CalculateTileDistance(structureTile, (col, row)) > 2)
                {
                    continue;
                }

                if (MapUtils.CalculateTileDistance(monsterTile, (col, row)) > visionRange)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int GetVisionRange(Monster monster)
    {
        return monster.Type switch
        {
            MonsterType.Flying => FlyingMonsterVisionRange,
            _ => RegularMonsterVisionRange
        };
    }

    private (int col, int row) GetRandomTileNearCenter(int maxDistanceFromCenter)
    {
        var centerTile = (col: mapManager.Columns / 2, row: mapManager.Rows / 2);
        var candidateTiles = new List<(int col, int row)>();

        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                var dist = MapUtils.CalculateTileDistance(centerTile, (col, row));
                if (dist <= 1) continue;
                if (dist > maxDistanceFromCenter) continue;
                candidateTiles.Add((col, row));
            }
        }

        if (candidateTiles.Count == 0)
        {
            return centerTile;
        }

        return candidateTiles[GamelabGame.Rng.Next(candidateTiles.Count)];
    }

    private (int col, int row) GetRandomNeighborAsLastResort((int col, int row) startTile, Monster monster)
    {
        var candidates = new List<(int col, int row)>();
        foreach (var neighbor in MapUtils.GetSurroundingTiles(startTile))
        {
            if (!MapUtils.IsInBounds(neighbor))
            {
                continue;
            }

            Entity structure = mapManager.GetStructure(neighbor);
            if (structure == null || monster.CanStepOn(structure) || structure is WallTile || structure is Fence || structure is Tower)
            {
                candidates.Add(neighbor);
            }
        }

        if (candidates.Count == 0)
        {
            return startTile;
        }

        return candidates[GamelabGame.Rng.Next(candidates.Count)];
    }

    /// <summary>
    /// Collects all structures on the map that can be targeted by the given monster.
    /// </summary>
    private List<((int, int) Tile, Entity Target)> GetAllTargetStructures(Monster monster)
    {
        var targets = new List<((int, int), Entity)>();
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                Entity structure = mapManager.GetStructure((col, row));
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
        int[,] costToGetThere,
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
            int damage = costToGetThere[col, row];
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

            int bestDamage = costToGetThere[best.Value.Tile.Item1, best.Value.Tile.Item2];
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
        _monsterToTargetDist[monster] = timeToBeAttacked;

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
        _monsterToTargetDist.Remove(monster);

        int targetIndex = _activeTargets.FindIndex(t => t.col == previousTile.col && t.row == previousTile.row);
        if (monsters.Count == 0 || mapManager.GetStructure((_activeTargets[targetIndex].col,_activeTargets[targetIndex].row)) == null)
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
    private List<(int, int)> ReconstructPath((int col, int row) startTile, (int col, int row) endTile,
                                            (int col, int row)?[,] previousTile, int[,] timeToReach, Monster monster)
    {
        if (!MapUtils.IsInBounds(endTile) || timeToReach[endTile.col, endTile.row] == int.MaxValue)
        {
            return new List<(int, int)>();
        }

        var path = new List<(int, int)>();
        var current = endTile;
        //var endStructure = mapManager.GetStructure(endTile);
        //if (!monster.CanStepOn(endStructure))
        //{
        //    current = previousTile[current.col, current.row] ?? current;
        //}
        int maxSteps = mapManager.Columns * mapManager.Rows;

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
}

