using System;

public class SmallMonsterGroup : MonsterGroup
{
    public override MonsterType MonsterType => MonsterType.Small;

    public override bool isGrounded => true;

    protected override TimeSpan speed => TimeSpan.FromSeconds(1);

    public SmallMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
    }
}