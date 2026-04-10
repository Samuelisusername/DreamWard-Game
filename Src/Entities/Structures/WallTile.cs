/// <summary>
/// A wall tile entity. Can be attacked and stepped on.
/// Regular monsters must attack it first; flying monsters ignore it.
/// </summary>
public class WallTile : Structure
{
    public override bool IsFlat => false;
    public WallTile(string animName, (int,int) pos, int health = 50)
        : base(animName, pos, health)
    {
    }
}
