using System.Collections.Generic;
using UnityEngine;
using dataAccess;
using Skill;
using UnityEngine.UI;

namespace mainMenu
{
    public partial class SkillStonesBox : MonoBehaviour
    {
        int _orderType = 0;

        private int OrderType
        {
            get => _orderType;
            set
            {
                _orderType = value;
                if (_orderType > 2)
                {
                    _orderType = 0;
                }
            }
        }
        
        // 功能本身直接放按钮上，但text要适配到SkillStonesBox上。
        public void SwitchOrder()
        {
            OrderType++;
            Selected.gameObject.SetActive(false);
            RestFilter();
        }
              
        List<string> Order(List<string> targets)
        {
            switch (OrderType)
            {
                case 0: // 以技能ID
                    orderButtonText.text = "Default";
                    return ByDevID(targets, 1);
                case 1: // 等级降序
                    orderButtonText.text = "Level DES";
                    return ByLevel(targets,0);
                case 2: // 等级升序
                    orderButtonText.text = "Level ASC";
                return ByLevel(targets, 1);
            }
            return targets;
        }
        
        List<string> ByDevID(List<string> targets, int order) //1:升序 0:降序 
        {
            for (int i = 0; i < targets.Count - 1; i++)
            {
                for (int j = 0; j < targets.Count - 1 - i; j++)
                {
                    StoneOfPlayerInfo myStone1 = Stones.Get(targets[j]);
                    StoneOfPlayerInfo myStone2 = Stones.Get(targets[j + 1]);
                    SkillConfig skillConfig1 = SkillConfigTable.GetSkillConfigByRecordId(myStone1.SkillId);
                    SkillConfig skillConfig2 = SkillConfigTable.GetSkillConfigByRecordId(myStone2.SkillId);

                    if (order == 1 ? int.Parse(skillConfig1.RECORD_ID) > int.Parse(skillConfig2.RECORD_ID) : int.Parse(skillConfig2.RECORD_ID) < int.Parse(skillConfig1.RECORD_ID))
                    {
                        string temp = targets[j];
                        targets[j] = targets[j + 1];
                        targets[j + 1] = temp;
                    }
                }
            }
            return targets;
        }
        
        // 等级升序降序
        List<string> ByLevel(List<string> targets, int order) //1:升序 0:降序 
        {
            for (int i = 0; i < targets.Count - 1; i++)
            {
                for (int j = 0; j < targets.Count - 1 - i; j++)
                {
                    var myStone1 = Stones.Get(targets[j]);
                    var myStone2 = Stones.Get(targets[j+1]);
                    
                    if (order == 1 ? myStone1.Level > myStone2.Level : myStone1.Level < myStone2.Level)
                    {
                        string temp = targets[j];
                        targets[j] = targets[j + 1];
                        targets[j + 1] = temp;
                    }
                }
            }
            return targets;
        }
    }
}