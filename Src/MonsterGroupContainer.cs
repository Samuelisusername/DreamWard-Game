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

    public bool NoMonsters => this.numberofgroups == 0;
    public int numberofgroups => this._Monstergroups.Count;
    public int numberOfMonsters() => _Monstergroups.Sum(m => m.Count); 

    public int how_many_monsters_of_that_type(MonsterType monstertype)
    {   
        return _Monstergroups.Where(g => g.monsterType == monstertype).Sum(g => g.Count);
    }

    public bool SingleMonster(MonsterType monstertype) => how_many_monsters_of_that_type(monstertype) == 1;

    public (int, int) position_on_tile(MonsterType MonsterGrouptype) 
    {
        var sorted_grouped_monstergroups = _Monstergroups.GroupBy(m => m.monsterType).OrderBy(bucket => bucket.Key).ToList();
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

        var matchingGroup = _Monstergroups.FirstOrDefault(g => g.monsterType == type && g.common_Next_Target() == target);

        if (matchingGroup != null)
        {
            matchingGroup.addMonster(monster);
        }
        else
        {
            var newGroup = new MonsterGroupNew_Unfinished(TilePosition, 0)
            {
                monsterType = type
            };
            newGroup.addMonster(monster);
            _Monstergroups.Add(newGroup);
        }
    }

    public void removeMonsterGroup(MonsterGroupNew_Unfinished group)
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

            if (group.isEmpty)
            {
                _Monstergroups.RemoveAt(i);
            }
        }
    }

    public void Move()
    {
        for (int i = _Monstergroups.Count - 1; i >= 0; i--)
        {
            _Monstergroups[i].move();
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
                group.take_damage_aoe(damage); 
            }
        }
        else
        {
            // Single Target: Just hit the first group in the container
            _Monstergroups[0].take_damage_single_target(damage);
        }
    }

    public void RemoveDead()
    {
        foreach(var group in _Monstergroups)
        {
            group.RemoveDead();
        }
        _Monstergroups.RemoveAll(g => g.isEmpty);
    }

    #region Visuals and Drawing
    private MapManager mapManager => GamelabGame.MapManager;
    private readonly Vector2 _localOffset = new Vector2(-32, -90);

    public Dictionary<string, AnimatedSprite> _amalgamSpriteIdle;
    public Dictionary<string, AnimatedSprite> _amalgamSpriteWalk;
    public Dictionary<string, AnimatedSprite> _amalgamSpriteAttack;
    public Dictionary<string, AnimatedSprite> _normalSpriteIdle;
    public Dictionary<string, AnimatedSprite> _normalSpriteWalk;
    public Dictionary<string, AnimatedSprite> _normalSpriteAttack;

    public void LoadSprites()
    {
        _amalgamSpriteIdle = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_idle", mapManager.Scale);
        _amalgamSpriteWalk = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_walk", mapManager.Scale);
        _amalgamSpriteAttack = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_attack", mapManager.Scale);
        _normalSpriteIdle = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_idle", mapManager.Scale);
        _normalSpriteWalk = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_walk", mapManager.Scale);
        _normalSpriteAttack = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_attack", mapManager.Scale);
    }

    public void SetScale(Vector2 scale)
    {
        if (_amalgamSpriteIdle == null) return; 
        
        foreach (var sprite in _amalgamSpriteIdle) sprite.Value.Scale = scale;
        foreach (var sprite in _amalgamSpriteWalk) sprite.Value.Scale = scale;
        foreach (var sprite in _amalgamSpriteAttack) sprite.Value.Scale = scale;
        foreach (var sprite in _normalSpriteIdle) sprite.Value.Scale = scale;
        foreach (var sprite in _normalSpriteWalk) sprite.Value.Scale = scale;
        foreach (var sprite in _normalSpriteAttack) sprite.Value.Scale = scale;
    }
    public void Draw(SpriteBatch spriteBatch)
    {
        if (_normalSpriteWalk == null || _normalSpriteWalk.Count == 0) LoadSprites();
        if (NoMonsters || _normalSpriteWalk == null) return;

        foreach (var group in _Monstergroups)
        {
            if (group.isEmpty) continue;

            Vector2 worldPos = group.GetInterpolatedWorldPosition();
            
            // 1. Get the direction using your original MapUtils method!
            string dir = MapUtils.GetDirection(TilePosition, group.common_Next_Target());

            // 2. Select the right sprite (Single vs Amalgam)
            Dictionary<string, AnimatedSprite> spriteDict;
            if (group.monsterType == MonsterType.Flying)
            {
                spriteDict = group.Count > 1 ? _amalgamSpriteWalk : _normalSpriteWalk; 
            }
            else
            {
                spriteDict = group.Count > 1 ? _amalgamSpriteWalk : _normalSpriteWalk;
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
                (int totalGroups, int rank) = position_on_tile(group.monsterType);
                Vector2 rankOffset = new Vector2(rank * 10 * mapManager.Scale.X, 0); 
                
                sprite.Draw(spriteBatch, worldPos + (_localOffset * mapManager.Scale) + rankOffset);
                
                // Keep the animation ticking!
                sprite.Update(new GameTime(TimeSpan.Zero, GamelabGame.Instance.TargetElapsedTime));
            }
        }
    }
    
#endregion
}