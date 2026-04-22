/// <summary>
///  Inflicts Damage on Ground Monsters
/// </summary>
public class Spike : Tower
{

    public Spike((int,int) pos, int health = 50) : base("spikes_idle", pos, health){}
    protected override float damagePerSecond => 2f;

    protected override int rangeDepth => 0;
    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}