/// <summary>
/// Attracts Monsters
/// </summary>
public class Decoy : Structure
{
    public Decoy((int,int) pos)
        : base("decoy_idle", pos)
    {
    }

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}
