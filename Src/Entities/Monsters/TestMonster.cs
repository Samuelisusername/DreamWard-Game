using System;
using System.Collections.Generic;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class TestMonster : Entity
{   
    /// <summary>
    /// THe map manage used by the monster to 
    /// </summary>
    private MapManager _mapManager;

    //this is the time it takes a Monster to move from one tile to the next
    private TimeSpan _speed; 

    //how much time elapsed since the monster started moving from the last tile?
    private TimeSpan _elapsed = TimeSpan.Zero;

    //the path the entity tries to follow
    private List<(int,int)> _path = new List<(int,int)>();

    /// <summary>
    /// the path the entity tries to follow
    /// </summary>
    public List<(int,int)> Path 
    {
        get{return _path;}
        set
        {
            foreach (var item in value)
            {
                if(item.Item1 < 0 || item.Item1 >= _mapManager.Columns || item.Item2 <0 || item.Item2 >= _mapManager.Rows)
                {
                    _path = [value[0]];
                    return;
                }
            }
            _path = value;
        }
    }

    //did the entity reach its goal
    private bool _goalReached => _path.Count <= 1;

    //where inbetween two tiles is the monster?
    private float _pathProgress;

    //the offset to draw the monster on the tile
    private readonly Vector2 _localOffset = new Vector2(-32,-90);


    public TestMonster(AnimatedSprite sprite, MapManager mapManager, TimeSpan speed, (int,int) tilePosition)
    {
        _sprite = sprite;
        _sprite.Scale = mapManager.Scale;
        _mapManager = mapManager;
        _speed = speed;
        TilePosition = tilePosition;
    }

    public new void Update(GameTime gameTime)
    {
        if(!_goalReached)
        {
            _elapsed += gameTime.ElapsedGameTime;
            if(_elapsed >= _speed)
            {
                _elapsed -= _speed;
                _mapManager.MoveMonster(Path[0],Path[1]);
                Path.RemoveAt(0);
                TilePosition = Path[0];
            }
            _pathProgress = (float)(_elapsed / _speed);
        }
        

        base.Update(gameTime);
    }

    /// <summary>
    /// draw the monster
    /// </summary>
    /// <param name="spriteBatch"></param>
    public override void Draw(SpriteBatch spriteBatch)
    {
        if(_goalReached)
        {
            _sprite.Draw(spriteBatch, _mapManager.GetWorldPositionOfTile(TilePosition.Item1,TilePosition.Item2) + _localOffset * _sprite.Scale);
        }
        else
        {
            Vector2 origin = _mapManager.GetWorldPositionOfTile(Path[0].Item1, Path[0].Item2);
            Vector2 goal = _mapManager.GetWorldPositionOfTile(Path[1].Item1, Path[1].Item2);
            Vector2 dir = goal-origin;
            _sprite.Draw(spriteBatch, origin + dir * _pathProgress + _localOffset * _sprite.Scale);            
        }
    }
}