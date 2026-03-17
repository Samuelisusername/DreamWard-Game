using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Gamelab.Screens;

public class MapManager
{    
    private readonly TileSet _tileSet;


    private readonly int[,] _tiles; //col first 

    //stores which tiles has which structure on it -1 if none
    private readonly int[,] _structures;//colfirst 

    
    //stores which tiles has which monster on it -1 if none
    private readonly Entity[,] _monsters;//colfirst



    public int Rows { get; }

    public int Columns { get; }

    public Vector2 Scale { get; set; }

    public float TileWidth => _tileSet.TileWidth * Scale.X;

    public float TileHeight => _tileSet.TileHeight * Scale.Y;

    public float ScaledOffset => TileHeight /2;
    
    private TimeSpan _elapsed;

    public TimeSpan _delay;

    private int _animationOffset = 0;

    public Vector2 Offset;

    public MapManager(TileSet set, int columns, int rows, TimeSpan delay)
    {
        _delay = delay;
        _tileSet = set;
        Rows = rows;
        Columns = columns;
        Scale = Vector2.One;
        _tiles = new int[Columns,Rows];
        _monsters = new Entity[Columns,Rows];
    }

    public void SetTile(int col, int row, int tileSetID)
    {
        _tiles[col,row] = tileSetID;
    }

    public TextureRegion GetTile(int col, int row)
    {        
        return _tileSet.GetTile(_tiles[col,row] + _animationOffset);
    }

    public Vector2 GetWorldPositionOfTile(int col, int row)
    {
        float tileX = col * TileWidth - (col * ScaledOffset);
        float tileY = col % 2 == 0 ? row * TileHeight : row * TileHeight + ScaledOffset;

        return new Vector2(tileX, tileY) + Offset; //TODO: this returns the top left corner so far mybe change to something else
    }

    public void SetStructure(int col, int row, int structureSetId)
    {
        _structures[col,row] = structureSetId;
    }

    public void SetMonster(int col, int row, Entity monster)
    {
        _monsters[col,row] = monster;
    }

    public Entity GetMonster(int col, int row)
    {        
        return _monsters[col,row];
    }

    public void MoveMonster((int,int) origin, (int,int) goal)
    {
        _monsters[goal.Item1,goal.Item2] =  _monsters[origin.Item1,origin.Item2];
        _monsters[origin.Item1,origin.Item2] = null;
    }

    public void Update(GameTime gameTime)
    {
        _elapsed += gameTime.ElapsedGameTime;

        if(_elapsed >= _delay)
        {
            _elapsed -= _delay;
            _animationOffset = (_animationOffset + 1) % 2;
        }
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
                ((TestMonster)(_monsters[i,j]))?.Update(gameTime);
            }
        }
    }

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

    /// <summary>
    /// returns the index of the tile that is at the position on screen
    /// </summary>
    /// <param name="position"></param>
    /// <returns>returns [col,row] of the tile at position. returns [-1,-1] if the position is not on the tilemap</returns>
    private (int,int) getTileIndex(Vector2 position)
    {
        position -= Offset;
        (int,int) res;
        int colAprox = (int)(position.X / (TileWidth - ScaledOffset));
        int isOddRow = colAprox % 2;
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

        if(res.Item1 >= 0 && res.Item1 < Columns && res.Item2 >= 0 && res.Item2 < Rows)
        {
            return res;
        }
        else
        {
            return (-1,-1);
        }
    }

    public void HandleClick(Vector2 position)
    {
        (int,int) index = getTileIndex(position);
        if(index.Item1 >= 0)
        {
            TestMonster newMonster = new TestMonster(HexMapScreen.textureAtlas["monsters"].CreateAnimatedSprite("fly-animation"), this, TimeSpan.FromSeconds(2),index)
            {
                Path = [index, (index.Item1 - 1, index.Item2 - 1), (index.Item1 - 2, index.Item2 - 2), (index.Item1 - 2, index.Item2 - 3), (index.Item1 - 3, index.Item2 - 3), (index.Item1 - 4, index.Item2 - 4)]
            };
            _monsters[index.Item1,index.Item2] = newMonster;

            _tiles[index.Item1,index.Item2] = _tiles[index.Item1,index.Item2] > 1 ? _tiles[index.Item1,index.Item2] - 2 : _tiles[index.Item1,index.Item2] +2;
        }

    }

    public void MoveMap(Vector2 delta)
    {
        Offset += delta;
    }

    public void Zoom(float delta, Vector2 focus)
    {
        Vector2 oldMapPoint = focus - Offset;
        Vector2 newScale = new Vector2(Math.Max(Scale.X + delta,0.5f), Math.Max(Scale.Y + delta,0.5f));
        Vector2 scaleRatio = newScale / Scale;
        Vector2 newMapPoint = oldMapPoint * scaleRatio;
        Vector2 correction = oldMapPoint - newMapPoint;
        Offset += correction;
        Scale = newScale;
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
                _monsters[i,j]?.SetScale(newScale);
            }
        }
    }
}