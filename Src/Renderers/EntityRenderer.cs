using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

public class EntityRenderer
{
    private MapManager _manager;

    private List<Entity> _entities;

    public EntityRenderer(MapManager mapManager, List<Entity> entities)
    {
        _manager = mapManager;
        _entities = entities;
    }

    public void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Update all entities
        foreach (var entity in _entities)
        {
            entity.Update(gameTime);
        }

        // Remove destroyed ones
        _entities.RemoveAll(e => e.IsDestroyed);
    }

    /// <summary>
    /// draws all entities (e.g. the player, monsters, sheep, rats) and 
    /// anything else that moves (attack projectiles, health lost by monsters, dynamic defences, effects etc).
    /// To get the entites to draw it calles the MapManager.
    /// </summary>
    /// <param name="gameTime"></param>
    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var entity in _entities)
        {
            entity.Draw(spriteBatch);
        }
        for(int i = 0; i < _manager.Columns; i++)
        {
            for(int j = 0; j < _manager.Rows; j++)
            {
                _manager.GetMonster(i,j)?.Draw(spriteBatch);
            }            
        }
    }
}