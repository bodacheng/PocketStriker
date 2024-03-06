using System.Collections.Generic;
using UnityEngine;
using dataAccess;

namespace mainMenu
{
    public partial class TheNineSlot : MonoBehaviour
    {
        List<StoneOfPlayerInfo> GetMyStonesOnNineSlot()
        {
            var returnValue = new List<StoneOfPlayerInfo>();
            var instanceIds = GetUsingStonesId();
            for (var i = 0; i < instanceIds.Count; i++)
            {
                returnValue.Add(Stones.Get(instanceIds[i]));
            }
            return returnValue;
        }
        
        // 获取当前九宫格内技能石存档id, 长度为当前九宫格内技能石数量
        List<string> GetUsingStonesId()
        {
            A1DragAndDropCell.UpdateMyItem();
            A2DragAndDropCell.UpdateMyItem();
            A3DragAndDropCell.UpdateMyItem();
            B1DragAndDropCell.UpdateMyItem();
            B2DragAndDropCell.UpdateMyItem();
            B3DragAndDropCell.UpdateMyItem();
            C1DragAndDropCell.UpdateMyItem();
            C2DragAndDropCell.UpdateMyItem();
            C3DragAndDropCell.UpdateMyItem();
            
            var instanceIds = new List<string>();
            var A1 = A1DragAndDropCell.GetItem()?.instanceId;
            var A2 = A2DragAndDropCell.GetItem()?.instanceId;
            var A3 = A3DragAndDropCell.GetItem()?.instanceId;
            var B1 = B1DragAndDropCell.GetItem()?.instanceId;
            var B2 = B2DragAndDropCell.GetItem()?.instanceId;
            var B3 = B3DragAndDropCell.GetItem()?.instanceId;
            var C1 = C1DragAndDropCell.GetItem()?.instanceId;
            var C2 = C2DragAndDropCell.GetItem()?.instanceId;
            var C3 = C3DragAndDropCell.GetItem()?.instanceId;
            
            if (A1 != null)
                instanceIds.Add(A1);
            if (A2 != null)
                instanceIds.Add(A2);
            if (A3 != null)
                instanceIds.Add(A3);
            if (B1 != null)
                instanceIds.Add(B1);
            if (B2 != null)
                instanceIds.Add(B2);
            if (B3 != null)
                instanceIds.Add(B3);
            if (C1 != null)
                instanceIds.Add(C1);
            if (C2 != null)
                instanceIds.Add(C2);
            if (C3 != null)
                instanceIds.Add(C3);
            return instanceIds;
        }

        public SkillSet GetCurrentNineAndTwo()
        {
            var returnValue = new SkillSet();
            var A1 = A1DragAndDropCell.GetItem() != null ? A1DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var A2 = A2DragAndDropCell.GetItem() != null ? A2DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var A3 = A3DragAndDropCell.GetItem() != null ? A3DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var B1 = B1DragAndDropCell.GetItem() != null ? B1DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var B2 = B2DragAndDropCell.GetItem() != null ? B2DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var B3 = B3DragAndDropCell.GetItem() != null ? B3DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var C1 = C1DragAndDropCell.GetItem() != null ? C1DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var C2 = C2DragAndDropCell.GetItem() != null ? C2DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;
            var C3 = C3DragAndDropCell.GetItem() != null ? C3DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : null;

            returnValue.a1 = A1;
            returnValue.a2 = A2;
            returnValue.a3 = A3;
            returnValue.b1 = B1;
            returnValue.b2 = B2;
            returnValue.b3 = B3;
            returnValue.c1 = C1;
            returnValue.c2 = C2;
            returnValue.c3 = C3;
            
            return returnValue;
        }
        
        // 返回的是技能定义ID，长度固定为9
        public List<string> GetCurrentNineSlotAllSkillIds()
        {
            var nineSkillIDs = new List<string>();
            var A1 = A1DragAndDropCell.GetItem() != null ? A1DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var A2 = A2DragAndDropCell.GetItem() != null ? A2DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var A3 = A3DragAndDropCell.GetItem() != null ? A3DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var B1 = B1DragAndDropCell.GetItem() != null ? B1DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var B2 = B2DragAndDropCell.GetItem() != null ? B2DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var B3 = B3DragAndDropCell.GetItem() != null ? B3DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var C1 = C1DragAndDropCell.GetItem() != null ? C1DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var C2 = C2DragAndDropCell.GetItem() != null ? C2DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            var C3 = C3DragAndDropCell.GetItem() != null ? C3DragAndDropCell.GetItem()._SkillConfig.RECORD_ID : "-1";
            
            nineSkillIDs.Add(A1);
            nineSkillIDs.Add(A2);
            nineSkillIDs.Add(A3);
            nineSkillIDs.Add(B1);
            nineSkillIDs.Add(B2);
            nineSkillIDs.Add(B3);
            nineSkillIDs.Add(C1);
            nineSkillIDs.Add(C2);
            nineSkillIDs.Add(C3);
            return nineSkillIDs;
        }
        
        void ShowNineSlotExSurplus(int wholePoint)
        {
            int pointRemain = wholePoint / 10;
            var i = 0;
            for (i = 0; i < remainCharges.Count; i++)
            {
                if (i + 1 <= pointRemain)
                {
                    remainCharges[i].SetActive(true);
                }
                else
                {
                    remainCharges[i].SetActive(false);
                }
            }
            
            var tempCount = 0;
            for (i = 0; i < burdenCharges.Count; i++)
            {
                if (-i - 1 >= pointRemain)
                {
                    burdenCharges[i].SetActive(true);
                    tempCount++;
                }
                else
                {
                    burdenCharges[i].SetActive(false);
                }
            }
            
            float temp = tempCount / (float)burdenCharges.Count;
            //overHeatBar.gameObject.SetActive(tempCount > 0);
            overHeatBar.value = temp;
        }

        void RefreshCurrentHpBasedOnNineSlots()
        {
            var stoneList = GetMyStonesOnNineSlot();
            var level = new List<int>();
            var skillIDs = new List<string>();
            
            foreach(var one in stoneList)
            {
                level.Add(one.Level);
                skillIDs.Add(one.SkillId);
            }
            
            _HP.text = "HP:" + IniHp(skillIDs, level);
        }
                
        static float IniHp(List<string> skillIDs, List<int> level)
        {
            float wholeHp = 0;
            for (var index = 0; index < skillIDs.Count; index++)
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillIDs[index]);
                wholeHp += FightGlobalSetting.StoneHpCal(skillConfig.HP_WEIGHT, level[index]);
            }
            return wholeHp;
        }
    }
}