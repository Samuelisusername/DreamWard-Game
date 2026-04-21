public class RegularMonster : Monster
{

    public override float DamagePerSecond => 3;

    public override MonsterType Type => MonsterType.Regular;

    
    public RegularMonster((int, int) startTile,  int health = 30) : base(startTile, health)
    {
    }

    public override bool CanTarget(Entity entity)
    {
        if (entity is VoidTile) return false;
        if (entity is TreeTile) return false;        
        if (entity is Meadow meadow && (!meadow.HasSheep || meadow.IsPolymorphed)) return false;
        if (entity is PlacableMeadow) return false; //TODO: when can they attack meadows
        if (entity is Spike) return false;
        return true;
    }

    public override bool CanStepOn(Entity entity)
    {
        if (entity is VoidTile ) return false;
        if (entity is TreeTile) return false;
        if (entity is WallTile) return false;
        // if (entity is Wall) return false; TODO: Once pathfinding is updated uncomment this
        if (entity is Fence) return false;
        if (entity is Tower && entity is not Spike) return false;
        if (entity is Meadow meadow && meadow.HasSheep) return false;
        if (entity is Buff) return false;
        if (entity is Decoy) return false;
        // if (entity is Tent) return false; TODO: Once pathfinding is updated uncomment this
        return true;
    }
}