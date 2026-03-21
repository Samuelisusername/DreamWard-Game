using System;
using System.Collections.Generic;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;

/// <summary>
/// A wall tile entity. Can be attacked and stepped on.
/// Regular monsters must attack it first; flying monsters ignore it.
/// </summary>
public class WallTile : Entity
{
    public WallTile(MapManager mapManager, (int,int) pos, int health = 50)
        : base(MakeStaticSprite(mapManager), mapManager, pos, health)
    {
    }
    private static AnimatedSprite MakeStaticSprite(MapManager mapManager)
    {
        var region = GamelabGame.textureAtlas["day_top"].GetRegion("wall0");
        var anim = new Animation(new List<TextureRegion> { region }, TimeSpan.FromSeconds(1));
        return new AnimatedSprite(anim);
    }
}
