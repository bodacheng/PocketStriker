using MCombat.Shared.Camera;

public abstract class CameraMode : CameraModeCore, ICameraManagerLinkedMode
{
    public CameraManager cameraManager;

    public void SetCameraManager(CameraManagerCore cameraManagerCore)
    {
        cameraManager = (CameraManager)cameraManagerCore;
    }
}
