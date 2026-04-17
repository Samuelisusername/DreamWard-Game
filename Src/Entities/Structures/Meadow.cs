using Gamelab;
using Microsoft.Xna.Framework;

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

    public Meadow((int,int) pos, int sanityGain)
        : base("meadow_idle", pos, 50)
    {
        SanityGain = sanityGain;
        float lerpConst = .25f;
        
        if(SanityGain == 10)
        {
            _spriteTop.Color = Color.Lerp(Color.White, Color.Red, lerpConst);
            _spriteBase.Color = Color.Lerp(Color.White, Color.Red, lerpConst);
        }
        else if(SanityGain == 15)
        {
            _spriteTop.Color = Color.Lerp(Color.White, Color.Blue, lerpConst);
            _spriteBase.Color = Color.Lerp(Color.White, Color.Blue, lerpConst);
        }
        else if(SanityGain == 20)
        {
            _spriteTop.Color = Color.Lerp(Color.White, Color.Orange, lerpConst);
            _spriteBase.Color = Color.Lerp(Color.White, Color.Orange, lerpConst);
        }
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
}

public class PlacableMeadow : Structure
{
    public int SanityGain {get; private set;}
    public PlacableMeadow((int,int) pos, int sanityGain)
        : base("meadow_idle", pos, 5)
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
