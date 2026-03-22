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
    public WallTile(string animName, MapManager mapManager, (int,int) pos, int health = 50)
        : base(animName, mapManager, pos, health)
    {
    }
}
