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
    private bool _draw_tile_coordinates = false; // setting to true will draw the (column, row) of each tile
    private Sprite         _deadSheepDay;
    private AnimatedSprite _deadSheepNight;
    private static readonly Vector2 StructureLocalOffset = new Vector2(-32, -96);


    /// <summary>
    /// draws anything stationary, mainly all tiles
    /// (e.g. meadows, void tiles, fences, etc, and any static defence tiles).
    /// To get the tiles and entites to draw it calles the MapManager.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle screen, GameTime gameTime, (int col, int row)? skipTile = null, bool drawRange = false, CardRenderer cardRenderer = null)
    {
        _deadSheepNight?.Update(gameTime);
        int oX = 128 * (int)mapManager.Scale.X;
        int oY = 128 * (int)mapManager.Scale.Y;
        screen = new Rectangle(screen.X - oX , screen.Y - oY, screen.Width + 2*oX, screen.Height + 2*oY);
        if(gamePhaseManager.IsDay && gamePhaseManager.IsInTransition)
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
                    if(!(MapUtils.IsInBounds(localTilePos) && combatManager.PlayingField.Contains(localTilePos))
                        || mapManager.IsVoidFallingTile(localTilePos.col, localTilePos.row))
                    {
                        offset = mapManager.TilesOfsets[globalTilePos.col, globalTilePos.row];
                        offsetProgressInv = 1-(Math.Clamp((float)mapManager.FallTimer[globalTilePos.col, globalTilePos.row].TotalSeconds, 0 , (float)mapManager.MaxFallTime.TotalSeconds)/ (float)mapManager.MaxFallTime.TotalSeconds);
                    }                    
                    Vector2 worldPos  = mapManager.GetWorldPositionOfTile(localTilePos) + offset;
                    if(!screen.Contains(worldPos)) continue;

                    // TILEDRAWING

                    //get the tile and its position
                    TextureRegion tile = mapManager.GetTile(globalTilePos);
                    if (tile == null) continue;


                    Color tileColor = Color.White;
                    #if DEBUG
                    if(MapUtils.IsInBounds(localTilePos) && mapManager.Debug[localTilePos.col, localTilePos.row])
                        tileColor = Color.Red;
                    #endif
                    tile.Draw(spriteBatch, worldPos, tileColor * offsetProgressInv, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);
                    #if DEBUG
                    if (_draw_tile_coordinates)
                        DrawTileCoordinates(localTilePos.col, localTilePos.row, worldPos, spriteBatch);
                    #endif


                    //check if we draw the entity
                    if (skipTile.HasValue && skipTile.Value == localTilePos) continue;

                    //BASEDRAWING                
                    Structure s = mapManager.GetStructure(globalTilePos, true);
                    s?.DrawBase(spriteBatch, offset, offsetProgressInv);

                    //TOPDRAWING
                    s?.Draw(spriteBatch, offset, offsetProgressInv);

                    if (mapManager.SheepDied && localTilePos == mapManager.SheepPosition)
                    {
                        _deadSheepDay ??= GamelabGame.TextureAtlas["day_top"].CreateSprite("dead_sheep0");
                        _deadSheepDay.Scale = mapManager.Scale;
                        _deadSheepDay.Draw(spriteBatch, worldPos + StructureLocalOffset * mapManager.Scale, offsetProgressInv);
                    }

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
                    Vector2 worldPos  = mapManager.GetWorldPositionOfTile(localTilePos);
                    if(!screen.Contains(worldPos)) continue;
                    (int col, int row) globalTilePos = (j, i/2);
                    
                    if(!gamePhaseManager.IsDay && !(MapUtils.IsInBounds(localTilePos) && combatManager.PlayingField.Contains(localTilePos)))
                    {
                        continue;
                    }
                    
                    // TILEDRAWING
                    //get the tile and its position
                    TextureRegion tile = mapManager.GetTile(globalTilePos);
                    if (tile == null) continue;

                    Color tileColor = Color.White;
                    #if DEBUG
                    if(MapUtils.IsInBounds(localTilePos) && mapManager.Debug[localTilePos.col, localTilePos.row])
                        tileColor = Color.Red;
                    #endif
                    tile.Draw(spriteBatch, worldPos, tileColor, 0f, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 1f);
                    #if DEBUG
                    if (_draw_tile_coordinates)
                        DrawTileCoordinates(localTilePos.col, localTilePos.row, worldPos, spriteBatch);
                    #endif

                    //BASEDRAWING                
                    Structure s = mapManager.GetStructure(globalTilePos, true);
                    s?.DrawBase(spriteBatch);
                }            
            }
            
            if(drawRange && cardRenderer is not null)
            {
                cardRenderer.DrawAllTowerRanges(spriteBatch, mapManager);
            }

            for (int i = 0; i < mapManager.Rows*6; i++)
            {
                for (int j = i % 2; j < mapManager.Columns*3; j += 2)
                {       
                    
                    (int col, int row) localTilePos = (j - mapManager.Columns, (i / 2)-mapManager.Rows);
                    (int col, int row) globalTilePos = (j, i/2);

                    if(!gamePhaseManager.IsDay && !(MapUtils.IsInBounds(localTilePos) && combatManager.PlayingField.Contains(localTilePos)))
                    {
                        continue;
                    }

                    //GROUNDEDMONSTERDRAWING               
                    mapManager.GetMonsterContainer(localTilePos)?.DrawShadow(spriteBatch); 
                    mapManager.GetMonsterContainer(localTilePos)?.DrawGround(spriteBatch);

                    //TOPDRAWING
                    Structure s = mapManager.GetStructure(globalTilePos, true);
                    s?.Draw(spriteBatch);

                    if (mapManager.SheepDied && localTilePos == mapManager.SheepPosition)
                    {
                        Vector2 wp = mapManager.GetWorldPositionOfTile(localTilePos);
                        Vector2 drawPos = wp + StructureLocalOffset * mapManager.Scale;
                        if (gamePhaseManager.IsDay)
                        {
                            _deadSheepDay ??= GamelabGame.TextureAtlas["day_top"].CreateSprite("dead_sheep0");
                            _deadSheepDay.Scale = mapManager.Scale;
                            _deadSheepDay.Draw(spriteBatch, drawPos);
                        }
                        else
                        {
                            _deadSheepNight ??= GamelabGame.TextureAtlas["night_top"].CreateAnimatedSprite("dead_sheep_idle");
                            _deadSheepNight.Scale = mapManager.Scale;
                            _deadSheepNight.Draw(spriteBatch, drawPos);
                        }
                    }

                    //FLYINGMONSTERDRWAING
                    //mapManager.GetMonsterContainer(localTilePos)?.DrawAir(spriteBatch);
                }
            }
            
            for (int i = 0; i < mapManager.Rows*6; i++)
            {
                for (int j = i % 2; j < mapManager.Columns*3; j += 2)
                {       
                    
                    (int col, int row) localTilePos = (j - mapManager.Columns, (i / 2)-mapManager.Rows);

                    if(!gamePhaseManager.IsDay && !(MapUtils.IsInBounds(localTilePos) && combatManager.PlayingField.Contains(localTilePos)))
                    {
                        continue;
                    }
                    
                    //FLYINGMONSTERDRWAING
                    mapManager.GetMonsterContainer(localTilePos)?.DrawAir(spriteBatch);
                }
            }
        }
        //spriteBatch.DrawString(GamelabGame.Font, GamelabGame.InGameTime.ToString(), Vector2.One * 400, Color.Red);

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