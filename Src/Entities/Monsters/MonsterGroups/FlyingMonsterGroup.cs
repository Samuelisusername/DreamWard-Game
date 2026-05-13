using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class FlyingMonsterGroup : MonsterGroup
{
    private Texture2D shadow;
    public override bool isGrounded => false;
    public FlyingMonsterGroup((int, int) tilePos) : base(tilePos)
    {
        shadow = GamelabGame.Instance.Content.Load<Texture2D>("Sprites/shadowDummy");
    }

    public override MonsterType MonsterType => MonsterType.Flying;
    protected override int attackFrame => 3;
    

    //protected override TimeSpan speed => TimeSpan.FromSeconds(2);


    public override Vector2 LocalOffset => new Vector2(-44, -90);
    public override Vector2 GetInterpolatedWorldPosition(bool basePoint = false)
    {
        if(basePoint)
        {
            return base.GetInterpolatedWorldPosition();
        }
        Vector2 origin = mapManager.GetWorldPositionOfTile(TilePosition);
        if (Monsters.Count == 0) return origin;
        //they are always flying
        origin -= Vector2.UnitY * 30 * mapManager.Scale;
        
        var goal = Monsters[0].GetCurrentGoal();
        Vector2 goalPos = mapManager.GetWorldPositionOfTile(goal);
        //the monster always flies
        goalPos -= Vector2.UnitY * 30 * mapManager.Scale;
        return origin + (goalPos - origin) * combatManager.PathProgress[MonsterType];
    }

    public void DrawShadow(SpriteBatch spriteBatch/*, (int totalGroups, int rank) posOnTile*/)
    {
        if (IsEmpty) return;

        Vector2 worldPos = GetInterpolatedWorldPosition();
        Vector2 shadowPos = base.GetInterpolatedWorldPosition();
        
        // Use the container's rank logic to offset them so they don't overlap perfectly
        // Vector2 rankOffset = new Vector2(posOnTile.rank * 10 * mapManager.Scale.X, 0);

        //sprite.Draw(spriteBatch, worldPos + (_localOffset * mapManager.Scale) + rankOffset);
        //sprite.Draw(spriteBatch, shadowPos + (_localOffset * mapManager.Scale) + rankOffset);
        
        spriteBatch.Draw(shadow,
        shadowPos +  (LocalOffset * mapManager.Scale),
        new Rectangle(0,0,128,128),
        Color.White * 0.2f,
        0,
        Vector2.Zero,
        mapManager.Scale,
        SpriteEffects.None,
        0);
    }

}