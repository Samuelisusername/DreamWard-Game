/// <summary>
/// Attracts Monsters
/// </summary>
public class Decoy : Structure
{
    public Decoy(string animName, (int,int) pos, int health = 20)
        : base(animName, pos, health)
    {
    }
}
