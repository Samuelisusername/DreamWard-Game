using System;
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
    private SanityManager _sanityManager;

    public Tent(AnimatedSprite sprite, MapManager mapManager, (int,int) pos)
        : base(sprite, mapManager, pos, 12)
    {
    }

    public void SetSanityManager(SanityManager sanityManager)
    {
        _sanityManager = sanityManager;
    }

    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);
        if (_sanityManager != null)
            _sanityManager.Sanity = Math.Max(0, (int)Math.Ceiling(_currentHealth));
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

