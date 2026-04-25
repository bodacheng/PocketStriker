using System;
using UnityEngine;
using UnityEngine.UI;

public class GangbangHeroIcon : HeroIcon
{
    [SerializeField] BOButton minusBtn;
    [SerializeField] BOButton plusBtn;
    [SerializeField] Text count;

    private Func<int> countGet;

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
