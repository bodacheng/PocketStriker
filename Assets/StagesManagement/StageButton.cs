using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public partial class StageButton : MonoBehaviour
{
    [SerializeField] BOButton button;
    [SerializeField] HeroIcon unitIconPrefab;
    [SerializeField] GangbangHeroIcon gangbangIconPrefab;
    [SerializeField] RectTransform iconsT;
    [SerializeField] Text id;
    [SerializeField] RewardUI rewardUI;
    [SerializeField] GameObject enemyDoubleExModeFlg;
    [SerializeField] GameObject enemyInfiniteExModeFlg;

    CanvasGroup _modeFlag;
    bool _layingOutAdventureCard;

    const float CardPadding = 28;
    const float MetadataWidth = 175;
    const float PortraitGap = 14;
    const float FooterBottom = 22;
    const float FooterHeight = 34;
    const float EnergyLabelWidth = 96;
    const float CardMinimumHeight = 200;
    
    public Button Button => button;
    public RewardUI RewardUI => rewardUI;
    
    public CriticalGaugeMode CriticalGaugeMode
    {
        set
        {
            enemyDoubleExModeFlg.SetActive(value == CriticalGaugeMode.DoubleGain);
            enemyInfiniteExModeFlg.SetActive(value == CriticalGaugeMode.Unlimited);
        }
    }

    private int stageNo;
    public int StageNo
    {
        get=> stageNo;
        set
        {
            stageNo = value;
            id.text = value.ToString();
        }
    }

    public void SetFightMode(int mode)
    {
        if (_modeFlag != null)
        {
            _modeFlag.gameObject.SetActive(false);
            Destroy(_modeFlag.gameObject);
        }
        var prefabName = mode == AdventureModeRules.EvolutionMode ? "EvolutionModeFlg"
            : mode == AdventureModeRules.MultiMode ? "MultiModeFlg" : "RotationModeFlg";
        var prefab = Resources.Load<GameObject>("DummyLayerSystem/Common/" + prefabName);
        if (prefab == null) return;

        var flag = Instantiate(prefab, transform);
        flag.transform.localScale = Vector3.one;
        var background = flag.GetComponent<Image>();
        var accent = Color.Lerp(background.color, Color.white, 0.65f);
        background.enabled = false;
        foreach (var graphic in flag.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        foreach (var text in flag.GetComponentsInChildren<Text>(true))
        {
            Stretch(text.rectTransform);
            text.color = accent;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 28;
            text.resizeTextForBestFit = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            StyleLabelShadow(text);
        }
        // Tone down the decorative fill so portraits and labels read over the scene.
        var panel = transform.Find("bg").GetComponent<Image>();
        panel.transform.SetAsFirstSibling();
        panel.color = new Color(0.06f, 0.12f, 0.17f, 0.9f);
        StyleCriticalGaugeFlag(enemyDoubleExModeFlg, "StageEnergyDouble", new Color(1, 0.86f, 0.4f, 1));
        StyleCriticalGaugeFlag(enemyInfiniteExModeFlg, "StageEnergyUnlimited", new Color(1, 0.55f, 0.7f, 1));
        _modeFlag = flag.AddComponent<CanvasGroup>();
        _modeFlag.blocksRaycasts = false;

        var layout = GetComponent<LayoutElement>();
        if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
        layout.minHeight = CardMinimumHeight;
        layout.preferredHeight = CardMinimumHeight;
        LayoutAdventureCard();
    }

    void OnRectTransformDimensionsChange()
    {
        LayoutAdventureCard();
    }

    static void PlaceAtBottom(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void StyleLabelShadow(Text text)
    {
        var shadow = text.GetComponent<Shadow>();
        if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.75f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    static void StyleCriticalGaugeFlag(GameObject flag, string languageCode, Color color)
    {
        if (flag == null) return;
        foreach (var image in flag.GetComponentsInChildren<Image>(true))
        {
            image.enabled = false;
            image.raycastTarget = false;
        }
        foreach (var converter in flag.GetComponentsInChildren<LanguageConverter>(true))
            converter.ChangeAtOnce(languageCode);
        foreach (var label in flag.GetComponentsInChildren<Text>(true))
        {
            Stretch(label.rectTransform);
            label.color = color;
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = 18;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            StyleLabelShadow(label);
        }
    }

    void LayoutAdventureCard()
    {
        if (_modeFlag == null || _layingOutAdventureCard) return;
        _layingOutAdventureCard = true;
        try
        {
            var root = (RectTransform)transform;
            var centerY = root.rect.height * 0.5f;
            PlaceAtBottom(id.rectTransform, new Vector2(CardPadding, centerY + 22), new Vector2(MetadataWidth, 50));
            id.fontSize = 32;
            id.resizeTextForBestFit = false;
            id.alignment = TextAnchor.MiddleLeft;
            PlaceAtBottom((RectTransform)_modeFlag.transform,
                new Vector2(CardPadding, centerY - 20), new Vector2(MetadataWidth, 38));

            // The enemy row is centered against the whole card, independently of rewards.
            iconsT.anchorMin = Vector2.zero;
            iconsT.anchorMax = Vector2.one;
            iconsT.offsetMin = new Vector2(CardPadding + MetadataWidth + 110, CardPadding);
            iconsT.offsetMax = new Vector2(-CardPadding, -CardPadding);
            var iconLayout = iconsT.GetComponent<HorizontalLayoutGroup>();
            if (iconLayout != null)
            {
                iconLayout.spacing = PortraitGap;
                iconLayout.childAlignment = TextAnchor.MiddleCenter;
                iconLayout.childControlWidth = iconLayout.childControlHeight = false;
                iconLayout.childForceExpandWidth = iconLayout.childForceExpandHeight = false;
                iconLayout.padding = new RectOffset();
            }
            var visibleIcons = iconsT.Cast<Transform>().Where(child => child.gameObject.activeSelf).ToList();
            if (visibleIcons.Count > 0)
            {
                var availableWidth = iconsT.rect.width - PortraitGap * (visibleIcons.Count - 1);
                var iconSize = Mathf.Max(1, Mathf.Min(100, iconsT.rect.height, availableWidth / visibleIcons.Count));
                foreach (var icon in visibleIcons)
                    ((RectTransform)icon).sizeDelta = new Vector2(iconSize, iconSize);
            }

            LayoutRewards();
            LayoutCriticalGaugeFlag(enemyDoubleExModeFlg);
            LayoutCriticalGaugeFlag(enemyInfiniteExModeFlg);
        }
        finally
        {
            _layingOutAdventureCard = false;
        }
    }

    void LayoutRewards()
    {
        var root = (RectTransform)rewardUI.transform;
        foreach (var graphic in rewardUI.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        var layout = root.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        var rows = root.Cast<RectTransform>().ToArray();
        var widths = rows.Select(row =>
        {
            var count = row.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.transform.parent == row);
            var mark = row.Find("GotMark");
            var digitsWidth = Mathf.Max(40, (count?.text.Length ?? 2) * 12.5f);
            return 36 + digitsWidth + (mark != null && mark.gameObject.activeSelf ? 24 : 0);
        }).ToArray();
        const float rewardGap = 12;
        var totalWidth = widths.Sum() + rewardGap * Mathf.Max(0, rows.Length - 1);
        PlaceAtBottom(root, new Vector2(CardPadding, FooterBottom), new Vector2(totalWidth, FooterHeight));
        var left = 0f;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var rowWidth = widths[index];
            PlaceAtBottom(row, new Vector2(left, 0), new Vector2(rowWidth, FooterHeight));
            foreach (var child in row.GetComponentsInChildren<RectTransform>(true).Where(child => child.parent == row))
            {
                var count = child.GetComponent<Text>();
                if (count != null)
                {
                    var mark = row.Find("GotMark");
                    var markWidth = mark != null && mark.gameObject.activeSelf ? 24 : 0;
                    PlaceAtBottom(child, new Vector2(36, 0), new Vector2(rowWidth - 36 - markWidth, FooterHeight));
                    count.fontSize = 24;
                    count.resizeTextForBestFit = true;
                    count.resizeTextMinSize = 16;
                    count.resizeTextMaxSize = 24;
                    count.alignment = TextAnchor.MiddleLeft;
                    count.horizontalOverflow = HorizontalWrapMode.Wrap;
                    count.verticalOverflow = VerticalWrapMode.Truncate;
                }
                else if (child.name == "GotMark")
                    PlaceAtBottom(child, new Vector2(rowWidth - 22, 6), new Vector2(22, 22));
                else
                    PlaceAtBottom(child, new Vector2(0, 1), new Vector2(32, 32));
            }
            left += rowWidth + rewardGap;
        }
    }

    void LayoutCriticalGaugeFlag(GameObject flag)
    {
        if (flag == null) return;
        var rect = (RectTransform)flag.transform;
        var parent = (RectTransform)rect.parent;
        var root = (RectTransform)transform;
        PlaceAtBottom(parent, new Vector2(CardPadding + MetadataWidth + 8, root.rect.height * 0.5f - 20),
            new Vector2(EnergyLabelWidth, 38));
        Stretch(rect);
    }

    public void ChangeColorOfIcons(bool on)
    {
        var buttonImage = GetComponent<Image>();
        buttonImage.color = new Color(buttonImage.color.r, buttonImage.color.g, buttonImage.color.b, on ? 1 : 0.3f);
        id.color = new Color(id.color.r, id.color.g, id.color.b, on ? 1 : 0.65f);
        // Keep the battle type legible while the locked card border dims.
        if (_modeFlag != null)
            _modeFlag.alpha = 1;
        button.interactable = on;
        // ArcadeTop assigns the actual award values and claimed state just before this call.
        LayoutAdventureCard();
    }
    
    public void LoadUnitIcons(List<UnitInfo> units, Func<UnitInfo, UniTask> iconButtonFeature, bool clickBoss = false, Func<bool> isCurrent = null)
    {
        var heroIcons = UnitInfosShow(units, 
            async (x) =>
            {
                if (this == null || (isCurrent != null && !isCurrent())) return;
                var targetUnitInfo = units.FirstOrDefault(info => info.id == x);
                if (targetUnitInfo != null) await iconButtonFeature(targetUnitInfo);
            },
            iconsT
        );
        
        for (var i = 0; i < heroIcons.Count; i++)
        {
            var heroIcon = heroIcons[i];
            if (clickBoss && i == 0)
            {
                heroIcon.iconButton.onClick.Invoke();
            }
        }
        LayoutAdventureCard();
    } 
    
    List<HeroIcon> UnitInfosShow(List<UnitInfo> heroSets, Action<string> iconFeature, RectTransform showT)
    {
        foreach (Transform t in showT)
        {
            t.gameObject.SetActive(false);
            Destroy(t.gameObject);
        }
        var icons = new List<HeroIcon>();
        foreach (var unitInfo in heroSets)
        {
            void load(UnitInfo unitInfo)
            {
                var v = HeroIcon.ArrangeHeroIconToParent(unitIconPrefab, unitInfo, iconFeature, showT);
                icons.Add(v);
            }
            load(unitInfo);
        }
        for (var i = 0; i < icons.Count; i++)
        {
            icons[i].iconButton.targetGraphic.raycastTarget = true;
        }
        return icons;
    }
}
