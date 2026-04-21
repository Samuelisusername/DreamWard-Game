using System;

public class BigMonsterGroup : MonsterGroup
{
    public override MonsterType MonsterType => MonsterType.Big;

    public override bool isGrounded => true;

    protected override TimeSpan speed => TimeSpan.FromSeconds(3);

    public BigMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
    }
}