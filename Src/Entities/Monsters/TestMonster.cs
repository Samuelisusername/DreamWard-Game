using System;
using System.Collections.Generic;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class TestMonster
{   
    /// <summary>
    /// THe map manage used by the monster to 
    /// </summary>
    private MapManager _mapManager;

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
    public bool GoalReached => _path.Count <= 1;

    public bool ShouldMove {get; set;}



    public TestMonster(MapManager mapManager, (int,int) tilePosition, (int,int) goal)
    {
        _mapManager = mapManager;
        RandomPath(tilePosition, goal);
    }

    public void RandomPath((int,int) tilePosition, (int,int) goal)
    {
        _path.Clear();
        if(goal == (-1,-1))
        {
            do
            {
                goal = (tilePosition.Item1 + (int)Random.Shared.NextInt64(-5,6), tilePosition.Item2 + (int)Random.Shared.NextInt64(-5,6));
            } while (goal.Item1 < 0 || goal.Item1 >= _mapManager.Rows || goal.Item2 < 0 || goal.Item2 >= _mapManager.Columns);            
        }
         
        while(goal != tilePosition)
        {
            _path.Add(goal);
            if(Math.Abs(goal.Item1 - tilePosition.Item1) > Math.Abs(goal.Item2 - tilePosition.Item2))
            {
                if(goal.Item1 < tilePosition.Item1)
                {
                    goal = (goal.Item1+1,goal.Item2);
                }
                else
                {
                    goal = (goal.Item1-1,goal.Item2);
                }
            }
            else
            {
                if(goal.Item2 < tilePosition.Item2)
                {
                    goal = (goal.Item1,goal.Item2+1);
                }
                else
                {
                    goal = (goal.Item1,goal.Item2-1);
                }
            }
        }   
        Path.Add(tilePosition);
        Path.Reverse();
    }


    public (int,int) GetCurrentGoal()
    {
        if(GoalReached)
        {
            return _path[0];
        }
        else
        {
            return _path[1];
        }
    }
}