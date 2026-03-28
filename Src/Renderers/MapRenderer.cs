using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DreamWardUtils.Graphics;
using Gamelab;

public class MapRenderer
{
    private MapManager mapManager => GamelabGame.MapManager;
    private const bool _draw_tile_coordinates = false; // setting to true will draw the (column, row) of each tile


    /// <summary>
    /// draws anything stationary, mainly all tiles
    /// (e.g. meadows, void tiles, fences, etc, and any static defence tiles).
    /// To get the tiles and entites to draw it calles the MapManager.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch)
    {
        for(int i = 0; i < mapManager.Columns; i++)
        {
            for(int j = 0; j < mapManager.Rows; j++)
            {
                TextureRegion tile = mapManager.GetTile((i,j));
                
                Vector2 tilePos = mapManager.GetWorldPositionOfTile((i,j));

                tile.Draw(spriteBatch, tilePos, Color.White, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);
                if (_draw_tile_coordinates) {
                    DrawTileCoordinates(i, j, tilePos, spriteBatch);
                }
                
            }            
        }
    }

    /// <summary>
    /// Draws the coordinates (column, row) of each tile for debugging.
    /// </summary>
    private void DrawTileCoordinates(int column, int row, Vector2 tilePos, SpriteBatch spriteBatch)
    {
        string debugText = $"({column},{row})";

        // Center the debug text on the tile and scale it
        int fontSize = (int)(mapManager.Scale.X * 13);
        var font = GamelabGame.Instance.fontSystem.GetFont(fontSize);
        var textSize = font.MeasureString(debugText);
        Vector2 textCenter = new Vector2(
            tilePos.X + mapManager.TileWidth / 2 - textSize.X / 2,
            tilePos.Y + mapManager.TileHeight / 2 - textSize.Y / 2
        );
        font.DrawText(spriteBatch, debugText, textCenter, Color.White);
    }
}