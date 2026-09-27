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

    const float CardPadding = 18;
    const float ModeLabelWidth = 220;
    const float ModeLabelHeight = 56;
    const float CardContentBottom = 88;
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

        // Keep the mode in its own footer. Parenting it to the stage number
        // inherited a tiny text box and let the frame run into the rewards.
        var flag = Instantiate(prefab, transform);
        var rect = flag.GetComponent<RectTransform>();
        PlaceAtBottom(rect, new Vector2(CardPadding, CardPadding),
            new Vector2(ModeLabelWidth, ModeLabelHeight));
        rect.localScale = Vector3.one;

        var background = flag.GetComponent<Image>();
        var accent = background.color;
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.color = new Color(0.025f, 0.05f, 0.085f, 1);
        var border = flag.AddComponent<Outline>();
        border.effectColor = accent;
        border.effectDistance = new Vector2(1.5f, -1.5f);
        foreach (var graphic in flag.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        foreach (var text in flag.GetComponentsInChildren<Text>(true))
        {
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12, 4);
            text.rectTransform.offsetMax = new Vector2(-12, -4);
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 32;
            text.resizeTextForBestFit = false;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }
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

    void LayoutAdventureCard()
    {
        if (_modeFlag == null || _layingOutAdventureCard) return;
        _layingOutAdventureCard = true;
        try
        {
            PlaceAtBottom(id.rectTransform, new Vector2(CardPadding, CardContentBottom), new Vector2(100, 100));
            id.fontSize = 36;
            id.resizeTextForBestFit = false;
            PlaceAtBottom((RectTransform)rewardUI.transform, new Vector2(130, CardContentBottom), new Vector2(120, 100));

            // The metadata column and enemy portraits have separate bounds.
            // Refit the portraits after the parent layout chooses the card width.
            iconsT.anchorMin = Vector2.zero;
            iconsT.anchorMax = Vector2.one;
            iconsT.offsetMin = new Vector2(CardPadding * 2 + ModeLabelWidth, CardContentBottom);
            iconsT.offsetMax = new Vector2(-CardPadding, -12);
            var iconLayout = iconsT.GetComponent<HorizontalLayoutGroup>();
            if (iconLayout != null) iconLayout.spacing = 16;
            var visibleIcons = iconsT.Cast<Transform>().Where(child => child.gameObject.activeSelf).ToList();
            if (visibleIcons.Count > 0)
            {
                var availableWidth = iconsT.rect.width - 16 * (visibleIcons.Count - 1);
                var iconSize = Mathf.Max(1, Mathf.Min(100, iconsT.rect.height, availableWidth / visibleIcons.Count));
                foreach (var icon in visibleIcons)
                    ((RectTransform)icon).sizeDelta = new Vector2(iconSize, iconSize);
            }

            LayoutCriticalGaugeFlag(enemyDoubleExModeFlg);
            LayoutCriticalGaugeFlag(enemyInfiniteExModeFlg);
        }
        finally
        {
            _layingOutAdventureCard = false;
        }
    }

    static void LayoutCriticalGaugeFlag(GameObject flag)
    {
        if (flag == null) return;
        var rect = (RectTransform)flag.transform;
        var parent = (RectTransform)rect.parent;
        parent.anchorMin = parent.anchorMax = new Vector2(1, 0);
        parent.pivot = new Vector2(1, 0);
        parent.anchoredPosition = new Vector2(-CardPadding, CardPadding);
        parent.sizeDelta = new Vector2(120, ModeLabelHeight);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    
    public void ChangeColorOfIcons(bool on)
    {
        var buttonImage = GetComponent<Image>();
        buttonImage.color = new Color(buttonImage.color.r, buttonImage.color.g, buttonImage.color.b, on ? 1 : 0.3f);
        id.color = new Color(id.color.r, id.color.g, id.color.b, on ? 1 : 0.65f);
        // Locked stages still need an opaque, legible battle-type label.
        if (_modeFlag != null)
            _modeFlag.alpha = 1;
        button.interactable = on;
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
