using System;
using UnityEngine;
using UnityEngine.UI;

public class GangbangHeroIcon : HeroIcon
{
    [SerializeField] BOButton minusBtn;
    [SerializeField] BOButton plusBtn;
    [SerializeField] Text count;
    
    void SetUp(Func<int, int> countSet, Func<int> countGet, bool enableCountSet = true)
    {
        count.text = countGet().ToString();
        if (enableCountSet)
        {
            void ShowActive(int newCount)
            {
                if (newCount > 0)
                {
                    LightOn();
                }
                else
                {
                    Grey();
                }
            }
            
            void Plus()
            {
                var currentCount = countGet();
                countSet(currentCount + 1);
                var newCount = countGet();
                count.text = newCount.ToString();
                ShowActive(newCount);
            }
            plusBtn.SetListener(Plus);
            plusBtn.onHold.AddListener(Plus);

            void Minus()
            {
                var currentCount = countGet();
                countSet(currentCount - 1);
                var newCount = countGet();
                count.text = newCount.ToString();
                ShowActive(newCount);
            }
            
            minusBtn.SetListener(Minus);
            minusBtn.onHold.AddListener(Minus);
            ShowActive(countGet());
        }
        else
        {
            plusBtn.gameObject.SetActive(false);
            minusBtn.gameObject.SetActive(false);
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
