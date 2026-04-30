using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Gamelab;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework.Graphics;
using Gamelab.Managers;
using System.Runtime.InteropServices;

/// <summary>
/// Represents a group of monsters with the same next target
/// </summary>
public abstract class MonsterGroup
{    
    // The shortcut to find your MapManager
    protected MapManager mapManager => GamelabGame.MapManager;
    protected PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;
    private SanityManager sanityManager => GamelabGame.SanityManager;

    public List<Monster> Monsters { get; private set; } 
    protected virtual TimeSpan speed => GamelabGame.Instance.Balance.MonsterSpeed[this.GetType()];
    protected TimeSpan _elapsed = TimeSpan.Zero;
    protected float _pathProgress;
    public (int,int) TilePosition {get; set;}
    public abstract MonsterType MonsterType {get;}
    private bool shouldRecalcPath;
    public bool shouldCheckMerge;
    public bool IsNew {get; set;}
    
    private TimeSpan attackCooldown = TimeSpan.FromMilliseconds(125 * 7);
    private TimeSpan elapsedAttackCooldown;

    protected abstract int attackFrame{get;}

    public MonsterGroup((int, int) tilePos, TimeSpan elapsed)
    {
        Monsters = new List<Monster>();
        TilePosition = tilePos;
        string type = MonsterUtils.GetMonsterTypeName(MonsterType);
        sprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite(type+"_idleS");
        sprite.Scale = mapManager.Scale;
        _elapsed = elapsed;
        _pathProgress = (float)(_elapsed/speed);
        IsNew = true;        
        elapsedAttackCooldown = TimeSpan.FromMilliseconds(125 * (7-attackFrame));
    }  

    // Satisfies the Entity requirement. Container handles the actual visual offset!
    public abstract Vector2 LocalOffset {get;}
    public int Count => Monsters.Count;
    public bool IsEmpty => this.Count == 0; 
    public float Health => Monsters.Sum(m => m.CurrentHealth);
    public float Damage => Monsters.Sum(m => m.DamagePerSecond); 

    protected bool _goalReached => Monsters.All(i => i.GoalReached);
    protected bool _oldGoalReached = false;
    private bool isMerging;

    public abstract bool isGrounded {get;}

    public void ResetGroup((int,int) pos)
    {
        shouldRecalcPath = false;
        TilePosition = pos;
    }

    public virtual Vector2 GetInterpolatedWorldPosition()
    {
        Vector2 origin = mapManager.GetWorldPositionOfTile(TilePosition);
        if (Monsters.Count == 0) return origin;
        
        var goal = Monsters[0].GetCurrentGoal();
        Vector2 goalPos = mapManager.GetWorldPositionOfTile(goal);
        return origin + (goalPos - origin) * _pathProgress;
    }

    public void RemoveDead() 
    {
        int oldCount = Count;
        Vector2 worldPos = GetInterpolatedWorldPosition();
        switch (MonsterType)
        {
            case MonsterType.Regular:
                worldPos = worldPos +  (new Vector2(45, 5) * mapManager.Scale);
                break;
            case MonsterType.Flying:
                worldPos = worldPos +  (new Vector2(20, 5) * mapManager.Scale);
                break;
            case MonsterType.Small:
                worldPos = worldPos +  (new Vector2(32, 25) * mapManager.Scale);
                break;
            case MonsterType.Big:
                worldPos = worldPos +  (new Vector2(28, -5) * mapManager.Scale);
                break;
        }
        Monsters.RemoveAll(m => m.IsDestroyed);
        if(Count <= 2 && oldCount > 2)
        {
            SetAnimation();
        }
        if(oldCount != Count)
        {
            for(int i = 0; i < 40; i++)
            {
                ParticleManager.AddParticle(new Particle
                {
                    WorldPosition = (worldPos - mapManager.Offset)/mapManager.Scale,
                    Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 150, (float)(Random.Shared.NextDouble() -0.5f) * 150),
                    LifeSpan = TimeSpan.FromSeconds(0.5),
                    Color = Color.Black
                });
            }
        }
    }
    
    public (int, int) CommonNextTarget()
    {
        if (IsEmpty) return this.TilePosition;
        return Monsters[0].GetCurrentGoal();
    }
    
    public (int, int)? CommonAttackTarget()
    {
        if (IsEmpty) return null;
        return Monsters[0].AttackTarget;
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
        shouldCheckMerge = false;

        if (_goalReached != _oldGoalReached && !isMerging)
        {
            SetAnimation();
        }
        
        _elapsed += gameTime.ElapsedGameTime;
        Monsters.ForEach(m => m.TickHitFlash(gameTime));
        if(isMerging && (speed - _elapsed).TotalMilliseconds < 125)
        {
            SetAnimation("rebuild");
            isMerging = false;
        }
        if (_elapsed >= speed)
        {
            _elapsed -= speed;
            if(!_goalReached)
            {
                Monsters.ForEach(i => i.ShouldMove = true);
            }
            else 
            {         
                shouldCheckMerge = true;
                if(shouldRecalcPath)
                {
                    foreach (Monster monster in Monsters)
                    {
                        monster.NextTarget = pathFindingManager.GetNextPosition(monster);
                        if(pathFindingManager.GetTarget(monster) is {} target)
                        {
                            Entity destStructure = mapManager.GetStructure(target);
                            if(destStructure != null)
                            {
                                if(monster.CanTarget(destStructure) && MapUtils.CalculateTileDistance(target, monster.TilePosition) <= 1)
                                {
                                    monster.AttackTarget = target; // Target the wall to attack
                                    continue;
                                }
                            }
                        }
                        monster.AttackTarget = null;
                    }
                    SetAnimation();
                    IsNew = true;
                }  
            }
        }
        _pathProgress = (float)(_elapsed / speed);

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
        mapManager.AddExistingMonsterGroup(this, CommonNextTarget(), shouldRecalcPath, _elapsed);
        return true;
    } 
    
    public void QueuePathRecalc()
    {
        shouldRecalcPath = true;
    }

    protected AnimatedSprite sprite;

    protected float HitFlashAlpha
    {
        get
        {
            if (Monsters.Count == 0) return 1f;
            float min = 1f;
            foreach (var m in Monsters)
            {
                if (m.HitFlashAlpha < min) min = m.HitFlashAlpha;
            }
            return min;
        }
    }

    public virtual void Draw(SpriteBatch spriteBatch/*, (int totalGroups, int rank) posOnTile*/)
    {
        if (IsEmpty) return;

        Vector2 worldPos = GetInterpolatedWorldPosition();

        // Use the container's rank logic to offset them so they don't overlap perfectly
        // Vector2 rankOffset = new Vector2(posOnTile.rank * 10 * mapManager.Scale.X, 0);

        sprite.Draw(spriteBatch, worldPos + (LocalOffset * mapManager.Scale), HitFlashAlpha);

    }

    public void SetScale(Vector2 scale)
    {
        sprite.Scale = scale;
    }

    private void SetAnimation(string anim = null)
    {
        string type = MonsterUtils.GetMonsterTypeName(MonsterType);
        string isAmalgam = Count >2 ? "_amalgam_" : "_";
        
        string direction = MapUtils.GetDirection(TilePosition, CommonAttackTarget() ?? CommonNextTarget());
        direction = direction == "" ? "S" : direction;
        switch (anim)
        {
            case "merge":
                anim = CommonNextTarget() == TilePosition ? "mergeIdle" + direction + "_smokeWalk" : "merge" + direction + "_smokeWalk";
                break;
            case "rebuild":
                anim = CommonNextTarget() == TilePosition ? (CommonAttackTarget() == null ? "rebuildIdle" + direction + "_idle" + direction : "rebuildIdle" + direction + "_attack" + direction) : "rebuild" + direction + "_walk" + direction;
                if(anim.Contains("attack"))
                {
                    elapsedAttackCooldown =  TimeSpan.FromMilliseconds(125 * (7-attackFrame -1));
                }
                break;
            default:
                anim = CommonNextTarget() == TilePosition ? (CommonAttackTarget() == null ? "idle" + direction :  "attack" + direction) : "walk" + direction;
                if(anim.Contains("attack"))
                {
                    elapsedAttackCooldown =  TimeSpan.FromMilliseconds(125 * (7-attackFrame));
                }
            break;
        }
        //Turn into Amalgam
        sprite.Animation = GamelabGame.textureAtlas["monsters"].GetAnimation(type+ isAmalgam + anim );
        if(direction.Contains("E")) sprite.SpriteEffects = SpriteEffects.FlipHorizontally;
        else sprite.SpriteEffects = SpriteEffects.None;
    }

    public void StartMerge()
    {
        //TODO: Add the merge anim once it exists
        SetAnimation("merge");
        //GamelabGame.GameSpeed = 0.1f;
        isMerging = true;
    }

    public void Attack(GameTime gameTime)
    {
        if (!_goalReached || CommonAttackTarget() == null)
        {
            elapsedAttackCooldown =  TimeSpan.FromMilliseconds(125 * (7-attackFrame));
            return;
        }
        
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        elapsedAttackCooldown += gameTime.ElapsedGameTime;
        if(elapsedAttackCooldown >= attackCooldown)
        {
            //tower should attack
            elapsedAttackCooldown -= attackCooldown;
            var structure = mapManager.GetStructure(CommonAttackTarget().Value);
            if (structure == null || structure.IsDestroyed || (structure is Meadow m && (!m.HasSheep || m.IsPolymorphed)))
            {
                return; 
            }
            Monsters.ForEach(i => i.Attack(gameTime, structure));
        }     
    }

}