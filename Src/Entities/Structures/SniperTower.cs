public class SniperTower : Tower
{
    public SniperTower((int, int) pos, int health = 8) : base("towerSniper_idle", pos, health)
    {
    }

    protected override float damagePerSecond => 8f;

    protected override int rangeDepth => 1;
}