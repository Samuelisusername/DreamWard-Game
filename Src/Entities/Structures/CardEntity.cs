using Gamelab;

public abstract class CardEntity : Structure
{
    public CardEntity(string animName, (int,int) pos)
        : base(animName, pos)
    {
    }
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

