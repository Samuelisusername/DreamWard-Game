using System;
using System.Collections.Generic;
using System.Linq;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class MonsterGroup
{  
    private MapManager mapManager => GamelabGame.MapManager;
    
    //this is the time it takes a Monster to move from one tile to the next
    private TimeSpan _speed; 

    //how much time elapsed since the monster started moving from the last tile?
    private TimeSpan _elapsed = TimeSpan.Zero;

    //where inbetween two tiles is the monster?
    private float _pathProgress;

    //the offset to draw the monster on the tile
    private readonly Vector2 _localOffset = new Vector2(-32,-90);

    public List<Monster> Monsters{get; private set;}

    /// <summary>
    /// Posistion of the entity on the map
    /// </summary>
    public (int,int) TilePosition {get; protected set;}

    //the animation of this entity
    protected Dictionary<string,AnimatedSprite> _amalgamSpriteIdle;

    //the animation of this entity
    protected Dictionary<string,AnimatedSprite> _amalgamSpriteWalk;

    //the animation of this entity
    protected Dictionary<string,AnimatedSprite> _amalgamSpriteAttack;

    //the animation of this entity
    protected Dictionary<string,AnimatedSprite> _normalSpriteIdle;

    //the animation of this entity
    protected Dictionary<string,AnimatedSprite> _normalSpriteWalk;

    //the animation of this entity
    protected Dictionary<string,AnimatedSprite> _normalSpriteAttack;

    private bool _goalReached => Monsters.Select(i => i.GoalReached).All(j => j);

    public int Count => Monsters.Count;    

    public float TotalCurrentHealth => Monsters.Sum(m => m.CurrentHealth);
    public float TotalMaxHealth => Monsters.Sum(m => m.MaxHealth);

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
        if(Count == 0)
        {
            mapManager.RemoveMonsterGroup(TilePosition);
        }
    }

    public MonsterGroup(TimeSpan speed, (int,int) tilePosition)
    {
        _amalgamSpriteIdle = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_idle", mapManager.Scale);
        _amalgamSpriteWalk = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_walk", mapManager.Scale);
        _amalgamSpriteAttack = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_amalgam_attack", mapManager.Scale);
        _normalSpriteIdle = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_idle", mapManager.Scale);
        _normalSpriteWalk = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_walk", mapManager.Scale);
        _normalSpriteAttack = GamelabGame.textureAtlas["monsters"].CreateAnimatedSpriteAtlas("normal_attack", mapManager.Scale);
        _speed = speed;
        Monsters = new List<Monster>();
        TilePosition = tilePosition;
    }
    
    public void Update(GameTime gameTime)
    {
        foreach (var sprite in _amalgamSpriteIdle)
        {
            sprite.Value.Update(gameTime);
        }
        foreach (var sprite in _amalgamSpriteWalk)
        {
            sprite.Value.Update(gameTime);
        }
        foreach (var sprite in _amalgamSpriteAttack)
        {
            sprite.Value.Update(gameTime);
        }
        foreach (var sprite in _normalSpriteIdle)
        {
            sprite.Value.Update(gameTime);
        }
        foreach (var sprite in _normalSpriteWalk)
        {
            sprite.Value.Update(gameTime);
        }
        foreach (var sprite in _normalSpriteAttack)
        {
            sprite.Value.Update(gameTime);
        }
        if(!_goalReached)
        {
            // Before interpolating, check if any monster is blocked by a structure it can't step on.
            // If so, trim its path now so it never visually moves toward the blocked tile.
            for (int i = 0; i < Monsters.Count; i++)
            {
                var monster = Monsters[i];
                if (!monster.GoalReached)
                {
                    var dest = monster.Path[1];
                    Entity destStructure = mapManager.GetStructure(dest);
                    if (destStructure != null && !monster.CanStepOn(destStructure))
                    {
                        monster.AttackTarget = dest;
                        monster.Path = new List<(int,int)> { monster.Path[0] };
                    }
                }
            }

            _elapsed += gameTime.ElapsedGameTime;
            if(_elapsed >= _speed)
            {
                _elapsed -= _speed;
                Monsters.ForEach(i => i.ShouldMove = !i.GoalReached);
            }
            _pathProgress = (float)(_elapsed / _speed);
        }
    }
    
    public void Move()
    {
        for (int i = 0; i < Monsters.Count; i++)
        {
            if(!Monsters[i].GoalReached && Monsters[i].ShouldMove)
            {
                Monster monster = Monsters[i];
                Monsters.Remove(monster);
                monster.Move();
                monster.ShouldMove = false;
                i--;
            }
        }
        if(Monsters.Count == 0)
        {
            mapManager.RemoveMonsterGroup(TilePosition);
        }
    }

    public void AddMonster(Monster monster)
    {
        Monsters.Add(monster);
    }

    /// <summary>
    /// sets the scale for rendering the entity
    /// </summary>
    /// <param name="scale"></param>
    public void SetScale(Vector2 scale)
    {
        
        foreach (var sprite in _amalgamSpriteIdle)
        {
            sprite.Value.Scale = scale;
        }
        foreach (var sprite in _amalgamSpriteWalk)
        {
            sprite.Value.Scale = scale;
        }
        foreach (var sprite in _amalgamSpriteAttack)
        {
            sprite.Value.Scale = scale;
        }
        foreach (var sprite in _normalSpriteIdle)
        {
            sprite.Value.Scale = scale;
        }
        foreach (var sprite in _normalSpriteWalk)
        {
            sprite.Value.Scale = scale;
        }
        foreach (var sprite in _normalSpriteAttack)
        {
            sprite.Value.Scale = scale;
        }
    }

    /// <summary>
    /// draw the monster
    /// </summary>
    public void Draw(SpriteBatch spriteBatch)
    {  
        List<(int,int)> goals = Monsters.Select(i => i.GetCurrentGoal()).ToList();
        List<(int,int)> amalgamGoals = goals.Where(i => goals.Count(j => j == i) > 2).Distinct().ToList();
        List<(int,int)> singleGoals = goals.Where(i => goals.Count(j => j == i) <= 2).Distinct().ToList();

        foreach (var tileGoal in amalgamGoals)
        {
            Vector2 origin = mapManager.GetWorldPositionOfTile(TilePosition);
            Vector2 goal = mapManager.GetWorldPositionOfTile(tileGoal);
            Vector2 dir = goal-origin;
            if(dir == Vector2.Zero)
            {
                _amalgamSpriteIdle["S"].Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _amalgamSpriteIdle["S"].Scale);
            }
            else
            {
                string key = MapUtils.GetDirection(TilePosition, tileGoal);
                _amalgamSpriteWalk[key].Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _amalgamSpriteWalk[key].Scale);
            }
            
        }

        
        foreach (var tileGoal in singleGoals)
        {
            Vector2 origin = mapManager.GetWorldPositionOfTile(TilePosition);
            Vector2 goal = mapManager.GetWorldPositionOfTile(tileGoal);
            Vector2 dir = goal-origin;            
            if(dir == Vector2.Zero)
            {
                _normalSpriteIdle["S"].Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _normalSpriteIdle["S"].Scale);
            }
            else
            {
                string key = MapUtils.GetDirection(TilePosition, tileGoal);
                _normalSpriteWalk[key].Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _normalSpriteWalk[key].Scale);
            }     
        }
    }

    public void TakeDamage(float damage)
    {
        foreach (var monster in Monsters)
        {
            monster.TakeDamage(damage);
        }
    }
}