/// <summary>
///  Inflicts Damage on Ground Monsters
/// </summary>
public class Spikes : Tower
{
    public override bool IsFlat => true;
    public Spikes(string animName, (int,int) pos, float damagePerSecond = 2f, int health = 50)
        : base(animName, pos, damagePerSecond, health)
    {
        RangeDepth = 0;
    }
}