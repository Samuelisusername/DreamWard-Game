using System;
using System.Collections.Generic;
using System.Linq;
using Gamelab;
using Gamelab.Managers;
using Gamelab.Utils.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoGame.Extended.ECS;


public abstract class Monster : Entity
{
    protected MapManager mapManager => GamelabGame.MapManager;
    protected PathFindingManager pathFindingManager => GamelabGame.PathFindingManager;
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CombatManager combatManager => GamelabGame.CombatManager;

    Logger logger = new Logger("Monster");
    public (int col, int row) NextTarget{get; set;}

    // Damage this monster deals per second when attacking
    public virtual float DamagePerSecond => GamelabGame.Instance.Balance.MonsterDamage[this.GetType()];

    // The type of this monster (Regular or Flying)
    public abstract MonsterType Type { get; }

    // Has the monster reached its target and shouldn't move while it attacks
    public bool TargetReached => TilePosition == NextTarget;

    // Flag set by MonsterGroup when it's time to step forward
    public bool ShouldMove { get; set; }

    // The tile this monster is trying to attack (set when blocked or when reaching goal)
    public (int,int)? AttackTarget { get; set; }

    public string Facing {get; private set;}

    private static readonly TimeSpan hitFlashDuration = TimeSpan.FromMilliseconds(300);
    private TimeSpan hitFlashRemaining = TimeSpan.Zero;

    public float HitFlashWhite
    {
        get
        {
            if (hitFlashRemaining <= TimeSpan.Zero) return 0f;
            return (float)(hitFlashRemaining.TotalSeconds / hitFlashDuration.TotalSeconds);
        }
    }

    public void TickHitFlash(GameTime gameTime)
    {
        if (hitFlashRemaining > TimeSpan.Zero)
        {
            hitFlashRemaining -= gameTime.ElapsedGameTime;
            if (hitFlashRemaining < TimeSpan.Zero) hitFlashRemaining = TimeSpan.Zero;
        }
    }
    private float damageAccumulator;
    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);
        damageAccumulator += amount;
        if(damageAccumulator >= 1)
        {
            var group = mapManager.GetMonsterContainer(TilePosition).GetMonsterGroup(this);
            Vector2 worldPos = group.GetInterpolatedWorldPosition();
            switch (Type)
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
            ParticleManager.AddParticle(new Particle
            {
                WorldPosition = (worldPos -(Vector2.UnitX * ((float)Random.Shared.NextDouble() -0.5f) * 15 * mapManager.Scale) - mapManager.Offset)/mapManager.Scale,
                Velocity = new Vector2(((float)Random.Shared.NextDouble() -0.5f) * 6, -22f),
                Color = Color.Red,
                LifeSpan = TimeSpan.FromSeconds(1.0),
                Text = $"-{(int)damageAccumulator}",
                Font = GamelabGame.Font,
                Scale = 0.3f,
                Updater = (particle, gameTime) =>
                {
                    float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                    particle.Color  = Color.Lerp(particle.Color, Color.FromNonPremultiplied(particle.Color.R,particle.Color.G,particle.Color.B,(byte)0) , 1.0f * dt);
                    particle.Scale += 0.3f * dt;
                    particle.Velocity += new Vector2(5,7.5f) * dt;
                }
            });
            damageAccumulator -= MathF.Floor(damageAccumulator);
        }
        hitFlashRemaining = hitFlashDuration;
        if(IsDestroyed && combatManager.PriorityMonster == this)
        {
            var filteredList = mapManager.GetMonsterContainer(TilePosition).GetMonsterGroup(this).Monsters.Where(i => !i.IsDestroyed);
            if(filteredList.Any())
            {
                combatManager.PriorityMonster = filteredList.MinBy(i => i.CurrentHealth);
            }
        }
    }

    public override bool IsBuildEntity => false;

    protected override Vector2 _localOffset => throw new NotImplementedException();

    public Monster((int,int) startTile)
        : base(startTile)
    {
        // DamagePerSecond = type == MonsterType.Flying ? damagePerSecond * 0.5f : damagePerSecond;
        // Name = type == MonsterType.Regular ? "Regular" : "Flying";
        NextTarget = GamelabGame.PathFindingManager.GetNextPosition(this);
    }

    /// <summary>
    /// Returns whether this monster can target/attack the given entity.
    /// Regular: can hit all except VoidTile.
    /// Flying: can hit all except VoidTile and WallTile (flies over walls).
    /// </summary>
    public abstract bool CanTarget(Entity entity);

    /// <summary>
    /// Returns whether this monster can step on a tile occupied by the given entity.
    /// VoidTile: only flying monsters can step on it.
    /// WallTile/Fence/Tower: regular monsters cannot step on it (must attack first), flying ignores it.
    /// </summary>
    public abstract bool CanStepOn(Entity entity);
    // Inside your base Monster.cs class
    public virtual bool CanStepOnTile(int tileIndex)
{
    // By default, if the ground is missing (-1), monsters cannot walk there.
    return tileIndex != -1;
}


    /// <summary>
    /// Steps the monster forward on its path by one tile.
    /// Called by MonsterGroup when movement timing triggers.
    /// </summary>
    public void Move(bool shouldRecalcPath)
    {
        if (TargetReached || !ShouldMove) return;
        TilePosition = NextTarget; 
        if(shouldRecalcPath)
        {            
            // AttackTarget = null;
            // var newPath = pathFindingManager.CalculatePath(this);
            //     if (newPath != null && newPath.Count > 0)
            //         Path = newPath;  
        }
        NextTarget = pathFindingManager.GetNextPosition(this);
        ShouldMove = false;
    }

    /// <summary>
    /// Deals damage to entities on the same tile based on DPS and elapsed time.
    /// Only attacks when the monster has reached its goal.
    /// </summary>
    public void Attack(Entity target)
    {
        if (target == null || target.IsDestroyed) return;
        target.TakeDamage(DamagePerSecond);
    }

    public void Attack(GameTime gameTime, Structure structure)
    {
        
            if(sanityManager != null && structure is Tent)
            {                           
                sanityManager.DeductSanity((int)DamagePerSecond);
                AudioManager.Play("grunt_03"); 
                //TODO: grunt sound?
            }
            else
            {
                Attack(structure);
                if(structure.IsDestroyed)
                {
                    AttackTarget = null;
                }
                AudioManager.Play("grunt_03"); 
                //TODO: grunt sound?
            }       
    }

    /// <summary>
    /// Returns the next tile the monster is heading toward
    /// </summary>
    public (int,int) GetCurrentGoal()
    {
        return NextTarget;
    }
    

    public override void Update(GameTime gameTime)
    {
        throw new NotImplementedException();
    }
}
