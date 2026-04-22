/// <summary>
/// A void tile entity. Cannot be attacked or stepped on (except by flying monsters).
/// </summary>
public class VoidTile : Structure
{
    public VoidTile((int,int) pos)
        : base("void_idle", pos, int.MaxValue)
    {
    }
    
    protected override (string, string)[] infos => [("Health:", "No")];
}
