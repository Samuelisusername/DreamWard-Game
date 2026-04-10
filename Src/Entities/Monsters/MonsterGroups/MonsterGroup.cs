using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Gamelab;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Represents a group of monsters with the same next target
/// </summary>
public abstract class MonsterGroup
{    
    // The shortcut to find your MapManager
    protected MapManager mapManager => GamelabGame.MapManager;
    protected PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;
    public List<Monster> Monsters { get; private set; } 
    protected abstract TimeSpan speed {get;}
    protected TimeSpan _elapsed = TimeSpan.Zero;
    protected float _pathProgress;
    public (int,int) TilePosition {get; set;}
    public abstract MonsterType MonsterType {get;}
    private bool shouldRecalcPath;


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
    protected bool _oldGoalReached = false;

    public void ResetGroup((int,int) pos)
    {
        shouldRecalcPath = false;
        _elapsed = TimeSpan.Zero;
        TilePosition = pos;
    }

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
            int oldCount = Count;
            Monsters.Add(monster);
            if(oldCount == 0 ||oldCount <= 2 && Count > 2)
            {
                SetAnimation();
            }
        }
    }

    public void AddMonsters(List<Monster> monsters)
    {
        if (IsEmpty || monsters[0].GetCurrentGoal() == this.CommonNextTarget()) 
        {
            int oldCount = Count;
            Monsters.AddRange(monsters);
            if(oldCount == 0 ||oldCount <= 2 && Count > 2)
            {
                SetAnimation();
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
                if(i.CanTarget(destStructure))
                {
                    i.AttackTarget = i.GetCurrentGoal(); // Target the wall to attack
                    i.Path = new List<(int,int)> { i.Path[0] };
                }
                else
                {
                    throw new Exception("monster has a path through a tile it cant step on and it cant attack, not good :(");
                }
            });
            if(_goalReached)
            {
                SetAnimation();
            }
        }

        if (!_goalReached)
        {
            if(_oldGoalReached)
            {
                SetAnimation();
            }
            _elapsed += gameTime.ElapsedGameTime;
            if (_elapsed >= speed)
            {
                _elapsed -= speed;
                Monsters.ForEach(i => i.ShouldMove = !i.GoalReached);
            }
            _pathProgress = (float)(_elapsed / speed);
        }
        _oldGoalReached = _goalReached;
        sprite.Update(gameTime);
    }
    
    // Tells the individual monsters to actually step forward
    public bool Move()
    {
        if(_goalReached || !Monsters.All(i => i.ShouldMove))
        {
            return false;
        }
        mapManager.AddExistingMonsterGroup(this, CommonNextTarget(), shouldRecalcPath);
        return true;
    } 
    
    public void QueuePathRecalc()
    {
        if(_goalReached)
        {
            Monsters.ForEach(i =>
            {
                i.AttackTarget = null;
                var newPath = pathFindingManager.CalculatePath(i);
                if (newPath != null && newPath.Count > 0)
                    i.Path = newPath;         
            });
        }
        else
        {
            shouldRecalcPath = true;
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

    private void SetAnimation()
    {        
        string type = MonsterUtils.GetMonsterTypeName(MonsterType);
        string isAmalgam = Count >2 ? "_amalgam_" : "_";
        string anim = CommonNextTarget() == TilePosition ? "idleS" : "walk";
        string direction = MapUtils.GetDirection(TilePosition, CommonNextTarget());
        //Turn into Amalgam
        sprite.Animation = GamelabGame.textureAtlas["monsters"].GetAnimation(type+ isAmalgam + anim + direction);
        if(direction.Contains("E")) sprite.SpriteEffects = SpriteEffects.FlipHorizontally;
        else sprite.SpriteEffects = SpriteEffects.None;
    }

}