using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DreamWardUtils.Graphics; 
using Gamelab;                 

public class MonsterGroupContainer 
{   
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
                    newGroup =  new RegularMonsterGroup(tilePosition);
                    break;
                case MonsterType.Flying:
                    newGroup = new FlyingMonsterGroup(tilePosition);
                    break;
                default:
                    throw new Exception("Add the new MonsterGroup Type to MonsterGroupContainer.cs in AddExistingMonster()");
            }
            newGroup.AddMonster(monster);
            Monstergroups.Add(newGroup);
        }
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
            Monstergroups[i].Move();
        }
    }

    // Add "bool isAOE" to the arguments
    public void TakeDamage(float damage, bool isAOE)
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
#endregion
}