using System;
using Gamelab;
using Gamelab.Screens;

public class GamePhaseManager
{
    private readonly SanityManager _sanityManager;
    private readonly MapManager _mapManager;
    private readonly CardManager _cardManager;
    private readonly CombatManager _combatManager;

    public GamePhaseManager(SanityManager sanityManager, MapManager mapManager, CardManager cardManager, CombatManager combatManager)
    {
        this._sanityManager = sanityManager ?? throw new ArgumentNullException(nameof(mapManager));
        this._mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
        this._cardManager = cardManager ?? throw new ArgumentNullException(nameof(cardManager));
        this._combatManager = combatManager ?? throw new ArgumentNullException(nameof(combatManager));
    }

    public bool IsDay { get; private set; } = true;
    public int DayCount { get; private set; } = 0;

    /// <summary>
    /// Switches the game to day and set up the day cycle
    /// </summary>
    public void SwitchToDay()
    {
        // call the SanityManger, MapManger and CardManager to set up the new day
        _sanityManager.AddSanityGainedFromNewDay();
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
        _combatManager.SpawnMonsters();
        IsDay = false;
        // Same as above with MapManager/MapRenderer
    }

    public void checkGameOver()
    {
        if (_sanityManager.Sanity <= 0 || DayCount >= 10)
        {
            EndGame();
        }
    }

    private void EndGame()
    {
        GamelabGame.Instance.SwitchToScreen(new Gamelab.Screens.GameOverScreen(GamelabGame.Instance));
    }
}