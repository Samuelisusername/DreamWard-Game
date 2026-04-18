using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Gamelab.Managers;
using System.Collections.Generic;
using System.Linq;
using Gamelab.Utils.Logging;

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

    public void SpawnMonsters(int count)
    {
        // foreach (var item in spawnableArea)
        // {
        //     mapManager.Debug[item.Item1, item.Item2] = true;
        // }
        // mapManager.AddNewMonster((6,15));
        // mapManager.AddNewMonster((6,16));
        for (int i = 0; i < count; i++)
        {
            (int, int) pos = SpawnableArea[GamelabGame.Rng.Next(SpawnableArea.Count)];
            if(!mapManager.AddNewMonster(pos))
            {
                i--;
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
                    if(mapManager.GetStructure(tile) is not TreeTile && !canSpawn[tile.col, tile.row])
                    {
                        // logger.Info($"tile: {tile} got added by: {search[0]}");
                        search.Add(tile);
                    }
                    canSpawn[tile.col, tile.row] = true;
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
                    if(toCheck.Any(k => !MapUtils.IsInBounds(k) || !canSpawn[k.col,k.row]) && mapManager.GetStructure((i,j)) is TreeTile)
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
                        res.Add((i,j));
                    }
                }
            }
        }

        SpawnableArea = res;
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
                tower.Attack(dt);
            }
        }

        // 3. Monsters Attack
        foreach (MonsterGroupContainer container in GetAllContainers())
        {
            foreach (var monsterGroup in container.MonsterGroups)
            {
                foreach (var monster in monsterGroup.Monsters)
                {
                    if (!monster.GoalReached) continue; 

                    var goalTile = monster.AttackTarget ?? monster.TilePosition;
                    var structure = mapManager.GetStructure(goalTile);
                    if (structure == null || structure.IsDestroyed) continue;
                    if (structure is Meadow m && !m.HasSheep) continue;

                    else if(sanityManager != null && structure is Tent)
                    {                           
                        float damage = monster.DamagePerSecond * dt;
                        _sanityDamageAccumulator += damage;
                    }
                    else
                    {
                        monster.Attack(structure, dt);

                        if (!_monsterGruntTimers.ContainsKey(monsterGroup))
                        {
                            _monsterGruntTimers[monsterGroup] = 0f;
                        }

                        _monsterGruntTimers[monsterGroup] -= dt;

                        if (_monsterGruntTimers[monsterGroup] <= 0f)
                        {
                            AudioManager.Play("grunt_03"); 
                            _monsterGruntTimers[monsterGroup] = 1.0f; 
                        }
                    }   
                }
            }
            
            if (_sanityDamageAccumulator >= 1f)
            {
                int toDeduct = (int)_sanityDamageAccumulator;
                sanityManager.DeductSanity(toDeduct);
                _sanityDamageAccumulator -= toDeduct;
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