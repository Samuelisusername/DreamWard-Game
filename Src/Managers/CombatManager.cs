using System;
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

        // Monsters attack structures on the same tile
        for (int i = 0; i < GamelabGame.MapManager.Columns; i++)
        {
            for (int j = 0; j < GamelabGame.MapManager.Rows; j++)
            {
                var group = GamelabGame.MapManager.GetMonster(i, j);
                var structure = GamelabGame.MapManager.GetStructure(i, j);
                if (group != null && structure != null && !structure.IsDestroyed)
                {
                    float hpBefore = structure.CurrentHealth;
                    foreach (var monster in group.Monsters)
                    {
                        monster.Attack(structure, dt);
                    }

                    if (structure is Tent or Sheep && GamelabGame.SanityManager != null)
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
        for (int i = 0; i < GamelabGame.MapManager.Columns; i++)
        {
            for (int j = 0; j < GamelabGame.MapManager.Rows; j++)
            {
                if (GamelabGame.MapManager.GetStructure(i,j) != null && GamelabGame.MapManager.GetStructure(i,j).IsDestroyed)
                {
                    if (GamelabGame.MapManager.GetStructure(i,j) is Tent)
                        GamelabGame.MapManager.TentDestroyed = true;
                    GamelabGame.MapManager.RemoveStructure(i,j);
                }
            }
        }
    }
}