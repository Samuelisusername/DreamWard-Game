/// <summary>
/// A tree tile entity. Cannot be attacked, stepped on, or built over.
/// Purely decorative obstacle that blocks movement and building.
/// </summary>
public class TreeTile : Structure
{
    public override bool CanMove => false;
    public TreeTile(string animName, (int,int) pos)
        : base(animName, pos, int.MaxValue)
    {
    }
}
