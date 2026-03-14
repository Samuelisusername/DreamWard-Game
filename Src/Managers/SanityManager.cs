using System;

public class SanityManger
{

    public int Sanity {get; private set;}

    /// <summary>
    /// Removes sanity from the players total
    /// </summary>
    /// <param name="amount">the amount of sanity to remove if it is negative the amount is added insead</param>
    public void RemoveSanity(int amount)
    {
        Sanity -= amount;
    }

    /// <summary>
    /// Calculates the Sanity gained at the beginning of a day and adds it to the total
    /// </summary>
    public void AddSanityGainedFromNewDay()
    {
        throw new NotImplementedException();
    }
}