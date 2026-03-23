using System;
using Gamelab;
using Gamelab.Screens;

public class SanityManager
{

    public int Sanity {get; private set;}

    public SanityManager()
    {
        Sanity = 50; // initialize sanity at the beginning of the game
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
            if (GamelabGame.GamePhaseManager != null)
                GamelabGame.GamePhaseManager.checkGameOver();
            else
                GamelabGame.Instance.SwitchToScreen(new Gamelab.Screens.GameOverScreen(GamelabGame.Instance));
        }
    }

    /// <summary>
    /// Deducts sanity from the players total if possible, otherwise does nothing and returns false
    /// </summary>
    /// <param name="amount">the amount of sanity to remove</param>
    public bool TryDeductSanity(int amount)
    {   
        if (amount < 0) throw new ArgumentOutOfRangeException("amount", "Amount cannot be negative");
        if (Sanity < amount) return false;
        Sanity -= amount;
        return true;
    }

    /// <summary>
    /// Adds sanity to the players total
    /// </summary>
    /// <param name="amount">the amount of sanity to add</param>
    /// Not sure if this method is even needed...
    public bool AddSanity(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException("amount", "Amount cannot be negative");
        Sanity += amount;
        return true;
    }

    /// <summary>
    /// Calculates the Sanity gained at the beginning of a day and adds it to the total
    /// </summary>
    public void AddSanityGainedFromNewDay()
    {
        Sanity += 10;
        // Missing Ma
        // pManager functionality (meadows) to implement
        //for (int i = 0; i < _map_manager.Columns; i++)
        //{
        //    for (int j = 0; j < _map_manager.Rows; j++)
        //    {
        //        if (_map_manager.GetTile(i, j).IsMeadow)
        //        {
        //            int distance = _pathFindingManager.CalculateTileDistance(i, j, _map_manager.PlayerColumn, _map_manager.PlayerRow);
        //            if (distance <= 3) AddSanity(4);
        //            else AddSanity(2);
        //        }
        //        throw new notimplementedexception();
        //    }
        //}
    }
}