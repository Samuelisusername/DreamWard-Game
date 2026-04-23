using System;
using System.Collections.Generic;

public class BalanceParams
{
    public Dictionary<Type,int> TowerRanges;
    public Dictionary<Type,float> TowerDamage;
    public Dictionary<Type,float> EntityHealth;
    public Dictionary<Type,float> MonsterDamage;
    public Dictionary<Type,TimeSpan> MonsterSpeed;
}