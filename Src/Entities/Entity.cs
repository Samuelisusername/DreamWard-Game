
using System.Diagnostics;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public abstract class Entity
{   
    //healts of the entity
    protected int _health;

    /// <summary>
    /// Posistion of the entity on the map
    /// </summary>
    public (int,int) TilePosition {get; protected set;}

    /// <summary>
    /// is the Entity destoryed
    /// </summary>
    public bool IsDestroyed => _health <= 0;

    //the animation of this entity
    protected AnimatedSprite _sprite;

    /// <summary>
    /// removes health from the entity
    /// </summary>
    public void TakeDamage(int amount)
    {
        _health -= amount;
    }

    public void Update(GameTime gameTime)
    {
        _sprite.Update(gameTime);
    }

    /// <summary>
    /// sets the scale for rendering the entity
    /// </summary>
    /// <param name="scale"></param>
    public void SetScale(Vector2 scale)
    {
        _sprite.Scale = scale;
    }

    /// <summary>
    /// draws the entity
    /// </summary>
    public abstract void Draw(SpriteBatch spriteBatch);
}