using Gamelab;

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
    public Meadow(string animName, (int,int) pos, int sanityGain)
        : base(animName, pos, 50)
    {
        SanityGain = sanityGain;
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
        SetAnimation("bush_idle");
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
    public PlacableMeadow(string animName, (int,int) pos, int sanityGain)
        : base(animName, pos, 5)
    {
        SanityGain = sanityGain;
    }

    public void ActivateMeadow(int gain)
    {
        SanityGain = gain;
        SetAnimation("hill_idle");
    }
    public void DeactivateMeadow()
    {
        SanityGain = 5;
        SetAnimation("void_idle");
    }
}
