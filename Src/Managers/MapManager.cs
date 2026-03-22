using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Gamelab;
using System.Collections.Generic;
using Gamelab.Managers;

public class MapManager
{    
#region storage

    //all the possible Textures a tile can have are held in here
    private readonly TileSet _tileSet;

    //which tile of the tileset is display at this position?
    private readonly int[,] _tiles; //col first 

    //stores which tiles has which structure on it -1 if none
    private readonly Entity[,] _structures;//colfirst
    
    //stores which tiles has which monster on it -1 if none
    private readonly MonsterGroup[,] _monsters;//colfirst

    private SanityManager _sanityManager;
    private float _sanityDamageAccumulator;

    private PathFindingManager _pathFindingManager;

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
    public bool TentDestroyed { get; private set; }

    public void SetSanityManager(SanityManager sanityManager)
    {
        _sanityManager = sanityManager;
    }

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
        _monsters = new MonsterGroup[Columns,Rows];
        _structures = new Entity[Columns,Rows];
        _pathFindingManager = new PathFindingManager(this);
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

        CombatUpdate(gameTime);
    }

    private void CombatUpdate(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Towers attack monsters in range (focus first monster found)
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                if (_structures[i,j] is Tower tower && !tower.IsDestroyed)
                {
                    var tilesInRange = tower.GetTilesInRange();
                    foreach (var tile in tilesInRange)
                    {
                        var group = _monsters[tile.Item1, tile.Item2];
                        if (group != null && group.Count > 0)
                        {
                            group.Monsters[0].TakeDamage(tower.DamagePerSecond * dt);
                            break;
                        }
                    }
                }
            }
        }

        // Monsters attack structures on the same tile
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                var group = _monsters[i,j];
                var structure = _structures[i,j];
                if (group != null && structure != null && !structure.IsDestroyed)
                {
                    float hpBefore = structure.CurrentHealth;
                    foreach (var monster in group.Monsters)
                    {
                        monster.Attack(structure, dt);
                    }

                    if (structure is Tent or Sheep && _sanityManager != null)
                    {
                        float damage = hpBefore - structure.CurrentHealth;
                        _sanityDamageAccumulator += damage;
                        if (_sanityDamageAccumulator >= 1f)
                        {
                            int toDeduct = (int)_sanityDamageAccumulator;
                            _sanityManager.DeductSanity(toDeduct);
                            _sanityDamageAccumulator -= toDeduct;
                        }
                    }
                }
            }
        }

        // Remove dead monsters and empty groups
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                if (_monsters[i,j] != null)
                {
                    _monsters[i,j].RemoveDead();
                    if (_monsters[i,j].Count == 0)
                        _monsters[i,j] = null;
                }
            }
        }

        // Remove dead structures
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                if (_structures[i,j] != null && _structures[i,j].IsDestroyed)
                {
                    if (_structures[i,j] is Tent)
                        TentDestroyed = true;
                    _structures[i,j] = null;
                }
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
    public MonsterGroup GetMonster(int col, int row)
    {        
        return _monsters[col,row];
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
        var atlas = GamelabGame.textureAtlas["night_top"];

        string animKey = card.SpriteName switch
        {
            "dreamcatcher" => "wall-animation", // map dreamcatcher to wall
            "sniper" => "sniper-animation",
            "aoe" => "aoe-animation",
            "fence" => "fence-animation",
            "meadow" => "meadow-animation",
            _ => null
        };

        if (animKey == null) return false; // no matching animation

        var sprite = atlas.CreateAnimatedSprite(animKey);

        Entity entity = card.Name switch
        {
            "sniper" => new Tower(sprite, this, (col,row), damagePerSecond: 8f, health: 100),
            "aoe" => new AreaTower(sprite, this, (col,row), damagePerSecond: 5f, health: 80),
            "fence" => new Fence(sprite, this, (col,row)),
            "meadow" => new Meadow(sprite, this, (col,row)),
            "dreamcatcher" => new Wall(sprite, this, (col,row)),
            _ => null
        };

        if (entity != null){
            entity.SetAsBuildEntity();
            _structures[col,row] = entity;
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
                _tiles[i,j] = Random.Shared.Next(0,_tileSet.Count / 2) * 2;
            }
        }
    }

    public (int,int) GetTileFromScreenPosition(Vector2 pos)
    {
        return getTileIndex(pos);
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

#region Testing funcs

    public void SpawnRandomMonsters()
    {
        for(int i = 0; i < 10; i ++)
        {
            (int,int) index = ((int)Random.Shared.NextInt64(0,10),(int)Random.Shared.NextInt64(0,10));
            MonsterGroup group = new MonsterGroup(this, TimeSpan.FromSeconds(2),index);
            var sprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_walkSW");
            Monster newMonster = new Monster(sprite, this, index, (5,5));
            group.AddMonster(newMonster);
            _monsters[index.Item1,index.Item2] = group;
        }
    }

    /// <summary>
    /// Spawns a monster at a random edge tile that walks toward the given target tile
    /// </summary>
    public void SpawnMonsterAtEdge(int targetCol, int targetRow)
    {
        int side = Random.Shared.Next(4);
        int col, row;
        switch (side)
        {
            case 0:  col = 0;            row = Random.Shared.Next(Rows);    break;
            case 1:  col = Columns - 1;  row = Random.Shared.Next(Rows);    break;
            case 2:  col = Random.Shared.Next(Columns); row = 0;            break;
            default: col = Random.Shared.Next(Columns); row = Rows - 1;     break;
        }

        var sprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_walkSW");
        Monster newMonster = new Monster(sprite, this, (col, row), (targetCol, targetRow));

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
        var smartPath = _pathFindingManager.CalculatePath(newMonster);
        if (smartPath != null && smartPath.Count > 0)
        {
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