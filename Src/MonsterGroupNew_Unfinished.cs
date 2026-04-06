using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Gamelab; // This fixes the 'GamelabGame' error!

public class MonsterGroupNew_Unfinished : Entity
{
    public List<Monster> Monsters { get; private set; } 
    private TimeSpan _speed = TimeSpan.FromSeconds(2); // Example speed
    private TimeSpan _elapsed = TimeSpan.Zero;
    private float _pathProgress;

    public MonsterType MonsterType;
    
    // The shortcut to find your MapManager
    private MapManager mapManager => GamelabGame.MapManager;

    public MonsterGroupNew_Unfinished((int, int) tilePos, int maxHealth) : base(tilePos, maxHealth)
    {
        Monsters = new List<Monster>();
        TilePosition = tilePos;
    }  

    // Satisfies the Entity requirement. Container handles the actual visual offset!
    protected override Vector2 _localOffset => Vector2.Zero; 
    public override bool IsBuildEntity => false;
    public int Count => Monsters.Count;
    public bool IsSingle => this.Count == 1;
    public bool IsEmpty => this.Count == 0; 
    public float Health => Monsters.Sum(m => m.CurrentHealth);
    public float Damage => Monsters.Sum(m => m.DamagePerSecond); 

    private bool _goalReached => Monsters.All(i => i.GoalReached);
    private bool _goalNotReached => Monsters.All(i => !i.GoalReached);
    public bool All_monsters_have_goal_reached_or_All_not_goal_reached => _goalNotReached || _goalReached;

    public Vector2 GetInterpolatedWorldPosition() 
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
    
    public (int, int) CommonNextTarget()
    {
        if (IsEmpty) return this.TilePosition; 
        return Monsters[0].GetCurrentGoal();
    }

    public void TakeDamageAOE(float damage) 
    {
        if (IsEmpty) return;
        foreach(var monster in Monsters)
        {
            monster.TakeDamage(damage);
        }
        RemoveDead();
    }

    public void TakeDamageSingleTarget(float damage)
    {
        if (IsEmpty) return;
        Monsters[0].TakeDamage(damage);
        RemoveDead();
    }

    public void DealDamage(Entity target, float deltaTime)
    {
        if (IsEmpty || target == null || target.IsDestroyed) return;
        float totalDamage = this.Damage * deltaTime;
        target.TakeDamage(totalDamage);
    }

    // Tells the individual monsters to actually step forward
    public void Move()
    {
        for (int i = 0; i < Monsters.Count; i++)
        {
            if (!Monsters[i].GoalReached && Monsters[i].ShouldMove)
            {
                Monster monster = Monsters[i];
                Monsters.Remove(monster); // Take it out of THIS group
                monster.Move();           // Automatically hands it to the next tile's Container
                monster.ShouldMove = false;
                i--; // Adjust loop index because we removed an item
            }
        }
    } 

    public void AddMonster(Monster monster)
    {
        if (IsEmpty || monster.GetCurrentGoal() == this.CommonNextTarget()) 
        {
            Monsters.Add(monster);
        }
    }

    public void RemoveMonster(Monster monster)
    {
        Monsters.Remove(monster);
    } 

    public override void Update(GameTime gameTime)
    {
        if (!_goalReached)
        {
            _elapsed += gameTime.ElapsedGameTime;
            if (_elapsed >= _speed)
            {
                _elapsed -= _speed;
                Monsters.ForEach(i => i.ShouldMove = !i.GoalReached);
            }
            _pathProgress = (float)(_elapsed / _speed);
        }
    }  
}