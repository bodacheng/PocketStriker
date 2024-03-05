using UnityEngine;

class CertainYAntiVibrationCamera : CameraMode
{
    Vector3 CameraTargetPos;
    Vector3 enemiescenter;//敌人的位置中心
    Vector3 focuscenter;//校准中心。是根据控制角色与敌人中心两个位置决定
    Quaternion ToRotation;//目标相机旋角
    Vector2 screenpos;
    Vector3 xzOff = Vector3.forward;//相机从focuscenter出发的角度，最大的难点。
    float angele;//这个现在是用来计算场地原点（0，0，0）与敌人中心，控制角色连线的角度
    float h;    
    Vector3 FirstPoint;
    Vector3 SecondPoint;
    Vector3 rotateToDirection;
    
    public CertainYAntiVibrationCamera(float XZDis, float YDis)
    {
        this.XZDis = XZDis;
        this.YDis = YDis;
    }
    
    public override void LocalUpdate(Camera _camera)
    {
        screenpos = _camera.WorldToViewportPoint(meCenter.position);
        if ((screenpos.x < 0.3 || screenpos.x > 0.7) && YDis < 9)
        {
            XZDis += 1f;
            YDis += 1f;
        }
        if ((screenpos.x > 0.4 && screenpos.x < 0.6 && screenpos.y > 0.4 && screenpos.y < 0.6) && XZDis > 12f)
        {
            XZDis -= 1f;
            YDis -= 1f;
        }
        if (targets.Count > 0)
        {
            enemiescenter = Vector3.zero;
            foreach (Transform o in this.targets)
            {
                if (o != null)
                    enemiescenter += o.transform.position;
                screenpos = _camera.WorldToViewportPoint(o.position);
                if (screenpos.x < 0.2 || screenpos.x > 0.8)
                {
                    XZDis += 0.4f;
                    YDis += 0.4f;
                }
                if ((screenpos.x > 0.4 && screenpos.x < 0.6 && screenpos.y > 0.4 && screenpos.y < 0.6) && XZDis > 10f)
                {
                    XZDis -= 0.4f;
                    YDis -= 0.4f;
                }
            }
            enemiescenter /= targets.Count;
            focuscenter = meCenter.position + (enemiescenter - meCenter.position) * 1 / 2;
        }else{
            focuscenter = meCenter.position;
        }
        enemiescenter.y = 0;
        angele = Vector3.Angle(meCenter.position - Vector3.zero, enemiescenter - Vector3.zero);
        if (auto)
        {
            xzOff = -(1 - (angele / (180f - angele))) * (meCenter.position + enemiescenter) + (angele / (180f - angele)) * (meCenter.position - enemiescenter);
        }
        else
        {
            h = UnityEngine.Input.GetAxis("Horizontal") + UltimateJoystick.GetHorizontalAxis("RotateCamera");
            xzOff = Quaternion.AngleAxis(h * 1.5f, Vector3.up) * xzOff;
            //if (Input.GetMouseButtonDown(0))//Input.GetTouch(0).phase == TouchPhase.Began
            //{
            //    FirstPoint = Input.mousePosition;
            //    xAngleTemp = xAngle;
            //    xzOff_onstartrecord = xzOff;
            //}else if (Input.GetMouseButton(0))
            //{
            //    SecondPoint = Input.mousePosition;
            //    xAngle = xAngleTemp - (SecondPoint.x - FirstPoint.x);
            //    xzOff = Quaternion.AngleAxis(xAngle, Vector3.up) * xzOff_onstartrecord;
            //}
        }
        xzOff.y = 0;

        //下面的那个(meCenter.position + enemiescenter)，其实是说从0，0，0到他们
        CameraTargetPos = focuscenter + xzOff.normalized * XZDis;//focuscenter + xzOff.normalized * XZDis;
        CameraTargetPos.y = Mathf.Clamp(YDis - angele / 180 * 10f, 6f,7f);//夹角越大，相机越低。夹角小说明两个角色在画面里一上一下，更俯视一些会看的更方便。
        
        rotateToDirection = focuscenter - CameraTargetPos;
        rotateToDirection.y = 0;
        rotateToDirection = rotateToDirection.normalized + Vector3.down/2;
        
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, CameraTargetPos, Time.deltaTime / (0.2f + Time.deltaTime));//上下这两部分都是分母里那个附加值越大，变得越慢。
        ToRotation = Quaternion.LookRotation(rotateToDirection);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, Time.deltaTime / (0.2f + Time.deltaTime));
    }
}