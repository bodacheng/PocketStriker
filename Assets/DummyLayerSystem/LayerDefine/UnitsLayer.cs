using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

namespace mainMenu
{
    public class UnitsLayer : UILayer
    {
        [Header("filter")]
        [SerializeField] UnitFilter filter;
        
        [Header("角色属性框")]
        [SerializeField] HeroIcon noMagic;
        
        [Header("选中框")]
        [SerializeField] GameObject selectedFrame;
        
        [Header("宠物栏parent")]
        [SerializeField] GridLayoutGroup grid;
        
        readonly List<string> typeOfUnitsIHave = new List<string>();
        readonly IDictionary<string, HeroIcon> heroIcons = new Dictionary<string, HeroIcon>();
        public ReactiveProperty<string> Selected { get; } = new ReactiveProperty<string> (null);
        private Action<string> onClick;
        public Action<string> OnClick => onClick;
        
        HeroIcon GetUnitIcon(string instanceID)
        {
            if (instanceID == null)
                return null;
            heroIcons.TryGetValue(instanceID, out var unitIcon);
            return unitIcon;
        }
        
        public void SetUnitsIconOnClick(Action<string> onClick)
        {
            this.onClick = onClick;
            foreach (var kv in heroIcons)
            {
                kv.Value.iconButton.SetListener(()=> { onClick.Invoke(kv.Key);});
            }
        }
        
        //icon的排列，显示   
        public void DisplayUnitIcons(IDictionary<string, UnitInfo> dic, bool clearBtnFeature, bool withSkillCheck = false)
        {
            Selected.Subscribe(x =>
            {
                var targetingIcon = GetUnitIcon(x);
                HeroIcon.SelectedFeature(targetingIcon?.transform, selectedFrame, 1f);
            }).AddTo(gameObject);
            
            grid.gameObject.SetActive(true);
            UnitIconsGenerate(dic, clearBtnFeature, withSkillCheck);
            foreach (var keyValuePair in heroIcons)
            {
                keyValuePair.Value.gameObject.SetActive(false);
            }
            var icons = filter.OrderIcons(heroIcons.Values.ToList());
            for (var i = 0; i < icons.Count; i++)
            {
                var targetingIcon = icons[i];
                if (targetingIcon == null)
                {
                    Debug.Log("严重错误");
                    return;
                }
                targetingIcon.gameObject.SetActive(true);
                targetingIcon.transform.SetParent(grid.transform);
                targetingIcon.transform.localScale = Vector3.one;
                targetingIcon.transform.localPosition = Vector3.zero;
            }

            SetGridGroupSize(grid, grid.transform.GetComponent<RectTransform>().offsetMin.x);
            displayUnitIconsAfterAction?.Invoke();
        }

        void AddUnitIcon(string instanceID, bool clearBtnFeature, bool withSkillCheck)
        {
            var unitInfo = dataAccess.Units.Get(instanceID);
            var unitConfig = Units.GetUnitConfig(unitInfo.r_id);
            if (unitConfig == null)
            {
                Debug.Log("unit ID:"+ unitInfo.r_id + " doesnt exist in this version");
                return;
            }
            
            var targetingIcon = GetUnitIcon(instanceID);
            if (targetingIcon == null)
            {
                targetingIcon = Instantiate(noMagic);
                targetingIcon.name = unitConfig.REAL_NAME + "_icon";
                targetingIcon.ChangeIcon(unitInfo, withSkillCheck);
                targetingIcon.InstanceID = instanceID;
                DicAdd<string, HeroIcon>.Add(heroIcons, instanceID, targetingIcon);
            }
            if (clearBtnFeature)
                targetingIcon.iconButton.onClick.RemoveAllListeners();
            if (!typeOfUnitsIHave.Contains(unitConfig.TYPE))
            {
                typeOfUnitsIHave.Add(unitConfig.TYPE);
            }
        }
        
        public void OnTypeChangeMyMonsterBox()
        {
            DisplayUnitIcons(dataAccess.Units.Dic, false);
        }
        
        void UnitIconsGenerate(IDictionary<string, UnitInfo> dic, bool clearButtonFeature, bool withSkillCheck)
        {
            foreach (var keyValuePair in dic)
            {
                AddUnitIcon(keyValuePair.Value.id, clearButtonFeature, withSkillCheck);
            }
            filter.RefreshTypeDropDown(typeOfUnitsIHave);
        }
        
        Action displayUnitIconsAfterAction;
        public void SetDisplayUnitIconsAfterAction(Action a)
        {
            this.displayUnitIconsAfterAction = a;
        }
        
        public void ForceClickUnit(string unitRId)
        {
            foreach (var iconKV in heroIcons)
            {
                iconKV.Value.iconButton.interactable = unitRId == iconKV.Value.unitConfig.RECORD_ID;
            }
        }
    }
}