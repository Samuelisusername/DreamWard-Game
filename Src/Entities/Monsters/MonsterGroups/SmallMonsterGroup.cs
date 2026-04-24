using System;
using Microsoft.Xna.Framework;

public class SmallMonsterGroup : MonsterGroup
{
    public override MonsterType MonsterType => MonsterType.Small;
    protected override int attackFrame => 3;

    public override bool isGrounded => true;

    //protected override TimeSpan speed => TimeSpan.FromSeconds(1);
    public override Vector2 LocalOffset => new Vector2(-32, -85);

    public SmallMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
    }
}