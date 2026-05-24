using Gamelab;
using Gamelab.Screens;
using Gamelab.Managers;
using Microsoft.Xna.Framework;
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
    private bool _pendingWin = false;
    private float _winTimer = 0f;

    public void Update(GameTime gameTime)
    {
        if (_pendingWin)
        {
            _winTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_winTimer <= 0f)
            {
                _pendingWin = false;
                EndGame(true, GameOverType.Win);
            }
        }
    }

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
        checkGameOver(GameOverType.Win);
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
            var screen = new GameOverScreen(GamelabGame.Instance);
            screen.SetGameOverType(GameOverType.TentIsolated);
            GamelabGame.Instance.SwitchToScreen(screen);
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
            var screen = new GameOverScreen(GamelabGame.Instance);
            screen.SetGameOverType(GameOverType.TentIsolated);
            GamelabGame.Instance.SwitchToScreen(screen);
            return; 
        }
        else
        {
            Console.WriteLine($"spawanablearea count was this: {combatManager.SpawnableArea.Count-2}");
        }
        if(DayCount - 1 < monsterCount.Length)
        {
            combatManager.SpawnMonsters(monsterCount[DayCount -1], DayCount);
        }
        else
        {
            combatManager.SpawnMonsters((100* ((DayCount -1)/10)) + (((DayCount -1) % 10) *((DayCount -1) % 10)), DayCount);
        }
        IsDay = false;
        AudioManager.PlayMusic("night_music");

    }

    public void checkGameOver(GameOverType type)
    {
        if (sanityManager.Sanity <= 0)
        {
            EndGame(false, type); // lose
        }
        else if (DayCount == 11)
        {
            _pendingWin = true; //win
            _winTimer = 2.0f; // wait 2 second
        }
    }

    private void EndGame(bool won, GameOverType type)
    {
        GamelabGame.GameSpeed = 1.0f;
        GameOverScreen.DidWin = won; 
            var screen = new GameOverScreen(GamelabGame.Instance);
            screen.SetGameOverType(type);
            GamelabGame.Instance.SwitchToScreen(screen);
    }
}