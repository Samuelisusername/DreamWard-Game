using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DreamWardUtils.Graphics; 
using Gamelab;                 

public class MonsterGroupContainer 
{   
    public (int, int) TilePosition { get; private set; }
    public List<MonsterGroup> Monstergroups; 
    
    public MonsterGroupContainer((int, int) tilePosition)
    {
        TilePosition = tilePosition;
        Monstergroups = new List<MonsterGroup>();

    }

    public bool NoMonsters => this.Numberofgroups == 0;
    public int Numberofgroups => this.Monstergroups.Count;

    public int GetNumberOfMonstersByType(MonsterType monstertype)
    {   
        return Monstergroups.Where(g => g.MonsterType == monstertype).Sum(g => g.Count);
    }

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
                    newGroup =  new RegularMonsterGroup(TilePosition);
                    break;
                case MonsterType.Flying:
                    newGroup = new FlyingMonsterGroup(TilePosition);
                    break;
                default:
                 throw new Exception();
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
        if (NoMonsters) return;

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
    public void Draw(SpriteBatch spriteBatch)
    {
        Monstergroups.ForEach(i => i.Draw(spriteBatch, PositionOnTile(i.MonsterType)));
    }
    
#endregion
}