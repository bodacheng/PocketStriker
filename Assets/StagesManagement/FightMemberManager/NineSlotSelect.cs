#if UNITY_EDITOR
using UnityEngine;
using Skill;

public partial class StageEditor {

    string A1ButtonText = "A1";
    string A2ButtonText = "A2";
    string A3ButtonText = "A3";
    string B1ButtonText = "B1";
    string B2ButtonText = "B2";
    string B3ButtonText = "B3";
    string C1ButtonText = "C1";
    string C2ButtonText = "C2";
    string C3ButtonText = "C3";
    
    void NineSlotPart()
    {
        void SlotAnalyze(int targetSlot)
        {
            string nowSkillID = null;
            SkillConfig defaultSkillConfig = null;
            switch(targetSlot)
            {
                case 1:
                nowSkillID = _focusingUnitInfo.set.a1;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                A1ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 2:
                nowSkillID = _focusingUnitInfo.set.a2;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                A2ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 3:
                nowSkillID = _focusingUnitInfo.set.a3;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                A3ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 4:
                nowSkillID = _focusingUnitInfo.set.b1;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                B1ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 5:
                nowSkillID = _focusingUnitInfo.set.b2;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                B2ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 6:
                nowSkillID = _focusingUnitInfo.set.b3;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                B3ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 7:
                nowSkillID = _focusingUnitInfo.set.c1;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                C1ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 8:
                nowSkillID = _focusingUnitInfo.set.c2;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                C2ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
                case 9:
                nowSkillID = _focusingUnitInfo.set.c3;
                defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(nowSkillID);
                C3ButtonText = RefreshButtonText(defaultSkillConfig);
                break;
            }
            
            GUI.backgroundColor = Repeated(_focusingUnitInfo.set, nowSkillID) ? Color.red : (defaultSkillConfig != null ? Color.yellow : Color.white);
        }
        
        GUILayout.BeginHorizontal();
        SlotAnalyze(1);
        if (GUILayout.Button(A1ButtonText, _targetSlot == 1 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 1;
        }
        SlotAnalyze(2);
        if (GUILayout.Button(A2ButtonText, _targetSlot == 2 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 2;
        }
        SlotAnalyze(3);
        if (GUILayout.Button(A3ButtonText, _targetSlot == 3 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 3;
        }
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        SlotAnalyze(4);
        if (GUILayout.Button(B1ButtonText, _targetSlot == 4 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 4;
        }
        SlotAnalyze(5);
        if (GUILayout.Button(B2ButtonText, _targetSlot == 5 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 5;
        }
        SlotAnalyze(6);
        if (GUILayout.Button(B3ButtonText, _targetSlot == 6 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 6;
        }
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        SlotAnalyze(7);
        if (GUILayout.Button(C1ButtonText, _targetSlot == 7 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 7;
        }
        SlotAnalyze(8);
        if (GUILayout.Button(C2ButtonText, _targetSlot == 8 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 8;
        }
        SlotAnalyze(9);
        if (GUILayout.Button(C3ButtonText, _targetSlot == 9 ? _buttonStyleNineAndTwoSelected : _buttonStyleNineAndTwo))
        {
            _targetSlot = 9;
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }
    
    bool Repeated(SkillSet nineAndTwo, string recordID)
    {
        var currentSkillList = nineAndTwo.SkillIDList();
        var count = 0;
        for (var i = 0; i < currentSkillList.Count; i++)
        {
            if (currentSkillList[i] == recordID)
                count += 1;
        }
        return count > 1;
    }
        
    string RefreshButtonText(SkillConfig skillConfig)
    {
        if (skillConfig == null)
        {
            return "-";
        }
        switch(skillConfig.SP_LEVEL)
        {
            case 0:
                return "●";
            case 1:
                return "★";
            case 2:
                return "★★";
            case 3:
                return "★★★";
            default:
                return "-";
        }
    }
}
#endif