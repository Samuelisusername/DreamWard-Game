using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Gamelab.Managers;
using System.Collections.Generic;
using System.Linq;

public class CombatManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    private Dictionary<MonsterGroup, float> _monsterGruntTimers = new Dictionary<MonsterGroup, float>();
    
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;

    private float _sanityDamageAccumulator;
    private Dictionary<Tower, float> _towerSoundTimers = new Dictionary<Tower, float>();

    private List<(int,int)> spawnableArea;

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
        // var list = GetSpawnableArea();
        // foreach (var item in list)
        // {
        //     mapManager.PlaceEntity(new Fence("fence_idle", item),item);
        // }
        for (int i = 0; i < count; i++)
        {
            (int, int) pos = spawnableArea[Random.Shared.Next(spawnableArea.Count)];
            if(!mapManager.AddNewMonster(pos))
            {
                i--;
            }
            
        }
    }

    public void GetSpawnableArea()
    {
        List<(int,int)> search = [mapManager.GetTent()];
        List<(int,int)> res = [];
        int i = 0;
        while(i < search.Count)
        {
            List<(int,int)> toCheck = MapUtils.getSurroundingTiles(search[i], true);
            int count = 0;
            foreach ((int col, int row) tile in toCheck)
            {
                if(MapUtils.IsInBounds(tile))
                {
                    if(mapManager.GetStructure(tile) is not TreeTile && !search.Contains(tile))
                    {
                        search.Add(tile);
                    }
                }
                else
                {
                    count++;
                }
            }
            if(count > 0 && mapManager.GetStructure(search[i]) is not TreeTile)
            {
                res.Add(search[i]);
            }
            i++;
        }

        for (int j = 0; j < res.Count; j++)
        {
        
        }

        spawnableArea = res;
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

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // 2. Towers Attack
        foreach (Entity entity in mapManager.Structures)
        {
            if (entity is Tower tower && !tower.IsDestroyed)
            {
                var tilesInRange = tower.GetTilesInRange();
                bool dealtDamageThisFrame = false;
                foreach (var tile in tilesInRange)
                {
                    // Target the new Container!
// NEW
                    var targetContainer = mapManager.GetMonsterContainer(tile);
                    if (targetContainer != null && !targetContainer.IsEmpty)
                    {
                        // Check if the tower shooting is an AreaTower!
                        bool isAOE = tower is AreaTower; 
                        
                        // Pass that info into the container
                        targetContainer.TakeDamage(tower.DamagePerSecond * dt, isAOE); 
                        dealtDamageThisFrame = true;
                    }
                }
                
                if (dealtDamageThisFrame)
                {
                    if (!_towerSoundTimers.ContainsKey(tower))
                        _towerSoundTimers[tower] = 0f;

                    _towerSoundTimers[tower] -= dt;

                    if (_towerSoundTimers[tower] <= 0f)
                    {
                        string soundToPlay = tower is AreaTower ? "AOE-tower_stomp" : "shoot_01"; 
                        float cooldown = tower is AreaTower ? 1.5f : 1.0f;

                        AudioManager.Play(soundToPlay);
                        _towerSoundTimers[tower] = cooldown; 
                    }
                }
            }
        }

        // 3. Monsters Attack
        foreach (MonsterGroupContainer container in GetAllContainers())
        {
            foreach (var monsterGroup in container.Monstergroups)
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
                
                if (entity is Tower deadTower && _towerSoundTimers.ContainsKey(deadTower))
                {
                    _towerSoundTimers.Remove(deadTower);
                }
                anyStructureDestroyed = true;
                mapManager.RemoveStructure(entity.TilePosition);
            }
        }

        if (anyStructureDestroyed)
        {
            foreach (MonsterGroupContainer container in GetAllContainers())
            {
                foreach (var monsterGroup in container.Monstergroups)
                {
                    monsterGroup.QueuePathRecalc();
                }
            }
        }
    }
}