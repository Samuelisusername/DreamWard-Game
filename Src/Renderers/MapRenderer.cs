using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DreamWardUtils.Graphics;

public class MapRenderer
{
    private MapManager _manager;

    public MapRenderer(MapManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// draws anything stationary, mainly all tiles
    /// (e.g. meadows, void tiles, fences, etc, and any static defence tiles).
    /// To get the tiles and entites to draw it calles the MapManager.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch)
    {
        for(int i = 0; i < _manager.Columns; i++)
        {
            for(int j = 0; j < _manager.Rows; j++)
            {
                TextureRegion tile = _manager.GetTile(i,j);
                
                Vector2 tilePos = _manager.GetWorldPositionOfTile(i,j);

                tile.Draw(spriteBatch, tilePos, Color.White, 0f, Vector2.Zero, _manager.Scale, SpriteEffects.None, 1f);
            }            
        }
    }
}