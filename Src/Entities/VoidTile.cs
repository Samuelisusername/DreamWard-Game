using System;
using System.Collections.Generic;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;

/// <summary>
/// A void tile entity. Cannot be attacked or stepped on (except by flying monsters).
/// </summary>
public class VoidTile : Entity
{
    public VoidTile(MapManager mapManager, (int,int) pos)
        : base(MakeStaticSprite(mapManager), mapManager, pos, int.MaxValue)
    {
    }

    /// <summary>
    /// It's static since there's only one frame.
    /// </summary>
    private static AnimatedSprite MakeStaticSprite(MapManager mapManager)
    {
        var region = GamelabGame.textureAtlas["day_top"].GetRegion("void0");
        var anim = new Animation(new List<TextureRegion> { region }, TimeSpan.FromSeconds(1));
        return new AnimatedSprite(anim);
    }
}
