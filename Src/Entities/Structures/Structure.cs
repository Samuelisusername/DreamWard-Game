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

    #region statcard

    protected Texture2D _dummy = GamelabGame.DummyText;
    protected SpriteFont _font = GamelabGame.Font;
    protected abstract (string label, string info)[] infos {get;}

    protected virtual string bottomText => null;

    public bool ShouldDrawCard {get; set;}
    
    protected readonly Color COLOR_D = new(0x29, 0x24, 0x10);    
    protected readonly Color COLOR_G = new Color(0x7d, 0x64, 0x48);
    protected readonly Color COLOR_L = new Color(0xc7, 0xb8, 0x9c);

    public virtual void DrawCard(SpriteBatch spriteBatch)
    {
        if(!ShouldDrawCard) return;
        
        Color rectColor = gamePhaseManager.IsDay
            ? COLOR_L
            : new Color(33, 28, 11);      // #211c0b

        Color textColor = gamePhaseManager.IsDay
            ? COLOR_D
            : new Color(214, 214, 214); 

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        int pad = 10;
        int midPad = 50;
        int heightPad = 50;
        int boxWidth = (int)infos.Cast<(string, string)>().Max(i => _font.MeasureString(i.Item1 + i.Item2).X) + 2 * pad + midPad;
        int boxHeight = (int)infos.Cast<(string, string)>().Sum(i => _font.MeasureString(i.Item1).Y) + (pad * (infos.Length + 1));
        if(bottomText != null)
        {
            boxWidth = Math.Max(boxWidth, (int)_font.MeasureString(bottomText).X + 2 * pad);
            boxHeight += (int)_font.MeasureString(bottomText).Y + heightPad;
        }
        int left = (int)(worldPos.X + (31.5f * mapManager.Scale.X)) - boxWidth/2;
        int top = (int)(worldPos.Y - 64 - boxHeight);


        spriteBatch.Draw(_dummy, new Rectangle(left, top, boxWidth, boxHeight), rectColor * 0.5f);
        int yOffset = pad;
        for (int i = 0; i < infos.Length; i++)
        {
            Vector2 posLabel = new(left + pad, top + yOffset);
            spriteBatch.DrawString(_font, infos[i].label, posLabel, textColor);
            Vector2 posInfo = new(left + boxWidth - pad - _font.MeasureString(infos[i].info).X, posLabel.Y);
            spriteBatch.DrawString(_font, infos[i].info, posInfo, textColor);
            yOffset += (int)_font.MeasureString(infos[i].info).Y + pad;
        }
        if(bottomText != null)
        {
            Vector2 pos = new (worldPos.X + (31.5f * mapManager.Scale.X) - _font.MeasureString(bottomText).X/2, top + yOffset - pad + heightPad);
            spriteBatch.DrawString(_font, bottomText, pos, textColor);
        }
    }

    #endregion
}