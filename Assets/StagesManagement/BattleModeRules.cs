/// <summary>Encounter-owned modes; saved practice choices never override progression battles.</summary>
public static class BattleModeRules
{
    public static bool AllowsModeSwitch(FightEventType eventType)
    {
        return eventType == FightEventType.Self || eventType == FightEventType.SkillTest;
    }

    public static bool AllowsGroupBattle(FightEventType eventType)
    {
        return eventType != FightEventType.Event && eventType != FightEventType.Arena;
    }

    public static FightMode ResolveMode(FightEventType eventType, string stageId,
        int configuredMode, bool groupBattle, FightMode currentMode)
    {
        // Boss encounters and ranked Arena are always sequential fights.
        if (eventType == FightEventType.Event || eventType == FightEventType.Arena)
            return FightMode.Rotate;
        if (AllowsModeSwitch(eventType))
            return currentMode;
        if (groupBattle || eventType == FightEventType.Gangbang)
            return FightMode.Group;
        if (eventType == FightEventType.Quest)
        {
            switch (AdventureModeRules.ResolveMode(stageId, configuredMode))
            {
                case AdventureModeRules.MultiMode: return FightMode.Multi;
                case AdventureModeRules.EvolutionMode: return FightMode.Evolve;
                case AdventureModeRules.GroupMode: return FightMode.Group;
                default: return FightMode.Rotate;
            }
        }
        return currentMode;
    }

    public static int GetPreparationMode(FightMode mode)
    {
        switch (mode)
        {
            case FightMode.Multi: return AdventureModeRules.MultiMode;
            case FightMode.Evolve: return AdventureModeRules.EvolutionMode;
            case FightMode.Group: return AdventureModeRules.GroupMode;
            default: return AdventureModeRules.RotationMode;
        }
    }
}
