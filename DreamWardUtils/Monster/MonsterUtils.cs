using System;

public enum MonsterType
{
    Regular,
    Flying
}

public static class MonsterUtils
{
    public static string GetMonsterTypeName(MonsterType type)
    {
        switch (type)
        {
            case MonsterType.Regular:
                return "normal";
            case MonsterType.Flying:
                return "fly";
            default:
                throw new ArgumentOutOfRangeException("cant translate type yet add it on MonsterUtils.cs");
        }
    }
}