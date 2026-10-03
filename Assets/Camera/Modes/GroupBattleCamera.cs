using System.Collections.Generic;
using FightScene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A fixed full-arena view with touch-controlled horizontal orbit.</summary>
public sealed class GroupBattleCamera : BattleCameraMode
{
    public const float ArenaEdgeReserve = .1f;
    public const float ArenaHeightReserve = 5;
    // Visible attack poses can exceed the physical standing-body height.
    public const float StandingHeightReserve = 5f;
    const float SideInset = .006f;
    const int MouseFingerId = -1;

    readonly List<RaycastResult> _uiHits = new List<RaycastResult>(16);
    PointerEventData _pointer;
    EventSystem _pointerEvents;
    BattleCameraFraming.Pose _pose;
    Rect _lastPixelRect;
    bool _initialized;
    bool _canSetH = true;

    public Vector3 ArenaCenter { get; private set; }
    public float ArenaRadius { get; private set; }
    public float Yaw { get; private set; }
    public BattleArenaOrbitGesture OrbitGesture { get; } = new BattleArenaOrbitGesture();
    public override float Pitch => BattleCameraProfiles.Group.Pitch;
    public override bool IsFramingInitialized => _initialized;
    public override bool IsHoldingReplacementFraming { get; protected set; }
    public override bool IsSettlingReplacementFraming => false;
    public override int FramedUnitCount => 0;
    public override float ShadowReceiverDistance { get; protected set; }
    public override BattleCameraFraming.Pose DesiredPose => _pose;
    public override BattleCameraFraming.Pose CurrentPose => _pose;
    public override bool CanSetH
    {
        get => _canSetH;
        set
        {
            _canSetH = value;
            if (!value) OrbitGesture.Reset();
        }
    }

    public override Rect GetUsableViewport(Camera camera)
    {
        var viewport = base.GetUsableViewport(camera);
        // Group has no portrait rail. Use the safe area's full width and keep
        // only a narrow rim instead of the ordinary fighter-framing margin.
        var pixels = camera.pixelRect;
        var safe = Screen.safeArea;
        viewport.xMin = Mathf.Clamp01((safe.xMin - pixels.xMin) / Mathf.Max(1, pixels.width)) + SideInset;
        viewport.xMax = Mathf.Clamp01((safe.xMax - pixels.xMin) / Mathf.Max(1, pixels.width)) - SideInset;
        return BattleCameraFraming.ClampViewport(viewport);
    }

    public GroupBattleCamera() => fieldOfView = BattleCameraProfiles.Group.FieldOfView;

    public override void Enter(Camera camera)
    {
        CanSetH = true;
        _initialized = false;
        OrbitGesture.Reset();
        ResetUsableViewport();
        // The battle ring and physics sensor use world origin, independently
        // of the map prefab's authored decorative offset and every unit target.
        ArenaCenter = Vector3.zero;
        Yaw = cameraManager != null && cameraManager.TopDownModeEndRef != null
            ? cameraManager.TopDownModeEndRef.eulerAngles.y : 0;
        ApplyFieldOfView(camera, fieldOfView);
        UpdatePose(camera);
    }

    public override void Exit(Camera camera)
    {
        OrbitGesture.Reset();
        _initialized = false;
        ShadowReceiverDistance = 0;
        ReleaseHUD();
        _pointer = null;
        _pointerEvents = null;
        _uiHits.Clear();
    }

    public override void LocalUpdate(Camera camera)
    {
        if (camera == null) return;
        if (camera.pixelRect != _lastPixelRect) OrbitGesture.Reset();
        var hud = HUD;
        bool canOrbit = CanSetH && Time.timeScale > 0 && Application.isFocused
            && FSceneProcessesRunner.Main.currentProcess is FightingProcess
            && hud != null && hud.gameObject.activeInHierarchy
            && !FightLogger.value.GameOver.Value;
        if (canOrbit) Yaw = Mathf.Repeat(Yaw + ReadOrbitInput(camera), 360);
        else OrbitGesture.Reset();
        UpdatePose(camera);
    }

    void UpdatePose(Camera camera)
    {
        if (camera == null) return;
        var boundary = BoundaryControlByGod.target;
        ArenaRadius = Mathf.Max(.1f, boundary != null ? boundary.EffectiveBattleRadius
            : BoundaryControlByGod._BattleRingRadius > 0 ? BoundaryControlByGod._BattleRingRadius : 20);
        _pose = CalculateArenaPose(ArenaCenter, ArenaRadius, camera.aspect,
            GetUsableViewport(camera), Yaw, camera.nearClipPlane);
        camera.transform.SetPositionAndRotation(_pose.Position, _pose.Rotation);
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, _pose.Distance + ArenaRadius + 100);
        // Keep shadows across the complete arena even after most fighters die.
        float horizontal = new Vector2(_pose.Position.x - ArenaCenter.x, _pose.Position.z - ArenaCenter.z).magnitude
            + ArenaRadius + ArenaEdgeReserve;
        float vertical = Mathf.Abs(_pose.Position.y - ArenaCenter.y);
        ShadowReceiverDistance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical) + 2;
        _lastPixelRect = camera.pixelRect;
        _initialized = true;
    }

    public static BattleCameraFraming.Pose CalculateArenaPose(Vector3 center, float radius, float aspect,
        Rect usableViewport, float yaw = 0, float nearClip = .3f)
    {
        var profile = BattleCameraProfiles.Group;
        var viewport = BattleCameraFraming.ClampViewport(usableViewport);
        var rotation = Quaternion.Euler(profile.Pitch, yaw, 0);
        var right = rotation * Vector3.right;
        var up = rotation * Vector3.up;
        var forward = rotation * Vector3.forward;
        float tanVertical = Mathf.Tan(profile.FieldOfView * .5f * Mathf.Deg2Rad);
        float tanHorizontal = tanVertical * Mathf.Max(.1f, aspect);
        float offsetX = viewport.center.x * 2 - 1;
        float offsetY = viewport.center.y * 2 - 1;
        float extent = Mathf.Max(.1f, radius) + ArenaEdgeReserve;
        float distance = profile.MinimumDistance;
        // Fit the ground circle tightly to the side margins. A five-unit-tall
        // cylinder at those same margins adds needless horizontal empty space.
        distance = Mathf.Max(distance, CylinderSupport(right / (viewport.width * tanHorizontal)
            - forward * (1 + offsetX / viewport.width), extent, 0));
        distance = Mathf.Max(distance, CylinderSupport(-right / (viewport.width * tanHorizontal)
            - forward * (1 - offsetX / viewport.width), extent, 0));
        // Standing bodies can use the narrow rim around the ring, but still
        // remain inside the camera. Keep extra vertical room for tall attacks.
        distance = Mathf.Max(distance, CylinderSupport((right / tanHorizontal - forward)
            / Mathf.Max(.01f, 1 - offsetX), extent, StandingHeightReserve));
        distance = Mathf.Max(distance, CylinderSupport((-right / tanHorizontal - forward)
            / Mathf.Max(.01f, 1 + offsetX), extent, StandingHeightReserve));
        distance = Mathf.Max(distance, CylinderSupport(up / (viewport.height * tanVertical)
            - forward * (1 + offsetY / viewport.height), extent, ArenaHeightReserve));
        distance = Mathf.Max(distance, CylinderSupport(-up / (viewport.height * tanVertical)
            - forward * (1 - offsetY / viewport.height), extent, ArenaHeightReserve));
        distance = Mathf.Max(distance, CylinderSupport(-forward, extent, ArenaHeightReserve) + nearClip + .1f);
        return BattleCameraFraming.WithDistance(new BattleCameraFraming.Pose
            { Center = center, Rotation = rotation, Viewport = viewport }, distance + .02f, aspect, profile.FieldOfView);
    }

    static float CylinderSupport(Vector3 direction, float radius, float height)
        => radius * new Vector2(direction.x, direction.z).magnitude + height * Mathf.Max(0, direction.y);

    float ReadOrbitInput(Camera camera)
    {
        float delta = 0;
        if (Input.touchCount > 0)
        {
            if (OrbitGesture.ActiveFingerId == MouseFingerId) OrbitGesture.Reset();
            bool ownerPresent = false;
            for (int index = 0; index < Input.touchCount; index++)
            {
                var touch = Input.GetTouch(index);
                bool blocked = IsPointerBlocked(touch.position, touch.fingerId, camera);
                delta += OrbitGesture.UpdatePointer(touch.fingerId, touch.phase, touch.position, blocked, camera.pixelWidth);
                ownerPresent |= OrbitGesture.ActiveFingerId == touch.fingerId;
            }
            if (!ownerPresent) OrbitGesture.Reset();
            return delta;
        }
        if (OrbitGesture.ActiveFingerId.HasValue && OrbitGesture.ActiveFingerId != MouseFingerId) OrbitGesture.Reset();
        if (Input.GetMouseButtonDown(0))
            return OrbitGesture.UpdatePointer(MouseFingerId, TouchPhase.Began, Input.mousePosition,
                IsPointerBlocked(Input.mousePosition, MouseFingerId, camera), camera.pixelWidth);
        if (Input.GetMouseButton(0))
            return OrbitGesture.UpdatePointer(MouseFingerId, TouchPhase.Moved, Input.mousePosition,
                IsPointerBlocked(Input.mousePosition, MouseFingerId, camera), camera.pixelWidth);
        OrbitGesture.Reset();
        return 0;
    }

    bool IsPointerBlocked(Vector2 position, int fingerId, Camera camera)
    {
        if (!camera.pixelRect.Contains(position)) return true;
        var events = EventSystem.current;
        if (events == null) return false;
        if (_pointer == null || _pointerEvents != events)
        {
            _pointer = new PointerEventData(events);
            _pointerEvents = events;
        }
        _pointer.Reset();
        _pointer.pointerId = fingerId;
        _pointer.position = position;
        _uiHits.Clear();
        events.RaycastAll(_pointer, _uiHits);
        foreach (var hit in _uiHits)
            if (hit.module is GraphicRaycaster && IsOrbitBlockingUI(hit.gameObject)) return true;
        return false;
    }

    public static bool IsOrbitBlockingUI(GameObject hit)
    {
        if (hit == null) return false;
        var joystick = hit.GetComponentInParent<UltimateJoystick>();
        // The existing transparent, full-screen RotateCamera pad is the
        // battle's drag surface; it must not swallow all arena orbit gestures.
        if (joystick != null) return joystick.joystickName != "RotateCamera";
        if (hit.GetComponentInParent<Selectable>() != null
            || ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit) != null
            || ExecuteEvents.GetEventHandler<IDragHandler>(hit) != null) return true;
        // Passive HUD/canvas backgrounds are allowed. Other UI layers are
        // modal and retain their gestures, including their empty backdrops.
        var layer = hit.GetComponentInParent<UILayer>();
        return layer != null && layer is not FightingStepLayer;
    }
}
