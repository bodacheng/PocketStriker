using UnityEngine;
using DG.Tweening;

class ChatGptFix : CameraMode
{
    Vector3 cameraTargetPos;
    Vector3 enemiesCenter;
    Vector3 rotateToDirection;
    Vector2 meScreenPos;
    Vector2 enemyScreenPos;
    Vector3 xzOff;
    Vector3 lookPoint;
    Quaternion ToRotation;
    float autoRotateSpeed = 10;
    float _changeSpeed;
    float _transitionSpeedPara = 10f;
    readonly float _lookPointHeight = 2.2f;
    readonly float _minXZ;
    readonly float _minY;
    readonly float fieldOfView;
    const float LookPointEnemyBias = 0.55f;
    const float FocusSmoothTime = 0.08f;
    const float EnemySmoothTime = 0.1f;
    Vector3 _smoothedMePos;
    Vector3 _smoothedEnemyCenter;
    Vector3 _mePosVelocity;
    Vector3 _enemyCenterVelocity;
    bool _followStateInitialized;
    
    public bool AutoRotateCamera
    {
        get => PlayerPrefs.GetInt("AutoRotateCamera") == 1;
        set
        {
            PlayerPrefs.SetInt("AutoRotateCamera", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    float TransitionSpeedPara
    {
        get => _transitionSpeedPara;
        set => _transitionSpeedPara = Mathf.Clamp(value, 0.2f, 2f);
    }
    
    public ChatGptFix(float XZDis, float YDis, float fieldOfView)
    {
        _minXZ = XZDis;
        _minY = YDis;
        this.XZDis = XZDis;
        this.YDis = YDis;
        this.fieldOfView = fieldOfView;
    }

    private float XZDistance
    {
        get => XZDis;
        set => XZDis = Mathf.Clamp(value, _minXZ , _minXZ + 20f);
    }
    
    private float YDistance
    {
        get => YDis;
        set => YDis = Mathf.Clamp(value, _minY , _minY + 20f);
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
    
    public override void LocalUpdate(Camera camera)
    {
        if (meCenter == null)
        {
            return;
        }
        var rawMePos = meCenter.position;
        
        _changeSpeed = 2 * Time.deltaTime / (TransitionSpeedPara + Time.deltaTime); //分母里那个附加值越大，变得越慢。
        bool hasTargets = TryGetAveragePosition(targets, out var rawEnemiesCenter);
        if (!hasTargets)
        {
            rawEnemiesCenter = rawMePos + meCenter.forward * 10f;
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
        else if (AutoRotateCamera && hasTargets)
        {
            var desiredOrbit = GetDesiredOrbitDirection(mePos, enemiesCenter, xzOff);
            if (xzOff.sqrMagnitude <= 0.001f)
            {
                xzOff = desiredOrbit;
            }
            else
            {
                xzOff = Vector3.Slerp(xzOff.normalized, desiredOrbit, autoRotateSpeed * 0.15f * Time.deltaTime);
            }
            xzOff.y = 0;
        }
        
        ePosX = enemyScreenPos.x / Screen.width;
        ePosY = enemyScreenPos.y / Screen.height;
        mPosX = meScreenPos.x / Screen.width;
        mPosY = meScreenPos.y / Screen.height;
        
        if (ePosX >= 0.3 && ePosX <= 0.7 &&
            mPosX >= 0.3 && mPosX <= 0.7 &&
            ePosY >= 0.3 && ePosY <= 0.7 &&
            mPosY >= 0.3 && mPosY <= 0.7)
        {
            XZDistance -= _changeSpeed;
            YDistance -= _changeSpeed;
        }
        else if (ePosX <= 0.2 || ePosX >= 0.8 || 
                 mPosX <= 0.2 || mPosX >= 0.8 || 
                 ePosY <= 0.2 || ePosY >= 0.8 || 
                 mPosY <= 0.2 || mPosY >= 0.8)
        {
            XZDistance += _changeSpeed;
            YDistance += _changeSpeed;
        }
        
        lookPoint = Vector3.Lerp(mePos, enemiesCenter, hasTargets ? LookPointEnemyBias : 0.5f);
        cameraTargetPos = lookPoint + xzOff.normalized * XZDistance;
        cameraTargetPos.y = YDistance;
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
