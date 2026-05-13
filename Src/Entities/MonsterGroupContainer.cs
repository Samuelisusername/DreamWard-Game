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
    public List<MonsterGroup> MonsterGroups;
    
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
                    newGroup =  new RegularMonsterGroup(tilePosition);
                    break;
                case MonsterType.Flying:
                    newGroup = new FlyingMonsterGroup(tilePosition);
                    break;
                case MonsterType.Small:
                    newGroup = new SmallMonsterGroup(tilePosition);
                    break;
                case MonsterType.Big:
                    newGroup = new BigMonsterGroup(tilePosition);
                    break;
                default:
                    throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
            }
            newGroup.AddMonster(monster);
            MonsterGroups.Add(newGroup);
        }
    }

    public void AddExistingMonsterGroup(MonsterGroup monsterGroup)
    {
        (int col, int row) oldStart = monsterGroup.TilePosition;
        (int col, int row) oldGoal = monsterGroup.CommonNextTarget();

        string oldDir = MapUtils.GetDirection(oldStart, oldGoal);

        monsterGroup.Monsters.ForEach(i => i.Move());


        List<List<Monster>> monsterLists = monsterGroup.Monsters.GroupBy(i => i.GetCurrentGoal()).Select(i => i.ToList()).ToList();
        
        foreach (List<Monster> monsters in monsterLists)
        {
            if(monsters[0].TargetReached)
            {
                //monster is idle add all monsters to the idle group if it exists
                var matchingGroup = MonsterGroups.FirstOrDefault(g => g.MonsterType == monsters[0].Type && g.CommonNextTarget() == tilePosition);

                if (matchingGroup != null)
                {
                    matchingGroup.SetLastDirection(oldDir);
                    matchingGroup.AddMonsters(monsters);
                }
                else
                {
                    MonsterGroup newGroup;
                    switch (monsters[0].Type)
                    {
                        case MonsterType.Regular:
                            newGroup =  new RegularMonsterGroup(tilePosition);
                            break;
                        case MonsterType.Flying:
                            newGroup = new FlyingMonsterGroup(tilePosition);
                            break;
                        case MonsterType.Small:
                            newGroup = new SmallMonsterGroup(tilePosition);
                            break;
                        case MonsterType.Big:
                            newGroup = new BigMonsterGroup(tilePosition);
                            break;
                        default:
                            throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
                    }
                    newGroup.SetLastDirection(oldDir);
                    newGroup.AddMonsters(monsters);
                    MonsterGroups.Add(newGroup);
                }
            }
            else if(oldDir == MapUtils.GetDirection(oldGoal, monsters[0].GetCurrentGoal()))
            {
                //monsters move in the same direction keep them in the monstergroup
                monsterGroup.ResetGroup(tilePosition);
                MonsterGroups.Add(monsterGroup);
            }
            else
            {
                // monsters change direction
                MonsterGroup newGroup;
                switch (monsters[0].Type)
                {
                    case MonsterType.Regular:
                        newGroup =  new RegularMonsterGroup(tilePosition);
                        break;
                    case MonsterType.Flying:
                        newGroup = new FlyingMonsterGroup(tilePosition);
                        break;
                    case MonsterType.Small:
                        newGroup = new SmallMonsterGroup(tilePosition);
                        break;
                    case MonsterType.Big:
                        newGroup = new BigMonsterGroup(tilePosition);
                        break;
                    default:
                        throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
                }
                newGroup.AddMonsters(monsters);
                MonsterGroups.Add(newGroup);
            }
        }

        monsterGroup.Monsters.RemoveAll(i => oldDir != MapUtils.GetDirection(oldGoal, i.GetCurrentGoal()));
    }

    public void CleanUpGroups(MonsterType type)
    {
        List<List<MonsterGroup>> monsterGroupLists = MonsterGroups.Where(i => i.MonsterType == type).GroupBy(i => i.CommonNextTarget()).Select(i => i.ToList()).ToList();

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

    //TODO make this dependent on monstertype
    public void InitMerge(MonsterType type)
    {
        List<(int,int)> neighbor = MapUtils.GetSurroundingTiles(tilePosition);
        neighbor.Add(tilePosition);
        int count = 0;
        foreach ((int col, int row) pos in neighbor)
        {
            foreach (MonsterGroup group in mapManager.GetMonsterContainer(pos).MonsterGroups.Where(i => i.MonsterType == type))
            {
                if(group.CommonNextTarget() == tilePosition)
                {
                    count++;
                }
            }
        }
        if(count > 1)
        {
            foreach ((int col, int row) pos in neighbor)
            {
                foreach (MonsterGroup group in mapManager.GetMonsterContainer(pos).MonsterGroups.Where(i => i.MonsterType == type))
                {
                    if(group.CommonNextTarget() == tilePosition)
                    {
                        group.StartMerge();
                    }
                }
            }
        }
    }

#endregion
}