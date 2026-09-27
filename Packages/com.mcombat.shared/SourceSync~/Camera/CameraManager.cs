using System.Collections.Generic;
using MCombat.Shared.Camera;

public class CameraManager : CameraManagerCore
{
    protected override bool ReenterSameModeOnAssign => true;

    protected override IDictionary<C_Mode, CameraModeCore> CreateModeDictionary()
    {
        return new Dictionary<C_Mode, CameraModeCore>
        {
            {C_Mode.CertainYAntiVibration, new ChatGptFix(8f, 5f, 40f)},
            {C_Mode.ApproachToCertainDis, new LerpToCertainDistance(5f, 1f)},
            {C_Mode.keepTargetLeft, new keepTargetLeftCamera()},
            {C_Mode.WatchOver, new MCamera(20f, 15f, 25f)},
            {C_Mode.StartAndEnd, new StartToEndMode()},
            {C_Mode.RoundBoundary, new CenterSurroundCamera(25f, 10f)},
            {C_Mode.TopDown, new TouchTopDownCamera(12f, 20f, 25)},
            {C_Mode.ScreenSaver, new New2023(8.8f, 5f)}
        };
    }
}
