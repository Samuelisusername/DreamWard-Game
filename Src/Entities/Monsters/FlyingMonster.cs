public class FlyingMonster : Monster
{

    //public override float DamagePerSecond => 1.5f;

    public override MonsterType Type => MonsterType.Flying;

    public override bool CanTarget(Entity entity)
    {
        if (entity is VoidTile) return false;
        if (entity is TreeTile) return false;
        if (entity is Meadow meadow && (!meadow.HasSheep || meadow.IsPolymorphed)) return false;
        if (entity is PlacableMeadow) return false; //TODO: when can they attack meadows
        if (entity is WallTile) return false;
        if (entity is Spike) return false;
        return true;
    }
    public override bool CanStepOnTile(int tileIndex)
    {
        // Flying monsters can be over empty space or regular grass.
        return true; 
    }

    public override bool CanStepOn(Entity entity)
    {
        if (entity is TreeTile) return false;
        if (entity is Wall) return false;
        return true;
    }
    
    public FlyingMonster((int, int) startTile) : base(startTile)
    {
    }
}