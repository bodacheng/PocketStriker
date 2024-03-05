using System.Collections.Generic;
using System.Linq;
using dataAccess;
using UnityEngine;
using Skill;
using mainMenu;
using NoSuchStudio.Common;

public partial class SkillSet
{
    // 随机技能组
    public static SkillSet RandomSkillSet(string type, string originSkill, bool baseOnAcc, SkillStonesBox.StoneFilterForm filterForm = null, bool noSpLimit = false)
    {
        var skillSet = new SkillSet();
        var originSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(originSkill);
        
        skillSet = RandomSkillSetRec(type, skillSet, new List<int>()
        {
            1,2,3,4,5,6,7,8,9
        }, originSkillConfig, baseOnAcc, filterForm, noSpLimit);
        skillSet.SortNineAndTwo();
        return skillSet;
    }
    
    static SkillStonesBox.StoneFilterForm DecideRemainForm(SkillSet skillSet, string type, SkillStonesBox.StoneFilterForm form, int targetSlot, bool noSpLimit = false)
    {
        if (noSpLimit)
        {
            return form;
        }
        
        var currentStartSkills = new List<string>();
        if (skillSet.a1 != null)
            currentStartSkills.Add(skillSet.a1);
        if (skillSet.b1 != null)
            currentStartSkills.Add(skillSet.b1);
        if (skillSet.c1 != null)
            currentStartSkills.Add(skillSet.c1);

        var startSlots = new List<int>() { 1, 4, 7 };
        if (currentStartSkills.Count == 2 && startSlots.Contains(targetSlot))
        {
            bool hasNormal = false;
            foreach (var skillId in currentStartSkills)
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillId);
                hasNormal = skillConfig.SP_LEVEL == 0;
            }

            if (!hasNormal)
            {
                form = new SkillStonesBox.StoneFilterForm
                {
                    Type = type,
                    ExType = new [] { 0 }
                };
                return form;
            }
        }
        
        // 必然从0开始，可能依次包括1，2，3
        bool useRemainSlotSPLevelCal = false;
        var remainSlotSpLevel = RemainSlotSPLevelCal(skillSet);
        if (form != null)
        {
            var formSps = form.ExType.ToList();
            foreach (var spLevel in formSps)
            {
                if (!remainSlotSpLevel.Contains(spLevel))
                    useRemainSlotSPLevelCal = true;
            }

            if (useRemainSlotSPLevelCal)
            {
                form = new SkillStonesBox.StoneFilterForm
                {
                    Type = type,
                    ExType = RemainSlotSPLevelCal(skillSet).ToArray()
                };
            }
            return form;
        }
        
        form = new SkillStonesBox.StoneFilterForm
        {
            Type = type,
            ExType = RemainSlotSPLevelCal(skillSet).ToArray()
        };

        return form;
    }
    
    /// <summary>
    /// 递归适配技能组
    /// </summary>
    /// <param name="type"></param>
    /// <param name="skillSet"></param>
    /// <param name="filterForm"></param>
    /// <param name="remainSlots"></param>
    /// <param name="origin"></param>
    /// <param name="baseOnAcc"></param>
    /// <returns></returns>
    static SkillSet RandomSkillSetRec(string type, SkillSet skillSet, List<int> remainSlots, SkillConfig origin, bool baseOnAcc, 
        SkillStonesBox.StoneFilterForm filterForm, bool noSpLimit = false)
    {
        var targetSlot = remainSlots.Random();
        remainSlots.Remove(targetSlot);
        filterForm = DecideRemainForm(skillSet, type, filterForm, targetSlot, noSpLimit);
        
        var exceptSkIds = skillSet.SkillIDList();
        foreach (var passiveSKill in UnitPassiveTable.GetPassiveSKillRecordIds())
        {
            if (!exceptSkIds.Contains(passiveSKill))
                exceptSkIds.Add(passiveSKill);
        }
        string skillId;
        if (remainSlots.Count == 8 && origin != null)
        {
            skillId = origin.RECORD_ID;
        }
        else
        {
            if (baseOnAcc)
            {
                var stoneInfoModel = Stones.SearchStoneForRandomSetFromAccount(filterForm, exceptSkIds);
                if (stoneInfoModel == null) // 如果账户已经没有符合要求的石头
                {
                    Debug.Log("无法为" + targetSlot + "找到合适技能石");
                    return skillSet;
                }
                skillId = stoneInfoModel.SkillId;
            }
            else
            {
                var found = RandomSkillIDOfStone(filterForm, exceptSkIds);
                if (found == null) // 如果账户已经没有符合要求的石头
                {
                    Debug.Log("无法为" + targetSlot + "找到合适技能石");
                    return skillSet;
                }
                skillId = found;
            }
        }
        
        switch (targetSlot)
        {
            case 1:
                skillSet.a1 = skillId;
                break;
            case 2:
                skillSet.a2 = skillId;
                break;
            case 3:
                skillSet.a3 = skillId;
                break;
            case 4:
                skillSet.b1 = skillId;
                break;
            case 5:
                skillSet.b2 = skillId;
                break;
            case 6:
                skillSet.b3 = skillId;
                break;
            case 7:
                skillSet.c1 = skillId;
                break;
            case 8:
                skillSet.c2 = skillId;
                break;
            case 9:
                skillSet.c3 = skillId;
                break;
        }
        if (remainSlots.Count == 0)
        {
            return skillSet;
        }
        return RandomSkillSetRec(type, skillSet, remainSlots, origin, baseOnAcc, filterForm, noSpLimit);
    }
}