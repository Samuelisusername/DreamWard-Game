using System;

public enum MonsterType
{
    Regular,
    Flying,
    Small,
    Big
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
            case MonsterType.Small:
                return "smol";
            case MonsterType.Big:
                return "strong";
            default:
                throw new ArgumentOutOfRangeException("cant translate type yet add it on MonsterUtils.cs");
        }
    }
}