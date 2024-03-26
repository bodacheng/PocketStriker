using System;
using System.Collections.Generic;
using dataAccess;
using Skill;

public partial class SkillSet
{
    /// <summary>
    /// 这个仅仅是一个辅助于技能编辑画面的工具，并不能用来判断角色实际装备中技能
    /// 实际装备中技能是用Stones.GetEquippingStones
    /// </summary>
    /// <returns></returns>
    public List<string> SkillIDList()
    {
        var ids = new List<string>();
        
        if (a1 != null)
            ids.Add(a1);
        if (a2 != null)
            ids.Add(a2);
        if (a3 != null)
            ids.Add(a3);
            
        if (b1 != null)
            ids.Add(b1);
        if (b2 != null)
            ids.Add(b2);
        if (b3 != null)
            ids.Add(b3);
            
        if (c1 != null)
            ids.Add(c1);
        if (c2 != null)
            ids.Add(c2);
        if (c3 != null)
            ids.Add(c3);
        
        return ids;
    }

    public List<string> GetAllInstanceIdsThatRelatesToCurrentSet()
    {
        List<string> instanceIds = new List<string>();
        foreach (var skillId in SkillIDList())
        {
            instanceIds.AddRange(Stones.GetMyStonesBySkillID(skillId));
        }

        return instanceIds;
    }
    
    // 获取平均技能等级
    public float GetAerLevel(List<float> levels)
    {
        float aver = 0;
        foreach (var t in levels)
        {
            aver += t;
        }
        return (float)Math.Round(aver / levels.Count, 1);
    }
    
    public static float INI_Hp(List<string> skillIds, float lv)
    {
        float wholeHp = 0;
        foreach (var skillId in skillIds)
        {
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillId);
            wholeHp += FightGlobalSetting.StoneHpCal(skillConfig.HP_WEIGHT, lv);
        }
        return wholeHp;
    }
    
    // 获取技能实体列表，调用必须在SortNineAndTwo之后
    public List<SkillEntity> SkillEntityList()
    {
        var behaviorTransitionSets = new List<SkillEntity>();
        
        if (A1 != null)
            behaviorTransitionSets.Add(A1);
        if (A2 != null)
            behaviorTransitionSets.Add(A2);
        if (A3 != null)
            behaviorTransitionSets.Add(A3);
        
        if (B1 != null)
            behaviorTransitionSets.Add(B1);
        if (B2 != null)
            behaviorTransitionSets.Add(B2);
        if (B3 != null)
            behaviorTransitionSets.Add(B3);
        
        if (C1 != null)
            behaviorTransitionSets.Add(C1);
        if (C2 != null)
            behaviorTransitionSets.Add(C2);
        if (C3 != null)
            behaviorTransitionSets.Add(C3);
            
        if (D != null)
            behaviorTransitionSets.Add(D);
        if (M != null)
            behaviorTransitionSets.Add(M);
        if (R != null)
            behaviorTransitionSets.Add(R);
        if (_empty != null)
            behaviorTransitionSets.Add(_empty);
        if (_victory != null)
            behaviorTransitionSets.Add(_victory);    
        if (_death != null)
            behaviorTransitionSets.Add(_death);
        if (_hit != null)
            behaviorTransitionSets.Add(_hit);
        if (_getUp != null)
            behaviorTransitionSets.Add(_getUp);
        if (_knockOff != null)
            behaviorTransitionSets.Add(_knockOff);
            
        return behaviorTransitionSets;
    }
    
    //下面的环节纯粹是针对SkillPrintOut的一些处理
    public IDictionary<int, SkillEntity> GetAttack1Chan()
    {
        IDictionary<int, SkillEntity> chain = new Dictionary<int, SkillEntity>
        {
            { 1, A1 },
            { 2, A2 },
            { 3, A3 }
        };
        return chain;
    }
    public IDictionary<int, SkillEntity> GetAttack2Chan()
    {
        IDictionary<int, SkillEntity> chain = new Dictionary<int, SkillEntity>
        {
            { 1, B1 },
            { 2, B2 },
            { 3, B3 }
        };
        return chain;
    }
    public IDictionary<int, SkillEntity> GetAttack3Chan()
    {
        IDictionary<int, SkillEntity> chain = new Dictionary<int, SkillEntity>
        {
            { 1, C1 },
            { 2, C2 },
            { 3, C3 }
        };
        return chain;
    }
    
    public SkillConfig GetA1Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(a1);
    }
    public SkillConfig GetA2Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(a2);
    }
    public SkillConfig GetA3Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(a3);
    }
    public SkillConfig GetB1Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(b1);
    }
    public SkillConfig GetB2Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(b2);
    }
    public SkillConfig GetB3Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(b3);
    }
    public SkillConfig GetC1Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(c1);
    }
    public SkillConfig GetC2Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(c2);
    }
    public SkillConfig GetC3Config()
    {
        return SkillConfigTable.GetSkillConfigByRecordId(c3);
    }
        
    public SkillEntity GetM_STS()
    {
        return M;
    }
}
