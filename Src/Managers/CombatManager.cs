using System;
using Gamelab;
using Microsoft.Xna.Framework;
using Gamelab.Managers;
using System.Collections.Generic;

public class CombatManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    private Dictionary<MonsterGroup, float> _monsterGruntTimers = new Dictionary<MonsterGroup, float>();
    
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;

    private float _sanityDamageAccumulator;

    private Dictionary<Tower, float> _towerSoundTimers = new Dictionary<Tower, float>();

    /// <summary>
    /// Spawns a monster at a random edge tile that walks toward the given target tile
    /// </summary>
    public void SpawnMonsters(int count)
    {
        for (int i = 0; i < 1000; i++)
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
            
            mapManager.AddNewMonster((col,row));
        }
    }

    /// <summary>
    /// Checks if enough time passed for monsters to attack/move and of so does that
    /// </summary>
    public void ManageMoveAndAttack(GameTime gameTime)
    {        
        //move the monsters if needed we do this in two loops to avoid updating monsters twice
        foreach (MonsterGroup monsterGroup in mapManager.MonsterGroups)
        {
            monsterGroup?.Move();
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Towers attack monsters in range (focus first monster found)
        foreach (Entity entity in mapManager.Structures)
        {
            if (entity is Tower tower && !tower.IsDestroyed)
            {
                var tilesInRange = tower.GetTilesInRange();
                bool dealtDamageThisFrame = false;
                foreach (var tile in tilesInRange)
                {
                   // mapManager.GetMonsterGroup(tile)?.TakeDamage(tower.DamagePerSecond * dt);
                    //possibly a grunt sound here or a laser sound or sth like that
                    var targetGroup = mapManager.GetMonsterGroup(tile);
                    if (targetGroup != null)
                    {
                        targetGroup.TakeDamage(tower.DamagePerSecond * dt);
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

                    if (dealtDamageThisFrame)
                    {
                        if (!_towerSoundTimers.ContainsKey(tower))
                            _towerSoundTimers[tower] = 0f;

                        _towerSoundTimers[tower] -= dt;

                        if (_towerSoundTimers[tower] <= 0f)
                        {
                            // Check the specific type of the tower to pick the sound and cooldown
                            string soundToPlay;
                            float cooldown;

                            if (tower is AreaTower)
                            {
                                // The AOE tower sound
                                soundToPlay = "AOE-tower_stomp"; 
                                
                                // You might want a slower cooldown for the heavy stomp 
                                // Adjust this number if it plays too fast.
                                cooldown = 1.5f; 
                            }
                            else 
                            {
                                // The standard Sniper tower sound
                                soundToPlay = "shoot_01"; 
                                cooldown = 1.0f;
                            }

                            AudioManager.Play(soundToPlay);
                            _towerSoundTimers[tower] = cooldown; 
                        }
                    }
                        
                        // Set the cooldown (1.0f = plays sound once per second)
                        //change this to 0.5f for faster shooting sounds!
                        _towerSoundTimers[tower] = 1.0f; 
                    }
                }
            }
        }

        // Monsters attack the structure on their goal tile when they've stopped moving
        foreach (MonsterGroup monsterGroup in mapManager.MonsterGroups)
        {
            if (monsterGroup == null) continue;

            foreach (var monster in monsterGroup.Monsters)
            {
                if (!monster.GoalReached) continue;

                var goalTile = monster.AttackTarget ?? monster.TilePosition;
                var structure = mapManager.GetStructure(goalTile);
                if (structure == null || structure.IsDestroyed) continue;

                else if(sanityManager != null && ((structure is Meadow meadow && meadow.HasSheep && !meadow.IsPolymorphed) || structure is Tent))
                {                           
                    // Meadow/Tent damage goes through sanity
                    float damage = monster.DamagePerSecond * dt;
                    _sanityDamageAccumulator += damage;
                    //sound to damage sanity
                }
                else
                {
                    monster.Attack(structure, dt);
                    //sound to damage towers / monsters 

                        // --- THE NEW GRUNT COOLDOWN LOGIC ---
                        
                        // 1. If this group isn't in our dictionary yet, add it and set the timer to 0
                        if (!_monsterGruntTimers.ContainsKey(monsterGroup))
                        {
                            _monsterGruntTimers[monsterGroup] = 0f;
                        }

                        // 2. Subtract the frame time (dt) from their personal timer
                        _monsterGruntTimers[monsterGroup] -= dt;

                        // 3. If the timer drops to zero (or below), let them grunt!
                        if (_monsterGruntTimers[monsterGroup] <= 0f)
                        {
                            AudioManager.Play("grunt_03"); // Play your sound
                            
                            // 4. Reset the clock! Set to 1.0f for 1 second, or 2.0f for 2 seconds.
                            _monsterGruntTimers[monsterGroup] = 1.0f; 
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

        // Remove dead monsters and empty groups
        foreach (MonsterGroup monsterGroup in mapManager.MonsterGroups)
        {
            if(monsterGroup != null)
            {
                monsterGroup.RemoveDead();
            }
        }

        // Remove dead structures
        bool anyStructureDestroyed = false;
        foreach (Entity entity in mapManager.Structures)
        {
            if (entity != null && entity.IsDestroyed)
            {
                if (entity is Tent)
                {
                    mapManager.TentDestroyed = true;
                }
                if (entity is Tower deadTower && _towerSoundTimers.ContainsKey(deadTower))
                {
                    _towerSoundTimers.Remove(deadTower);
                }
                anyStructureDestroyed = true;
                mapManager.RemoveStructure(entity.TilePosition);
            }
        }

        // When ANY structure is destroyed, recalculate paths for ALL monsters
        if (anyStructureDestroyed)
        {
            foreach (MonsterGroup monsterGroup in mapManager.MonsterGroups)
            {
                if (monsterGroup == null) continue;
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