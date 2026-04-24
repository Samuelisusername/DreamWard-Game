using System;
using System.Collections.Generic;
using Gamelab;

public class PathFindingManager
{
    private MapManager mapManager => GamelabGame.MapManager;

    private const int RegularMonsterVisionRange = 5;
    private const int FlyingMonsterVisionRange = 8;
    private const int SimplePathMinDefenceStrength = 1; // encourages taking a shorter route
    private const int RandomTileMaxDistanceFromCenter = 7;

    // Keep track of all tiles that are going to be attacked and by who
    private readonly List<(int col, int row, int timeToBeAttacked, int monsterCount)> _activeTargets;
    private readonly Dictionary<(int col, int row), HashSet<Monster>> _monstersTargeting;
    private readonly Dictionary<Monster, (int col, int row)> _monsterToTarget;
    private readonly Dictionary<Monster, int> _monsterToTargetDist;

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


        // Already there, don't move
        //if (_monsterToTargetDist.TryGetValue(monster, out var dist) && dist <= 2)
        //{
        //    if (_monsterToTarget.TryGetValue(monster, out var targetTile))
        //    {
        //        var endStructure = mapManager.GetStructure(targetTile);
        //        if (endStructure != null && !monster.CanStepOn(endStructure))
        //        {
        //            _monsterToTargetDist[monster] = 1; // trick the monster into attacking
        //            return monster.TilePosition;
        //        }
        //    }
        //}

        UnregisterMonsterTarget(monster);
        UpdateDefenceStrengths();

        // Find out at what time can each tile, that is reachable without damage, be reached.
        (int col, int row) startTile = monster.TilePosition;
        int[,] costToGetThere = new int[mapManager.Columns, mapManager.Rows];
        (int col, int row)?[,] previousTile = new (int col, int row)?[mapManager.Columns, mapManager.Rows]; // For path reconstruction.
        int[,] timeToReach = new int[mapManager.Columns, mapManager.Rows];
        CalculateWeakestPaths(startTile, monster, _defenceStrength, costToGetThere, previousTile, timeToReach, allowThroughBreakableStructures: false);

        var allTargets = GetAllTargetStructures(monster);
        int visionRange = GetVisionRange(monster);
        int maxTargetDistance = visionRange;

        var visibleTargets = allTargets.FindAll(target =>
            MapUtils.CalculateTileDistance(startTile, target.Tile) <= maxTargetDistance &&
            CanSeeStructureAndSurroundings(startTile, target.Tile, visionRange));

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

        // prototype: monsters share knowledge of all targets
        foreach (var activeTarget in _activeTargets)
        {
            (int col, int row, int timeToBeAttacked, int monsterCount) = activeTarget;
            visibleTargets.Add(((col, row), mapManager.GetStructure((row, col))));
        }

        var bestPrimary = SelectBestTarget(visibleTargets, costToGetThere, timeToReach, IsPrimaryTarget, requireZeroDamage: false);
        if (bestPrimary != null)
        {
            var primaryPath = ReconstructPath(startTile, bestPrimary.Value.Tile, previousTile, timeToReach, monster);
            if (primaryPath.Count > 0)
            {
                RegisterTarget(monster, bestPrimary.Value.Tile, timeToReach[bestPrimary.Value.Tile.Item1, bestPrimary.Value.Tile.Item2]);
                return primaryPath[Math.Min(1, primaryPath.Count - 1)];
            }
        }

        var bestSecondary = SelectBestTarget(visibleTargets, costToGetThere, timeToReach, t => IsSecondaryTarget(monster, t), requireZeroDamage: false);
        if (bestSecondary != null)
        {
            var secondaryPath = ReconstructPath(startTile, bestSecondary.Value.Tile, previousTile, timeToReach, monster);
            if (secondaryPath.Count > 0)
            {
                RegisterTarget(monster, bestSecondary.Value.Tile, timeToReach[bestSecondary.Value.Tile.Item1, bestSecondary.Value.Tile.Item2]);
                return secondaryPath[Math.Min(1, secondaryPath.Count - 1)];
            }
        }

        for (int i = 0; i < 10; ++i)
        {
            var randomCenterTile = GetRandomTileNearCenter(RandomTileMaxDistanceFromCenter);
            CalculateWeakestPaths(startTile, monster, _simplePathDefenceStrength, costToGetThere, previousTile, timeToReach, allowThroughBreakableStructures: true);
            var centerPath = ReconstructPath(startTile, randomCenterTile, previousTile, timeToReach, monster);
            if (centerPath.Count > 1)
            {
                return centerPath[1];
            }
        }

        return GetRandomNeighborAsLastResort(startTile, monster);
    }

    /// <summary>
    /// Removes tracking information for a monster that left the map or died.
    /// </summary>
    public void DeleteMonster(Monster monster)
    {
        UnregisterMonsterTarget(monster);
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
    }

    /// <summary>
    /// Recomputes how many defenders cover each map tile.
    /// </summary>
    private void UpdateDefenceStrengths()
    {
        // reset data structures
        Array.Clear(_defenceStrength, 0, _defenceStrength.Length);
        _defenders.Clear();
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
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
                if (structure is Tower tower)
                {
                    defendedTiles = tower.GetTilesInRange();
                }
                else if (structure is Buff buff)
                {
                    defendedTiles = MapUtils.GetSurroundingTiles(buff.TilePosition);
                }
                else
                {
                    continue;
                }
                foreach (var defendedTile in defendedTiles)
                {
                    _defenceStrength[defendedTile.Item1, defendedTile.Item2]++;
                    _simplePathDefenceStrength[defendedTile.Item1, defendedTile.Item2] += 1;
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
    private void CalculateWeakestPaths((int col, int row) startTile, Monster monster, int[,] defenseStrength, int[,] costToGetThere, (int col, int row)?[,] previousTile, int[,] timeToReach, bool allowThroughBreakableStructures)
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

        int startDefence = defenseStrength[startTile.col, startTile.row];
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
                    bool isBreakableStructure = structureOnNeighbor is WallTile || structureOnNeighbor is Fence || structureOnNeighbor is Tower;
                    if (!isTarget && (!allowThroughBreakableStructures || !isBreakableStructure))
                    {
                        continue;
                    }
                }

                int neighborDefence = defenseStrength[neighbor.Item1, neighbor.Item2];
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

        return candidateTiles[Random.Shared.Next(candidateTiles.Count)];
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

        return candidates[Random.Shared.Next(candidates.Count)];
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

