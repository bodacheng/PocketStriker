using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the authored portrait canvas inside the current device safe area.</summary>
[DisallowMultipleComponent]
public sealed class PortraitSafeAreaLayout : MonoBehaviour
{
    Canvas _canvas;
    CanvasScaler _scaler;
    RectTransform _safeArea;
    Vector2 _referenceResolution;
    Vector2Int _screenSize;
    Rect _screenSafeArea;
    bool _initialized;
    float _bannerHeight;
#if UNITY_EDITOR
    // Device simulator input used by the local banner regression only.
    Rect? safeAreaForValidation;
#endif
    Rect DeviceSafeArea
    {
        get
        {
#if UNITY_EDITOR
            if (safeAreaForValidation.HasValue) return safeAreaForValidation.Value;
#endif
            return Screen.safeArea;
        }
    }
    void OnEnable() { BannerAds.OccupiedAreaChanged += Refresh; }
    void OnDisable() { BannerAds.OccupiedAreaChanged -= Refresh; }
    void Refresh() { if (_initialized && _canvas != null) Apply(); }

    public static Rect ContentSafeArea(Rect safe, Vector2 screen, float bannerHeight)
    {
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(Vector2.zero, screen);
        // The native TopLeft banner starts at the device safe top. Deduct its
        // actual pixel height exactly once, before conversion to canvas units.
        float height = Mathf.Clamp(bannerHeight, 0, Mathf.Max(0, safe.height - 1));
        safe.yMax -= height;
        return safe;
    }

    public void Initialize(Canvas canvas, RectTransform safeArea)
    {
        _canvas = canvas;
        _safeArea = safeArea;
        _scaler = canvas.GetComponent<CanvasScaler>();
        if (_scaler == null) return;
        // Capture once: repeated initialization must not compound the notch inset.
        if (!_initialized) _referenceResolution = _scaler.referenceResolution;
        _initialized = true;
        Apply();
    }

    void Update()
    {
        if (!_initialized || _canvas == null) return;
        if (_screenSize.x != Screen.width || _screenSize.y != Screen.height ||
            _screenSafeArea != DeviceSafeArea || !Mathf.Approximately(_bannerHeight, BannerAds.OccupiedHeightPixels))
            Apply();
    }

    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        _screenSize = new Vector2Int(Screen.width, Screen.height);
        _screenSafeArea = DeviceSafeArea;
        _bannerHeight = BannerAds.OccupiedHeightPixels;
        var safe = ContentSafeArea(_screenSafeArea, _screenSize, _bannerHeight);

        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        _scaler.referenceResolution = new Vector2(
            _referenceResolution.x * Screen.width / safe.width,
            _referenceResolution.y * Screen.height / safe.height);

        // The fallback hanger can be the canvas itself; never anchor a root canvas.
        if (_safeArea != null && _safeArea != _canvas.transform)
        {
            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }
        UnityEngine.Canvas.ForceUpdateCanvases();
        foreach (var layer in _canvas.GetComponentsInChildren<UILayer>(true))
            layer.ResizeAreas();
    }
}
