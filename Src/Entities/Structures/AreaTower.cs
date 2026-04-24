

using System.Runtime.CompilerServices;
using Gamelab;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Tower with depth-2 hex range (own tile + immediate neighbors + their neighbors).
/// </summary>
public class AreaTower : Tower
{
    public AreaTower((int,int) pos) : base("towerAOE_idle", pos)
    {
    }


    // protected override float damagePerSecond => 5f;

    // protected override int rangeDepth => 1;

    protected override (string,string)[] infos { get => [("Health:", $"{CurrentHealth}"), ("DPS:", $"{damagePerSecond}"), ("Total Damage Dealt:", $"{(int)totalDamage}")];}
}