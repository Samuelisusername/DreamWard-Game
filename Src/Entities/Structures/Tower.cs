using System.Collections.Generic;

/// <summary>
/// Tower with depth-1 hex range (own tile + immediate neighbors).
/// Deals damage to monsters on tiles in range.
/// </summary>
public class Tower : Structure
{
    public override bool IsFlat => false;
    public float DamagePerSecond { get; set; }
    public int RangeDepth { get; protected set; }

    public Tower(string animName, (int,int) pos, float damagePerSecond = 3f, int health = 7)
        : base(animName, pos, health)
    {
        DamagePerSecond = damagePerSecond;
        RangeDepth = 2;
    }

    /// <summary>
    /// Returns all hex tiles within this tower's range (including its own tile).
    /// </summary>
    public List<(int,int)> GetTilesInRange()
    {
        var tiles = new HashSet<(int,int)>();
        tiles.Add(TilePosition);
        var frontier = new List<(int,int)> { TilePosition };

        for (int depth = 0; depth < RangeDepth; depth++)
        {
            var nextFrontier = new List<(int,int)>();
            foreach (var tile in frontier)
            {
                foreach (var neighbor in GetHexNeighbors(tile))
                {
                    if (neighbor.Item1 >= 0 && neighbor.Item1 < mapManager.Columns &&
                        neighbor.Item2 >= 0 && neighbor.Item2 < mapManager.Rows &&
                        tiles.Add(neighbor))
                    {
                        nextFrontier.Add(neighbor);
                    }
                }
            }
            frontier = nextFrontier;
        }

        return new List<(int,int)>(tiles);
    }

    /// <summary>
    /// Returns whether the given tile position is within this tower's attack range.
    /// </summary>
    public bool IsTileInRange((int,int) tilePos)
    {
        return GetTilesInRange().Contains(tilePos);
    }

    /// <summary>
    /// Returns the 6 hex neighbors for a given tile using the offset hex grid layout.
    /// </summary>
    public static (int,int)[] GetHexNeighbors((int,int) tile)
    {
        if (tile.Item1 % 2 == 0)
        {
            return new[]
            {
                (tile.Item1 - 1, tile.Item2 - 1),
                (tile.Item1 - 1, tile.Item2),
                (tile.Item1,     tile.Item2 - 1),
                (tile.Item1,     tile.Item2 + 1),
                (tile.Item1 + 1, tile.Item2 - 1),
                (tile.Item1 + 1, tile.Item2),
            };
        }
        else
        {
            return new[]
            {
                (tile.Item1 - 1, tile.Item2),
                (tile.Item1 - 1, tile.Item2 + 1),
                (tile.Item1,     tile.Item2 - 1),
                (tile.Item1,     tile.Item2 + 1),
                (tile.Item1 + 1, tile.Item2),
                (tile.Item1 + 1, tile.Item2 + 1),
            };
        }
    }
}

/// <summary>
/// Tower with depth-2 hex range (own tile + immediate neighbors + their neighbors).
/// </summary>
public class AreaTower : Tower
{
    public AreaTower(string animName, (int,int) pos,
                     float damagePerSecond = 2f, int health = 7)
        : base(animName, pos, damagePerSecond, health)
    {
        RangeDepth = 1;
    }
}
