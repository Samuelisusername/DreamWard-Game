using System;
using System.Collections.Generic;
using Gamelab;

// Default configuration values for pathfinding behavior. These can be overriden for specific monster types in the PathFindingManager constructor.
public class PathFindingConfig
{
    public int VisionRange { get; set; } = 5; // how far away targets can the monster see (note that target information is shared between monsters)
    public int DiscoveryPhaseMinTileCost { get; set; } = 20; // higher number encourages taking a shorter route when discovering targets
    public int AttackPhaseMinTileCost { get; set; } = 2; // higher number encourages taking a shorter route when going to a target
    public int GlobalVisitedPenalty { get; set; } = 10; // higher number encourages the monsters as a whole to spread out and explore
    public int LocalVisitedStartPenalty { get; set; } = 10; // see below
    public int LocalVisitedGrowthFactor { get; set; } = 2; // if a monster has visited a tile N times recently, the penalty for that tile is LocalVisitedStartPenalty * (LocalVisitedGrowthFactor ^ N)
    public int RecentPathLength { get; set; } = 10; // how many previous tiles to keep track of for the local visited penalty
    public int NormalDefenceStrength { get; set; } = 10; // the defence strength of all towers is estimated as this number (TODO: consider DamagePerSecond)
    public int BuffPenalty { get; set; } = 50; // higher number means monsters are more repelled by buffed tiles
    public int FencePenalty { get; set; } = 4; // higher number means monster are more likely to go around fences/walls
    public int RandomTileMaxDistanceFromCenter { get; set; } = 8; // if the monsters haven't found any targets, they will move towards a random tile at most RandomTileMaxDistanceFromCenter tiles from the center
    public bool MeetUpBeforeAttacking { get; set; } = false; // if true, monsters of the same type will try to meet up at MeetingPoint before attacking targets
    public (int col, int row)? MeetingPoint { get; set; } = null; // this is where the monsters of said type meet if set to a value
}

public class PathFindingManager
{
    private MapManager mapManager => GamelabGame.MapManager;

    private readonly Dictionary<MonsterType, PathFindingConfig> _configOverrides = new();

    // Keep track of all tiles that are going to be attacked and by who
    private readonly HashSet<(int col, int row)> _discoveredTargets;
    private readonly Dictionary<(int col, int row), HashSet<Monster>> _targetToMonsters;
    private readonly Dictionary<Monster, (int col, int row)> _monsterToTarget;
    private readonly Dictionary<Monster, int> _monsterToTargetDist;
    private readonly Dictionary<Monster, Queue<(int col, int row)>> _monsterToRecentPath;

    // Defences and costs for pathfinding
    private readonly int[,] _defenceStrength;
    private readonly int[,] _simplePathDefenceStrength;
    private readonly Dictionary<(int col, int row), List<Entity>> _defenders;
    private readonly int[,] _visited;

    // Cache paths
    private readonly Dictionary<Monster, Queue<(int col, int row)>> _cachedPaths;

    // Temporary profiling variables
    private static bool _profilingEnabled = false; // Set to true to enable profiling logs
    private static bool _debugPrintsEnabled = false;
    private static readonly System.Diagnostics.Stopwatch _stepTimer = new System.Diagnostics.Stopwatch();
    private static readonly System.Diagnostics.Stopwatch _logTimer = System.Diagnostics.Stopwatch.StartNew();
    private static double _timeUpdateDef = 0;
    private static double _timeCalcPaths1 = 0;
    private static double _timeTargetVis = 0;
    private static double _timePrimary = 0;
    private static double _timeSecondary = 0;
    private static double _timeFallback = 0;
    private static double _timeCache = 0;
    private static int _callCount = 0;
    private static int _cacheHitCount = 0;

    /// <summary>
    /// Initializes a pathfinding manager bound to a map.
    /// </summary>
    public PathFindingManager()
    {
        _configOverrides[MonsterType.Flying] = new PathFindingConfig { VisionRange = 6 };
        _configOverrides[MonsterType.Big] = new PathFindingConfig { AttackPhaseMinTileCost = 30, FencePenalty = 0 };
        _configOverrides[MonsterType.Small] = new PathFindingConfig { VisionRange = 5, AttackPhaseMinTileCost = 1,
                                                                        FencePenalty = 10, MeetUpBeforeAttacking = true };

        _discoveredTargets = new HashSet<(int col, int row)>();
        _targetToMonsters = new Dictionary<(int, int), HashSet<Monster>>();
        _monsterToTarget = new Dictionary<Monster, (int, int)>();
        _monsterToTargetDist = new Dictionary<Monster, int>();
        _monsterToRecentPath = new Dictionary<Monster, Queue<(int col, int row)>>();
        _defenceStrength = new int[mapManager.Columns, mapManager.Rows];
        _simplePathDefenceStrength = new int[mapManager.Columns, mapManager.Rows];
        _defenders = new Dictionary<(int, int), List<Entity>>();
        _visited = new int[mapManager.Columns, mapManager.Rows];
        _cachedPaths = new Dictionary<Monster, Queue<(int col, int row)>>();
    }

    /// <summary>
    /// Notifies the PathFindingManager that the Structures on the map have changed (e.g. new structures added), so it can clear cached paths that may be invalid.
    /// </summary>
    public void NotifyStructuresChanged()
    {
        _cachedPaths.Clear();
        _targetToMonsters.Clear();
        _monsterToTarget.Clear();
        _monsterToTargetDist.Clear();
    }

    private PathFindingConfig GetConfig(Monster monster)
    {
        if (!_configOverrides.TryGetValue(monster.Type, out var config))
        {
            config = new PathFindingConfig();
            _configOverrides[monster.Type] = config;
        }
        return config;
    }

    /// <summary>
    /// Gives the monster's actual target.
    /// </summary>
    public (int, int)? GetTarget(Monster monster)
    {
        (int, int)? targetTile = _monsterToTarget.TryGetValue(monster, out var target) ? target : null;
        if (_debugPrintsEnabled) Log($"GetTarget returned {targetTile}, monster");
        return targetTile;
    }

    public (int, int) GetNextPositionIfSafe(Monster monster, (int, int) currentTile, (int, int) nextTile)
    {
        var config = GetConfig(monster);
        var nextStructure = mapManager.GetStructure(nextTile);
        bool isIntendedTarget = _monsterToTarget.TryGetValue(monster, out var target) && target == nextTile;
        if (nextStructure != null && monster.CanTarget(nextStructure))
         {
            bool canStepOn = monster.CanStepOn(nextStructure);
            bool isPrimaryTarget = IsPrimaryTarget(nextStructure);
            bool isFlyingAndIntended = monster is FlyingMonster && isIntendedTarget;
            if (!canStepOn || isPrimaryTarget || isFlyingAndIntended)
            {
                _monsterToTargetDist[monster] = 1;
                _monsterToTarget[monster] = nextTile;
                PushToRecentPath(monster, currentTile);
                // print canStepOn, isPrimaryTarget, isFlyingAndIntended for debugging
                if (_debugPrintsEnabled) Log($"GetNextPositionIfSafe returned the current tile {currentTile} because of structure {nextStructure.GetType()} on the way (canStepOn: {canStepOn}, isPrimaryTarget: {isPrimaryTarget}, isFlyingAndIntended: {isFlyingAndIntended})", monster);
                return currentTile;
            }
            
        }
        bool nextTileHasMonster = false;
        bool nextTileHasBigMonster = false;
        foreach (Entity e in mapManager.GetAllEntities(nextTile))
        {
            if (e is Monster m)
            {
                nextTileHasMonster = true;
                if (m is BigMonster)
                {
                    nextTileHasBigMonster = true;
                    break;
                }
            }
        }
        if (nextTileHasBigMonster || (monster is BigMonster && nextTileHasMonster))
        {
            
            if (IsStationary(monster))
            {
                PushToRecentPath(monster, nextTile);
                if (_debugPrintsEnabled) Log($"GetNextPositionIfSafe returned the next tile {nextTile} because the monster has been stationary because of another monster", monster);
                return nextTile;
            }
            PushToRecentPath(monster, currentTile);
            if (_debugPrintsEnabled) Log($"GetNextPositionIfSafe returned the current tile {currentTile} because of another monster on the way", monster);
            return currentTile;
        }
        
        _visited[nextTile.Item1, nextTile.Item2] += config.GlobalVisitedPenalty;
        PushToRecentPath(monster, nextTile);
        if (_debugPrintsEnabled) Log($"GetNextPositionIfSafe returned the next tile {nextTile}", monster);
        return nextTile;
    }

    private void PushToRecentPath(Monster monster, (int col, int row) tile)
    {
        var config = GetConfig(monster);
        if (!_monsterToRecentPath.TryGetValue(monster, out var queue))
        {
            queue = new Queue<(int col, int row)>();
            _monsterToRecentPath[monster] = queue;
        }
        queue.Enqueue(tile);
        if (queue.Count > config.RecentPathLength)
            queue.Dequeue();
    }

    private bool IsStationary(Monster monster)
    {
        if (_monsterToRecentPath.TryGetValue(monster, out var queue) && queue.Count > 0)
        {
            (int col, int row) firstTile = queue.Peek();
            foreach ((int col, int row) tile in queue)
            {
                if (tile != firstTile)
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    private void CachePath(Monster monster, List<(int, int)> path)
    {
        var queue = new Queue<(int, int)>();
        // index 0 is Current tile, index 1 is Immediate return, cache index 2 and onward
        for (int i = 2; i < path.Count; i++)
        {
            queue.Enqueue(path[i]);
        }
        _cachedPaths[monster] = queue;
    }

    private (int, int)? TryGetCachedNextPosition(Monster monster)
    {
        if (_cachedPaths.TryGetValue(monster, out var cachedPath))
        {
            if (cachedPath.Count > 0)
            {
                var nextTile = cachedPath.Dequeue();

                _monsterToTargetDist[monster] = cachedPath.Count + 1;

                var actualNextTile = GetNextPositionIfSafe(monster, monster.TilePosition, nextTile);

                // If blocked by another un-walkable entity, OR the path just ended, drop the cache!
                if (actualNextTile != nextTile || cachedPath.Count == 0)
                {
                    _cachedPaths.Remove(monster);
                }

                return actualNextTile;
            }

            _cachedPaths.Remove(monster);
        }

        return null;
    }

    /// <summary>
    /// Gives the monster's next move.
    /// </summary>
    public (int, int) GetNextPosition(Monster monster)
    {
        // 1. CACHED
        if (_profilingEnabled)
        {
            LogProfilingMetrics();
            _callCount++;
            _stepTimer.Restart();
        }
        (int col, int row) currentTile = monster.TilePosition;
        UpdateDiscoveredTargets(monster);
        var cachedPos = TryGetCachedNextPosition(monster);
        if (cachedPos.HasValue)
        {
            if (_profilingEnabled)
            {
                _cacheHitCount++;
                _timeCache += _stepTimer.Elapsed.TotalMilliseconds;
                _stepTimer.Restart();
            }
            return cachedPos.Value;
        }

        // Update defences and other penalties before calculating new paths
        UnregisterMonsterTarget(monster);
        var localPenalty = UpdateDefenceStrengths(monster);
        if (_profilingEnabled)
        {
            _timeUpdateDef += _stepTimer.Elapsed.TotalMilliseconds;
            _stepTimer.Restart();
        }

        // Precompute a path from the monster to all tiles on the map
        int[,] costToGetThere = new int[mapManager.Columns, mapManager.Rows];
        (int col, int row)?[,] previousTile = new (int col, int row)?[mapManager.Columns, mapManager.Rows]; // For path reconstruction.
        int[,] timeToReach = new int[mapManager.Columns, mapManager.Rows];
        CalculateWeakestPaths(currentTile, monster, _defenceStrength, localPenalty, costToGetThere, previousTile, timeToReach, allowThroughBreakableStructures: true);
        if (_profilingEnabled)
        {
            _timeCalcPaths1 += _stepTimer.Elapsed.TotalMilliseconds;
            _stepTimer.Restart();
        }

        // Create a list of all possible targets
        var allPossibleTargets = new List<((int,int), Entity entity)>();
        foreach (var target in _discoveredTargets)
        {
            Entity entity = mapManager.GetStructure(target);
            allPossibleTargets.Add((target, entity));
        }

        // If a monster sees a decoy, it forgets all other targets
        foreach (var target in allPossibleTargets)
        {
            var (location, entity) = target;
            if (entity is Decoy decoy)
            {
                allPossibleTargets.Clear();
                allPossibleTargets.Add(target);
                break;
            }
        }
        if (_profilingEnabled)
        {
            _timeTargetVis += _stepTimer.Elapsed.TotalMilliseconds;
            _stepTimer.Restart();
        }

        // 2. MEETING POINT (code is down below since it requires path recomputation)

        // 3. PRIMARY TARGETS
        var config = GetConfig(monster);
        var bestPrimary = SelectBestTarget(allPossibleTargets, costToGetThere, timeToReach, IsPrimaryTarget, requireZeroDamage: false);
        if (bestPrimary != null && !config.MeetingPoint.HasValue)
        {
            var primaryPath = ReconstructPath(currentTile, bestPrimary.Value.Tile, previousTile, timeToReach, monster);
            if (primaryPath.Count > 0)
            {
                RegisterTarget(monster, bestPrimary.Value.Tile, timeToReach[bestPrimary.Value.Tile.Item1, bestPrimary.Value.Tile.Item2]);
                CachePath(monster, primaryPath);
                var nextTile = primaryPath[Math.Min(1, primaryPath.Count - 1)];

                if (_profilingEnabled)
                {
                    _timePrimary += _stepTimer.Elapsed.TotalMilliseconds;
                }
                return GetNextPositionIfSafe(monster, currentTile, nextTile);
            }
        }
        if (_profilingEnabled)
        {
            _timePrimary += _stepTimer.Elapsed.TotalMilliseconds;
            _stepTimer.Restart();
        }

        // 4. SECONDARY TARGETS (all other entities that can be attacked)
        var bestSecondary = SelectBestTarget(allPossibleTargets, costToGetThere, timeToReach, t => IsSecondaryTarget(monster, t), requireZeroDamage: false);
        if (bestSecondary != null && !config.MeetingPoint.HasValue)
        {
            var secondaryPath = ReconstructPath(currentTile, bestSecondary.Value.Tile, previousTile, timeToReach, monster);
            if (secondaryPath.Count > 0)
            {
                RegisterTarget(monster, bestSecondary.Value.Tile, timeToReach[bestSecondary.Value.Tile.Item1, bestSecondary.Value.Tile.Item2]);
                CachePath(monster, secondaryPath);
                var nextTile = secondaryPath[Math.Min(1, secondaryPath.Count - 1)];

                if (_profilingEnabled)
                {
                    _timeSecondary += _stepTimer.Elapsed.TotalMilliseconds;
                }
                return GetNextPositionIfSafe(monster, currentTile, nextTile);
            }
        }
        if (_profilingEnabled)
        {
            _timeSecondary += _stepTimer.Elapsed.TotalMilliseconds;
            _stepTimer.Restart();
        }

        
        // 2. MEETING POINT and 4. RANDOM TILE NEAR CENTER
        for (int i = config.RandomTileMaxDistanceFromCenter; i >= 0; --i)
        {
            var destination = config.MeetingPoint.HasValue ? config.MeetingPoint.Value : GetRandomTileNearCenter(i);
            if (_visited[destination.col, destination.row] > 0 && !config.MeetingPoint.HasValue)
            {
                continue;
            }
            if (destination == currentTile)
            {
                if (config.MeetingPoint.HasValue)
                {
                    var realDestination = bestPrimary != null ? bestPrimary.Value.Tile : bestSecondary != null ? bestSecondary.Value.Tile : destination;
                    AttackTogetherIfMeetingPointFull(currentTile, realDestination, previousTile, timeToReach, monster);
                }
                if (_profilingEnabled)
                {
                    _timeFallback += _stepTimer.Elapsed.TotalMilliseconds;
                }
                return currentTile;
            }
            CalculateWeakestPaths(currentTile, monster, _simplePathDefenceStrength, localPenalty, costToGetThere, previousTile, timeToReach, allowThroughBreakableStructures: false);
            var centerPath = ReconstructPath(currentTile, destination, previousTile, timeToReach, monster);
            if (centerPath.Count > 0)
            {
                CachePath(monster, centerPath);
                var nextTile = centerPath[Math.Min(1, centerPath.Count - 1)];

                if (_profilingEnabled)
                {
                    _timeFallback += _stepTimer.Elapsed.TotalMilliseconds;
                }
                return GetNextPositionIfSafe(monster, currentTile, nextTile);
            }
        }

        // 5. RANDOM NEIGHBOR
        var next = GetRandomNeighborAsLastResort(currentTile, monster);
        if (_profilingEnabled)
        {
            _timeFallback += _stepTimer.Elapsed.TotalMilliseconds;
        }
        return GetNextPositionIfSafe(monster, currentTile, next);
    }

    private void UpdateDiscoveredTargets(Monster monster)
    {
        (int, int) currentTile = monster.TilePosition;
        int visionRange = GetConfig(monster).VisionRange;
        List<(int,int)> surroundingTiles = MapUtils.GetSurroundingCircle(currentTile, 0, visionRange);
        foreach (var surroundingTile in surroundingTiles)
        {
            Entity structure = mapManager.GetStructure(surroundingTile);
            // For now assume, the monster can only discover targets that it can also target itself...
            if (structure == null || structure.IsDestroyed || !monster.CanTarget(structure))
            {
                continue;
            }
            if (_discoveredTargets.Add(surroundingTile))
            {
                // Clear all caches when a *new* target is registered, since it may affect pathfinding for all monsters
                _cachedPaths.Clear();
                UpdateMeetingPoint(monster, surroundingTile);
            }
        }
    }

    private void UpdateMeetingPoint(Monster monster, (int, int) discoveredTarget)
    {
        var config = GetConfig(monster);
        if (!config.MeetUpBeforeAttacking || config.MeetingPoint.HasValue) { return; }

        // Update the defences and calculate the costs from the discovered target to all tiles
        int[,] localPenalty = UpdateDefenceStrengths(monster);
        int[,] costToGetThere = new int[mapManager.Columns, mapManager.Rows];
        (int col, int row)?[,] previousTile = new (int col, int row)?[mapManager.Columns, mapManager.Rows]; // For path reconstruction.
        int[,] timeToReach = new int[mapManager.Columns, mapManager.Rows];
        CalculateWeakestPaths(discoveredTarget, monster, _defenceStrength, localPenalty, costToGetThere, previousTile, timeToReach, allowThroughBreakableStructures: true);
        
        // Choose a tile that is not defended and that has a weak path to the target
        var minDefenceStrength = int.MaxValue;
        int minCostToGetThere = int.MaxValue;
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                if (timeToReach[col, row] == int.MaxValue)
                {
                    continue;
                }
                if (_defenceStrength[col, row] < minDefenceStrength ||
                    (_defenceStrength[col, row] == minDefenceStrength && costToGetThere[col, row] < minCostToGetThere))
                {
                    minDefenceStrength = _defenceStrength[col, row];
                    minCostToGetThere = costToGetThere[col, row];
                    config.MeetingPoint = (col, row);
                }
            }
        }
    }

    private void AttackTogetherIfMeetingPointFull((int col, int row) currentTile, (int col, int row) realDestination,
                                            (int col, int row)?[,] previousTile, int[,] timeToReach, Monster monster)
    {
        var config = GetConfig(monster);
        if (!config.MeetUpBeforeAttacking || !config.MeetingPoint.HasValue || config.MeetingPoint != monster.TilePosition)
        {
            return;
        }


        int MonstersExpectedCount = 0;
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                foreach (Entity entity in mapManager.GetAllEntities((col, row)))
                {
                    if (entity is Monster m && m.Type == monster.Type)
                    {
                        MonstersExpectedCount++;
                    }
                }
            }
        }

        int monstersArrivedCount = 0;
        List<Monster> monstersArrived = new List<Monster>();
        (int col, int row) meetingPoint = config.MeetingPoint.Value;
        foreach (Entity entity in mapManager.GetAllEntities(meetingPoint))
        {
            if (entity is Monster m && m.Type == monster.Type)
            {
                monstersArrivedCount++;
                monstersArrived.Add(m);
            }
        }

        if (monstersArrivedCount < MonstersExpectedCount) { return; }

        // Enough monsters have arrived -> attack together!
        config.MeetingPoint = null;
        _cachedPaths.Clear();

        var pathToRealTarget = ReconstructPath(currentTile, realDestination, previousTile, timeToReach, monster);
        foreach (Monster m in monstersArrived)
        {
            RegisterTarget(m, realDestination, timeToReach[realDestination.Item1, realDestination.Item2]);
            CachePath(monster, pathToRealTarget);
            var nextTile = pathToRealTarget[Math.Min(1, pathToRealTarget.Count - 1)];
        }
    }

    private void LogProfilingMetrics()
    {
        // Log every 10 seconds
        if (_logTimer.ElapsedMilliseconds > 10000)
        {
            double total = _timeCache + _timeUpdateDef + _timeCalcPaths1 + _timeTargetVis + _timePrimary + _timeSecondary + _timeFallback;
            double elapsedRealTime = _logTimer.ElapsedMilliseconds;
            Log($"\n--- PathFinding Profiling ({_callCount} calls over {elapsedRealTime / 1000.0:F1}s) ---");
            Log($"Total Time:      {total:F2} ms ({(total / elapsedRealTime * 100):F2}% of elapsed time)");
            Log($"Cache Hit Rate:  {(_callCount > 0 ? (double)_cacheHitCount / _callCount * 100 : 0):F1}% ({_cacheHitCount}/{_callCount})");
            Log($"Cached Path:     {_timeCache:F2} ms ({(total > 0 ? _timeCache / total * 100 : 0):F1}%)");
            Log($"UpdateDefences:  {_timeUpdateDef:F2} ms ({(total > 0 ? _timeUpdateDef / total * 100 : 0):F1}%)");
            Log($"CalcPaths 1:     {_timeCalcPaths1:F2} ms ({(total > 0 ? _timeCalcPaths1 / total * 100 : 0):F1}%)");
            Log($"Find Targets:    {_timeTargetVis:F2} ms ({(total > 0 ? _timeTargetVis / total * 100 : 0):F1}%)");
            Log($"Best Primary:    {_timePrimary:F2} ms ({(total > 0 ? _timePrimary / total * 100 : 0):F1}%)");
            Log($"Best Secondary:  {_timeSecondary:F2} ms ({(total > 0 ? _timeSecondary / total * 100 : 0):F1}%)");
            Log($"Fallback Center: {_timeFallback:F2} ms ({(total > 0 ? _timeFallback / total * 100 : 0):F1}%)");

            // Reset counters
            _timeUpdateDef = 0; _timeCalcPaths1 = 0; _timeTargetVis = 0;
            _timePrimary = 0; _timeSecondary = 0; _timeFallback = 0; _timeCache = 0; _callCount = 0; _cacheHitCount = 0;
            _logTimer.Restart();
        }
    }

    private void Log(string message, Monster monster = null)
    {
        if (monster == null)
        {
            System.Console.WriteLine($"[{DateTime.Now:HH:mm:ss::ff}] {message}");
            return;
        }
        var targetStr = _monsterToTarget.TryGetValue(monster, out var target) ? target.ToString() : "null";
        var distStr = _monsterToTargetDist.TryGetValue(monster, out var dist) ? dist.ToString() : "null";
        System.Console.WriteLine($"[{DateTime.Now:HH:mm:ss::ff}] {message} -- Global variables: target {targetStr}, dist {distStr}");
    }

    /// <summary>
    /// Removes tracking information for a monster that left the map or died.
    /// </summary>
    public void DeleteMonster(Monster monster)
    {
        UnregisterMonsterTarget(monster);
        _monsterToRecentPath.Remove(monster);
        _cachedPaths.Remove(monster);
    }

    /// <summary>
    /// returns the length of the monsters path to their next target
    /// </summary>
    /// <param name="monster"></param>
    /// <returns></returns>
    public int GetDistanceToTarget(Monster monster) // we're not calculating the distance to the nearest Target (the store) :)
    {
        if (_monsterToTargetDist.TryGetValue(monster, out var dist))
        {
            if (_debugPrintsEnabled) Log($"PathLength returned {dist}", monster);
            return dist;
        }
        if (_debugPrintsEnabled) Log($"PathLength returned 100", monster);
        return 100;
    }

    /// <summary>
    /// Clears all target-intent tracking state, e.g. when a night ends.
    /// </summary>
    public void ClearEverythingWhenNightEnds()
    {
        _discoveredTargets.Clear();
        _targetToMonsters.Clear();
        _monsterToTarget.Clear();
        _monsterToTargetDist.Clear();
        _defenders.Clear();
        _monsterToRecentPath.Clear();
        _cachedPaths.Clear();
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                _visited[col, row] = 0;
            }
        }

    }

    /// <summary>
    /// Recomputes how many defenders cover each map tile.
    /// </summary>
    private int[,] UpdateDefenceStrengths(Monster monster)
    {
        var config = GetConfig(monster);
        // reset data structures
        _defenders.Clear();
        for (int col = 0; col < mapManager.Columns; col++)
        {
            for (int row = 0; row < mapManager.Rows; row++)
            {
                _defenceStrength[col, row] = config.AttackPhaseMinTileCost;
                _simplePathDefenceStrength[col, row] = config.DiscoveryPhaseMinTileCost + _visited[col, row];
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
                if (structure is Spike || structure is Bomb)
                {
                    continue;
                }
                else if (structure is Tower tower)
                {
                    defendedTiles = tower.GetTilesInRange();
                    strength = config.NormalDefenceStrength;
                }
                else if (structure is Buff buff)
                {
                    defendedTiles = MapUtils.GetSurroundingTiles(buff.TilePosition);
                    strength = config.BuffPenalty; // make buffed tiles more powerful
                }
                else if (monster is not FlyingMonster && (structure is Fence fence || structure is WallTile || structure is Wall))
                {
                    defendedTiles = new List<(int, int)> { (col, row) };
                    strength = config.FencePenalty;
                }
                else if (monster is FlyingMonster && structure is Wall)
                {
                    defendedTiles = new List<(int, int)> { (col, row) };
                    strength = config.FencePenalty;
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
        if (_monsterToRecentPath.TryGetValue(monster, out var recentQueue))
        {
            foreach (var (col, row) in recentQueue)
            {
                localPenalty[col, row] = Math.Max(localPenalty[col, row], config.LocalVisitedStartPenalty);
                localPenalty[col, row] *= config.LocalVisitedGrowthFactor;
            }
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
                int tileIndex = mapManager.GetTileIndex(neighbor);
                
                // If the ground is gone and the monster is not a flyer, skip this neighbor
                if (!monster.CanStepOnTile(tileIndex))
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

        if (!_targetToMonsters.TryGetValue(targetTile, out var monsters))
        {
            monsters = new HashSet<Monster>();
            _targetToMonsters[targetTile] = monsters;
        }

        monsters.Add(monster);
        _monsterToTarget[monster] = targetTile;
        _monsterToTargetDist[monster] = timeToBeAttacked;
    }

    private void UnregisterMonsterTarget(Monster monster)
    {
        _monsterToTargetDist.Remove(monster);
        if (!_monsterToTarget.TryGetValue(monster, out var target))
        {
            return;
        }

        _monsterToTarget.Remove(monster);

        if (!_targetToMonsters.TryGetValue(target, out var monsters))
        {
            return;
        }

        monsters.Remove(monster);
        
        if (monsters.Count == 0 || mapManager.GetStructure(target) == null)
        {
            _targetToMonsters.Remove(target);
            _discoveredTargets.Remove(target);
            return;
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

