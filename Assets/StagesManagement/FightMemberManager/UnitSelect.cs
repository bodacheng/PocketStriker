#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using System.Linq;

public partial class StageEditor {
    
    string UnitSelect()
    {
        // 角色选择
        var focusingUnitConfig = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(_focusingUnitInfo.r_id));
        _focusingType = focusingUnitConfig != null ? EditorGUILayout.TextField("Unit Type", focusingUnitConfig.TYPE) : EditorGUILayout.TextField("Unit Type", _focusingType);
        _unitIDsAndNames = new Dictionary<string, string>() { { "-1", "空" } };
        foreach(var keyValuePair in Units.GetMonsterIDsAndNamesDic(_focusingType))
        {
            _unitIDsAndNames.Add(keyValuePair.Key, keyValuePair.Value);
        }
        var index = 0;
        foreach (var keyValuePair in _unitIDsAndNames)
        {
            if (keyValuePair.Key == _focusingUnitInfo.r_id)
            {
                _selectedUnitIndex = index;
                break;
            }
            index++;
        }
        _selectedUnitIndex = EditorGUILayout.Popup("角色名：", _selectedUnitIndex, _unitIDsAndNames.Values.ToArray());
        _focusingUnitInfo.r_id =  _unitIDsAndNames.Count > _selectedUnitIndex ? _unitIDsAndNames.ElementAt(_selectedUnitIndex).Key : null;
        _focusingUnitInfo.level = EditorGUILayout.FloatField("Unit Level:", _focusingUnitInfo.level);
        return _focusingUnitInfo.r_id;
    }
}
#endif