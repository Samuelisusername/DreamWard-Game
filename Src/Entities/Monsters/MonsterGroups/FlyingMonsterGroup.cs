using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class FlyingMonsterGroup : MonsterGroup
{
    private Texture2D shadow;
    public override bool isGrounded => Monsters[0].Path.Count > 2 ? false : true;
    public FlyingMonsterGroup((int, int) tilePos, TimeSpan elapsed) : base(tilePos, elapsed)
    {
        shadow = GamelabGame.Instance.Content.Load<Texture2D>("Sprites/shadowDummy");
    }

    public override MonsterType MonsterType => MonsterType.Flying;
    

    //protected override TimeSpan speed => TimeSpan.FromSeconds(2);


    public override Vector2 LocalOffset => new Vector2(-44, -90);
    public override Vector2 GetInterpolatedWorldPosition()
    {
        Vector2 origin = mapManager.GetWorldPositionOfTile(TilePosition);
        if (Monsters.Count == 0) return origin;
        if(CommonNextTarget() != TilePosition)
        {
            //they are still flying
            origin -= Vector2.UnitY * 50 * mapManager.Scale;
        }
        
        var goal = Monsters[0].GetCurrentGoal();
        Vector2 goalPos = mapManager.GetWorldPositionOfTile(goal);
        if(Monsters[0].Path.Count > 2)
        {
            //the monster moves on next step
            goalPos -= Vector2.UnitY * 50 * mapManager.Scale;
        }
        return origin + (goalPos - origin) * _pathProgress;
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

    public override void Draw(SpriteBatch spriteBatch/*, (int totalGroups, int rank) posOnTile*/)
    {
        if (IsEmpty) return;

        Vector2 worldPos = GetInterpolatedWorldPosition();
        Vector2 shadowPos = base.GetInterpolatedWorldPosition();
        
        // Use the container's rank logic to offset them so they don't overlap perfectly
        // Vector2 rankOffset = new Vector2(posOnTile.rank * 10 * mapManager.Scale.X, 0);

        sprite.Draw(spriteBatch, worldPos + (LocalOffset * mapManager.Scale));
        //sprite.Draw(spriteBatch, shadowPos + (_localOffset * mapManager.Scale) + rankOffset);
        
    }

}