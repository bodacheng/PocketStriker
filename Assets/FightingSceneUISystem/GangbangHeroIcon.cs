using System;
using UnityEngine;
using UnityEngine.UI;

public class GangbangHeroIcon : HeroIcon
{
    [SerializeField] BOButton minusBtn;
    [SerializeField] BOButton plusBtn;
    [SerializeField] Text count;

    private Func<int> countGet;

    /// <summary>Portrait above a regular minus/count/plus row for the portrait preparation rails.</summary>
    public void ApplyVerticalPreparationStyle(float portraitSize, float railWidth, Font font, Color accent)
    {
        var rect = (RectTransform)transform;
        rect.sizeDelta = Vector2.one * portraitSize;
        ApplyPreparationStyle();
        SetPreparationScrollRect(GetComponentInParent<ScrollRect>());
        RefreshPresentationSize();
        float buttonWidth = 50;
        float rowLeft = (portraitSize - railWidth) * 0.5f;
        StyleCountButton(minusBtn, "-", rowLeft, portraitSize + 12, buttonWidth, font, accent);
        StyleCountButton(plusBtn, "+", rowLeft + railWidth - buttonWidth, portraitSize + 12, buttonWidth, font, accent);
        var countRect = count.rectTransform;
        PlaceControl(countRect, rowLeft + buttonWidth + 2, portraitSize + 12, railWidth - buttonWidth * 2 - 4, 48);
        count.fontSize = 28;
        count.resizeTextForBestFit = false;
        count.alignment = TextAnchor.MiddleCenter;
        count.color = new Color(0.88f, 0.93f, 0.95f);
        count.raycastTarget = false;
        foreach (var shadow in count.GetComponents<Shadow>()) shadow.enabled = false;
    }

    static void StyleCountButton(BOButton button, string caption, float left, float top, float width, Font font, Color accent)
    {
        if (button == null) return;
        PlaceControl((RectTransform)button.transform, left, top, width, 48);
        var label = button.transform.Find("PreparationCountLabel")?.GetComponent<Text>();
        if (label == null)
        {
            foreach (var image in button.GetComponentsInChildren<Image>(true)) image.enabled = false;
            var node = new GameObject("PreparationCountLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            node.layer = button.gameObject.layer;
            node.transform.SetParent(button.transform, false);
            label = node.GetComponent<Text>();
            label.font = font;
        }
        label.text = caption;
        PreparationButtonSkin.Apply(button, label, accent, false, 28);
        PreparationButtonSkin.Fit(label.rectTransform, 4, 2);
    }

    static void PlaceControl(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    void SetUp(Func<int, int> countSet, Func<int> countGet, bool enableCountSet = true)
    {
        this.countGet = countGet;
        RefreshCount();
        if (enableCountSet)
        {
            void Plus()
            {
                var currentCount = countGet();
                countSet(currentCount + 1);
                RefreshCount();
            }
            plusBtn.SetListener(Plus);
            plusBtn.onHold.AddListener(Plus);

            void Minus()
            {
                var currentCount = countGet();
                countSet(currentCount - 1);
                RefreshCount();
            }

            minusBtn.SetListener(Minus);
            minusBtn.onHold.AddListener(Minus);
        }
        else
        {
            plusBtn.gameObject.SetActive(false);
            minusBtn.gameObject.SetActive(false);
        }
    }

    public void RefreshCount()
    {
        if (countGet == null)
        {
            return;
        }

        var value = countGet();
        count.text = value.ToString();
        if (value > 0)
        {
            LightOn();
        }
        else
        {
            Grey();
        }
    }

    public static GangbangHeroIcon ArrangeGangbangHeroIconToParent(
        Func<int, int> teamCountSet, Func<int> teamCountGet,
        GangbangHeroIcon prefab, UnitInfo unitInfo, Action<string> iconBehaviour,
        RectTransform T, bool withSkillCheck = false, bool enableCountSet = true, bool showIllegalFlag = false, float iconSize = 100)
    {
        var icon = Instantiate(prefab);
        var unitConfig = Units.GetUnitConfig(unitInfo.r_id);
        icon.unitConfig = unitConfig;
        icon.InstanceID = unitInfo.id;
        icon.ChangeIcon(unitInfo, withSkillCheck, teamCountGet);
        icon.GetComponent<RectTransform>().sizeDelta = new Vector2(iconSize,iconSize);
        icon.SetUp(teamCountSet, teamCountGet, enableCountSet);
        icon.transform.SetParent(T);
        icon.transform.localPosition = Vector3.one;
        icon.transform.localScale = Vector3.one;
        if (icon.WarnFlag != null)
            icon.WarnFlag.SetActive(showIllegalFlag && unitInfo.set.CheckEdit() != SkillSet.SkillEditError.Perfect);
        icon.iconButton.interactable = true;
        icon.iconButton.SetListener(
            ()=>
            {
                iconBehaviour(unitInfo.id);
            }
        );
        icon.gameObject.SetActive(true);
        return icon;
    }
}
