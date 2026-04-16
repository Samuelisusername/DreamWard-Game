using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DreamWardUtils.Graphics;
using Gamelab;
using System;

public class MapRenderer
{
    private MapManager mapManager => GamelabGame.MapManager;
    private CombatManager combatManager => GamelabGame.CombatManager;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    private const bool _draw_tile_coordinates = false; // setting to true will draw the (column, row) of each tile
    

    /// <summary>
    /// draws anything stationary, mainly all tiles
    /// (e.g. meadows, void tiles, fences, etc, and any static defence tiles).
    /// To get the tiles and entites to draw it calles the MapManager.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, (int col, int row)? skipTile = null)
    {
        if(gamePhaseManager.IsDay)
        {
            for (int i = 0; i < mapManager.Rows*6; i++)
            {
                for (int j = i % 2; j < mapManager.Columns*3; j += 2)
                {                
                    //we draw the whole map including entities in the same loop to facilitate the tiles falling
                    //the order of drawing is:
                    // 1. the map tiles
                    // 2. top_base
                    // 3. Grounded Monsters
                    // 4. top
                    // 5. Flying Monsters

                    // the tileposition in the array
                    (int col, int row) localTilePos = (j - mapManager.Columns, (i / 2)-mapManager.Rows);
                    (int col, int row) globalTilePos = (j, i/2);
                    //the offset from the falling anim
                    Vector2 offset = Vector2.Zero;
                    float offsetProgressInv = 1;
                    if(!(MapUtils.IsInBounds(localTilePos) && combatManager.PlayingField.Contains(localTilePos)))
                    {
                        //if the tile is outside the playing fild add the falling offset
                        offset = mapManager.TilesOfsets[globalTilePos.col, globalTilePos.row];
                        offsetProgressInv = 1-(Math.Clamp((float)mapManager.FallTimer[globalTilePos.col, globalTilePos.row].TotalSeconds, 0 , (float)mapManager.MaxFallTime.TotalSeconds)/ (float)mapManager.MaxFallTime.TotalSeconds);
                    }
                    
                    // TILEDRAWING

                    //get the tile and its position
                    TextureRegion tile = mapManager.GetTile(globalTilePos);
                    Vector2 worldPos  = mapManager.GetWorldPositionOfTile(localTilePos);

                    #if DEBUG
                    if(MapUtils.IsInBounds(localTilePos) && mapManager.Debug[localTilePos.col, localTilePos.row])
                    {
                        tile.Draw(spriteBatch, worldPos + (offset * mapManager.Scale), Color.Red * offsetProgressInv, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);
                        if (_draw_tile_coordinates) {
                            DrawTileCoordinates(localTilePos.col, localTilePos.row, worldPos, spriteBatch);
                        }
                    }
                    else
                    {
                        tile.Draw(spriteBatch, worldPos + (offset * mapManager.Scale), Color.White * offsetProgressInv, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);
                        if (_draw_tile_coordinates) {
                            DrawTileCoordinates(localTilePos.col, localTilePos.row, worldPos, spriteBatch);
                        }
                    }
                    #else

                    tile.Draw(spriteBatch, worldPos + (offset * mapManager.Scale), Color.White * offsetProgressInv, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);

                    #endif


                    //check if we draw the entity
                    if (skipTile.HasValue && skipTile.Value == localTilePos) continue;

                    //BASEDRAWING                
                    Structure s = mapManager.GetStructure(globalTilePos, true);                
                    if(s?.IsFlat ?? false)
                    {
                        s.Draw(spriteBatch, offset, offsetProgressInv);
                    }

                    //GROUNDEDMONSTERDRAWING                
                    mapManager.GetMonsterContainer(localTilePos)?.DrawGround(spriteBatch);

                    //TOPDRAWING
                    s = mapManager.GetStructure(globalTilePos, true);
                    if(!(s?.IsFlat ?? true))
                    {
                        s.Draw(spriteBatch, offset, offsetProgressInv);
                    }

                    //FLYINGMONSTERDRWAING
                    mapManager.GetMonsterContainer(localTilePos)?.DrawAir(spriteBatch);

                }            
            }
        }
        else
        {
            for (int i = 0; i < mapManager.Rows*6; i++)
            {
                for (int j = i % 2; j < mapManager.Columns*3; j += 2)
                {
                    // the tileposition in the array
                    (int col, int row) localTilePos = (j - mapManager.Columns, (i / 2)-mapManager.Rows);
                    (int col, int row) globalTilePos = (j, i/2);
                    
                    if(!(MapUtils.IsInBounds(localTilePos) && combatManager.PlayingField.Contains(localTilePos)))
                    {
                        continue;
                    }
                    
                    // TILEDRAWING
                    //get the tile and its position
                    TextureRegion tile = mapManager.GetTile(globalTilePos);
                    Vector2 worldPos  = mapManager.GetWorldPositionOfTile(localTilePos);

                    #if DEBUG
                    if(MapUtils.IsInBounds(localTilePos) && mapManager.Debug[localTilePos.col, localTilePos.row])
                    {
                        tile.Draw(spriteBatch, worldPos, Color.Red, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);
                        if (_draw_tile_coordinates) {
                            DrawTileCoordinates(localTilePos.col, localTilePos.row, worldPos, spriteBatch);
                        }
                    }
                    else
                    {
                        tile.Draw(spriteBatch, worldPos, Color.White, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);
                        if (_draw_tile_coordinates) {
                            DrawTileCoordinates(localTilePos.col, localTilePos.row, worldPos, spriteBatch);
                        }
                    }
                    #else

                    tile.Draw(spriteBatch, worldPos, Color.White, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);

                    #endif
                }            
            }
            
            
            for (int i = 0; i < mapManager.Rows*6; i++)
            {
                for (int j = i % 2; j < mapManager.Columns*3; j += 2)
                {       
                    
                    (int col, int row) localTilePos = (j - mapManager.Columns, (i / 2)-mapManager.Rows);
                    (int col, int row) globalTilePos = (j, i/2);

                    if(!(MapUtils.IsInBounds(localTilePos) && combatManager.PlayingField.Contains(localTilePos)))
                    {
                        continue;
                    }

                    //check if we draw the entity
                    if (skipTile.HasValue && skipTile.Value == localTilePos) continue;

                    //BASEDRAWING                
                    Structure s = mapManager.GetStructure(globalTilePos, true);                
                    if(s?.IsFlat ?? false)
                    {
                        s.Draw(spriteBatch);
                    }

                    //GROUNDEDMONSTERDRAWING                
                    mapManager.GetMonsterContainer(localTilePos)?.DrawGround(spriteBatch);

                    //TOPDRAWING
                    s = mapManager.GetStructure(globalTilePos, true);
                    if(!(s?.IsFlat ?? true))
                    {
                        s.Draw(spriteBatch);
                    }

                    //FLYINGMONSTERDRWAING
                    mapManager.GetMonsterContainer(localTilePos)?.DrawAir(spriteBatch);
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