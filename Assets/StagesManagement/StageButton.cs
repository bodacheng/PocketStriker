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
    
    public void ChangeColorOfIcons(bool on)
    {
        var buttonImage = GetComponent<Image>();
        buttonImage.color = new Color(buttonImage.color.r, buttonImage.color.g, buttonImage.color.b, on ? 1 : 0.3f);
        id.color = new Color(id.color.r, id.color.g, id.color.b, on ? 1 : 0.3f);
        button.interactable = on;
    }
    
    public void LoadUnitIcons(List<UnitInfo> units, Func<UnitInfo, UniTask> iconButtonFeature, bool clickBoss = false)
    {
        var heroIcons = UnitInfosShow(units, 
            async (x) =>
            {
                ProgressLayer.Loading(string.Empty);
                var targetUnitInfo = units.FirstOrDefault(info => info.id == x);
                await iconButtonFeature(targetUnitInfo);
                ProgressLayer.Close();
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