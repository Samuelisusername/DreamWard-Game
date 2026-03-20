
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
    /// <summary>
    /// THe map manage used by the monster to 
    /// </summary>
    private MapManager _mapManager;

    //this is the time it takes a Monster to move from one tile to the next
    private TimeSpan _speed; 

    //how much time elapsed since the monster started moving from the last tile?
    private TimeSpan _elapsed = TimeSpan.Zero;

    //where inbetween two tiles is the monster?
    private float _pathProgress;

    //the offset to draw the monster on the tile
    private readonly Vector2 _localOffset = new Vector2(-32,-90);

    private List<TestMonster> _monsters;

    /// <summary>
    /// Posistion of the entity on the map
    /// </summary>
    public (int,int) TilePosition {get; protected set;}

    //the animation of this entity
    protected AnimatedSprite _amalgamSprite;

    //the animation of this entity
    protected AnimatedSprite _normalSprite;

    private bool _goalReached => _monsters.Select(i => i.GoalReached).All(j => j);

    public int Count => _monsters.Count;

    public MonsterGroup(MapManager mapManager, TimeSpan speed, (int,int) tilePosition)
    {
        _amalgamSprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("amalgam_walk");
        _amalgamSprite.Scale = mapManager.Scale;
        _normalSprite = GamelabGame.textureAtlas["monsters"].CreateAnimatedSprite("normal_walk");
        _normalSprite.Scale = mapManager.Scale;
        _mapManager = mapManager;
        _speed = speed;
        _monsters = new List<TestMonster>();
        TilePosition = tilePosition;
    }
    
    public void Update(GameTime gameTime)
    {
        _amalgamSprite.Update(gameTime);
        _normalSprite.Update(gameTime);
        if(!_goalReached)
        {
            _elapsed += gameTime.ElapsedGameTime;
            if(_elapsed >= _speed)
            {
                _elapsed -= _speed;
                _monsters.ForEach(i => i.ShouldMove = !i.GoalReached);
                //_mapManager.MoveMonster(this,Path[0],Path[1]);
            }
            _pathProgress = (float)(_elapsed / _speed);
        }
    }
    
    public void Move()
    {        
        for (int i = 0; i <  _monsters.Count; i++)
        {
            if(!_monsters[i].GoalReached && _monsters[i].ShouldMove)
            {
                TestMonster monster = _monsters[i];
                _monsters.Remove(monster);
                _mapManager.MoveMonster(monster,monster.Path[0],monster.Path[1]);  
                monster.Path.RemoveAt(0);
                monster.ShouldMove = false;
                i--;
            }
        }
    }

    public void AddMonster(TestMonster monster)
    {
        _monsters.Add(monster);
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
        List<(int,int)> goals = _monsters.Select(i => i.GetCurrentGoal()).ToList();
        List<(int,int)> amalgamGoals = goals.Where(i => goals.Count(j => j == i) > 2).Distinct().ToList();
        List<(int,int)> singleGoals = goals.Where(i => goals.Count(j => j == i) <= 2).Distinct().ToList();

        foreach (var tileGoal in amalgamGoals)
        {
            Vector2 origin = _mapManager.GetWorldPositionOfTile(TilePosition.Item1, TilePosition.Item2);
            Vector2 goal = _mapManager.GetWorldPositionOfTile(tileGoal.Item1, tileGoal.Item2);
            Vector2 dir = goal-origin;
            _amalgamSprite.Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _amalgamSprite.Scale);     
        }

        
        foreach (var tileGoal in singleGoals)
        {
            Vector2 origin = _mapManager.GetWorldPositionOfTile(TilePosition.Item1, TilePosition.Item2);
            Vector2 goal = _mapManager.GetWorldPositionOfTile(tileGoal.Item1, tileGoal.Item2);
            Vector2 dir = goal-origin;
            _normalSprite.Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _normalSprite.Scale);     
        }
    }

    public void RandomziePath()
    {
        foreach (var monster in _monsters)
        {
            monster.RandomPath(TilePosition, (-1,-1));
        }
    }
}