using System;

public class RegularMonsterGroup : MonsterGroup
{
    public RegularMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
    }

    public override MonsterType MonsterType => MonsterType.Regular;

    protected override TimeSpan speed => TimeSpan.FromSeconds(2);
}