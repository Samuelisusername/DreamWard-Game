using System;
using System.Linq;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using DreamWardUtils.Graphics;

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
    public override (string label, string info)[] Infos => !gamePhaseManager.IsInTransition && !IsGrazed && !HasSheep && mapManager.GetSheep() != null ?
     [("Sanity gain/day:", $"+{SanityGain}[SANITY]"), ("Cost to move:", $"-{(int)MathF.Round(MapUtils.CalculateTileDistance(TilePosition, mapManager.SheepPosition) * MapManager.MoveCostMultiplier)}[SANITY]"), ("Flowers left:", $"{MaxGrazingDays - DaysGrazed}/{MaxGrazingDays}")] :
     !gamePhaseManager.IsInTransition && !IsGrazed && HasSheep && mapManager.GetSheep() != null ?
     [("Sheep health:", $"{(int)CurrentHealth}/{(int)MaxHealth}"), ("Sanity gain/day:", $"+{SanityGain}[SANITY]"), ("Flowers left:", $"{MaxGrazingDays - DaysGrazed}/{MaxGrazingDays}")] :
     [("Sanity gain/day:", $"+{SanityGain}[SANITY]")];

    public override string BottomText => !gamePhaseManager.IsInTransition && !HasSheep && mapManager.GetSheep() != null ? !IsGrazed ? "Click and hold to move sheep here" : "Can't move sheep to grazed spot" : null;

    private bool resetMoveProgress = true;
    private Sprite _portrait ;


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
        _portrait = GamelabGame.TextureAtlas["day_top"].CreateSprite("sheepBig0");
        _portrait.Scale = Vector2.One * 2;
        _portrait.Origin = new Vector2(49,112);
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
            foreach (var pos in MapUtils.GetSurroundingTiles(TilePosition))
                if (mapManager.Structures[pos.Item1, pos.Item2] is PlacableMeadow pm)
                    pm.DeactivateMeadow();
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

    public void TriggerCantAffordFlash() => CantAffordFlash = FlashDuration;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (resetMoveProgress)
        { 
            BarProgress = TimeSpan.Zero;
        }
        resetMoveProgress = true;
        if (CantAffordFlash > TimeSpan.Zero)
            CantAffordFlash -= gameTime.ElapsedGameTime;
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
        if (HasSheep || mapManager.GetSheep() == null || TutorialManager.CurrentInfoStructure != this) return false;
        resetMoveProgress = false;
        BarProgress += delta;
        if (BarProgress > BarComplete)
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
}

public class PlacableMeadow : Structure
{
    private int _baseSanityGain;
    public int SanityGain { get; private set; }

    public override (string, string)[] Infos => [("Sanity gain:", $"{SanityGain}")];

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