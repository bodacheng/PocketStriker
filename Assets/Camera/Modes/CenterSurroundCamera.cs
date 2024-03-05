using UnityEngine;

public class CenterSurroundCamera : CameraMode
{
    Vector3 CameraTargetPos;
    Vector3 focuscenter = Vector3.zero; //校准中心。是根据控制角色与敌人中心两个位置决定
    Quaternion ToRotation; //目标相机旋角
    Vector3 xzOff = Vector3.forward; //相机从focuscenter出发的角度，最大的难点。
    Vector3 xzOff_onstartrecord;
    float h;
    readonly float xAngleTemp;
    Vector3 rotateToDirection;
    
    public CenterSurroundCamera(float XZDis, float YDis)
    {
        this.XZDis = XZDis;
        this.YDis = YDis;
    }
    
    public override void LocalUpdate(Camera _camera)
    {
        h = UnityEngine.Input.GetAxis("Horizontal") + UltimateJoystick.GetHorizontalAxis("RotateCamera");
        xzOff = Quaternion.AngleAxis(h * 1.5f, Vector3.up) * xzOff;                
        xzOff.y = 0;
        
        CameraTargetPos = focuscenter + xzOff.normalized * XZDis;
        CameraTargetPos.y = YDis;
        
        rotateToDirection = focuscenter - CameraTargetPos;
        rotateToDirection.y = 0;
        rotateToDirection = rotateToDirection.normalized + Vector3.down/2;
        
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, CameraTargetPos, Time.deltaTime / (0.2f + Time.deltaTime));//上下这两部分都是分母里那个附加值越大，变得越慢。
        ToRotation = Quaternion.LookRotation(rotateToDirection);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, Time.deltaTime / (0.2f + Time.deltaTime));
    }

    public override void Enter(Camera _camera)
    {
    }
}

