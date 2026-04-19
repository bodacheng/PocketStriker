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
    Quaternion ToRotation;
    float _changeSpeed;
    float zoomOutSpeedExtra = 8;
    float zoomInSpeedExtra = 8;
    float _transitionSpeedPara = 10f;
    readonly float _lookPointHeight = 2.2f;
    readonly float _minXZ;
    readonly float fieldOfView;
    const float AutoRotateLerp = 4f;
    const float LookPointEnemyBias = 0.55f;
    const float FocusSmoothTime = 0.08f;
    const float EnemySmoothTime = 0.1f;
    Vector3 _smoothedMePos;
    Vector3 _smoothedEnemyCenter;
    Vector3 _mePosVelocity;
    Vector3 _enemyCenterVelocity;
    bool _followStateInitialized;
    
    float TransitionSpeedPara
    {
        get => _transitionSpeedPara;
        set => _transitionSpeedPara = Mathf.Clamp(value, 0.2f, 5f);
    }

    private float disToH; // 相机距离中心点的横轴距离与高的比值
    public MCamera(float XZDis, float YDis, float fieldOfView, float zoomOutSpeedExtra, float zoomInSpeedExtra)
    {
        _minXZ = XZDis;
        this.XZDis = XZDis;
        this.YDis = YDis;
        this.disToH = (float) ((decimal)this.YDis/ (decimal)this.XZDis);
        this.fieldOfView = fieldOfView;
        this.zoomOutSpeedExtra = zoomOutSpeedExtra;
        this.zoomInSpeedExtra = zoomInSpeedExtra;
    }

    private float XZDistance
    {
        get => XZDis;
        set => XZDis = Mathf.Clamp(value, _minXZ , _minXZ + 60f);
    }

    public override void Enter(Camera _camera)
    {
        CanSetH = true;
        _followStateInitialized = false;
        _mePosVelocity = Vector3.zero;
        _enemyCenterVelocity = Vector3.zero;
        _camera.fieldOfView = this.fieldOfView;
        CameraManager._subCamera.fieldOfView = this.fieldOfView;
        LocalUpdate(_camera);
        xzOff = _camera.transform.position - lookPoint;
        xzOff.y = 0;
        if (xzOff.sqrMagnitude <= 0.001f)
        {
            xzOff = GetDesiredOrbitDirection(mePos, enemiesCenter, -Vector3.forward);
        }
        TransitionSpeedPara = 5f;
        DOTween.To(()=> TransitionSpeedPara, (x) => TransitionSpeedPara = x, 0.001f, 1f);
    }

    public override void Exit(Camera _camera)
    {
        _followStateInitialized = false;
        _mePosVelocity = Vector3.zero;
        _enemyCenterVelocity = Vector3.zero;
    }

    float h;

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
    
    public override void LocalUpdate(Camera camera)
    {
        Vector3 rawMePos;
        if (meCenter != null)
        {
            rawMePos = meCenter.position;
        }
        else
        {
            if (!TryGetAveragePosition(myTeamTargets, out rawMePos))
            {
                return;
            }
        }
        
        _changeSpeed = Time.deltaTime / (TransitionSpeedPara + Time.deltaTime); //分母里那个附加值越大，变得越慢。
        bool hasTargets = TryGetAveragePosition(targets, out var rawEnemiesCenter);
        if (!hasTargets)
        {
            var fallbackForward = meCenter != null ? meCenter.forward : Vector3.forward;
            fallbackForward.y = 0;
            rawEnemiesCenter = rawMePos + (fallbackForward.sqrMagnitude > 0.001f ? fallbackForward.normalized : Vector3.forward) * 10f;
        }

        if (!_followStateInitialized)
        {
            _smoothedMePos = rawMePos;
            _smoothedEnemyCenter = rawEnemiesCenter;
            _followStateInitialized = true;
        }
        else
        {
            _smoothedMePos = Vector3.SmoothDamp(_smoothedMePos, rawMePos, ref _mePosVelocity, FocusSmoothTime);
            _smoothedEnemyCenter = Vector3.SmoothDamp(_smoothedEnemyCenter, rawEnemiesCenter, ref _enemyCenterVelocity, EnemySmoothTime);
        }

        mePos = _smoothedMePos;
        enemiesCenter = _smoothedEnemyCenter;
        
        enemyScreenPos = camera.WorldToScreenPoint(enemiesCenter);
        meScreenPos = camera.WorldToScreenPoint(mePos);
        
        if (CanSetH)
        {
            h = UltimateJoystick.GetHorizontalAxis("RotateCamera");
        }
        
        if (h != 0)
        {
            xzOff = Quaternion.AngleAxis(h * 2f, Vector3.up) * xzOff;
            xzOff.y = 0;
        }
        else if (hasTargets)
        {
            var desiredOrbit = GetDesiredOrbitDirection(mePos, enemiesCenter, xzOff);
            if (xzOff.sqrMagnitude <= 0.001f)
            {
                xzOff = desiredOrbit;
            }
            else
            {
                xzOff = Vector3.Slerp(xzOff.normalized, desiredOrbit, AutoRotateLerp * Time.deltaTime);
            }
            xzOff.y = 0;
        }

        void AdjustXZDis(List<Transform> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return;
            }

            bool shouldZoomOut = false;
            bool shouldZoomIn = true;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }
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
                XZDistance -= _changeSpeed * zoomInSpeedExtra;
            }
            else if (shouldZoomOut)
            {
                XZDistance += _changeSpeed * zoomOutSpeedExtra;
            }
        }
        
        var wholeTargets = new List<Transform>() { };
        if (meCenter != null)
        {
            wholeTargets.Add(meCenter);
        }
        if (myTeamTargets != null)
        {
            wholeTargets.AddRange(myTeamTargets);
        }
        if (targets != null)
        {
            wholeTargets.AddRange(targets);
        }
        AdjustXZDis(wholeTargets);
        
        YDis = XZDistance * disToH;

        lookPoint = Vector3.Lerp(mePos, enemiesCenter, hasTargets ? LookPointEnemyBias : 0.5f);
        cameraTargetPos = lookPoint + xzOff.normalized * XZDistance;
        cameraTargetPos.y = YDis;
        lookPoint.y = Mathf.Lerp(mePos.y, enemiesCenter.y, hasTargets ? 0.5f : 0f) + _lookPointHeight;
        
        if (hasTargets || h != 0)
        {
            camera.transform.position = Vector3.Lerp(camera.transform.position, cameraTargetPos, _changeSpeed);
            rotateToDirection = lookPoint - cameraTargetPos;
            ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
            camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, ToRotation, _changeSpeed);
        }
    }
}
