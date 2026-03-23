using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DreamWardUtils.Graphics;
using Gamelab;

namespace Tests;

public class PathFindingManagerTests
{
    private const string TentAnim = "tent_idle";
    private const string SheepAnim = "sheepSmall_idle";
    private const string WallAnim = "wall_idle";
    private const string TowerAnim = "towerSniper_idle";
    private const string MonsterAnim = "normal_walkSW";

    private PathFindingManager _pm;

    [SetUp]
    public void Setup()
    {
        var mapManager = CreateTestMapManager();
        SetStaticProperty(typeof(GamelabGame), "MapManager", mapManager);
        EnsureTestTextureAtlases();
        _pm = new PathFindingManager();
    }

    private static MapManager CreateTestMapManager(int columns = 8, int rows = 8)
    {
        var tileSet = new TileSet(new TextureRegion(), 1, 1);
        return new MapManager(tileSet, columns, rows, TimeSpan.FromSeconds(1));
    }

    private static IEnumerable<TestCaseData> DistanceTestCases()
    {
        // Format: new TestCaseData( (startCol, startRow), (endCol, endRow) ).Returns(expected).SetName("...");

        // --- BASE CASE ---
        yield return new TestCaseData((0, 0), (0, 0)).Returns(0).SetName("Same Tile");

        // --- ODD COLUMN NEIGHBORS (Center: 1, 1) ---
        yield return new TestCaseData((1, 1), (1, 0)).Returns(1).SetName("Odd Col: Top Neighbor");
        yield return new TestCaseData((1, 1), (1, 2)).Returns(1).SetName("Odd Col: Bottom Neighbor");
        yield return new TestCaseData((1, 1), (0, 1)).Returns(1).SetName("Odd Col: Top-Left Neighbor");
        yield return new TestCaseData((1, 1), (2, 1)).Returns(1).SetName("Odd Col: Top-Right Neighbor");
        yield return new TestCaseData((1, 1), (0, 2)).Returns(1).SetName("Odd Col: Bottom-Left Neighbor");
        yield return new TestCaseData((1, 1), (2, 2)).Returns(1).SetName("Odd Col: Bottom-Right Neighbor");

        // --- EVEN COLUMN NEIGHBORS (Center: 2, 1) ---
        yield return new TestCaseData((2, 1), (2, 0)).Returns(1).SetName("Even Col: Top Neighbor");
        yield return new TestCaseData((2, 1), (2, 2)).Returns(1).SetName("Even Col: Bottom Neighbor");
        yield return new TestCaseData((2, 1), (1, 0)).Returns(1).SetName("Even Col: Top-Left Neighbor");
        yield return new TestCaseData((2, 1), (3, 0)).Returns(1).SetName("Even Col: Top-Right Neighbor");
        yield return new TestCaseData((2, 1), (1, 1)).Returns(1).SetName("Even Col: Bottom-Left Neighbor");
        yield return new TestCaseData((2, 1), (3, 1)).Returns(1).SetName("Even Col: Bottom-Right Neighbor");

        // --- LONGER DISTANCES ---
        yield return new TestCaseData((0, 3), (3, 0)).Returns(4).SetName("Long Diagonal (From Image)");

        // Pure Vertical (Straight down the same column)
        yield return new TestCaseData((0, 0), (0, 5)).Returns(5).SetName("Vertical Line Down");
        yield return new TestCaseData((1, 8), (1, 1)).Returns(7).SetName("Vertical Line Up");

        // Pure Horizontal (Zig-zagging across columns)
        yield return new TestCaseData((0, 0), (4, 0)).Returns(4).SetName("Horizontal Zig-Zag Right");
        yield return new TestCaseData((5, 2), (0, 2)).Returns(5).SetName("Horizontal Zig-Zag Left");

        // Long Diagonals
        yield return new TestCaseData((0, 0), (5, 5)).Returns(8).SetName("Long Diagonal Down-Right");
        yield return new TestCaseData((6, 6), (1, 2)).Returns(6).SetName("Long Diagonal Up-Left");

        // Stress Tests & Extreme Coordinates
        yield return new TestCaseData((0, 0), (100, 100)).Returns(150).SetName("Large Map Extreme Distance");

        // Reversibility (A -> B must equal B -> A)
        yield return new TestCaseData((100, 100), (0, 0)).Returns(150).SetName("Reversibility: Large Map Extreme Distance");
        yield return new TestCaseData((3, 0), (0, 3)).Returns(4).SetName("Reversibility: Long Diagonal");
    }

    [TestCaseSource(nameof(DistanceTestCases))]
    public int CalculateTileDistance_ReturnsCorrectValue((int col, int row) start, (int col, int row) target)
    {
        return _pm.CalculateTileDistance(start.col, start.row, target.col, target.row);
    }

    [Test]
    public void CalculatePath_EndToEnd_NoTargets_ReturnsStayPutPath()
    {
        var scenario = new PathFindingScenario();

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        Assert.That(path, Is.EqualTo(new List<(int, int)> { (0, 0) }));
    }

    [Test]
    public void CalculatePath_EndToEnd_OnlySecondaryTarget_GoesToSecondary()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceWall((4, 4));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (4, 4));
    }

    [Test]
    public void CalculatePath_EndToEnd_NoDefense_PrefersPrimaryTarget()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 2));
        scenario.PlaceWall((4, 4));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (2, 2));
    }

    [Test]
    public void CalculatePath_EndToEnd_MultiplePrimaryTargets_ChoosesCloserPrimary()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((1, 0));
        scenario.PlaceSheep((5, 5));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (1, 0));
    }

    [Test]
    public void CalculatePath_EndToEnd_PrimaryDefended_ChoosesZeroDamageSecondary()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 2));
        scenario.PlaceWall((5, 5));
        scenario.PlaceTower((2, 1));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (5, 5));
    }

    [Test]
    public void CalculatePath_EndToEnd_OnlyDefendedPrimary_StillTargetsPrimary()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 2));
        scenario.PlaceTower((2, 1));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (2, 2));
        Assert.That(path.Count, Is.GreaterThan(1));
    }



    [Test]
    public void CalculatePath_EndToEnd_PrimaryPreferredEvenWhenSecondaryIsCloser()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((6, 6));
        scenario.PlaceWall((1, 0));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (6, 6));
    }

    [Test]
    public void CalculatePath_EndToEnd_MultipleSecondaryTargets_ChoosesCloserSecondary()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceWall((1, 0));
        scenario.PlaceWall((6, 6));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (1, 0));
    }



    [Test]
    public void CalculatePath_EndToEnd_BetweenTwoPrimaryTargets_PrefersLowerDefenseTarget()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 3));
        scenario.PlaceSheep((4, 3));

        scenario.PlaceTower((2, 2));

        scenario.PlaceTower((4, 2));
        scenario.PlaceTower((5, 3));

        List<(int, int)> path = scenario.CalculatePath((3, 3));

        AssertValidPath(path, (3, 3), (2, 3));
    }

    [Test]
    public void CalculatePath_EndToEnd_TargetSurroundedByWallsExceptBottom_FromTopSide_FindsPath()
    {
        var scenario = new PathFindingScenario();

        scenario.PlaceTent((3, 3));

        // Surround target on all sides except bottom side.
        scenario.PlaceWall((2, 3));
        scenario.PlaceWall((2, 4));
        scenario.PlaceWall((3, 2));
        scenario.PlaceWall((4, 3));
        scenario.PlaceWall((4, 4));

        List<(int, int)> path = scenario.CalculatePath((3, 1));

        AssertValidPath(path, (3, 1), (3, 3));
    }

    [Test]
    public void CalculatePath_EndToEnd_PathAlwaysStaysInsideMapBounds()
    {
        var scenario = new PathFindingScenario(10, 10);
        scenario.PlaceTent((8, 8));
        scenario.PlaceTower((7, 8));
        scenario.PlaceTower((8, 7));
        scenario.PlaceTower((7, 7));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        Assert.That(path.Count, Is.GreaterThan(0));
        foreach (var tile in path)
        {
            Assert.That(tile.Item1, Is.GreaterThanOrEqualTo(0).And.LessThan(10));
            Assert.That(tile.Item2, Is.GreaterThanOrEqualTo(0).And.LessThan(10));
        }
    }


    [Test]
    public void CalculatePath_EndToEnd_RepeatedCallsKeepTargetStable()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 2));
        scenario.PlaceWall((6, 6));

        List<(int, int)> firstPath = scenario.CalculatePath((0, 0));
        List<(int, int)> secondPath = scenario.CalculatePath((0, 0));

        AssertValidPath(firstPath, (0, 0), (2, 2));
        AssertValidPath(secondPath, (0, 0), (2, 2));
        Assert.That(firstPath, Is.EqualTo(secondPath));
    }

    [Test]
    public void CalculatePath_EndToEnd_FlyingMonster_WithOnlyWallTile_TargetsNothing()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceWallTileStub((1, 2));

        List<(int, int)> path = scenario.CalculatePath((0, 2), MonsterType.Flying);

        Assert.That(path, Is.EqualTo(new List<(int, int)> { (0, 2) }));
    }

    [Test]
    public void CalculatePath_EndToEnd_InvalidActiveTargetEntry_IsSkipped()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 2));
        scenario.AddActiveTarget((-1, -1, 5));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (2, 2));
    }

    [Test]
    public void ReconstructPath_InvalidEndTile_ReturnsEmptyPath()
    {
        var scenario = new PathFindingScenario();
        (int col, int row)?[,] previousTile = new (int, int)?[scenario.MapManager.Columns, scenario.MapManager.Rows];
        int[,] timeToReach = new int[scenario.MapManager.Columns, scenario.MapManager.Rows];

        List<(int, int)> path = InvokeReconstructPath(
            scenario.PathFindingManager,
            (0, 0),
            (-1, 0),
            previousTile,
            timeToReach);

        Assert.That(path, Is.Empty);
    }

    [Test]
    public void ReconstructPath_MissingPreviousLink_ReturnsEmptyPath()
    {
        var scenario = new PathFindingScenario();
        (int col, int row)?[,] previousTile = new (int, int)?[scenario.MapManager.Columns, scenario.MapManager.Rows];
        int[,] timeToReach = new int[scenario.MapManager.Columns, scenario.MapManager.Rows];

        timeToReach[1, 0] = 1;

        List<(int, int)> path = InvokeReconstructPath(
            scenario.PathFindingManager,
            (0, 0),
            (1, 0),
            previousTile,
            timeToReach);

        Assert.That(path, Is.Empty);
    }

    [Test]
    public void CalculatePath_EndToEnd_GroupMonsters_GangUpOnMiddleActiveTarget()
    {
        var scenario = new PathFindingScenario(12, 8);

        // Middle target (closest for the left monster).
        scenario.PlaceTent((3, 3));
        var middleDefender = scenario.PlaceTower((3, 4));

        // Right-side targets, each two tiles to the right of the group monsters.
        scenario.PlaceSheep((9, 2));
        scenario.PlaceSheep((9, 3));
        scenario.PlaceSheep((9, 4));
        var rightDefender1 = scenario.PlaceTower((10, 2));
        var rightDefender2 = scenario.PlaceTower((10, 3));
        var rightDefender3 = scenario.PlaceTower((10, 4));

        // First monster on the left picks the middle target and registers it as active.
        List<(int, int)> leftMonsterPath = scenario.CalculatePath((0, 3));

        // Group monsters are farther from the middle but should coordinate and gang up on it.
        List<(int, int)> groupPath1 = scenario.CalculatePath((7, 2));
        List<(int, int)> groupPath2 = scenario.CalculatePath((7, 3));
        List<(int, int)> groupPath3 = scenario.CalculatePath((7, 4));

        AssertValidPath(leftMonsterPath, (0, 3), (3, 3));

        AssertValidPath(groupPath1, (7, 2), (3, 3));
        AssertValidPath(groupPath2, (7, 3), (3, 3));
        AssertValidPath(groupPath3, (7, 4), (3, 3));

        Assert.That(groupPath1[^1], Is.Not.EqualTo((9, 2)));
        Assert.That(groupPath2[^1], Is.Not.EqualTo((9, 3)));
        Assert.That(groupPath3[^1], Is.Not.EqualTo((9, 4)));
    }

    private static void AssertValidPath(List<(int, int)> path, (int, int) expectedStart, (int, int) expectedEnd)
    {
        Assert.That(path, Is.Not.Null);
        Assert.That(path.Count, Is.GreaterThan(0));
        Assert.That(path[0], Is.EqualTo(expectedStart));
        Assert.That(path[^1], Is.EqualTo(expectedEnd));

        for (int i = 1; i < path.Count; i++)
        {
            var previous = path[i - 1];
            var current = path[i];
            if (previous == current)
            {
                continue;
            }

            var validNeighbors = Tower.GetHexNeighbors(previous);
            Assert.That(validNeighbors.Contains(current), Is.True,
                $"Invalid step from {previous} to {current} at index {i}.");
        }
    }

    private static List<(int, int)> InvokeReconstructPath(
        PathFindingManager manager,
        (int col, int row) startTile,
        (int col, int row) endTile,
        (int col, int row)?[,] previousTile,
        int[,] timeToReach)
    {
        MethodInfo method = typeof(PathFindingManager)
            .GetMethod("ReconstructPath", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (List<(int, int)>)method.Invoke(manager, [startTile, endTile, previousTile, timeToReach])!;
    }

    private static T CreateUninitializedEntity<T>(int health = 100) where T : Entity
    {
        T entity = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        FieldInfo healthField = typeof(Entity).GetField("_currentHealth", BindingFlags.Instance | BindingFlags.NonPublic)!;
        healthField.SetValue(entity, health);

        return entity;
    }

    private static void EnsureTestTextureAtlases()
    {
        if (GamelabGame.textureAtlas != null) return;

        var atlas = new TextureAtlas();
        var animation = new Animation(new List<TextureRegion> { new TextureRegion() }, TimeSpan.FromMilliseconds(100));
        atlas.AddAnimation(TentAnim, animation);
        atlas.AddAnimation(SheepAnim, animation);
        atlas.AddAnimation(WallAnim, animation);
        atlas.AddAnimation(TowerAnim, animation);
        atlas.AddAnimation(MonsterAnim, animation);

        GamelabGame.textureAtlas = new Dictionary<string, TextureAtlas>
        {
            ["day_top"] = atlas,
            ["night_top"] = atlas,
            ["monsters"] = atlas
        };
    }

    private static void SetStaticProperty(Type type, string propertyName, object value)
    {
        var backingField = type.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        backingField!.SetValue(null, value);
    }

    private sealed class PathFindingScenario
    {
        public MapManager MapManager { get; }
        public PathFindingManager PathFindingManager { get; }

        public PathFindingScenario(int columns = 8, int rows = 8)
        {
            MapManager = CreateTestMapManager(columns, rows);
            SetStaticProperty(typeof(GamelabGame), "MapManager", MapManager);
            EnsureTestTextureAtlases();
            PathFindingManager = new PathFindingManager();
        }

        public Tent PlaceTent((int col, int row) tile)
        {
            var tent = new Tent(TentAnim, MapManager, tile);
            Assert.That(MapManager.PlaceEntity(tent, tile.col, tile.row), Is.True);
            return tent;
        }

        public Sheep PlaceSheep((int col, int row) tile)
        {
            var sheep = new Sheep(SheepAnim, MapManager, tile);
            Assert.That(MapManager.PlaceEntity(sheep, tile.col, tile.row), Is.True);
            return sheep;
        }

        public Wall PlaceWall((int col, int row) tile)
        {
            var wall = new Wall(WallAnim, MapManager, tile);
            Assert.That(MapManager.PlaceEntity(wall, tile.col, tile.row), Is.True);
            return wall;
        }

        public Tower PlaceTower((int col, int row) tile)
        {
            var tower = new Tower(TowerAnim, MapManager, tile);
            Assert.That(MapManager.PlaceEntity(tower, tile.col, tile.row), Is.True);
            return tower;
        }

        public WallTile PlaceWallTileStub((int col, int row) tile, int health = 100)
        {
            var wallTile = CreateUninitializedEntity<WallTile>(health);
            Assert.That(MapManager.PlaceEntity(wallTile, tile.col, tile.row), Is.True);
            return wallTile;
        }

        public void AddActiveTarget((int col, int row, int timeToBeAttacked) activeTarget)
        {
            FieldInfo activeTargetsField = typeof(PathFindingManager)
                .GetField("_activeTargets", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var activeTargets = (List<(int col, int row, int timeToBeAttacked, int monsterCount)>)activeTargetsField.GetValue(PathFindingManager)!;
            activeTargets.Add((activeTarget.col, activeTarget.row, activeTarget.timeToBeAttacked, 0));
        }

        public List<(int, int)> CalculatePath((int col, int row) start, MonsterType type = MonsterType.Regular)
        {
            var monster = new Monster(MonsterAnim, MapManager, start, start, type);
            return PathFindingManager.CalculatePath(monster);
        }
    }

    [Test]
    public void CalculatePath_RepeatedForSameMonster_DoesNotGrowTrackingCollections()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 2));

        var monster = new Monster(MonsterAnim, scenario.MapManager, (0, 0), (0, 0), MonsterType.Regular);

        for (int i = 0; i < 100; i++)
        {
            List<(int, int)> path = scenario.PathFindingManager.CalculatePath(monster);
            AssertValidPath(path, (0, 0), (2, 2));
        }

        FieldInfo activeTargetsField = typeof(PathFindingManager)
            .GetField("_activeTargets", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var activeTargets = (List<(int col, int row, int timeToBeAttacked, int monsterCount)>)activeTargetsField.GetValue(scenario.PathFindingManager)!;

        FieldInfo monstersTargetingField = typeof(PathFindingManager)
            .GetField("_monstersTargeting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var monstersTargeting = (Dictionary<(int col, int row), HashSet<Monster>>)monstersTargetingField.GetValue(scenario.PathFindingManager)!;

        FieldInfo monsterToTargetField = typeof(PathFindingManager)
            .GetField("_monsterToTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var monsterToTarget = (Dictionary<Monster, (int col, int row)>)monsterToTargetField.GetValue(scenario.PathFindingManager)!;

        Assert.That(activeTargets.Count, Is.LessThanOrEqualTo(1));
        Assert.That(monstersTargeting.Count, Is.LessThanOrEqualTo(1));
        Assert.That(monsterToTarget.Count, Is.EqualTo(1));

        if (activeTargets.Count == 1)
        {
            Assert.That(activeTargets[0].monsterCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void CalculatePath_RepeatedForSameMonster_WithNewTargetBetweenCalls_ReplacesTrackingWithoutGrowth()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((6, 6));

        var monster = new Monster(MonsterAnim, scenario.MapManager, (0, 0), (0, 0), MonsterType.Regular);

        List<(int, int)> firstPath = scenario.PathFindingManager.CalculatePath(monster);
        AssertValidPath(firstPath, (0, 0), (6, 6));

        scenario.PlaceSheep((1, 0));

        List<(int, int)> secondPath = scenario.PathFindingManager.CalculatePath(monster);
        AssertValidPath(secondPath, (0, 0), (1, 0));

        FieldInfo activeTargetsField = typeof(PathFindingManager)
            .GetField("_activeTargets", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var activeTargets = (List<(int col, int row, int timeToBeAttacked, int monsterCount)>)activeTargetsField.GetValue(scenario.PathFindingManager)!;

        FieldInfo monstersTargetingField = typeof(PathFindingManager)
            .GetField("_monstersTargeting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var monstersTargeting = (Dictionary<(int col, int row), HashSet<Monster>>)monstersTargetingField.GetValue(scenario.PathFindingManager)!;

        FieldInfo monsterToTargetField = typeof(PathFindingManager)
            .GetField("_monsterToTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var monsterToTarget = (Dictionary<Monster, (int col, int row)>)monsterToTargetField.GetValue(scenario.PathFindingManager)!;

        Assert.That(activeTargets.Count, Is.EqualTo(1));
        Assert.That(activeTargets[0].col, Is.EqualTo(1));
        Assert.That(activeTargets[0].row, Is.EqualTo(0));
        Assert.That(activeTargets[0].monsterCount, Is.EqualTo(1));

        Assert.That(monstersTargeting.Count, Is.EqualTo(1));
        Assert.That(monstersTargeting.ContainsKey((1, 0)), Is.True);
        Assert.That(monstersTargeting.ContainsKey((6, 6)), Is.False);
        Assert.That(monstersTargeting[(1, 0)].Contains(monster), Is.True);

        Assert.That(monsterToTarget.Count, Is.EqualTo(1));
        Assert.That(monsterToTarget[monster], Is.EqualTo((1, 0)));
    }

    [Test]
    [Category("Performance")]
    public void CalculatePath_PerformanceSmoke_1000Iterations_NotInsanelySlow()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((2, 2));
        scenario.PlaceTower((2, 1));

        var monster = new Monster(MonsterAnim, scenario.MapManager, (0, 0), (0, 0), MonsterType.Regular);

        const int warmupIterations = 20;
        for (int i = 0; i < warmupIterations; i++)
        {
            _ = scenario.PathFindingManager.CalculatePath(monster);
        }

        const int measuredIterations = 1000;
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < measuredIterations; i++)
        {
            _ = scenario.PathFindingManager.CalculatePath(monster);
        }
        stopwatch.Stop();

        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)),
            $"Pathfinding performance smoke test exceeded threshold: {stopwatch.Elapsed.TotalMilliseconds:F1} ms for {measuredIterations} runs.");
    }
}