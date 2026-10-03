using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Fits a RawImage while retaining a scroll phase or the complete height of a static gradient.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RawImage))]
public sealed class ScrollingBackgroundAspectFill : UIBehaviour
{
    const float UVTolerance = .000001f;
    [SerializeField, Tooltip("UV span of the approved artwork before aspect cropping. Values above one retain its scale while sampling a repeating texture.")]
    Vector2 referenceUvSize = Vector2.one;
    [SerializeField, Tooltip("Keep the complete vertical gradient stationary. The texture repeats only horizontally to fill wider screens.")]
    bool staticGradient;
    [SerializeField, Tooltip("Mirror the static background vertically without modifying its texture or other UI.")]
    bool flipStaticVertically;
    RawImage image;
    OffsetScrolling scroller;

    public Vector2 ReferenceUvSize => referenceUvSize;
    public bool StaticGradient => staticGradient;
    public bool FlipStaticVertically => flipStaticVertically;

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
        // Legacy backgrounds retain their phase; static backgrounds restore
        // their full-height view when they become visible again.
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
        if (staticGradient)
        {
            // Only this background's UV scroller is stopped. Model and UI
            // animations on other objects retain their normal behaviour.
            if (scroller == null) scroller = GetComponent<OffsetScrolling>();
            if (scroller != null && scroller.enabled) scroller.enabled = false;
        }
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
        if (staticGradient)
        {
            // Preserve both color endpoints on every aspect ratio. U Repeat
            // extends the same pixel rows on tablets; V Clamp keeps the top
            // and bottom from wrapping into each other.
            float width = targetAspect / textureAspect;
            if (!PositiveFinite(width)) return;
            var fixedView = new Rect((1f - width) * .5f,
                flipStaticVertically ? 1f : 0f, width, flipStaticVertically ? -1f : 1f);
            var actual = image.uvRect;
            if (!(Mathf.Abs(actual.x - fixedView.x) <= UVTolerance
                && Mathf.Abs(actual.y - fixedView.y) <= UVTolerance
                && Mathf.Abs(actual.width - fixedView.width) <= UVTolerance
                && Mathf.Abs(actual.height - fixedView.height) <= UVTolerance))
                image.uvRect = fixedView;
            return;
        }
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
