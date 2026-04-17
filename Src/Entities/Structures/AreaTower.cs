

/// <summary>
/// Tower with depth-2 hex range (own tile + immediate neighbors + their neighbors).
/// </summary>
public class AreaTower : Tower
{
    public AreaTower((int,int) pos, int health = 7) : base("towerAOE_idle", pos, health)
    {
    }

    protected override float damagePerSecond => 5f;

    protected override int rangeDepth => 1;
}