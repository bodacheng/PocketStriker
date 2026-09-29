using UnityEngine;

/// <summary>Keeps the persistent debug window below the notch and above the home indicator.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class PocketStrikerDebugConsoleLayout : MonoBehaviour
{
    [SerializeField] RectTransform logWindow;
    Canvas consoleCanvas;

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
        var minimum = new Vector2(safe.xMin, safe.yMin) / scale;
        var maximum = new Vector2(safe.xMax - Screen.width, safe.yMax - Screen.height) / scale;
        if (logWindow.offsetMin != minimum) logWindow.offsetMin = minimum;
        if (logWindow.offsetMax != maximum) logWindow.offsetMax = maximum;
    }
}
