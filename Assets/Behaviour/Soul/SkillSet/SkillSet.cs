using Skill;
using System;
using System.Collections.Generic;
using dataAccess;
using MCombat.Shared.Behaviour;

[Serializable]
public partial class SkillSet {

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

    public string a1, a2, a3;
    public string b1, b2, b3;
    public string c1, c2, c3;
    
    private bool _def;
    private MoveType _moveType;
    
    public bool GetD()
    {
        return _def;
    }

    public MoveType GetM()
    {
        return _moveType;
    }

    public RushType GetR()
    {
        return RushType.None;
    }
    
    public SkillSet()
    {
        a1 = null; a2 = null; a3 = null;
        b1 = null; b2 = null; b3 = null;
        c1 = null; c2 = null; c3 = null;
        
        _moveType = MoveType.normal;
        _def = false;
    }

    public SkillSet(MoveType moveType, bool canDefend)
    {
        a1 = null; a2 = null; a3 = null;
        b1 = null; b2 = null; b3 = null;
        c1 = null; c2 = null; c3 = null;

        this._moveType = moveType;
        this._def = canDefend;
    }

    public SkillSet(MoveType moveType, bool canDefend, RushType rushType)
        : this(moveType, canDefend)
    {
    }

    public SkillSet DeepCopy()
    {
        return (SkillSet)MemberwiseClone();
    }

    public void SetPassive(bool _Def, MoveType _MoveType)
    {
        _def = _Def;
        _moveType = _MoveType;
    }

    public void SetPassive(bool _Def, MoveType _MoveType, RushType _RushType)
    {
        SetPassive(_Def, _MoveType);
    }

    public SkillEditError CheckEdit()
    {
        return CheckEdit(
            a1, a2, a3,
            b1, b2, b3,
            c1, c2, c3
        );
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

    /// <summary>
    /// 这个仅仅是一个辅助于技能编辑画面的工具，并不能用来判断角色实际装备中技能
    /// 实际装备中技能是用Stones.GetEquippingStones
    /// </summary>
    /// <returns></returns>
    public List<string> SkillIDList()
    {
        return SkillSetSlotUtility.SkillIdList(a1, a2, a3, b1, b2, b3, c1, c2, c3);
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
        return SkillSetSlotUtility.AverageLevel(levels);
    }

    public static float INI_Hp(List<string> skillIds, float lv)
    {
        return SkillSetSlotUtility.CalculateHp(
            skillIds,
            lv,
            SkillConfigTable.GetSkillConfigByRecordId,
            FightGlobalSetting.StoneHpCal);
    }

    // 获取技能实体列表，调用必须在SortNineAndTwo之后
    public List<SkillEntity> SkillEntityList()
    {
        EnsureBuildResult();
        return SkillSetBuildUtility.SkillEntityList(_buildResult);
    }

    //下面的环节纯粹是针对SkillPrintOut的一些处理
    public IDictionary<int, SkillEntity> GetAttack1Chan()
    {
        EnsureBuildResult();
        return SkillSetBuildUtility.AttackChain(_buildResult, 1);
    }
    public IDictionary<int, SkillEntity> GetAttack2Chan()
    {
        EnsureBuildResult();
        return SkillSetBuildUtility.AttackChain(_buildResult, 2);
    }
    public IDictionary<int, SkillEntity> GetAttack3Chan()
    {
        EnsureBuildResult();
        return SkillSetBuildUtility.AttackChain(_buildResult, 3);
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

    public int GetLowestSpLevel()
    {
        return SkillSetSlotUtility.LowestSpLevel(
            a1, a2, a3,
            b1, b2, b3,
            c1, c2, c3,
            SkillConfigTable.GetSkillConfigByRecordId);
    }

    // 这个的运行是建立在九宫格满的前提上
    public int RecommendedTargetReplaceSlot(bool mugen)
    {
        return SkillSetSlotUtility.RecommendedTargetReplaceSlot(
            SkillIDList(),
            SkillConfigTable.GetSkillConfigByRecordId,
            mugen);
    }
}
