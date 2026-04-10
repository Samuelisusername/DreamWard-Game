using System;

public class RegularMonsterGroup : MonsterGroup
{
    public RegularMonsterGroup((int, int) tilePos) : base(tilePos)
    {
    }

    public override MonsterType MonsterType => MonsterType.Regular;

    protected override TimeSpan speed => TimeSpan.FromSeconds(2);
}