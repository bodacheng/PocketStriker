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
    Vector3 frontWPos, backWPos;
    Quaternion ToRotation;
    float autoChangeAngleLimit = 15f;
    float autoRotateSpeed = 360f;
    float _changeSpeed;
    readonly float _lookPointHeight = 1.5f;
    readonly float _minXZ;
    readonly float _minY;
    float fieldOfView;
    private float _transitionSpeedPara;
    private const float ScreenDiffThresholdSqr = 100f * 100f;
    private const float VerticalAlignmentMinYDiff = 0.03f;
    private const float VerticalAlignmentMaxXDiff = 0.25f;
    private const float AutoRotateMinSmoothTime = 0.05f;
    private const float AutoRotateMaxSmoothTime = 0.18f;
    private float _autoRotateVelocity;
    
    public bool AutoRotateCamera
    {
        get => PlayerPrefs.GetInt("AutoRotateCamera") == 1;
        set
        {
            PlayerPrefs.SetInt("AutoRotateCamera", value ? 1 : 0);
            PlayerPrefs.Save();
        }
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
    
    private float UsedHeight
    {
        get => YDis;
        set => YDis = Mathf.Clamp(value, _minY , _minY + 20f);
    }

    public override void Enter(Camera _camera)
    {
        CanSetH = true;
        ApplyFieldOfView(_camera, this.fieldOfView);
        if (_camera == null)
        {
            return;
        }

        LocalUpdate(_camera);
        xzOff = _camera.transform.position - lookPoint;
        xzOff.y = 0;
        _transitionSpeedPara = 0f;
        DOTween.To(()=> _transitionSpeedPara, (x) => _transitionSpeedPara = x, CommonSetting.CameraSpeed, 1f);
    }

    float h;
    private bool _canSetH;
    public bool CanSetH
    {
        get => _canSetH;
        set
        {
            _canSetH = value;
            h = 0;
        }
    }

    private Vector3 mePos;
    
    public override void LocalUpdate(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        if (meCenter != null)
        {
            mePos = meCenter.position;
        }

        _changeSpeed = _transitionSpeedPara * Time.deltaTime;
        var hasTargets = TryGetAveragePosition(targets, out enemiesCenter);
        if (!hasTargets)
        {
            var fallbackForward = meCenter != null ? meCenter.forward : camera.transform.forward;
            if (fallbackForward.sqrMagnitude <= 0.0001f)
            {
                fallbackForward = Vector3.forward;
            }

            enemiesCenter = mePos + fallbackForward.normalized * 10f;
        }
        
        enemyScreenPos = camera.WorldToScreenPoint(enemiesCenter);
        meScreenPos = camera.WorldToScreenPoint(mePos);
        float normalizedXDiff = Mathf.Abs(enemyScreenPos.x - meScreenPos.x) / Screen.width;
        float normalizedYDiff = Mathf.Abs(enemyScreenPos.y - meScreenPos.y) / Screen.height;
        Vector2 screenDiff = enemyScreenPos - meScreenPos;
        float angleToHorizontal = Mathf.Abs(Vector2.SignedAngle(screenDiff, Vector2.right));
        if (angleToHorizontal > 90f)
        {
            angleToHorizontal = 180f - angleToHorizontal;
        }
        float verticalityRatio = Mathf.Sqrt(Mathf.Clamp01(angleToHorizontal / 90f));
        float dynamicAutoRotateSpeed = autoRotateSpeed * verticalityRatio;
        
        if (CanSetH)
        {
            h = UltimateJoystick.GetHorizontalAxis("RotateCamera");
        }
        
        if (h != 0)
        {
            xzOff = Quaternion.AngleAxis(h * 1.5f, Vector3.up) * xzOff;
            xzOff.y = 0;
            _autoRotateVelocity = 0f;
        }
        else
        {
            if (AutoRotateCamera && hasTargets && meCenter != null && screenDiff.sqrMagnitude > ScreenDiffThresholdSqr)
            {
                if (ShouldRotateForVerticalAlignment(normalizedXDiff, normalizedYDiff, angleToHorizontal))
                {
                    Vector3 planarDiff = enemiesCenter - mePos;
                    planarDiff.y = 0f;
                    if (planarDiff.sqrMagnitude > 0.0001f)
                    {
                        Vector3 currentForward = camera.transform.forward;
                        currentForward.y = 0f;
                        if (currentForward.sqrMagnitude < 0.0001f)
                        {
                            currentForward = -(xzOff.sqrMagnitude > 0.0001f ? xzOff : planarDiff.normalized);
                        }
                        currentForward.Normalize();

                        Vector3 desiredForward = Vector3.Cross(Vector3.up, planarDiff).normalized;
                        if (desiredForward.sqrMagnitude < 0.0001f)
                        {
                            desiredForward = currentForward;
                        }
                        else
                        {
                            var alternativeForward = -desiredForward;
                            if (Vector3.Angle(currentForward, alternativeForward) < Vector3.Angle(currentForward, desiredForward))
                            {
                                desiredForward = alternativeForward;
                            }
                        }

                        float currentYaw = Mathf.Atan2(currentForward.x, currentForward.z) * Mathf.Rad2Deg;
                        float desiredYaw = Mathf.Atan2(desiredForward.x, desiredForward.z) * Mathf.Rad2Deg;
                        float smoothTime = Mathf.Lerp(AutoRotateMaxSmoothTime, AutoRotateMinSmoothTime, verticalityRatio);
                        float maxSpeed = Mathf.Max(dynamicAutoRotateSpeed, 0.01f);
                        float newYaw = Mathf.SmoothDampAngle(currentYaw, desiredYaw, ref _autoRotateVelocity, smoothTime, maxSpeed, Time.deltaTime);
                        Vector3 smoothedForward = new Vector3(Mathf.Sin(newYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(newYaw * Mathf.Deg2Rad));
                        if (smoothedForward.sqrMagnitude > 0.0001f)
                        {
                            smoothedForward.Normalize();
                            var newXZ = -smoothedForward;
                            newXZ.y = 0f;
                            if (newXZ.sqrMagnitude > 0.0001f)
                            {
                                xzOff = newXZ.normalized;
                            }
                        }
                    }
                    else
                    {
                        _autoRotateVelocity = 0f;
                    }
                }
                else
                {
                    _autoRotateVelocity = 0f;
                }
            }
            else
            {
                _autoRotateVelocity = 0f;
            }
        }
        
        float ePosX = enemyScreenPos.x / Screen.width;
        float ePosY = enemyScreenPos.y / Screen.height;
        float mPosX = meScreenPos.x / Screen.width;
        float mPosY = meScreenPos.y / Screen.height;
        
        if (ePosX >= 0.3f && ePosX <= 0.7f &&
            mPosX >= 0.3f && mPosX <= 0.7f &&
            ePosY >= 0.3f && ePosY <= 0.7f &&
            mPosY >= 0.3f && mPosY <= 0.7f)
        {
            XZDistance -= _changeSpeed;
            UsedHeight -= _changeSpeed;
        }
        else if (ePosX <= 0.2f || ePosX >= 0.8f ||
                 mPosX <= 0.2f || mPosX >= 0.8f ||
                 ePosY <= 0.2f || ePosY >= 0.8f ||
                 mPosY <= 0.2f || mPosY >= 0.8f)
        {
            XZDistance += _changeSpeed;
            UsedHeight += _changeSpeed;
        }
        
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

        cameraTargetPos = lookPoint + xzOff.normalized * XZDistance;
        cameraTargetPos.y = UsedHeight;
        
        if (hasTargets || meCenter != null || h != 0)
        {
            camera.transform.position = Vector3.Lerp(camera.transform.position, cameraTargetPos, _changeSpeed);

            lookPoint = (backWPos - frontWPos) * 0.5f + frontWPos;
            lookPoint.y = _lookPointHeight;

            rotateToDirection = lookPoint - cameraTargetPos;
            ToRotation = Quaternion.LookRotation(rotateToDirection.normalized);
            camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, ToRotation, _changeSpeed);
        }
    }

    private bool ShouldRotateForVerticalAlignment(float normalizedXDiff, float normalizedYDiff, float angleToHorizontal)
    {
        if (normalizedYDiff < VerticalAlignmentMinYDiff)
        {
            return false;
        }

        if (normalizedXDiff > VerticalAlignmentMaxXDiff)
        {
            return false;
        }

        return angleToHorizontal >= autoChangeAngleLimit;
    }
}
