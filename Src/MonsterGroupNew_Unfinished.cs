using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;


public class MonsterGroupNew_Unfinished : Entity
{
    //all monsters of the same type
    //
    public MonsterGroupNew_Unfinished((int, int)tilePos, int maxHealth):base(tilePos, maxHealth)
    {
        Monsters = [];
    }  
    //the offset to draw the monster on the tile
    private readonly Vector2 _localOffset = new Vector2(-32,-90); //hmmmmmmmmm is this necessary now that we we have this offset thing in the monstergroup container?
    
    public List<Monster> Monsters{get; private set;} //private set im guessing we add monsters here and remove them here the add and remove methods are called from the Containers..
    private TimeSpan _speed;
    private TimeSpan _elapsed = TimeSpan.Zero;

    /// <summary>
    /// Posistion of the entity on the map
    /// </summary>
    public (int,int) TilePosition {get; protected set;}

    //where inbetween two tiles is the monster?
    private float _pathProgress;

    public MonsterType monsterType;
    public int Count => Monsters.Count;
    public bool isSingle => this.Count ==1;
    public bool isEmpty => this.Count ==0; 
    public float health => Monsters.Sum(m => m.CurrentHealth);
    public float damage => Monsters.Sum(m => m.DamagePerSecond); 
    private bool _goalReached => Monsters.Select(i => i.GoalReached).All(j => j);
    private bool _goalNotReached => Monsters.Select(i=>i.GoalReached).All(j => !j);
    public bool All_monsters_have_goal_reached_or_All_not_goal_reached  => _goalNotReached || _goalReached;
    public Vector2 GetInterpolatedWorldPosition() // we will have to adapt this one... based on the amount of groups on one tile
    {
        Vector2 origin = mapManager.GetWorldPositionOfTile(TilePosition);
        if (Monsters.Count == 0) return origin;
        var goal = Monsters[0].GetCurrentGoal();
        Vector2 goalPos = mapManager.GetWorldPositionOfTile(goal);
        return origin + (goalPos - origin) * _pathProgress;
    }
        public void RemoveDead() 
    {
        Monsters.RemoveAll(m => m.IsDestroyed);
    }
    
    public (int, int) common_Next_Target()
    {
        if (isEmpty) return this.TilePosition; 
        Monster firstMonster = Monsters[0];
        return firstMonster.GetCurrentGoal();
    }
    public void take_damage_aoe(float damage) // pick the first monster and give it damage
    {
        if (isEmpty) return;
        foreach(var monster in Monsters)
        {
            monster.TakeDamage(damage);
        }

        
    }
    public void take_damage_single_target(float damage)
    {
        if(isEmpty) return;
        Monsters[0].TakeDamage(damage);
    }
    public void make_damage(Entity target, float deltaTime)
    {
        if (isEmpty || target == null || target.IsDestroyed) return;
        float totalDamage = this.damage * deltaTime;
        target.TakeDamage(totalDamage);
    }
    public void move(){} //on what level does move happen?
    public void addMonster(){} //if a monster is added to the pack (only valid if has the same direction or target)
    public void removeMonster(){} //if a monster dies or changes target
    public override void Update(GameTime gameTime)
    {
    // You can put monster movement or logic here later.
    // For now, leaving it empty is perfectly fine to fix the error!
    }  
    public override bool IsBuildEntity => false;
    // Depending on how it's defined in Entity.cs, this might need to be 'public' or 'protected'


    



}