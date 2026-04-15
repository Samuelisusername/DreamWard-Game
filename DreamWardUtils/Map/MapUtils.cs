using System;
using System.Collections.Generic;
using System.Linq;

public static class MapUtils
{
    public static int Columns {get; private set;}
    public static int Rows {get; private set;}

    public static void Setup(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public static List<(int,int)> GetSurroundingTiles((int col, int row) pos, bool outOfBounds = false)
    {
        List<(int,int)> res;
        if(pos.col % 2 == 0)
        {
            res =  new List<(int, int)>{(pos.col, pos.row-1),(pos.col, pos.row+1), (pos.col-1,pos.row), (pos.col-1, pos.row-1),(pos.col+1,pos.row), (pos.col+1, pos.row-1)};
        }
        else
        {
            res  = new List<(int, int)>{(pos.col,pos.row-1),(pos.col,pos.row+1), (pos.col-1,pos.row), (pos.col-1, pos.row+1),(pos.col+1,pos.row), (pos.col+1, pos.row+1)};
        }
        return outOfBounds ? res : res.Where(i => i.Item1 >= 0 && i.Item1 < Columns && i.Item2 >= 0 && i.Item2 < Rows).ToList();
    }
    
    /// <summary>
    /// Gets all the tiles in the circle orround pos, from is inclusive distance, to is exclusive distance
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="from">inclusive</param>
    /// <param name="to"></param>
    /// <returns></returns>
    public static List<(int,int)> GetSurroundingCircle((int col, int row) pos, int from, int to)
    {
        List<(int,int)> res = new List<(int,int)>();
        List<(int,int)> frontier = new List<(int,int)> { pos };        
        res.Add(pos);

        for (int i = 0; i < to -1; i++)
        {
            List<(int,int)> nextFrontier = new List<(int,int)>();
            foreach (var tile in frontier)
            {
                foreach (var neighbor in GetSurroundingTiles(tile))
                {
                    if (IsInBounds(neighbor) && !res.Contains(neighbor))
                    {
                        res.Add(neighbor);
                        nextFrontier.Add(neighbor);
                    }
                }
            }
            frontier = nextFrontier;
        }

        return res.Where(i => CalculateTileDistance(i, pos) >= from).ToList();
    }

    public static string GetDirection((int, int) from, (int,int) to)
    {
        if(from == to) return "";
        string WE = from.Item1 > to.Item1 ? "W" : (from.Item1 == to.Item1 ? "" : "E");
        string NS = from.Item2 > to.Item2 ? "N" : (from.Item2 < to.Item2 ? "S" : (from.Item1 % 2 == 0 ? "S" : "N"));

        return NS + WE;
    }

    
    /// <summary>
    /// calculates the shortest distance (in number of tiles) between two tiles on the map.
    /// does not take into account possible obstacles on the map.
    /// </summary>
    /// <param name="pos1.col">the column of the first tile</param>
    /// <param name="pos2.col">the column of the second tile</param>
    /// <param name="pos1.row">the row of the first tile</param>
    /// <param name="pos2.row">the row of the second tile</param>
    public static int CalculateTileDistance((int col, int row) pos1, (int col, int row) pos2)
    {
        // Map to cube coordinates
        int q1 = pos1.col;
        int q2 = pos2.col;
        int r1 = pos1.row - (int)Math.Floor(pos1.col / 2.0);
        int r2 = pos2.row - (int)Math.Floor(pos2.col / 2.0);
        int s1 = -q1 - r1;
        int s2 = -q2 - r2;

        // The distance is the maximum absolute difference among the three axes
        int delta_q = Math.Abs(q1 - q2);
        int delta_r = Math.Abs(r1 - r2);
        int delta_s = Math.Abs(s1 - s2);
        return Math.Max(delta_q, Math.Max(delta_r, delta_s));
    }

    
    public static bool IsInBounds((int col, int row) tile)
    {
        return tile.col >= 0 && tile.col < Columns && tile.row >= 0 && tile.row < Rows;
    }
}