using UnityEngine;

class ChatGptFix : CameraMode
{
    // PocketStriker tuning for MCombat's midpoint/orbit camera.
    const float LookPointHeight = 1.5f;
    const float FollowSmoothTime = 0.4f;
    const float EntryDuration = 1f;
    const float FocusDeadZone = 0.12f;
    const float MaxFocusLag = 0.6f;
    const float AutoRotateStartAngle = 40f;
    const float AutoRotateStopAngle = 20f;
    const float AutoRotateSpeed = 35f;
    const float AutoRotateSmoothTime = 0.65f;
    const float AutoRotateMinSeparation = 2.5f;
    const float ManualRotateSpeed = 90f;
    const float ZoomOutSmoothTime = 0.55f;
    const float ZoomInSmoothTime = 2f;
    const float ZoomOutMaxSpeed = 12f;
    const float ZoomInMaxSpeed = 3f;
    const float ZoomInDelay = 0.65f;
    const float ZoomDeadZone = 0.75f;
    const float FramingExtent = 0.8f;
    const float SafetyFramingExtent = 0.95f;
    const float ReferenceAspect = 9f / 16f;
    const float FighterHalfWidth = 0.4f;
    const float FighterHalfHeight = 1.5f;

    readonly float _minXZ;
    readonly float _heightRatio;
    Vector3 xzOff;
    Vector3 lookPoint;
    Vector3 _focusVelocity;
    Vector3 _entryOffset;
    Quaternion _entryRotation;
    float _entryElapsed;
    float _autoRotateVelocity;
    float _zoomVelocity;
    float _framingDistance;
    float _zoomInTimer;
    bool _autoRotating;

    public bool AutoRotateCamera
    {
        get => PlayerPrefs.GetInt("AutoRotateCamera", 1) == 1;
        set
        {
            PlayerPrefs.SetInt("AutoRotateCamera", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public ChatGptFix(float XZDis, float YDis, float fieldOfView)
    {
        _minXZ = XZDis;
        _heightRatio = (YDis - LookPointHeight) / XZDis;
        this.XZDis = XZDis;
        this.YDis = YDis;
        this.fieldOfView = fieldOfView;
    }

    public bool CanSetH { get; set; }

    public override void Enter(Camera camera)
    {
        CanSetH = true;
        _focusVelocity = Vector3.zero;
        _entryElapsed = 0f;
        _autoRotateVelocity = 0f;
        _zoomVelocity = 0f;
        _zoomInTimer = 0f;
        _autoRotating = false;
        ApplyFieldOfView(camera, fieldOfView);
        if (camera == null)
            return;

        _entryRotation = camera.transform.rotation;

        bool hasTargets = TryGetAveragePosition(targets, out var enemiesCenter);
        lookPoint = meCenter != null
            ? (hasTargets ? (meCenter.position + enemiesCenter) * 0.5f : meCenter.position)
            : (hasTargets ? enemiesCenter : camera.transform.position);
        lookPoint.y = LookPointHeight;
        _entryOffset = camera.transform.position - lookPoint;

        // Initialize before updating, including when a replacement fighter re-enters this mode.
        xzOff = camera.transform.position - lookPoint;
        xzOff.y = 0f;
        XZDis = Mathf.Max(GetMinimumDistance(camera), xzOff.magnitude);
        _framingDistance = GetMinimumDistance(camera);
        if (xzOff.sqrMagnitude < 0.0001f)
        {
            xzOff = -camera.transform.forward;
            xzOff.y = 0f;
        }
        xzOff = xzOff.sqrMagnitude > 0.0001f ? xzOff.normalized : -Vector3.forward;
    }

    public override void LocalUpdate(Camera camera)
    {
        UpdateCamera(camera, Time.deltaTime);
    }

    void UpdateCamera(Camera camera, float deltaTime)
    {
        if (camera == null || deltaTime <= 0f)
            return;

        bool hasTargets = TryGetAveragePosition(targets, out var enemiesCenter);
        if (meCenter == null && !hasTargets)
            return;

        var mePos = meCenter != null ? meCenter.position : enemiesCenter;
        if (!hasTargets)
            enemiesCenter = mePos;

        var midpoint = (mePos + enemiesCenter) * 0.5f;
        midpoint.y = LookPointHeight;
        var focusDelta = midpoint - lookPoint;
        if (focusDelta.magnitude > FocusDeadZone)
        {
            var focusTarget = midpoint - focusDelta.normalized * FocusDeadZone;
            lookPoint = Vector3.SmoothDamp(lookPoint, focusTarget, ref _focusVelocity,
                FollowSmoothTime, Mathf.Infinity, deltaTime);
        }
        else
        {
            _focusVelocity = Vector3.zero;
        }
        // Keep normal-speed chases visible without using zoom to compensate for pan lag.
        lookPoint = midpoint + Vector3.ClampMagnitude(lookPoint - midpoint, MaxFocusLag);

        float h = CanSetH ? UltimateJoystick.GetHorizontalAxis("RotateCamera") : 0f;
        if (Mathf.Abs(h) > 0.01f)
        {
            xzOff = Quaternion.AngleAxis(h * ManualRotateSpeed * deltaTime, Vector3.up) * xzOff;
            _autoRotating = false;
            _autoRotateVelocity = 0f;
        }
        else
        {
            UpdateAutoRotation(camera, mePos, enemiesCenter, hasTargets && meCenter != null, deltaTime);
        }

        // Fit the pair around its desired midpoint, not its position in the lagging camera.
        // Running together or a hit reaction must not continually push the camera farther away.
        float requiredDistance = GetFramingDistance(camera, mePos, enemiesCenter, midpoint, FramingExtent);
        if (requiredDistance >= _framingDistance)
        {
            _framingDistance = requiredDistance;
            _zoomInTimer = 0f;
        }
        else if (requiredDistance < _framingDistance - ZoomDeadZone)
        {
            _zoomInTimer += deltaTime;
            if (_zoomInTimer >= ZoomInDelay)
                _framingDistance = requiredDistance;
        }
        else
        {
            _zoomInTimer = 0f;
        }

        bool zoomingOut = _framingDistance > XZDis;
        XZDis = Mathf.SmoothDamp(XZDis, _framingDistance, ref _zoomVelocity,
            zoomingOut ? ZoomOutSmoothTime : ZoomInSmoothTime,
            zoomingOut ? ZoomOutMaxSpeed : ZoomInMaxSpeed, deltaTime);
        // Only override outward damping if fast separation would otherwise lose a fighter.
        float safetyDistance = GetFramingDistance(camera, mePos, enemiesCenter, lookPoint, SafetyFramingExtent);
        if (XZDis < safetyDistance)
        {
            XZDis = safetyDistance;
            _zoomVelocity = Mathf.Max(0f, _zoomVelocity);
        }
        YDis = LookPointHeight + XZDis * _heightRatio;

        // Each channel is already damped; damping the full position again delays outward zoom.
        var cameraTargetPos = lookPoint + xzOff * XZDis;
        cameraTargetPos.y = YDis;
        var targetRotation = Quaternion.LookRotation(lookPoint - cameraTargetPos, Vector3.up);
        _entryElapsed += deltaTime;
        float entryBlend = Mathf.SmoothStep(0f, 1f, _entryElapsed / EntryDuration);
        // Follow the moving midpoint during entry as well, including active-fighter changes.
        camera.transform.position = lookPoint + Vector3.Lerp(_entryOffset, cameraTargetPos - lookPoint, entryBlend);
        camera.transform.rotation = Quaternion.Slerp(_entryRotation, targetRotation, entryBlend);
    }

    void UpdateAutoRotation(Camera camera, Vector3 mePos, Vector3 enemiesCenter, bool hasPair, float deltaTime)
    {
        var combatLine = enemiesCenter - mePos;
        combatLine.y = 0f;
        if (!AutoRotateCamera || !hasPair || combatLine.sqrMagnitude < AutoRotateMinSeparation * AutoRotateMinSeparation)
        {
            _autoRotating = false;
            _autoRotateVelocity = 0f;
            return;
        }

        var enemyViewport = camera.WorldToViewportPoint(enemiesCenter);
        var meViewport = camera.WorldToViewportPoint(mePos);
        var screenDiff = new Vector2((enemyViewport.x - meViewport.x) * camera.aspect,
            enemyViewport.y - meViewport.y);
        float angle = Mathf.Abs(Vector2.SignedAngle(screenDiff, Vector2.right));
        if (angle > 90f)
            angle = 180f - angle;

        if (_autoRotating)
            _autoRotating = angle > AutoRotateStopAngle;
        else
            _autoRotating = angle >= AutoRotateStartAngle && Mathf.Abs(screenDiff.y) > 0.05f;

        if (!_autoRotating)
        {
            _autoRotateVelocity = 0f;
            return;
        }

        // Preserve the current side of the fight; close crossings should not flip the orbit.
        var desiredOrbit = GetDesiredOrbitDirection(mePos, enemiesCenter, xzOff);
        float currentYaw = Mathf.Atan2(xzOff.x, xzOff.z) * Mathf.Rad2Deg;
        float desiredYaw = Mathf.Atan2(desiredOrbit.x, desiredOrbit.z) * Mathf.Rad2Deg;
        float yaw = Mathf.SmoothDampAngle(currentYaw, desiredYaw, ref _autoRotateVelocity,
            AutoRotateSmoothTime, AutoRotateSpeed, deltaTime) * Mathf.Deg2Rad;
        xzOff = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    }

    float GetMinimumDistance(Camera camera)
    {
        // Preserve the comfortable horizontal framing on taller phones.
        return _minXZ * Mathf.Max(1f, ReferenceAspect / Mathf.Max(camera.aspect, 0.1f));
    }

    float GetFramingDistance(Camera camera, Vector3 mePos, Vector3 enemiesCenter, Vector3 midpoint, float extent)
    {
        var forward = -(xzOff + Vector3.up * _heightRatio).normalized;
        var right = Vector3.Cross(Vector3.up, forward).normalized;
        var up = Vector3.Cross(forward, right);
        float tanVertical = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * extent;
        float tanHorizontal = tanVertical * Mathf.Max(camera.aspect, 0.1f);
        float radialDistance = 0f;
        FitFighter(mePos);
        FitFighter(enemiesCenter);
        return Mathf.Max(GetMinimumDistance(camera), radialDistance / Mathf.Sqrt(1f + _heightRatio * _heightRatio));

        void FitFighter(Vector3 position)
        {
            // Targets are geometry centers, so reserve room both above and below them.
            FitPoint(position - Vector3.up * FighterHalfHeight - midpoint);
            FitPoint(position + Vector3.up * FighterHalfHeight - midpoint);
        }

        void FitPoint(Vector3 offset)
        {
            float depth = Vector3.Dot(offset, forward);
            float horizontal = (Mathf.Abs(Vector3.Dot(offset, right)) + FighterHalfWidth + FocusDeadZone) / tanHorizontal - depth;
            float vertical = Mathf.Abs(Vector3.Dot(offset, up)) / tanVertical - depth;
            radialDistance = Mathf.Max(radialDistance, Mathf.Max(horizontal, vertical));
        }
    }
}
