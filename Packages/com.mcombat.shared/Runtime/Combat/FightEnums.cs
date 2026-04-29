public enum CriticalGaugeMode
{
    Normal,
    DoubleGain,
    Unlimited
}

public enum AIMode
{
    Aggressive,
    Dumb
}

// 系统会根据这个量来决定一场战斗结束后应该做什么。
public enum FightEventType
{
    Screensaver = 0,
    Quest = 1,
    Arena = 2,
    Gangbang = 3,
    Self = 4,
    SkillTest = 5,
    Event = 6
}

public enum FightMode
{
    Rotate = 1,
    Multi = 2,
    Evolve = 3,
    Group = 4
}

public enum TeamMode
{
    Keep = 0,
    MultiRaid = 1,
    Rotation = 2
}
