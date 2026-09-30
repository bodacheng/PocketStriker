using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared safe content geometry for the two layers composing battle loading.</summary>
public static class LoadingScreenLayout
{
    public static Rect SafeBounds(RectTransform root)
    {
        var bounds = root.rect;
        var safe = PosCal.SafeAreaRect;
        if (safe == null || root == safe || root.IsChildOf(safe)) return bounds;
        var corners = new Vector3[4];
        safe.GetWorldCorners(corners);
        var lower = root.InverseTransformPoint(corners[0]);
        var upper = root.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(Mathf.Max(bounds.xMin, lower.x), Mathf.Max(bounds.yMin, lower.y),
            Mathf.Min(bounds.xMax, upper.x), Mathf.Min(bounds.yMax, upper.y));
    }

    public static float Margin(Rect safe) => Mathf.Clamp(safe.width * 0.07f, 36f, 76f);
    public static float FooterHeight(Rect safe) => Mathf.Clamp(safe.height * 0.14f, 188f, 268f);

    public static void Place(RectTransform target, RectTransform root, Rect bounds)
    {
        if (target.parent != root) target.SetParent(root, false);
        target.localScale = Vector3.one;
        target.anchorMin = target.anchorMax = Vector2.zero;
        target.pivot = Vector2.zero;
        target.anchoredPosition = bounds.min - root.rect.min;
        target.sizeDelta = bounds.size;
    }

    public static float TextHeight(Text text, float width)
    {
        if (string.IsNullOrEmpty(text.text)) return 0;
        var settings = text.GetGenerationSettings(new Vector2(width, 0));
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        return Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit) + 4f;
    }

    public static void ConfigureText(Text text, int fontSize, TextAnchor alignment)
    {
        text.fontSize = fontSize;
        text.resizeTextForBestFit = false;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        var outline = text.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }
}
