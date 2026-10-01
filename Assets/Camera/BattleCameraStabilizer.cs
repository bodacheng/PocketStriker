using System.Collections.Generic;
using UnityEngine;

/// <summary>Filters battle composition while retaining an immediate full-model safety floor.</summary>
public sealed class BattleCameraStabilizer
{
    const float CenterDeadZone = 0.02f;
    const float YawDeadZone = 8f;
    const float ZoomHysteresis = 0.06f;
    readonly BattleCameraProfile _profile;
    Vector3 _center, _centerVelocity;
    float _heightVelocity, _yawVelocity, _distanceVelocity;
    float _distance, _zoomTarget, _peakDistance, _peakAge;

    public float Yaw { get; private set; }
    public bool IsInitialized { get; private set; }
    public BattleCameraFraming.Pose DesiredPose { get; private set; }
    public BattleCameraFraming.Pose CurrentPose { get; private set; }

    public BattleCameraStabilizer(BattleCameraProfile profile) => _profile = profile;

    public void Reset(float yaw = 0)
    {
        Yaw = yaw;
        IsInitialized = false;
        _centerVelocity = Vector3.zero;
        _heightVelocity = _yawVelocity = _distanceVelocity = 0;
        _distance = _zoomTarget = _peakDistance = _peakAge = 0;
        DesiredPose = CurrentPose = default;
    }

    public float UpdateYaw(float targetYaw, float deltaTime, bool manual = false)
    {
        float step = Mathf.Clamp(deltaTime, 0, 0.05f);
        if (step <= 0) return Yaw;
        if (manual)
        {
            Yaw = targetYaw;
            _yawVelocity = 0;
        }
        else
        {
            float delta = Mathf.DeltaAngle(Yaw, targetYaw);
            float filteredTarget = Yaw + Mathf.Sign(delta) * Mathf.Max(0, Mathf.Abs(delta) - YawDeadZone);
            Yaw = Mathf.SmoothDampAngle(Yaw, filteredTarget, ref _yawVelocity, 0.65f, 20f, step);
        }
        return Yaw;
    }

    public BattleCameraFraming.Pose Update(IReadOnlyList<Bounds> rawBounds, Vector3 trackingCenter,
        float aspect, float fieldOfView, Rect usable, float nearClip, float deltaTime, bool holdFraming = false,
        IReadOnlyList<BattleCameraFraming.BodyEnvelope> bodyEnvelopes = null)
    {
        float step = Mathf.Clamp(deltaTime, 0, 0.05f);
        var rotation = Quaternion.Euler(_profile.Pitch, Yaw, 0);
        var desired = BattleCameraFraming.CalculatePoseAtCenter(rawBounds, aspect, fieldOfView, usable,
            rotation, trackingCenter, _profile.MinimumDistance, nearClip, bodyEnvelopes);
        DesiredPose = WithReserve(desired, aspect, fieldOfView);
        if (!IsInitialized)
        {
            _center = trackingCenter;
            _distance = _zoomTarget = _peakDistance = DesiredPose.Distance;
            IsInitialized = true;
        }
        else if (holdFraming)
        {
            _centerVelocity = Vector3.zero;
            _heightVelocity = _distanceVelocity = 0;
        }
        else if (step > 0)
        {
            // Subpixel animation, root punch and physics steps stay inside this
            // composition window instead of moving the entire environment.
            float halfHeight = _distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalZone = Mathf.Max(0.08f, 2 * halfHeight * aspect * usable.width * CenterDeadZone);
            float verticalZone = Mathf.Max(0.12f, 2 * halfHeight * usable.height * CenterDeadZone);
            var delta = trackingCenter - _center;
            var horizontal = new Vector3(delta.x, 0, delta.z);
            var target = _center + horizontal.normalized * Mathf.Max(0, horizontal.magnitude - horizontalZone);
            // Strong knockback/running must not leave the center far behind,
            // forcing the safety floor to compensate with an excessive zoom.
            // Contact punches remain below this screen-relative threshold.
            float catchUpThreshold = Mathf.Max(0.8f, horizontalZone * 4);
            float catchUp = Mathf.InverseLerp(catchUpThreshold, catchUpThreshold * 2, horizontal.magnitude);
            float horizontalSmoothTime = Mathf.Lerp(_profile.CenterSmoothTime, 0.12f, catchUp);
            float y = Mathf.SmoothDamp(_center.y,
                _center.y + Mathf.Sign(delta.y) * Mathf.Max(0, Mathf.Abs(delta.y) - verticalZone),
                ref _heightVelocity, Mathf.Max(0.45f, _profile.CenterSmoothTime), Mathf.Infinity, step);
            _center = Vector3.SmoothDamp(_center, target, ref _centerVelocity,
                horizontalSmoothTime, Mathf.Infinity, step);
            _center.y = y;
            _centerVelocity.y = 0;
        }

        // Fit against the filtered center and heading, using the unfiltered live
        // geometry. Padding usually absorbs attacks; true separation still gets
        // an immediate minimum distance so no fighter is clipped for a frame.
        var fitted = BattleCameraFraming.CalculatePoseAtCenter(rawBounds, aspect, fieldOfView, usable,
            rotation, _center, _profile.MinimumDistance, nearClip);
        var envelopeFit = bodyEnvelopes == null ? fitted
            : BattleCameraFraming.CalculatePoseAtCenter(null, aspect, fieldOfView, usable,
                rotation, _center, _profile.MinimumDistance, nearClip, bodyEnvelopes);
        float reserved = Mathf.Max(fitted.Distance, envelopeFit.Distance) * (1 + _profile.FramingReserve);
        if (holdFraming)
            _distance = Mathf.Max(_distance, fitted.Distance);
        else if (step > 0)
        {
            if (reserved >= _peakDistance * 0.995f)
            {
                _peakDistance = Mathf.Max(_peakDistance, reserved);
                _peakAge = 0;
            }
            else
            {
                _peakAge += step;
                if (_peakAge >= _profile.ZoomHoldTime) _peakDistance = reserved;
            }
            if (_peakDistance > _zoomTarget || _peakDistance < _zoomTarget * (1 - ZoomHysteresis))
                _zoomTarget = _peakDistance;
            if (_zoomTarget > _distance)
                _distance = Mathf.SmoothDamp(_distance, _zoomTarget, ref _distanceVelocity, 0.24f,
                    Mathf.Infinity, step);
            else
            {
                _distanceVelocity = 0;
                _distance = BattleCameraFraming.SmoothDistance(_distance, _zoomTarget, step, _profile.DistanceSmoothTime);
            }
        }
        // Resizing the viewport or switching a visible model while paused can
        // require more space even though ordinary tracking remains frozen.
        if (_distance < fitted.Distance)
        {
            _distance = fitted.Distance;
            _distanceVelocity = 0;
        }
        CurrentPose = BattleCameraFraming.WithDistance(fitted, _distance, aspect, fieldOfView);
        return CurrentPose;
    }

    BattleCameraFraming.Pose WithReserve(BattleCameraFraming.Pose pose, float aspect, float fov) =>
        BattleCameraFraming.WithDistance(pose, pose.Distance * (1 + _profile.FramingReserve), aspect, fov);
}
