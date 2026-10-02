using System.Collections.Generic;
using FightScene;
using UnityEngine;

/// <summary>Keeps every live, fielded model in view while retaining a controllable battle orbit.</summary>
public class AllUnitsBattleCamera : CameraMode
{
    readonly float _pitch;
    readonly BattleCameraProfile _profile;
    readonly BattleCameraStabilizer _stabilizer;
    readonly List<Bounds> _bounds = new List<Bounds>(200);
    readonly List<BattleCameraFraming.BodyEnvelope> _bodyEnvelopes = new List<BattleCameraFraming.BodyEnvelope>(200);
    readonly Dictionary<Data_Center, ModelFramingReference> _models = new Dictionary<Data_Center, ModelFramingReference>();
    readonly Vector3[] _corners = new Vector3[4];
    FightingStepLayer _hud;
    Bounds _trackingBounds;
    bool _hasTrackingBounds;
    bool _autoOrbitEngaged;
    Data_Center _lastFirst, _lastSecond;
    float _handoffRemaining;
    Rect _lastUsableViewport;
    bool _hasUsableViewport;

    sealed class ModelFramingReference
    {
        public Transform Root;
        public Renderer[] Renderers;
        public float CenterHeight;
        public float HorizontalRadius;
    }

    public float Pitch => _pitch;
    public bool IsFramingInitialized => _stabilizer.IsInitialized;
    public bool IsHoldingReplacementFraming { get; private set; }
    public bool IsSettlingReplacementFraming => _handoffRemaining > 0;
    public int FramedUnitCount => _bounds.Count;
    public BattleCameraFraming.Pose DesiredPose => _stabilizer.DesiredPose;
    public BattleCameraFraming.Pose CurrentPose => _stabilizer.CurrentPose;

    public bool CanSetH { get; set; } = true;
    public bool AutoRotateCamera
    {
        get => PlayerPrefs.GetInt("AutoRotateCamera", 1) == 1;
        set { PlayerPrefs.SetInt("AutoRotateCamera", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public AllUnitsBattleCamera(float pitch, float fov, float minimumDistance = 6,
        float centerSmoothTime = 0.32f, float distanceSmoothTime = 2f)
        : this(new BattleCameraProfile(pitch, fov, minimumDistance, centerSmoothTime, distanceSmoothTime)) { }

    public AllUnitsBattleCamera(BattleCameraProfile profile)
    {
        _profile = profile;
        _pitch = profile.Pitch;
        fieldOfView = profile.FieldOfView;
        _stabilizer = new BattleCameraStabilizer(profile);
    }

    public override void Enter(Camera camera)
    {
        ApplyFieldOfView(camera, fieldOfView);
        CanSetH = true;
        ResetFraming(camera);
        _hud = Object.FindFirstObjectByType<FightingStepLayer>(FindObjectsInactive.Include);
        UpdateCamera(camera, 0);
    }

    public override void Exit(Camera camera)
    {
        ResetFraming(camera);
        _hud = null;
    }

    public override void LocalUpdate(Camera camera) => UpdateCamera(camera, Time.deltaTime);

    void UpdateCamera(Camera camera, float deltaTime)
    {
        if (camera == null) return;
        var manager = RTFightManager.Target;
        if (manager == null) return;
        _bounds.Clear();
        _bodyEnvelopes.Clear();
        _hasTrackingBounds = false;
        if (FSceneProcessesRunner.Main.currentProcess is PreparingProcess)
        {
            // HUD setup may select a camera while models are still in remote staging.
            // Initialize from the final fielded models on the first CountDown frame.
            ResetFraming(camera);
            return;
        }
        bool wasHolding = IsHoldingReplacementFraming;
        var firstUnit = manager.team1?.RMode_Unit.Value;
        var secondUnit = manager.team2?.RMode_Unit.Value;
        // Preserve both sides until an actual replacement is active. The final
        // knockout uses the same hold until the result process owns the camera;
        // the existence of a reserve must not decide whether a body is abandoned.
        IsHoldingReplacementFraming = _stabilizer.IsInitialized && this is DuelBattleCamera
            && (!HasLiveRotationFighter(manager.team1) || !HasLiveRotationFighter(manager.team2));
        if (this is DuelBattleCamera && !IsHoldingReplacementFraming && _stabilizer.IsInitialized
            && (wasHolding || firstUnit != _lastFirst || secondUnit != _lastSecond))
            _handoffRemaining = .7f;
        else _handoffRemaining = Mathf.Max(0, _handoffRemaining - Mathf.Clamp(deltaTime, 0, .05f));
        if (!IsHoldingReplacementFraming)
        {
            _lastFirst = firstUnit;
            _lastSecond = secondUnit;
        }
        AddTeam(manager.team1);
        AddTeam(manager.team2);
        if (_bounds.Count == 0) return;

        var usable = UsableViewport(camera);
        bool countDown = FSceneProcessesRunner.Main.currentProcess is CountDownProcess;
        bool hasFirstAnchor = TryGetTeamAnchor(manager.team1, out var firstAnchor);
        bool hasSecondAnchor = TryGetTeamAnchor(manager.team2, out var secondAnchor);
        bool hasTeamAxis = hasFirstAnchor && hasSecondAnchor;
        if (hasTeamAxis && manager.team2.teamConfig.myTeam == RTFightManager.playerTeam)
        {
            var swap = firstAnchor; firstAnchor = secondAnchor; secondAnchor = swap;
        }
        if (!_stabilizer.IsInitialized && hasTeamAxis)
            _stabilizer.Reset(DiagonalYaw(firstAnchor, secondAnchor, camera, usable));
        // The countdown presents the final team arrangement. Do not inherit a
        // menu/result heading or rotate that arrangement back into a side view.
        float horizontal = CanSetH && !countDown ? UltimateJoystick.GetHorizontalAxis("RotateCamera") : 0;
        if (Mathf.Abs(horizontal) > 0.01f)
        {
            _autoOrbitEngaged = false;
            _stabilizer.UpdateYaw(_stabilizer.Yaw + horizontal * 75 * Mathf.Clamp(deltaTime, 0, 0.05f),
                deltaTime, true);
        }
        else if (!countDown && !IsHoldingReplacementFraming && !IsSettlingReplacementFraming
            && this is DuelBattleCamera && AutoRotateCamera)
        {
            bool rotating = false;
            var first = manager.team1.RMode_Unit.Value?.WholeT;
            var second = manager.team2.RMode_Unit.Value?.WholeT;
            if (first != null && second != null)
            {
                var combatLine = second.position - first.position;
                combatLine.y = 0;
                // Separate engage/release distances prevent small contact and
                // hit-reaction motion from repeatedly starting camera orbit.
                float threshold = _autoOrbitEngaged ? 2.4f : 3.2f;
                _autoOrbitEngaged = combatLine.sqrMagnitude > threshold * threshold;
                if (_autoOrbitEngaged)
                {
                    // Keep the same diagonal side after combat starts; the
                    // existing slow orbit and engage/release window absorb
                    // passing, impacts and brief changes of the combat axis.
                    var player = first.position;
                    var opponent = second.position;
                    if (manager.team2.teamConfig.myTeam == RTFightManager.playerTeam)
                    {
                        var swap = player; player = opponent; opponent = swap;
                    }
                    // Orbit follows the ground axis, not animated body-center
                    // height. A distant launch/landing must not rotate the arena.
                    _stabilizer.UpdateYaw(BattleCameraOpening.CalculatePlanarYaw(player, opponent,
                        _profile, camera.aspect, usable), deltaTime);
                    rotating = true;
                }
            }
            else _autoOrbitEngaged = false;
            if (!rotating) _stabilizer.UpdateYaw(_stabilizer.Yaw, deltaTime, true);
        }
        else
        {
            _autoOrbitEngaged = false;
            _stabilizer.UpdateYaw(_stabilizer.Yaw, deltaTime, true);
        }

        var fitted = _stabilizer.Update(_bounds, _trackingBounds.center, camera.aspect, fieldOfView, usable,
            camera.nearClipPlane, deltaTime, IsHoldingReplacementFraming, _bodyEnvelopes, IsSettlingReplacementFraming);
        camera.transform.SetPositionAndRotation(fitted.Position, fitted.Rotation);
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, fitted.Distance + 100);
    }

    void ResetFraming(Camera camera)
    {
        _stabilizer.Reset(camera != null ? camera.transform.eulerAngles.y : 0);
        IsHoldingReplacementFraming = false;
        _models.Clear();
        _bounds.Clear();
        _bodyEnvelopes.Clear();
        _hasTrackingBounds = false;
        _autoOrbitEngaged = false;
        _lastFirst = _lastSecond = null;
        _handoffRemaining = 0;
        _hasUsableViewport = false;
    }

    static bool HasLiveRotationFighter(UnitsManger team)
    {
        var unit = team?.RMode_Unit.Value;
        return unit != null && unit.WholeT != null && unit.gameObject.activeInHierarchy
            && unit.WholeT.gameObject.activeInHierarchy && !unit.FightDataRef.IsDead.Value;
    }

    float DiagonalYaw(Vector3 player, Vector3 opponent, Camera camera, Rect usable)
        => BattleCameraOpening.CalculateYaw(player, opponent, _profile, _bounds, _bodyEnvelopes,
            _trackingBounds.center, camera.aspect, usable, camera.nearClipPlane);

    static bool TryGetTeamAnchor(UnitsManger team, out Vector3 anchor)
    {
        anchor = Vector3.zero;
        int count = 0;
        if (team?.teamMembers?.mDict == null) return false;
        foreach (var unit in team.teamMembers.mDict.Values)
        {
            if (unit == null || unit.WholeT == null
                || !BattleCameraFraming.ShouldIncludeUnit(team.TeamMode,
                    unit.gameObject.activeInHierarchy && unit.WholeT.gameObject.activeInHierarchy,
                    unit.FightDataRef.IsDead.Value, team.RMode_Unit.Value == unit)) continue;
            anchor += unit.geometryCenter != null ? unit.geometryCenter.position : unit.WholeT.position;
            count++;
        }
        if (count == 0) return false;
        anchor /= count;
        return true;
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
            bool newModel = !_models.TryGetValue(unit, out var model) || model.Root != unit.WholeT;
            if (newModel)
            {
                model = new ModelFramingReference { Root = unit.WholeT,
                    Renderers = unit.WholeT.GetComponentsInChildren<Renderer>(true) };
                _models[unit] = model;
            }
            if (!BattleCameraFraming.TryGetModelBounds(model.Renderers, out var box))
            {
                // A model not yet rendered still gets a scale-aware body envelope.
                var size = Vector3.Scale(new Vector3(1, 2.8f, 1), Abs(unit.WholeT.lossyScale));
                box = new Bounds(unit.WholeT.position + Vector3.up * size.y * 0.5f, size);
            }
            float scaleY = Mathf.Abs(unit.WholeT.lossyScale.y);
            float horizontalScale = Mathf.Max(Mathf.Abs(unit.WholeT.lossyScale.x), Mathf.Abs(unit.WholeT.lossyScale.z));
            if (newModel)
            {
                model.CenterHeight = (box.center.y - unit.WholeT.position.y) / Mathf.Max(0.001f, scaleY);
                var offset = box.center - unit.WholeT.position;
                // Reserve the neutral silhouette in every horizontal direction.
                // An upright cylinder anticipates turns without the excessive
                // corner padding of a world-axis-aligned square footprint.
                model.HorizontalRadius = (new Vector2(box.extents.x, box.extents.z).magnitude
                    + new Vector2(offset.x, offset.z).magnitude) / Mathf.Max(0.001f, horizontalScale) + 0.15f;
            }
            // Animation changes the safety envelope, but does not move the
            // composition target whenever a hand, sword or cape extends.
            var anchor = unit.WholeT.position + Vector3.up * (model.CenterHeight * scaleY);
            if (!_hasTrackingBounds)
            {
                _trackingBounds = new Bounds(anchor, Vector3.zero);
                _hasTrackingBounds = true;
            }
            else _trackingBounds.Encapsulate(anchor);
            float radius = model.HorizontalRadius * horizontalScale;
            _bodyEnvelopes.Add(new BattleCameraFraming.BodyEnvelope(
                new Vector3(unit.WholeT.position.x, box.center.y, unit.WholeT.position.z), radius, box.size.y + 0.5f));
            // Exceptional attacks/jumps still include their actual full bounds.
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
        // Evolution and result flows temporarily hide/remove the HUD. Its
        // disappearance is not a request to move or zoom the battle camera.
        if ((_hud == null || !_hud.gameObject.activeInHierarchy) && _hasUsableViewport)
            return _lastUsableViewport;
        if (_hud != null)
        {
            if (_hud.MiddleArea != null) middle = UIViewport(_hud.MiddleArea, camera);
            var portraits = _hud.Team1UI?.SideIconsContainer;
            excludeRail = FightLoad.Fight != null && !FightLoad.Fight.IsGroupBattle
                && portraits != null && portraits.gameObject.activeInHierarchy;
            if (excludeRail) rail = UIViewport(portraits, camera);
        }
        _lastUsableViewport = this is DuelBattleCamera
            ? BattleCameraFraming.CalculateDuelViewport(safe, middle, rail, excludeRail)
            : BattleCameraFraming.CalculateUsableViewport(safe, middle, rail, excludeRail);
        _hasUsableViewport = true;
        return _lastUsableViewport;
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
