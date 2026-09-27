using UnityEngine;
using DG.Tweening;

class New2023 : CameraMode
{
    Vector3 cameraTargetPos;
    Vector3 enemiesCenter;
    Vector3 rotateToDirection;
    Vector2 meScreenPos;
    Vector2 enemyScreenPos;
    Vector3 xzOff;
    Vector3 lookPoint;
    Vector3 frontWPos, backWPos;
    Quaternion ToRotation;
    float _changeSpeed;
    float _transitionSpeedPara = 10f;
    readonly float _lookPointHeight = 2.3f;
    readonly float _minXZ;

    float TransitionSpeedPara
    {
        get => _transitionSpeedPara;
        set => _transitionSpeedPara = Mathf.Clamp(value, 0.2f, 5f);
    }
    
    public New2023(float XZDis, float YDis)
    {
        _minXZ = XZDis;
        this.XZDis = XZDis;
        this.YDis = YDis;
    }

    private float XZDistance
    {
        get => XZDis;
        set => XZDis = Mathf.Clamp(value, _minXZ , _minXZ + 20f);
    }

    public override void Enter(Camera _camera)
    {
        LocalUpdate(_camera);
        xzOff = _camera.transform.position - lookPoint;
        xzOff.y = 0;
        TransitionSpeedPara =5f;
        DOTween.To(()=> TransitionSpeedPara, (x) => TransitionSpeedPara = x, 0.001f, 1f);
    }

    float h;
    float ePosX;
    float ePosY;
    float mPosX;
    float mPosY;
    
    public override void LocalUpdate(Camera _camera)
    {
        h = UltimateJoystick.GetHorizontalAxis("RotateCamera");
        if (h != 0)
        {
            xzOff = Quaternion.AngleAxis(h * 1.5f, Vector3.up) * xzOff;
            xzOff.y = 0;
        }
        
        _changeSpeed = Time.deltaTime / (TransitionSpeedPara + Time.deltaTime); //分母里那个附加值越大，变得越慢。
        enemiesCenter = Vector3.zero;
        if (targets != null && targets.Count > 0)
        {
            foreach (var o in targets)
            {
                if (o != null)
                {
                    enemiesCenter += o.transform.position;
                }
            }
        }
        else
        {
            enemiesCenter = meCenter.position;
        }

        enemiesCenter /= targets.Count;

        enemyScreenPos = _camera.WorldToScreenPoint(enemiesCenter);
        meScreenPos = _camera.WorldToScreenPoint(meCenter.position);

        ePosX = (float)((decimal)enemyScreenPos.x / Screen.width);
        ePosY = (float)((decimal)enemyScreenPos.y / Screen.height);
        mPosX = (float)((decimal)meScreenPos.x / Screen.width);
        mPosY = (float)((decimal)meScreenPos.y / Screen.height);
        
        enemyScreenPos = new Vector2((enemyScreenPos.x /Screen.width),(enemyScreenPos.y /Screen.height));
        meScreenPos = new Vector2((meScreenPos.x /Screen.width), (meScreenPos.y /Screen.height));
        
        // 判断我与敌人哪个更接近相机位置
        if (enemyScreenPos.y >= meScreenPos.y)
        {
            frontWPos = meCenter.position;
            backWPos = enemiesCenter;
        }
        else
        {
            frontWPos = enemiesCenter;
            backWPos = meCenter.position;
        }
        
        if (ePosX >= 0.3 && ePosX <= 0.7 &&
            mPosX >= 0.3 && mPosX <= 0.7 &&
            ePosY >= 0.3 && ePosY <= 0.7 &&
            mPosY >= 0.3 && mPosY <= 0.7)
        {
            XZDistance -= _changeSpeed;
        }
        else if (ePosX <= 0.2 || ePosX >= 0.8 || 
                 mPosX <= 0.2 || mPosX >= 0.8 || 
                 ePosY <= 0.2 || ePosY >= 0.8 || 
                 mPosY <= 0.2 || mPosY >= 0.8)
        {
            XZDistance += _changeSpeed;
        }
                
        lookPoint = (backWPos - frontWPos) * 0.5f + frontWPos;
        cameraTargetPos = lookPoint + xzOff.normalized * XZDistance;
        cameraTargetPos.y = YDis;
        lookPoint.y = _lookPointHeight;
        
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, cameraTargetPos, _changeSpeed);
        rotateToDirection = lookPoint - cameraTargetPos;
        ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, _changeSpeed);
    }
}