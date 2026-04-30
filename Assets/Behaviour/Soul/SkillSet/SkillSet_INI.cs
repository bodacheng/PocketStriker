using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using Skill;
using UnityEngine;

public partial class SkillSet
{
    SkillEntity A1, A2, A3, B1, B2, B3, C1, C2, C3, D, M, R;
    public SkillEntity MSkillEntity => M;

    #region 基础进程实体
    SkillEntity _empty, _victory, _death, _hit, _getUp, _knockOff;
    #endregion

    private readonly List<SkillEntity> _h1EList = new List<SkillEntity>();
    private readonly List<SkillEntity> _h2EList = new List<SkillEntity>();
    private readonly List<SkillEntity> _h3EList = new List<SkillEntity>();
    private readonly List<string> _h1List = new List<string>();
    private readonly List<string> _h2List = new List<string>();
    private readonly List<string> _h3List = new List<string>();
    SkillSetBuildResult _buildResult;

    /// <summary>
    /// 根据9个技能的id对技能组信息进行一个整理和补全，
    /// 生成技能实体。这里有个问题是，生成的技能实体在这里并没有依据等级
    /// 设置好对应的攻击和血量
    /// 整理三连击的连续关系。根据数据库配置好相应技能的属性。
    /// </summary>
    public void SortNineAndTwo()
    {
        _buildResult = SkillSetBuildUtility.BuildNineAndTwo(
            a1,
            a2,
            a3,
            b1,
            b2,
            b3,
            c1,
            c2,
            c3,
            _def,
            _moveType,
            FightGlobalSetting.HasDefend,
            SkillConfigTable.GetSkillConfigByRecordId);

        ApplyBuildResult(_buildResult);
    }

    // FormFightingSetsByNineAndTwo(string type,NineAndTwo nineAndTwo, passiveSkillConfigs passiveSkillConfigs, int AI_level) -->
    // 1.SortNineAndTwo(passiveSkillConfigs):整理三连击的连续关系。根据数据库配置好相应技能的属性。
    // 2.GenerateBehaviourSets():正式配置各State_Transition_Set，并且适配好所有技能组的force和casual迁移。
    public IDictionary<string, SkillEntity> GenerateBehaviourSets()
    {
        EnsureBuildResult();
        var seDic = SkillSetBuildUtility.GenerateBehaviourSets(
            _buildResult,
            FightGlobalSetting.HasDefend,
            Debug.Log);

        _empty = _buildResult.Empty;
        _victory = _buildResult.Victory;
        _death = _buildResult.Death;
        _hit = _buildResult.Hit;
        _getUp = _buildResult.GetUp;
        _knockOff = _buildResult.KnockOff;

        return seDic;
    }

    void EnsureBuildResult()
    {
        if (_buildResult == null)
        {
            SortNineAndTwo();
        }
    }

    void ApplyBuildResult(SkillSetBuildResult result)
    {
        A1 = result.A1;
        A2 = result.A2;
        A3 = result.A3;
        B1 = result.B1;
        B2 = result.B2;
        B3 = result.B3;
        C1 = result.C1;
        C2 = result.C2;
        C3 = result.C3;
        D = result.D;
        M = result.M;
        R = result.R;

        CopyList(_h1EList, result.H1Entities);
        CopyList(_h2EList, result.H2Entities);
        CopyList(_h3EList, result.H3Entities);
        CopyList(_h1List, result.H1Names);
        CopyList(_h2List, result.H2Names);
        CopyList(_h3List, result.H3Names);
    }

    static void CopyList<T>(List<T> target, List<T> source)
    {
        target.Clear();
        target.AddRange(source);
    }
}
