public class SniperTower : Tower
{
    public SniperTower((int, int) pos) : base("towerSniper_idle", pos)
    {
    }

    // protected override float damagePerSecond => 8f;

    // protected override int rangeDepth => 2;

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}