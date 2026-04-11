/// <summary>
/// Attracts Monsters
/// </summary>
public class Decoy : Structure
{
    public override bool IsFlat => false;
    public Decoy(string animName, (int,int) pos, int health = 20)
        : base(animName, pos, health)
    {
    }
}
