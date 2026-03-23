/// <summary>
/// A tree tile entity. Cannot be attacked, stepped on, or built over.
/// Purely decorative obstacle that blocks movement and building.
/// </summary>
public class TreeTile : Entity
{
    public TreeTile(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, int.MaxValue, true)
    {
    }
}
