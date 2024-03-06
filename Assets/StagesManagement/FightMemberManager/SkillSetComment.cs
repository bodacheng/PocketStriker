#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public partial class StageEditor {

    SkillSet.SkillEditError se;
    
    void SkillSetComment()
    {
        // 技能组评价
        GUILayout.BeginHorizontal();
        se = SkillSet.CheckEdit(
            _focusingUnitInfo.set.GetA1Config()?.RECORD_ID,_focusingUnitInfo.set.GetA2Config()?.RECORD_ID, _focusingUnitInfo.set.GetA3Config()?.RECORD_ID,
            _focusingUnitInfo.set.GetB1Config()?.RECORD_ID,_focusingUnitInfo.set.GetB2Config()?.RECORD_ID, _focusingUnitInfo.set.GetB3Config()?.RECORD_ID,
            _focusingUnitInfo.set.GetC1Config()?.RECORD_ID,_focusingUnitInfo.set.GetC2Config()?.RECORD_ID, _focusingUnitInfo.set.GetC3Config()?.RECORD_ID);
            
        switch (se)
        {
            case SkillSet.SkillEditError.Perfect:
                _title.normal.textColor = Color.green;
                EditorGUILayout.LabelField("合法", _title);
            break;
            case SkillSet.SkillEditError.NoNormalStart:
                _title.normal.textColor = Color.red;
                EditorGUILayout.LabelField("首发技能无普攻", _title);
            break;
            case SkillSet.SkillEditError.RepeatedSkill:
                _title.normal.textColor = Color.red;
                EditorGUILayout.LabelField("技能重复", _title);
            break;
            case SkillSet.SkillEditError.UnBalanced:
                _title.normal.textColor = Color.red;
                EditorGUILayout.LabelField("必杀普攻不平衡", _title);
            break;
            case SkillSet.SkillEditError.NotFull:
                _title.normal.textColor = Color.red;
                EditorGUILayout.LabelField("不满", _title);
                break;
        }
        _title.normal.textColor = Color.black;
        GUILayout.EndHorizontal();
    }
}
#endif