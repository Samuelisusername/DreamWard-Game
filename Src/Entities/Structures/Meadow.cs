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
    private const int MaxGrazingDays = 2;
    private SanityManager sanityManager => GamelabGame.SanityManager;
    public bool HasSheep{get; private set;}

    public bool IsPolymorphed{get; private set;}

    private int _baseSanityGain;
    public int SanityGain => IsDepleted ? 0 : _baseSanityGain;

    public bool IsGrazed {get; private set;}

    public int DaysGrazed {get; private set;}
    public bool IsDepleted {get; private set;}

    public override bool CanMove => false;
    protected override (string label, string info)[] infos =>
    !IsGrazed && !HasSheep ? [("Health:", $"{CurrentHealth}"), ("Sanity gain:", $"{SanityGain}"), ("Cost to move:", $"{(int)MathF.Floor(MapUtils.CalculateTileDistance(TilePosition, mapManager.SheepPosition) * MapManager.MoveCostMultiplier)}")] :
     [("Health:", $"{CurrentHealth}"), ("Sanity gain:", $"{SanityGain}")];

    protected override string bottomText => !HasSheep ? !IsGrazed ?  "Click and hold to move sheep here" : "Cant move sheep to grazed spot" : null;

    protected TimeSpan timeToMove = TimeSpan.FromSeconds(1.5f);
    protected TimeSpan moveProgress = TimeSpan.Zero;

    private bool resetSanity = true;
    private TimeSpan _cantAffordFlash = TimeSpan.Zero;
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(0.5);

    public Meadow((int,int) pos, int sanityGain)
        : base(sanityGain == 10 ? "meadow_red_idle" : sanityGain == 15 ? "meadow_blue_idle" : sanityGain == 20 ? "meadow_orange_idle" : "meadow_idle", pos)
    {
        _baseSanityGain = sanityGain;
    }

    public void TickGrazingDay()
    {
        if (!HasSheep || IsDepleted) return;
        DaysGrazed++;
        if (DaysGrazed > MaxGrazingDays)
        {
            IsDepleted = true;
            IsGrazed = true;
            SetAnimation("meadowGrazed_idle");
        }
    }

    public void TriggerCantAffordFlash() => _cantAffordFlash = FlashDuration;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if(resetSanity)
        {
            moveProgress = TimeSpan.Zero;
        }
        resetSanity = true;
        if(_cantAffordFlash > TimeSpan.Zero)
            _cantAffordFlash -= gameTime.ElapsedGameTime;
    }

    public void AddSheep(bool isPolymorphed = false)
    {
        HasSheep = true;
        IsPolymorphed = isPolymorphed;
        DaysGrazed = 0;
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
        IsGrazed = true;
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

        // Floor to integer scale so every font stroke occupies whole screen pixels without growing larger than intended.
        float sx = MathF.Max(1f, MathF.Floor(_screenTransform.M11));
        float sy = MathF.Max(1f, MathF.Floor(_screenTransform.M22));
        // Anchor uses the true (non-rounded) transform so the card stays over the tile.
        Vector2 ToScreen(float x, float y) => new Vector2(
            MathF.Round(x * _screenTransform.M11 + _screenTransform.M41),
            MathF.Round(y * _screenTransform.M22 + _screenTransform.M42));

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        int pad = 10;
        int midPad = 50;
        int heightPad = 50;

        int boxWidth  = (int)MathF.Round((infos.Cast<(string, string)>().Max(i => _font.MeasureString(i.Item1 + i.Item2).X) + 2 * pad + midPad) * sx);
        int boxHeight = (int)MathF.Round((infos.Cast<(string, string)>().Sum(i => _font.MeasureString(i.Item1).Y) + pad * (infos.Length + 1)) * sy);
        if(bottomText != null)
        {
            boxWidth  = Math.Max(boxWidth,  (int)MathF.Round((_font.MeasureString(bottomText).X + 2 * pad) * sx));
            boxHeight += (int)MathF.Round((_font.MeasureString(bottomText).Y + heightPad) * sy);
        }

        Vector2 anchor = ToScreen(worldPos.X + 31.5f * mapManager.Scale.X, worldPos.Y);
        int left = (int)(anchor.X - boxWidth / 2f);
        int top  = (int)(anchor.Y - MathF.Round(16 * mapManager.Scale.X * sy) - boxHeight);

        spriteBatch.Draw(_dummy, new Rectangle(left, top, boxWidth, boxHeight), rectColor * 0.5f);

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
            int barH = (int)MathF.Round(_font.MeasureString(bottomText).Y * sy);
            if(_cantAffordFlash > TimeSpan.Zero)
            {
                float t = (float)(_cantAffordFlash / FlashDuration);
                spriteBatch.Draw(_dummy, new Rectangle((int)pos.X, (int)pos.Y, (int)textW, barH), Color.Red * (t * 0.6f));
            }
            else
            {
                int fillW = (int)MathF.Round(textW * (float)(moveProgress / timeToMove));
                spriteBatch.Draw(_dummy, new Rectangle((int)pos.X, (int)pos.Y, fillW, barH), loadColor * 0.5f);
            }
            spriteBatch.DrawString(_font, bottomText, pos, textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
        }
    }
}

public class PlacableMeadow : Structure
{
    private int _baseSanityGain;
    public int SanityGain {get; private set;}

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];

    public PlacableMeadow((int,int) pos, int sanityGain)
        : base("meadowPlaced_idle", pos)
    {
        _baseSanityGain = sanityGain;
        SanityGain = sanityGain;
    }

    public void ActivateMeadow(int gain)
    {
        SanityGain = gain;
        SetAnimation("meadow_idle");
    }
    public void DeactivateMeadow()
    {
        SanityGain = _baseSanityGain;
        SetAnimation("meadowPlaced_idle");
    }
}
