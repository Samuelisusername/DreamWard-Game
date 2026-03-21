using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;

public class CardEntity : Entity
{
    public CardEntity(AnimatedSprite sprite, MapManager mapManager, (int,int) pos, int health = 100)
        : base(sprite, mapManager, pos, health)
    {
    }
}

public class Tent : CardEntity
{
    public Tent(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 12) 
    {
    }
}

public class Sheep : CardEntity
{
    public Sheep(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 12)
    {
    }
}

public class Rat : CardEntity
{
    public Rat(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 12)
    {
    }
}

public class Meadow : CardEntity
{
    public Meadow(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 1000)
    {
    }
}

public class Fence : CardEntity
{
    public Fence(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 15)
    {
    }
}

public class Wall : CardEntity
{
    public Wall(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 10)
    {
    }
}

public class SniperTower : CardEntity
{
    public SniperTower(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 7)
    {
    }
}

public class AOETower : CardEntity
{
    public AOETower(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 7)
    {
    }
}