using System;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// A tree tile entity. Cannot be attacked, stepped on, or built over.
/// Purely decorative obstacle that blocks movement and building.
/// </summary>
public class TreeTile : Structure
{
    public override bool IsFlat => false;
    public override bool CanMove => false;

    private readonly SpriteEffects _flip;

    protected override Microsoft.Xna.Framework.Vector2 _localOffset =>
        _flip == SpriteEffects.FlipHorizontally
            ? new Microsoft.Xna.Framework.Vector2(-33, -96)
            : new Microsoft.Xna.Framework.Vector2(-32, -96);

    public TreeTile(string animName, (int,int) pos)
        : base(animName, pos, int.MaxValue)
    {
        _flip = Random.Shared.NextDouble() < 0.5 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _sprite.SpriteEffects = _flip;
    }

    public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        base.Update(gameTime);
        _sprite.SpriteEffects = _flip;
    }
}
