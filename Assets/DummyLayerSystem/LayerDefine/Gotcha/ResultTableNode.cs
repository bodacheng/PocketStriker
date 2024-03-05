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

    public void Setup(string recordId, double weight)
    {
        var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(recordId);
        name.text = SkillNameTable.GetSkillName(recordId);
        rate.text = Math.Round(weight * 100, 2) + "%";
        IconForShow(recordId);
        SkillStoneDetail.ShowSKillRanges(close, near, far, skillConfig.AIAttrs.AI_MIN_DIS, skillConfig.AIAttrs.AI_MAX_DIS);
        SkillStoneDetail.ShowSkillStoneExType(Ex1Icon, Ex2Icon, Ex3Icon, skillConfig.SP_LEVEL);
    }
    
    // 额外生成一个技能石图像
    async void IconForShow(string skillID)
    {
        var item = await Stones.GenerateStoneModel(skillID, false);
        foreach (Transform child in iconT) 
        {
            Destroy(child.gameObject);
        }
        item.transform.SetParent(iconT);
        item.gameObject.SetActive(true);
        item.transform.localPosition = Vector3.zero;
        item.transform.localScale = Vector3.one;
        item.transform.GetComponent<RectTransform>().sizeDelta = iconT.transform.GetComponent<RectTransform>().sizeDelta;
    }
}