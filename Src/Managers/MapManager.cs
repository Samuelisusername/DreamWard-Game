using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Gamelab;
using System.Collections.Generic;
using Gamelab.Managers;
using System.Linq;
using Gamelab.Utils.Logging;

public class MapManager
{ 
#region Managers
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CombatManager combatManager => GamelabGame.CombatManager;
    private CardManager cardManager => GamelabGame.CardManager;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    
    private readonly Logger logger = new("Game");
#endregion
    
#region storage

    //all the possible Textures a tile can have are held in here
    private readonly Dictionary<string, TileSet> _tileSets;

    //which tile of the tileset is display at this position?
    private readonly int[,] _tiles; //col first
    
    //which tile of the tileset is display at this position?
    private readonly int[,] _exteriorTiles; //col first

    //stores which tiles has which structure on it -1 if none
    public readonly Structure[,] Structures; //colfirst

    //stores which tiles has which structure on it -1 if none
    public readonly Structure[,] ExteriorStructures; //colfirst
    
    //stores which tiles has which monster on it -1 if none
    public readonly MonsterGroup[,] MonsterGroups; //colfirst

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
    public Vector2 Offset {get; private set;}
    
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

    /// <summary>
    /// The position of the Sheep on the map
    /// </summary>
    public (int col, int row) SheepPosition {get; set;}
    
    /// <summary>
    /// True when the tent has been destroyed by monsters
    /// </summary>
    public bool TentDestroyed { get; set; }
    
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
        _exteriorTiles = new int[Columns*3,Rows*3];
        MonsterGroups = new MonsterGroup[Columns,Rows];
        Structures = new Structure[Columns,Rows];
        ExteriorStructures = new Structure[Columns*3,Rows*3];
        
        FillRandomly();
        Scale = new Vector2(4, 4);
        MapUtils.Setup(Columns,Rows);
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
                MonsterGroups[i,j]?.Update(gameTime);
                Structures[i,j]?.Update(gameTime);
            }
        }
        foreach (var tree in ExteriorStructures)
        {
            tree?.Update(gameTime);
        }

        //if its night manage monsters
        if(!gamePhaseManager.IsDay)
        {
            combatManager.ManageMoveAndAttack(gameTime);
            if(MonsterGroups.Cast<MonsterGroup>().All(i => i == null))
            {
                gamePhaseManager.SwitchToDay(); 
            }
        }
    }

#region storage helper functions
    
    /// <summary>
    /// returns the Tile at the position
    /// </summary>
    public TextureRegion GetTile((int col, int row) pos)
    {
        if(pos.col - Columns >= 0 && pos.col - Columns < Columns && pos.row - Rows >= 0 && pos.row - Rows < Rows)
        {
            pos.col -= Columns;
            pos.row -= Rows;
            return gamePhaseManager.IsDay ?
                _tileSets["day"].GetTile(_tiles[pos.col,pos.row] + _animationOffset) :
                _tileSets["night"].GetTile(_tiles[pos.col, pos.row] + _animationOffset);
        }
        return gamePhaseManager.IsDay ?
                _tileSets["day"].GetTile(_exteriorTiles[pos.col,pos.row] + _animationOffset) :
                _tileSets["night"].GetTile(_exteriorTiles[pos.col, pos.row] + _animationOffset);
    }
    
    /// <summary>
    /// returns the world position of the tile that is asked for
    /// </summary>
    public Vector2 GetWorldPositionOfTile((int col, int row) pos)
    {
        float tileX = pos.col * TileWidth - (pos.col * ScaledOffset);
        float tileY = pos.col % 2 == 0 ? pos.row * TileHeight : pos.row * TileHeight + ScaledOffset;

        return new Vector2(tileX, tileY) + Offset;
    }
    
    /// <summary>
    /// Create a new monster at the given location
    /// </summary>
    public void AddNewMonster((int col, int row) pos)
    {
        if(Structures[pos.col, pos.row] == null)
        {            
            Monster newMonster = new RegularMonster(pos);

            if (MonsterGroups[pos.col, pos.row] != null)
            {
                MonsterGroups[pos.col, pos.row].AddMonster(newMonster);
            }
            else
            {
                MonsterGroup group = new MonsterGroup(TimeSpan.FromSeconds(2), pos);
                group.AddMonster(newMonster);
                MonsterGroups[pos.col, pos.row] = group;
            }  
        }     
    }

    /// <summary>
    /// Add an existing Monster to the position
    /// </summary>
    public void AddExistingMonster(Monster monster, (int col, int row) pos)
    {
        if(GetMonsterGroup(pos) is {} monsterGroup)
        {
            monsterGroup.AddMonster(monster);
        }
        else
        {
            MonsterGroups[pos.Item1,pos.Item2] = new MonsterGroup(TimeSpan.FromSeconds(2), pos);
            GetMonsterGroup(pos).AddMonster(monster);
        }
    }
    
    /// <summary>
    /// returns the Monster on the tile
    /// </summary>
    public MonsterGroup GetMonsterGroup((int col, int row) pos)
    {        
        return MonsterGroups[pos.col,pos.row];
    }
    
    /// <summary>
    /// returns the Monster on the tile
    /// </summary>
    public MonsterGroup RemoveMonsterGroup((int col, int row) pos)
    {        
        return MonsterGroups[pos.col, pos.row] = null;
    }

    /// <summary>
    /// returns the structure at the given position
    /// </summary>
    public Structure GetStructure((int col, int row) pos, bool wholeMap = false)
    {
        if(wholeMap)
        {        
            if(pos.col - Columns >= 0 && pos.col - Columns < Columns && pos.row - Rows >= 0 && pos.row - Rows < Rows)
            {
                pos.col -= Columns;
                pos.row -= Rows;            
                return Structures[pos.col, pos.row];
            }
            return ExteriorStructures[pos.col,pos.row];
        }
        return Structures[pos.col, pos.row];    
    }

    /// <summary>
    /// removes the structure at the given position
    /// </summary>
    public void RemoveStructure((int col, int row) pos)
    {
        Structures[pos.col, pos.row] = null;
    }
    
    /// <summary>
    /// Places an entity at the given position
    /// </summary>
    public bool PlaceEntity(Structure structure, (int col, int row) pos)
    {        
        bool occupied = Structures[pos.col, pos.row] != null;
        if (occupied)
        {
            Console.WriteLine("Tile already occupied!");
            AudioManager.Play("error");
            return false;
        }

        if (structure != null)
        {
            Structures[pos.col, pos.row] = structure;

            return true;
        }
        return false;
    }

    
    /// <summary>
    /// Places an Sheep at the given position
    /// </summary>
    public bool PlaceSheep(Meadow entity, (int col, int row) pos)
    {
        if(!PlaceEntity(entity, pos))
        {
            return false;
        }        
        entity.AddSheep();
        SheepPosition = (pos.col, pos.row);
        return true;
    }


    /// <summary>
    /// Places an entity defined by the Card at the given position
    /// </summary>
    public bool PlaceEntity(Card card, (int col, int row) pos)
    {        
        bool occupied = Structures[pos.col, pos.row] != null;
        if (occupied)
        {
            Console.WriteLine("Tile already occupied!");
            return false;
        }

        Structure structure = cardManager.GetStructureFromCard(card, pos);

        if (structure != null){
            Structures[pos.col, pos.row] = structure;
            if(structure is PlacableMeadow placableMeadow)
            {
                foreach (var position in MapUtils.getSurroundingTiles(pos))
                {
                    if(Structures[position.Item1,position.Item2] is Meadow meadow && meadow.HasSheep)
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
    public List<Entity> GetAllEntities((int col, int row) pos)
    {
        List<Entity> res = [GetStructure(pos), .. GetMonsterGroup(pos).Monsters];
        return res;
    }

    /// <summary>
    /// Fills the tileset randomly
    /// </summary>
    public void FillRandomly()
    {
        string[] treeAnims = { "treeBig_idle", "treeSmall_idle" };
        for(int i = 0; i < Columns * 3; i++)
        {
            for(int j = 0; j < Rows * 3; j++)
            {
                if(i - Columns >= 0 && i - Columns < Columns && j - Rows >= 0 && j - Rows < Rows)
                {                        
                    _tiles[i - Columns, j - Rows] = Random.Shared.Next(0,_tileSets["day"].Count / 2) * 2;
                    _exteriorTiles[i, j] = -1;
                }
                else
                {
                    _exteriorTiles[i, j] = Random.Shared.Next(0,_tileSets["day"].Count / 2) * 2;
                }
            }
        }
    }

    public void AddTrees(List<(int col, int row)> treePlacements)
    {
        int seed = Random.Shared.Next();
        logger.Info("seed: " + seed);
        FastNoiseLite noise = new FastNoiseLite();
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFractalOctaves(1);
        float maxDistance = MapUtils.CalculateTileDistance(GetTent(),(-1,-1));

        for(int i = 0; i < Columns * 3; i++)
        {
            for(int j = 0; j < Rows * 3; j++)
            {
                float noiseval = (noise.GetNoise(i * 10,j * 10) + 1) / 2;
                if(!(i - Columns >= 0 && i - Columns < Columns && j - Rows >= 0 && j - Rows < Rows))
                {                       
                    if(noiseval < 0.2)
                    {
                        //check passed spawn tree replace with noise in the future
                        var tree = new TreeTile(Random.Shared.NextDouble() < 0.75 ? "treeBig_idle" : "treeSmall_idle", (i-Columns, j-Rows));
                        ExteriorStructures[i,j] = ExteriorStructures[i,j] ?? tree;  
                    }
                    else if(noiseval < 0.6)
                    {
                        var tree = new TreeTile(Random.Shared.NextDouble() < 0.25 ? "treeBig_idle" : "treeSmall_idle", (i-Columns, j-Rows));
                        ExteriorStructures[i,j] = ExteriorStructures[i,j] ?? tree;  
                    }
                    else if(Random.Shared.NextDouble() < 0.25)
                    {
                        var tree = new TreeTile(Random.Shared.NextDouble() < 0.25 ? "treeBig_idle" : "treeSmall_idle", (i-Columns, j-Rows));
                        ExteriorStructures[i,j] = ExteriorStructures[i,j] ?? tree;  
                    }
                              
                }
                else
                {   
                    float relativeDist = MapUtils.CalculateTileDistance(GetTent(),(i-Columns,j-Rows)) / maxDistance;
                    relativeDist = MathF.Pow(relativeDist,2);
                    noiseval /= relativeDist;
                    if(noiseval < 0.2)
                    {
                        //check passed spawn tree replace with noise in the future
                        var tree = new TreeTile(Random.Shared.NextDouble() < 0.75 ? "treeBig_idle" : "treeSmall_idle", (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;  
                    }
                    else if(noiseval < 0.4)
                    {
                        var tree = new TreeTile(Random.Shared.NextDouble() < 0.25 ? "treeBig_idle" : "treeSmall_idle", (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;  
                    }
                    else if(Random.Shared.NextDouble() < 0.25 * relativeDist)
                    {
                        var tree = new TreeTile(Random.Shared.NextDouble() < 0.25 ? "treeBig_idle" : "treeSmall_idle", (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;  
                    }
                }
            }
        }
        
                
    }
    
    
    /// <summary>
    /// returns the index of the tile that is at the position on screen
    /// </summary>
    /// <param name="position"></param>
    /// <returns>returns [col,row] of the tile at position. returns [-1,-1] if the position is not on the tilemap</returns>
    public (int col, int pos) GetTileFromScreenPosition(Vector2 position)
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
    /// returns the 
    /// </summary>
    /// <returns></returns>
    public (int col, int row) GetTent()
    {
        foreach (var item in Structures)
        {
            if(item as Tent is {} cool)
            {
                return cool.TilePosition;
            }
        }
        return (-1,-1);
    }

    /// <summary>
    /// tries to move the sheep to the position clicked on screen
    /// </summary>
    public void TryMoveSheep(Vector2 pos)
    {
        (int col, int row) tilePos = GetTileFromScreenPosition(pos);
        if(tilePos.row < 0 ) return;
        Entity entity = GetStructure(tilePos);
        if(entity is Meadow meadow)
        {
            int distance = MapUtils.CalculateTileDistance(SheepPosition, tilePos);
            if(sanityManager.TryDeductSanity(distance))
            {
                foreach (var position in MapUtils.getSurroundingTiles(tilePos))
                {
                    if(Structures[position.Item1,position.Item2] is PlacableMeadow toActivate)
                    {
                        toActivate.ActivateMeadow(meadow.SanityGain);
                    }
                }
                foreach (var position in MapUtils.getSurroundingTiles(SheepPosition))
                {
                    if(Structures[position.Item1,position.Item2] is PlacableMeadow toDeactivate)
                    {
                        toDeactivate.DeactivateMeadow();
                    }
                }
                var oldMeadow = Structures[SheepPosition.Item1, SheepPosition.Item2] as Meadow;
                bool wasPolymorphed = oldMeadow.IsPolymorphed;
                oldMeadow.RemoveSheep();
                meadow.AddSheep(wasPolymorphed);
                SheepPosition = (tilePos.col, tilePos.row);
            }
        } 
    }

    /// <summary>
    /// returns the meadow with the sheep on it
    /// </summary>
    public Meadow GetSheep()
    {
        return Structures[SheepPosition.Item1, SheepPosition.Item2] as Meadow;
    }

    /// <summary>
    /// returns all meadows
    /// </summary>
    public List<PlacableMeadow> GetAllMeadows()
    {
        List<PlacableMeadow> res = new List<PlacableMeadow>();
        foreach (var structure in Structures)
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


    public void InitOffset(Point screenSize)
    {
        Offset = -GetWorldPositionOfTileWithoutOffset((Columns/2,Rows/2)) + (screenSize.ToVector2() / 2) - new Vector2(TileWidth/2,TileHeight/2);
    }
    /// <summary>
    /// Moves the map into the direction of delta
    /// </summary>
    public void MoveMap(Vector2 delta, Point screenSize)
    {
        Offset += delta;
        Vector2 maxOffset = screenSize.ToVector2() / 2;
        Vector2 minOffset = -GetWorldPositionOfTileWithoutOffset((Columns,Rows)) + maxOffset;
        Offset = new Vector2(Math.Clamp(Offset.X, minOffset.X, maxOffset.X), Math.Clamp(Offset.Y, minOffset.Y, maxOffset.Y));
    }

    private Vector2 GetWorldPositionOfTileWithoutOffset((int col, int row) pos)
    {
        float tileX = pos.col * TileWidth - (pos.col * ScaledOffset);
        float tileY = pos.col % 2 == 0 ? pos.row * TileHeight : pos.row * TileHeight + ScaledOffset;

        return new Vector2(tileX, tileY);
    }

    /// <summary>
    /// Zooms the map by delta, towards the focus point
    /// </summary>
    public void Zoom(float delta, Vector2 focus, Point screenSize)
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
        MoveMap(correction, screenSize);
        Scale = newScale;
        //scale all the monsters
        foreach(MonsterGroup monsterGroup in MonsterGroups)
        {
            monsterGroup?.SetScale(newScale);
        }
    }
#endregion
}