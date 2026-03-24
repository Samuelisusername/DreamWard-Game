
using System.Diagnostics;
using System.Security;
using DreamWardUtils.Graphics;
using Gamelab;
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

    private bool _isDaySprite = true;

    protected string _animName;

    /// <summary>
    /// removes health from the entity
    /// </summary>
    public void TakeDamage(float amount)
    {
        _currentHealth -= amount;
    }

    public void Update(GameTime gameTime)
    {
        if(IsBuildEntity && GamelabGame.GamePhaseManager.IsDay != _isDaySprite)
        {
            _isDaySprite = !_isDaySprite;
            _sprite = _isDaySprite ? GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) : GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
        }
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
    protected Vector2 _localMonsterOffset = new Vector2(-32, -90);
    protected Vector2 _localStructureOffset = new Vector2(-32, -96);
    public Entity(string animName, MapManager mapManager, (int,int) tilePos, int health = 100, bool isBuildEntity = false)
    {
        IsBuildEntity = isBuildEntity;
        _animName = animName;
        if(IsBuildEntity)
        {
            _sprite = GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(animName);
        }
        else
        {            
            _sprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite(animName);
        }
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
        if(!IsBuildEntity)
        {
            _sprite.Draw(spriteBatch, worldPos + _localMonsterOffset * _sprite.Scale);
        }
        else
        {
            _sprite.Draw(spriteBatch, worldPos + _localStructureOffset * _sprite.Scale);
        }
    }
}