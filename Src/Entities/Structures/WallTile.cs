/// <summary>
/// A wall tile entity. Can be attacked and stepped on.
/// Regular monsters must attack it first; flying monsters ignore it.
/// </summary>
public class WallTile : Structure
{
    public WallTile(string animName, (int,int) pos)
        : base(animName, pos)
    {
    }

    
    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}
