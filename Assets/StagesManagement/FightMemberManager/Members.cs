#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using Singleton;

public partial class StageEditor {

    Texture2D GetIconTexture2D(Sprite icon)
    {
        if (icon == null)
        {
            icon = DefaultIconSetting._unitSlotEmpty;
            if (icon == null)
            {
                return Texture2D.whiteTexture;
            }
        }

        if (_spriteTextureCache.TryGetValue(icon, out var cachedTexture))
        {
            return cachedTexture;
        }

        var width = (int)icon.textureRect.width;
        var height = (int)icon.textureRect.height;
        var croppedTexture = new Texture2D(width, height) { hideFlags = HideFlags.HideAndDontSave };
        var pixels = icon.texture.GetPixels(
            (int)icon.textureRect.x,
            (int)icon.textureRect.y,
            width,
            height);
        croppedTexture.SetPixels(pixels);
        croppedTexture.Apply();
        _spriteTextureCache[icon] = croppedTexture;
        return croppedTexture;
    }

    int _selectedUnitIndex;
    string _focusingPosID;
    int _unitCount = 3;

    void Members(FightMembers target, Func<string, GangbangInfo.SoldierGroupSet> gangbangGet = null)
    {
        void UnitSlot(int posNum, Func<string, GangbangInfo.SoldierGroupSet> gangbangGet = null)
        {
            var unitInfo = target.EnemySets.Get(0, posNum);
            var texture = ResolveSlotIcon(unitInfo);
            _unitBtnContent ??= new GUIContent();
            _unitBtnContent.image = texture;
            _unitBtnContent.text = string.Empty;
            _unitBtnContent.tooltip = unitInfo?.r_id ?? string.Empty;
            if (GUI.Button(new Rect(posNum * 40, 0, 20, 20), _unitBtnContent, _focusingPosID == posNum.ToString() ? _unitIconSelectedStyle : _unitIconStyle))
            {
                _selectedUnitIndex = 0;
                _focusingPosID = posNum.ToString();
                _focusingUnitInfo = target.EnemySets.Get(0, posNum);
                _targetSlot = 0;
            }

            if (gangbangGet != null && unitInfo != null)
            {
                var groupSet = gangbangGet(unitInfo.id);
                if (groupSet != null)
                {
                    groupSet.Count = EditorGUI.IntField(new Rect(posNum * 70 + 30, 0, 20, 20), groupSet.Count);
                }
            }
        }
        EditorGUILayout.LabelField(" Enemies infos ");

        GUILayout.BeginHorizontal();
        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(50), GUILayout.Width(400));
        GUI.BeginGroup(rect);
        try
        {
            _unitCount = Mathf.Max(target.EnemySets.GetValues().Count, _unitCount);

            for (int i = 0; i < _unitCount; i++)
            {
                UnitSlot(i, gangbangGet);
            }

            if (GUI.Button(new Rect(_unitCount * 40, 0, 20, 20), "+"))
            {
                _unitCount++;
            }
        }
        finally
        {
            GUI.EndGroup();
            GUILayout.EndHorizontal();
        }
    }

    Texture2D ResolveSlotIcon(UnitInfo unitInfo)
    {
        if (unitInfo == null || string.IsNullOrEmpty(unitInfo.r_id))
        {
            return GetIconTexture2D(null);
        }

        var sprite = GetCachedUnitIcon(unitInfo.r_id);
        return GetIconTexture2D(sprite);
    }
}
#endif
