using System;
using dataAccess;
using mainMenu;
using UnityEngine;
using UnityEngine.UI;

public class ResultTableNode : MonoBehaviour
{
    [SerializeField] Text name;
    [SerializeField] Text rate;
    [SerializeField] RectTransform iconT;
    [SerializeField] GameObject close, near, far;
    [SerializeField] GameObject Ex1Icon, Ex2Icon, Ex3Icon;
    bool arranging;
    bool presentationReady;

    public void Setup(string recordId, double weight)
    {
        var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(recordId);
        name.text = skillConfig != null ? skillConfig.SHOW_NAME : SkillNameTable.GetSkillName(recordId ?? string.Empty);
        rate.text = Math.Round(weight * 100, 2) + "%";
        if (skillConfig != null)
            IconForShow(recordId);
        SkillStoneDetail.ShowSKillRanges(close, near, far, skillConfig?.AIAttrs?.AI_MIN_DIS ?? -10, skillConfig?.AIAttrs?.AI_MAX_DIS ?? -10);
        SkillStoneDetail.ShowSkillStoneExType(Ex1Icon, Ex2Icon, Ex3Icon, skillConfig?.SP_LEVEL ?? -1);
        ApplyPresentation();
    }

    void ApplyPresentation()
    {
        var panel = GetComponent<RawImage>();
        if (panel != null)
        {
            panel.texture = null;
            panel.color = new Color(0.065f, 0.105f, 0.135f, 0.94f);
        }
        // Range and EX marks are information, not additional buttons.
        foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        foreach (var shadow in GetComponentsInChildren<Shadow>(true)) shadow.enabled = false;
        StyleText(name, 30, TextAnchor.MiddleLeft, new Color(0.96f, 0.94f, 0.86f));
        StyleText(rate, 30, TextAnchor.MiddleRight, new Color(0.96f, 0.78f, 0.43f));
        var fill = Resources.Load<Sprite>("UI/Preparation/PreparationButtonFill");
        foreach (var mark in new[] { close, near, far, Ex1Icon, Ex2Icon, Ex3Icon })
        {
            if (mark == null) continue;
            foreach (var image in mark.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Border Glow") { image.gameObject.SetActive(false); continue; }
                image.sprite = fill;
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
                image.type = Image.Type.Sliced;
                image.color = mark == close || mark == near || mark == far
                    ? new Color(0.16f, 0.25f, 0.29f) : new Color(0.78f, 0.62f, 0.32f);
            }
            foreach (var text in mark.GetComponentsInChildren<Text>(true))
                StyleText(text, 26, TextAnchor.MiddleCenter, new Color(0.94f, 0.92f, 0.83f));
            foreach (var raw in mark.GetComponentsInChildren<RawImage>(true))
            { raw.texture = null; raw.color = new Color(0.78f, 0.62f, 0.32f); }
            var layout = mark.transform.parent.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;
        }
        presentationReady = true;
        ArrangePresentation();
    }

    static void StyleText(Text text, int size, TextAnchor alignment, Color color)
    {
        text.fontSize = size;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = size - 4;
        text.resizeTextMaxSize = size;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    void OnRectTransformDimensionsChange()
    {
        if (presentationReady) ArrangePresentation();
    }

    void ArrangePresentation()
    {
        if (arranging || name == null || rate == null || iconT == null) return;
        arranging = true;
        try
        {
            var root = (RectTransform)transform;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 136);
            float width = root.rect.width;
            Place(iconT, 18, 20, 96, 96);
            Place(name.rectTransform, 134, 8, Mathf.Max(80, width - 310), 64);
            Place(rate.rectTransform, width - 160, 8, 140, 64);
            var rangeRoot = (RectTransform)close.transform.parent;
            var exRoot = (RectTransform)Ex1Icon.transform.parent;
            Place(exRoot, 134, 91, 146, 22);
            Place(rangeRoot, Mathf.Max(300, width - 266), 84, 246, 38);
            for (int i = 0; i < 3; i++)
            {
                Place((RectTransform)new[] { Ex1Icon, Ex2Icon, Ex3Icon }[i].transform, i * 46, 0, 36, 16);
                var mark = new[] { close, near, far }[i];
                Place((RectTransform)mark.transform, i * 82, 0, 72, 38);
                foreach (var label in mark.GetComponentsInChildren<Text>(true))
                {
                    label.rectTransform.anchorMin = Vector2.zero;
                    label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                }
            }
        }
        finally { arranging = false; }
    }

    static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    async void IconForShow(string skillID)
    {
        var item = await Stones.GenerateStoneModel(skillID, false);
        if (item == null) return;
        if (this == null || iconT == null)
        { Destroy(item.gameObject); return; }
        foreach (Transform child in iconT) Destroy(child.gameObject);
        item.transform.SetParent(iconT, false);
        item.gameObject.SetActive(true);
        var itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = itemRect.anchorMax = itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = Vector2.zero;
        item.transform.localScale = Vector3.one;
        item.GetComponent<RectTransform>().sizeDelta = iconT.sizeDelta;
    }
}
