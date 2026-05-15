/// <summary>
/// A void tile entity. Cannot be attacked or stepped on (except by flying monsters).
/// </summary>
public class VoidTile : Structure
{
    public override bool CanMove => false;
    public VoidTile((int,int) pos)
        : base("void_idle", pos)
    {
    }
    
    public override (string, string)[] Infos => [("Health:", "No")];
}
