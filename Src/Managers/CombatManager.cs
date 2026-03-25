using System;
using System.Collections.Generic;
using Gamelab;
using Microsoft.Xna.Framework;

public class CombatManager
{
    
    private float _sanityDamageAccumulator;
    /// <summary>
    /// Spawns a monster at a random edge tile that walks toward the given target tile
    /// </summary>
    public void SpawnMonsters(int count)
    {
        for (int i = 0; i < count; i++)
        {
            (int targetCol, int targetRow) = GamelabGame.MapManager.GetTent();
            int side = Random.Shared.Next(4);
            int col, row;
            switch (side)
            {
                case 0:  col = 0;            row = Random.Shared.Next(GamelabGame.MapManager.Rows);    break;
                case 1:  col = GamelabGame.MapManager.Columns - 1;  row = Random.Shared.Next(GamelabGame.MapManager.Rows);    break;
                case 2:  col = Random.Shared.Next(GamelabGame.MapManager.Columns); row = 0;            break;
                default: col = Random.Shared.Next(GamelabGame.MapManager.Columns); row = GamelabGame.MapManager.Rows - 1;     break;
            }
            
            GamelabGame.MapManager.AddMonster(col,row, targetCol, targetRow);
        }
    }

    /// <summary>
    /// Checks if enough time passed for monsters to attack/move and of so does that
    /// </summary>
    public void ManageMoveAndAttack(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Towers attack monsters in range (focus first monster found)
        for (int i = 0; i < GamelabGame.MapManager.Columns; i++)
        {
            for (int j = 0; j < GamelabGame.MapManager.Rows; j++)
            {
                if (GamelabGame.MapManager.GetStructure(i,j) is AreaTower areaTower && !areaTower.IsDestroyed)
                {
                    var tilesInRange = areaTower.GetTilesInRange();
                    foreach (var tile in tilesInRange)
                    {
                        var group = GamelabGame.MapManager.GetMonster(tile.Item1, tile.Item2);
                        if(group != null)
                        {
                            foreach (var monster in group.Monsters)
                            {
                                monster.TakeDamage(areaTower.DamagePerSecond * dt);
                            }
                        }
                    }
                }
                else if (GamelabGame.MapManager.GetStructure(i,j) is Tower tower && !tower.IsDestroyed)
                {
                    var tilesInRange = tower.GetTilesInRange();
                    foreach (var tile in tilesInRange)
                    {
                        var group = GamelabGame.MapManager.GetMonster(tile.Item1, tile.Item2);
                        if (group != null && group.Count > 0)
                        {
                            group.Monsters[0].TakeDamage(tower.DamagePerSecond * dt);
                            break;
                        }
                    }
                }
            }
        }

        // Monsters attack the structure on their goal tile when they've stopped moving
        for (int i = 0; i < GamelabGame.MapManager.Columns; i++)
        {
            for (int j = 0; j < GamelabGame.MapManager.Rows; j++)
            {
                var group = GamelabGame.MapManager.GetMonster(i, j);
                if (group == null) continue;

                foreach (var monster in group.Monsters)
                {
                    if (!monster.GoalReached) continue;

                    var goalTile = monster.AttackTarget ?? monster.TilePosition;
                    var structure = GamelabGame.MapManager.GetStructure(goalTile.Item1, goalTile.Item2);
                    if (structure == null || structure.IsDestroyed) continue;

                    if (GamelabGame.SanityManager != null && structure is Tent tent)
                    {
                        // Tent damage goes through sanity — tent HP mirrors sanity
                        float damage = monster.DamagePerSecond * dt;
                        _sanityDamageAccumulator += damage;
                        if (_sanityDamageAccumulator >= 1f)
                        {
                            int toDeduct = (int)_sanityDamageAccumulator;
                            GamelabGame.SanityManager.DeductSanity(toDeduct);
                            _sanityDamageAccumulator -= toDeduct;
                        }
                        tent.SyncHealthWithSanity(); 
                    }
                    else if(GamelabGame.SanityManager != null && structure is Meadow meadow)
                    {                           
                        // Tent damage goes through sanity — tent HP mirrors sanity
                        float damage = monster.DamagePerSecond * dt;
                        _sanityDamageAccumulator += damage;
                        if (_sanityDamageAccumulator >= 1f)
                        {
                            int toDeduct = (int)_sanityDamageAccumulator;
                            GamelabGame.SanityManager.DeductSanity(toDeduct);
                            _sanityDamageAccumulator -= toDeduct;
                        }
                        meadow.SyncHealthWithSanity(); 
                    }
                    else
                    {
                        float hpBefore = structure.CurrentHealth;
                        monster.Attack(structure, dt);

                        if (structure is Sheep && GamelabGame.SanityManager != null)
                        {
                            float damage = hpBefore - structure.CurrentHealth;
                            _sanityDamageAccumulator += damage;
                            if (_sanityDamageAccumulator >= 1f)
                            {
                                int toDeduct = (int)_sanityDamageAccumulator;
                                GamelabGame.SanityManager.DeductSanity(toDeduct);
                                _sanityDamageAccumulator -= toDeduct;
                            }
                        }
                    }
                }
            }
        }

        // Remove dead monsters and empty groups
        for (int i = 0; i < GamelabGame.MapManager.Columns; i++)
        {
            for (int j = 0; j < GamelabGame.MapManager.Rows; j++)
            {
                if (GamelabGame.MapManager.GetMonster(i,j) != null)
                {
                    GamelabGame.MapManager.GetMonster(i,j).RemoveDead();
                }
            }
        }

        // Remove dead structures
        bool anyStructureDestroyed = false;
        for (int i = 0; i < GamelabGame.MapManager.Columns; i++)
        {
            for (int j = 0; j < GamelabGame.MapManager.Rows; j++)
            {
                var structure = GamelabGame.MapManager.GetStructure(i,j);
                if (structure != null && structure.IsDestroyed)
                {
                    if (structure is Tent)
                        GamelabGame.MapManager.TentDestroyed = true;
                    anyStructureDestroyed = true;
                    GamelabGame.MapManager.RemoveStructure(i,j);
                }
            }
        }

        // When ANY structure is destroyed, recalculate paths for ALL monsters
        if (anyStructureDestroyed)
        {
            for (int i = 0; i < GamelabGame.MapManager.Columns; i++)
            {
                for (int j = 0; j < GamelabGame.MapManager.Rows; j++)
                {
                    var group = GamelabGame.MapManager.GetMonster(i, j);
                    if (group == null) continue;
                    foreach (var monster in group.Monsters)
                    {
                        monster.AttackTarget = null;
                        var newPath = GamelabGame.PathFindingManager.CalculatePath(monster);
                        if (newPath != null && newPath.Count > 0)
                            monster.Path = newPath;
                    }
                }
            }
        }
    }
}