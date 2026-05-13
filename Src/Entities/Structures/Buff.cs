using Gamelab;

/// <summary>
/// Repels Monsters
/// </summary>
public class Buff : Structure
{
    public Buff((int,int) pos)
        : base("buff_idle", pos)
    {
    }

    public override (string, string)[] Infos => [("Health:", $"{CurrentHealth}")];
}
