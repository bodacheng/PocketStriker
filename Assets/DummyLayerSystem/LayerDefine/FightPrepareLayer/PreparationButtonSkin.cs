using UnityEngine;
using UnityEngine.UI;

// Opt-in preparation styling. Other screens keep their original prefab visuals.
public sealed class PreparationButtonSkin : MonoBehaviour
{
    BOButton _button;
    Image _outline;
    Text _label;
    Color _accent, _ink;
    bool _guide, _lastInteractable;

    public static PreparationButtonSkin Apply(BOButton button, Text label, Color accent, bool primary, int fontSize)
    {
        var skin = button.GetComponent<PreparationButtonSkin>();
        if (skin != null) return skin;
        var fillSprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonFill");
        var outlineSprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonOutline");
        if (fillSprite == null || outlineSprite == null) return null;
        skin = button.gameObject.AddComponent<PreparationButtonSkin>();
        skin._button = button;
        skin._label = label;
        skin._accent = accent;
        skin._ink = primary ? new Color(1f, 0.96f, 0.84f) : new Color(0.78f, 0.9f, 0.94f);

        var fill = Graphic(button.transform, "PreparationFill", fillSprite);
        fill.raycastTarget = true;
        fill.transform.SetAsFirstSibling();
        skin._outline = Graphic(button.transform, "PreparationOutline", outlineSprite);
        skin._outline.transform.SetSiblingIndex(1);
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.ColorTint;
        var baseColor = primary ? new Color(0.46f, 0.35f, 0.16f) : new Color(0.075f, 0.16f, 0.21f, 0.96f);
        var colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = primary ? new Color(0.61f, 0.47f, 0.23f) : new Color(0.14f, 0.29f, 0.35f);
        colors.pressedColor = primary ? new Color(0.33f, 0.25f, 0.11f) : new Color(0.045f, 0.10f, 0.15f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.19f, 0.24f, 0.28f, 0.75f);
        colors.colorMultiplier = 1;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        foreach (var shadow in label.GetComponents<Shadow>()) shadow.enabled = false;
        label.fontSize = fontSize;
        label.resizeTextForBestFit = false;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        Fit(label.rectTransform, 22, 8);
        label.transform.SetAsLastSibling();
        skin.RefreshState();
        return skin;
    }

    public void SetGuide(bool guide)
    {
        _guide = guide;
        RefreshState();
    }

    public void SetAccent(Color accent, Color ink)
    {
        _accent = accent;
        _ink = ink;
        RefreshState();
    }

    public void RefreshState()
    {
        if (_button == null) return;
        _lastInteractable = _button.interactable;
        _label.color = _lastInteractable ? _ink : new Color(0.58f, 0.65f, 0.70f);
        _outline.color = _lastInteractable ? _accent : new Color(0.36f, 0.45f, 0.51f, 0.55f);
    }

    void Update()
    {
        if (_button == null) return;
        if (_lastInteractable != _button.interactable) RefreshState();
        if (_guide && _lastInteractable)
        {
            var color = _accent;
            color.a = 0.66f + 0.34f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            _outline.color = color;
        }
    }

    static Image Graphic(Transform parent, string name, Sprite sprite)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.layer = parent.gameObject.layer;
        node.transform.SetParent(parent, false);
        var image = node.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1;
        image.raycastTarget = false;
        Fit(image.rectTransform, 0, 0);
        return image;
    }

    public static void Fit(RectTransform rect, float horizontal = 0, float vertical = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
        rect.localScale = Vector3.one;
    }
}
