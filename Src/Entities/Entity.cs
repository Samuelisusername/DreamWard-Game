
using Microsoft.Xna.Framework;

public class Entity
{
    protected int _health;

    public int[] TilePosition {get; private set;}

    public Vector2 GlobalPosition {get; private set;}

    public bool IsDestroyed => _health <= 0;

    public void TakeDamage(int amount)
    {
        _health -= amount;
    }
}