using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Gamelab.Managers;
using System.Collections.Generic;

public class CombatManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    private Dictionary<MonsterGroupNew_Unfinished, float> _monsterGruntTimers = new Dictionary<MonsterGroupNew_Unfinished, float>();
    
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;

    private float _sanityDamageAccumulator;
    private Dictionary<Tower, float> _towerSoundTimers = new Dictionary<Tower, float>();

    // HELPER: Scans the map grid to get a snapshot of all active containers!
    private List<MonsterGroupContainer> GetAllContainers()
    {
        var activeContainers = new List<MonsterGroupContainer>();
        for (int i = 0; i < mapManager.Columns; i++)
        {
            for (int j = 0; j < mapManager.Rows; j++)
            {
                var container = mapManager.GetMonsterContainer((i, j));
                if (container != null && !container.NoMonsters)
                {
                    activeContainers.Add(container);
                }
            }
        }
        return activeContainers;
    }

    public void SpawnMonsters(int count)
    {
        for (int i = 0; i < count; i++)
        {
            (int targetCol, int targetRow) = mapManager.GetTent();
            int side = Random.Shared.Next(4);
            int col, row;
            switch (side)
            {
                case 0:  col = 0;            row = Random.Shared.Next(mapManager.Rows);    break;
                case 1:  col = mapManager.Columns - 1;  row = Random.Shared.Next(mapManager.Rows);    break;
                case 2:  col = Random.Shared.Next(mapManager.Columns); row = 0;            break;
                default: col = Random.Shared.Next(mapManager.Columns); row = mapManager.Rows - 1;     break;
            }
            
            if(!mapManager.AddNewMonster((col,row)))
            {
                i--;
            }
            
        }
    }
    

    public void ManageMoveAndAttack(GameTime gameTime)
    {        
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
                    if (targetContainer != null && !targetContainer.NoMonsters)
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
            foreach (var monsterGroup in container._Monstergroups)
            {
                foreach (var monster in monsterGroup.Monsters)
                {
                    if (!monster.GoalReached) continue; // git commit -m"medow with sheeps health separate from sanity but tied to the sheep. meadow is destroyed if health reaches 0 and is removed by mapmanager making it not generate any sanity points"

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
                foreach (var monsterGroup in container._Monstergroups)
                {
                    foreach (var monster in monsterGroup.Monsters)
                    {
                        monster.AttackTarget = null;
                        var newPath = pathFindingManager.CalculatePath(monster);
                        if (newPath != null && newPath.Count > 0)
                            monster.Path = newPath;
                    }
                }
            }
        }
    }
}