using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Places battle explanations against live controls, in the battle layer's safe coordinates.</summary>
public sealed class BattleTutorialLayout : MonoBehaviour
{
    public sealed class Callout
    {
        public Text Label;
        public RectTransform Target;
        public RectTransform SecondaryTarget;
        public RectTransform Arrow;
        public RectTransform Panel;
        public Rect TargetBounds;
        public Vector2 Tip;
        internal RectTransform Group;
        internal RectTransform Line;
        internal RectTransform Frame;
        internal bool Vitals;
        internal bool Skill;
    }

    const float Margin = 24;
    const float Padding = 20;
    const float Gap = 80;
    static readonly Color Accent = new Color(1f, 0.9f, 0.36f, 1f);
    readonly List<Callout> callouts = new List<Callout>();
    readonly List<Text> centeredLabels = new List<Text>();
    readonly List<RectTransform> centeredPanels = new List<RectTransform>();
    readonly List<Callout> skills = new List<Callout>();
    readonly List<Vector2> skillSizes = new List<Vector2>();
    readonly List<Vector2> skillCenters = new List<Vector2>();
    readonly Vector3[] corners = new Vector3[4];
    RectTransform root;
    Func<RectTransform> healthTarget;
    Func<RectTransform> energyTarget;
    public IReadOnlyList<Callout> Callouts => callouts;
    public IReadOnlyList<Text> CenteredLabels => centeredLabels;
    public Rect SafeBounds { get; private set; }

    public void Initialize(RectTransform battleRoot, Func<RectTransform> healthBarTarget,
        Func<RectTransform> energyBarTarget = null)
    {
        healthTarget = healthBarTarget;
        energyTarget = energyBarTarget;
        if (root != null) return;
        root = battleRoot;
        foreach (var name in new[] { "Tutorial", "DreamComboTutorial" })
        {
            var overlay = root.Find(name);
            if (overlay == null) continue;
            foreach (var clamper in overlay.GetComponentsInChildren<UIPosClamper>(true))
            {
                // The old component moved the group once but retained landscape-sized child offsets.
                clamper.enabled = false;
                var group = clamper.Target;
                var label = group.GetComponentInChildren<Text>(true);
                var arrow = group.Find("arrow") as RectTransform;
                if (label == null || arrow == null) continue;
                Stretch(group);
                var callout = new Callout
                {
                    Group = group, Label = label, Arrow = arrow, Target = clamper.Source,
                    Vitals = group.parent.name == "UserHPEXBarIntro",
                    Skill = group.parent.name == "SkillTutorialLayer",
                    Panel = MakeImage(group, "ExplanationPanel", new Color(0.035f, 0.065f, 0.11f, 1f)),
                    Line = MakeImage(group, "PointerLine", Accent),
                    Frame = MakeFrame(group)
                };
                callout.Panel.SetAsFirstSibling();
                callout.Line.SetAsFirstSibling();
                callout.Frame.SetAsFirstSibling();
                arrow.GetComponent<Graphic>().raycastTarget = false;
                arrow.GetComponent<Graphic>().color = Accent;
                ConfigureText(label, 38);
                // This legacy icon duplicates the real Dream Combo button at an unrelated offset.
                foreach (Transform child in group)
                    if (child != label.transform && child != arrow && child != callout.Panel &&
                        child != callout.Line && child != callout.Frame)
                        child.gameObject.SetActive(false);
                callouts.Add(callout);
            }
        }
        foreach (var path in new[] { "Tutorial/IntroBegin/Text", "Tutorial/JustUseAI/Text" })
        {
            var label = root.Find(path)?.GetComponent<Text>();
            if (label == null) continue;
            ConfigureText(label, 48);
            centeredLabels.Add(label);
            var panel = MakeImage(label.transform.parent, "ExplanationPanel", new Color(0.035f, 0.065f, 0.11f, 1f));
            panel.SetSiblingIndex(label.transform.GetSiblingIndex());
            centeredPanels.Add(panel);
        }
        RefreshLayout();
    }

    void LateUpdate() => RefreshLayout();

    public void RefreshLayout()
    {
        if (root == null) return;
        SafeBounds = root.rect;
        if (PosCal.SafeAreaRect != null)
        {
            var safe = Bounds(PosCal.SafeAreaRect);
            SafeBounds = Rect.MinMaxRect(Mathf.Max(SafeBounds.xMin, safe.xMin), Mathf.Max(SafeBounds.yMin, safe.yMin),
                Mathf.Min(SafeBounds.xMax, safe.xMax), Mathf.Min(SafeBounds.yMax, safe.yMax));
        }
        var area = Rect.MinMaxRect(SafeBounds.xMin + Margin, SafeBounds.yMin + Margin,
            SafeBounds.xMax - Margin, SafeBounds.yMax - Margin);
        if (area.width <= 0 || area.height <= 0) return;
        for (int i = 0; i < centeredLabels.Count; i++)
        {
            var label = centeredLabels[i];
            if (!label.gameObject.activeInHierarchy) continue;
            var size = Measure(label, Mathf.Min(840, area.width), area.height);
            PlacePanel(label, centeredPanels[i], area.center, size);
        }
        skills.Clear();
        foreach (var callout in callouts)
        {
            if (!callout.Group.gameObject.activeInHierarchy) continue;
            if (callout.Vitals)
            {
                callout.Target = healthTarget?.Invoke();
                callout.SecondaryTarget = energyTarget?.Invoke();
            }
            bool available = callout.Target != null && callout.Target.gameObject.activeInHierarchy;
            callout.Arrow.gameObject.SetActive(available);
            callout.Line.gameObject.SetActive(available);
            callout.Frame.gameObject.SetActive(available);
            if (!available)
            {
                // Keep the explanation readable while a unit's UI is being populated, without pointing elsewhere.
                PlacePanel(callout.Label, callout.Panel, area.center, Measure(callout.Label, Mathf.Min(720, area.width), area.height));
                continue;
            }
            callout.TargetBounds = Bounds(callout.Target);
            if (callout.SecondaryTarget != null)
                callout.TargetBounds = Union(callout.TargetBounds, Bounds(callout.SecondaryTarget));
            if (callout.Skill) { skills.Add(callout); continue; }
            LayoutCallout(callout, area);
        }
        // The three staggered attack controls are closer than wrapped labels in some languages.
        // Keep their reading order aligned with the buttons without allowing label overlap.
        skills.Sort((a, b) => b.TargetBounds.center.y.CompareTo(a.TargetBounds.center.y));
        skillSizes.Clear();
        skillCenters.Clear();
        float ceiling = area.yMax;
        foreach (var callout in skills)
        {
            float width = Mathf.Clamp(callout.TargetBounds.xMin - Gap - area.xMin, 280, 640);
            var size = Measure(callout.Label, Mathf.Min(width, area.width), area.height);
            var center = new Vector2(area.xMin + size.x / 2,
                Mathf.Min(callout.TargetBounds.center.y, ceiling - size.y / 2));
            skillSizes.Add(size);
            skillCenters.Add(center);
            ceiling = center.y - size.y / 2 - 18;
        }
        float lift = skills.Count == 0 ? 0 : Mathf.Max(0, area.yMin - (ceiling + 18));
        for (int i = 0; i < skills.Count; i++)
        {
            var center = skillCenters[i] + Vector2.up * lift;
            PlacePanel(skills[i].Label, skills[i].Panel, center, skillSizes[i]);
            DrawPointer(skills[i], center, skillSizes[i]);
        }
    }

    void LayoutCallout(Callout callout, Rect area)
    {
        var target = callout.TargetBounds;
        bool toRight = target.center.x < area.center.x;
        float space = toRight ? area.xMax - target.xMax - Gap : target.xMin - Gap - area.xMin;
        float width = Mathf.Clamp(space, 320, 680);
        var size = Measure(callout.Label, Mathf.Min(width, area.width), area.height);
        var center = new Vector2(toRight ? target.xMax + Gap + size.x / 2 : target.xMin - Gap - size.x / 2, target.center.y);
        if (space < 320)
            center = new Vector2(area.center.x, target.center.y < area.center.y
                ? target.yMax + Gap + size.y / 2 : target.yMin - Gap - size.y / 2);
        center = Clamp(center, size, area);
        PlacePanel(callout.Label, callout.Panel, center, size);
        DrawPointer(callout, center, size);
    }

    Vector2 Measure(Text label, float width, float maxHeight)
    {
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - Padding * 2);
        // Legacy Text's preferredHeight omits leading used by the bundled CJK font
        // when truncation is enabled. Reserve a line so the final glyphs survive.
        float height = Mathf.Ceil(label.preferredHeight + label.fontSize * 1.5f) + Padding * 2;
        return new Vector2(width, Mathf.Min(maxHeight, height));
    }

    void PlacePanel(Text label, RectTransform panel, Vector2 center, Vector2 size)
    {
        Place(panel, center, size);
        Place(label.rectTransform, center, size - Vector2.one * Padding * 2);
    }

    void DrawPointer(Callout callout, Vector2 panelCenter, Vector2 panelSize)
    {
        var target = callout.TargetBounds;
        // Attack buttons have overlapping rectangular corners around their round
        // controls. Inset those outlines so adjacent highlights remain distinct.
        var frame = callout.Skill
            ? new Rect(target.position + Vector2.one * 14, target.size - Vector2.one * 28)
            : target;
        Vector2 direction = (target.center - panelCenter).normalized;
        callout.Tip = Edge(frame, panelCenter);
        var from = Edge(new Rect(panelCenter - panelSize / 2, panelSize), target.center);
        var end = callout.Tip - direction * 54;
        var line = end - from;
        Place(callout.Line, (from + end) / 2, new Vector2(line.magnitude, 3));
        callout.Line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg);
        Place(callout.Arrow, callout.Tip, new Vector2(42, 66));
        callout.Arrow.pivot = new Vector2(0.5f, 1f);
        callout.Arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        Place(callout.Frame, frame.center, frame.size + (callout.Skill ? Vector2.zero : Vector2.one * 12));
    }

    static Vector2 Edge(Rect rect, Vector2 toward)
    {
        var delta = toward - rect.center;
        float x = Mathf.Abs(delta.x) > 0.001f ? rect.width / 2 / Mathf.Abs(delta.x) : float.PositiveInfinity;
        float y = Mathf.Abs(delta.y) > 0.001f ? rect.height / 2 / Mathf.Abs(delta.y) : float.PositiveInfinity;
        return rect.center + delta * Mathf.Min(x, y);
    }

    void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = size;
        rect.position = root.TransformPoint(position);
    }

    Rect Bounds(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        Vector2 min = root.InverseTransformPoint(corners[0]), max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 point = root.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    static Vector2 Clamp(Vector2 center, Vector2 size, Rect area) => new Vector2(
        Mathf.Clamp(center.x, area.xMin + size.x / 2, area.xMax - size.x / 2),
        Mathf.Clamp(center.y, area.yMin + size.y / 2, area.yMax - size.y / 2));
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    static void ConfigureText(Text label, int size)
    {
        label.resizeTextForBestFit = false;
        label.fontSize = size;
        label.lineSpacing = 1.15f;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.color = Color.white;
    }
    static RectTransform MakeImage(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return (RectTransform)obj.transform;
    }
    static RectTransform MakeFrame(Transform parent)
    {
        var obj = new GameObject("TargetFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleTutorialFrame));
        obj.transform.SetParent(parent, false);
        var graphic = obj.GetComponent<BattleTutorialFrame>();
        graphic.color = Accent; graphic.raycastTarget = false;
        return (RectTransform)obj.transform;
    }
}

/// <summary>A hollow UI outline keeps the indicated control visible.</summary>
public sealed class BattleTutorialFrame : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        const float thickness = 3;
        AddRect(mesh, new Rect(rect.xMin, rect.yMin, rect.width, thickness));
        AddRect(mesh, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness));
        AddRect(mesh, new Rect(rect.xMin, rect.yMin + thickness, thickness, rect.height - thickness * 2));
        AddRect(mesh, new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, rect.height - thickness * 2));
    }
    void AddRect(VertexHelper mesh, Rect rect)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}
