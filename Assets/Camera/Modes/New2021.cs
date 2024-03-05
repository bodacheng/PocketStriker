using UnityEngine;

class New2021 : CameraMode//  改编自CertainYAntiVabration
{
    Vector3 CameraTargetPos;
    Vector3 enemiesCenter;//敌人的位置中心
    Quaternion ToRotation;//目标相机旋角
    Vector3 rotateToDirection;
    Vector3 temp;
    Vector2 mescreenpos;
    Vector2 enemyscreenpos;
    Vector3 xzOff = Vector3.forward;//相机从focuscenter出发的角度，最大的难点。
    
    public New2021(float XZDis, float YDis)
    {
        this.XZDis = XZDis;
        this.YDis = YDis;
    }

    float fixy, h;
    public override void LocalUpdate(Camera _camera)
    {
        h = Input.GetAxis("Horizontal") + UltimateJoystick.GetHorizontalAxis("RotateCamera");
        xzOff = Quaternion.AngleAxis(h * 1.5f, Vector3.up) * xzOff;
        xzOff.y = 0;
        if (auto)
        {
            if (targets != null && targets.Count > 0)
            {
                enemiesCenter = Vector3.zero;
                foreach (var o in targets)
                {
                    if (o != null)
                    {
                        enemiesCenter += o.transform.position;
                    }
                }
                enemiesCenter /= targets.Count;
                enemiesCenter.y = 0;
                enemyscreenpos = _camera.WorldToViewportPoint(enemiesCenter);
                mescreenpos = _camera.WorldToViewportPoint(meCenter.position);

                if (enemyscreenpos.x < 0.08 || enemyscreenpos.x > 0.92 || enemyscreenpos.y < 0.1)
                {
                    xzOff = Vector3.RotateTowards(xzOff, meCenter.position - enemiesCenter, 4 * Time.deltaTime, 0.0f);
                }else{
                    // special version
                    //Vector3 midpoint = (enemiescenter + meCenter.position) / 2;
                    //float dis1 = Vector3.Distance(_camera.transform.position, midpoint + GetVerticalDir(meCenter.position - enemiescenter) * 5f);    // 试验点1
                    //float dis2 = Vector3.Distance(_camera.transform.position, midpoint + GetVerticalDir(enemiescenter - meCenter.position) * 5f);    // 试验点1
                    //xzOff = dis1 < dis2 ? 
                    //Vector3.RotateTowards(xzOff, GetVerticalDir(meCenter.position - enemiescenter), Time.deltaTime, 0.0f)
                    //:
                    //Vector3.RotateTowards(xzOff, GetVerticalDir(enemiescenter - meCenter.position), Time.deltaTime, 0.0f);
                    
                    // 如果敌人和player在画面中太过倾向于一上一下的位置，自动调整相机至他们连线的垂直方向
                    if (Mathf.Abs(mescreenpos.x - enemyscreenpos.x) < (Mathf.Abs(mescreenpos.y - enemyscreenpos.y) + 0.2f) &&
                        (enemyscreenpos.x > 0.35 && enemyscreenpos.x < 0.65))
                    {
                        xzOff = Vector3.RotateTowards(xzOff, 
                            mescreenpos.x > enemyscreenpos.x ?
                            GetVerticalDir(meCenter.position - enemiesCenter):
                            GetVerticalDir(enemiesCenter - meCenter.position)
                            , Time.deltaTime, 0.0f);
                    }
                }
            }
        }

        // 相机所在位置是基于player位置偏移一定距离
        CameraTargetPos = meCenter.position + xzOff.normalized * XZDis;
        CameraTargetPos += Vector3.up * YDis;
        fixy = Mathf.Clamp(CameraTargetPos.y, YDis, CameraTargetPos.y);
        CameraTargetPos.y = fixy;
        //CameraTargetPos.y = Mathf.Clamp(YDis - angele / 180 * 10f, 6f,7f);//夹角越大，相机越低。夹角小说明两个角色在画面里一上一下，更俯视一些会看的更方便。
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, CameraTargetPos, Time.deltaTime / (0.2f + Time.deltaTime));//上下这两部分都是分母里那个附加值越大，变得越慢。

        temp = meCenter.position;
        temp = new Vector3(temp.x, 2, temp.z);

        rotateToDirection = temp - CameraTargetPos;
        ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, (Time.deltaTime) / (0.2f + Time.deltaTime));
    }
}