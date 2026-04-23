using Gamelab;

public abstract class CardEntity : Structure
{
    public CardEntity(string animName, (int,int) pos)
        : base(animName, pos)
    {
    }
}

public class Tent : CardEntity
{
    private SanityManager sanityManager => GamelabGame.SanityManager;

    public override bool CanMove => false;

    public Tent(string animName, (int,int) pos)
        : base(animName, pos)
    {
    }
    
    public override float CurrentHealth { get => sanityManager?.Sanity ?? 0; protected set => base.CurrentHealth = value; }
    public override float MaxHealth
    { 
        get => (sanityManager?.Sanity ?? 0) > base.MaxHealth ? sanityManager?.Sanity ?? 0 : base.MaxHealth;
        protected set => base.MaxHealth = value; 
    }

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}

public class Fence : CardEntity
{
    public Fence((int,int) pos)
        : base("fence_idle", pos)
    {
    }

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}

public class Wall : CardEntity
{
    public Wall((int,int) pos)
        : base("wall_idle", pos)
    {
    }

    protected override (string, string)[] infos => [("Health:", $"{CurrentHealth}")];
}

