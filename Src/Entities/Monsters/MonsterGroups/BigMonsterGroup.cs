using System;
using Microsoft.Xna.Framework;

public class BigMonsterGroup : MonsterGroup
{
    public override MonsterType MonsterType => MonsterType.Big;

    public override bool isGrounded => true;

    //protected override TimeSpan speed => TimeSpan.FromSeconds(3);
    public override Vector2 LocalOffset => new Vector2(-32, -90);

    public BigMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
    }
}