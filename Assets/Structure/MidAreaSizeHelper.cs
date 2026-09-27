using UnityEngine;

public class MidAreaSizeHelper : MonoBehaviour
{
    [SerializeField] private bool keepTopPos;
    [SerializeField] private float height;
    [SerializeField] RectTransform rectTransform;
    
    public void Resize()
    {
        if (rectTransform == null) rectTransform = transform as RectTransform;
        if (rectTransform == null || !(rectTransform.parent is RectTransform parent)) return;

        // The parent is normally the safe-area layer, not the full-screen canvas.
        // Keep the chosen edge fixed even when the rect does not use stretch anchors.
        float oldHeight = rectTransform.rect.height;
        float newHeight = Mathf.Clamp(height, 0, parent.rect.height);
        var position = rectTransform.anchoredPosition;
        position.y += keepTopPos
            ? (oldHeight - newHeight) * (1 - rectTransform.pivot.y)
            : (newHeight - oldHeight) * rectTransform.pivot.y;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);
        rectTransform.anchoredPosition = position;
    }
}
