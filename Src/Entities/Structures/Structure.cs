using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public abstract class Structure : Entity
{
    //the animation of this entity
    protected AnimatedSprite _sprite;

    private bool _isDaySprite = true;
    
    protected string _animName;
    
    public override bool IsBuildEntity => true;

    protected override Vector2 _localOffset => new Vector2(-32, -96);

    protected GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    protected MapManager mapManager => GamelabGame.MapManager;

    public Structure(string animName, (int, int) tilePos, int health = 100) : base(tilePos, health)
    {
        _animName = animName;
        _sprite = GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(animName);

        if (_sprite != null)
            _sprite.Scale = mapManager.Scale;
    }

    public override void Update(GameTime gameTime)
    {
        if(gamePhaseManager.IsDay != _isDaySprite)
        {
            _isDaySprite = !_isDaySprite;
            _sprite = _isDaySprite ? GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
            GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
        }
        if (_sprite != null)
            _sprite.Scale = mapManager.Scale;
        _sprite.Update(gameTime);
    }    

    /// <summary>
    /// draws the entity
    /// </summary>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (_sprite == null) return;

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        _sprite.Draw(spriteBatch, worldPos + _localOffset * _sprite.Scale);
    }

    protected void SetAnimation(string animName)
    {
        _animName = animName;
        _sprite = gamePhaseManager.IsDay ? 
        GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
        GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
        _sprite.Scale = mapManager.Scale;
    }
    
    public void DrawTransparent(SpriteBatch spriteBatch, float alpha)
    {
        if (_sprite == null) return;
        _sprite.Scale = mapManager.Scale;

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);

        var originalColor = _sprite.Color;
        _sprite.Color = Color.White * alpha;

        _sprite.Draw(spriteBatch, worldPos + _localOffset * _sprite.Scale);

        _sprite.Color = originalColor;
    }

}