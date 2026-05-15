/// <summary>
/// Attracts Monsters
/// </summary>
public class Decoy : Structure
{
    public Decoy((int,int) pos)
        : base("decoy_idle", pos)
    {
    }

    public override (string, string)[] Infos => [("Health:", $"{CurrentHealth}")];
}
