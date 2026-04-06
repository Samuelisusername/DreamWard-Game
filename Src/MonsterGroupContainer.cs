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
    public List<MonsterGroupNew_Unfinished> _Monstergroups; 
    
    public MonsterGroupContainer((int, int) tilePosition)
    {
        TilePosition = tilePosition;
        _Monstergroups = new List<MonsterGroupNew_Unfinished>();

    }

    public bool NoMonsters => this.Numberofgroups == 0;
    public int Numberofgroups => this._Monstergroups.Count;
    public int NumberOfMonsters() => _Monstergroups.Sum(m => m.Count); 

    public int GetNumberOfMonstersByType(MonsterType monstertype)
    {   
        return _Monstergroups.Where(g => g.MonsterType == monstertype).Sum(g => g.Count);
    }

    public bool SingleMonster(MonsterType monstertype) => GetNumberOfMonstersByType(monstertype) == 1;

    public (int, int) PositionOnTile(MonsterType MonsterGrouptype) 
    {
        var sorted_grouped_monstergroups = _Monstergroups.GroupBy(m => m.MonsterType).OrderBy(bucket => bucket.Key).ToList();
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

        var matchingGroup = _Monstergroups.FirstOrDefault(g => g.MonsterType == type && g.CommonNextTarget() == target);

        if (matchingGroup != null)
        {
            matchingGroup.AddMonster(monster);
        }
        else
        {
            var newGroup = new MonsterGroupNew_Unfinished(TilePosition, 0)
            {
                MonsterType = type
            };
            newGroup.AddMonster(monster);
            _Monstergroups.Add(newGroup);
        }
    }

    public void RemoveMonsterGroup(MonsterGroupNew_Unfinished group)
    {
        _Monstergroups.Remove(group);
    }

   public void Update(GameTime gameTime)
    {
        for (int i = _Monstergroups.Count - 1; i >= 0; i--)
        {
            var group = _Monstergroups[i];

            // --- THE MISSING WALL/FENCE CHECK ---
            // Assuming your MonsterGroupNew_Unfinished class has a public 'Monsters' list
            foreach (var monster in group.Monsters)
            {
                if (!monster.GoalReached && monster.Path != null && monster.Path.Count > 1)
                {
                    var dest = monster.Path[1];
                    Entity destStructure = mapManager.GetStructure(dest);
                    
                    // If there is a structure and the monster CANNOT fly/step over it
                    if (destStructure != null && !monster.CanStepOn(destStructure))
                    {
                        monster.AttackTarget = dest; // Target the wall to attack
                        monster.Path = new List<(int,int)> { monster.Path[0] }; // Stop moving
                    }
                }
            }
            // ------------------------------------

            group.Update(gameTime);

            if (group.IsEmpty)
            {
                _Monstergroups.RemoveAt(i);
            }
        }
    }

    public void Move()
    {
        for (int i = _Monstergroups.Count - 1; i >= 0; i--)
        {
            _Monstergroups[i].Move();
        }
    }

// Add "bool isAOE" to the arguments
    public void TakeDamage(float damage, bool isAOE)
    {
        if (NoMonsters) return;

        if (isAOE)
        {
            // AOE: Loop through every single group on this tile
            foreach (var group in _Monstergroups)
            {
                // Note: You will need to make sure MonsterGroupNew_Unfinished 
                // has a method called 'take_damage_aoe' that damages all monsters inside it!
                group.TakeDamageAOE(damage); 
            }
        }
        else
        {
            // Single Target: Just hit the first group in the container
            _Monstergroups[0].TakeDamageSingleTarget(damage);
        }
    }

    public void RemoveDead()
    {
        foreach(var group in _Monstergroups)
        {
            group.RemoveDead();
        }
        _Monstergroups.RemoveAll(g => g.IsEmpty);
    }

    #region Visuals and Drawing
    private MapManager mapManager => GamelabGame.MapManager;
    private readonly Vector2 _localOffset = new Vector2(-32, -90);

    public Dictionary<string, AnimatedSprite> AmalgamSpriteIdle;
    public Dictionary<string, AnimatedSprite> AmalgamSpriteWalk;
    public Dictionary<string, AnimatedSprite> AmalgamSpriteAttack;
    public Dictionary<string, AnimatedSprite> NormalSpriteIdle;
    public Dictionary<string, AnimatedSprite> NormalSpriteWalk;
    public Dictionary<string, AnimatedSprite> NormalSpriteAttack;

    public void LoadSprites()
    {
        AmalgamSpriteIdle = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_idle", mapManager.Scale);
        AmalgamSpriteWalk = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_walk", mapManager.Scale);
        AmalgamSpriteAttack = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_attack", mapManager.Scale);
        NormalSpriteIdle = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_idle", mapManager.Scale);
        NormalSpriteWalk = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_walk", mapManager.Scale);
        NormalSpriteAttack = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_attack", mapManager.Scale);
    }

    public void SetScale(Vector2 scale)
    {
        if (AmalgamSpriteIdle == null) return; 
        
        foreach (var sprite in AmalgamSpriteIdle) sprite.Value.Scale = scale;
        foreach (var sprite in AmalgamSpriteWalk) sprite.Value.Scale = scale;
        foreach (var sprite in AmalgamSpriteAttack) sprite.Value.Scale = scale;
        foreach (var sprite in NormalSpriteIdle) sprite.Value.Scale = scale;
        foreach (var sprite in NormalSpriteWalk) sprite.Value.Scale = scale;
        foreach (var sprite in NormalSpriteAttack) sprite.Value.Scale = scale;
    }
    public void Draw(SpriteBatch spriteBatch)
    {
        if (NormalSpriteWalk == null || NormalSpriteWalk.Count == 0) LoadSprites();
        if (NoMonsters || NormalSpriteWalk == null) return;

        foreach (var group in _Monstergroups)
        {
            if (group.IsEmpty) continue;

            Vector2 worldPos = group.GetInterpolatedWorldPosition();
            
            // 1. Get the direction using your original MapUtils method!
            string dir = MapUtils.GetDirection(TilePosition, group.CommonNextTarget());

            // 2. Select the right sprite (Single vs Amalgam)
            Dictionary<string, AnimatedSprite> spriteDict;
            if (group.MonsterType == MonsterType.Flying)
            {
                spriteDict = group.Count > 1 ? AmalgamSpriteWalk : NormalSpriteWalk; 
            }
            else
            {
                spriteDict = group.Count > 1 ? AmalgamSpriteWalk : NormalSpriteWalk;
            }
            
            // Fallback in case a direction name is missing or they are stationary
            if (string.IsNullOrEmpty(dir) || !spriteDict.ContainsKey(dir)) 
            {
                dir = "S"; // Fallback to South/Idle
            }

            // 3. Draw the sprite!
            if (spriteDict.TryGetValue(dir, out var sprite))
            {
                // Use the container's rank logic to offset them so they don't overlap perfectly
                (int totalGroups, int rank) = PositionOnTile(group.MonsterType);
                Vector2 rankOffset = new Vector2(rank * 10 * mapManager.Scale.X, 0); 
                
                sprite.Draw(spriteBatch, worldPos + (_localOffset * mapManager.Scale) + rankOffset);
                
                // Keep the animation ticking!
                sprite.Update(new GameTime(TimeSpan.Zero, GamelabGame.Instance.TargetElapsedTime));
            }
        }
    }
    
#endregion
}