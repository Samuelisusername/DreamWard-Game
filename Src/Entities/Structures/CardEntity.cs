using Gamelab;

public abstract class CardEntity : Structure
{
    public CardEntity(string animName, (int,int) pos, int health = 100)
        : base(animName, pos, health)
    {
    }
}

public class Tent : CardEntity
{
    private SanityManager sanityManager => GamelabGame.SanityManager;

    public override bool CanMove => false;

    public Tent(string animName, (int,int) pos)
        : base(animName, pos, GamelabGame.SanityManager?.Sanity ?? 100)
    {
    }

    public override float CurrentHealth { get => sanityManager?.Sanity ?? 0; protected set => base.CurrentHealth = value; }
    public override float MaxHealth
    { 
        get => (sanityManager?.Sanity ?? 0) > base.MaxHealth ? sanityManager?.Sanity ?? 0 : base.MaxHealth;
        protected set => base.MaxHealth = value; 
    }
}

public class Sheep : CardEntity
{
    public Sheep(string animName, (int,int) pos)
        : base(animName, pos, 12)
    {
    }
}

public class Rat : CardEntity
{
    public Rat(string animName, (int,int) pos)
        : base(animName, pos, 12)
    {
    }
}

// public class Meadow : CardEntity
// {
//     public Meadow(string animName, MapManager mapManager, (int,int) pos)
//         : base(animName, mapManager, pos, 1000)
//     {
//     }
// }

public class Fence : CardEntity
{
    public Fence((int,int) pos)
        : base("fence_idle", pos, 15)
    {
    }
}

public class Wall : CardEntity
{
    public Wall((int,int) pos)
        : base("wall_idle", pos, 10)
    {
    }
}

