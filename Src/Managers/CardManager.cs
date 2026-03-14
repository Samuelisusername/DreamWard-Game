using System;
using System.Collections.Generic;

public class CardManager
{
    public List<Card> TileCards {get; private set;}

    /// <summary>
    /// Get action cards
    /// </summary>
    /// <param name="amount">how many action cards should be drawn</param>
    /// <returns>An array with the Actioncards that were drawn</returns>
    public Card[] GetActionCards(int amount)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Shuffles the TileCards
    /// </summary>
    public void Shuffle()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// gets the top cards of the TileCardStack
    /// </summary>
    /// <param name="amount">how many cards should be returned</param>
    /// <returns>The cards that are on top of the TileCardStack</returns>
    public Card[] GetTopTileCards(int amount)
    {
        throw new NotImplementedException();
    }
}