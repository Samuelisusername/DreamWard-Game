using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;        

public class MonsterGroupContainer 
{   
    private MapManager mapManager => GamelabGame.MapManager;
    private (int, int) tilePosition;
    public List<MonsterGroup> Monstergroups;
    
    public MonsterGroupContainer((int, int) tilePosition)
    {
        this.tilePosition = tilePosition;
        Monstergroups = new List<MonsterGroup>();

    }

    public bool IsEmpty => Monstergroups.Count == 0;

    public (int, int) PositionOnTile(MonsterType MonsterGrouptype) 
    {
        var sorted_grouped_monstergroups = Monstergroups.GroupBy(m => m.MonsterType).OrderBy(bucket => bucket.Key).ToList();
        int totalVisualGroups = sorted_grouped_monstergroups.Count;
        
        int curr_rank = 0; 
        foreach (var group in sorted_grouped_monstergroups)
        {
            if(group.Key == MonsterGrouptype) return (totalVisualGroups, curr_rank);
            curr_rank++;
        }
        return (totalVisualGroups, -1);
    }

    public void AddExistingMonster(Monster monster)
    {
        var target = monster.GetCurrentGoal();
        var type = monster.Type;

        var matchingGroup = Monstergroups.FirstOrDefault(g => g.MonsterType == type && g.CommonNextTarget() == target);

        if (matchingGroup != null)
        {
            matchingGroup.AddMonster(monster);
        }

        else
        {
            MonsterGroup newGroup;
            switch (monster.Type)
            {
                case MonsterType.Regular:
                    newGroup =  new RegularMonsterGroup(tilePosition, TimeSpan.Zero);
                    break;
                case MonsterType.Flying:
                    newGroup = new FlyingMonsterGroup(tilePosition, TimeSpan.Zero);
                    break;
                default:
                    throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
            }
            newGroup.AddMonster(monster);
            Monstergroups.Add(newGroup);
        }
    }

    public void AddExistingMonsterGroup(MonsterGroup monsterGroup, bool shouldRecalcPath, TimeSpan elapsed)
    {
        (int col, int row) oldStart = monsterGroup.TilePosition;
        (int col, int row) oldGoal = monsterGroup.CommonNextTarget();

        string oldDir = MapUtils.GetDirection(oldStart, oldGoal);

        monsterGroup.Monsters.ForEach(i => i.Move(shouldRecalcPath));

        List<List<Monster>> monsterLists = monsterGroup.Monsters.GroupBy(i => i.GetCurrentGoal()).Select(i => i.ToList()).ToList();
        
        foreach (List<Monster> monsters in monsterLists)
        {
            if(monsters[0].GoalReached)
            {
                //monster is idle add all monsters to the idle group if it exists
                var matchingGroup = Monstergroups.FirstOrDefault(g => g.MonsterType == monsters[0].Type && g.CommonNextTarget() == tilePosition);

                if (matchingGroup != null)
                {
                    matchingGroup.AddMonsters(monsters);
                }
                else
                {
                    MonsterGroup newGroup;
                    switch (monsters[0].Type)
                    {
                        case MonsterType.Regular:
                            newGroup =  new RegularMonsterGroup(tilePosition, elapsed);
                            break;
                        case MonsterType.Flying:
                            newGroup = new FlyingMonsterGroup(tilePosition, elapsed);
                            break;
                        default:
                            throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
                    }
                    newGroup.AddMonsters(monsters);
                    Monstergroups.Add(newGroup);
                    newGroup.shouldCheckMerge = true;
                }
            }
            else if(oldDir == MapUtils.GetDirection(oldGoal, monsters[0].GetCurrentGoal()))
            {
                //monsters move in the same direction keep them in the monstergroup
                monsterGroup.ResetGroup(tilePosition);
                Monstergroups.Add(monsterGroup);
                monsterGroup.shouldCheckMerge = true;
            }
            else
            {
                // monsters change direction
                MonsterGroup newGroup;
                switch (monsters[0].Type)
                {
                    case MonsterType.Regular:
                        newGroup =  new RegularMonsterGroup(tilePosition, elapsed);
                        break;
                    case MonsterType.Flying:
                        newGroup = new FlyingMonsterGroup(tilePosition, elapsed);
                        break;
                    default:
                        throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
                }
                newGroup.AddMonsters(monsters);
                Monstergroups.Add(newGroup);
                newGroup.shouldCheckMerge = true;
            }
        }

        monsterGroup.Monsters.RemoveAll(i => oldDir != MapUtils.GetDirection(oldGoal, i.GetCurrentGoal()));
    }

   public void Update(GameTime gameTime)
    {
        for (int i = Monstergroups.Count - 1; i >= 0; i--)
        {
            var group = Monstergroups[i];
            group.Update(gameTime);

            if (group.IsEmpty)
            {
                Monstergroups.RemoveAt(i);
            }
        }
    }

    public void Move()
    {
        for (int i = Monstergroups.Count - 1; i >= 0; i--)
        {
            if (Monstergroups[i].Move())
            {
                Monstergroups.Remove(Monstergroups[i]);
                i--;
            }
        }
    }

    // Add "bool isAOE" to the arguments
    public void TakeDamage(float damage, bool isAOE, bool isSpike)
    {
        if (IsEmpty) return;

        if (isAOE)
        {
            // AOE: Loop through every single group on this tile
            foreach (var group in Monstergroups)
            {
                // Note: You will need to make sure MonsterGroupNew_Unfinished 
                // has a method called 'take_damage_aoe' that damages all monsters inside it!
                group.TakeDamageAOE(damage); 
            }
        }
        else if(isSpike)
        {
            //spike all non flyiers take damage
            foreach (var group in Monstergroups)
            {
                // Note: You will need to make sure MonsterGroupNew_Unfinished 
                // has a method called 'take_damage_aoe' that damages all monsters inside it!
                if(group.MonsterType != MonsterType.Flying)
                {
                    group.TakeDamageAOE(damage);   
                }
            }
        }
        else 
        {
            // Single Target: Just hit the first group in the container
            // TODO: single should be more consisten than this
            Monstergroups[0].TakeDamageSingleTarget(damage);
        }
    }

    public void RemoveDead()
    {
        foreach(var group in Monstergroups)
        {
            group.RemoveDead();
        }
        Monstergroups.RemoveAll(g => g.IsEmpty);
    }

#region Visuals and Drawing
    public void SetScale(Vector2 scale)
    {
        Monstergroups.ForEach(i => i.SetScale(scale));
    }
    public void DrawGround(SpriteBatch spriteBatch)
    {
        Monstergroups.ForEach(i =>
        {
            if(i.MonsterType != MonsterType.Flying)
            {
                i.Draw(spriteBatch, PositionOnTile(i.MonsterType));
            }
        });
    }
    
    public void DrawAir(SpriteBatch spriteBatch)
    {
        Monstergroups.ForEach(i =>
        {
            if(i.MonsterType == MonsterType.Flying)
            {
                i.Draw(spriteBatch, PositionOnTile(i.MonsterType));
            }
        });
    }

    public void InitMerge()
    {
        List<(int,int)> neighbor = MapUtils.getSurroundingTiles(tilePosition);
        neighbor.Add(tilePosition);
        Dictionary<MonsterType, int> counts = new Dictionary<MonsterType, int>();
        foreach ((int col, int row) pos in neighbor)
        {
            foreach (MonsterGroup group in mapManager.GetMonsterContainer(pos).Monstergroups)
            {
                if(group.shouldCheckMerge && group.CommonNextTarget() == tilePosition )
                {
                    if (!counts.TryGetValue(group.MonsterType, out _))
                    {
                        counts.Add(group.MonsterType, 0); 
                    }
                    counts[group.MonsterType] += 1;
                }
            }
        }
        foreach ((int col, int row) pos in neighbor)
        {
            foreach (MonsterGroup group in mapManager.GetMonsterContainer(pos).Monstergroups)
            {
                if(counts.TryGetValue(group.MonsterType, out int value) && value > 1 && group.CommonNextTarget() == tilePosition)
                {
                    group.StartMerge();
                }
            }
        }
    }

#endregion
}