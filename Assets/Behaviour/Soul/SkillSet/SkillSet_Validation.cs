using System.Collections.Generic;
using Skill;

public partial class SkillSet
{
    public enum SkillEditError
    {
        Empty,
        NotFull,
        UnBalanced,
        RepeatedSkill,
        NoNormalStart,
        NoAtLeastTwoEx, // Tutorial
        Perfect
    }
    
    // 判断技能组是否合法。包括了首技能有无普攻，有无重复，总点数是否平衡 这三方面
    public static SkillEditError CheckEdit(string a1, string a2, string a3, string b1, string b2, string b3, string c1, string c2, string c3, bool atLeastTwoExSkill = false)
    {
        bool IsEmpty(string skillId)
        {
            if (!HasStone(skillId))
            {
                return true;
            }
            var passiveSkills = UnitPassiveTable.GetPassiveSKillRecordIds();
            return passiveSkills.Contains(skillId);
        }
        
        bool HasStone(string skillID)
        {
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillID);
            return skillConfig != null;
        }
        
        if (IsEmpty(a1) && IsEmpty(a2) && IsEmpty(a3) && IsEmpty(b1) && IsEmpty(b2) && IsEmpty(b3) && IsEmpty(c1) && IsEmpty(c2) && IsEmpty(c3))
        {
            return SkillEditError.Empty;
        }
        
        var wholePoint = SkillBalancePoint(a1, a2, a3, b1, b2, b3, c1, c2, c3);
        if (wholePoint < 0)
        {
            return SkillEditError.UnBalanced;
        }
        
        if (!(HasStone(a1) && HasStone(a2) && HasStone(a3) &&
              HasStone(b1) && HasStone(b2) && HasStone(b3) &&
              HasStone(c1) && HasStone(c2) && HasStone(c3)))
        {
            return SkillEditError.NotFull;
        }
        
        // 第一列技能必须有普通技能
        if (CheckStartSKills(a1, b1, c1) == SkillEditError.NoNormalStart)
        {
            return SkillEditError.NoNormalStart;
        }
        
        if (!CheckRepeat(a1, a2, a3, b1, b2, b3, c1, c2, c3))
        {
            return SkillEditError.RepeatedSkill;
        }

        if (atLeastTwoExSkill && !CheckAtLeastTwoEx(a1, a2, a3, b1, b2, b3, c1, c2, c3))
        {
            return SkillEditError.NoAtLeastTwoEx;
        }
        
        return SkillEditError.Perfect;
    }
    
    // 当前总分。不问技能组是否合法
    public static int SkillBalancePoint(string a1SkillId, string a2SkillId, string a3SkillId, string b1SkillId, string b2SkillId, string b3SkillId, string c1SkillId, string c2SkillId, string c3SkillId)
    {
        var skillConfigA1 = SkillConfigTable.GetSkillConfigByRecordId(a1SkillId);
        var skillConfigA2 = SkillConfigTable.GetSkillConfigByRecordId(a2SkillId);
        var skillConfigA3 = SkillConfigTable.GetSkillConfigByRecordId(a3SkillId);
        var skillConfigB1 = SkillConfigTable.GetSkillConfigByRecordId(b1SkillId);
        var skillConfigB2 = SkillConfigTable.GetSkillConfigByRecordId(b2SkillId);
        var skillConfigB3 = SkillConfigTable.GetSkillConfigByRecordId(b3SkillId);
        var skillConfigC1 = SkillConfigTable.GetSkillConfigByRecordId(c1SkillId);
        var skillConfigC2 = SkillConfigTable.GetSkillConfigByRecordId(c2SkillId);
        var skillConfigC3 = SkillConfigTable.GetSkillConfigByRecordId(c3SkillId);
        
        var skillConfigs = new List<SkillConfig>();
        
        if (skillConfigA1 != null)
            skillConfigs.Add(skillConfigA1);
        if (skillConfigA2 != null)
            skillConfigs.Add(skillConfigA2);
        if (skillConfigA3 != null)
            skillConfigs.Add(skillConfigA3);
        if (skillConfigB1 != null)
            skillConfigs.Add(skillConfigB1);
        if (skillConfigB2 != null)
            skillConfigs.Add(skillConfigB2);
        if (skillConfigB3 != null)
            skillConfigs.Add(skillConfigB3);
        if (skillConfigC1 != null)
            skillConfigs.Add(skillConfigC1);
        if (skillConfigC2 != null)
            skillConfigs.Add(skillConfigC2);
        if (skillConfigC3 != null)
            skillConfigs.Add(skillConfigC3);
            
        var balancePoint = 0;
        foreach (var t in skillConfigs)
        {
            switch (t.SP_LEVEL)
            {
                case 0:
                    balancePoint += 10;
                    break;
                case 1:
                    balancePoint -= 10;
                    break;
                case 2:
                    balancePoint -= 20;
                    break;
                case 3:
                    balancePoint -= 30;
                    break;
                case -1:
                    break;
            }
        }
        return balancePoint;
    }
    
    // 查看技能组内是否有重复 false :不合法，有重复  true：合法，无重复
    static bool CheckRepeat(string a1, string a2, string a3, string b1, string b2, string b3, string c1, string c2, string c3)
    {
        // 检查技能重复
        var checkSame = new List<string>
        {
            a1,
            a2,
            a3,
            b1,
            b2,
            b3,
            c1,
            c2,
            c3
        };
        
        for (var i = 0; i < checkSame.Count; i++)
        {
            if (i != checkSame.Count - 1 && SkillConfigTable.GetSkillConfigByRecordId(checkSame[i]) != null)
            {
                for (var y = i + 1; y < checkSame.Count; y++)
                {
                    if (checkSame[i] == checkSame[y])
                        return false;
                }
            }
        }
        
        return true;
    }
    
    static bool CheckAtLeastTwoEx(string a1, string a2, string a3, string b1, string b2, string b3, string c1, string c2, string c3)
    {
        // 检查技能重复
        var ids = new List<string>
        {
            a1,
            a2,
            a3,
            b1,
            b2,
            b3,
            c1,
            c2,
            c3
        };

        int exCount = 0;
        foreach (var id in ids)
        {
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(id);
            if (skillConfig.SP_LEVEL != 0)
            {
                exCount++;
            }
        }
        
        return exCount >= 2;
    }
    
    // 检查起始技能有没有普通技能
    static SkillEditError CheckStartSKills(string a1Skill, string a2Skill, string a3Skill)
    {
        // 第一列技能必须有普通技能
        var normalSkillsOfAList = new List<string>();            
        var skillConfigA1 = SkillConfigTable.GetSkillConfigByRecordId(a1Skill);
        var skillConfigB1 = SkillConfigTable.GetSkillConfigByRecordId(a2Skill);
        var skillConfigC1 = SkillConfigTable.GetSkillConfigByRecordId(a3Skill);
        
        if (skillConfigA1 != null && skillConfigA1.SP_LEVEL == 0)
            normalSkillsOfAList.Add(skillConfigA1.REAL_NAME);
        if (skillConfigB1 != null && skillConfigB1.SP_LEVEL == 0)
            normalSkillsOfAList.Add(skillConfigB1.REAL_NAME);
        if (skillConfigC1 != null && skillConfigC1.SP_LEVEL == 0)
            normalSkillsOfAList.Add(skillConfigC1.REAL_NAME);
        
        return normalSkillsOfAList.Count == 0 ? SkillEditError.NoNormalStart : SkillEditError.Perfect;
    }
}