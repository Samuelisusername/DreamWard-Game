using System;
using Microsoft.Xna.Framework;

public class RegularMonsterGroup : MonsterGroup
{
    public override bool isGrounded => true;
    public RegularMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
    }

    public override MonsterType MonsterType => MonsterType.Regular;

    //protected override TimeSpan speed => TimeSpan.FromSeconds(2);

    public override Vector2 LocalOffset => new Vector2(-20, -90);
}