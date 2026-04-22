/// <summary>
/// Attracts Monsters
/// </summary>
public class Decoy : Structure
{
    public Decoy((int,int) pos, int health = 20)
        : base("decoy_idle", pos, health)
    {
    }

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}
