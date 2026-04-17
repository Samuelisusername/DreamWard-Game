using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public abstract class Structure : Entity
{
    //the animation of this entity
    protected AnimatedSprite _spriteTop;
    protected AnimatedSprite _spriteBase;

    private bool _isDaySprite = true;
    
    protected string _animName;
    
    public override bool IsBuildEntity => true;

    protected override Vector2 _localOffset => new Vector2(-32, -96);

    protected GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    protected MapManager mapManager => GamelabGame.MapManager;

    public Structure(string animName, (int, int) tilePos, int health = 100) : base(tilePos, health)
    {
        _animName = animName;
        _spriteTop = GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(animName);
        _spriteBase = GamelabGame.textureAtlas["day_top_base"].CreateAnimatedSprite(animName);

        if (_spriteTop != null)
            _spriteTop.Scale = mapManager.Scale;
        if (_spriteBase != null)
            _spriteBase.Scale = mapManager.Scale;
    }

    public override void Update(GameTime gameTime)
    {
        if(gamePhaseManager.IsDay != _isDaySprite)
        {
            _isDaySprite = !_isDaySprite;
            _spriteTop.Animation = _isDaySprite ? GamelabGame.textureAtlas["day_top"].GetAnimation(_animName) :
            GamelabGame.textureAtlas["night_top"].GetAnimation(_animName);
            _spriteBase.Animation = _isDaySprite ? GamelabGame.textureAtlas["day_top_base"].GetAnimation(_animName) :
            GamelabGame.textureAtlas["night_top_base"].GetAnimation(_animName);
        }
        if (_spriteTop != null)
            _spriteTop.Scale = mapManager.Scale;
        if (_spriteBase != null)
            _spriteBase.Scale = mapManager.Scale;
        _spriteTop.Update(gameTime);
        _spriteBase.Update(gameTime);
    }    

    /// <summary>
    /// draws the entity
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Vector2 offset = new(), float alpha = 1.0f)
    {
        if (_spriteTop == null) return;
        #if DEBUG
        
        if(MapUtils.IsInBounds(TilePosition) && mapManager.Debug[TilePosition.Item1, TilePosition.Item2])
        {
            _spriteTop.Color = Color.Red;
        }

        #endif

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        _spriteTop.Draw(spriteBatch, worldPos + (_localOffset * _spriteTop.Scale + offset), alpha);
    }

    /// <summary>
    /// draws the entity
    /// </summary>
    public void DrawBase(SpriteBatch spriteBatch, Vector2 offset = new(), float alpha = 1.0f)
    {
        if (_spriteBase == null) return;
        #if DEBUG
        
        if(MapUtils.IsInBounds(TilePosition) && mapManager.Debug[TilePosition.Item1, TilePosition.Item2])
        {
            _spriteBase.Color = Color.Red;
        }

        #endif

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        _spriteBase.Draw(spriteBatch, worldPos + (_localOffset * _spriteBase.Scale + offset), alpha);
    }

    protected void SetAnimation(string animName)
    {
        _animName = animName;
        _spriteTop = gamePhaseManager.IsDay ? 
        GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
        GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
        _spriteTop.Scale = mapManager.Scale;
        _spriteBase = gamePhaseManager.IsDay ? 
        GamelabGame.textureAtlas["day_top_base"].CreateAnimatedSprite(_animName) :
        GamelabGame.textureAtlas["night_top_base"].CreateAnimatedSprite(_animName);
        _spriteBase.Scale = mapManager.Scale;
    }
}