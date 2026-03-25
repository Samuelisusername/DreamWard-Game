using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

public static class MapUtils
{
    public static int Columns {get; private set;}
    public static int Rows {get; private set;}

    public static void Setup(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public static List<(int,int)> getSurroundingTiles(int col, int row)
    {
        List<(int,int)> res;
        if(col %2 == 0)
        {
            res =  new List<(int, int)>{(col,row-1),(col,row+1), (col-1,row), (col-1, row-1),(col+1,row), (col+1, row-1)};
        }
        else
        {
            res  = new List<(int, int)>{(col,row-1),(col,row+1), (col-1,row), (col-1, row+1),(col+1,row), (col+1, row+1)};
        }
        return res.Where(i => i .Item1 >= 0 && i.Item1 < Columns && i.Item2 >= 0 && i.Item2 < Rows).ToList();
    }

    public static string GetDirection((int, int) from, (int,int) to)
    {
        string WE = from.Item1 > to.Item1 ? "W" : (from.Item1 == to.Item1 ? "" : "E");
        string NS = from.Item2 > to.Item2 ? "N" : (from.Item2 < to.Item2 ? "S" : (from.Item1 % 2 == 0 ? "S" : "N"));

        return NS + WE;
    }
}