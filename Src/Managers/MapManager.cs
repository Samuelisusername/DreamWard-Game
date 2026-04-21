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
    
    // NEW: stores the container for each tile, handling all monsters
    public readonly MonsterGroupContainer[,] MonsterContainers; //colfirst

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

    //which frame of the animation are we on?
    private int _animationOffset = 0;

    public bool isFalling {get; set;} = false;

    public TimeSpan[,] FallTimer;

    public TimeSpan MaxFallTime = TimeSpan.FromSeconds(0.5f);

    public TimeSpan MaxTotalFallTime;
    public TimeSpan TotalFallTime;

    private float fallSpeed = 1000; //units per second

    private float shakeMax = 0.2f; //shaketime max in seconds
    private float shakeOffset = 0.05f; //shaketime max in seconds

    public Vector2[,] TilesOfsets {get; set;}

#endregion


    public MapManager(TileSet setDay, TileSet setNight, int columns, int rows)
    {
        _tileSets = new Dictionary<string, TileSet>
        {
            { "day", setDay },
            { "night", setNight }
        };
        Columns = columns;
        Rows = rows;
        _tiles = new int[Columns,Rows];
        _exteriorTiles = new int[Columns*3,Rows*3];
        Structures = new Structure[Columns,Rows];
        ExteriorStructures = new Structure[Columns*3,Rows*3];
        TilesOfsets = new Vector2[Columns*3,Rows*3];
        FallTimer = new TimeSpan[Columns*3,Rows*3];
        #if DEBUG
        Debug = new bool[Columns,Rows];
        #endif
        
        // NEW: Initialize the container grid
        MonsterContainers = new MonsterGroupContainer[Columns,Rows];
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                // We MUST initialize an empty container for every tile to prevent null errors
                MonsterContainers[i, j] = new MonsterGroupContainer((i, j));
            }
        }
        
        FillRandomly();
        Scale = new Vector2(1, 1);
        MapUtils.Setup(Columns,Rows);
    }
    
    public void Update(GameTime gameTime)
    {   
        //progress the animation using the active tileset's frame duration from JSON
        TimeSpan delay = _tileSets[gamePhaseManager.IsDay ? "day" : "night"].FrameDuration;
        _elapsed += gameTime.ElapsedGameTime;
        if(_elapsed >= delay)
        {
            _elapsed -= delay;
            _animationOffset = (_animationOffset + 1) % 2;
        }
        if(isFalling)
        {
            TotalFallTime += gameTime.ElapsedGameTime;
            for (int i = 0; i < Columns * 3; i++)
            {
                for (int j = 0; j < Rows*3; j++)
                {
                    if(FallTimer[i,j] > TimeSpan.Zero)
                    {
                        //falling phase
                        FallTimer[i,j] += gameTime.ElapsedGameTime;
                        TilesOfsets[i,j] = Vector2.UnitY * (float)FallTimer[i,j].TotalSeconds * fallSpeed * Scale;
                    }
                    else
                    {
                        //shaking phase
                        FallTimer[i,j] += gameTime.ElapsedGameTime;
                        if(FallTimer[i,j] > TimeSpan.Zero)
                        {
                            TilesOfsets[i,j] = Vector2.UnitY * (float)FallTimer[i,j].TotalSeconds * fallSpeed * Scale;
                        }
                        else
                        {
                            TilesOfsets[i,j] = new Vector2((float)Random.Shared.NextDouble(), (float)Random.Shared.NextDouble()) * Math.Clamp((float)-FallTimer[i,j].TotalSeconds, 0.0f, 4.0f) * Scale;
                        }
                    }
                }
            }
            if(FallTimer.Cast<TimeSpan>().All(i => i > MaxFallTime))
            {
                isFalling = false;
                gamePhaseManager.FinishSwitchToNight();
            }
        }

        //Update all the monsters and structures
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
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
            // NEW: Check if ALL containers on the map are entirely empty
            if(MonsterContainers.Cast<MonsterGroupContainer>().All(c => c.IsEmpty))
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
        if(MapUtils.IsInBounds((pos.col- Columns, pos.row - Rows)))
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
/// <summary>
    /// Create a new monster at the given location
    /// </summary>
    public bool AddNewMonster((int col, int row) pos, int level)
    {
        Structure structureOnTile = Structures[pos.col,pos.row];
        Monster newMonster = null;
        
        // Generate a random number between 0 and 99
        int roll = GamelabGame.Rng.Next(100);

        //TODO: move this logic to the combat manager and probably make less random
        switch (level)
        {
            case 1:
                newMonster = new RegularMonster(pos);
                break;
            case 2:
                newMonster = new RegularMonster(pos);
                break;
            case 3:
                if (roll < 30)
                {
                    newMonster = new FlyingMonster(pos); 
                }
                else 
                {
                    newMonster = new RegularMonster(pos);
                }
                break;
            case 4:
                if (roll < 50)
                {
                    newMonster = new FlyingMonster(pos); 
                }
                else 
                {
                    newMonster = new RegularMonster(pos);
                }
                break;
            case 5:
                if (roll < 30)
                {
                    newMonster = new SmallMonster(pos); 
                }
                else 
                {
                    newMonster = new RegularMonster(pos);
                }
                break;
            case 6:
                if (roll < 50)
                {
                    newMonster = new SmallMonster(pos); 
                }
                else 
                {
                    newMonster = new RegularMonster(pos);
                }
                break;
            case 7:
                if (roll < 90)
                {
                    newMonster = new SmallMonster(pos); 
                }
                else 
                {
                    newMonster = new RegularMonster(pos);
                }
                break;
            case 8:
                if (roll < 20)
                {
                    newMonster = new FlyingMonster(pos); 
                }
                else if(roll < 70)
                {
                    newMonster = new SmallMonster(pos);
                }
                else 
                {
                    newMonster = new RegularMonster(pos);
                }
                break;
            case 9:
                if (roll < 30)
                {
                    newMonster = new FlyingMonster(pos); 
                }
                else if(roll < 60)
                {
                    newMonster = new SmallMonster(pos);
                }
                else 
                {
                    newMonster = new RegularMonster(pos);
                }
                break;
            case 10:
                if(roll < 10)
                {
                    newMonster = new BigMonster(pos); 
                }
                else if(roll < 60)
                {
                    newMonster = new SmallMonster(pos);
                }
                else if(roll < 80)
                {
                    newMonster = new RegularMonster(pos);
                }
                else if(roll < 80)
                {
                    newMonster = new FlyingMonster(pos);
                }
                break;
            default:
                newMonster = new BigMonster(pos); 
                break;
        }
        if(structureOnTile is not null && !newMonster.CanStepOn(structureOnTile))
        {
            return false;
        }
        // Let the container handle sorting it into the correct group
        MonsterContainers[pos.col, pos.row].AddExistingMonster(newMonster); 
        return true;     
    }

    public void AddExistingMonsterGroup(MonsterGroup monsterGroup,  (int col, int row) pos, bool shouldRecalcPath, TimeSpan elapsed)
    {
        MonsterContainers[pos.col, pos.row].AddExistingMonsterGroup(monsterGroup, shouldRecalcPath, elapsed);
    }
    
    /// <summary>
    /// returns the MonsterContainer on the tile
    /// </summary>
    public MonsterGroupContainer GetMonsterContainer((int col, int row) pos)
    {        
        if(MapUtils.IsInBounds(pos))
        {
            return MonsterContainers[pos.col, pos.row];            
        }
        return null;
    }

    /// <summary>
    /// returns the structure at the given position
    /// </summary>
    public Structure GetStructure((int col, int row) pos, bool wholeMap = false)
    {
        if(wholeMap)
        {        
            if(MapUtils.IsInBounds((pos.col - Columns, pos.row - Rows)))
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
            logger.Info("Tile already occupied! " + pos);
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
                foreach (var position in MapUtils.GetSurroundingTiles(pos))
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
    /// return all entities on this tile (monsters and structures)
    /// </summary>
    public List<Entity> GetAllEntities((int col, int row) pos)
    {
        List<Entity> res = new List<Entity>();
        
        // Add structure if there is one
        var structure = GetStructure(pos);
        if (structure != null)
        {
            res.Add(structure);
        }

        // NEW: Dig into the container and grab all individual monsters
        foreach (var group in MonsterContainers[pos.col, pos.row].MonsterGroups)
        {
            res.AddRange(group.Monsters); 
        }

        return res;
    }

    /// <summary>
    /// Fills the tileset randomly
    /// </summary>
    public void FillRandomly()
    {
        string[] treeAnims = { "treeBig_idle", "treeMid_idle", "treeSmall_idle" };
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

    public void fillMap()
    {
        int centerX = Columns / 2;
        int centerY = Rows / 2;

        // 🏕️ Tent at center
        PlaceEntity(new Tent("tent_idle",
            (centerX, centerY)
        ), (centerX, centerY));

        Meadow starterMeadow = new Meadow((centerX + 1, centerY), 5);

        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX, centerY + 1)), (centerX, centerY + 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX, centerY - 1)), (centerX, centerY - 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX-1, centerY - 1)), (centerX-1, centerY - 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX-1, centerY )), (centerX-1, centerY ));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 1, centerY + 1)), (centerX + 1, centerY + 1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 1, centerY -1)), (centerX + 1, centerY -1));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 2, centerY)), (centerX + 2, centerY ));
        // mapManager.PlaceEntity(new Fence("fence_idle", (centerX + 2, centerY + 1)), (centerX + 2, centerY + 1));


        // 🐑 Sheep next to tent
        PlaceSheep(starterMeadow ,(centerX + 1, centerY));

        // 🌲 Random trees
        AddTrees();
        combatManager.GetSpawnableArea();
        AddMeadows();
    }

    public void AddTrees()
    {
        int noiseSeed = GamelabGame.Seed;
        FastNoiseLite noise = new FastNoiseLite(noiseSeed);
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
                    if(noiseval < 0.4)
                    {
                        var tree = new TreeTile(PickTreeAnim(0.60, 0.40), (i-Columns, j-Rows));
                        ExteriorStructures[i,j] = ExteriorStructures[i,j] ?? tree;
                    }
                    else if(noiseval < 0.7)
                    {
                        var tree = new TreeTile(PickTreeAnim(0.30, 0.65), (i-Columns, j-Rows));
                        ExteriorStructures[i,j] = ExteriorStructures[i,j] ?? tree;
                    }
                    else if(GamelabGame.Rng.NextDouble() < 0.25)
                    {
                        var tree = new TreeTile(PickTreeAnim(0.01, 0.55), (i-Columns, j-Rows));
                        ExteriorStructures[i,j] = ExteriorStructures[i,j] ?? tree;
                    }
                              
                }
                else
                {   
                    float relativeDist = MapUtils.CalculateTileDistance(GetTent(),(i-Columns,j-Rows)) / maxDistance;
                    relativeDist = MathF.Pow(relativeDist,2.125f);
                    noiseval /= relativeDist;
                    if(noiseval < 0.3)
                    {
                        //check passed spawn tree replace with noise in the future
                        var tree = new TreeTile(PickTreeAnim(0.4, 0.4), (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;
                    }
                    else if(noiseval < 0.5)
                    {
                        var tree = new TreeTile(PickTreeAnim(0.1, 0.45), (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;
                    }
                    else if(GamelabGame.Rng.NextDouble() < 0.25 * relativeDist)
                    {
                        var tree = new TreeTile(PickTreeAnim(0.0, 0.35), (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;
                    }
                }
            }
        }       
    }
    
    private void AddMeadows()
    {
        (int col, int row) tent = GetTent();

        List<(int,int)> firstZone = MapUtils.GetSurroundingCircle(tent,4,5);        
        int randomIndexFirst = GamelabGame.Rng.Next(firstZone.Count);
        for(int i = 0; i < 3; i++)
        {
            randomIndexFirst += firstZone.Count/3;
            randomIndexFirst %= firstZone.Count;
            (int,int) newPos = firstZone[randomIndexFirst];
            Meadow m = new Meadow(newPos, 10);
            while(!PlaceEntity(m ,newPos))
            {
                randomIndexFirst++;
                randomIndexFirst %= firstZone.Count;
                newPos = firstZone[randomIndexFirst];
                m = new Meadow(newPos, 10);
            }
        }


        List<(int,int)> secondZone = MapUtils.GetSurroundingCircle(tent,7,8);        
        int randomIndexSecond = GamelabGame.Rng.Next(secondZone.Count);
        for(int i = 0; i < 3; i++)
        {            
            randomIndexSecond += secondZone.Count/3;
            randomIndexSecond %= secondZone.Count;
            (int,int) newPos = secondZone[randomIndexSecond];
            Meadow m = new Meadow(newPos, 15);
            while(!PlaceEntity(m ,newPos))
            {
                randomIndexSecond++;
                randomIndexSecond %= secondZone.Count;
                newPos = secondZone[randomIndexSecond];
                m = new Meadow(newPos, 15);
            }
        }

        List<(int,int)> thirdZone = MapUtils.GetSurroundingCircle(tent,10,11);
        int randomIndexThird = GamelabGame.Rng.Next(thirdZone.Count);
        for(int i = 0; i < 3; i++)
        {            
            randomIndexThird += thirdZone.Count/3;
            randomIndexThird %= thirdZone.Count;
            (int,int) newPos = thirdZone[randomIndexThird];
            Meadow m = new Meadow(newPos, 20);
            while(!PlaceEntity(m ,newPos))
            {
                randomIndexThird++;
                randomIndexThird %= thirdZone.Count;
                newPos = thirdZone[randomIndexThird];
                m = new Meadow(newPos, 20);
            }
        }
    }

    /// <summary>
    /// returns the index of the tile that is at the position on screen
    /// </summary>
    public (int col, int pos) GetTileFromScreenPosition(Vector2 position, bool wholeMap = false)
    {
        position -= Offset;
        (int col, int row) res;
        //get the approximate column of the click
        int colAprox = (int)(position.X / (TileWidth - ScaledOffset));
        int isOddRow = colAprox % 2;
        //get the row if aprox is correct and the row if approx was shifter to the right
        int row = isOddRow  == 0? (int)Math.Floor(position.Y/TileHeight) : (int)Math.Floor((position.Y - ScaledOffset)/TileHeight);
        int rowL = isOddRow * isOddRow == 1? (int)Math.Floor(position.Y/TileHeight) : (int)Math.Floor((position.Y - ScaledOffset)/TileHeight);
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

        if(wholeMap)
        {
            return (res.col + Columns, res.row + Rows);
        }
        else
        {
            //check if the click is in on the map
            if(MapUtils.IsInBounds(res))
            {
                return res;
            }
            else
            {
                return (-1,-1);
            }
        }        
    }

    /// <summary>
    /// returns the tent
    /// </summary>
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

    public void HandleMapClick(Vector2 pos)
    {
        TryMoveSheep(pos);
        ShakeTree(pos);
    }

    /// <summary>
    /// tries to move the sheep to the position clicked on screen
    /// </summary>
    public void TryMoveSheep(Vector2 pos)
    {
        (int col, int row) tilePos = GetTileFromScreenPosition(pos);
        if(tilePos.col < 0) return;
        Entity entity = GetStructure(tilePos);
        if(entity is Meadow meadow && !meadow.HasSheep)
        {
            int distance = MapUtils.CalculateTileDistance(SheepPosition, tilePos);
            if(sanityManager.TryDeductSanity(distance))
            {
                foreach (var position in MapUtils.GetSurroundingTiles(tilePos))
                {
                    if(Structures[position.Item1,position.Item2] is PlacableMeadow toActivate)
                    {
                        toActivate.ActivateMeadow(meadow.SanityGain);
                    }
                }
                foreach (var position in MapUtils.GetSurroundingTiles(SheepPosition))
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

    public void ShakeTree(Vector2 pos)
    {
        (int col, int row) tilePos = GetTileFromScreenPosition(pos, true);
        Entity entity = GetStructure(tilePos, true);
        if(entity is TreeTile tree)
        {
            tree.Shake();
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
        float screenHalfX = screenSize.X / 2f;
        float screenHalfY = screenSize.Y / 2f;
        // Clamp rightward/downward pan so the left/top exterior borders stay one tile off-screen
        float tileStep = TileWidth - ScaledOffset;
        float maxX = Math.Min(screenHalfX, (Columns - 1) * tileStep);
        float maxY = Math.Min(screenHalfY, (Rows - 1) * TileHeight);
        Vector2 maxOffset = new Vector2(maxX, maxY);
        Vector2 minOffset = -GetWorldPositionOfTileWithoutOffset((Columns,Rows)) + new Vector2(screenHalfX, screenHalfY);
        Offset = new Vector2(Math.Clamp(Offset.X, minOffset.X, maxOffset.X), Math.Clamp(Offset.Y, minOffset.Y, maxOffset.Y));
    }

    private Vector2 GetWorldPositionOfTileWithoutOffset((int col, int row) pos)
    {
        float tileX = pos.col * TileWidth - (pos.col * ScaledOffset);
        float tileY = pos.col % 2 == 0 ? pos.row * TileHeight : pos.row * TileHeight + ScaledOffset;

        return new Vector2(tileX, tileY);
    }

    /// <summary>
    /// Picks a tree animation name. bigChance and midChance are cumulative probabilities [0,1].
    /// Remainder goes to treeSmall_idle.
    /// </summary>
    private static string PickTreeAnim(double bigChance, double midChance)
    {
        double r = Random.Shared.NextDouble();
        if (r < bigChance) return "treeBig_idle";
        if (r < bigChance + midChance) return "treeMid_idle";
        return "treeSmall_idle";
    }

    /// <summary>
    /// Zooms the map by delta, towards the focus point
    /// </summary>
    public void Zoom(float delta, Vector2 focus, Point screenSize)
    {
        //where are we zooming in on?
        Vector2 oldMapPoint = focus - Offset;
        //get the new scale
        Vector2 newScale = new Vector2(Math.Clamp(Scale.X * (delta > 0 ? 1.09f : 0.91f), 1f, 8f), Math.Clamp(Scale.Y * (delta > 0 ? 1.09f : 0.91f), 1f, 8f));
        //how much did the scale change?
        Vector2 scaleRatio = newScale / Scale;
        //figure out wherere the focused point is going to be once the map is scaled
        Vector2 newMapPoint = oldMapPoint * scaleRatio;
        //correct the offset so that the focus point stays at the same place on screen
        Vector2 correction = oldMapPoint - newMapPoint;
        MoveMap(correction, screenSize);
        Scale = newScale;
        
        // NEW: scale all the monster containers
        foreach(MonsterGroupContainer container in MonsterContainers)
        {
            container.SetScale(newScale);
        }
    }
#endregion

#region Animation

    public void StartFalling()
    {
        isFalling = true;
        for (int i = 0; i < Columns * 3; i++)
        {
            for (int j = 0; j < Rows*3; j++)
            {
                int dist = MapUtils.CalculateTileDistance((i - Columns,j-Rows), GetTent());
                FallTimer[i,j] = TimeSpan.FromSeconds(-((shakeMax * (float)Random.Shared.NextDouble()) + (Math.Min(dist, 25) * shakeOffset)));
            }
        }
        MaxTotalFallTime = -FallTimer.Cast<TimeSpan>().MaxBy(i => -i + MaxFallTime) + MaxFallTime;
        TotalFallTime = TimeSpan.Zero;
    }

    public void ResetFall()
    {
        for (int i = 0; i < Columns * 3; i++)
        {
            for (int j = 0; j < Rows*3; j++)
            {
                TilesOfsets[i,j] = Vector2.Zero;
            }
        }
        MaxTotalFallTime = TimeSpan.FromSeconds(1f);
        TotalFallTime = TimeSpan.Zero;
    }

#endregion

#if DEBUG
public bool[,] Debug{get; private set;}

#endif

}