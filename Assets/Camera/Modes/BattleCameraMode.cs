using UnityEngine;

/// <summary>Shared battle-camera controls and HUD viewport, independent of what the mode frames.</summary>
public abstract class BattleCameraMode : CameraMode
{
    FightingStepLayer _hud;
    readonly Vector3[] _corners = new Vector3[4];
    Rect _lastUsableViewport;
    bool _hasUsableViewport;

    public abstract float Pitch { get; }
    public abstract bool IsFramingInitialized { get; }
    public abstract bool IsHoldingReplacementFraming { get; protected set; }
    public abstract bool IsSettlingReplacementFraming { get; }
    public abstract int FramedUnitCount { get; }
    public abstract float ShadowReceiverDistance { get; protected set; }
    public abstract BattleCameraFraming.Pose DesiredPose { get; }
    public abstract BattleCameraFraming.Pose CurrentPose { get; }
    public virtual bool CanSetH { get; set; } = true;

    protected FightingStepLayer HUD
    {
        get
        {
            if (_hud == null) _hud = Object.FindFirstObjectByType<FightingStepLayer>(FindObjectsInactive.Include);
            return _hud;
        }
    }

    protected void ResetUsableViewport() => _hasUsableViewport = false;
    protected void ReleaseHUD() => _hud = null;

    public virtual Rect GetUsableViewport(Camera camera)
    {
        var safe = ScreenRectToViewport(Screen.safeArea, camera);
        var middle = safe;
        var rail = new Rect();
        bool excludeRail = false;
        var hud = HUD;
        // Hiding the HUD for a result or modal does not change the composition.
        if ((hud == null || !hud.gameObject.activeInHierarchy) && _hasUsableViewport)
            return _lastUsableViewport;
        if (hud != null)
        {
            if (hud.MiddleArea != null) middle = UIViewport(hud.MiddleArea, camera);
            var portraits = hud.Team1UI?.SideIconsContainer;
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
}
