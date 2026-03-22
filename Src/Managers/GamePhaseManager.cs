using System;
using Gamelab;
using Gamelab.Screens;

public class GamePhaseManager
{

    public GamePhaseManager()
    {
    }

    public bool IsDay { get; private set; } = true;
    public int DayCount { get; private set; } = 1;

    /// <summary>
    /// Switches the game to day and set up the day cycle
    /// </summary>
    public void SwitchToDay()
    {
        // call the SanityManger, MapManger and CardManager to set up the new day
        //GamelabGame.SanityManager.AddSanityGainedFromNewDay();
        // call CardManager, how??
        // I'm 50/50 on whether GamePhaseManager should call something like mapManager.StartNewDay() or
        // if the MapManager/Renderer should just check the GamePhaseManager's IsDay property
        IsDay = true;
        DayCount += 1;
        checkGameOver();
    }

    /// <summary>
    /// Switches the game to night and set up the night cycle
    /// </summary>
    public void SwitchToNight()
    {
        GamelabGame.CombatManager.SpawnMonsters(DayCount * 3);
        IsDay = false;
        // Same as above with MapManager/MapRenderer
    }

    public void checkGameOver()
    {
        if (GamelabGame.SanityManager.Sanity <= 0 || DayCount >= 10)
        {
            EndGame();
        }
    }

    private void EndGame()
    {
        GamelabGame.Instance.SwitchToScreen(new Gamelab.Screens.GameOverScreen(GamelabGame.Instance));
    }
}