using Gamelab;
using Gamelab.Screens;
using Gamelab.Managers;
using System;

public class GamePhaseManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    private SanityManager sanityManager => GamelabGame.SanityManager;
    private CombatManager combatManager => GamelabGame.CombatManager;
    private CardManager cardManager => GamelabGame.CardManager;
    public bool IsDay { get; private set; } = true;
    public bool IsInTransition { get; private set; } = false;
    public int DayCount { get; private set; } = 1;

    /// <summary>
    /// Switches the game to day and set up the day cycle
    /// </summary>
    public void SwitchToDay()
    {
        IsDay = true;
        IsInTransition = false;
        DayCount += 1;
        TutorialManager.AdvanceDay();
        mapManager.GetSheep()?.TryUnpolymorphSheep();
        // call the SanityManager, MapManager and CardManager to set up the new day
        int gained = sanityManager.AddSanityGainedFromNewDay();
        cardManager.GenerateDailyCards(DayCount);
        GamelabGame.Instance.MapScreenInstance?.SpawnSanityGainOverSheep(gained);
        GamelabGame.Instance.MapScreenInstance?.ShowDayPopup(DayCount);
        AudioManager.PlayMusic("day_music");
        checkGameOver();
        mapManager.ResetFall();
        GamelabGame.GameSpeed = 1.0f;
    }

    /// <summary>
    /// Switches the game to night and set up the night cycle
    /// </summary>
    public void SwitchToNight()
    {
        if (combatManager.SpawnableArea.Count-2 == 0)
        {
#if DEBUG
            Console.WriteLine("spawnableArea.count was 0");
#endif
            GameOverScreen.DidWin = false; 
            GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
            return; 
        }
        else
        {
#if DEBUG
            Console.WriteLine($"spawanablearea count was this: {combatManager.SpawnableArea.Count}");
#endif
        }
        TutorialManager.HandleTutorialAction(TutorialAction.StartedNight);
        mapManager.StartFalling();
        IsInTransition = true;
    }
    private int[] monsterCount = [3, 6, 12, 18, 30, 50, 90, 80, 80, 100];

    public void FinishSwitchToNight()
    {      
        combatManager.GetSpawnableArea();
        if (combatManager.SpawnableArea.Count-2 == 0)
        {
            // Force the game back to day or trigger a 'Game Over' / 'Island Lost' state
            GameOverScreen.DidWin = false; 
            GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
            return; 
        }
        else
        {
            Console.WriteLine($"spawanablearea count was this: {combatManager.SpawnableArea.Count-2}");
        }
        combatManager.SpawnMonsters(monsterCount[DayCount -1], DayCount);
        IsDay = false;
        AudioManager.PlayMusic("night_music");

    }

    public void checkGameOver()
    {
        if (sanityManager.Sanity <= 0)
        {
            EndGame(false); // lose
        }
        else if (DayCount > 10)
        {
            EndGame(true); // win
        }
    }

    private void EndGame(bool won)
    {
        GamelabGame.GameSpeed = 1.0f;
        GameOverScreen.DidWin = won; 
        GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
    }
}