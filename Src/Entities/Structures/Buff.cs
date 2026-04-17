/// <summary>
/// Repels Monsters
/// </summary>
public class Buff : Structure
{
    public Buff(string animName, (int,int) pos, int health = 20)
        : base(animName, pos, health)
    {
    }
}
