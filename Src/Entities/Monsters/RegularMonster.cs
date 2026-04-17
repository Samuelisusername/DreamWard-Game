public class RegularMonster : Monster
{

    public override float DamagePerSecond => 3;

    public override MonsterType Type => MonsterType.Regular;

    public override string Name => "Regular";

    
    public RegularMonster((int, int) startTile,  int health = 30) : base(startTile, health)
    {
    }

    public override bool CanTarget(Entity entity)
    {
        if (entity is VoidTile) return false;
        if (entity is TreeTile) return false;        
        if (entity is Meadow meadow && (!meadow.HasSheep || meadow.IsPolymorphed)) return false;
        if (entity is PlacableMeadow) return false; //TODO: when can they attack meadows
        if (entity is Spikes) return false;
        return true;
    }

    public override bool CanStepOn(Entity entity)
    {
        if (entity is VoidTile ) return false;
        if (entity is TreeTile) return false;
        if (entity is WallTile) return false;
        if (entity is Wall) return false;
        if (entity is Fence) return false;
        if (entity is Tower && entity is not Spikes) return false;
        return true;
    }
}