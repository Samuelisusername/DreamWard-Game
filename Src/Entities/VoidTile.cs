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
    public VoidTile(string animName, MapManager mapManager, (int,int) pos)
        : base(animName, mapManager, pos, int.MaxValue)
    {
    }
}
