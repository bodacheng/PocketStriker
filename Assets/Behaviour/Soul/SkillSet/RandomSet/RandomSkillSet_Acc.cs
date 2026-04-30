using System.Collections.Generic;
using System.Linq;
using dataAccess;
using UnityEngine;
using Skill;
using mainMenu;
using System;
using MCombat.Shared.Behaviour;
using Random = System.Random;

public partial class SkillSet
{
    // 随机技能组
    public static SkillSet RandomSkillSet(string originSkill, bool baseOnAcc, SkillStonesBox.StoneFilterForm filterForm, bool noSpLimit = false)
    {
        var skillSet = new SkillSet();
        var originSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(originSkill);
        if (filterForm == null)
        {
            filterForm = CreateFilterForm(originSkillConfig != null ? originSkillConfig.TYPE : null);
        }

        skillSet = RandomSkillSetRec(skillSet, new List<int>()
        {
            1,2,3,4,5,6,7,8,9
        }, originSkillConfig, baseOnAcc, filterForm, noSpLimit);
        skillSet.SortNineAndTwo();
        return skillSet;
    }

    public static SkillSet RandomSkillSet(string type, string originSkill, bool baseOnAcc, SkillStonesBox.StoneFilterForm filterForm = null, bool noSpLimit = false)
    {
        if (filterForm == null)
        {
            filterForm = CreateFilterForm(type);
        }
        else if (string.IsNullOrEmpty(filterForm.Type))
        {
            filterForm.Type = type;
        }

        return RandomSkillSet(originSkill, baseOnAcc, filterForm, noSpLimit);
    }

    static SkillStonesBox.StoneFilterForm CreateFilterForm(string type)
    {
        return new SkillStonesBox.StoneFilterForm
        {
            Type = type
        };
    }

    static SkillStonesBox.StoneFilterForm DecideRemainForm(SkillSet skillSet, SkillStonesBox.StoneFilterForm form, int targetSlot, bool noSpLimit = false)
    {
        if (form == null)
        {
            form = CreateFilterForm(null);
        }

        if (noSpLimit)
        {
            return form;
        }

        var currentStartSkills = SkillSetSlotUtility.StartSkillIds(skillSet.a1, skillSet.b1, skillSet.c1);

        if (currentStartSkills.Count == 2 && SkillSetSlotUtility.IsStartSlot(targetSlot))
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
                    Type = form.Type,
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
                    Type = form.Type,
                    ExType = RemainSlotSPLevelCal(skillSet).ToArray()
                };
            }
            return form;
        }

        form = new SkillStonesBox.StoneFilterForm
        {
            Type = form.Type,
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
    static SkillSet RandomSkillSetRec(SkillSet skillSet, List<int> remainSlots, SkillConfig origin, bool baseOnAcc,
        SkillStonesBox.StoneFilterForm filterForm, bool noSpLimit = false)
    {
        var random = new Random();
        int randomIndex = random.Next(remainSlots.Count); // Get a random index
        int targetSlot = remainSlots[randomIndex]; // Get the random item
        remainSlots.Remove(targetSlot);
        filterForm = DecideRemainForm(skillSet, filterForm, targetSlot, noSpLimit);

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
                    stoneInfoModel = Stones.SearchStoneForRandomSetFromAccount(CreateFilterForm(filterForm.Type), exceptSkIds);
                    if (stoneInfoModel == null)
                    {
                        Debug.Log("无法为" + targetSlot + "找到合适技能石");
                        return skillSet;
                    }
                }
                skillId = stoneInfoModel.SkillId;
            }
            else
            {
                var found = RandomSkillIDOfStone(filterForm, exceptSkIds);
                if (found == null) // 如果账户已经没有符合要求的石头
                {
                    found = RandomSkillIDOfStone(CreateFilterForm(filterForm.Type), exceptSkIds);
                    if (found == null)
                    {
                        Debug.Log("无法为" + targetSlot + "找到合适技能石");
                        return skillSet;
                    }
                }
                skillId = found;
            }
        }

        SkillSetSlotUtility.SetSlot(
            targetSlot,
            skillId,
            ref skillSet.a1,
            ref skillSet.a2,
            ref skillSet.a3,
            ref skillSet.b1,
            ref skillSet.b2,
            ref skillSet.b3,
            ref skillSet.c1,
            ref skillSet.c2,
            ref skillSet.c3);
        if (remainSlots.Count == 0)
        {
            return skillSet;
        }
        return RandomSkillSetRec(skillSet, remainSlots, origin, baseOnAcc, filterForm, noSpLimit);
    }
}
