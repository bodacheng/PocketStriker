using FightScene;

public readonly struct BattleCameraProfile
{
    public readonly float Pitch;
    public readonly float FieldOfView;
    public readonly float MinimumDistance;
    public readonly float CenterSmoothTime;
    public readonly float DistanceSmoothTime;

    public BattleCameraProfile(float pitch, float fieldOfView, float minimumDistance,
        float centerSmoothTime = 0.22f, float distanceSmoothTime = 2f)
    {
        Pitch = pitch;
        FieldOfView = fieldOfView;
        MinimumDistance = minimumDistance;
        CenterSmoothTime = centerSmoothTime;
        DistanceSmoothTime = distanceSmoothTime;
    }
}

public static class BattleCameraProfiles
{
    // Duels need a clearer view over the fighters and a prompt return to melee
    // framing after separation. Crowd cameras retain their wider, slower follow.
    public static readonly BattleCameraProfile Duel = new BattleCameraProfile(32, 45, 6, 0.10f, 0.45f);
    public static readonly BattleCameraProfile MultiRaid = new BattleCameraProfile(33, 45, 6);
    public static readonly BattleCameraProfile Group = new BattleCameraProfile(46, 50, 6);

    public static C_Mode ResolveMode(bool groupBattle, TeamMode first, TeamMode second)
        => groupBattle ? C_Mode.TopDown
            : first == TeamMode.Rotation && second == TeamMode.Rotation
                ? C_Mode.CertainYAntiVibration : C_Mode.WatchOver;
}

public class DuelBattleCamera : AllUnitsBattleCamera
{
    public DuelBattleCamera() : base(BattleCameraProfiles.Duel) { }
}

public sealed class MultiRaidBattleCamera : AllUnitsBattleCamera
{
    public MultiRaidBattleCamera() : base(BattleCameraProfiles.MultiRaid) { }
}

public sealed class GroupBattleCamera : AllUnitsBattleCamera
{
    public GroupBattleCamera() : base(BattleCameraProfiles.Group) { }
}
