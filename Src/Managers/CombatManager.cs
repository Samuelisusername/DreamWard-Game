using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Gamelab.Managers;
using System.Collections.Generic;
using System.Linq;
using Gamelab.Utils.Logging;
using Gamelab.Screens;

public class CombatManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    private Dictionary<MonsterGroup, float> _monsterGruntTimers = new Dictionary<MonsterGroup, float>();
    
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;

    private float _sanityDamageAccumulator;

    public List<(int,int)> SpawnableArea {get; private set;}
    public List<(int,int)> PlayingField {get; private set;}
    private Logger logger = new Logger("Combat Manager");

    public MonsterGroup PriorityGroup {get;set;}

    // HELPER: Scans the map grid to get a snapshot of all active containers!
    private List<MonsterGroupContainer> GetAllContainers()
    {
        var activeContainers = new List<MonsterGroupContainer>();
        for (int i = 0; i < mapManager.Columns; i++)
        {
            for (int j = 0; j < mapManager.Rows; j++)
            {
                var container = mapManager.GetMonsterContainer((i, j));
                if (container != null && !container.IsEmpty)
                {
                    activeContainers.Add(container);
                }
            }
        }
        return activeContainers;
    }

    public void SpawnMonsters(int count, int level)
    {
        // foreach (var item in spawnableArea)
        // {
        //     mapManager.Debug[item.Item1, item.Item2] = true;
        // }
        // mapManager.AddNewMonster((6,15));
        // mapManager.AddNewMonster((6,16));
        int maxattempts = 5*count;
        int attempts = 0; 
        
        for (int i = 0; i < count; i++)
        {
            attempts++;
            if(SpawnableArea.Count == 0)
            {
                Console.WriteLine("no spawnable areas for monsters");
                GameOverScreen.DidWin = false; 
                GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
                return;
            }
            (int, int) pos = SpawnableArea[GamelabGame.Rng.Next(SpawnableArea.Count)];
            if(!mapManager.AddNewMonster(pos, level))
            {
                i--;
            }   
            if(attempts > maxattempts){
                Console.WriteLine("not enough spawnable areas for monsters");
                GameOverScreen.DidWin = false; 
                GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
                return;
            }         
        }    

        // 2. Update all the Mergeanims
        foreach (MonsterGroupContainer container in mapManager.MonsterContainers) 
        {
            container.InitMerge();
        }
    }

    /// <summary>
    /// Gets the spawnable area of the map, these are all the tiles
    /// that are bordering the mpBorder or a tree that is connected to the mapborder by other trees
    /// </summary>
    public void GetSpawnableArea()
    {
        bool[,] canSpawn = new bool[mapManager.Columns,mapManager.Rows];
        (int col, int row) tentPos = mapManager.GetTent();
        canSpawn[tentPos.col, tentPos.row] = true;
        List<(int,int)> search = [tentPos];        
        List<(int,int)> res = [];
        //bfs to find all the spawnable tiles connected to the tent dialated by one tile 
        while(search.Count > 0)
        {
            List<(int,int)> toCheck = MapUtils.GetSurroundingTiles(search[0], true);
            foreach ((int col, int row) tile in toCheck)
            {
                if(MapUtils.IsInBounds(tile))
                {
                    int tileIndex = mapManager.GetTileIndex(tile);
                    if(tileIndex != -1){
                        if(mapManager.GetStructure(tile) is not TreeTile && !canSpawn[tile.col, tile.row])
                        {
                            // logger.Info($"tile: {tile} got added by: {search[0]}");
                            search.Add(tile);
                        }
                        canSpawn[tile.col, tile.row] = true;
                    }
                }
            }
            search.RemoveAt(0);
        }
        
        //find all the trees that are connected to the border of the map and run BFS on the trees to mark them as unspawnable area
        for(int i = 0; i < mapManager.Columns; i++)
        {
            for(int j = 0; j < mapManager.Rows; j++)
            {
                if(canSpawn[i,j])
                {
                    List<(int col, int row)> toCheck = MapUtils.GetSurroundingTiles((i,j), true);
                    if(toCheck.Any(k => !MapUtils.IsInBounds(k) || (!canSpawn[k.col,k.row] && MapUtils.IsInBounds(k) && mapManager.GetStructure(k) != null)) && mapManager.GetStructure((i,j)) is TreeTile)
                    {
                        //found a tree on the edge/ in a big cluster so do bfs to find all the trees in that group
                        canSpawn[i,j] = false;
                        search = [(i,j)];
                        while(search.Count > 0)
                        {                            
                            List<(int,int)> neighbors = MapUtils.GetSurroundingTiles(search[0], true);
                            foreach ((int col, int row) tile in neighbors)
                            {
                                if(MapUtils.IsInBounds(tile))
                                {
                                    if(mapManager.GetStructure(tile) is TreeTile && canSpawn[tile.col, tile.row])
                                    {
                                        search.Add(tile);
                                        canSpawn[tile.col, tile.row] = false;
                                    }
                                }
                            }
                            search.RemoveAt(0);
                        }
                    }
                }
            }
        }

        PlayingField = new List<(int, int)>();
        for(int i = 0; i < mapManager.Columns; i++)
        {
            for(int j = 0; j < mapManager.Rows; j++)
            {
                if(canSpawn[i,j])
                {
                    PlayingField.Add((i,j));
                }
            }
        }
        bool tent_connected_to_border = PlayingField.Any(k=> MapUtils.GetSurroundingTiles(k).Any(l => !MapUtils.IsInBounds(l)));
        if (!tent_connected_to_border)
        {
            GameOverScreen.DidWin = false;
        }
        //take the border of the spawnable area and save it as the spawnable tiles
        for(int i = 0; i < mapManager.Columns; i++)
        {
            for(int j = 0; j < mapManager.Rows; j++)
            {
                if(canSpawn[i,j])
                {
                    List<(int col, int row)> toCheck = MapUtils.GetSurroundingTiles((i,j), true);
                    if(toCheck.Any(k => !MapUtils.IsInBounds(k) || !canSpawn[k.col,k.row]))
                    {
                        if(toCheck.Any(k=> MapUtils.IsInBounds(k) && mapManager.GetTileIndex(k)==-1)) continue;
                        res.Add((i,j));
                    }
                }
            }
        }

        SpawnableArea = res;
        Console.WriteLine("getspawnableAre finished");
        Console.WriteLine($"Playing field count: {PlayingField.Count};        Spawnable area count: {res.Count}");
        // foreach(var tiles in res)
        // {
        //     Console.WriteLine($"{mapManager.GetTileIndex(tiles)}");
        // }
    }
    

    public void ManageMoveAndAttack(GameTime gameTime)
    {    
        foreach (MonsterGroupContainer container in GetAllContainers()) 
        {
            container.Update(gameTime);
        }

        // 1. Move everything using our snapshot
        foreach (MonsterGroupContainer container in GetAllContainers()) 
        {
            container.Move();
        }

        // 1. Move everything using our snapshot
        foreach (MonsterGroupContainer container in GetAllContainers()) 
        {
            container.CleanUpGroups();
        }

        // 2. Update all the Mergeanims
        foreach (MonsterGroupContainer container in mapManager.MonsterContainers) 
        {
            container.InitMerge();
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // 2. Towers Attack
        foreach (Entity entity in mapManager.Structures)
        {
            if (entity is Tower tower && !tower.IsDestroyed)
            {
                tower.Attack(gameTime);
            }
        }

        // 3. Monsters Attack
        foreach (MonsterGroupContainer container in GetAllContainers())
        {
            foreach (var monsterGroup in container.MonsterGroups)
            {
                monsterGroup.Attack(gameTime);
            }
        }

        // 4. Clean up Dead Monsters
        foreach (MonsterGroupContainer container in GetAllContainers())
        {
            container.RemoveDead();
        }

        // 5. Structure Destruction Check
        bool anyStructureDestroyed = false;
        foreach (Entity entity in mapManager.Structures)
        {
            if (entity != null && entity.IsDestroyed)
            {
                if (entity is Tent) mapManager.TentDestroyed = true;
                if (entity is Meadow destroyedMeadow)
                {
                    foreach (var pos in MapUtils.GetSurroundingTiles(entity.TilePosition))
                    {
                        if (mapManager.Structures[pos.Item1, pos.Item2] is PlacableMeadow pm)
                            pm.DeactivateMeadow();
                    }
                }
                anyStructureDestroyed = true;
                mapManager.RemoveStructure(entity.TilePosition);
            }
        }

        if (anyStructureDestroyed)
        {
            foreach (MonsterGroupContainer container in GetAllContainers())
            {
                foreach (var monsterGroup in container.MonsterGroups)
                {
                    monsterGroup.QueuePathRecalc();
                }
            }
        }
    }
}