using Microsoft.Xna.Framework;

public abstract class Entity
{   
    public virtual float CurrentHealth {get; protected set;}
    public virtual float MaxHealth {get; protected set;}

    /// <summary>
    /// Posistion of the entity on the map
    /// </summary>
    public (int,int) TilePosition {get; set;}
    public abstract bool IsBuildEntity { get ;}

    /// <summary>
    /// is the Entity destoryed
    /// </summary>
    public bool IsDestroyed => CurrentHealth <= 0;

    /// <summary>
    /// removes health from the entity
    /// </summary>
    public void TakeDamage(float amount)
    {
        CurrentHealth -= amount;
    }

    public abstract void Update(GameTime gameTime);

    protected abstract Vector2 _localOffset{get;}
    public Entity((int,int) tilePos, int health = 100)
    {
        TilePosition = tilePos;
        CurrentHealth = health;
        MaxHealth = health;
    }
}