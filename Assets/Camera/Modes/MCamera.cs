using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

class MCamera : CameraMode
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
    float autoChangeAngleLimit = 30f;
    float autoRotateSpeed = 100;
    float _changeSpeed;
    float zoomSpeedExtra = 6;
    float _transitionSpeedPara = 10f;
    readonly float _lookPointHeight = 2f;
    readonly float _minXZ;
    float fieldOfView;
    private float screenDifferForRotate = 150;
    
    float TransitionSpeedPara
    {
        get => _transitionSpeedPara;
        set => _transitionSpeedPara = Mathf.Clamp(value, 0.2f, 5f);
    }

    private float disToH; // 相机距离中心点的横轴距离与高的比值
    public MCamera(float XZDis, float YDis, float fieldOfView)
    {
        _minXZ = XZDis;
        this.XZDis = XZDis;
        this.YDis = YDis;
        this.disToH = (float) ((decimal)this.YDis/ (decimal)this.XZDis);
        this.fieldOfView = fieldOfView;
    }

    private float XZDistance
    {
        get => XZDis;
        set => XZDis = Mathf.Clamp(value, _minXZ , _minXZ + 60f);
    }

    public override void Enter(Camera _camera)
    {
        CanSetH = true;
        _camera.fieldOfView = this.fieldOfView;
        CameraManager._subCamera.fieldOfView = this.fieldOfView;
        LocalUpdate(_camera);
        xzOff = _camera.transform.position - lookPoint;
        xzOff.y = 0;
        TransitionSpeedPara = 5f;
        DOTween.To(()=> TransitionSpeedPara, (x) => TransitionSpeedPara = x, 0.001f, 1f);
    }

    float h;
    float ePosX;
    float ePosY;
    float mPosX;
    float mPosY;

    private bool _canSetH;
    public bool CanSetH
    {
        get => _canSetH;
        set
        {
            _canSetH = value;
            if (!_canSetH)
                h = 0;
        }   
    }

    private Vector3 mePos;
    private float _autoRotateTimer;
    private bool _currentRotateClockWiseDirection;
    
    public override void LocalUpdate(Camera camera)
    {
        if (meCenter != null)
        {
            mePos = meCenter.position;
        }
        else
        {
            if (myTeamTargets.Count > 0)
            {
                mePos = Vector3.zero;
                foreach (var o in myTeamTargets)
                {
                    if (o != null)
                    {
                        mePos += o.transform.position;
                    }
                }
                mePos /= myTeamTargets.Count;
            }
        }
        
        _changeSpeed = Time.deltaTime / (TransitionSpeedPara + Time.deltaTime); //分母里那个附加值越大，变得越慢。
        bool hasTargets = targets != null && targets.Count > 0;
        if (hasTargets)
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
        }
        
        enemyScreenPos = camera.WorldToScreenPoint(enemiesCenter);
        meScreenPos = camera.WorldToScreenPoint(mePos);
        
        if (CanSetH)
        {
            h = UltimateJoystick.GetHorizontalAxis("RotateCamera");
        }
        
        if (h != 0)
        {
            xzOff = Quaternion.AngleAxis(h * 1.5f, Vector3.up) * xzOff;
            xzOff.y = 0;
        }
        else
        {
            // if (Vector2.Distance(meScreenPos, enemyScreenPos) > screenDifferForRotate)
            // {
            //     float angleToHorizontal = 0;
            //     float CheckNeedForAutoRotate()
            //     {
            //         if (meScreenPos.x < enemyScreenPos.x)
            //         {
            //             return Mathf.Abs(Vector2.Angle(enemyScreenPos - meScreenPos, Vector3.right));
            //         }
            //         else
            //         {
            //             return Mathf.Abs(Vector2.Angle(enemyScreenPos - meScreenPos, -Vector3.right));
            //         }
            //     }
            //     
            //     angleToHorizontal = CheckNeedForAutoRotate();
            //     if (angleToHorizontal > autoChangeAngleLimit)
            //     {
            //         bool Clock()
            //         {
            //             if (meScreenPos.x < enemyScreenPos.x)
            //             {
            //                 return meScreenPos.y < enemyScreenPos.y;
            //             }
            //             else
            //             {
            //                 return meScreenPos.y > enemyScreenPos.y;
            //             }
            //         }
            //         _currentRotateClockWiseDirection = Clock();
            //         // 如果夹角大于限制，则缓慢调整相机角度
            //         xzOff = Quaternion.Euler(0f, autoRotateSpeed *
            //                                      ((angleToHorizontal - autoChangeAngleLimit)/(90 - autoChangeAngleLimit)) * Time.deltaTime  // 分母是垂直情况下两个对象屏幕连线超出的"垂直界限"，分子是实际超过的界限。这个值是确保在垂直时候相机扭转最快，随后扭转变缓和
            //                                      * (_currentRotateClockWiseDirection ? -1f : 1f), 0f) * xzOff;
            //     }
            // }
        }

        void AdjustXZDis(List<Transform> targets)
        {
            bool shouldZoomOut = false;
            bool shouldZoomIn = true;
            foreach (var target in targets)
            {
                var screenPos = camera.WorldToScreenPoint(target.position);
                var ePosX = (float)((decimal)screenPos.x / Screen.width);
                var ePosY = (float)((decimal)screenPos.y / Screen.height);

                float edgeForIn = 0.15f;
                float edgeForOut = 0.1f;
                shouldZoomIn &= (ePosX >= edgeForIn && ePosX <= (1 - edgeForIn) && ePosY >= edgeForIn && ePosY <= (1 - edgeForIn));
                shouldZoomOut |= (ePosX < edgeForOut || ePosX > (1 - edgeForOut) || ePosY < edgeForOut || ePosY > (1 - edgeForOut));
            }
            
            if (shouldZoomIn)
            {
                XZDistance -= _changeSpeed * zoomSpeedExtra;
            }
            else if (shouldZoomOut)
            {
                XZDistance += _changeSpeed * zoomSpeedExtra;
            }
        }
        
        var wholeTargets = new List<Transform>() { };
        wholeTargets.AddRange(myTeamTargets);
        wholeTargets.AddRange(targets);
        AdjustXZDis(wholeTargets);
        
        YDis = XZDistance * disToH;
        
        // 判断我与敌人哪个更接近相机位置
        if (enemyScreenPos.y >= meScreenPos.y)
        {
            frontWPos = mePos;
            backWPos = enemiesCenter;
        }
        else
        {
            frontWPos = enemiesCenter;
            backWPos = mePos;
        }
        
        lookPoint = (backWPos - frontWPos) * 0.5f + frontWPos;
        cameraTargetPos = lookPoint + xzOff.normalized * XZDistance;
        cameraTargetPos.y = YDis;
        lookPoint.y = _lookPointHeight;
        
        if (hasTargets || h != 0)
        {
            camera.transform.position = Vector3.Lerp(camera.transform.position, cameraTargetPos, _changeSpeed);
            rotateToDirection = lookPoint - cameraTargetPos;
            ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
            camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, ToRotation, _changeSpeed);
        }
    }
}