using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Gamelab;
using Gamelab.Managers;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Represents a group of monsters with the same next target
/// </summary>
public abstract class MonsterGroup
{    
    // The shortcut to find your MapManager
    protected MapManager mapManager => GamelabGame.MapManager;
    protected CombatManager combatManager => GamelabGame.CombatManager;

    public List<Monster> Monsters { get; private set; }
    public (int,int) TilePosition {get; set;}
    public abstract MonsterType MonsterType {get;}
    public bool IsNew {get; set;}
    private string _lastWalkDirection = "S";

    private string _lastAnim = "";
    
    private TimeSpan attackCooldown => AnimFrameDelay * 7;
    private TimeSpan elapsedAttackCooldown;

    protected abstract int attackFrame{get;}

    public MonsterGroup((int, int) tilePos)
    {
        Monsters = new List<Monster>();
        TilePosition = tilePos;
        string type = MonsterUtils.GetMonsterTypeName(MonsterType);
        sprite = GamelabGame.TextureAtlas["monsters"].CreateAnimatedSprite(type+"_idleS");
        sprite.Animation.Delay = AnimFrameDelay;
        sprite.Scale = mapManager.Scale;
        IsNew = true;        
        elapsedAttackCooldown = AnimFrameDelay * (7-attackFrame);
    }  

    // Satisfies the Entity requirement. Container handles the actual visual offset!
    public abstract Vector2 LocalOffset {get;}
    public int Count => Monsters.Count;
    public bool IsEmpty => this.Count == 0; 
    public float Health => Monsters.Sum(m => m.CurrentHealth);
    public float Damage => Monsters.Sum(m => m.DamagePerSecond); 

    protected bool _goalReached => Monsters.All(i => i.TargetReached);
    protected bool _oldGoalReached = false;
    private bool isMerging;

    public abstract bool isGrounded {get;}
    protected virtual TimeSpan AnimFrameDelay => TimeSpan.FromMilliseconds(125);

    public void ResetGroup((int,int) pos)
    {
        TilePosition = pos;
        Moved = true;
    }

    public void SetLastDirection(string dir)
    {
        if (dir != "") _lastWalkDirection = dir;
    }

    public virtual Vector2 GetInterpolatedWorldPosition(bool basePoint = false)
    {
        Vector2 origin = mapManager.GetWorldPositionOfTile(TilePosition);
        if (Monsters.Count == 0) return origin;
        
        var goal = Monsters[0].GetCurrentGoal();
        Vector2 goalPos = mapManager.GetWorldPositionOfTile(goal);
        return origin + (goalPos - origin) * combatManager.PathProgress[MonsterType];
    }

    public void RemoveDead() 
    {
        int oldCount = Count;
        Vector2 worldPos = GetInterpolatedWorldPosition();
        switch (MonsterType)
        {
            case MonsterType.Regular:
                worldPos = worldPos +  (new Vector2(45, 11) * mapManager.Scale);
                break;
            case MonsterType.Flying:
                worldPos = worldPos +  (new Vector2(21, 8) * mapManager.Scale);
                break;
            case MonsterType.Small:
                worldPos = worldPos +  (new Vector2(33, 28) * mapManager.Scale);
                break;
            case MonsterType.Big:
                worldPos = worldPos +  (new Vector2(33, 5) * mapManager.Scale);
                break;
        }
        Monsters.RemoveAll(m => m.IsDestroyed);
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


    public void AddMonster(Monster monster)
    {
        if (IsEmpty || monster.GetCurrentGoal() == this.CommonNextTarget()) 
        {
            int oldCount = Count;
            Monsters.Add(monster);
        }
    }

    public void AddMonsters(List<Monster> monsters)
    {
        if (IsEmpty || monsters[0].GetCurrentGoal() == this.CommonNextTarget()) 
        {
            int oldCount = Count;
            Monsters.AddRange(monsters);
        }
    }

    public void Update(GameTime gameTime)
    {
        Moved = false;
        Monsters.ForEach(m => m.TickHitFlash(gameTime));
        if(isMerging && (combatManager.MonsterSpeeds[MonsterType] - combatManager.ElapsedTimers[MonsterType]) < AnimFrameDelay)
        {
            SetAnimation("rebuild");
            isMerging = false;
        }
        _oldGoalReached = _goalReached;
        sprite.Update(gameTime);
    }

    public bool Moved = true;
    
    // Tells the individual monsters to actually step forward
    public bool Move()
    {
        if(_goalReached || Moved)
        {
            return false;
        }
        mapManager.AddExistingMonsterGroup(this, CommonNextTarget());
        return true;
    }

    protected AnimatedSprite sprite;

    protected float HitFlashWhite
    {
        get
        {
            if (Monsters.Count == 0) return 0f;
            float max = 0f;
            foreach (var m in Monsters)
            {
                if (m.HitFlashWhite > max) max = m.HitFlashWhite;
            }
            return max;
        }
    }

    public virtual void Draw(SpriteBatch spriteBatch/*, (int totalGroups, int rank) posOnTile*/)
    {
        if (IsEmpty) return;

        Vector2 worldPos = GetInterpolatedWorldPosition();

        // Use the container's rank logic to offset them so they don't overlap perfectly
        // Vector2 rankOffset = new Vector2(posOnTile.rank * 10 * mapManager.Scale.X, 0);
        Vector2 spritePos = worldPos + (LocalOffset * mapManager.Scale);
        float flash = HitFlashWhite;
        if (flash > 0f)
        {
            spriteBatch.End();
            GamelabGame.WhiteFlashShader.Parameters["FlashAmount"].SetValue(flash);
            spriteBatch.Begin(effect: GamelabGame.WhiteFlashShader,
                samplerState: GamelabGame.RenderSampler,
                transformMatrix: GamelabGame.RenderTransform);
            sprite.Draw(spriteBatch, spritePos, 1.0f);
            spriteBatch.End();
            spriteBatch.Begin(samplerState: GamelabGame.RenderSampler,
                transformMatrix: GamelabGame.RenderTransform);
        }
        else
        {
            sprite.Draw(spriteBatch, spritePos, 1.0f);
        }
        if (Monsters.Contains(combatManager.PriorityMonster))
        {
            TextureRegion target = GamelabGame.TextureAtlas["interface"].GetRegion("target0");
            switch (MonsterType)
            {
                case MonsterType.Regular:
                    worldPos = worldPos + (new Vector2(35, -25) * mapManager.Scale);
                    break;
                case MonsterType.Flying:
                    worldPos = worldPos + (new Vector2(10, -35) * mapManager.Scale);
                    break;
                case MonsterType.Small:
                    worldPos = worldPos + (new Vector2(23, -5) * mapManager.Scale);
                    break;
                case MonsterType.Big:
                    worldPos = worldPos + (new Vector2(25, -45) * mapManager.Scale);
                    break;
            }
            target.Draw(spriteBatch, worldPos, Color.White, 0, Vector2.Zero, mapManager.Scale, SpriteEffects.None, 0);
        }

        
        //worldPos = GetInterpolatedWorldPosition(true);
        // switch (MonsterType)
        // {
        //     case MonsterType.Regular:
        //         worldPos = worldPos +  (new Vector2(45, 24) * mapManager.Scale );
        //         break;
        //     case MonsterType.Flying:
        //         worldPos = worldPos +  (new Vector2(21, 24) * mapManager.Scale );
        //         break;
        //     case MonsterType.Small:
        //         worldPos = worldPos +  (new Vector2(33, 31) * mapManager.Scale );
        //         break;
        //     case MonsterType.Big:
        //         worldPos = worldPos +  (new Vector2(33, 30) * mapManager.Scale );
        //         break;
        // }
        // spriteBatch.Draw(GamelabGame.DummyText,
        //         worldPos,
        //         new Rectangle(0, 0, 1, 1),
        //         Color.Red,
        //         0,
        //         Vector2.One,
        //         GamelabGame.MapManager.Scale * 2,
        //         SpriteEffects.None,
        //         0);
    }

    public void SetScale(Vector2 scale)
    {
        sprite.Scale = scale;
    }

    public void SetAnimation(string anim = null)
    {
        string type = MonsterUtils.GetMonsterTypeName(MonsterType);
        string isAmalgam = Count > 2 ? "_amalgam_" : "_";
        
        (int, int) dirTarget = CommonAttackTarget() ?? CommonNextTarget();
        string direction;
        if (dirTarget != TilePosition)
        {
            direction = MapUtils.GetDirection(TilePosition, dirTarget);
            _lastWalkDirection = direction;
        }
        else
        {
            direction = _lastWalkDirection;
        }
        switch (anim)
        {
            case "merge":
                anim = CommonNextTarget() == TilePosition ? "mergeIdle" + direction + "_smokeWalk" : "merge" + direction + "_smokeWalk";
                break;
            case "rebuild":
                anim = CommonNextTarget() == TilePosition ? (CommonAttackTarget() == null ? "rebuildIdle" + direction + "_idle" + direction : "rebuildIdle" + direction + "_attack" + direction) : "rebuild" + direction + "_walk" + direction;
                if(anim.Contains("attack"))
                {
                    elapsedAttackCooldown =  AnimFrameDelay * (7-attackFrame -1);
                }
                break;
            default:
                anim = CommonNextTarget() == TilePosition ? (CommonAttackTarget() == null ? "idle" + direction :  "attack" + direction) : "walk" + direction;
                if(anim.Contains("attack"))
                {
                    elapsedAttackCooldown =  AnimFrameDelay * (7-attackFrame);
                }
            break;
        }
        //Turn into Amalgam
        if(type+ isAmalgam + anim == _lastAnim)
        {
            return;
        }
        _lastAnim = type+ isAmalgam + anim;
        sprite.Animation = GamelabGame.TextureAtlas["monsters"].GetAnimation(type+ isAmalgam + anim );
        sprite.Animation.Delay = AnimFrameDelay;
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
            elapsedAttackCooldown =  AnimFrameDelay * (7-attackFrame);
            return;
        }
        
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        elapsedAttackCooldown += gameTime.ElapsedGameTime;
        if(elapsedAttackCooldown >= attackCooldown)
        {
            //monster should attack
            elapsedAttackCooldown -= attackCooldown;
            var structure = mapManager.GetStructure(CommonAttackTarget().Value);
            if (structure == null || structure.IsDestroyed || (structure is Meadow m && (!m.HasSheep || m.IsPolymorphed)))
            {
                return; 
            }
            Monsters.ForEach(i => i.Attack(gameTime, structure));
            AudioManager.Play("grunt_03");// now the attack sound happens on a group level
        }     
    }

}