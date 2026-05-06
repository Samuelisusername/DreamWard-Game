using System;
using System.Linq;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class Tent : CardEntity
{
    private SanityManager sanityManager => GamelabGame.SanityManager;

    public override bool CanMove => false;

    private TimeSpan zTimer = TimeSpan.FromSeconds(0.5f);
    private int zCycle = 0;
    private Sprite _portrait;


    public Tent(string animName, (int,int) pos)
        : base(animName, pos)
    { 
        _portrait = GamelabGame.TextureAtlas["day_player"].CreateSprite("player0");
        _portrait.Scale = Vector2.One * 2;
        _portrait.Origin = new Vector2(2,18);
    }
    
    public override float CurrentHealth { get => sanityManager?.Sanity ?? 0; protected set => base.CurrentHealth = value; }
    public override float MaxHealth
    { 
        get => (sanityManager?.Sanity ?? 0) > base.MaxHealth ? sanityManager?.Sanity ?? 0 : base.MaxHealth;
        protected set => base.MaxHealth = value; 
    }

    protected override (string label, string info)[] infos => [("Sanity:", $"{CurrentHealth}")];

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        zTimer -= gameTime.ElapsedGameTime;
        if(zTimer <= TimeSpan.Zero)
        {
            zCycle += 1;
            zCycle %= 3;
            if(zCycle == 0)
            {
                zTimer = TimeSpan.FromSeconds(1);
            }
            else
            {
                zTimer = TimeSpan.FromSeconds(0.3f);
            }
            Vector2 worldPos = (mapManager.GetWorldPositionOfTile(TilePosition) + (new Vector2(40, 5) * mapManager.Scale) - mapManager.Offset) / mapManager.Scale;
            
            if(!gamePhaseManager.IsDay)
            {
                ParticleManager.AddParticle(new Particle
                {
                    WorldPosition = worldPos ,
                    Velocity = new Vector2(10f, -15f),
                    Color = Color.LightGray,
                    LifeSpan = TimeSpan.FromSeconds(1.0),
                    Text = $"z",
                    Font = _font,
                    Scale = 0.3f,
                    Updater = (particle, gameTime) =>
                    {            
                        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                        particle.Color  = Color.Lerp(particle.Color, Color.FromNonPremultiplied(particle.Color.R,particle.Color.G,particle.Color.B,(byte)0) , 1.0f * dt);
                        particle.Scale += 0.5f * dt;
                        particle.Velocity += new Vector2(5,7.5f) * dt;
                    }
                });
            }
        }
    }

    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);
        mapManager.InitScreenShake(TimeSpan.FromSeconds(0.3f), 7);
    }

    public override void DrawCard(SpriteBatch spriteBatch, Rectangle screen)
    {
        if(!ShouldDrawCard) return;
        
        Color rectColor = gamePhaseManager.IsDay
            ? COLOR_L
            : new Color(33, 28, 11);      // #211c0b

        Color textColor = gamePhaseManager.IsDay
            ? COLOR_D
            : new Color(214, 214, 214); 

        // Floor to integer scale so every font stroke occupies whole screen pixels without growing larger than intended.
        float sx = MathF.Max(1f, MathF.Floor(_screenTransform.M11));
        float sy = MathF.Max(1f, MathF.Floor(_screenTransform.M22));
        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        int pad = 10;
        int midPad = 50;
        int heightPad = 50;
        int maxTutorialWidth = 300;

        int boxHeight;
        int boxWidth;
        Vector2 boxDimensions = Vector2.Zero;
        if(tutorialText != null)
        {
            boxDimensions = ProcessTextWithIcons(null, _font, tutorialText, Vector2.Zero, Color.White, maxTutorialWidth * sx, new Vector2(sx, sy), true);
            boxWidth = (int)boxDimensions.X + (pad * 4) + (int)(_portrait.Scale.X * 11);
            boxHeight = Math.Max((int)boxDimensions.Y + (pad * 2), (int)(_portrait.Scale.Y * 28) + (pad * 2));
        }
        else
        {
            boxWidth  = (int)MathF.Round((infos.Cast<(string, string)>().Max(i => _font.MeasureString(i.Item1 + i.Item2).X) + 2 * pad + midPad) * sx);
            boxHeight = (int)MathF.Round((infos.Cast<(string, string)>().Sum(i => _font.MeasureString(i.Item1).Y) + pad * (infos.Length + 1)) * sy);
            if(bottomText != null)
            {
                boxWidth  = Math.Max(boxWidth,  (int)MathF.Round((_font.MeasureString(bottomText).X + 2 * pad) * sx));
                boxHeight += (int)MathF.Round((_font.MeasureString(bottomText).Y + heightPad) * sy);
            }
        }

        

        Vector2 anchor = ToScreen(worldPos.X + 31.5f * mapManager.Scale.X, worldPos.Y);
        if((int)(anchor.X - boxWidth / 2f) < screen.Left + (2*pad))
        {
            anchor.X = screen.Left + (2*pad) + (boxWidth / 2f);
        }        
        else if((int)(anchor.X - boxWidth / 2f) + boxWidth > screen.Right - (2*pad))
        {
            anchor.X  = screen.Right - boxWidth - (2*pad)  + (boxWidth / 2f);
        }
        if((int)(anchor.Y - MathF.Round(16 * mapManager.Scale.X * sy) - boxHeight) < screen.Top + (2*pad))
        {
            anchor.Y = screen.Top + (2*pad) + MathF.Round(16 * mapManager.Scale.X * sy) + boxHeight;
        }
        int left = (int)(anchor.X - boxWidth / 2f);
        int top  = (int)(anchor.Y - MathF.Round(16 * mapManager.Scale.X * sy) - boxHeight);

        spriteBatch.Draw(_dummy, new Rectangle(left, top, boxWidth, boxHeight), rectColor * 0.5f);

        if(tutorialText != null)
        {
            Vector2 textStartPos = new Vector2(left + MathF.Round(((3 * pad) + (int)(_portrait.Scale.X * 11)) * sx), top + MathF.Round(pad * sy));
            _portrait.Draw(spriteBatch, new Vector2(left + MathF.Round(pad * sx), top + MathF.Round(boxHeight/2 * sy)));    
            // Pass false for isMeasuring, pass the actual SpriteBatch
            ProcessTextWithIcons(spriteBatch, _font, tutorialText, textStartPos, textColor, maxTutorialWidth * sx, new Vector2(sx, sy), false);
            //spriteBatch.DrawString(_font, displayText, new Vector2(left + MathF.Round(pad * sx), top + MathF.Round(pad * sy)) , textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
        }
        else
        {
            float yOff = MathF.Round(pad * sy);
            for (int i = 0; i < infos.Length; i++)
            {
                var posLabel = new Vector2(left + MathF.Round(pad * sx), top + yOff);
                spriteBatch.DrawString(_font, infos[i].label, posLabel, textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
                float infoW = _font.MeasureString(infos[i].info).X * sx;
                var posInfo = new Vector2((int)(left + boxWidth - MathF.Round(pad * sx) - infoW), posLabel.Y);
                spriteBatch.DrawString(_font, infos[i].info, posInfo, textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
                yOff += MathF.Round((_font.MeasureString(infos[i].info).Y + pad) * sy);
            }
            if(bottomText != null)
            {
                float textW = _font.MeasureString(bottomText).X * sx;
                var pos = new Vector2((int)(anchor.X - textW / 2f), (int)(top + yOff - MathF.Round(pad * sy) + MathF.Round(heightPad * sy)));
                spriteBatch.DrawString(_font, bottomText, pos, textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            } 
        }
    }
}
