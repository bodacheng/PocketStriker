#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Singleton;

public partial class StageEditor {
    
    Texture2D GetIconTexture2D(Sprite icon)
    {
        if (icon == null)
        {
            icon = DefaultIconSetting._unitSlotEmpty;
        }
        var croppedTexture = new Texture2D( (int)icon.textureRect.width, (int)icon.textureRect.height );
        var pixels = icon.texture.GetPixels(
            (int)icon.textureRect.x, 
            (int)icon.textureRect.y, 
            (int)icon.textureRect.width, 
            (int)icon.textureRect.height );
        croppedTexture.SetPixels( pixels );
        croppedTexture.Apply();
        return croppedTexture;
    }
    
    int _selectedUnitIndex;
    string _focusingPosID;
    
    void Members(FightMembers target, Func<string, GangbangInfo.SoldierGroupSet> gangbangGet = null)
    {
        async void UnitSlot(int posNum, Func<string, GangbangInfo.SoldierGroupSet> gangbangGet = null)
        {
            var unitInfo = target.EnemySets.Get(0, posNum);
            var sprite = unitInfo != null ? await UnitIconDic.Load(unitInfo.r_id) : null;
            _unitBtnContent = new GUIContent(GetIconTexture2D(sprite));
            if (GUI.Button(new Rect(posNum * 70, 0, 20, 20), _unitBtnContent, _focusingPosID == posNum.ToString() ? _unitIconSelectedStyle : _unitIconStyle))
            {
                _selectedUnitIndex = 0;
                _focusingPosID = posNum.ToString();
                _focusingUnitInfo = target.EnemySets.Get(0, posNum);
                _targetSlot = 0;
            }
            if (gangbangGet != null)
            {
                var info = target.EnemySets.Get(0, posNum);
                if (info != null)
                {
                    gangbangGet(info.id).Count =
                        EditorGUI.IntField(new Rect(posNum * 70 + 30, 0, 20, 20), gangbangGet(info.id).Count);
                }
            }
        }
        
        EditorGUILayout.LabelField(" Enemies infos ");
        GUILayout.BeginHorizontal();
        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(50), GUILayout.Width(400));
        GUI.BeginGroup(rect);
        UnitSlot(0, gangbangGet);
        UnitSlot(1, gangbangGet);
        UnitSlot(2, gangbangGet);
        GUI.EndGroup();
        GUILayout.EndHorizontal();
    }
}
#endif