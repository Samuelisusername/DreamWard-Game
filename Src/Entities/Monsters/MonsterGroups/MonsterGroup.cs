using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Gamelab;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework.Graphics; // This fixes the 'GamelabGame' error!

/// <summary>
/// Represents a group of monsters with the same next target
/// </summary>
public abstract class MonsterGroup
{    
    // The shortcut to find your MapManager
    protected MapManager mapManager => GamelabGame.MapManager;
    public List<Monster> Monsters { get; private set; } 
    protected abstract TimeSpan speed {get;}
    protected TimeSpan _elapsed = TimeSpan.Zero;
    protected float _pathProgress;
    public (int,int) TilePosition {get; set;}
    public abstract MonsterType MonsterType {get;}

    public MonsterGroup((int, int) tilePos)
    {
        Monsters = new List<Monster>();
        TilePosition = tilePos;
        string type = MonsterUtils.GetMonsterTypeName(MonsterType);
        sprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite(type+"_idleS");
        sprite.Scale = mapManager.Scale;
    }  

    // Satisfies the Entity requirement. Container handles the actual visual offset!
    protected Vector2 _localOffset => new Vector2(-32, -90);
    public int Count => Monsters.Count;
    public bool IsEmpty => this.Count == 0; 
    public float Health => Monsters.Sum(m => m.CurrentHealth);
    public float Damage => Monsters.Sum(m => m.DamagePerSecond); 

    protected bool _goalReached => Monsters.All(i => i.GoalReached);

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


    public void AddMonster(Monster monster)
    {
        if (IsEmpty || monster.GetCurrentGoal() == this.CommonNextTarget()) 
        {
            Monsters.Add(monster);
            if(Count == 1)
            {
                string type = MonsterUtils.GetMonsterTypeName(MonsterType);
                string anim = CommonNextTarget() == TilePosition ? "idleS" : "walk";
                string direction = MapUtils.GetDirection(TilePosition, CommonNextTarget());
                //Turn into Amalgam
                sprite.Animation = GamelabGame.textureAtlas["monsters"].GetAnimation(type+"_" + anim + direction);
                if(direction.Contains("E")) sprite.SpriteEffects = SpriteEffects.FlipHorizontally;
                else sprite.SpriteEffects = SpriteEffects.None;
            }
            if(Count == 3)
            {
                string type = MonsterUtils.GetMonsterTypeName(MonsterType);
                string anim = CommonNextTarget() == TilePosition ? "idleS" : "walk";
                string direction = MapUtils.GetDirection(TilePosition, CommonNextTarget());
                //Turn into Amalgam
                sprite.Animation = GamelabGame.textureAtlas["monsters"].GetAnimation(type+"_amalgam_" + anim + direction);
                if(direction.Contains("E")) sprite.SpriteEffects = SpriteEffects.FlipHorizontally;
                else sprite.SpriteEffects = SpriteEffects.None;
            }
        }
    }

    public void Update(GameTime gameTime)
    {
        Entity destStructure = mapManager.GetStructure(CommonNextTarget());
        if (destStructure != null && !Monsters[0].CanStepOn(destStructure))
        {
            Monsters.ForEach( i=>
            {
                if(i.CanStepOn(destStructure))
                {
                    i.AttackTarget = i.GetCurrentGoal(); // Target the wall to attack
                    i.Path = new List<(int,int)> { i.Path[0] };
                }
            });
        }

        if (!_goalReached)
        {
            _elapsed += gameTime.ElapsedGameTime;
            if (_elapsed >= speed)
            {
                _elapsed -= speed;
                Monsters.ForEach(i => i.ShouldMove = !i.GoalReached);
            }
            _pathProgress = (float)(_elapsed / speed);
        }
        sprite.Update(gameTime);
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
                monster.Move();        // Automatically hands it to the next tile's Container
                monster.ShouldMove = false;
                i--; // Adjust loop index because we removed an item
            }
        }
    } 

    protected AnimatedSprite sprite;

    public void Draw(SpriteBatch spriteBatch, (int totalGroups, int rank) posOnTile)
    {
        if (IsEmpty) return;

        Vector2 worldPos = GetInterpolatedWorldPosition();
        
        // Use the container's rank logic to offset them so they don't overlap perfectly
        Vector2 rankOffset = new Vector2(posOnTile.rank * 10 * mapManager.Scale.X, 0); 
        
        sprite.Draw(spriteBatch, worldPos + (_localOffset * mapManager.Scale) + rankOffset);
        
    }

    public void SetScale(Vector2 scale)
    {
        sprite.Scale = scale;
    }

}