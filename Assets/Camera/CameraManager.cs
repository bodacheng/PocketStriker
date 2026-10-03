using System.Collections.Generic;
using MCombat.Shared.Camera;

[UnityEngine.DefaultExecutionOrder(1000)]
public class CameraManager : CameraManagerCore
{
    readonly BattleShadowCoverage _shadowCoverage = new BattleShadowCoverage();
    public BattleCameraMode CurrentBattleCamera => CurrentMode as BattleCameraMode;
    protected override bool ReenterSameModeOnAssign => CurrentMode is not BattleCameraMode;

    protected override void LateUpdate()
    {
        base.LateUpdate();
        _shadowCoverage.Update(mainCamera, CurrentBattleCamera?.ShadowReceiverDistance ?? 0,
            UnityEngine.Time.unscaledDeltaTime);
    }

    void OnDisable()
    {
        CancelArenaGesture();
        _shadowCoverage.Dispose();
    }
    void OnDestroy() => _shadowCoverage.Dispose();

    void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelArenaGesture();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) CancelArenaGesture();
    }

    void CancelArenaGesture()
    {
        if (CurrentMode is GroupBattleCamera arena) arena.OrbitGesture.Reset();
    }

    protected override IDictionary<C_Mode, CameraModeCore> CreateModeDictionary()
    {
        return new Dictionary<C_Mode, CameraModeCore>
        {
            {C_Mode.CertainYAntiVibration, new ChatGptFix()},
            {C_Mode.ApproachToCertainDis, new LerpToCertainDistance(5f, 1f)},
            {C_Mode.keepTargetLeft, new keepTargetLeftCamera()},
            {C_Mode.WatchOver, new MultiRaidBattleCamera()},
            {C_Mode.StartAndEnd, new StartToEndMode()},
            {C_Mode.RoundBoundary, new CenterSurroundCamera(25f, 10f)},
            {C_Mode.TopDown, new GroupBattleCamera()},
            {C_Mode.ScreenSaver, new New2023(8.8f, 5f)}
        };
    }
}
