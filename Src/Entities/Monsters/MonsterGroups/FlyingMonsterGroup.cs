using System;

public class FlyingMonsterGroup : MonsterGroup
{
    public FlyingMonsterGroup((int, int) tilePos) : base(tilePos)
    {
    }

    public override MonsterType MonsterType => MonsterType.Flying;
    

    protected override TimeSpan speed => TimeSpan.FromSeconds(2);
}