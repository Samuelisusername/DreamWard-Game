using System;

public class GamePhaseManager
{
    public bool IsDay {get; private set;}

    /// <summary>
    /// Switches the game to day and set up the day cycle
    /// </summary>
    public void SwitchToDay()
    {
        // call the SanityManger, MapManger and CardManager to set up the new day
        IsDay = true;
        throw new NotImplementedException();
    }

    /// <summary>
    /// Switches the game to night and set up the night cycle
    /// </summary>
    public void SwitchToNight()
    {
        // call the CombatManager to set up the new Night
        IsDay = false;
        throw new NotImplementedException();
    }
}