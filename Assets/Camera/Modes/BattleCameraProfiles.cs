using FightScene;

public readonly struct BattleCameraProfile
{
    public readonly float Pitch;
    public readonly float FieldOfView;
    public readonly float MinimumDistance;

    public BattleCameraProfile(float pitch, float fieldOfView, float minimumDistance)
    {
        Pitch = pitch;
        FieldOfView = fieldOfView;
        MinimumDistance = minimumDistance;
    }
}

public static class BattleCameraProfiles
{
    // The previous duel's (8 - 1.5) / 14 elevation is about 25 degrees.
    // The previous multiplayer's (15 - 2) / 20 elevation is about 33 degrees.
    public static readonly BattleCameraProfile Duel = new BattleCameraProfile(25, 45, 6);
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
