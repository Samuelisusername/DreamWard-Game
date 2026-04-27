using Gamelab;
using Microsoft.Xna.Framework;

public abstract class Entity
{   
    public virtual float CurrentHealth {get; protected set;}

    private MapManager mapManager => GamelabGame.MapManager;
    
    public virtual float MaxHealth 
    {
        get => maxHealth == 0 ? GamelabGame.Instance.Balance.EntityHealth[this.GetType()] : maxHealth;
        protected set => maxHealth = value;
    }

    protected float maxHealth;

    /// <summary>
    /// Posistion of the entity on the map
    /// </summary>
    public (int,int) TilePosition {get; set;}
    public abstract bool IsBuildEntity { get ;}

    public virtual bool CanMove => true;

    /// <summary>
    /// is the Entity destoryed
    /// </summary>
    public bool IsDestroyed => CurrentHealth <= 0;

    /// <summary>
    /// removes health from the entity
    /// </summary>
    public virtual void TakeDamage(float amount)
    {
        CurrentHealth -= amount;
    }

    public void Heal(float amount)
    {
        CurrentHealth = MathHelper.Min(CurrentHealth + amount, MaxHealth);
    }

    public abstract void Update(GameTime gameTime);

    protected abstract Vector2 _localOffset{get;}
    public Entity((int,int) tilePos)
    {
        TilePosition = tilePos;
        CurrentHealth = MaxHealth;
    }
}