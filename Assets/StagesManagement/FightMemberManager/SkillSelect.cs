#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using mainMenu;

public partial class StageEditor {

    int _selectSkillExLevel = -1;
    readonly int[] _exLevels = {-1, 0, 1, 2, 3 };
    readonly string[] _exLevelShows = { "ALL", "普攻", "一级必杀", "二级必杀", "三级必杀" };
    int[] _spSelected = { 0, 1, 2, 3 };
    readonly bool[] _rangeFilter = { false, false, false };
    bool _skillSelectFilter;
    bool _filterRanges = true;
    IDictionary<string, string> _skillIDsAndNames = new Dictionary<string, string>();
    
    void SkillSelect()
    {
        _skillSelectFilter = EditorGUILayout.Toggle("限制技能选择条件", _skillSelectFilter, _attackRangeToggleGUI);
        if (_skillSelectFilter)
        {
            EditorGUILayout.LabelField(" ~~~~~  限制技能条件  ~~~~~ ", _title);
            _selectSkillExLevel = EditorGUILayout.IntPopup("必杀技等级:", _selectSkillExLevel, _exLevelShows, _exLevels);
            switch(_selectSkillExLevel)
            {
                case 0:
                    _spSelected = new[] { 0 };
                break;
                case 1:
                    _spSelected = new[] { 1 };
                break;
                case 2:
                    _spSelected = new[] { 2 };
                break;
                case 3:
                    _spSelected = new[] { 3 };
                break;
                default:
                    _spSelected = new[] { 0, 1, 2, 3 };
                break;
            }
            
            _filterRanges = EditorGUILayout.BeginToggleGroup("限定攻击范围", _filterRanges);
            if (!_filterRanges)
            {
                _rangeFilter[0] = false;
                _rangeFilter[1] = false;
                _rangeFilter[2] = false;
            }else{
                _rangeFilter[0] = EditorGUILayout.Toggle("近", _rangeFilter[0], _attackRangeToggleGUI);
                _rangeFilter[1] = EditorGUILayout.Toggle("中", _rangeFilter[1], _attackRangeToggleGUI);
                _rangeFilter[2] = EditorGUILayout.Toggle("远", _rangeFilter[2], _attackRangeToggleGUI);
            }
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.LabelField(" ~~~~~  以下将陈列根据条件删选出的技能  ~~~~~ ", _title);
            GUILayout.Space(10f);
        }
        
        var filterForm = new SkillStonesBox.StoneFilterForm
        {
            Type = _focusingType,
            Close = _rangeFilter[0],
            Near = _rangeFilter[1],
            Far = _rangeFilter[2],
            ExType = _spSelected,
            BType = Skill.BehaviorType.NONE
        };        
        _skillIDsAndNames = SkillList(filterForm);// 待研究
        
        int index2 = 0;
        int selectedSkillIndex = 0;
        foreach (var keyValuePair in _skillIDsAndNames)
        {
            if (keyValuePair.Key == GetFocusSkillId())
            {
                selectedSkillIndex = index2;
                break;
            }
            index2++;
        }
        
        selectedSkillIndex = EditorGUILayout.Popup("技能：", selectedSkillIndex, _skillIDsAndNames.Values.ToArray());
        SetSkillId(selectedSkillIndex == 0 ? null : _skillIDsAndNames.ElementAt(selectedSkillIndex).Key);
    }
    
    IDictionary<string,string> SkillList(SkillStonesBox.StoneFilterForm filterForm)
    {
        IDictionary<string, string> returnValue = new Dictionary<string, string>
        {
            { "-1", "空" }
        };
        var skillIDAndNameDic = SkillConfigTable.GetSkillIDAndNameDic(filterForm);
        foreach(var keyValuePair in skillIDAndNameDic)
        {
            returnValue.Add(keyValuePair.Key, keyValuePair.Value);
        }
        return returnValue;
    }
}
#endif