using System.Collections.Generic;
using MCombat.Shared.Camera;

public class CameraManager : CameraManagerCore
{
    protected override IDictionary<C_Mode, CameraModeCore> CreateModeDictionary()
    {
        return new Dictionary<C_Mode, CameraModeCore>
        {
            {C_Mode.CertainYAntiVibration, new ChatGptFix(20f, 10f, 45f)},
            {C_Mode.ApproachToCertainDis, new LerpToCertainDistance(5f, 1f)},
            {C_Mode.keepTargetLeft, new keepTargetLeftCamera()},
            {C_Mode.WatchOver, new MCamera(35f, 15f, 30f, 20, 5)},
            {C_Mode.StartAndEnd, new StartToEndMode()},
            {C_Mode.RoundBoundary, new CenterSurroundCamera(25f, 10f)},
            {C_Mode.TopDown, new GangV(32, 23)},
            {C_Mode.ScreenSaver, new New2023(8.8f, 5f)}
        };
    }
}
