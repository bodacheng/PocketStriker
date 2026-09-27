using System;
using dataAccess;
using mainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace mainMenu
{
    public class StoneLevelUpInfoItem : MonoBehaviour
    {
        [SerializeField] RectTransform iconT;
        [SerializeField] Text name;
        [SerializeField] Text level;
        [SerializeField] Text upLevel;

        public void Setup(SkillStoneLevelUpForm form)
        {
            var info = Stones.Get(form.targetStoneID);
            var config = SkillConfigTable.GetSkillConfigByRecordId(info.SkillId);
            IconForShow(config.RECORD_ID);
            name.text = config.SHOW_NAME;
            level.text = info.Level.ToString();
            upLevel.text = "+" + (form.stoneInstances.Count / 4).ToString();
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
}