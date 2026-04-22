using System;
using System.Linq;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// A Meadow Tile Entity it can have a sheep on it or not
/// </summary>
public class Meadow : Structure
{
    private SanityManager sanityManager => GamelabGame.SanityManager;
    public bool HasSheep{get; private set;}

    public bool IsPolymorphed{get; private set;}

    public int SanityGain {get; private set;}

    public override bool CanMove => false;
    protected override (string label, string info)[] infos => [("Health:", $"{CurrentHealth}"), ("Sanity gain:", $"{SanityGain}"), ("Cost to move:", $"{MapUtils.CalculateTileDistance(TilePosition, mapManager.SheepPosition)}")];

    protected override string bottomText => "Click and hold to move sheep here";

    protected TimeSpan timeToMove = TimeSpan.FromSeconds(2.5f);
    protected TimeSpan moveProgress = TimeSpan.Zero;

    private bool resetSanity = true;

    public Meadow((int,int) pos, int sanityGain)
        : base(sanityGain == 10 ? "meadow_red_idle" : sanityGain == 15 ? "meadow_blue_idle" : sanityGain == 20 ? "meadow_orange_idle" : "meadow_idle", pos, 50)
    {
        SanityGain = sanityGain;
        // float lerpConst = .25f;
        
        // if(SanityGain == 10)
        // {
        //     _spriteTop.Color = Color.Lerp(Color.White, Color.Red, lerpConst);
        //     _spriteBase.Color = Color.Lerp(Color.White, Color.Red, lerpConst);
        // }
        // else if(SanityGain == 15)
        // {
        //     _spriteTop.Color = Color.Lerp(Color.White, Color.Blue, lerpConst);
        //     _spriteBase.Color = Color.Lerp(Color.White, Color.Blue, lerpConst);
        // }
        // else if(SanityGain == 20)
        // {
        //     _spriteTop.Color = Color.Lerp(Color.White, Color.Orange, lerpConst);
        //     _spriteBase.Color = Color.Lerp(Color.White, Color.Orange, lerpConst);
        // }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if(resetSanity)
        {
            moveProgress = TimeSpan.Zero;
        }
        resetSanity = true;
    }

    public void AddSheep(bool isPolymorphed = false)
    {
        HasSheep = true;
        IsPolymorphed = isPolymorphed;
        if (IsPolymorphed)
            SetAnimation("rats_idle");
        else
            SetAnimation("sheepSmall_idle");
    }

    public bool AddProgress(TimeSpan delta)
    {
        if(HasSheep || mapManager.GetSheep() == null) return false;
        resetSanity = false;
        moveProgress += delta;
        if(moveProgress > timeToMove)
        {
            return true;
        }
        return false;
    }

    public void RemoveSheep()
    {
        HasSheep = false;
        SetAnimation("meadowGrazed_idle");
    }

    public void PolymorphSheep()
    {
        if(HasSheep)
        {
            IsPolymorphed = true;
            SetAnimation("rats_idle");
        }
    }

    public void TryUnpolymorphSheep()
    {
        if(HasSheep)
        {
            IsPolymorphed = false;
            SetAnimation("sheepSmall_idle");
        }
    }
    public override void DrawCard(SpriteBatch spriteBatch)
    {
        if(!ShouldDrawCard) return;
        
        Color rectColor = gamePhaseManager.IsDay
            ? COLOR_L
            : new Color(33, 28, 11);

        Color loadColor = gamePhaseManager.IsDay
            ? COLOR_G
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
            if(mapManager.GetSheep() != null)
            {                
                Point size = _font.MeasureString(bottomText).ToPoint();
                size.X = (int)(size.X * (moveProgress / timeToMove));
                spriteBatch.Draw(_dummy, new Rectangle(pos.ToPoint(), size), loadColor * 0.5f);
            }
            spriteBatch.DrawString(_font, bottomText, pos, textColor);
        }
    }
}

public class PlacableMeadow : Structure
{
    public int SanityGain {get; private set;}

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];

    public PlacableMeadow((int,int) pos, int sanityGain)
        : base("meadowPlaced_idle", pos, 5)
    {
        SanityGain = sanityGain;
    }

    public void ActivateMeadow(int gain)
    {
        SanityGain = gain;
        SetAnimation("meadow_idle");
    }
    public void DeactivateMeadow()
    {
        SanityGain = 5;
        SetAnimation("meadowPlaced_idle");
    }
}
