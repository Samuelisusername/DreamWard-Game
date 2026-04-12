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
        return new MapManager(tileSet, tileSet, columns, rows);
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
        return MapUtils.CalculateTileDistance(start, target);
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
    public void CalculatePath_EndToEnd_MultiplePrimaryTargets_ChoosesCloserPrimary()
    {
        var scenario = new PathFindingScenario();
        scenario.PlaceTent((1, 0));
        scenario.PlaceSheep((5, 5));

        List<(int, int)> path = scenario.CalculatePath((0, 0));

        AssertValidPath(path, (0, 0), (1, 0));
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

        PropertyInfo healthProperty = typeof(Entity).GetProperty("CurrentHealth", BindingFlags.Instance | BindingFlags.Public);
        healthProperty.GetSetMethod(nonPublic: true)?.Invoke(entity, [health]);

        return entity;
    }

    private static T CreateUninitializedMonster<T>((int col, int row) startPos, int health = 100) where T : Monster
    {
        T entity = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        PropertyInfo healthProperty = typeof(Entity).GetProperty("CurrentHealth", BindingFlags.Instance | BindingFlags.Public);
        healthProperty.GetSetMethod(nonPublic: true)?.Invoke(entity, [health]);

        PropertyInfo posProperty = typeof(Entity).GetProperty("TilePosition", BindingFlags.Instance | BindingFlags.Public);
        posProperty.GetSetMethod()?.Invoke(entity, [startPos]);

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
            SetStaticProperty(typeof(GamelabGame), "SanityManager", new SanityManager());
            SetStaticProperty(typeof(GamelabGame), "GamePhaseManager", new GamePhaseManager());
            EnsureTestTextureAtlases();
            PathFindingManager = new PathFindingManager();
        }

        public Tent PlaceTent((int col, int row) tile)
        {
            var tent = new Tent(TentAnim, tile);
            Assert.That(MapManager.PlaceEntity(tent, tile), Is.True);
            return tent;
        }

        public Meadow PlaceSheep((int col, int row) tile)
        {
            var sheep = new Meadow(SheepAnim, tile, 10);
            Assert.That(MapManager.PlaceSheep(sheep, tile), Is.True);
            return sheep;
        }

        public Wall PlaceWall((int col, int row) tile)
        {
            var wall = new Wall(WallAnim, tile);
            Assert.That(MapManager.PlaceEntity(wall, tile), Is.True);
            return wall;
        }

        public Tower PlaceTower((int col, int row) tile)
        {
            var tower = new Tower(TowerAnim, tile);
            Assert.That(MapManager.PlaceEntity(tower, tile), Is.True);
            return tower;
        }

        public WallTile PlaceWallTileStub((int col, int row) tile, int health = 100)
        {
            var wallTile = CreateUninitializedEntity<WallTile>(health);
            Assert.That(MapManager.PlaceEntity(wallTile, tile), Is.True);
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
            switch (type)
            {
                case MonsterType.Regular:
                    var monster = CreateUninitializedMonster<RegularMonster>(start);
                    return PathFindingManager.CalculatePath(monster);
                case MonsterType.Flying:
                    var flyingMonster = CreateUninitializedMonster<FlyingMonster>(start);
                    return PathFindingManager.CalculatePath(flyingMonster);
                default:
                    return [];
            }
            
        }
    }

}