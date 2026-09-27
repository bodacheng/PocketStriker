using Soul;

public static class SkillLogIdentity
{
    public static string ResolveCurrentSkillKey(BehaviorRunner runner)
    {
        if (runner == null)
            return null;

        if (runner.currentSKillEntity != null && !string.IsNullOrEmpty(runner.currentSKillEntity.SkillID))
            return runner.currentSKillEntity.SkillID;

        return runner.GetNowState()?.StateKey;
    }
}
