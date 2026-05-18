public class BigMonster : Monster
{
    //public override float DamagePerSecond => 10;

    public override MonsterType Type => MonsterType.Big;

    public override bool CanStepOn(Entity entity)
    {
        if (entity is VoidTile ) return false;
        if (entity is TreeTile) return false;
        if (entity is WallTile) return false;
        if (entity is Wall) return false;
        if (entity is Fence) return false;
        if (entity is Tower && entity is not Spike) return false;
        if (entity is Meadow meadow && meadow.HasSheep && !meadow.IsPolymorphed) return false;
        if (entity is Buff) return false;
        if (entity is Decoy) return false;
        if (entity is PlacableMeadow) return false;
        if (entity is Tent) return false;
        return true;
    }

    public override bool CanTarget(Entity entity)
    {
        if (entity is VoidTile) return false;
        if (entity is TreeTile) return false;        
        if (entity is Meadow meadow && (!meadow.HasSheep || meadow.IsPolymorphed)) return false;
        if (entity is Spike) return false;
        return true;
    }
    
    public BigMonster((int, int) startTile) : base(startTile)
    {
    }
}