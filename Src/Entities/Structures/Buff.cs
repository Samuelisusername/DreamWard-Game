/// <summary>
/// Repels Monsters
/// </summary>
public class Buff : Structure
{
    public Buff((int,int) pos, int health = 20)
        : base("buff_idle", pos, health)
    {
    }
}
