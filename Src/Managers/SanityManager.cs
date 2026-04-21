using System;
using System.Collections.Generic;
using Gamelab;
using Gamelab.Screens;

public class SanityManager
{
    private MapManager mapManager => GamelabGame.MapManager;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    
    public int Sanity {get; private set;}
    
    public int sanity_ {get; private set;}

    public SanityManager()
    {
        Sanity = 20; // initialize sanity at the beginning of the game
    }

    /// <summary>
    /// Deducts sanity from the players total. The game might end if sanity goes to 0.
    /// </summary>
    /// <param name="amount">the amount of sanity to remove</param>
    public void DeductSanity(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException("amount", "Amount cannot be negative");
        Sanity -= amount;
        if (Sanity <= 0)
        {
            Sanity = 0;
            if (gamePhaseManager != null)
                gamePhaseManager.checkGameOver();
            else
                GamelabGame.Instance.SwitchToScreen(new GameOverScreen(GamelabGame.Instance));
        }
    }

    /// <summary>
    /// Deducts sanity from the players total if possible, otherwise does nothing and returns false
    /// </summary>
    /// <param name="amount">the amount of sanity to remove</param>
    public bool TryDeductSanity(int amount)
    {   
        if (amount < 0) throw new ArgumentOutOfRangeException("amount", "Amount cannot be negative");
        if (Sanity <= amount) return false;
        Sanity -= amount;
        return true;
    }

    /// <summary>
    /// Calculates the Sanity gained at the beginning of a day and adds it to the total
    /// </summary>
    public int AddSanityGainedFromNewDay()
    {
        sanity_ = Sanity;
        Sanity += mapManager.GetSheep().SanityGain;
        List<PlacableMeadow> meadows = mapManager.GetAllMeadows();
        foreach (var meadow in meadows)
        {
            Sanity += meadow.SanityGain;
        }
        return Sanity-sanity_;
    }
}