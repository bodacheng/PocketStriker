using UnityEngine;

class OneVOneMode : CameraMode
{
    Vector3 CameraTargetPos;
    Vector3 enemiesCenter;//敌人的位置中心
    Quaternion ToRotation;//目标相机旋角
    Vector3 rotateToDirection;// 这个值在整个计算里用于两次内容完全不同的计算。一个是计算xzOff的中间变量，一个是用于计算相机看向角度
    Vector2 mescreenpos;
    Vector2 enemyscreenpos;
    Vector3 xzOff = -Vector3.forward;//相机从focuscenter出发的角度，最大的难点。

    float startAutoRotateRange = 9; // 两个角色的实际位置低于该值的话则开始偏移向垂直角度

    readonly float xzMax = 16f;// 相机距离焦点的xz方向最远距离
    float lookdownDegree = 0.5f; //相机向下方看的角度，以横向为单位1
    float zoomAcc;
    float ZoomAcc // 相机调整焦距的加速度。以恒定速度拉远或拉近相机会造成抖动
    {
        get { return zoomAcc; }
        set
        {
            zoomAcc = Mathf.Clamp(value, -1f, 1f);
        }
    }

    float zoomcounter;
    float zoomChangeInter = 2f;// zoom in or out 切换所必须达到的时间间隔

    float heightOfXZRate = 0.65f;//高度恒定为XZ_distance的百分之多少
    float xzd;
    float XZ_distance
    {
        get { return xzd; }
        set
        {
            xzd = Mathf.Clamp(value, 8.5f, xzMax);
            YDis = this.xzd * heightOfXZRate;
        }
    }

    public OneVOneMode(float XZDis)
    {
        this.XZ_distance = XZDis;
        YDis = this.XZ_distance * heightOfXZRate;
    }

    bool justEnterdThisMode = true;
    public override void Enter(Camera _camera)
    {
        if (meCenter == null)
            return;
        if (justEnterdThisMode)
        {
            if (targets != null && targets.Count > 0)
            {
                enemiesCenter = Vector3.zero;
                foreach (Transform o in targets)
                {
                    if (o != null)
                    {
                        enemiesCenter += o.transform.position;
                    }
                }
                enemiesCenter /= targets.Count;
            }
            enemyscreenpos = _camera.WorldToViewportPoint(enemiesCenter);
            mescreenpos = _camera.WorldToViewportPoint(meCenter.position);
            temp = Mathf.Abs(mescreenpos.x - enemyscreenpos.x);
            temp = Mathf.Sqrt(temp);

            rotateToDirection = mescreenpos.x > enemyscreenpos.x ? GetVerticalDir(meCenter.position - enemiesCenter) : GetVerticalDir(enemiesCenter - meCenter.position);
            rotateToDirection = rotateToDirection * (1 - temp);
            rotateToDirection += (meCenter.position - enemiesCenter).normalized * temp;
            xzOff = rotateToDirection;

            justEnterdThisMode = false;
        }
    }

    public override void Exit(Camera _camera)
    {
        justEnterdThisMode = true;
    }

    float h, maxheight;
    float temp;
    bool zoomDirection = false;// false :拉近 true：拉远
    Vector3 SlerpCenter, tempV3;
    public override void LocalUpdate(Camera _camera)
    {
        if (meCenter == null)
            return;

        if (targets != null && targets.Count > 0)
        {
            enemiesCenter = Vector3.zero;
            foreach (Transform o in targets)
            {
                if (o != null)
                {
                    enemiesCenter += o.transform.position;
                }
            }
            enemiesCenter /= targets.Count;
            enemyscreenpos = _camera.WorldToViewportPoint(enemiesCenter);
            mescreenpos = _camera.WorldToViewportPoint(meCenter.position);

            zoomcounter += Time.deltaTime;

            void ZoomOut()
            {
                if (!zoomDirection) //&& zoomcounter > zoomChangeInter) // 敌人超出屏幕范围的话立刻zoomout
                {
                    zoomDirection = true;
                    ZoomAcc = 0;
                    zoomcounter = 0;
                }
                if (zoomDirection)
                    ZoomAcc += Time.deltaTime;
            }

            void ZoomIn()
            {
                if (zoomDirection && zoomcounter > zoomChangeInter) // zoomin的话需要确保zoomcounter > zoomChangeInter防止频繁zoomout或in从而画面过抖
                {
                    zoomDirection = false;
                    ZoomAcc = 0;
                    zoomcounter = 0;
                }
                if (!zoomDirection)
                    ZoomAcc -= Time.deltaTime;
            }

            if (mescreenpos.y < 0.2f)
            {
                ZoomIn();
            }
            else
            {
                if (enemyscreenpos.x < 0.1 || enemyscreenpos.x > 0.9 || enemyscreenpos.y < 0.2 || mescreenpos.x < 0.1 || mescreenpos.x > 0.9)
                {
                    ZoomOut();
                }
                else
                {
                    ZoomIn();
                }
            }
            XZ_distance += ZoomAcc;
        }

        if (auto)
        {
            if (targets != null && targets.Count > 0)
            {
                temp = Vector3.Distance(meCenter.position, enemiesCenter);
                if (temp < startAutoRotateRange)
                {
                    rotateToDirection = mescreenpos.x > enemyscreenpos.x ? GetVerticalDir(meCenter.position - enemiesCenter) : GetVerticalDir(enemiesCenter - meCenter.position);
                    speed = Vector3.Angle(xzOff, rotateToDirection) / 180;
                    xzOff = Vector3.RotateTowards(xzOff, rotateToDirection, speed * Time.deltaTime / (0.2f + Time.deltaTime), 0.0f);
                }
            }
        }

        h = Input.GetAxis("Horizontal") + UltimateJoystick.GetHorizontalAxis("RotateCamera");
        xzOff = Quaternion.AngleAxis(h * 2f, Vector3.up) * xzOff;
        maxheight = Mathf.Max(meCenter.position.y, enemiesCenter.y);

        // 相机所在位置计算
        temp = Mathf.Max(maxheight, YDis);
        // Slerp中心
        SlerpCenter = meCenter.position;
        SlerpCenter.y = temp;

        // Slerp目标
        CameraTargetPos = meCenter.position + xzOff.normalized * XZ_distance;
        CameraTargetPos.y = temp;
        
        // Slerp
        tempV3 = Vector3.Slerp(_camera.transform.position - SlerpCenter, CameraTargetPos - SlerpCenter, Time.deltaTime / (0.1f + Time.deltaTime));//分母里那个附加值越大，变得越慢。

        _camera.transform.position = tempV3 + SlerpCenter;

        // 计算相机“看向的位置”
        rotateToDirection = meCenter.position - _camera.transform.position;
        rotateToDirection.y = 0;
        rotateToDirection = rotateToDirection.normalized;
        rotateToDirection.y = -lookdownDegree * 1;
        ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, Time.deltaTime / (0.1f + Time.deltaTime));
    }
}