using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Fills a RawImage with an undistorted crop while retaining its existing UV scroll.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RawImage))]
public sealed class ScrollingBackgroundAspectFill : UIBehaviour
{
    const float UVTolerance = .000001f;
    [SerializeField, Tooltip("UV span of the approved artwork before aspect cropping. Values above one retain its scale while sampling a repeating texture.")]
    Vector2 referenceUvSize = Vector2.one;
    RawImage image;

    public Vector2 ReferenceUvSize => referenceUvSize;

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.preWillRenderCanvases -= RefreshAspectFill;
        Canvas.preWillRenderCanvases += RefreshAspectFill;
        RefreshAspectFill();
    }

    protected override void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshAspectFill;
        // OffsetScrolling pauses with the background. Retain its phase so
        // reactivation continues where it stopped, including after a resize.
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        Canvas.preWillRenderCanvases -= RefreshAspectFill;
        base.OnDestroy();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        RefreshAspectFill();
    }

    public void RefreshAspectFill()
    {
        if (!isActiveAndEnabled) return;
        // A dimension callback can arrive before OnEnable; do not depend on
        // Awake having run. The pre-render callback also handles late textures.
        if (image == null) image = GetComponent<RawImage>();
        if (image == null || image.texture == null) return;
        var rect = image.rectTransform.rect;
        var texture = image.texture;
        if (!PositiveFinite(rect.width) || !PositiveFinite(rect.height)
            || texture.width <= 0 || texture.height <= 0) return;

        float targetAspect = rect.width / rect.height;
        float textureAspect = (float)texture.width / texture.height;
        var reference = referenceUvSize;
        if (!PositiveFinite(reference.x) || !PositiveFinite(reference.y))
            reference = Vector2.one;
        // The repeating texture may contain a different number of complete
        // units than the approved portrait. Crop that reference span so the
        // pattern keeps the approved scale on phones and tablets.
        float referenceAspect = reference.x * textureAspect / reference.y;
        var size = targetAspect > referenceAspect
            ? new Vector2(reference.x, reference.x * textureAspect / targetAspect)
            : new Vector2(reference.y * targetAspect / textureAspect, reference.y);
        if (!PositiveFinite(size.x) || !PositiveFinite(size.y)) return;
        var current = image.uvRect;
        if (Mathf.Abs(current.width - size.x) <= UVTolerance
            && Mathf.Abs(current.height - size.y) <= UVTolerance) return;

        // Preserve the current UV center, including every increment made by
        // OffsetScrolling. Only the crop changes; do not recenter each frame.
        image.uvRect = new Rect(current.position + (current.size - size) * .5f, size);
    }

    static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
}
