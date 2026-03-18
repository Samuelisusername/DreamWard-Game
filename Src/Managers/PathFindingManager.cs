using System;

public class PathFindingManager
{
    /// <summary>
    /// calculates the path of a Monster
    /// </summary>
    public /*Path"type"*/ void CalculatePath()
    {
        throw new NotFiniteNumberException();
    }

    /// <summary>
    /// calculates the shortest distance (in number of tiles) between two tiles on the map.
    /// does not take into account possible obstacles on the map.
    /// </summary>
    /// <param name="col1">the column of the first tile</param>
    /// <param name="col2">the column of the second tile</param>
    /// <param name="row1">the row of the first tile</param>
    /// <param name="row2">the row of the second tile</param>
    public int CalculateTileDistance(int col1, int row1, int col2, int row2)
    {
        // Map to cube coordinates
        int q1 = col1;
        int q2 = col2;
        int r1 = row1 - (int) Math.Floor(col1 / 2.0);
        int r2 = row2 - (int) Math.Floor(col2 / 2.0);
        int s1 = -q1 - r1;
        int s2 = -q2 - r2;

        // The distance is the maximum absolute difference among the three axes
        int delta_q = Math.Abs(q1 - q2);
        int delta_r = Math.Abs(r1 - r2);
        int delta_s = Math.Abs(s1 - s2);
        return Math.Max(delta_q, Math.Max(delta_r, delta_s));
    }

}