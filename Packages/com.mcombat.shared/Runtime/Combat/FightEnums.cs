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

namespace MCombat.Shared.Combat
{
    public static class FightControlPolicy
    {
        public static bool IsGroupBattle(global::FightMode fightMode)
        {
            return fightMode == global::FightMode.Group;
        }

        public static bool IsGroupBattle(global::FightMode fightMode, global::FightEventType eventType)
        {
            return IsGroupBattle(fightMode) || eventType == global::FightEventType.Gangbang;
        }

        public static bool AllowsManualUnitControl(global::FightMode fightMode)
        {
            return !IsGroupBattle(fightMode);
        }

        public static bool AllowsManualUnitControl(global::FightMode fightMode, global::FightEventType eventType)
        {
            return !IsGroupBattle(fightMode, eventType);
        }

        public static bool ShouldForceAutoBattle(global::FightMode fightMode, global::FightEventType eventType)
        {
            return eventType is global::FightEventType.Screensaver or global::FightEventType.SkillTest
                   || !AllowsManualUnitControl(fightMode, eventType);
        }

        public static bool ShouldRunFirstQuestTutorial(
            global::FightMode fightMode,
            string fightId,
            global::FightEventType eventType)
        {
            return AllowsManualUnitControl(fightMode, eventType)
                   && fightId == "1"
                   && eventType == global::FightEventType.Quest;
        }
    }
}
