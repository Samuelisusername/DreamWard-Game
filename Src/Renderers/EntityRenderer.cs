using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework.Graphics;

public class EntityRenderer
{
    private MapManager _manager;

    public EntityRenderer(MapManager manager)
    {
        _manager = manager;
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
                TestMonster monster = (TestMonster)_manager.GetMonster(i,j);
                monster?.Draw(spriteBatch);
            }            
        }
    }
}