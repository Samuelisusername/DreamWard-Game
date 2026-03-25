using Autofac.Features.GeneratedFactories;
using Gamelab;

/// <summary>
/// A Meadow Tile Entity it can have a sheep on it or not
/// </summary>
public class Meadow : Entity
{
    public bool HasSheep{get; private set;}

    public bool IsPolymorphed{get; private set;}

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

    public void PolymorphSheep()
    {
        if(HasSheep)
        {
            IsPolymorphed = true;
            _animName = "rats_idle";
            _sprite = GamelabGame.GamePhaseManager.IsDay ? 
            GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
            GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
        }
    }

    public void UnpolymorphSheep()
    {
        if(HasSheep)
        {
            IsPolymorphed = false;
            _animName = "sheepSmall_idle";
            _sprite = GamelabGame.GamePhaseManager.IsDay ? 
            GamelabGame.textureAtlas["day_top"].CreateAnimatedSprite(_animName) :
            GamelabGame.textureAtlas["night_top"].CreateAnimatedSprite(_animName);
        }
    }
    

    /// <summary>
    /// Syncs the tent's HP with the current sanity value so the tent
    /// dies exactly when sanity reaches 0.
    /// </summary>
    public void SyncHealthWithSanity()
    {
        int sanity = Gamelab.GamelabGame.SanityManager?.Sanity ?? 0;
        _currentHealth = sanity;
        _maxHealth = sanity > _maxHealth ? sanity : _maxHealth;
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
