using dataAccess;
using UnityEngine;
using mainMenu;
using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using Skill;

public partial class SkillSet
{
    // 根据账户内拥有的技能石来补完当前九宫格
    public static SkillSet FixSkillSet(string type, SkillSet originSkillSet, bool baseOnAcc, string focusingUnitInstanceId)
    {
        var skillSet = SkillSetRandomFix(type, originSkillSet, 9, baseOnAcc, focusingUnitInstanceId);
        if (skillSet == null)
        {
            return null;
        }
        
        skillSet.SortNineAndTwo();
        return skillSet;
    }

    static SkillSet SkillSetRandomFix(string type, SkillSet _skillSet, int targetSlot, bool baseOnAcc, string focusingUnitInstanceId)
    {
        if (targetSlot == 0)
        {
            return _skillSet;
        }
        
        string skillId = SkillSetSlotUtility.GetSlot(
            targetSlot,
            _skillSet.a1,
            _skillSet.a2,
            _skillSet.a3,
            _skillSet.b1,
            _skillSet.b2,
            _skillSet.b3,
            _skillSet.c1,
            _skillSet.c2,
            _skillSet.c3);
        
        // 已经有技能石的格子不做修改
        if (SkillConfigTable.GetSkillConfigByRecordId(skillId) != null)
            return SkillSetRandomFix(type, _skillSet, targetSlot - 1, baseOnAcc, focusingUnitInstanceId);

        skillId = null;
        
        SkillStonesBox.StoneFilterForm filterForm;

        bool CheckLastFirstColumnSkill()
        {
            var list = new List<SkillConfig>();
            foreach (var startSkillId in SkillSetSlotUtility.StartSkillIds(_skillSet.a1, _skillSet.b1, _skillSet.c1))
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(startSkillId);
                if (skillConfig != null)
                    list.Add(skillConfig);
            }
            
            return list.Count == 2 && list.TrueForAll(x=> x.SP_LEVEL != 0);
        }
        
        if (CheckLastFirstColumnSkill() && SkillSetSlotUtility.IsStartSlot(targetSlot))
        {
            filterForm = new SkillStonesBox.StoneFilterForm
            {
                Type = type,
                ExType = new int[1] { 0 },
                Close = false,
                Near = false,
                Far = false
            };
            goto A;
        }
        
        var remainSpLevelList = RemainSlotSPLevelCal(_skillSet, focusingUnitInstanceId, _skillSet.GetAllInstanceIdsThatRelatesToCurrentSet());
        if (remainSpLevelList.Count > 1 && remainSpLevelList.Contains(0))
        {
            remainSpLevelList.Remove(0); // 必杀技优先
        }
        
        filterForm = new SkillStonesBox.StoneFilterForm
        {
            Type = type,
            ExType = remainSpLevelList.ToArray(),
            Close = false,
            Near = false,
            Far = false
        };

        A:

        var exceptSkIds = _skillSet.SkillIDList();
        foreach (var passiveSKill in UnitPassiveTable.GetPassiveSKillRecordIds())
        {
            if (!exceptSkIds.Contains(passiveSKill))
                exceptSkIds.Add(passiveSKill);
        }
        
        if (baseOnAcc)
        {
            var stoneInfoModel = Stones.SearchStoneForRandomSetFromAccount(filterForm, exceptSkIds);
            if (stoneInfoModel == null) // 如果账户已经没有符合要求的石头
            {
                Debug.Log("无法为" + targetSlot + "找到合适技能石");
                return null;
            }
            skillId = stoneInfoModel.SkillId;
        }
        else
        {
            var found = RandomSkillIDOfStone(filterForm, exceptSkIds);
            if (found == null) // 如果账户已经没有符合要求的石头
            {
                Debug.Log("无法为" + targetSlot + "找到合适技能石");
                return null;
            }
            skillId = found;
        }

        SkillSetSlotUtility.SetSlot(
            targetSlot,
            skillId,
            ref _skillSet.a1,
            ref _skillSet.a2,
            ref _skillSet.a3,
            ref _skillSet.b1,
            ref _skillSet.b2,
            ref _skillSet.b3,
            ref _skillSet.c1,
            ref _skillSet.c2,
            ref _skillSet.c3);
        
        if (targetSlot == 0)
        {
            var valR = CheckEdit(
                _skillSet.a1, _skillSet.a2, _skillSet.a3,
                _skillSet.b1, _skillSet.b2, _skillSet.b3,
                _skillSet.c1, _skillSet.c2, _skillSet.c3);
            
            return valR == SkillEditError.Perfect ? _skillSet : null;
        }
        return SkillSetRandomFix(type, _skillSet, targetSlot - 1, baseOnAcc, focusingUnitInstanceId);
    }
}
