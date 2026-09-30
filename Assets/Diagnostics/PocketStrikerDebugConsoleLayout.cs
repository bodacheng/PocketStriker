using UnityEngine;

/// <summary>Keeps the persistent debug window below the notch and above the home indicator.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class PocketStrikerDebugConsoleLayout : MonoBehaviour
{
    [SerializeField] RectTransform logWindow;
    [SerializeField] RectTransform logPopup;
    Canvas consoleCanvas;
    readonly Vector3[] popupCorners = new Vector3[4];

    void Awake()
    {
        consoleCanvas = GetComponent<Canvas>();
    }

    void LateUpdate()
    {
        if (logWindow == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        var safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0)
            return;

        var scale = Mathf.Max(consoleCanvas.scaleFactor, 0.001f);
        // Apply the insets at the window's current anchors so resizing still works.
        // The package's top-cutout adjustment is disabled in our prefab.
        var minimum = SafeAreaOffset(safe, Screen.width, Screen.height, logWindow.anchorMin) / scale;
        var maximum = SafeAreaOffset(safe, Screen.width, Screen.height, logWindow.anchorMax) / scale;
        if (logWindow.offsetMin != minimum) logWindow.offsetMin = minimum;
        if (logWindow.offsetMax != maximum) logWindow.offsetMax = maximum;

        if (logPopup != null && logPopup.gameObject.activeInHierarchy)
        {
            logPopup.GetWorldCorners(popupCorners);
            var camera = consoleCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : consoleCanvas.worldCamera;
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, popupCorners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(camera, popupCorners[2]);
            var shift = new Vector2(
                Mathf.Max(0, safe.xMin - bottomLeft.x) + Mathf.Min(0, safe.xMax - topRight.x),
                Mathf.Max(0, safe.yMin - bottomLeft.y) + Mathf.Min(0, safe.yMax - topRight.y));
            logPopup.anchoredPosition += shift / scale;
        }
    }

    public static Vector2 SafeAreaOffset(Rect safe, float width, float height, Vector2 anchor)
    {
        return new Vector2(
            Mathf.Lerp(safe.xMin, safe.xMax - width, anchor.x),
            Mathf.Lerp(safe.yMin, safe.yMax - height, anchor.y));
    }
}
