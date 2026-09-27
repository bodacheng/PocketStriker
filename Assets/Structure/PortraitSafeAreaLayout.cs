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
            _screenSafeArea != Screen.safeArea)
            Apply();
    }

    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        _screenSize = new Vector2Int(Screen.width, Screen.height);
        _screenSafeArea = Screen.safeArea;
        var safe = _screenSafeArea;
        if (safe.width <= 0 || safe.height <= 0)
            safe = new Rect(0, 0, Screen.width, Screen.height);

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
