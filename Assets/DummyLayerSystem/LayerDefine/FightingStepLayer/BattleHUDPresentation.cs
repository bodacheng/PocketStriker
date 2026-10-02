using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact, safe-area-aware battle controls. Input and combat state remain with their existing owners.</summary>
public sealed class BattleHUDPresentation : MonoBehaviour
{
    const float ControlsLift = 48f;
    const float ControlsAreaHeight = 344f + ControlsLift;
    FightingStepLayer _layer;
    RectTransform _root;
    Rect _lastSafe;
    int _lastPortraitCount = -1;
    bool _refreshing;
    SystemLanguage _lastLanguage;
    Font _font;
    readonly Dictionary<BOButton, Text> _labels = new Dictionary<BOButton, Text>();
    readonly Dictionary<AutoSwitch, bool> _autoStates = new Dictionary<AutoSwitch, bool>();
    static readonly Color Cyan = new Color(0.40f, 0.68f, 0.76f, 0.78f);
    static readonly Color Gold = new Color(0.83f, 0.72f, 0.43f, 0.9f);

    public void Initialize(FightingStepLayer layer)
    {
        if (_layer == layer) return;
        _layer = layer;
        _root = (RectTransform)layer.transform;
        var existing = layer.Team1UI?.AutoSwitch?.GetComponentInChildren<Text>(true);
        _font = existing != null ? existing.font : layer.GetComponentInChildren<Text>(true)?.font;
    }

    void LateUpdate()
    {
        if (_root == null) return;
        var rail = _layer.Team1UI?.SideIconsContainer;
        var safe = SafeRect();
        if (safe != _lastSafe || AppSetting.Value.Language != _lastLanguage
            || (rail != null && rail.childCount != _lastPortraitCount)) RefreshLayout();
        RefreshAutoState(_layer.Team1UI?.AutoSwitch);
        RefreshAutoState(_layer.Team2UI?.AutoSwitch);
    }

    public void RefreshLayout()
    {
        if (_refreshing || _root == null || _root.rect.width <= 0 || _root.rect.height <= 0) return;
        _refreshing = true;
        try
        {
            var safe = SafeRect();
            if (safe.width <= 0 || safe.height <= 0) return;
            _lastSafe = safe;
            _lastLanguage = AppSetting.Value.Language;
            var u = Mathf.Clamp(safe.width / 1200f, 0.72f, 1.1f);
            Section(_layer.TopArea, safe.xMin, safe.yMax - 104 * u, safe.width, 104 * u);
            Section(_layer.BottomArea, safe.xMin, safe.yMin, safe.width, ControlsAreaHeight * u);
            Section(_layer.MiddleArea, safe.xMin, safe.yMin + ControlsAreaHeight * u, safe.width, Mathf.Max(0, safe.height - (104 + ControlsAreaHeight) * u));

            Skin(_layer.PauseButton, "", Cyan, 40, true);
            PauseGlyph(_layer.PauseButton);
            Place(_layer.PauseButton.transform, new Vector2(safe.xMin + 58 * u, safe.yMax - 58 * u), new Vector2(76, 72) * u);
            LayoutAuto(_layer.Team1UI?.AutoSwitch, new Vector2(safe.xMax - 106 * u, safe.yMax - 58 * u), u);
            LayoutAuto(_layer.Team2UI?.AutoSwitch, new Vector2(safe.xMax - 288 * u, safe.yMax - 58 * u), u);

            var rail = _layer.Team1UI?.SideIconsContainer;
            if (rail != null)
            {
                _lastPortraitCount = rail.childCount;
                var railHeight = Mathf.Clamp(rail.GetComponentsInChildren<SideUnitIcon>(true).Length, 1, 4) * 178f - 8f;
                Place(rail, new Vector2(safe.xMin + 84 * u, safe.yMax - 120 * u - railHeight * u / 2), new Vector2(128, railHeight) * u);
                var grid = rail.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.cellSize = new Vector2(128, 170);
                    grid.spacing = new Vector2(0, 8);
                    grid.padding = new RectOffset();
                    grid.childAlignment = TextAnchor.UpperCenter;
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 1;
                    grid.startAxis = GridLayoutGroup.Axis.Vertical;
                }
                rail.localScale = Vector3.one * u;
                // Grid cells remain authored UI units; the rail scales as one group.
                rail.sizeDelta = new Vector2(128, railHeight);
                foreach (var icon in rail.GetComponentsInChildren<SideUnitIcon>(true))
                    icon.ApplyBattleHUDStyle(false, false);
            }
            StyleSelectedFrame(_layer.Team1UI?.SelectedFrame);
            StyleSelectedFrame(_layer.Team2UI?.SelectedFrame);
            Count(_layer.Team1UI?.LiveUnitCount, new Vector2(safe.center.x - 125 * u, safe.yMax - 66 * u), u, true);
            Count(_layer.Team2UI?.LiveUnitCount, new Vector2(safe.center.x + 125 * u, safe.yMax - 66 * u), u, false);

            var inputs = _layer.InputsManager;
            if (inputs != null)
            {
                var y = safe.yMin + (114 + ControlsLift) * u;
                LayoutSkill(inputs.AttackButton, new Vector2(safe.xMax - 448 * u, y), u);
                LayoutSkill(inputs.Fire1Button, new Vector2(safe.xMax - 274 * u, y), u);
                LayoutSkill(inputs.Fire2Button, new Vector2(safe.xMax - 100 * u, y), u);
                EffectOnly(inputs.DashButton);
                ActionGlyph(inputs.DashButton, false);
                Skin(inputs.DefendButton, Localize("GUARD", "ガード", "防御"), Cyan, ChineseLanguage ? 32 : 24);
                Place(inputs.DashButton.transform, new Vector2(safe.xMax - 448 * u, safe.yMin + (274 + ControlsLift) * u), new Vector2(100, 100) * u);
                Place(inputs.DefendButton.transform, new Vector2(safe.xMax - 274 * u, safe.yMin + (274 + ControlsLift) * u), new Vector2(100, 100) * u);
                EffectOnly(inputs.DreamComboBtn, inputs.DreamComboGauge != null ? inputs.DreamComboGauge.transform : null);
                ActionGlyph(inputs.DreamComboBtn, true);
                Place(inputs.DreamComboBtn.transform, new Vector2(safe.xMax - 100 * u, safe.yMin + (282 + ControlsLift) * u), new Vector2(112, 112) * u);
                var gauge = inputs.DreamComboGauge;
                if (gauge != null)
                {
                    PreparationButtonSkin.Fit((RectTransform)gauge.transform);
                    gauge.InnerColor.Value = new Color(0.86f, 0.74f, 0.43f);
                    gauge.EmptyColor.Value = new Color(0.27f, 0.31f, 0.32f, 0.92f);
                    gauge.BorderWidth.Value = 0;
                    gauge.Radius.Value = 0.43f;
                    gauge.LineWidth.Value = 0.045f;
                    gauge.InnerColor.ApplyToShader(false);
                    gauge.EmptyColor.ApplyToShader(false);
                    gauge.BorderWidth.ApplyToShader(false);
                    gauge.Radius.ApplyToShader(false);
                    gauge.LineWidth.ApplyToShader(false);
                }
                LayoutJoysticks(safe, u);
                inputs.RefreshHUDPresentation();
                // Visible action targets take precedence over the invisible camera touch pad.
                foreach (var button in new[] { inputs.AttackButton, inputs.Fire1Button, inputs.Fire2Button, inputs.DashButton, inputs.DefendButton, inputs.DreamComboBtn })
                    Overlay(button?.transform, 999);
            }
            _layer.Team1UI?.RefreshHUDPresentation();
            _layer.Team2UI?.RefreshHUDPresentation();
        }
        finally { _refreshing = false; }
    }

    void LayoutSkill(BOButton button, Vector2 center, float u)
    {
        if (button == null) return;
        Skin(button, "", new Color(0.43f, 0.58f, 0.63f, 0.64f), 1);
        Place(button.transform, center, new Vector2(156, 156) * u);
    }

    // These actions are represented by the existing elemental particle effects.
    // The transparent graphic only preserves their pointer-down/up hit target.
    static void EffectOnly(BOButton button, Transform gauge = null)
    {
        if (button == null) return;
        var skin = button.GetComponent<PreparationButtonSkin>();
        if (skin != null) skin.enabled = false;
        foreach (var label in button.GetComponentsInChildren<Text>(true)) label.enabled = false;
        foreach (var image in button.GetComponentsInChildren<Image>(true))
        {
            if (gauge != null && (image.transform == gauge || image.transform.IsChildOf(gauge))) continue;
            image.enabled = false;
            image.raycastTarget = false;
        }
        var hit = button.GetComponent<Image>();
        if (hit == null) hit = button.gameObject.AddComponent<Image>();
        hit.enabled = true;
        hit.sprite = null;
        hit.color = Color.clear;
        // This graphic intentionally has no visible pixels. Keep it in the canvas
        // so later layout/render passes cannot remove its expanded pointer target.
        hit.canvasRenderer.cullTransparentMesh = false;
        hit.raycastTarget = true;
        hit.raycastPadding = new Vector4(-20, -20, -20, -20);
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
    }

    static void ActionGlyph(BOButton button, bool dream)
    {
        if (button == null) return;
        var child = button.transform.Find("ActionGlyph");
        if (child == null)
        {
            var node = new GameObject("ActionGlyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleActionGlyph));
            node.layer = button.gameObject.layer;
            child = node.transform;
            child.SetParent(button.transform, false);
        }
        PreparationButtonSkin.Fit((RectTransform)child);
        var glyph = child.GetComponent<BattleActionGlyph>();
        glyph.enabled = true;
        glyph.DreamCombo = dream;
        glyph.color = new Color(.87f, .76f, .43f, 1);
        glyph.raycastTarget = false;
        glyph.SetVerticesDirty();
        child.SetAsLastSibling();
    }

    static void PauseGlyph(BOButton button)
    {
        if (button == null) return;
        for (int i = 0; i < 2; i++)
        {
            string name = "PauseBar" + i;
            var child = button.transform.Find(name);
            if (child == null)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                node.layer = button.gameObject.layer;
                child = node.transform;
                child.SetParent(button.transform, false);
            }
            var rect = (RectTransform)child;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = new Vector2(i == 0 ? -8 : 8, 0);
            rect.sizeDelta = new Vector2(6, 28);
            var image = child.GetComponent<Image>();
            image.enabled = true;
            image.color = new Color(0.78f, 0.9f, 0.94f);
            image.raycastTarget = false;
            child.SetAsLastSibling();
        }
    }

    void LayoutAuto(AutoSwitch auto, Vector2 center, float u)
    {
        if (auto == null) return;
        foreach (var animator in auto.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        var button = auto.GetComponent<BOButton>();
        if (button == null) button = auto.GetComponentInChildren<BOButton>(true);
        Skin(button, "AUTO", Cyan, 36, true);
        Place(auto.transform, center, new Vector2(164, 72) * u);
        _autoStates.Remove(auto);
        RefreshAutoState(auto);
    }

    void RefreshAutoState(AutoSwitch auto)
    {
        if (auto == null || auto.CurrentState == null) return;
        var state = auto.CurrentState();
        if (_autoStates.TryGetValue(auto, out var previous) && previous == state) return;
        _autoStates[auto] = state;
        var button = auto.GetComponent<BOButton>();
        if (button == null) button = auto.GetComponentInChildren<BOButton>(true);
        var skin = button?.GetComponent<PreparationButtonSkin>();
        skin?.SetAccent(state ? new Color(0.48f, 0.81f, 0.69f) : Cyan,
            state ? new Color(0.77f, 0.95f, 0.86f) : new Color(0.72f, 0.78f, 0.82f));
        if (button != null && _labels.TryGetValue(button, out var label)) label.text = state ? "AUTO •" : "AUTO";
    }

    void Skin(BOButton button, string caption, Color accent, int fontSize, bool legacyImages = false)
    {
        if (button == null) return;
        if (legacyImages)
            foreach (var image in button.GetComponentsInChildren<Image>(true))
                if (image.name != "PreparationFill" && image.name != "PreparationOutline" && !image.name.StartsWith("PauseBar"))
                {
                    image.color = Color.clear;
                    image.raycastTarget = false;
                }
        if (!_labels.TryGetValue(button, out var label))
        {
            if (legacyImages)
            {
                foreach (var image in button.GetComponentsInChildren<Image>(true))
                {
                    image.color = Color.clear;
                    image.raycastTarget = false;
                }
            }
            else if (button.GetComponent<Image>() is Image original)
            {
                original.color = Color.clear;
                original.raycastTarget = false;
            }
            label = legacyImages ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null)
            {
                var node = new GameObject("BattleHUDLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                node.layer = button.gameObject.layer;
                node.transform.SetParent(button.transform, false);
                label = node.GetComponent<Text>();
                label.font = _font;
            }
            else label.transform.SetParent(button.transform, false);
            _labels.Add(button, label);
            PreparationButtonSkin.Apply(button, label, accent, false, fontSize);
            PreparationButtonSkin.Fit(label.rectTransform, caption.Contains("\n") ? 12 : 8, 6);
            // Compact chrome keeps a generous touch target on narrow phones.
            // Graphic padding expands the hit region without adding visible bulk.
            if (button.targetGraphic != null && fontSize > 1)
                button.targetGraphic.raycastPadding = new Vector4(-20, -20, -20, -20);
        }
        label.fontSize = fontSize;
        label.text = caption;
        PreparationButtonSkin.Fit(label.rectTransform, caption.Contains("\n") ? 12 : 8, 6);
    }

    void LayoutJoysticks(Rect safe, float u)
    {
        foreach (var joystick in _layer.GetComponentsInChildren<UltimateJoystick>(true))
        {
            if (joystick.joystickName != "joystick") continue;
            joystick.UpdateParentCanvas();
            var canvas = joystick.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) continue;
            var canvasRect = (RectTransform)canvas.transform;
            var canvasSize = canvasRect.rect.size;
            var center = (Vector2)canvasRect.InverseTransformPoint(_root.TransformPoint(new Vector2(safe.xMin + 156 * u, safe.yMin + (160 + ControlsLift) * u)));
            var size = 216 * u;
            joystick.scalingAxis = UltimateJoystick.ScalingAxis.Width;
            joystick.anchor = UltimateJoystick.Anchor.Left;
            joystick.joystickSize = size / canvasSize.x * 10;
            joystick.positionHorizontal = Mathf.Clamp01((center.x - canvasRect.rect.xMin - size / 2) / (canvasSize.x - size)) * 100;
            joystick.positionVertical = Mathf.Clamp01((center.y - canvasRect.rect.yMin - size / 2) / (canvasSize.y - size)) * 100;
            joystick.showTension = false;
            joystick.showHighlight = false;
            joystick.rotationOffset = 0;
            joystick.joystickBase.localRotation = Quaternion.identity;
            joystick.joystick.localRotation = Quaternion.identity;
            foreach (var accent in joystick.TensionAccents) if (accent != null) accent.enabled = false;
            if (joystick.highlightBase != null) joystick.highlightBase.enabled = false;
            if (joystick.highlightJoystick != null) joystick.highlightJoystick.enabled = false;
            foreach (var image in joystick.GetComponentsInChildren<Image>(true))
            {
                if (image.transform == joystick.joystickBase || image.transform == joystick.joystick) continue;
                image.color = Color.clear;
                image.raycastTarget = image.transform == joystick.transform;
            }
            var baseImage = joystick.joystickBase.GetComponent<Image>();
            if (baseImage != null)
            {
                baseImage.sprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonOutline");
                baseImage.type = Image.Type.Sliced;
                baseImage.color = new Color(0.58f, 0.69f, 0.73f, 0.65f);
                baseImage.raycastTarget = false;
            }
            var knob = joystick.joystick.GetComponent<Image>();
            if (knob != null)
            {
                knob.sprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonFill");
                knob.type = Image.Type.Sliced;
                knob.color = new Color(0.62f, 0.72f, 0.76f, 0.63f);
                knob.raycastTarget = false;
            }
            joystick.UpdatePositioning();
            joystick.joystick.anchorMin = joystick.joystick.anchorMax = new Vector2(0.5f, 0.5f);
            joystick.joystick.pivot = new Vector2(0.5f, 0.5f);
            joystick.joystick.anchoredPosition = Vector2.zero;
            joystick.joystick.sizeDelta = new Vector2(68, 68) * u;
        }
    }

    void StyleSelectedFrame(RectTransform frame)
    {
        if (frame == null) return;
        PreparationButtonSkin.Fit(frame, -3, -3);
        var image = frame.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonOutline");
            image.type = Image.Type.Sliced;
            image.color = Gold;
            image.raycastTarget = false;
            foreach (var shadow in image.GetComponents<Shadow>()) shadow.enabled = false;
        }
    }

    void Count(Text text, Vector2 center, float u, bool player)
    {
        if (text == null) return;
        Place(text.transform, center, new Vector2(228, 38) * u);
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = player ? new Color(0.62f, 0.85f, 0.77f) : new Color(0.91f, 0.67f, 0.60f);
        text.raycastTarget = false;
        foreach (var shadow in text.GetComponents<Shadow>()) shadow.enabled = false;
    }

    Rect SafeRect()
    {
        var rect = _root.rect;
        var safe = PosCal.SafeAreaRect;
        if (safe == null || safe == _root || _root.IsChildOf(safe)) return rect;
        var corners = new Vector3[4];
        safe.GetWorldCorners(corners);
        var min = _root.InverseTransformPoint(corners[0]);
        var max = _root.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(Mathf.Max(rect.xMin, min.x), Mathf.Max(rect.yMin, min.y), Mathf.Min(rect.xMax, max.x), Mathf.Min(rect.yMax, max.y));
    }

    void Section(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null) return;
        Place(rect, new Vector2(x + width / 2, y + height / 2), new Vector2(width, height));
    }

    void Place(Transform target, Vector2 rootCenter, Vector2 size)
    {
        if (!(target is RectTransform rect) || !(rect.parent is RectTransform parent)) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = size;
        rect.position = _root.TransformPoint(rootCenter);
    }

    static void Overlay(Transform target, int order)
    {
        if (target == null) return;
        var canvas = target.GetComponent<Canvas>();
        if (canvas == null) canvas = target.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        if (target.GetComponent<GraphicRaycaster>() == null) target.gameObject.AddComponent<GraphicRaycaster>();
    }

    static string Localize(string english, string japanese, string chinese)
    {
        switch (AppSetting.Value.Language)
        {
            case SystemLanguage.Japanese: return japanese;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return chinese;
            default: return english;
        }
    }

    static bool ChineseLanguage => AppSetting.Value.Language == SystemLanguage.Chinese
        || AppSetting.Value.Language == SystemLanguage.ChineseSimplified
        || AppSetting.Value.Language == SystemLanguage.ChineseTraditional;
}
