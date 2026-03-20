using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Gamelab;

public class MapManager
{    
#region storage

    //all the possible Textures a tile can have are held in here
    private readonly TileSet _tileSet;

    //which tile of the tileset is display at this position?
    private readonly int[,] _tiles; //col first 

    //stores which tiles has which structure on it -1 if none
    private readonly int[,] _structures;//colfirst
    
    //stores which tiles has which monster on it -1 if none
    private readonly Entity[,] _monsters;//colfirst

#endregion

#region public properties

    /// <summary>
    /// The number of rows of the map
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// The number of columns of the map
    /// </summary>
    public int Columns { get; }
    
    /// <summary>
    /// The scale at which the map is rendered
    /// </summary>
    public Vector2 Scale { get; set; }

    /// <summary>
    /// The offset used to draw the map
    /// </summary>
    public Vector2 Offset {get; set;}
    
    /// <summary>
    /// The width of a single tile
    /// </summary>
    public float TileWidth => _tileSet.TileWidth * Scale.X;

    /// <summary>
    /// The height of a single tile
    /// </summary>
    public float TileHeight => _tileSet.TileHeight * Scale.Y;

    /// <summary>
    /// The region of the hexagon that overlaps with other hexagons
    /// </summary>
    public float ScaledOffset => TileHeight /2;
    
#endregion

#region animation fields 

    //the elapsed time since the start of the last animationframe
    private TimeSpan _elapsed;

    //the delay before the animationframe changes again
    private readonly TimeSpan _delay;

    //which frame of the animation are we on?
    private int _animationOffset = 0;

#endregion


    public MapManager(TileSet set, int columns, int rows, TimeSpan delay)
    {
        _tileSet = set;
        Columns = columns;
        Rows = rows;
        _delay = delay;
        _tiles = new int[Columns,Rows];
        _monsters = new Entity[Columns,Rows];
    }
    
    public void Update(GameTime gameTime)
    {   
        //progress the animation
        _elapsed += gameTime.ElapsedGameTime;
        if(_elapsed >= _delay)
        {
            _elapsed -= _delay;
            _animationOffset = (_animationOffset + 1) % 2;
        }
        //Update all the monsters
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
                ((TestMonster)_monsters[i,j])?.Update(gameTime);
            }
        }
    }

#region storage helper functions
    
    /// <summary>
    /// returns the Tile at the position
    /// </summary>
    public TextureRegion GetTile(int col, int row)
    {        
        return _tileSet.GetTile(_tiles[col,row] + _animationOffset);
    }

    /// <summary>
    /// returns the world position of the tile that is asked for
    /// </summary>
    public Vector2 GetWorldPositionOfTile(int col, int row)
    {
        float tileX = col * TileWidth - (col * ScaledOffset);
        float tileY = col % 2 == 0 ? row * TileHeight : row * TileHeight + ScaledOffset;

        return new Vector2(tileX, tileY) + Offset; //TODO: this returns the top left corner so far mybe change to something else
    }

    /// <summary>
    /// returns the Monster on the tile
    /// </summary>
    public Entity GetMonster(int col, int row)
    {        
        return _monsters[col,row];
    }

    /// <summary>
    /// moves the monsters in the tile speicified by origin to the tile specified by goal
    /// </summary>
    public void MoveMonster((int,int) origin, (int,int) goal)
    {
        _monsters[goal.Item1,goal.Item2] =  _monsters[origin.Item1,origin.Item2];
        _monsters[origin.Item1,origin.Item2] = null;
    }

    /// <summary>
    /// Fills the tileset randomly
    /// </summary>
    public void FillRandomly()
    {
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
                _tiles[i,j] = Random.Shared.Next(0,_tileSet.Count / 2) * 2;
            }
        }
    }

#endregion
    
#region Input handling

    /// <summary>
    /// returns the index of the tile that is at the position on screen
    /// </summary>
    /// <param name="position"></param>
    /// <returns>returns [col,row] of the tile at position. returns [-1,-1] if the position is not on the tilemap</returns>
    private (int,int) getTileIndex(Vector2 position)
    {
        position -= Offset;
        (int,int) res;
        //get the approximate column of the click
        int colAprox = (int)(position.X / (TileWidth - ScaledOffset));
        int isOddRow = colAprox % 2;
        //get the row if aprox is correct and the row if approx was shifter to the right
        int row = isOddRow  == 0? (int)Math.Floor(position.Y/TileHeight) : (int)Math.Floor((position.Y - ScaledOffset)/TileHeight);
        int rowL = isOddRow  == 1? (int)Math.Floor(position.Y/TileHeight) : (int)Math.Floor((position.Y - ScaledOffset)/TileHeight);
        float normalizedX = position.X - (colAprox*(TileWidth - (16*Scale.X)));
        if(normalizedX < ScaledOffset)
        {
            float normalizedY = position.Y - (row * TileHeight) - ScaledOffset - (isOddRow * ScaledOffset);
            if(normalizedX + (1 * Scale.X) >= normalizedY && normalizedX >= -normalizedY)
            {
                res = (colAprox,row);
            }
            else
            {                
                res = (colAprox-1,rowL);
            }
            
        }
        else
        {
            res = (colAprox,row);            
        }

        //check if the click is in on the map
        if(res.Item1 >= 0 && res.Item1 < Columns && res.Item2 >= 0 && res.Item2 < Rows)
        {
            return res;
        }
        else
        {
            return (-1,-1);
        }
    }

    /// <summary>
    /// Handles a click om the map, does not check if the click was actually on the map so far
    /// </summary>
    public void HandleClick(Vector2 position)
    {
        (int,int) index = getTileIndex(position);
        if(index.Item1 >= 0)
        {
            TestMonster newMonster = index.Item1 % 2 == 0 ?  new TestMonster(GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_walk"), this, TimeSpan.FromSeconds(2),index)
            {
                Path = [index, (index.Item1 - 1, index.Item2 - 1), (index.Item1 - 2, index.Item2 - 2), (index.Item1 - 2, index.Item2 - 3), (index.Item1 - 3, index.Item2 - 3), (index.Item1 - 4, index.Item2 - 4)]
            } :  new TestMonster(GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_fight"), this, TimeSpan.FromSeconds(2),index)
            {
                Path = [index, (index.Item1 - 1, index.Item2 - 1), (index.Item1 - 2, index.Item2 - 2), (index.Item1 - 2, index.Item2 - 3), (index.Item1 - 3, index.Item2 - 3), (index.Item1 - 4, index.Item2 - 4)]
            };
            _monsters[index.Item1,index.Item2] = newMonster;
        }

    }

    /// <summary>
    /// Moves the map into the direction of delta
    /// </summary>
    public void MoveMap(Vector2 delta)
    {
        Offset += delta;
    }

    /// <summary>
    /// Zooms the map by delta, towards the focus point
    /// </summary>
    public void Zoom(float delta, Vector2 focus)
    {
        //where are we zooming in on?
        Vector2 oldMapPoint = focus - Offset;
        //get the new scale
        Vector2 newScale = new Vector2(Math.Max(Scale.X + delta,0.5f), Math.Max(Scale.Y + delta,0.5f));
        //how much did the scale change?
        Vector2 scaleRatio = newScale / Scale;
        //figure out wherere the focused point is going to be once the map is scaled
        Vector2 newMapPoint = oldMapPoint * scaleRatio;
        //correct the offset so that the focus point stays at the same place on screen
        Vector2 correction = oldMapPoint - newMapPoint;
        Offset += correction;
        Scale = newScale;
        //scale all the monsters
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
                _monsters[i,j]?.SetScale(newScale);
            }
        }
    }
#endregion
}