using System;
using Microsoft.Xna.Framework;
using DreamWardUtils.Graphics;
using Gamelab;
using System.Collections.Generic;
using Gamelab.Managers;
using System.Linq;
using Gamelab.Utils.Logging;
using Gamelab.Screens;
using Microsoft.Xna.Framework.Graphics;

public class MapManager
{ 
#region Managers
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CombatManager combatManager => GamelabGame.CombatManager;
    private CardManager cardManager => GamelabGame.CardManager;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    // Add this near your other public properties
    
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
    // Add this near your other public properties
    public float TimeScale { get; set; } = 1.0f;
    
    // NEW: stores the container for each tile, handling all monsters
    public readonly MonsterGroupContainer[,] MonsterContainers; //colfirst
    // NEW: Tracks which specific tiles in the [Columns*3, Rows*3] grid are falling
    private bool[,] _isFallingTile;
    private HashSet<(int col, int row)> _pendingTileClear = new();
    public bool IsVoidFallingTile(int col, int row) => _pendingTileClear.Contains((col, row));

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
    public Vector2 Offset => offset + screenShakeOffset;
    
    /// <summary>
    /// The width of a single tile
    /// </summary>
    public float TileWidth => _tileSets["day"].TileWidth * Scale.X;

    /// <summary>
    /// Effective height is 32. The rest is only visible on borders.
    /// </summary>
    public float TileHeight => (_tileSets["day"].TileWidth / 2) * Scale.Y;

    /// <summary>
    /// The region of the hexagon that overlaps with other hexagons
    /// </summary>
    public float ScaledOffset => TileHeight /2;

    public static float MoveCostMultiplier => GamelabGame.Instance.Balance.MoveCostMultiplier;

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

    private Vector2 offset;

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
    private TimeSpan maxScreenShakeTime;
    private TimeSpan screenShakeTime;
    private float screenShakeIntensity;
    private Vector2 screenShakeOffset;


#endregion


    public MapManager(TileSet setDay, TileSet setNight, int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        _tileSets = new Dictionary<string, TileSet>
        {
            { "day", setDay },
            { "night", setNight }
        };
        _isFallingTile = new bool[Columns * 3, Rows * 3];

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
        Scale = new Vector2(2.5f, 2.5f);
        foreach (MonsterGroupContainer container in MonsterContainers)
            container.SetScale(Scale);
        MapUtils.Setup(Columns,Rows);
    }
    
    public void Update(GameTime gameTime)
    {   
        float multiplier = (!gamePhaseManager.IsDay) ? TimeScale : 1.0f; // Only speed up at night
        TimeSpan scaledElapsed = TimeSpan.FromTicks((long)(gameTime.ElapsedGameTime.Ticks * multiplier));
    
        // Create a temporary GameTime object for scaled logic
        GameTime scaledGameTime = new GameTime(gameTime.TotalGameTime, scaledElapsed);
        //progress the animation using the active tileset's frame duration from JSON
        TimeSpan delay = _tileSets[gamePhaseManager.IsDay ? "day" : "night"].FrameDuration;
        _elapsed += scaledElapsed;
        if(_elapsed >= delay)
        {
            _elapsed -= delay;
            _animationOffset = (_animationOffset + 1) % 2;
        }
        if (isFalling)
        {
            TotalFallTime += gameTime.ElapsedGameTime;
            for (int i = 0; i < Columns * 3; i++)
            {
                for (int j = 0; j < Rows * 3; j++)
                {
                    // NEW: If this specific tile isn't marked for falling, skip it!
                    if (!_isFallingTile[i, j]) continue;

                    if (FallTimer[i, j] > TimeSpan.Zero)
                    {
                        // Falling phase
                        FallTimer[i, j] += gameTime.ElapsedGameTime;
                        TilesOfsets[i, j] = Vector2.UnitY * (float)FallTimer[i, j].TotalSeconds * fallSpeed * Scale;
                    }
                    else
                    {
                        // Shaking phase
                        FallTimer[i, j] += gameTime.ElapsedGameTime;
                        if (FallTimer[i, j] > TimeSpan.Zero)
                        {
                            TilesOfsets[i, j] = Vector2.UnitY * (float)FallTimer[i, j].TotalSeconds * fallSpeed * Scale;
                        }
                        else
                        {
                            TilesOfsets[i, j] = new Vector2((float)Random.Shared.NextDouble(), (float)Random.Shared.NextDouble()) * Math.Clamp((float)-FallTimer[i, j].TotalSeconds, 0.0f, 4.0f) * Scale;
                        }
                    }
                }
            }
            // Existing check to stop the animation
            if (FallTimer.Cast<TimeSpan>().All(i => i > MaxFallTime))
            {
                isFalling = false;
                foreach (var (col, row) in _pendingTileClear)
                {
                    RemoveStructure((col, row));
                    _tiles[col, row] = -1;
                }
                _pendingTileClear.Clear();
                gamePhaseManager.FinishSwitchToNight();
            }
        }

        if (screenShakeTime > TimeSpan.Zero)
        {
            screenShakeTime -= gameTime.ElapsedGameTime;

            // Calculate how "strong" the shake is right now (0.0 to 1.0)
            float progress = (float)(screenShakeTime / maxScreenShakeTime); // Assuming a 0.5s default or use a maxDuration variable
            float currentPower = screenShakeIntensity * progress;

            // Get a random direction using the "Circle" logic from before!
            double angle = Random.Shared.NextDouble() * Math.PI * 2;
            screenShakeOffset = new Vector2(
                (float)Math.Cos(angle) * currentPower * Scale.X,
                (float)Math.Sin(angle) * currentPower * Scale.Y
            );
        }
        //Update all the monsters and structures
        for(int i = 0; i < Columns; i++)
        {
            for(int j = 0; j < Rows; j++)
            {
                Structures[i,j]?.Update(scaledGameTime);
            }
        }
        
        foreach (var tree in ExteriorStructures)
        {
            tree?.Update(gameTime);
        }

        //if its night manage monsters
        if(!gamePhaseManager.IsDay)
        {
            combatManager.ManageMoveAndAttack(scaledGameTime);
            // NEW: Check if ALL containers on the map are entirely empty
            if(MonsterContainers.Cast<MonsterGroupContainer>().All(c => c.IsEmpty))
            {
                GamelabGame.GameSpeed = 1f;
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
            
            // NEW: If the interior tile is empty space, safely return null
            if (_tiles[pos.col, pos.row] < 0) return null;

            return gamePhaseManager.IsDay ?
                _tileSets["day"].GetTile(_tiles[pos.col,pos.row] + _animationOffset) :
                _tileSets["night"].GetTile(_tiles[pos.col, pos.row] + _animationOffset);
        }
        
        // NEW: If the exterior tile is empty space, safely return null
        if (_exteriorTiles[pos.col, pos.row] < 0) return null;

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

    public void TriggerVoidEnclosureFall()
{
    Array.Clear(_isFallingTile, 0, _isFallingTile.Length);
    bool[,] isSafe = new bool[Columns, Rows];
    Queue<(int col, int row)> checkQueue = new Queue<(int, int)>();

    var tentPos = GetTent();
    if (tentPos.col == -1) return;

    checkQueue.Enqueue((tentPos.col, tentPos.row));
    isSafe[tentPos.col, tentPos.row] = true;

    // 1. Flood Fill
    while (checkQueue.Count > 0)
    {
        var current = checkQueue.Dequeue();

        foreach (var neighbor in MapUtils.GetSurroundingTiles(current))
        {
            int c = neighbor.Item1;
            int r = neighbor.Item2;

            if (isSafe[c, r]) continue;

            var structure = GetStructure(neighbor);

            // Check if it's a VoidTile OR if the base ground tile is missing (-1)
            bool isEmptySpace = _tiles[c, r] == -1; 
            bool isVoid = structure is VoidTile || isEmptySpace;

            if (isVoid)
            {
                isSafe[c, r] = true;
            }
            else
            {
                // Connected land
                isSafe[c, r] = true;
                checkQueue.Enqueue((c, r));
            }
        }
    }

    // 2. Drop the cut-off tiles AND the boundary VoidTiles
    for (int col = 0; col < Columns; col++)
    {
        for (int row = 0; row < Rows; row++)
        {
            var structure = GetStructure((col, row));
            bool isVoidTile = structure is VoidTile;

            // Drop if it's cut off from the tent OR if it's part of the VoidTile ring
            if (!isSafe[col, row] || isVoidTile)
            {
                int mapI = col + Columns;
                int mapJ = row + Rows;

                _isFallingTile[mapI, mapJ] = true;
                SetDistanceFallTimer(mapI, mapJ);

                // Defer clearing until animation completes so the tile stays visible while falling
                _pendingTileClear.Add((col, row));
            }
        }
    }

    // Refresh PlayingField now that void tiles and enclosed tiles are cleared,
    // so the fall renderer sees the updated boundary and animates cluster trees correctly.
    combatManager.GetSpawnableArea();
}
    // In MapManager.cs
    public int GetTileIndex((int col, int row) pos) {
        if (!MapUtils.IsInBounds(pos)) return -1;
        return _tiles[pos.col, pos.row];
    }
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
                if(roll < 5)
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
                else
                {
                    newMonster = new FlyingMonster(pos);
                }
                break;
            default:
                newMonster = new BigMonster(pos); 
                break;
        }
        int tileIndex = _tiles[pos.col, pos.row];
        if (!newMonster.CanStepOnTile(tileIndex))
        {
            return false;
        }
        if(structureOnTile is not null && !newMonster.CanStepOn(structureOnTile))
        {
            return false;
        }
        // Let the container handle sorting it into the correct group
        MonsterContainers[pos.col, pos.row].AddExistingMonster(newMonster); 
        return true;     
    }

    public void AddExistingMonsterGroup(MonsterGroup monsterGroup,  (int col, int row) pos)
    {
        MonsterContainers[pos.col, pos.row].AddExistingMonsterGroup(monsterGroup);
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

    public List<MonsterGroup> GetMonsterGroupsInRange((int col, int row) pos, int range)
    {
        List<MonsterGroup> res = new List<MonsterGroup>();
        
        var tilesInRangeGuaranteed = MapUtils.GetSurroundingCircle(pos, 0, range);

        foreach (var tile in tilesInRangeGuaranteed)
        {
            // Target the new Container!
            var targetContainer = GetMonsterContainer(tile);
            if (targetContainer != null && !targetContainer.IsEmpty)
            {
                foreach (var group in targetContainer.MonsterGroups)
                {
                    if (!group.IsEmpty)
                    {
                        res.Add(group);
                    }
                }
            }
        }

        var tilesInRangePossible = MapUtils.GetSurroundingCircle(pos, range, range + 2);

        foreach (var tile in tilesInRangePossible)
        {
            var targetContainer = GetMonsterContainer(tile);
            if (targetContainer != null && !targetContainer.IsEmpty)
            {
                foreach (var group in targetContainer.MonsterGroups)
                {
                    var startDist = MapUtils.CalculateTileDistance(group.TilePosition, pos);
                    var endDist = MapUtils.CalculateTileDistance(group.CommonNextTarget(), pos);
                    if (!group.IsEmpty && (startDist <= range || endDist <= range))
                    {
                        var worldPosition = group.GetInterpolatedWorldPosition(true);
                        //TODO: add the correct offsets
                        switch (group.MonsterType)
                        {
                            case MonsterType.Regular:
                                worldPosition = worldPosition +  (new Vector2(45, 24) * Scale );
                                break;
                            case MonsterType.Flying:
                                worldPosition = worldPosition +  (new Vector2(21, 24) * Scale );
                                break;
                            case MonsterType.Small:
                                worldPosition = worldPosition +  (new Vector2(33, 31) * Scale );
                                break;
                            case MonsterType.Big:
                                worldPosition = worldPosition +  (new Vector2(33, 30) * Scale );
                                break;
                        }
                        var mapPos = GetTileFromScreenPosition(worldPosition);
                        if(MapUtils.CalculateTileDistance(pos, mapPos) <= range)
                        {
                            res.Add(group); 
                        }
                    }
                }
            }
        }

        return res;
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
        GamelabGame.PathFindingManager.NotifyStructuresChanged();
    }
    
    /// <summary>
    /// Places an entity at the given position
    /// </summary>
    public bool PlaceEntity(Structure structure, (int col, int row) pos)
    {        
        bool occupied = Structures[pos.col, pos.row] != null;
        if (occupied)
        {
            // meadow init has a retry loop. can be ignored in that case
            logger.Info("Tile already occupied! " + pos);

            return false;
        }

        if (structure != null)
        {
            Structures[pos.col, pos.row] = structure;
            GamelabGame.PathFindingManager.NotifyStructuresChanged();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Places a Sheep at the given position
    /// </summary>
    public bool PlaceSheep(Meadow entity, (int col, int row) pos)
    {
        if(!PlaceEntity(entity, pos))
        {
            return false;
        }        
        entity.AddSheep();
        SheepPosition = (pos.col, pos.row);
        GamelabGame.PathFindingManager.NotifyStructuresChanged();
        return true;
    }


    /// <summary>
    /// Places an entity defined by the Card at the given position
    /// </summary>
    public bool PlaceEntity(Card card, (int col, int row) pos)
    {
        Structure existing = Structures[pos.col, pos.row];
        if (existing != null)
        {
            Structure incoming = cardManager.GetStructureFromCard(card, pos);
            if (incoming != null && existing.GetType() == incoming.GetType() && existing.CurrentHealth < existing.MaxHealth)
            {
                existing.Heal(existing.MaxHealth);
                return true;
            }
            Console.WriteLine("Tile already occupied!");
            AudioManager.Play("sheep_baa");
            return false;
        }

        Structure structure = cardManager.GetStructureFromCard(card, pos);

        if (structure != null){
            Structures[pos.col, pos.row] = structure;
            GamelabGame.PathFindingManager.NotifyStructuresChanged();
            if(structure is PlacableMeadow placableMeadow)
            {
                foreach (var position in MapUtils.GetSurroundingTiles(pos))
                {
                    if(Structures[position.Item1,position.Item2] is Meadow meadow && meadow.HasSheep && !meadow.IsDepleted)
                    {
                        placableMeadow.ActivateMeadow(meadow.SanityGain);
                    }
                }
            }
            InitScreenShake(TimeSpan.FromSeconds(0.3), 5);
            TutorialManager.HandleTutorialAction(TutorialAction.PlacedTower);
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

    public void FillMap()
    {
        int centerX = Columns / 2;
        int centerY = Rows / 2;

        // 🏕️ Tent at center
        PlaceEntity(new Tent("tent_idle",
            (centerX, centerY)
        ), (centerX, centerY));

        Meadow starterMeadow = new Meadow((centerX + 1, centerY), 5);

        // PlaceEntity(new Bomb((centerX, centerY + 1)), (centerX, centerY + 1));
        // PlaceEntity(new Bomb((centerX, centerY - 1)), (centerX, centerY - 1));
        // PlaceEntity(new Bomb((centerX-1, centerY + 1)), (centerX-1, centerY + 1));
        // PlaceEntity(new Bomb((centerX-1, centerY )), (centerX-1, centerY ));
        // PlaceEntity(new Bomb((centerX + 1, centerY + 1)), (centerX + 1, centerY + 1));
        // PlaceEntity(new Bomb((centerX + 1, centerY -1)), (centerX + 1, centerY -1));
        // PlaceEntity(new Bomb((centerX + 2, centerY)), (centerX + 2, centerY ));
        // PlaceEntity(new Bomb((centerX + 2, centerY - 1)), (centerX + 2, centerY - 1));


        // 🐑 Sheep next to tent
        PlaceSheep(starterMeadow ,(centerX + 1, centerY));

        // 🌲 Random trees
        AddTrees();
        combatManager.GetSpawnableArea();
        if (combatManager.SpawnableArea.Count-2 == 0)
        {
            Console.WriteLine("spawnableArea.count was 0");
            logger.Error("TENT IS ISOLATED! No ground connected to tent. Cancelling night.");
            GameOverScreen.DidWin = false; 
            GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
            return; 
        }
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
                        var tree = new TreeTile(PickFlairAnim(0.30, 0.65, 0.10), (i-Columns, j-Rows));
                        ExteriorStructures[i,j] = ExteriorStructures[i,j] ?? tree;
                    }
                    else if(GamelabGame.Rng.NextDouble() < 0.25)
                    {
                        var tree = new TreeTile(PickFlairAnim(0.01, 0.55, 0.44), (i-Columns, j-Rows));
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
                        var tree = new TreeTile(PickTreeAnim(0.4, 0.4), (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;
                    }
                    else if(noiseval < 0.5)
                    {
                        var tree = new TreeTile(PickFlairAnim(0.10, 0.45, 0.55), (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;
                    }
                    else if(GamelabGame.Rng.NextDouble() < 0.25 * relativeDist)
                    {
                        var tree = new TreeTile(PickFlairAnim(0.0, 0.35, 0.75), (i-Columns, j-Rows));
                        Structures[i-Columns,j-Rows] = Structures[i-Columns,j-Rows] ?? tree;
                    }
                }
            }
        }       
    }
    
    private void AddMeadows()
    {
        (int col, int row) sheepStart = SheepPosition;

        var zone1 = ShuffleList(new List<(List<string> displays, string label, Action<Meadow> grant)>
        {
            (new List<string>{ "bomb" },    null,                     m => cardManager.UnlockCard("bomb")),
            (new List<string>{ "spikes" }, null,                     m => cardManager.UnlockCard("spikes")),
            (null,                         "Adds New Tower Card Slot", m => cardManager.AddSlots(1)),
        });

        var zone2 = ShuffleList(new List<(List<string> displays, string label, Action<Meadow> grant)>
        {
            (new List<string>{ "dreamcatcher", "void" }, null,
                m => { cardManager.UnlockCard("dreamcatcher"); cardManager.UnlockCard("void"); }),
            (new List<string>{ "polymorph", "decoy" }, null,
                m => { cardManager.UnlockCard("polymorph"); cardManager.UnlockCard("decoy"); }),
            (null, "Adds New Barrier Card Slot",
                m => cardManager.AddSlots(2)),
        });

        var zone3 = ShuffleList(new List<(List<string> displays, string label, Action<Meadow> grant)>
        {
            (new List<string>{ "meadow", "buff" }, null,
                m => { cardManager.UnlockCard("meadow"); cardManager.UnlockCard("buff"); }),
            (null, "Bonus 10 Sanity",  m => { }),
            (null, "Adds New Support Card Slot", m => cardManager.AddSlots(3)),
        });

        var meadowGains = GamelabGame.Instance.Balance.MeadowSanityGains;
        PlaceMeadowsInZone(MapUtils.GetSurroundingCircle(sheepStart, 4,  5),  meadowGains["meadow_red_idle"],    zone1);
        PlaceMeadowsInZone(MapUtils.GetSurroundingCircle(sheepStart, 7,  8),  meadowGains["meadow_blue_idle"],   zone2);
        PlaceMeadowsInZone(MapUtils.GetSurroundingCircle(sheepStart, 10, 11), meadowGains["meadow_orange_idle"], zone3);
    }

    private void PlaceMeadowsInZone(
        List<(int,int)> zone,
        int sanityGain,
        List<(List<string> displays, string label, Action<Meadow> grant)> bonuses)
    {
        if (zone.Count == 0) return;
        int randomIndex = GamelabGame.Rng.Next(zone.Count);
        for (int i = 0; i < 3; i++)
        {
            randomIndex = (randomIndex + zone.Count / 3) % zone.Count;
            (int,int) newPos = zone[randomIndex];
            Meadow m = new Meadow(newPos, sanityGain);
            while (!PlaceEntity(m, newPos))
            {
                randomIndex = (randomIndex + 1) % zone.Count;
                newPos = zone[randomIndex];
                m = new Meadow(newPos, sanityGain);
            }
            m.UnlockCardSprites = bonuses[i].displays;
            m.BonusLabel        = bonuses[i].label;
            m.MeadowGrant       = bonuses[i].grant;

            if (bonuses[i].label == "Bonus 10 Sanity")
                m.BonusSanity = 10;
        }
    }

    private List<T> ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = GamelabGame.Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
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
        // At night only shake trees that are in the playing field (visible, not fallen)
        if (gamePhaseManager.IsInTransition)
        {
            var tile = GetTileFromScreenPosition(pos);
            if (tile.col < 0 || !combatManager.PlayingField.Contains(tile))
                return;
        }
        if(TrySetTarget(pos))
        {
            TutorialManager.CurrentInfoStructure = null;
            return;
        }
        if(ShakeTree(pos))
        {
            TutorialManager.CurrentInfoStructure = null;
            return;  
        }
        if(SetPopUpStructure(pos)) return;
        TutorialClick(pos);
    }

    private bool SetPopUpStructure(Vector2 pos)
    {
        (int col, int row) tilePos = GetTileFromScreenPosition(pos, true);
        Structure entity = GetStructure(tilePos, true);
        if(entity is not TreeTile)
        {
            TutorialManager.CurrentInfoStructure = entity;
            return true;
        }
        return false;
    }

    private bool TrySetTarget(Vector2 pos)
    {
        var tile = GetTileFromScreenPosition(pos);
        var potentialGroups = GetMonsterGroupsInRange(tile, 1);
        float minDist = float.MaxValue;
        MonsterGroup currentMin = null;
        foreach (var group in potentialGroups)
        {
            var worldPosition = group.GetInterpolatedWorldPosition(false);
            //TODO: add the correct offsets
            switch (group.MonsterType)
            {
                case MonsterType.Regular:
                    worldPosition = worldPosition +  (new Vector2(45, 24) * Scale);
                    break;
                case MonsterType.Flying:
                    worldPosition = worldPosition +  (new Vector2(21, 24) * Scale);
                    break;
                case MonsterType.Small:
                    worldPosition = worldPosition +  (new Vector2(33, 31) * Scale);
                    break;
                case MonsterType.Big:
                    worldPosition = worldPosition +  (new Vector2(33, 30) * Scale);
                    break;
            }
            var dist = Vector2.Distance(pos,worldPosition);
            if(dist < minDist)
            {
                minDist = dist;
                currentMin = group;
            }
        }
        if(currentMin == null) return false;
        combatManager.PriorityMonster = currentMin.Monsters.MinBy(i => i.CurrentHealth);
        return true;
    }

    /// <summary>
    /// tries to move the sheep to the position clicked on screen
    /// </summary>
    public void TryMoveSheep(Vector2 pos)
    {
        var oldMeadow = Structures[SheepPosition.Item1, SheepPosition.Item2] as Meadow;
        if(oldMeadow == null) return;
        if(gamePhaseManager.IsInTransition) return;
        (int col, int row) tilePos = GetTileFromScreenPosition(pos);
        if(tilePos.col < 0) return;
        Entity entity = GetStructure(tilePos);
        if(entity is Meadow meadow && !meadow.HasSheep)
        {
            int distance = MapUtils.CalculateTileDistance(SheepPosition, tilePos);
            int cost = (int)MathF.Floor(distance * MapManager.MoveCostMultiplier);
            if(sanityManager.TryDeductSanity(cost))
            {
                var newNeighbors = MapUtils.GetSurroundingTiles(tilePos);
                foreach (var position in newNeighbors)
                {
                    if(Structures[position.Item1,position.Item2] is PlacableMeadow toActivate)
                    {
                        toActivate.ActivateMeadow(meadow.SanityGain);
                    }
                }
                foreach (var position in MapUtils.GetSurroundingTiles(SheepPosition))
                {
                    if(newNeighbors.Contains(position)) continue;
                    if(Structures[position.Item1,position.Item2] is PlacableMeadow toDeactivate)
                    {
                        toDeactivate.DeactivateMeadow();
                    }
                }
                bool wasPolymorphed = oldMeadow.IsPolymorphed;
                oldMeadow.RemoveSheep();
                meadow.AddSheep(wasPolymorphed);
                GamelabGame.PathFindingManager.NotifyStructuresChanged();
                SheepPosition = (tilePos.col, tilePos.row);
                // Get New Stack Cards
                GamelabGame.CardManager.GrantStackCard();
                TutorialManager.HandleTutorialAction(TutorialAction.MovedSheep);
            }
        }
    }

    public bool ShakeTree(Vector2 pos)
    {
        (int col, int row) tilePos = GetTileFromScreenPosition(pos, true);
        Entity entity = GetStructure(tilePos, true);
        if(entity is TreeTile tree)
        {
            tree.Shake();
            return true;
        }
        return false;
    }

    private void TutorialClick(Vector2 pos)
    {
        (int col, int row) tilePos = GetTileFromScreenPosition(pos, true);
        Entity entity = GetStructure(tilePos, true);
        if(entity == TutorialManager.CurrentTutorialStructure)
        {
            TutorialManager.HandleTutorialAction(TutorialAction.Click);
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
        offset = -GetWorldPositionOfTileWithoutOffset((Columns/2,Rows/2)) + (screenSize.ToVector2() / 2) - new Vector2(TileWidth/2,TileHeight/2);
    }
    /// <summary>
    /// Moves the map into the direction of delta
    /// </summary>
    public void MoveMap(Vector2 delta, Point screenSize)
    {
        offset += delta;
        float screenHalfX = screenSize.X / 2f;
        float screenHalfY = screenSize.Y / 2f;
        // Clamp rightward/downward pan so the left/top exterior borders stay one tile off-screen
        float tileStep = TileWidth - ScaledOffset;
        float maxX = Math.Min(screenHalfX, (Columns - 1) * tileStep);
        float maxY = Math.Min(screenHalfY, (Rows - 1) * TileHeight);
        Vector2 maxOffset = new Vector2(maxX, maxY);
        Vector2 minOffset = -GetWorldPositionOfTileWithoutOffset((Columns,Rows)) + new Vector2(screenHalfX, screenHalfY);
        offset = new Vector2(Math.Clamp(offset.X, minOffset.X, maxOffset.X), Math.Clamp(offset.Y, minOffset.Y, maxOffset.Y));
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

    private static string PickFlairAnim(double bigChance, double midChance, double smallChance)
    {
        double r = Random.Shared.NextDouble();
        if (r < bigChance)                              return "treeBig_idle";
        if (r < bigChance + midChance)                  return "treeMid_idle";
        if (r < bigChance + midChance + smallChance)    return "treeSmall_idle";
        return "hill_idle";
    }

    /// <summary>
    /// Zooms the map by delta, towards the focus point
    /// </summary>
    public void Zoom(float delta, Vector2 focus, Point screenSize)
    {
        //where are we zooming in on?
        Vector2 oldMapPoint = focus - offset;
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
    
    private void SetDistanceFallTimer(int mapI, int mapJ)
    {
        int dist = MapUtils.CalculateTileDistance((mapI - Columns, mapJ - Rows), GetTent());
        FallTimer[mapI, mapJ] = TimeSpan.FromSeconds(-((shakeMax * (float)Random.Shared.NextDouble()) + (Math.Min(dist, 25) * shakeOffset)));
    }

    public void StartFalling()
{
    isFalling = true;
    for (int i = 0; i < Columns * 3; i++)
    {
        for (int j = 0; j < Rows * 3; j++)
        {
            if (_isFallingTile[i, j]) continue;
            _isFallingTile[i, j] = true;
            SetDistanceFallTimer(i, j);
        }
    }
}

    public void ResetFall()
    {
        for (int i = 0; i < Columns * 3; i++)
        {
            for (int j = 0; j < Rows*3; j++)
            {
                TilesOfsets[i,j] = Vector2.Zero;
                FallTimer[i,j] = TimeSpan.Zero;
            }
        }
        MaxTotalFallTime = TimeSpan.FromSeconds(1f);
        TotalFallTime = TimeSpan.Zero;
    }

    public void InitScreenShake(TimeSpan duration, float intensity)
    {
        screenShakeTime = duration;
        maxScreenShakeTime = duration;
        screenShakeIntensity = intensity;
    }

#endregion

#if DEBUG
public bool[,] Debug{get; private set;}

#endif

}