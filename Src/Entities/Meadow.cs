using Gamelab;

/// <summary>
/// A Meadow Tile Entity it can have a sheep on it or not
/// </summary>
public class Meadow : Entity
{
    public bool HasSheep{get; private set;}

    public int SanityGain {get; private set;}
    public Meadow(string animName, MapManager mapManager, (int,int) pos, int sanityGain)
        : base(animName, mapManager, pos, int.MaxValue, true)
    {
        SanityGain = sanityGain;
    }

    public void AddSheep()
    {
        HasSheep = true;
        _animName = "sheepSmall_idle";
        _sprite = GamelabGame.GamePhaseManager.IsDay ? 
        GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
        GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);

    }

    public void RemoveSheep()
    {
        HasSheep = false;
        _animName = "bush_idle";
        _sprite = GamelabGame.GamePhaseManager.IsDay ? 
        GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
        GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
    }
}

public class PlacableMeadow : Entity
{
    public int SanityGain {get; private set;}
    public PlacableMeadow(string animName, MapManager mapManager, (int,int) pos, int sanityGain)
        : base(animName, mapManager, pos, int.MaxValue, true)
    {
        SanityGain = sanityGain;
    }

    public void ActivateMeadow(int gain)
    {
        SanityGain = gain;
        _animName = "hill_idle";
        _sprite = GamelabGame.GamePhaseManager.IsDay ? 
        GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
        GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
    }
    public void DeactivateMeadow()
    {
        SanityGain = 5;
        _animName = "void_idle";
        _sprite = GamelabGame.GamePhaseManager.IsDay ? 
        GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
        GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
    }
}
