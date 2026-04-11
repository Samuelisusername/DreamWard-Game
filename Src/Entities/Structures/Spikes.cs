/// <summary>
///  Inflicts Damage on Ground Monsters
/// </summary>
public class Spikes : Structure
{
    public override bool IsFlat => false;
    public Spikes(string animName, (int,int) pos, int health = 50)
        : base(animName, pos, health)
    {
    }
}