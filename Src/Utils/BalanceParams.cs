using System;
using System.Collections.Generic;

public class BalanceParams
{
    public Dictionary<string,int> TowerRanges;
    public Dictionary<string,float> TowerDamage;
    public Dictionary<string,float> EntityHealth;
    public Dictionary<string,float> MonsterDamage;
    public Dictionary<string,TimeSpan> MonsterSpeed;
    public int StartingSanity;
    public float MoveCostMultiplier;
    public int MaxGrazingDays;
    public Dictionary<string,int> MeadowSanityGains;
    public Dictionary<string,int> CardCosts;
}