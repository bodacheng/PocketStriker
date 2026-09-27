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

        var flag = Instantiate(prefab, id.transform);
        var rect = flag.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(140, 44);
        rect.localScale = Vector3.one;
        foreach (var graphic in flag.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        foreach (var text in flag.GetComponentsInChildren<Text>(true))
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 24;
        }
        _modeFlag = flag.AddComponent<CanvasGroup>();
        _modeFlag.blocksRaycasts = false;
    }
    
    public void ChangeColorOfIcons(bool on)
    {
        var buttonImage = GetComponent<Image>();
        buttonImage.color = new Color(buttonImage.color.r, buttonImage.color.g, buttonImage.color.b, on ? 1 : 0.3f);
        id.color = new Color(id.color.r, id.color.g, id.color.b, on ? 1 : 0.3f);
        if (_modeFlag != null)
            _modeFlag.alpha = on ? 1 : 0.3f;
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
