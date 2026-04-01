using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;


public class MonsterGroup : Entity
{
    //all monsters of the same type
    //
    public MonsterGroup()
    {
        _monsters = [];
        throw new NotImplementedException();
    }
    
    public List<Monster> _monsters;
    public MonsterType monsterType;
    public int size => _monsters.Count;
    public bool isSingle => this.size ==1;
    public bool isEmpty => this.size ==0; 
    public float health => _monsters.Sum(m => m.CurrentHealth);
    public float damage => _monsters.Sum(m => m.DamagePerSecond); 
    public string direction() // or target or next target (main point is that they have a common destination)
    {
        throw new NotImplementedException();
    }
    public void take_damage(){}
    public void make_damage(){}
    public void move(){} //they all move in unison
    public void addMonster(){} //if a monster is added to the pack (only valid if has the same direction or target)
    public void removeMonster(){} //if a monster dies or changes target


    



}