
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using dataAccess;


namespace mainMenu
{
    public class StoneUpdatesConfirm : UILayer
    {
        [SerializeField] private BOButton confirmBtn;
        [SerializeField] private BOButton cancelBtn;
        [SerializeField] private Text needGold;
        
        [SerializeField] VerticalLayoutGroup resultT;
        [SerializeField] StoneLevelUpInfoItem itemPrefab;
        
        public void ShowInfo(Action confirm, Action cancel, int needGD,
            List<SkillStoneLevelUpForm> updateAllStoneForms)
        {
            needGold.text = needGD.ToString();
            confirmBtn.SetListener(confirm);
            cancelBtn.SetListener(cancel);
            
            float rectHeight = 0;
            for (var i = 0; i < updateAllStoneForms.Count; i++)
            {
                var node = updateAllStoneForms[i];
                StoneLevelUpInfoItem item = Instantiate(itemPrefab);
                item.Setup(node);
                item.gameObject.transform.SetParent(resultT.transform);
                item.transform.localScale = Vector3.one;
                rectHeight += (item.transform.GetComponent<RectTransform>().rect.height + resultT.spacing);
            }
            
            resultT.GetComponent<RectTransform>().sizeDelta = 
                new Vector2(resultT.GetComponent<RectTransform>().sizeDelta.x, rectHeight);
        }
    }
}