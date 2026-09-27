using UnityEngine;
using DG.Tweening;
class New2022 : CameraMode//  改编自CertainYAntiVabration
{
Vector3 cameraTargetPos;
Vector3 enemiesCenter;//敌人的位置中心
Quaternion ToRotation;//目标相机旋角
Vector3 rotateToDirection;
Vector2 meScreenPos;
Vector2 enemyScreenPos;
Vector3 xzOff = - Vector3.forward;//相机从focuscenter出发的角度，最大的难点。
Vector3 lookPoint;
Vector3 frontWPos, backWPos;
Vector2 frontScreenPos, backScreenPos;

private float changeSpeed = 0.7f;
private float minXZ;

private float time_counter;
private float autoRotateDelay = 3f;

private float transitionSpeedPara = 10f;
public New2022(float XZDis, float YDis)
{
    minXZ = XZDis;
    this.XZDis = XZDis;
    this.YDis = YDis;
}

private float XZDistance
{
    get => XZDis;
    set
    {
        XZDis = Mathf.Clamp(value, minXZ , minXZ + 20f);
    }
}

public override void Enter(Camera _camera)
{
    LocalUpdate(_camera);
    xzOff = _camera.transform.position - frontWPos;
    xzOff.y = 0;
    transitionSpeedPara = 10f;
    DOTween.To(()=> transitionSpeedPara, (x) => transitionSpeedPara = x, 0.2f, 1f);
}

float fixY, h;
private float rate;//指的是双方连线相对水平方向的夹脚刍议180得到的数值。极值0和1都代表双方接近水平，0.5为垂直
private float c_offSet;//相机位置相对双方连线的偏移点，同时也是相机朝向的偏移变量，由rate计算而出，在rate为0和1时候，为0.5。
public override void LocalUpdate(Camera _camera)
{
    time_counter += Time.deltaTime;
    
    h = UltimateJoystick.GetHorizontalAxis("RotateCamera");
    xzOff = Quaternion.AngleAxis(h * 1.5f, Vector3.up) * xzOff;
    xzOff.y = 0;

    changeSpeed = Time.deltaTime / (transitionSpeedPara + Time.deltaTime);//分母里那个附加值越大，变得越慢。
    
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
        
        enemyScreenPos = _camera.WorldToViewportPoint(enemiesCenter);
        meScreenPos = _camera.WorldToViewportPoint(meCenter.position);
        
        // 判断我与敌人哪个更接近相机位置
        if (enemyScreenPos.y >= meScreenPos.y)
        {
            frontWPos = meCenter.position;
            frontScreenPos = meScreenPos;
            backWPos = enemiesCenter;
            backScreenPos = enemyScreenPos;
        }
        else
        {
            frontWPos = enemiesCenter;
            frontScreenPos = enemyScreenPos;
            backWPos = meCenter.position;
            backScreenPos = meScreenPos;
        }
        
        if (frontScreenPos.x <= 0.2 || frontScreenPos.x >= 0.8 || 
            backScreenPos.x <= 0.2 || backScreenPos.x >= 0.8 )
        {
            XZDistance += changeSpeed;
        }
        else
        {
            XZDistance -= changeSpeed;
        }
        
        rate = Vector2.Angle((backScreenPos - frontScreenPos), Vector2.right) / 180;
        // 0，1的时候数值为0.5,意思为双方站位越水平，越看向他们中间点。
        // 而0.5时候（双方站位垂直于屏幕）数值为0，为了让相机处于更靠近屏幕角色的正后
        c_offSet = (float)(2 * (Mathf.Pow(rate,2) - rate) + 0.5); 
        
        if (auto && time_counter > autoRotateDelay && (rate >= 0.3 && rate <= 0.7))//双方站位太垂直的情况下才主动旋转相机
        {
            xzOff = Vector3.RotateTowards(xzOff, 
                frontWPos.x > backWPos.x ?
                    GetVerticalDir(frontWPos - backWPos) : GetVerticalDir(backWPos - frontWPos), Time.deltaTime, 0.0f);
            //xzOff = Vector3.RotateTowards(xzOff, GetVerticalDir(frontWPos - backWPos), Time.deltaTime, 0.0f);
        };
        
        cameraTargetPos = (backWPos - frontWPos) * c_offSet + frontWPos + xzOff.normalized * XZDistance;
        cameraTargetPos += Vector3.up * YDis;
        fixY = Mathf.Clamp(cameraTargetPos.y, YDis, cameraTargetPos.y);
        cameraTargetPos.y = fixY;
        
        //  两个角色的站位越相对垂直，就越将视线转向距离屏幕更远的一方 
        lookPoint = (backWPos - frontWPos) * (1 - c_offSet) + frontWPos; 
        lookPoint.y = 2.3f;
        
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, cameraTargetPos, changeSpeed);
        rotateToDirection = lookPoint - cameraTargetPos;
        ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, changeSpeed);
    }
    else
    {
        cameraTargetPos = meCenter.position + xzOff.normalized * XZDistance;
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, cameraTargetPos, changeSpeed);
    }
}
}