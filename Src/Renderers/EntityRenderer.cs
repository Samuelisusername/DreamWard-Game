using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

public class EntityRenderer
{
    private MapManager _manager;

    public EntityRenderer(MapManager mapManager)
    {
        _manager = mapManager;
    }


    /// <summary>
    /// draws all entities (e.g. the player, monsters, sheep, rats) and 
    /// anything else that moves (attack projectiles, health lost by monsters, dynamic defences, effects etc).
    /// To get the entites to draw it calles the MapManager.
    /// </summary>
    /// <param name="gameTime"></param>
    public void Draw(SpriteBatch spriteBatch)
    {
        for(int i = 0; i < _manager.Columns; i++)
        {
            for(int j = 0; j < _manager.Rows; j++)
            {
                _manager.GetMonster(i,j)?.Draw(spriteBatch);
                _manager.GetStructure(i,j)?.Draw(spriteBatch);
            }            
        }
    }
}