using NUnit.Framework;
using System.Collections.Generic; // Required for IEnumerable

namespace Tests;

public class PathFindingManagerTests
{
    private PathFindingManager _pm;

    [SetUp]
    public void Setup()
    {
        _pm = new PathFindingManager();
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
}