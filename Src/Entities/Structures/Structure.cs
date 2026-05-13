using System;
using System.Linq;
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
    
    protected string _animNameBase;
    protected string _animNameTop;

    public override bool IsBuildEntity => true;

    protected override Vector2 _localOffset => new Vector2(-32, -96);

    protected GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    protected MapManager mapManager => GamelabGame.MapManager;

    public Structure(string animName, (int, int) tilePos) : base(tilePos)
    {
        _animNameBase = animName;
        _animNameTop = animName;
        _spriteTop = GamelabGame.TextureAtlas["day_top"].CreateAnimatedSprite(animName);
        _spriteBase = GamelabGame.TextureAtlas["day_top_base"].CreateAnimatedSprite(animName);

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
            if (_spriteTop != null)
                _spriteTop.Animation = _isDaySprite ? GamelabGame.TextureAtlas["day_top"].GetAnimation(_animNameTop) :
                GamelabGame.TextureAtlas["night_top"].GetAnimation(_animNameTop);
            if (_spriteBase != null)
                _spriteBase.Animation = _isDaySprite ? GamelabGame.TextureAtlas["day_top_base"].GetAnimation(_animNameBase) :
                GamelabGame.TextureAtlas["night_top_base"].GetAnimation(_animNameBase);
        }
        if (_spriteTop != null)
            _spriteTop.Scale = mapManager.Scale;
        if (_spriteBase != null)
            _spriteBase.Scale = mapManager.Scale;
        _spriteTop?.Update(gameTime);
        _spriteBase?.Update(gameTime);
    }    

    public Color? PreviewTint { get; set; }

    /// <summary>
    /// draws the entity
    /// </summary>
    public virtual void Draw(SpriteBatch spriteBatch, Vector2 offset = new(), float alpha = 1.0f)
    {
        if (_spriteTop == null) return;
        #if DEBUG

        if(MapUtils.IsInBounds(TilePosition) && mapManager.Debug[TilePosition.Item1, TilePosition.Item2])
        {
            _spriteTop.Color = Color.Red;
        }

        #endif

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        Color savedColor = _spriteTop.Color;
        if (PreviewTint.HasValue) _spriteTop.Color = PreviewTint.Value;
        _spriteTop.Draw(spriteBatch, worldPos + (_localOffset * _spriteTop.Scale + offset), alpha);
        if (PreviewTint.HasValue) _spriteTop.Color = savedColor;
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
        Color savedColor = _spriteBase.Color;
        if (PreviewTint.HasValue) _spriteBase.Color = PreviewTint.Value;
        _spriteBase.Draw(spriteBatch, worldPos + (_localOffset * _spriteBase.Scale + offset), alpha);
        if (PreviewTint.HasValue) _spriteBase.Color = savedColor;
    }

    protected void SetAnimation(string animName)
    {
        _animNameBase = animName;
        _animNameTop = animName;
        _spriteTop = gamePhaseManager.IsDay ? 
        GamelabGame.TextureAtlas["day_top"].CreateAnimatedSprite(animName) :
        GamelabGame.TextureAtlas["night_top"].CreateAnimatedSprite(animName);
        _spriteTop.Scale = mapManager.Scale;
        _spriteBase = gamePhaseManager.IsDay ? 
        GamelabGame.TextureAtlas["day_top_base"].CreateAnimatedSprite(animName) :
        GamelabGame.TextureAtlas["night_top_base"].CreateAnimatedSprite(animName);
        _spriteBase.Scale = mapManager.Scale;
    }

    protected void SetAnimationTop(string animName)
    {
        _animNameTop = animName;
        _spriteTop = gamePhaseManager.IsDay ?
        GamelabGame.TextureAtlas["day_top"].CreateAnimatedSprite(animName) :
        GamelabGame.TextureAtlas["night_top"].CreateAnimatedSprite(animName);
        if (_spriteTop != null)
            _spriteTop.Scale = mapManager.Scale;
    }

    protected void SetAnimationBase(string animName)
    {
        _animNameBase = animName;
        _spriteBase = gamePhaseManager.IsDay ?
        GamelabGame.TextureAtlas["day_top_base"].CreateAnimatedSprite(animName) :
        GamelabGame.TextureAtlas["night_top_base"].CreateAnimatedSprite(animName);
        if (_spriteBase != null)
            _spriteBase.Scale = mapManager.Scale;
    }

    public override void TakeDamage(float amount)
    {        
        base.TakeDamage(amount);        
        if(IsDestroyed)
        {
            Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
            for(int i = 0; i < 40; i++)
            {
                ParticleManager.AddParticle(new Particle
                {
                    WorldPosition = (worldPos + (_localOffset * _spriteTop.Scale) + (new Vector2(64, 100) * _spriteTop.Scale) - mapManager.Offset)/mapManager.Scale,
                    Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 150, (float)(Random.Shared.NextDouble() -0.5f) * 150),
                    LifeSpan = TimeSpan.FromSeconds(0.5),
                    Color = Color.Gray
                });
            }
            mapManager.InitScreenShake(TimeSpan.FromSeconds(0.3f), 7);
        }
    }

    #region statcard
    
    public TimeSpan BarComplete {get; protected set;} = TimeSpan.FromSeconds(1.5f);
    public TimeSpan BarProgress {get; protected set;} = TimeSpan.Zero;
    
    public TimeSpan CantAffordFlash {get; protected set;} = TimeSpan.Zero;
    public static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(0.5);


    public abstract (string label, string info)[] Infos {get;}

    public virtual string BottomText => null;
    
#endregion
}