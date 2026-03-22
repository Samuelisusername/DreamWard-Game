
using System.Diagnostics;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public abstract class Entity
{   
    //health of the entity
    protected float _currentHealth;
    protected float _maxHealth;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;

    /// <summary>
    /// Posistion of the entity on the map
    /// </summary>
    public (int,int) TilePosition {get; protected set;}
    public bool IsBuildEntity { get; private set; } = false;
    public void SetAsBuildEntity() => IsBuildEntity = true;
    public virtual void SetTilePosition((int,int) newPos)
    {
        TilePosition = newPos;
    }

    /// <summary>
    /// is the Entity destoryed
    /// </summary>
    public bool IsDestroyed => _currentHealth <= 0;

    //the animation of this entity
    protected AnimatedSprite _sprite;

    /// <summary>
    /// removes health from the entity
    /// </summary>
    public virtual void TakeDamage(float amount)
    {
        _currentHealth -= amount;
    }

    public void Update(GameTime gameTime)
    {
        if (_sprite != null)
            _sprite.Scale = _mapManager.Scale;
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

    protected MapManager _mapManager;
    protected Vector2 _localOffset = new Vector2(-32, -90);
    public Entity(AnimatedSprite sprite, MapManager mapManager, (int,int) tilePos, int health = 100)
    {
        _sprite = sprite;
        _mapManager = mapManager;
        TilePosition = tilePos;
        _currentHealth = health;
        _maxHealth = health;

        if (_sprite != null)
            _sprite.Scale = _mapManager.Scale;
    }

    /// <summary>
    /// draws the entity
    /// </summary>
    public virtual void Draw(SpriteBatch spriteBatch)
    {
        if (_sprite == null) return;

        Vector2 worldPos = _mapManager.GetWorldPositionOfTile(TilePosition.Item1, TilePosition.Item2);
        _sprite.Draw(spriteBatch, worldPos + _localOffset * _sprite.Scale);
    }
}