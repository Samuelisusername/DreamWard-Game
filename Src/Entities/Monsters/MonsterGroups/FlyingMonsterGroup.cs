using System;

public class FlyingMonsterGroup : MonsterGroup
{
    public FlyingMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
    }

    public override MonsterType MonsterType => MonsterType.Flying;
    

    protected override TimeSpan speed => TimeSpan.FromSeconds(2);
}