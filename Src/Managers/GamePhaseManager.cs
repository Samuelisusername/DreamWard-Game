using Gamelab;
using Gamelab.Screens;
using Gamelab.Managers;

public class GamePhaseManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CombatManager combatManager => GamelabGame.CombatManager;
    public bool IsDay { get; private set; } = true;
    public int DayCount { get; private set; } = 1;

    /// <summary>
    /// Switches the game to day and set up the day cycle
    /// </summary>
    public void SwitchToDay()
    {
        mapManager.GetSheep().TryUnpolymorphSheep();
        // call the SanityManger, MapManger and CardManager to set up the new day
        sanityManager.AddSanityGainedFromNewDay();
        //TODO: Call the Cardmanager to set up ned day
        IsDay = true;
        DayCount += 1;
        AudioManager.PlayMusic("day_music");
        checkGameOver();
    }

    /// <summary>
    /// Switches the game to night and set up the night cycle
    /// </summary>
    public void SwitchToNight()
    {
        combatManager.SpawnMonsters(DayCount * 3);
        IsDay = false;
        AudioManager.PlayMusic("night_music");
    }

    public void checkGameOver()
    {
        if (sanityManager.Sanity <= 0 || DayCount >= 10)
        {
            ///TODO: if DayCount >= 10 its not game over
            EndGame();
        }
    }

    private void EndGame()
    {
        GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
    }
}