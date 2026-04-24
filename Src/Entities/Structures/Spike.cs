/// <summary>
///  Inflicts Damage on Ground Monsters
/// </summary>
public class Spike : Tower
{

    public Spike((int,int) pos) : base("spikes_idle", pos){}
    // protected override float damagePerSecond => 2f;

    // protected override int rangeDepth => 0;
    protected override (string, string)[] infos => [("Health:", $"Yes"), ("DPS:", $"{damagePerSecond}"), ("Total Damage Dealt:", $"{(int)totalDamage}")];
}