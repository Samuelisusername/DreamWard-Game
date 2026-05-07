using System;
using System.Linq;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

/// <summary>
/// A Meadow Tile Entity it can have a sheep on it or not
/// </summary>
public class Meadow : Structure
{
    private static int MaxGrazingDays => GamelabGame.Instance.Balance.MaxGrazingDays;
    private SanityManager sanityManager => GamelabGame.SanityManager;
    public bool HasSheep { get; private set; }

    public bool IsPolymorphed { get; private set; }

    private int _baseSanityGain;
    public int SanityGain => IsDepleted ? 0 : _baseSanityGain;

    public bool IsGrazed { get; private set; }

    public int DaysGrazed { get; private set; }
    public bool IsDepleted { get; private set; }

    /// <summary>
    /// The sprite name of the card this meadow unlocks when the sheep survives one night here.
    /// Null means no card unlock (e.g. the Orange +10 Sanity meadow uses BonusSanity instead).
    /// </summary>
    public List<string> UnlockCardSprites { get; set; } = new();

    /// <summary>
    /// One-time flat sanity bonus granted when the sheep survives its first night here.
    /// 0 means no sanity bonus.
    /// </summary>
    public int BonusSanity { get; set; }

    /// <summary>
    /// True once the bonus (card unlock and/or sanity) has already been granted.
    /// </summary>
    public bool BonusGranted { get; private set; }

    public override bool CanMove => false;
    protected override (string label, string info)[] infos => !gamePhaseManager.IsInTransition && !IsGrazed && !HasSheep && mapManager.GetSheep() != null ?
     [("Health:", $"{CurrentHealth}"), ("Sanity gain:", $"{SanityGain}"), ("Cost to move:", $"{(int)MathF.Floor(MapUtils.CalculateTileDistance(TilePosition, mapManager.SheepPosition) * MapManager.MoveCostMultiplier)}")] :
     [("Health:", $"{CurrentHealth}"), ("Sanity gain:", $"{SanityGain}")];

    protected override string bottomText => !gamePhaseManager.IsInTransition && !HasSheep && mapManager.GetSheep() != null ? !IsGrazed ? "Click and hold to move sheep here" : "Cant move sheep to grazed spot" : null;

    protected TimeSpan timeToMove = TimeSpan.FromSeconds(1.5f);
    protected TimeSpan moveProgress = TimeSpan.Zero;

    private bool resetMoveProgress = true;
    private TimeSpan _cantAffordFlash = TimeSpan.Zero;
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(0.5);

    private static string SpriteForGain(int sanityGain)
    {
        var gains = GamelabGame.Instance.Balance.MeadowSanityGains;
        if (gains != null)
            foreach (var kvp in gains)
                if (kvp.Value == sanityGain) return kvp.Key;
        return "meadow_idle";
    }

    public Meadow((int, int) pos, int sanityGain)
        : base(SpriteForGain(sanityGain), pos)
    {
        _baseSanityGain = sanityGain;
    }

    public void TickGrazingDay()
    {
        if (!HasSheep || IsDepleted) return;
        DaysGrazed++;

        // Grant the meadow bonus once, on the first day tick (sheep survived one night).
        if (DaysGrazed == 1 && !BonusGranted)
        {
            TryGrantBonus();
        }

        if (DaysGrazed >= MaxGrazingDays)
        {
            IsDepleted = true;
            IsGrazed = true;
            SetAnimationBase("meadowGrazed_idle");
            SetAnimationTop("sheepSmall_idle");
        }
    }

    private void TryGrantBonus()
    {
        BonusGranted = true;

        if (BonusSanity > 0)
            sanityManager.AddSanity(BonusSanity);

        if (MeadowGrant != null)
        {
            MeadowGrant(this);
            MeadowGrant       = null;
            UnlockCardSprites = new();
            BonusLabel        = null;
        }
    }
    /// <summary>
    public Action<Meadow> MeadowGrant { get; set; }
    public string BonusLabel { get; set; }

    public void TriggerCantAffordFlash() => _cantAffordFlash = FlashDuration;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (resetMoveProgress)
        { 
            moveProgress = TimeSpan.Zero;
        }
        resetMoveProgress = true;
        if (_cantAffordFlash > TimeSpan.Zero)
            _cantAffordFlash -= gameTime.ElapsedGameTime;
    }

    public void AddSheep(bool isPolymorphed = false)
    {
        HasSheep = true;
        IsPolymorphed = isPolymorphed;
        DaysGrazed = 0;
        if (IsPolymorphed)
            SetAnimationTop("rats_idle");
        else
            SetAnimationTop("sheepSmall_idle");
    }

    public bool AddProgress(TimeSpan delta)
    {
        if (HasSheep || mapManager.GetSheep() == null) return false;
        resetMoveProgress = false;
        moveProgress += delta;
        if (moveProgress > timeToMove)
        {
            return true;
        }
        return false;
    }

    public void RemoveSheep()
    {
        HasSheep = false;
        if (IsDepleted)
        {
            IsGrazed = true;
            SetAnimation("meadowGrazed_idle");
        }
        else
        {
            SetAnimation(_animNameBase);
        }
    }

    public void PolymorphSheep()
    {
        if (HasSheep)
        {
            IsPolymorphed = true;
            SetAnimationTop("rats_idle");
        }
    }

    public void TryUnpolymorphSheep()
    {
        if (HasSheep)
        {
            IsPolymorphed = false;
            SetAnimationTop("sheepSmall_idle");
        }
    }
    public override void DrawCard(SpriteBatch spriteBatch, Rectangle screen)
    {
        if (!ShouldDrawCard) return;

        Color rectColor = gamePhaseManager.IsDay ? COLOR_L : new Color(33, 28, 11);
        Color loadColor = gamePhaseManager.IsDay ? COLOR_G : new Color(33, 28, 11);
        Color textColor = gamePhaseManager.IsDay ? COLOR_D : new Color(214, 214, 214);
        Color goldColor = new Color(255, 210, 80);
        Color dimColor  = textColor * 0.55f;

        float sx = MathF.Max(1f, MathF.Floor(_screenTransform.M11));
        float sy = MathF.Max(1f, MathF.Floor(_screenTransform.M22));

        Vector2 worldPos  = mapManager.GetWorldPositionOfTile(TilePosition);
        int pad = 10, midPad = 50, heightPad = 50;
        int sepH = (int)MathF.Round(2 * sy);

        bool hasBonusSection = gamePhaseManager.IsDay && MeadowGrant != null;
        bool showBonusLabel  = hasBonusSection && !string.IsNullOrEmpty(BonusLabel);
        string sectionHeader = "Survive the night here to unlock:";

        // ── Build card blocks once ─────────────────────────────────────────────
        var cardBlocks = new List<(string title, string[] body)>();
        if (hasBonusSection && UnlockCardSprites?.Count > 0)
        {
            foreach (var name in UnlockCardSprites)
            {
                var parts = CardRenderer.CardDescriptions.TryGetValue(name, out var d)
                    ? d.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    : new[] { name };
                cardBlocks.Add((parts[0], parts.Length > 1 ? parts[1..] : Array.Empty<string>()));
            }
        }

        // ── Local helpers ──────────────────────────────────────────────────────
        float MeasW(string s) => _font.MeasureString(s).X;
        float MeasH(string s) => _font.MeasureString(s).Y;
        int   ScW(string s)   => (int)MathF.Round((MeasW(s) + 2 * pad) * sx);
        int   ScH(string s)   => (int)MathF.Round((MeasH(s) + pad) * sy);

        Vector2 ToScreen(float x, float y) => new(
            MathF.Round(x * _screenTransform.M11 + _screenTransform.M41),
            MathF.Round(y * _screenTransform.M22 + _screenTransform.M42));

        // ── Box sizing ─────────────────────────────────────────────────────────
        int boxWidth = (int)MathF.Round(
            (infos.Cast<(string,string)>().Max(i => MeasW(i.Item1 + i.Item2)) + 2 * pad + midPad) * sx);
        int boxHeight = (int)MathF.Round(
            (infos.Cast<(string,string)>().Sum(i => MeasH(i.Item1)) + pad * (infos.Length + 1)) * sy);

        if (bottomText != null)
        {
            boxWidth   = Math.Max(boxWidth, ScW(bottomText));
            boxHeight += (int)MathF.Round((MeasH(bottomText) + heightPad) * sy);
        }

        if (hasBonusSection)
        {
            boxHeight += (int)MathF.Round(pad * sy) + sepH + (int)MathF.Round(pad * sy);
            boxWidth   = Math.Max(boxWidth, ScW(sectionHeader));
            boxHeight += ScH(sectionHeader);

            foreach (var (title, body) in cardBlocks)
            {
                boxWidth   = Math.Max(boxWidth, ScW(title));
                boxHeight += ScH(title);
                foreach (var line in body) { boxWidth = Math.Max(boxWidth, ScW(line)); boxHeight += ScH(line); }
                boxHeight += (int)MathF.Round(pad * sy);
            }

            if (showBonusLabel) { boxWidth = Math.Max(boxWidth, ScW(BonusLabel)); boxHeight += ScH(BonusLabel); }
        }

        // ── Layout anchors ─────────────────────────────────────────────────────
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

        // ── Draw helpers ───────────────────────────────────────────────────────
        float yOff = MathF.Round(pad * sy);
        void DrawCentred(string s, Color c) {
            spriteBatch.DrawString(_font, s,
                new Vector2((int)(anchor.X - MeasW(s) * sx / 2f), (int)(top + yOff)),
                c, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(s) + pad) * sy);
        }
        void DrawSep() {
            spriteBatch.Draw(_dummy,
                new Rectangle(left + (int)MathF.Round(pad * sx), (int)(top + yOff),
                            boxWidth - (int)MathF.Round(2 * pad * sx), sepH), dimColor);
            yOff += sepH;
        }

        // ── Stat rows ──────────────────────────────────────────────────────────
        for (int i = 0; i < infos.Length; i++)
        {
            spriteBatch.DrawString(_font, infos[i].label,
                new Vector2(left + MathF.Round(pad * sx), top + yOff),
                textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            float infoW = MeasW(infos[i].info) * sx;
            spriteBatch.DrawString(_font, infos[i].info,
                new Vector2((int)(left + boxWidth - MathF.Round(pad * sx) - infoW), top + yOff),
                textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(infos[i].info) + pad) * sy);
        }

        // ── Hold-to-move bar ───────────────────────────────────────────────────
        if (bottomText != null)
        {
            float textW = MeasW(bottomText) * sx;
            int   barH  = (int)MathF.Round(MeasH(bottomText) * sy);
            var   pos   = new Vector2((int)(anchor.X - textW / 2f),
                                    (int)(top + yOff - MathF.Round(pad * sy) + MathF.Round(heightPad * sy)));
            Color barColor = _cantAffordFlash > TimeSpan.Zero
                ? Color.Red * ((float)(_cantAffordFlash / FlashDuration) * 0.6f)
                : loadColor * 0.5f;
            int fillW = _cantAffordFlash > TimeSpan.Zero
                ? (int)textW
                : (int)MathF.Round(textW * (float)(moveProgress / timeToMove));
            spriteBatch.Draw(_dummy, new Rectangle((int)pos.X, (int)pos.Y, fillW, barH), barColor);
            spriteBatch.DrawString(_font, bottomText, pos, textColor,
                0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(bottomText) + heightPad) * sy);
        }

        // ── Bonus section ──────────────────────────────────────────────────────
        if (hasBonusSection)
        {
            yOff += MathF.Round(pad * sy);
            DrawSep();
            yOff += MathF.Round(pad * sy);
            DrawCentred(sectionHeader, dimColor);
            foreach (var (title, body) in cardBlocks)
            {
                DrawCentred(title, goldColor);
                foreach (var line in body) DrawCentred(line, textColor);
                yOff += MathF.Round(pad * sy);
            }
            if (showBonusLabel) DrawCentred(BonusLabel, goldColor);
        }
    }

}

public class PlacableMeadow : Structure
{
    private int _baseSanityGain;
    public int SanityGain { get; private set; }

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];

    public PlacableMeadow((int, int) pos, int sanityGain)
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