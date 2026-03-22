using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;

public class CardEntity : Entity
{
    public CardEntity(string animName, MapManager mapManager, (int,int) pos, int health = 100)
        : base(animName, mapManager, pos, health, true)
    {
    }
}

public class Tent : CardEntity
{
    public Tent(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, 12)
    {
    }
}

public class Sheep : CardEntity
{
    public Sheep(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, 12)
    {
    }
}

public class Rat : CardEntity
{
    public Rat(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, 12)
    {
    }
}

public class Meadow : CardEntity
{
    public Meadow(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, 1000)
    {
    }
}

public class Fence : CardEntity
{
    public Fence(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, 15)
    {
    }
}

public class Wall : CardEntity
{
    public Wall(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, 10)
    {
    }
}

