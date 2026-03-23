
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class MonsterGroup
{  

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
    protected AnimatedSprite _amalgamSprite;

    //the animation of this entity
    protected AnimatedSprite _normalSprite;

    private bool _goalReached => Monsters.Select(i => i.GoalReached).All(j => j);

    public int Count => Monsters.Count;

    public Vector2 GetInterpolatedWorldPosition()
    {
        Vector2 origin = GamelabGame.MapManager.GetWorldPositionOfTile(TilePosition.Item1, TilePosition.Item2);
        if (Monsters.Count == 0) return origin;
        var goal = Monsters[0].GetCurrentGoal();
        Vector2 goalPos = GamelabGame.MapManager.GetWorldPositionOfTile(goal.Item1, goal.Item2);
        return origin + (goalPos - origin) * _pathProgress;
    }

    public float TotalCurrentHealth => Monsters.Sum(m => m.CurrentHealth);
    public float TotalMaxHealth => Monsters.Sum(m => m.MaxHealth);

    public void RemoveDead()
    {
        Monsters.RemoveAll(m => m.IsDestroyed);
        if(Count == 0)
        {
            GamelabGame.MapManager.RemoveMonsterGroup(TilePosition.Item1, TilePosition.Item2);
        }
    }

    public MonsterGroup(MapManager mapManager, TimeSpan speed, (int,int) tilePosition)
    {
        _amalgamSprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_amalgam_walkSW");
        _amalgamSprite.Scale = mapManager.Scale;
        _normalSprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_walkSW");
        _normalSprite.Scale = mapManager.Scale;
        _speed = speed;
        Monsters = new List<Monster>();
        TilePosition = tilePosition;
    }
    
    public void Update(GameTime gameTime)
    {
        _amalgamSprite.Update(gameTime);
        _normalSprite.Update(gameTime);
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
                    Entity destStructure = GamelabGame.MapManager.GetStructure(dest.Item1, dest.Item2);
                    if (destStructure != null && !monster.CanStepOn(destStructure))
                    {
                        monster.AttackTarget = dest;
                        monster.Path = new System.Collections.Generic.List<(int,int)> { monster.Path[0] };
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
                (int,int) origin = monster.Path[0];
                (int,int) dest = monster.Path[1];
                monster.Move();
                GamelabGame.MapManager.MoveMonster(monster, origin, dest);
                monster.ShouldMove = false;
                i--;
            }
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
        _amalgamSprite.Scale = scale;
        _normalSprite.Scale = scale;
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
            Vector2 origin = GamelabGame.MapManager.GetWorldPositionOfTile(TilePosition.Item1, TilePosition.Item2);
            Vector2 goal = GamelabGame.MapManager.GetWorldPositionOfTile(tileGoal.Item1, tileGoal.Item2);
            Vector2 dir = goal-origin;
            _amalgamSprite.Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _amalgamSprite.Scale);     
        }

        
        foreach (var tileGoal in singleGoals)
        {
            Vector2 origin = GamelabGame.MapManager.GetWorldPositionOfTile(TilePosition.Item1, TilePosition.Item2);
            Vector2 goal = GamelabGame.MapManager.GetWorldPositionOfTile(tileGoal.Item1, tileGoal.Item2);
            Vector2 dir = goal-origin;
            _normalSprite.Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _normalSprite.Scale);     
        }
    }

    public void RandomziePath()
    {
        foreach (var monster in Monsters)
        {
            monster.GenerateHexPath(TilePosition, (-1,-1));
        }
    }
}