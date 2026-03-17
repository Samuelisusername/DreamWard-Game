
using System.Diagnostics;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public abstract class Entity
{
    protected int _health;

    public (int,int) TilePosition {get; protected set;}

    public bool IsDestroyed => _health <= 0;

    protected AnimatedSprite _sprite;

    public void TakeDamage(int amount)
    {
        _health -= amount;
    }

    public void Update(GameTime gameTime)
    {
        _sprite.Update(gameTime);
    }

    public void SetScale(Vector2 scale)
    {
        _sprite.Scale = scale;
    }

    public abstract void Draw(SpriteBatch spriteBatch);
}