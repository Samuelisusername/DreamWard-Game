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
    private PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;
    private (int, int) tilePosition;
    public List<MonsterGroup> MonsterGroups;

    private bool cleanUpGroups;
    
    public MonsterGroupContainer((int, int) tilePosition)
    {
        this.tilePosition = tilePosition;
        MonsterGroups = new List<MonsterGroup>();

    }

    public bool IsEmpty => MonsterGroups.Count == 0;

    public (int, int) PositionOnTile(MonsterType MonsterGrouptype) 
    {
        var sorted_grouped_monstergroups = MonsterGroups.GroupBy(m => m.MonsterType).OrderBy(bucket => bucket.Key).ToList();
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

        var matchingGroup = MonsterGroups.FirstOrDefault(g => g.MonsterType == type && g.CommonNextTarget() == target);

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
                case MonsterType.Small:
                    newGroup = new SmallMonsterGroup(tilePosition, TimeSpan.Zero);
                    break;
                case MonsterType.Big:
                    newGroup = new BigMonsterGroup(tilePosition, TimeSpan.Zero);
                    break;
                default:
                    throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
            }
            newGroup.AddMonster(monster);
            newGroup.shouldCheckMerge = true;
            MonsterGroups.Add(newGroup);
        }
    }

    public void AddExistingMonsterGroup(MonsterGroup monsterGroup, bool shouldRecalcPath, TimeSpan elapsed)
    {
        cleanUpGroups = true;
        (int col, int row) oldStart = monsterGroup.TilePosition;
        (int col, int row) oldGoal = monsterGroup.CommonNextTarget();

        string oldDir = MapUtils.GetDirection(oldStart, oldGoal);

        monsterGroup.Monsters.ForEach(i => i.Move(shouldRecalcPath));

        
        
        //if monster can attack structure on its way, do so
        if(monsterGroup.Monsters.All(i => i.GoalReached))
        {
            monsterGroup.Monsters.ForEach( i=>
            {
                if(pathFindingManager.GetTarget(i) is {} target)
                {
                    Entity destStructure = mapManager.GetStructure(target);
                    if(destStructure != null)
                    {
                        if(i.CanTarget(destStructure) && MapUtils.CalculateTileDistance(target, i.TilePosition) <= 1)
                        {
                            i.AttackTarget = pathFindingManager.GetTarget(i); // Target the wall to attack
                        } 
                    }
                }
            });
        }


        List<List<Monster>> monsterLists = monsterGroup.Monsters.GroupBy(i => i.GetCurrentGoal()).Select(i => i.ToList()).ToList();
        
        foreach (List<Monster> monsters in monsterLists)
        {
            if(monsters[0].GoalReached)
            {
                //monster is idle add all monsters to the idle group if it exists
                var matchingGroup = MonsterGroups.FirstOrDefault(g => g.MonsterType == monsters[0].Type && g.CommonNextTarget() == tilePosition);

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
                        case MonsterType.Small:
                            newGroup = new SmallMonsterGroup(tilePosition, elapsed);
                            break;
                        case MonsterType.Big:
                            newGroup = new BigMonsterGroup(tilePosition, TimeSpan.Zero);
                            break;
                        default:
                            throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
                    }
                    newGroup.AddMonsters(monsters);
                    MonsterGroups.Add(newGroup);
                    newGroup.shouldCheckMerge = true;
                }
            }
            else if(oldDir == MapUtils.GetDirection(oldGoal, monsters[0].GetCurrentGoal()))
            {
                //monsters move in the same direction keep them in the monstergroup
                monsterGroup.ResetGroup(tilePosition);
                MonsterGroups.Add(monsterGroup);
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
                    case MonsterType.Small:
                        newGroup = new SmallMonsterGroup(tilePosition, elapsed);
                        break;
                    case MonsterType.Big:
                        newGroup = new BigMonsterGroup(tilePosition, TimeSpan.Zero);
                        break;
                    default:
                        throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
                }
                newGroup.AddMonsters(monsters);
                MonsterGroups.Add(newGroup);
                newGroup.shouldCheckMerge = true;
            }
        }

        monsterGroup.Monsters.RemoveAll(i => oldDir != MapUtils.GetDirection(oldGoal, i.GetCurrentGoal()));
    }

    public void CleanUpGroups()
    {
        if(!cleanUpGroups) return;
        cleanUpGroups = false;
        List<List<MonsterGroup>> monsterGroupLists = MonsterGroups.GroupBy(i => (i.CommonNextTarget(),i.MonsterType)).Select(i => i.ToList()).ToList();

        foreach (var mgs in monsterGroupLists)
        {
            MonsterGroup mainGroup = mgs.FirstOrDefault(i => !i.IsNew) ?? mgs[0];
            foreach (var mg in mgs)
            {
                if(mg == mainGroup) continue;
                mainGroup.AddMonsters(mg.Monsters);
                mg.Monsters.Clear();
            }
        }
        for (int i = MonsterGroups.Count - 1; i >= 0; i--)
        {
            if (MonsterGroups[i].IsEmpty)
            {
                MonsterGroups.RemoveAt(i);
            }
            else
            {
                MonsterGroups[i].IsNew = false;
            }
        }
    }

   public void Update(GameTime gameTime)
    {
        for (int i = MonsterGroups.Count - 1; i >= 0; i--)
        {
            var group = MonsterGroups[i];
            group.Update(gameTime);
        }
    }

    public void Move()
    {
        for (int i = MonsterGroups.Count - 1; i >= 0; i--)
        {
            if (MonsterGroups[i].Move())
            {
                MonsterGroups.Remove(MonsterGroups[i]);
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
            foreach (var group in MonsterGroups)
            {
                // Note: You will need to make sure MonsterGroupNew_Unfinished 
                // has a method called 'take_damage_aoe' that damages all monsters inside it!
                group.TakeDamageAOE(damage); 
            }
        }
        else if(isSpike)
        {
            //spike all non flyiers take damage
            foreach (var group in MonsterGroups)
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
            MonsterGroups[0].TakeDamageSingleTarget(damage);
        }
    }

    public void RemoveDead()
    {
        foreach(var group in MonsterGroups)
        {
            group.RemoveDead();
        }
        
        MonsterGroups.RemoveAll(g => g.IsEmpty);
        
    }

    public Monster GetFirstMonster()
    {
        foreach (var group in MonsterGroups)
        {
            foreach (var monster in group.Monsters)
            {
                if(!monster.IsDestroyed)
                {
                    return monster;
                }
            }
        }
        return null;
    }
    public MonsterGroup GetMonsterGroup(Monster monster)
    {
        foreach (var group in MonsterGroups)
        {
            foreach (var m in group.Monsters)
            {
                if(m == monster)
                {
                    return group;
                }
            }
        }
        return null;
    }

#region Visuals and Drawing
    public void SetScale(Vector2 scale)
    {
        MonsterGroups.ForEach(i => i.SetScale(scale));
    }

    public void DrawShadow(SpriteBatch spriteBatch)
    {
        MonsterGroups.ForEach(i =>
        {
            if(i.MonsterType == MonsterType.Flying)
            {
                (i as FlyingMonsterGroup).DrawShadow(spriteBatch);
            }
        });
    }

    public void DrawGround(SpriteBatch spriteBatch)
    {
        MonsterGroups.ForEach(i =>
        {
            if(i.isGrounded)
            {
                i.Draw(spriteBatch);
            }
        });
    }
    
    public void DrawAir(SpriteBatch spriteBatch)
    {
        MonsterGroups.ForEach(i =>
        {
            if(!i.isGrounded)
            {
                i.Draw(spriteBatch);
            }
        });
    }

    public void InitMerge()
    {
        List<(int,int)> neighbor = MapUtils.GetSurroundingTiles(tilePosition);
        neighbor.Add(tilePosition);
        Dictionary<MonsterType, int> counts = new Dictionary<MonsterType, int>();
        foreach ((int col, int row) pos in neighbor)
        {
            foreach (MonsterGroup group in mapManager.GetMonsterContainer(pos).MonsterGroups)
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
            foreach (MonsterGroup group in mapManager.GetMonsterContainer(pos).MonsterGroups)
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