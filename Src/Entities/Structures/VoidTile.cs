/// <summary>
/// A void tile entity. Cannot be attacked or stepped on (except by flying monsters).
/// </summary>
public class VoidTile : Structure
{
    public VoidTile(string animName, (int,int) pos)
        : base(animName, pos, int.MaxValue)
    {
    }
}
