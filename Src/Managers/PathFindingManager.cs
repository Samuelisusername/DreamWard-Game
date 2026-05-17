using System;
using System.Collections.Generic;
using Gamelab;

public class PathFindingManager
{
    // Default configuration values for pathfinding behavior. These can be overridden per monster type in the constructor.
    private class PathFindingConfig
    {
        public int VisionRange { get; set; } = 5;
        public int DiscoveryPhaseMinTileCost { get; set; } = 20;
        public int AttackPhaseMinTileCost { get; set; } = 2;
        public int NormalDefenceStrength { get; set; } = 10;
        public int BuffPenalty { get; set; } = 50;
        public int FencePenalty { get; set; } = 4;
        public int RandomTileMaxDistanceFromCenter { get; set; } = 8;
        public bool MeetUpBeforeAttacking { get; set; } = false;
        public (int col, int row)? MeetingPoint { get; set; } = null;
    }

    // Per-type computed path state, rebuilt whenever structures change or new targets are discovered.
    private class TypePathData
    {
        public (int col, int row)?[,] NextTile;
        public (int col, int row)?[,] NextTarget;    // immediate next structure to attack on path
        public int[,] TileCostAttackPhase;
        public int[,] TileCostDiscoveryPhase;
        public bool DefenceDirty = true;
        public bool PathDirty = true;
        public List<(int col, int row)> DiscoverySupersource = new();

        public TypePathData(int columns, int rows)
        {
            NextTile = new (int col, int row)?[columns, rows];
            NextTarget = new (int col, int row)?[columns, rows];
            TileCostAttackPhase = new int[columns, rows];
            TileCostDiscoveryPhase = new int[columns, rows];
        }
    }

    private const int StationaryCheckLength = 5;

    private MapManager MapManager => GamelabGame.MapManager;

    private readonly Dictionary<MonsterType, PathFindingConfig> _configOverrides = new();
    private readonly Dictionary<MonsterType, TypePathData> _typeData = new();
    private readonly HashSet<(int col, int row)> _discoveredTargets;
    private readonly Dictionary<Monster, Queue<(int col, int row)>> _monsterToRecentPath;

    // Profiling
    private static bool _profilingEnabled = false;
    private static bool _debugPrintsEnabled = false;
    private static readonly System.Diagnostics.Stopwatch _logTimer = System.Diagnostics.Stopwatch.StartNew();
    private static readonly double _swFreqMs = System.Diagnostics.Stopwatch.Frequency / 1000.0;
    private static double _timeComputeDefence = 0;
    private static double _timeComputePaths = 0;
    private static double _timeGetNext = 0;
    private static int _callCount = 0;

    public PathFindingManager()
    {
        _configOverrides[MonsterType.Flying] = new PathFindingConfig { VisionRange = 6 };
        _configOverrides[MonsterType.Big] = new PathFindingConfig { AttackPhaseMinTileCost = 30, FencePenalty = 0 };
        _configOverrides[MonsterType.Small] = new PathFindingConfig { VisionRange = 5, AttackPhaseMinTileCost = 1,
                                                                        FencePenalty = 10, MeetUpBeforeAttacking = true };
        _discoveredTargets = new HashSet<(int col, int row)>();
        _monsterToRecentPath = new Dictionary<Monster, Queue<(int col, int row)>>();
    }

    private TypePathData GetOrCreateTypeData(MonsterType type)
    {
        if (!_typeData.TryGetValue(type, out var data))
        {
            data = new TypePathData(MapManager.Columns, MapManager.Rows);
            _typeData[type] = data;
        }
        return data;
    }

    private PathFindingConfig GetConfig(MonsterType type)
    {
        if (!_configOverrides.TryGetValue(type, out var config))
        {
            config = new PathFindingConfig();
            _configOverrides[type] = config;
        }
        return config;
    }

    private PathFindingConfig GetConfig(Monster monster) => GetConfig(monster.Type);

    // -------------------------------------------------------------------------
    // Public lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call when structures are built or destroyed. Marks defence and path data dirty so they get recomputed.
    /// </summary>
    public void NotifyStructuresChanged()
    {
        _discoveredTargets.RemoveWhere(tile =>
        {
            var s = MapManager.GetStructure(tile);
            return s == null || s.IsDestroyed;
        });

        foreach (var data in _typeData.Values)
        {
            data.DefenceDirty = true;
            data.PathDirty = true;
        }
    }

    /// <summary>
    /// Resets all pathfinding state between nights: discovered targets, recent paths, supersources, and meeting points.
    /// </summary>
    public void ClearEverythingWhenNightEnds()
    {
        _discoveredTargets.Clear();
        _monsterToRecentPath.Clear();
        foreach (var data in _typeData.Values)
        {
            data.DefenceDirty = true;
            data.PathDirty = true;
            data.DiscoverySupersource.Clear();
        }
        foreach (var config in _configOverrides.Values)
            config.MeetingPoint = null;
    }

    /// <summary>
    /// Removes tracking information for a monster that left the map or died.
    /// </summary>
    public void DeleteMonster(Monster monster)
    {
        _monsterToRecentPath.Remove(monster);
    }

    // -------------------------------------------------------------------------
    // Public per-tick queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gives the monster's next move. Should only be called when the monster is ready to move to the next tile, not every tick.
    /// </summary>
    public (int, int) GetNextPosition(Monster monster)
    {
        long t0 = _profilingEnabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        if (_profilingEnabled) { LogProfilingMetrics(); _callCount++; }

        (int col, int row) currentTile = monster.TilePosition;
        UpdateDiscoveredTargets(monster);

        var data = GetOrCreateTypeData(monster.Type);
        var config = GetConfig(monster);

        // If the monster just arrived at the meeting point, check whether to begin attacking
        if (config.MeetUpBeforeAttacking && config.MeetingPoint.HasValue && currentTile == config.MeetingPoint.Value)
            AttackTogetherIfMeetingPointFull(monster);

        if (data.PathDirty)
        {
            long tc = System.Diagnostics.Stopwatch.GetTimestamp();
            ComputeTypePaths(monster, data);
            if (_profilingEnabled) _timeComputePaths += ElapsedMs(tc);
        }

        bool isDiscoveryPhase = _discoveredTargets.Count == 0;
        var nextTileNullable = data.NextTile[currentTile.col, currentTile.row];

        if (nextTileNullable == null)
        {
            // Monster is at a supersource (target or discovery tile) or map is disconnected
            if (isDiscoveryPhase && data.DiscoverySupersource.Contains(currentTile))
                data.PathDirty = true;
            if (_profilingEnabled) _timeGetNext += ElapsedMs(t0);
            return currentTile;
        }

        (int, int) nextTile = nextTileNullable.Value;

        // 30% chance to take a random neighbor in discovery phase for natural spread
        if (isDiscoveryPhase && GamelabGame.Rng.NextDouble() < 0.3)
            nextTile = GetRandomNeighbor(currentTile, monster);

        if (_profilingEnabled) _timeGetNext += ElapsedMs(t0);
        return GetNextPositionIfPossible(monster, currentTile, nextTile);
    }

    /// <summary>
    /// Gives the monster's actual target (the adjacent structure it should be attacking right now).
    /// </summary>
    public (int, int)? GetTarget(Monster monster)
    {
        var data = GetOrCreateTypeData(monster.Type);
        var (col, row) = monster.TilePosition;
        var target = data.NextTarget[col, row];
        if (target == null) return null;
        if (_debugPrintsEnabled) Log($"GetTarget returned {target}", monster);
        return MapUtils.CalculateTileDistance(target.Value, monster.TilePosition) > 1 ? null : target;
    }

    // -------------------------------------------------------------------------
    // Path computation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Determines the supersource for the current phase and runs multi-source Dijkstra to fill NextTile and NextTarget.
    /// </summary>
    private void ComputeTypePaths(Monster sampleMonster, TypePathData data)
    {
        MonsterType type = sampleMonster.Type;
        var config = GetConfig(type);

        if (data.DefenceDirty)
        {
            long td = System.Diagnostics.Stopwatch.GetTimestamp();
            ComputeTileCosts(type, data);
            if (_profilingEnabled) _timeComputeDefence += ElapsedMs(td);
        }

        bool isDiscoveryPhase = _discoveredTargets.Count == 0;
        List<(int, int)> supersource;
        int[,] tileCost;
        bool allowThrough;

        // Determine supersource tiles (i.e. targets to head towards) and which tile costs to use
        if (isDiscoveryPhase)
        {
            supersource = SelectDiscoverySupersource(config);
            data.DiscoverySupersource = supersource;
            tileCost = data.TileCostDiscoveryPhase;
            allowThrough = false;
        }
        else if (config.MeetingPoint.HasValue)
        {
            supersource = new List<(int, int)> { config.MeetingPoint.Value };
            tileCost = data.TileCostDiscoveryPhase;
            allowThrough = false;
        }
        else
        {
            var primaryTargets = new List<(int, int)>();
            var decoyTargets = new List<(int, int)>();

            foreach (var tile in _discoveredTargets)
            {
                var entity = MapManager.GetStructure(tile);
                if (entity == null) continue;
                if (entity is Decoy)
                    decoyTargets.Add(tile);
                else if (IsPrimaryTarget(entity))
                    primaryTargets.Add(tile);
            }

            if (decoyTargets.Count > 0)
                supersource = decoyTargets;
            else if (primaryTargets.Count > 0)
                supersource = primaryTargets;
            else
                supersource = new List<(int, int)>(_discoveredTargets);

            tileCost = data.TileCostAttackPhase;
            allowThrough = true;
        }

        if (supersource.Count == 0)
        {
            data.PathDirty = false;
            return;
        }

        int cols = MapManager.Columns, rows = MapManager.Rows;
        for (int col = 0; col < cols; col++)
            for (int row = 0; row < rows; row++)
            {
                data.NextTile[col, row] = null;
                data.NextTarget[col, row] = null;
            }

        var costTemp = new int[cols, rows];
        CalculateWeakestPaths(supersource, tileCost, sampleMonster, costTemp, data.NextTile, data.NextTarget, allowThrough);

        data.PathDirty = false;
    }

    /// <summary>
    /// Computes tile cost arrays for attack and discovery phases for the given monster type.
    /// </summary>
    private void ComputeTileCosts(MonsterType type, TypePathData data)
    {
        var config = GetConfig(type);
        int cols = MapManager.Columns, rows = MapManager.Rows;

        for (int col = 0; col < cols; col++)
            for (int row = 0; row < rows; row++)
            {
                data.TileCostAttackPhase[col, row] = config.AttackPhaseMinTileCost;
                data.TileCostDiscoveryPhase[col, row] = config.DiscoveryPhaseMinTileCost;
            }

        for (int col = 0; col < cols; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                Entity structure = MapManager.GetStructure((col, row));
                if (structure == null) continue;

                List<(int, int)> defendedTiles;
                int cost;

                if (structure is Spike || structure is Bomb)
                    continue;
                else if (structure is Tower tower)
                {
                    defendedTiles = tower.GetTilesInRange();
                    cost = config.NormalDefenceStrength;
                }
                else if (structure is Buff buff)
                {
                    defendedTiles = MapUtils.GetSurroundingTiles(buff.TilePosition);
                    cost = config.BuffPenalty;
                }
                else if (type != MonsterType.Flying && (structure is Fence || structure is WallTile || structure is Wall))
                {
                    defendedTiles = new List<(int, int)> { (col, row) };
                    cost = config.FencePenalty;
                }
                else if (type == MonsterType.Flying && structure is Wall)
                {
                    defendedTiles = new List<(int, int)> { (col, row) };
                    cost = config.FencePenalty;
                }
                else
                    continue;

                foreach (var (tc, tr) in defendedTiles)
                {
                    data.TileCostAttackPhase[tc, tr] += cost;
                    data.TileCostDiscoveryPhase[tc, tr] += cost;
                }
            }
        }

        data.DefenceDirty = false;
    }

    // Returns 3 random tiles near the map center as Dijkstra supersources, so monsters spread organically during discovery rather than converging on one point.
    private List<(int col, int row)> SelectDiscoverySupersource(PathFindingConfig config)
    {
        var center = (col: MapManager.Columns / 2, row: MapManager.Rows / 2);
        var candidates = new List<(int col, int row)>();
        int maxDist = config.RandomTileMaxDistanceFromCenter;

        for (int col = 0; col < MapManager.Columns; col++)
            for (int row = 0; row < MapManager.Rows; row++)
            {
                int d = MapUtils.CalculateTileDistance(center, (col, row));
                if (d > 1 && d <= maxDist)
                    candidates.Add((col, row));
            }

        if (candidates.Count == 0)
            return new List<(int, int)> { center };

        const int count = 3;
        var result = new List<(int col, int row)>(count);
        var used = new HashSet<int>();
        int tries = 0;
        while (result.Count < count && tries < 200)
        {
            int idx = GamelabGame.Rng.Next(candidates.Count);
            if (used.Add(idx)) result.Add(candidates[idx]);
            tries++;
        }
        return result;
    }

    /// <summary>
    /// Multi-source Dijkstra minimising cumulative defence cost, computing NextTile and NextTarget for all tiles.
    /// NextTarget[T] is the immediate next structure to attack on the optimal path from T to the supersource.
    /// </summary>
    private void CalculateWeakestPaths(
        List<(int col, int row)> sources,
        int[,] tileCosts,
        Monster sampleMonster,
        int[,] costToGetThere,
        (int col, int row)?[,] nextTile,
        (int col, int row)?[,] nextTarget,
        bool allowThroughBreakableStructures)
    {
        int cols = MapManager.Columns, rows = MapManager.Rows;
        var timeToReach = new int[cols, rows];

        for (int col = 0; col < cols; col++)
            for (int row = 0; row < rows; row++)
            {
                costToGetThere[col, row] = int.MaxValue;
                timeToReach[col, row] = int.MaxValue;
            }

        var queue = new PriorityQueue<(int col, int row), (int damage, int time)>();

        foreach (var s in sources)
        {
            int cost = tileCosts[s.col, s.row];
            costToGetThere[s.col, s.row] = cost * cost;
            timeToReach[s.col, s.row] = 0;
            nextTile[s.col, s.row] = null;
            // Only mark the supersource as an attack target if it actually has an attackable structure.
            // Grass tiles (discovery) and meeting points (meetup) should not appear as targets.
            Entity sourceStructure = MapManager.GetStructure(s);
            nextTarget[s.col, s.row] = (sourceStructure != null && !sourceStructure.IsDestroyed && sampleMonster.CanTarget(sourceStructure)) ? s : null;
            queue.Enqueue(s, (costToGetThere[s.col, s.row], 0));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentDamage = costToGetThere[current.col, current.row];
            int currentTime = timeToReach[current.col, current.row];

            // Determine nextTarget propagation value for neighbors:
            // if 'current' has a secondary-target structure (can't step on, can attack),
            // neighbors must attack 'current' before passing through — so their nextTarget = current.
            // Otherwise they inherit current's nextTarget (path carries the target up the chain).
            Entity structureOnCurrent = MapManager.GetStructure(current);
            bool currentIsSecondary = structureOnCurrent != null && !structureOnCurrent.IsDestroyed
                && !sampleMonster.CanStepOn(structureOnCurrent) && sampleMonster.CanTarget(structureOnCurrent);
            var propagatedNextTarget = currentIsSecondary ? current : nextTarget[current.col, current.row];

            foreach (var neighbor in MapUtils.GetSurroundingTiles(current))
            {
                if (!MapUtils.IsInBounds(neighbor)) continue;

                int tileIndex = MapManager.GetTileIndex(neighbor);
                if (!sampleMonster.CanStepOnTile(tileIndex)) continue;

                Entity structureOnNeighbor = MapManager.GetStructure(neighbor);
                if (structureOnNeighbor != null && !sampleMonster.CanStepOn(structureOnNeighbor))
                {
                    bool isTarget = IsPrimaryTarget(structureOnNeighbor) || IsSecondaryTarget(sampleMonster, structureOnNeighbor);
                    bool isBreakable = structureOnNeighbor is WallTile || structureOnNeighbor is Fence
                        || structureOnNeighbor is Tower || structureOnNeighbor is Meadow
                        || structureOnNeighbor is Decoy || structureOnNeighbor is Tent;
                    if (!isTarget && (!allowThroughBreakableStructures || !isBreakable)) continue;
                }

                int neighborCost = tileCosts[neighbor.Item1, neighbor.Item2];
                int nextDamage = currentDamage + neighborCost;
                int nextTime = currentTime + 1;

                if (nextDamage < costToGetThere[neighbor.Item1, neighbor.Item2] ||
                    (nextDamage == costToGetThere[neighbor.Item1, neighbor.Item2] &&
                     nextTime < timeToReach[neighbor.Item1, neighbor.Item2]))
                {
                    costToGetThere[neighbor.Item1, neighbor.Item2] = nextDamage;
                    timeToReach[neighbor.Item1, neighbor.Item2] = nextTime;
                    nextTile[neighbor.Item1, neighbor.Item2] = current;
                    nextTarget[neighbor.Item1, neighbor.Item2] = propagatedNextTarget;
                    queue.Enqueue(neighbor, (nextDamage, nextTime));
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Target discovery and meetup
    // -------------------------------------------------------------------------

    private void UpdateDiscoveredTargets(Monster monster)
    {
        (int, int) currentTile = monster.TilePosition;
        int visionRange = GetConfig(monster).VisionRange;
        foreach (var tile in MapUtils.GetSurroundingCircle(currentTile, 0, visionRange))
        {
            Entity structure = MapManager.GetStructure(tile);
            if (structure == null || structure.IsDestroyed || !monster.CanTarget(structure))
                continue;
            if (_discoveredTargets.Add(tile))
            {
                // New target discovered — paths are stale for all types
                foreach (var d in _typeData.Values)
                    d.PathDirty = true;
                UpdateMeetingPoint(monster, tile);
            }
        }
    }

    // Picks the least-defended tile on the path to the newly discovered target as the meeting point for this monster type.
    private void UpdateMeetingPoint(Monster monster, (int, int) discoveredTarget)
    {
        var config = GetConfig(monster);
        if (!config.MeetUpBeforeAttacking || config.MeetingPoint.HasValue) return;

        var data = GetOrCreateTypeData(monster.Type);
        if (data.DefenceDirty) ComputeTileCosts(monster.Type, data);

        int cols = MapManager.Columns, rows = MapManager.Rows;
        var costToGetThere = new int[cols, rows];
        var nextTileBuf = new (int col, int row)?[cols, rows];
        var nextTargetBuf = new (int col, int row)?[cols, rows];

        CalculateWeakestPaths(
            new List<(int, int)> { discoveredTarget },
            data.TileCostAttackPhase, monster,
            costToGetThere, nextTileBuf, nextTargetBuf,
            allowThroughBreakableStructures: true);

        int minDef = int.MaxValue;
        int minCost = int.MaxValue;
        for (int col = 0; col < cols; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                if (costToGetThere[col, row] == int.MaxValue) continue;
                int def = data.TileCostAttackPhase[col, row];
                if (def < minDef || (def == minDef && costToGetThere[col, row] < minCost))
                {
                    minDef = def;
                    minCost = costToGetThere[col, row];
                    config.MeetingPoint = (col, row);
                }
            }
        }
        data.PathDirty = true;
    }

    // Clears the meeting point and triggers path recomputation once a majority (>50%) of monsters of this type have arrived, releasing them to attack together.
    private void AttackTogetherIfMeetingPointFull(Monster monster)
    {
        var config = GetConfig(monster);
        if (!config.MeetUpBeforeAttacking || !config.MeetingPoint.HasValue || config.MeetingPoint != monster.TilePosition)
            return;

        int totalMonsters = 0;
        for (int col = 0; col < MapManager.Columns; col++)
            for (int row = 0; row < MapManager.Rows; row++)
                foreach (Entity e in MapManager.GetAllEntities((col, row)))
                    if (e is Monster m && m.Type == monster.Type)
                        totalMonsters++;

        int arrivedCount = 0;
        (int col, int row) meetingPoint = config.MeetingPoint.Value;
        foreach (Entity e in MapManager.GetAllEntities(meetingPoint))
            if (e is Monster m && m.Type == monster.Type)
                arrivedCount++;

        if (arrivedCount * 2 + 1 < totalMonsters) return; // majority not yet arrived

        config.MeetingPoint = null;
        GetOrCreateTypeData(monster.Type).PathDirty = true;
    }

    // -------------------------------------------------------------------------
    // Movement safety
    // -------------------------------------------------------------------------

    // Makes sure the monster stops to attack from the neighboring tile and avoids stepping onto big monsters
    private (int, int) GetNextPositionIfPossible(Monster monster, (int, int) currentTile, (int, int) nextTile)
    {
        (int, int)? intendedTarget = GetTarget(monster);
        bool isIntendedTarget = intendedTarget.HasValue && intendedTarget.Value == nextTile;
        if (isIntendedTarget)
        {
            PushToRecentPath(monster, currentTile);
            if (_debugPrintsEnabled) Log($"GetNextPositionIfPossible returned {currentTile} (intended target)", monster);
            return currentTile;
        }

        bool nextTileHasMonster = false;
        bool nextTileHasBigMonster = false;
        foreach (Entity e in MapManager.GetAllEntities(nextTile))
        {
            if (e is Monster m)
            {
                nextTileHasMonster = true;
                if (m is BigMonster) { nextTileHasBigMonster = true; break; }
            }
        }
        if (nextTileHasBigMonster || (monster is BigMonster && nextTileHasMonster))
        {
            if (IsStationary(monster))
            {
                PushToRecentPath(monster, nextTile);
                if (_debugPrintsEnabled) Log($"GetNextPositionIfPossible pushed through to {nextTile} (stationary)", monster);
                return nextTile;
            }
            PushToRecentPath(monster, currentTile);
            if (_debugPrintsEnabled) Log($"GetNextPositionIfPossible returned {currentTile} (big monster block)", monster);
            return currentTile;
        }

        PushToRecentPath(monster, nextTile);
        if (_debugPrintsEnabled) Log($"GetNextPositionIfPossible returned {nextTile}", monster);
        return nextTile;
    }

    // Returns a random neighboring tile, that the monster can move to, for discovery-phase wandering.
    private (int col, int row) GetRandomNeighbor((int col, int row) startTile, Monster monster)
    {
        var candidates = new List<(int col, int row)>();
        foreach (var neighbor in MapUtils.GetSurroundingTiles(startTile))
        {
            Entity structure = MapManager.GetStructure(neighbor);
            if (monster.CanStepOn(structure) )
                candidates.Add(neighbor);
        }
        return candidates.Count == 0 ? startTile : candidates[GamelabGame.Rng.Next(candidates.Count)];
    }

    private void PushToRecentPath(Monster monster, (int col, int row) tile)
    {
        if (!_monsterToRecentPath.TryGetValue(monster, out var queue))
        {
            queue = new Queue<(int col, int row)>();
            _monsterToRecentPath[monster] = queue;
        }
        queue.Enqueue(tile);
        if (queue.Count > StationaryCheckLength)
            queue.Dequeue();
    }

    private bool IsStationary(Monster monster)
    {
        if (_monsterToRecentPath.TryGetValue(monster, out var queue) && queue.Count > 0)
        {
            var first = queue.Peek();
            foreach (var tile in queue)
                if (tile != first) return false;
            return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Static predicates
    // -------------------------------------------------------------------------

    private static bool IsPrimaryTarget(Entity target)
    {
        return target is Tent || (target is Meadow meadow && meadow.HasSheep && !meadow.IsPolymorphed);
    }

    private static bool IsSecondaryTarget(Monster monster, Entity target)
    {
        return monster.CanTarget(target) && !IsPrimaryTarget(target);
    }

    // -------------------------------------------------------------------------
    // Diagnostics
    // -------------------------------------------------------------------------

    private void LogProfilingMetrics()
    {
        if (_logTimer.ElapsedMilliseconds > 1000)
        {
            double total = _timeComputeDefence + _timeComputePaths + _timeGetNext;
            double elapsed = _logTimer.ElapsedMilliseconds;
            Log($"\n--- PathFinding Profiling ({_callCount} calls over {elapsed / 1000.0:F1}s) ---");
            Log($"Total Time:      {total:F2} ms ({total / elapsed * 100:F2}% of elapsed)");
            Log($"ComputeDefence:  {_timeComputeDefence:F2} ms ({(total > 0 ? _timeComputeDefence / total * 100 : 0):F1}%)");
            Log($"ComputePaths:    {_timeComputePaths:F2} ms ({(total > 0 ? _timeComputePaths / total * 100 : 0):F1}%)");
            Log($"GetNext:         {_timeGetNext:F2} ms ({(total > 0 ? _timeGetNext / total * 100 : 0):F1}%)");
            _timeComputeDefence = 0; _timeComputePaths = 0; _timeGetNext = 0; _callCount = 0;
            _logTimer.Restart();
        }
    }

    private static double ElapsedMs(long start) =>
        (System.Diagnostics.Stopwatch.GetTimestamp() - start) / _swFreqMs;

    private void Log(string message, Monster monster = null)
    {
        if (monster == null)
            System.Console.WriteLine($"[{DateTime.Now:HH:mm:ss::ff}] {message}");
        else
            System.Console.WriteLine($"[{DateTime.Now:HH:mm:ss::ff}] [{monster.Type}] {message}");
    }
}
