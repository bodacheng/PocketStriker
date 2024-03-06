using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public partial class StageButton : MonoBehaviour
{
    public void LoadUnitIconsGangbang(List<UnitInfo> units, Func<string, int> TeamCountGet, 
        Func<UnitInfo, UniTask> iconButtonFeature, bool clickBoss = false)
    {
        var heroIcons = GangbangUnitInfosShow(units, TeamCountGet,
            async (x) =>
            {
                ProgressLayer.Loading(string.Empty);
                var targetUnitInfo = units.FirstOrDefault(info => info.id == x);
                await iconButtonFeature(targetUnitInfo);
                ProgressLayer.Close();
            },
            iconsT);
        for (var i = 0; i < heroIcons.Count; i++)
        {
            var heroIcon = heroIcons[i];
            if (clickBoss && i == 0)
            {
                heroIcon.iconButton.onClick.Invoke();
            }
        }
    } 
    
    List<GangbangHeroIcon> GangbangUnitInfosShow(List<UnitInfo> heroSets, Func<string, int> TeamCountGet, Action<string> iconButtonFeature, RectTransform showT)
    {
        foreach (Transform t in showT)
        {
            Destroy(t.gameObject);
        }
        var icons = new List<GangbangHeroIcon>();
        foreach (var unitInfo in heroSets)
        {
            void Load(UnitInfo unitInfo)
            {
                var v = GangbangHeroIcon.ArrangeGangbangHeroIconToParent
                    (null,()=>TeamCountGet(unitInfo.id), gangbangIconPrefab, unitInfo, iconButtonFeature, showT, false, false);
                icons.Add(v);
            }
            Load(unitInfo);
        }
        for (var i = 0; i < icons.Count; i++)
        {
            icons[i].iconButton.targetGraphic.raycastTarget = true;
        }
        return icons;
    }
}
