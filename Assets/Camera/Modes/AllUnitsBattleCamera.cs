using System.Collections.Generic;
using FightScene;
using UnityEngine;

/// <summary>Keeps every live, fielded model in view while retaining a controllable battle orbit.</summary>
public class AllUnitsBattleCamera : CameraMode
{
    readonly float _pitch;
    readonly float _minimumDistance;
    readonly List<Bounds> _bounds = new List<Bounds>(200);
    readonly Dictionary<Data_Center, Renderer[]> _renderers = new Dictionary<Data_Center, Renderer[]>();
    readonly Vector3[] _corners = new Vector3[4];
    FightingStepLayer _hud;
    Vector3 _center;
    Vector3 _centerVelocity;
    float _yaw;
    float _distance;
    bool _initialized;

    public float Pitch => _pitch;
    public bool IsFramingInitialized => _initialized;
    public int FramedUnitCount => _bounds.Count;
    public BattleCameraFraming.Pose DesiredPose { get; private set; }
    public BattleCameraFraming.Pose CurrentPose { get; private set; }

    public bool CanSetH { get; set; } = true;
    public bool AutoRotateCamera
    {
        get => PlayerPrefs.GetInt("AutoRotateCamera", 1) == 1;
        set { PlayerPrefs.SetInt("AutoRotateCamera", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public AllUnitsBattleCamera(float pitch, float fov, float minimumDistance = 6)
    {
        _pitch = pitch;
        fieldOfView = fov;
        _minimumDistance = minimumDistance;
    }

    public AllUnitsBattleCamera(BattleCameraProfile profile)
        : this(profile.Pitch, profile.FieldOfView, profile.MinimumDistance) { }

    public override void Enter(Camera camera)
    {
        ApplyFieldOfView(camera, fieldOfView);
        CanSetH = true;
        _initialized = false;
        _centerVelocity = Vector3.zero;
        _renderers.Clear();
        _hud = Object.FindFirstObjectByType<FightingStepLayer>(FindObjectsInactive.Include);
        if (camera != null) _yaw = camera.transform.eulerAngles.y;
        UpdateCamera(camera, 0);
    }

    public override void Exit(Camera camera)
    {
        _renderers.Clear();
        _hud = null;
        _initialized = false;
    }

    public override void LocalUpdate(Camera camera) => UpdateCamera(camera, Time.deltaTime);

    void UpdateCamera(Camera camera, float deltaTime)
    {
        if (camera == null) return;
        var manager = RTFightManager.Target;
        if (manager == null) return;
        _bounds.Clear();
        if (FSceneProcessesRunner.Main.currentProcess is PreparingProcess)
        {
            // HUD setup may select a camera while models are still in remote staging.
            // Initialize from the final fielded models on the first CountDown frame.
            _initialized = false;
            _centerVelocity = Vector3.zero;
            return;
        }
        AddTeam(manager.team1);
        AddTeam(manager.team2);
        if (_bounds.Count == 0) return;

        var usable = UsableViewport(camera);
        float horizontal = CanSetH ? UltimateJoystick.GetHorizontalAxis("RotateCamera") : 0;
        if (Mathf.Abs(horizontal) > 0.01f)
            _yaw += horizontal * 75 * deltaTime;
        else if (manager.team1.TeamMode == TeamMode.Rotation && AutoRotateCamera)
        {
            var first = manager.team1.GetRModeUnitT();
            var second = manager.team2.GetRModeUnitT();
            if (first != null && second != null)
            {
                var orbit = GetDesiredOrbitDirection(first.position, second.position,
                    -(Quaternion.Euler(0, _yaw, 0) * Vector3.forward));
                if ((first.position - second.position).sqrMagnitude > 6.25f)
                {
                    float desiredYaw = Mathf.Atan2(-orbit.x, -orbit.z) * Mathf.Rad2Deg;
                    _yaw = Mathf.MoveTowardsAngle(_yaw, desiredYaw, deltaTime * 25);
                }
            }
        }

        var desired = BattleCameraFraming.CalculatePose(_bounds, camera.aspect, fieldOfView, usable,
            _pitch, _yaw, _minimumDistance, camera.nearClipPlane);
        DesiredPose = desired;
        if (!_initialized)
        {
            _center = desired.Center;
            _distance = desired.Distance;
            _initialized = true;
        }
        else if (deltaTime > 0)
            _center = Vector3.SmoothDamp(_center, desired.Center, ref _centerVelocity, 0.22f,
                Mathf.Infinity, deltaTime);

        // Solve again against the smoothed center: pan lag and camera rotation
        // must never cut off a previously visible teammate.
        var fitted = BattleCameraFraming.CalculatePoseAtCenter(_bounds, camera.aspect, fieldOfView, usable,
            desired.Rotation, _center, _minimumDistance, camera.nearClipPlane);
        _distance = BattleCameraFraming.SmoothDistance(_distance, fitted.Distance, deltaTime);
        fitted = BattleCameraFraming.WithDistance(fitted, _distance, camera.aspect, fieldOfView);
        CurrentPose = fitted;
        camera.transform.SetPositionAndRotation(fitted.Position, fitted.Rotation);
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, _distance + 100);
    }

    void AddTeam(UnitsManger team)
    {
        if (team?.teamMembers?.mDict == null) return;
        foreach (var unit in team.teamMembers.mDict.Values)
        {
            if (unit == null || unit.WholeT == null) continue;
            if (!BattleCameraFraming.ShouldIncludeUnit(team.TeamMode,
                unit.gameObject.activeInHierarchy && unit.WholeT.gameObject.activeInHierarchy,
                unit.FightDataRef.IsDead.Value, team.RMode_Unit.Value == unit)) continue;
            if (!_renderers.TryGetValue(unit, out var renderers))
            {
                renderers = unit.WholeT.GetComponentsInChildren<Renderer>(true);
                _renderers[unit] = renderers;
            }
            if (!BattleCameraFraming.TryGetModelBounds(renderers, out var box))
            {
                // A model not yet rendered still gets a scale-aware body envelope.
                var size = Vector3.Scale(new Vector3(1, 2.8f, 1), Abs(unit.WholeT.lossyScale));
                box = new Bounds(unit.WholeT.position + Vector3.up * size.y * 0.5f, size);
            }
            box.Expand(new Vector3(0.25f, 0.5f, 0.25f));
            _bounds.Add(box);
        }
    }

    Rect UsableViewport(Camera camera)
    {
        var safe = ScreenRectToViewport(Screen.safeArea, camera);
        var middle = safe;
        var rail = new Rect();
        bool excludeRail = false;
        if (_hud == null) _hud = Object.FindFirstObjectByType<FightingStepLayer>(FindObjectsInactive.Include);
        if (_hud != null)
        {
            if (_hud.MiddleArea != null) middle = UIViewport(_hud.MiddleArea, camera);
            var portraits = _hud.Team1UI?.SideIconsContainer;
            excludeRail = FightLoad.Fight != null && !FightLoad.Fight.IsGroupBattle
                && portraits != null && portraits.gameObject.activeInHierarchy;
            if (excludeRail) rail = UIViewport(portraits, camera);
        }
        return BattleCameraFraming.CalculateUsableViewport(safe, middle, rail, excludeRail);
    }

    public Rect GetUsableViewport(Camera camera) => UsableViewport(camera);

    Rect UIViewport(RectTransform rect, Camera camera)
    {
        rect.GetWorldCorners(_corners);
        var canvas = rect.GetComponentInParent<Canvas>();
        var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var min = RectTransformUtility.WorldToScreenPoint(uiCamera, _corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(uiCamera, _corners[2]);
        return ScreenRectToViewport(Rect.MinMaxRect(min.x, min.y, max.x, max.y), camera);
    }

    static Rect ScreenRectToViewport(Rect pixels, Camera camera)
    {
        var viewport = camera.pixelRect;
        return new Rect((pixels.x - viewport.x) / Mathf.Max(1, viewport.width),
            (pixels.y - viewport.y) / Mathf.Max(1, viewport.height),
            pixels.width / Mathf.Max(1, viewport.width), pixels.height / Mathf.Max(1, viewport.height));
    }

    static Vector3 Abs(Vector3 vector) => new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
}
