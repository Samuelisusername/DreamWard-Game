using System;
using Gamelab;
using Microsoft.Xna.Framework;

public class CombatManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;

    private float _sanityDamageAccumulator;

    /// <summary>
    /// Spawns a monster at a random edge tile that walks toward the given target tile
    /// </summary>
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
                foreach (var tile in tilesInRange)
                {
                    mapManager.GetMonsterGroup(tile)?.TakeDamage(tower.DamagePerSecond * dt);
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
                }
                else
                {
                    monster.Attack(structure, dt);
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