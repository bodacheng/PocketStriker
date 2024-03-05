using System.Collections.Generic;
using UnityEngine;
using Skill;
using System.Linq;

public partial class SkillSet
{
    SkillEntity A1, A2, A3, B1, B2, B3, C1, C2, C3, D, M, R;
    
    #region 基础进程实体
    SkillEntity _empty, _victory, _death, _hit, _getUp, _knockOff;
    #endregion
    
    private readonly List<SkillEntity> _h1EList = new List<SkillEntity>();
    private readonly List<SkillEntity> _h2EList = new List<SkillEntity>();
    private readonly List<SkillEntity> _h3EList = new List<SkillEntity>();
    private readonly List<string> _h1List = new List<string>();
    private readonly List<string> _h2List = new List<string>();
    private readonly List<string> _h3List = new List<string>();
    
    /// <summary>
    /// 根据9个技能的id对技能组信息进行一个整理和补全，
    /// 生成技能实体。这里有个问题是，生成的技能实体在这里并没有依据等级
    /// 设置好对应的攻击和血量
    /// 整理三连击的连续关系。根据数据库配置好相应技能的属性。
    /// </summary>
    public void SortNineAndTwo()
    {
        var aConfig1 = a1 != null ? SkillConfigTable.GetSkillConfigByRecordId(a1) : null;
        var aConfig2 = a2 != null ? SkillConfigTable.GetSkillConfigByRecordId(a2) : null;
        var aConfig3 = a3 != null ? SkillConfigTable.GetSkillConfigByRecordId(a3) : null;
        var bConfig1 = b1 != null ? SkillConfigTable.GetSkillConfigByRecordId(b1) : null;
        var bConfig2 = b2 != null ? SkillConfigTable.GetSkillConfigByRecordId(b2) : null;
        var bConfig3 = b3 != null ? SkillConfigTable.GetSkillConfigByRecordId(b3) : null;
        var cConfig1 = c1 != null ? SkillConfigTable.GetSkillConfigByRecordId(c1) : null;
        var cConfig2 = c2 != null ? SkillConfigTable.GetSkillConfigByRecordId(c2) : null;
        var cConfig3 = c3 != null ? SkillConfigTable.GetSkillConfigByRecordId(c3) : null;
        
        A1 = aConfig1 != null ? GetSkillEntity(a1) : null;
        A2 = aConfig2 != null ? GetSkillEntity(a2) : null;
        A3 = aConfig3 != null ? GetSkillEntity(a3) : null;
        
        B1 = bConfig1 != null ? GetSkillEntity(b1) : null;
        B2 = bConfig2 != null ? GetSkillEntity(b2) : null;
        B3 = bConfig3 != null ? GetSkillEntity(b3) : null;
        
        C1 = cConfig1 != null ? GetSkillEntity(c1) : null;
        C2 = cConfig2 != null ? GetSkillEntity(c2) : null;
        C3 = cConfig3 != null ? GetSkillEntity(c3) : null;
        
        ////////////  关于DMR 的处理，和角色本身被动有关，有别于现在的9宫  ////////////
        D = _def ? SkillEntity.GetD_SE() : null;
        M = SkillEntity.GetM_SE(_moveType);
        M.CAN_BE_CANCELLED_TO = false;
        R = SkillEntity.GetR_SE(_rushType);
        //////////////////////////////////////////////////////////////////////////
        
        _h1EList.Clear();
        _h2EList.Clear();
        _h3EList.Clear();
        
        _h1List.Clear();
        _h2List.Clear();
        _h3List.Clear();
        
        if (A1 != null)
        {
            _h1EList.Add(A1);
            _h1List.Add(A1.REAL_NAME);
            A1.EnterInput = InputKey.Attack1;
            A1.ExitInput = InputKey.Null;
        }
        if (A2 != null)
        {
            _h2EList.Add(A2);
            _h2List.Add(A2.REAL_NAME);
            A2.EnterInput = InputKey.Attack1;
            A2.ExitInput = InputKey.Null;
        }
        if (A3 != null)
        {
            _h3EList.Add(A3);
            _h3List.Add(A3.REAL_NAME);
            A3.EnterInput = InputKey.Attack1;
            A3.ExitInput = InputKey.Null;
        }

        if (B1 != null)
        {
            _h1EList.Add(B1);
            _h1List.Add(B1.REAL_NAME);
            B1.EnterInput = InputKey.Attack2;
            B1.ExitInput = InputKey.Null;
        }
        if (B2 != null)
        {
            _h2EList.Add(B2);
            _h2List.Add(B2.REAL_NAME);
            B2.EnterInput = InputKey.Attack2;
            B2.ExitInput = InputKey.Null;
        }
        if (B3 != null)
        {
            _h3EList.Add(B3);
            _h3List.Add(B3.REAL_NAME);
            B3.EnterInput = InputKey.Attack2;
            B3.ExitInput = InputKey.Null;
        }

        if (C1 != null)
        {
            _h1EList.Add(C1);
            _h1List.Add(C1.REAL_NAME);
            C1.EnterInput = InputKey.Attack3;
            C1.ExitInput = InputKey.Null;
        }
        if (C2 != null)
        {
            _h2EList.Add(C2);
            _h2List.Add(C2.REAL_NAME);
            C2.EnterInput = InputKey.Attack3;
            C2.ExitInput = InputKey.Null;
        }
        if (C3 != null)
        {
            _h3EList.Add(C3);
            _h3List.Add(C3.REAL_NAME);
            C3.EnterInput = InputKey.Attack3;
            C3.ExitInput = InputKey.Null;
        }
        
        if (R != null)
        {
            _h1EList.Add(R);
            _h2EList.Add(R);
            _h3EList.Add(R);
            
            _h1List.Add(R.REAL_NAME);
            _h2List.Add(R.REAL_NAME);
            _h3List.Add(R.REAL_NAME);
        }

        if (D != null && FightGlobalSetting.HasDefend)
        {
            _h1EList.Add(D);
            _h2EList.Add(D);
            _h3EList.Add(D);
            
            _h1List.Add(D.REAL_NAME);
            _h2List.Add(D.REAL_NAME);
            _h3List.Add(D.REAL_NAME);
        }
        
        for (var i = 0; i < _h1EList.Count; i++)
        {
            _h1EList[i].CasualTo = _h2List.ToArray();
        }
        for (var i = 0; i < _h2EList.Count; i++)
        {
            _h2EList[i].CasualTo = _h3List.ToArray();
        }
        for (var i = 0; i < _h3EList.Count; i++)
        {
            _h3EList[i].CasualTo = _h1List.ToArray();
        }
        
        M.CasualTo = _h1List.ToArray();
        if (D != null)
            D.CasualTo = _h1List.ToArray();
        if (R != null)
            R.CasualTo = _h1List.ToArray();
    }
    
    // FormFightingSetsByNineAndTwo(string type,NineAndTwo nineAndTwo, passiveSkillConfigs passiveSkillConfigs, int AI_level) -->
    // 1.SortNineAndTwo(passiveSkillConfigs):整理三连击的连续关系。根据数据库配置好相应技能的属性。
    // 2.GenerateBehaviourSets():正式配置各State_Transition_Set，并且适配好所有技能组的force和casual迁移。
    public IDictionary<string, SkillEntity> GenerateBehaviourSets()
    {
        IDictionary<string, SkillEntity> seDic = new Dictionary<string, SkillEntity>();
        var StateTransitionSetList = new List<SkillEntity>();
        
        _empty = new SkillEntity("Empty", BehaviorType.NONE, new AIAttrs(), null, null, InputKey.Null, InputKey.Null,  -1);
        _victory = new SkillEntity("Victory",BehaviorType.NONE, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, -1);
        _death = new SkillEntity("Death", BehaviorType.NONE, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, -1);
        _hit = new SkillEntity("Hit", BehaviorType.Hit, new AIAttrs(), _h1List.ToArray(),null,InputKey.Null, InputKey.Null, -1);
        _getUp = new SkillEntity("getUp",  BehaviorType.GetUp, new AIAttrs(), _h1List.ToArray(), null, InputKey.Any, InputKey.Null, -1);
        _knockOff = new SkillEntity("KnockOff", BehaviorType.KnockOff, new AIAttrs(), R != null ? new string[] { R.REAL_NAME } : new string[] {}, null, InputKey.Null, InputKey.Null, -1);
        if (FightGlobalSetting.HasDefend)
        {
            D.CasualTo = _h1List.ToArray();
            StateTransitionSetList.Add(D);
        }
        
        StateTransitionSetList.Add(_getUp);
        StateTransitionSetList.Add(_knockOff);
        StateTransitionSetList.Add(_empty);
        StateTransitionSetList.Add(_victory);
        StateTransitionSetList.Add(_death);
        StateTransitionSetList.Add(_hit);        
        StateTransitionSetList.Add(M);
        
        if (D != null && FightGlobalSetting.HasDefend)
        {
            StateTransitionSetList.Add(D);
        }
        if (R != null)
        {
            StateTransitionSetList.Add(R);
        }

        if (A1 != null)
        {
            StateTransitionSetList.Add(A1);
        }
        if (A2 != null)
        {
            StateTransitionSetList.Add(A2);
        }            
        if (A3 != null)
        {
            StateTransitionSetList.Add(A3);
        }
        if (B1 != null)
        {
            StateTransitionSetList.Add(B1);
        }            
        if (B2 != null)
        {
            StateTransitionSetList.Add(B2);
        }
        if (B3 != null)
        {
            StateTransitionSetList.Add(B3);
        }
        if (C1 != null)
        {
            StateTransitionSetList.Add(C1);
        }                    
        if (C2 != null)
        {
            StateTransitionSetList.Add(C2);
        }            
        if (C3 != null)
        {
            StateTransitionSetList.Add(C3);
        }
        
        foreach (var _SE in StateTransitionSetList)
        {
            if (_SE != M && _SE != _knockOff && _SE != _empty && _SE != _death && _SE != _victory)
            {
                var toOptions = _SE.CasualTo.ToList();
                if (!toOptions.Contains(M.REAL_NAME))
                {
                    toOptions.Add(M.REAL_NAME);
                }
                _SE.CasualTo = toOptions.ToArray();
            }
            if (_SE.REAL_NAME != null && !seDic.ContainsKey(_SE.REAL_NAME))
            {
                seDic.Add(new KeyValuePair<string, SkillEntity>(_SE.REAL_NAME, _SE));
            }
            else
            {
                if (_SE.REAL_NAME == null)
                {
                    Debug.Log("键值为空？？");
                }else{
                    Debug.Log("角色自身技能产生键值重复："+_SE.REAL_NAME);
                }
            }
        }
        return seDic;
    }
    
    SkillEntity GetSkillEntity(string skillId)
    {
        var sc = SkillConfigTable.GetSkillConfigByRecordId(skillId);
        if (sc == null)
        {
            return null;
        }
        
        var se = new SkillEntity(
            sc.RECORD_ID,
            sc.REAL_NAME,
            sc.STATE_TYPE,
            sc.AIAttrs,
            null,
            null,
            InputKey.Null,
            InputKey.Null,
            sc.SP_LEVEL
        );
        return se;
    }
}