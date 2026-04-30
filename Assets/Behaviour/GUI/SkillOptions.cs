#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using UnityEngine;
using Skill;

public partial class BehaviorRunnerGUI : Editor {
    
    public List<string> GetBeheviourOptions(string anim_path)
    {
        if (anim_path == null)
        {
            return null;
        }
        
        List<SkillConfig> SkillConfigs = SkillConfigTable.GetSkillConfigsOfType(anim_path);
        List<string> returnValue = BehaviorStateDefinitionUtility.CreateEditorBehaviorOptions(FightGlobalSetting.HasDefend, true);
        
        foreach (SkillConfig skillConfig in SkillConfigs)
        {
            if (!returnValue.Contains(skillConfig.REAL_NAME))
                returnValue.Add(skillConfig.REAL_NAME);
            else
                Debug.Log("重复的片段名，请检查资源");
        }
        return returnValue;
    }
    
    public List<string> GetBeheviourOptions(string anim_path, List<SkillEntity> toFormAttackStateList)
    {
        if (anim_path == null)
        {
            return null;
        }
        
        List<string> returnValue = new List<string>();
        foreach (SkillEntity _set in toFormAttackStateList)
        {
            if (!returnValue.Contains(_set.REAL_NAME))
            {
                returnValue.Add(_set.REAL_NAME);
            }
            else
            {
                Debug.Log("正在回避状态重复定义："+ _set.REAL_NAME);
            }
        }

        return returnValue;
    }
}
#endif
