using dataAccess;
using UnityEngine;

namespace mainMenu
{
    public partial class TheNineSlot : MonoBehaviour
    {
        // 基于当前九宫格对技能编辑进行合法判断 包括首发技能检测
        public SkillSet.SkillEditError CheckEditBasedOnCurrent(bool atLeastTwoExSkill)
        {
            var nineSkillIds = GetCurrentNineSlotAllSkillIds();
            return SkillSet.CheckEdit(nineSkillIds[0], nineSkillIds[1], nineSkillIds[2], 
                                        nineSkillIds[3], nineSkillIds[4], nineSkillIds[5],
                                        nineSkillIds[6], nineSkillIds[7], nineSkillIds[8],
                                        atLeastTwoExSkill);
        }
        
        // 基于角色存档对技能编辑进行合法判断. 必须接受完整validation检测
        public SkillSet.SkillEditError CheckEditAfterOneStoneRemoved(string unitInstanceID, string skillID)
        {
            var equipped = Stones.GetEquippingStones(unitInstanceID);
            string a1 = null, a2 = null, a3 = null, b1 = null, b2 = null, b3 = null, c1 = null, c2 = null, c3 = null;
            foreach (var t in equipped)
            {
                switch (t.slot)
                {
                    case "1":
                        a1 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "2":
                        a2 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "3":
                        a3 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "4":
                        b1 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "5":
                        b2 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "6":
                        b3 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "7":
                        c1 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "8":
                        c2 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                    case "9":
                        c3 = (t.SkillId != skillID) ? t.SkillId : "-1";
                        break;
                }
            }
            return SkillSet.CheckEdit(a1, a2, a3, b1, b2, b3, c1, c2, c3);
        }
    }
}