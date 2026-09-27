public static class AdventureModeRules
{
    public const int MultiMode = 1;
    public const int RotationMode = 2;
    public const int EvolutionMode = 3;

    public static bool IsTutorialStage(string stageId)
    {
        return stageId == "1" || stageId == "2";
    }

    public static int ResolveMode(string stageId, int configuredMode)
    {
        // The opening fights teach one hero's controls against the authored enemies.
        if (IsTutorialStage(stageId))
            return RotationMode;
        return configuredMode >= MultiMode && configuredMode <= EvolutionMode
            ? configuredMode
            : RotationMode;
    }

    public static bool UsesSingleHero(string stageId, bool evolutionMode)
    {
        return evolutionMode || IsTutorialStage(stageId);
    }

    public static string GetTeamSetKey(string stageId, bool evolutionMode)
    {
        return UsesSingleHero(stageId, evolutionMode) ? "arcade" : "origin";
    }

    public static int GetHeroLimit(string stageId, bool evolutionMode)
    {
        return UsesSingleHero(stageId, evolutionMode) ? 1 : 3;
    }

    public static bool IsValidHeroCount(string stageId, bool evolutionMode, int count)
    {
        return count >= 1 && count <= GetHeroLimit(stageId, evolutionMode);
    }
}
