using UnityEngine;

class ScreenSaverC : CameraMode
{
    Vector3 CameraTargetPos;
    Vector3 enemiescenter;//敌人的位置中心
    Quaternion ToRotation;//目标相机旋角
    Vector3 rotateToDirection;
    
    Vector2 mescreenpos;
    Vector2 enemyscreenpos;
    Vector3 xzOff = Vector3.forward;//相机从focuscenter出发的角度，最大的难点。
    
    public ScreenSaverC(float XZDis, float YDis)
    {
        this.XZDis = XZDis;
        this.YDis = YDis;
    }

    float h = 0.3f;//相机旋转速度
    public override void LocalUpdate(Camera _camera)
    {
        if (auto)
        {
            if (targets != null && targets.Count > 0)
            {
                enemiescenter = Vector3.zero;
                foreach (Transform o in targets)
                {
                    if (o != null)
                    {
                        enemiescenter += o.transform.position;
                    }
                }
                enemiescenter /= targets.Count;
                enemiescenter.y = 0;
                enemyscreenpos = _camera.WorldToViewportPoint(enemiescenter);
                mescreenpos = _camera.WorldToViewportPoint(meCenter.position);
                if (enemyscreenpos.x < 0.08 || enemyscreenpos.x > 0.92 || enemyscreenpos.y < 0.2 || enemyscreenpos.y > 0.9 ||
                    mescreenpos.x < 0.08 || mescreenpos.x > 0.92 || mescreenpos.y < 0.2 || mescreenpos.y > 0.9)
                {
                    if (this.XZDis < 15)
                    {
                        this.XZDis += Time.deltaTime * 1.5f;
                        this.YDis += Time.deltaTime * 1.5f;
                    }
                }else{
                    if (enemyscreenpos.x > 0.45 || enemyscreenpos.x < 0.55 || enemyscreenpos.y > 0.45 || enemyscreenpos.y < 0.55 ||
                        mescreenpos.x > 0.45 || mescreenpos.x < 0.55 || mescreenpos.y > 0.45 || mescreenpos.y < 0.55)
                    {
                        if (this.XZDis > 8.5)
                        {
                            this.XZDis -= Time.deltaTime;
                            this.YDis -= Time.deltaTime;
                        }
                    }
                }
            }
            xzOff = Quaternion.AngleAxis(h, Vector3.up) * xzOff;
        }
        
        //下面的那个(meCenter.position + enemiescenter)，其实是说从0，0，0到他们
        CameraTargetPos = (meCenter.position + enemiescenter)/2 + xzOff.normalized * XZDis;//focuscenter + xzOff.normalized * XZDis;
        CameraTargetPos += Vector3.up * YDis;
        //CameraTargetPos.y = Mathf.Clamp(YDis - angele / 180 * 10f, 6f,7f);//夹角越大，相机越低。夹角小说明两个角色在画面里一上一下，更俯视一些会看的更方便。
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, CameraTargetPos, Time.deltaTime / (0.2f + Time.deltaTime));//上下这两部分都是分母里那个附加值越大，变得越慢。

        rotateToDirection = ((meCenter.position + enemiescenter)/2 + Vector3.up * 2f) - CameraTargetPos;
        ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, (Time.deltaTime) / (0.2f + Time.deltaTime));
    }
}