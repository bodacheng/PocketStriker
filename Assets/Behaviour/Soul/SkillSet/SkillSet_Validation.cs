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
        return (SkillEditError)SkillSetValidationUtility.CheckEdit(
            a1,
            a2,
            a3,
            b1,
            b2,
            b3,
            c1,
            c2,
            c3,
            SkillConfigTable.GetSkillConfigByRecordId,
            UnitPassiveTable.GetPassiveSKillRecordIds(),
            GetSkillSetValidationCosts(),
            atLeastTwoExSkill);
    }
    
    // 当前总分。不问技能组是否合法
    public static int SkillBalancePoint(string a1SkillId, string a2SkillId, string a3SkillId, string b1SkillId, string b2SkillId, string b3SkillId, string c1SkillId, string c2SkillId, string c3SkillId)
    {
        return SkillSetValidationUtility.SkillBalancePoint(
            a1SkillId,
            a2SkillId,
            a3SkillId,
            b1SkillId,
            b2SkillId,
            b3SkillId,
            c1SkillId,
            c2SkillId,
            c3SkillId,
            SkillConfigTable.GetSkillConfigByRecordId,
            GetSkillSetValidationCosts());
    }

    static SkillSetValidationCosts GetSkillSetValidationCosts()
    {
        return new SkillSetValidationCosts(
            CommonSetting.SkillSetCostLimit,
            CommonSetting.Sp0Cost,
            CommonSetting.Sp1Cost,
            CommonSetting.Sp2Cost,
            CommonSetting.Sp3Cost);
    }
}
