#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Skill;

public partial class StageEditor {
    
    readonly int[] _exOptions = { 0, 1, 2, 3 };
    readonly string[] exOptionsDisplay = {"normal","ex1","ex2","ex3"};
    
    void SkillInfo(SkillConfig skillConfig)
    {
        if (skillConfig == null)
            return;
        EditorGUILayout.LabelField("技能详细信息");
        GUI.backgroundColor = new Color(1f, 0.7f, 0.5f);
        skillConfig.REAL_NAME = EditorGUILayout.TextField("Name",skillConfig.REAL_NAME);
        skillConfig.STATE_TYPE = (BehaviorType)EditorGUILayout.EnumPopup("Attack Type", skillConfig.STATE_TYPE);                                                    
        skillConfig.ATTACK_WEIGHT = EditorGUILayout.FloatField("AT", skillConfig.ATTACK_WEIGHT);
        skillConfig.SP_LEVEL = EditorGUILayout.IntPopup("SPLevel", skillConfig.SP_LEVEL, exOptionsDisplay, _exOptions);        
        EditorGUILayout.LabelField("AI模式技能触发范围");
        skillConfig.AIAttrs.AI_MIN_DIS = EditorGUILayout.FloatField("min_dis",skillConfig.AIAttrs.AI_MIN_DIS);
        skillConfig.AIAttrs.AI_MAX_DIS = EditorGUILayout.FloatField("max_dis",skillConfig.AIAttrs.AI_MAX_DIS);
        skillConfig.AIAttrs.height = EditorGUILayout.IntField("height",skillConfig.AIAttrs.height);
        GUI.backgroundColor = Color.white;
    }
}
#endif