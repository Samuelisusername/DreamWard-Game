using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Gamelab;
using System.Collections.Generic;
using Gamelab.Managers;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Media;

public class MapManager
{    
#region storage

    //all the possible Textures a tile can have are held in here
    internal readonly Dictionary<string, TileSet> _tileSets;

    //which tile of the tileset is display at this position?
    private readonly int[,] _tiles; //col first

    //stores which tiles has which structure on it -1 if none
    private readonly Entity[,] _structures;//colfirst
    
    //stores which tiles has which monster on it -1 if none
    private readonly MonsterGroup[,] _monsters;//colfirst

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
    /// True when the tent has been destroyed by monsters
    /// </summary>
    public bool TentDestroyed { get; set; }

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
    public float TileWidth => _tileSets["day"].TileWidth * Scale.X;

    /// <summary>
    /// The height of a single tile
    /// </summary>
    public float TileHeight => _tileSets["day"].TileHeight * Scale.Y;

    /// <summary>
    /// The region of the hexagon that overlaps with other hexagons
    /// </summary>
    public float ScaledOffset => TileHeight /2;

    public (int,int) SheepPosition {get; set;}
    
#endregion

#region animation fields 

    //the elapsed time since the start of the last animationframe
    private TimeSpan _elapsed;

    //the delay before the animationframe changes again
    private readonly TimeSpan _delay;

    //which frame of the animation are we on?
    private int _animationOffset = 0;

#endregion


    public MapManager(TileSet setDay, TileSet setNight, int columns, int rows, TimeSpan delay)
    {
        _tileSets = new Dictionary<string, TileSet>
        {
            { "day", setDay },
            { "night", setNight }
        };
        Columns = columns;
        Rows = rows;
        _delay = delay;
        _tiles = new int[Columns,Rows];
        _monsters = new MonsterGroup[Columns,Rows];
        _structures = new Entity[Columns,Rows];
    }

    /// <summary>
    /// Test-only constructor: uses the same TileSet for day and night.
    /// </summary>
    public MapManager(TileSet tileSet, int columns, int rows, TimeSpan delay)
        : this(tileSet, tileSet, columns, rows, delay)
    {
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
                _monsters[i,j]?.Update(gameTime);
                _structures[i,j]?.Update(gameTime);
            }
        }
        //move the monsters if needed we do this in two loops to avoid updating monsters twice
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {

                _monsters[i,j]?.Move();
            }
        }


        if(!GamelabGame.GamePhaseManager.IsDay)
        {
            GamelabGame.CombatManager.ManageMoveAndAttack(gameTime);
            foreach (var monster in _monsters)
            {
                if(monster != null)
                {
                    return;
                }
            }
            GamelabGame.GamePhaseManager.SwitchToDay(); 
        }
    }

#region storage helper functions
    
    /// <summary>
    /// returns the Tile at the position
    /// </summary>
    public TextureRegion GetTile(int col, int row)
    {        
        return GamelabGame.GamePhaseManager.IsDay ? _tileSets["day"].GetTile(_tiles[col,row] + _animationOffset) :  _tileSets["night"].GetTile(_tiles[col,row] + _animationOffset);
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
    
    public void AddMonster(int col, int row, int targetCol, int targetRow)
    {
        Monster newMonster = new Monster("normal_walkSW", this, (col, row), (targetCol, targetRow));

        // // --- GenerateHexPath version (simple greedy path to target) ---
        // if (_monsters[col, row] != null)
        // {
        //     _monsters[col, row].AddMonster(newMonster);
        // }
        // else
        // {
        //     MonsterGroup group = new MonsterGroup(this, TimeSpan.FromSeconds(2), (col, row));
        //     group.AddMonster(newMonster);
        //     _monsters[col, row] = group;
        // }

        // --- PathFindingManager version (strategic pathfinding) ---
        var smartPath = GamelabGame.PathFindingManager.CalculatePath(newMonster);
        if (smartPath != null && smartPath.Count > 0)
        {
            while(smartPath.Count >= 2 && smartPath[0] == smartPath[1])
            {
                smartPath.RemoveAt(0);
            }
            newMonster.Path = smartPath;
        }

        if (_monsters[col, row] != null)
        {
            _monsters[col, row].AddMonster(newMonster);
        }
        else
        {
            MonsterGroup group = new MonsterGroup(this, TimeSpan.FromSeconds(2), (col, row));
            group.AddMonster(newMonster);
            _monsters[col, row] = group;
        }       
    }

    /// <summary>
    /// returns the Monster on the tile
    /// </summary>
    public MonsterGroup GetMonster(int col, int row)
    {        
        return _monsters[col,row];
    }

    
    /// <summary>
    /// returns the Monster on the tile
    /// </summary>
    public MonsterGroup RemoveMonsterGroup(int col, int row)
    {        
        return _monsters[col,row] = null;
    }

    /// <summary>
    /// moves the monsters in the tile speicified by origin to the tile specified by goal
    /// </summary>
    public void MoveMonster(Monster monster, (int,int) origin, (int,int) goal)
    {
        if(_monsters[origin.Item1,origin.Item2].Count == 0)
        {
            _monsters[origin.Item1,origin.Item2] = null;
        }
        if( _monsters[goal.Item1,goal.Item2] != null)
        {
            _monsters[goal.Item1,goal.Item2].AddMonster(monster);
        }
        else
        {
            _monsters[goal.Item1,goal.Item2] = new MonsterGroup(this,  TimeSpan.FromSeconds(2),goal);
            _monsters[goal.Item1,goal.Item2].AddMonster(monster);
        }
    }

    /// <summary>
    /// returns the structure at the given position
    /// </summary>
    public Entity GetStructure(int col, int row)
    {
        return _structures[col,row];
    }

    /// <summary>
    /// removes the structure at the given position
    /// </summary>
    public void RemoveStructure(int col, int row)
    {
        _structures[col,row] = null;
    }
    
    
    /// <summary>
    /// Places an entity at the given position
    /// </summary>
    public bool PlaceEntity(Entity entity, int col, int row)
    {        
        bool occupied = _structures[col,row] != null;
        if (occupied)
        {
            Console.WriteLine("Tile already occupied!");
            AudioManager.Play("error");
            return false;
        }

        if (entity != null){
            entity.SetAsBuildEntity();
            
            _structures[col,row] = entity;

            return true;
        }
        return false;
    }

    
    /// <summary>
    /// Places an Sheep at the given position
    /// </summary>
    public bool PlaceSheep(Meadow entity, int col, int row)
    {        
        bool occupied = _structures[col,row] != null;
        if (occupied)
        {
            Console.WriteLine("Tile already occupied!");
            AudioManager.Play("error");
            return false;
        }

        if (entity != null){
            entity.SetAsBuildEntity();
            
            _structures[col,row] = entity;
            entity.AddSheep();
            SheepPosition = (col,row);
            return true;
        }
        return false;
    }


    /// <summary>
    /// Places an entity defined by the Card at the given position
    /// </summary>
    public bool PlaceEntity(Card card, int col, int row)
    {        
        bool occupied = _structures[col,row] != null;
        if (occupied)
        {
            Console.WriteLine("Tile already occupied!");
            return false;
        }

        string animKey = card.SpriteName switch
        {
            "dreamcatcher" => "wall_idle", // map dreamcatcher to wall
            "sniper" => "towerSniper_idle",
            "aoe" => "towerAOE_idle",
            "fence" => "fence_idle",
            "meadow" => "void_idle",
            _ => null
        };

        if (animKey == null) return false; // no matching animation

        Entity entity = card.Name switch
        {
            "sniper" => new Tower(animKey, this, (col,row), damagePerSecond: 8f, health: 100),
            "aoe" => new AreaTower(animKey, this, (col,row), damagePerSecond: 5f, health: 80),
            "fence" => new Fence(animKey, this, (col,row)),
            "meadow" => new PlacableMeadow(animKey, this, (col,row),2),
            "dreamcatcher" => new Wall(animKey, this, (col,row)),
            _ => null
        };

        if (entity != null){
            entity.SetAsBuildEntity();
            _structures[col,row] = entity;
            if(entity is PlacableMeadow placableMeadow)
            {
                foreach (var position in MapUtils.getSurroundingTiles(col,row))
                {
                    if(_structures[position.Item1,position.Item2] is Meadow meadow && meadow.HasSheep)
                    {
                        placableMeadow.ActivateMeadow(meadow.SanityGain);
                    }
                }
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// return all entities on this tile(monstes and structures)
    /// </summary>
    public List<Entity> GetAllEntities(int col, int row)
    {
        List<Entity> res = [GetStructure(col,row), .. GetMonster(col,row).Monsters];
        return res;
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
                _tiles[i,j] = Random.Shared.Next(0,_tileSets["day"].Count / 2) * 2;
            }
        }
    }

    public (int,int) GetTileFromScreenPosition(Vector2 pos)
    {
        return getTileIndex(pos);
    }

    public (int,int) GetTent()
    {
        foreach (var item in _structures)
        {
            if(item as Tent is {} cool)
            {
                return cool.TilePosition;
            }
        }
        return (-1,-1);
    }

    private List<(Entity original, Entity polymorphed)> _polymorphedEntities = new();

    public void RegisterPolymorph(Entity original, Entity polymorphed)
    {
        _polymorphedEntities.Add((original, polymorphed));
    }

    public void RestorePolymorphedEntities()
    {
        foreach (var (original, polymorphed) in _polymorphedEntities)
        {
            var tile = polymorphed.TilePosition;
            GamelabGame.MapManager.RemoveStructure(tile.Item1, tile.Item2);
            GamelabGame.MapManager.PlaceEntity(original, tile.Item1, tile.Item2);
        }
        _polymorphedEntities.Clear();
    }

    public void TryMoveSheep(Vector2 pos)
    {
        (int col, int row) = GetTileFromScreenPosition(pos);
        Entity entity = GetStructure(col,row);
        if(entity is Meadow meadow)
        {
            if(GamelabGame.SanityManager.TryDeductSanity(GamelabGame.PathFindingManager.CalculateTileDistance(SheepPosition.Item1, SheepPosition.Item2, col, row)))
            {
                foreach (var position in MapUtils.getSurroundingTiles(col,row))
                {
                    if(_structures[position.Item1,position.Item2] is PlacableMeadow toActivate)
                    {
                        toActivate.ActivateMeadow(meadow.SanityGain);
                    }
                }
                foreach (var position in MapUtils.getSurroundingTiles(SheepPosition.Item1, SheepPosition.Item2))
                {
                    if(_structures[position.Item1,position.Item2] is PlacableMeadow toDeactivate)
                    {
                        toDeactivate.DeactivateMeadow();
                    }
                }
                (_structures[SheepPosition.Item1, SheepPosition.Item2] as Meadow).RemoveSheep();
                meadow.AddSheep();
                SheepPosition = (col,row);
            }
        } 
    }

    public Meadow GetSheep()
    {
        return _structures[SheepPosition.Item1, SheepPosition.Item2] as Meadow;
    }

    public List<PlacableMeadow> GetAllMeadows()
    {
        List<PlacableMeadow> res = new List<PlacableMeadow>();
        foreach (var structure in _structures)
        {
            if(structure is PlacableMeadow placableMeadow)
            {
                res.Add(placableMeadow);
            }
        }
        return res;
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
            // Monster newMonster = index.Item1 % 2 == 0 ?  new Monster(GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_walk"), this, TimeSpan.FromSeconds(2),index) :  new Monster(GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_fight"), this, TimeSpan.FromSeconds(2),index);
            // _monsters[index.Item1,index.Item2].Add(newMonster);

            _tiles[index.Item1,index.Item2] = _tiles[index.Item1,index.Item2] > 1 ? _tiles[index.Item1,index.Item2] - 2 : _tiles[index.Item1,index.Item2] +2;
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
        Vector2 newScale = new Vector2(Math.Max(Scale.X * (delta > 0 ? 1.09f : 0.91f),1f), Math.Max(Scale.Y * (delta > 0 ? 1.09f : 0.91f),1f));
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

#region Testing funcs

    public void SpawnRandomMonsters()
    {
        for(int i = 0; i < 10; i ++)
        {
            (int,int) index = ((int)Random.Shared.NextInt64(0,10),(int)Random.Shared.NextInt64(0,10));
            MonsterGroup group = new MonsterGroup(this, TimeSpan.FromSeconds(2),index);
            Monster newMonster = new Monster("normal_walkSW", this, index, (5,5));
            group.AddMonster(newMonster);
            _monsters[index.Item1,index.Item2] = group;
        }
    }


    public void RandomizeGoal()
    {
         for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
                _monsters[i,j]?.RandomziePath();
            }
        }
    }
#endregion
}