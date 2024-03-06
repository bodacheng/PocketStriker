#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using mainMenu;
using Singleton;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

// 后排敌人——〉角色ID，
// localID = 0，脚本ID，等级 前排中央敌人——〉角色ID，localID = 1，脚本ID，等级 前排左敌人——〉角色ID，localID = 2，脚本ID，等级 前排右敌人——〉角色ID，localID = 3，脚本ID，等级
public partial class StageEditor
{
    IDictionary<string, string> _unitIDsAndNames;
    UnitInfo _focusingUnitInfo;
    string _focusingType = "human";

    async void UnitIconInitialize()
    {
        AsyncOperationHandle<IList<IResourceLocation>> locationHandle = Addressables.LoadResourceLocationsAsync("unit_icon");
        await locationHandle.Task;
        if (locationHandle.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var path in locationHandle.Result)
            {
                string[] words = path.PrimaryKey.Split(new [] {"unit/"}, StringSplitOptions.None);
                await UnitIconDic.Load(words[1]);
            }
        }
    }
    
    public void OnGUIView(FightMembers target,
        Func<string, GangbangInfo.SoldierGroupSet> gangbangGet = null)
    {
        if (!Initialized)
        {
            UIParamIni();
            Initialized = true;
        }
        
        GUILayout.Space(10);
        Members(target, gangbangGet);
        GUILayout.Space(10);
        
        // 指定站位人员的添加与删除 //
        GUILayout.BeginHorizontal();
        if (_focusingUnitInfo == null)
        {
            if (GUILayout.Button("Add", _addDeleteMember))
            {
                _focusingPosID ??= "0";
                _focusingUnitInfo = new UnitInfo
                {
                    id = _focusingPosID
                };
                target.EnemySets.Set(0, int.Parse(_focusingPosID), _focusingUnitInfo);
            }
        }
        if (_focusingUnitInfo != null)
        {
            if (GUILayout.Button("Delete", _addDeleteMember))
            {
                target.EnemySets.Set(0, int.Parse(_focusingPosID), null);
                _focusingUnitInfo = null;
                _targetSlot = 0;
            }
        }
        GUILayout.EndHorizontal();
        
        if (_focusingUnitInfo == null)
            goto A;
        
        UnitSelect();
            
        // 九宫格
        NineSlotPart();
        
        GUILayout.BeginHorizontal();

        void Random(SkillStonesBox.StoneFilterForm form, string unitRecordID, bool noSpLimit = false)
        {
            _targetSlot = 0;
            if (string.IsNullOrEmpty(_focusingType))
                return;
            var passiveSKillRecordId = UnitPassiveTable.GetUnitPassiveRecordId(unitRecordID);
            _focusingUnitInfo.set = SkillSet.RandomSkillSet(_focusingType, passiveSKillRecordId,  false, form, noSpLimit);
        }
        
        if (GUILayout.Button("一般", _buttonStyle))
        {
            var form = new SkillStonesBox.StoneFilterForm
            {
                Type = _focusingType,
                ExType = new[] { 0 }
            };
            Random(form, _focusingUnitInfo.r_id);
        }
        if (GUILayout.Button("中boss", _buttonStyle))
        {
            var form = new SkillStonesBox.StoneFilterForm
            {
                Type = _focusingType,
                ExType = new[] { 0, 1, 2 }
            };
            Random(form, _focusingUnitInfo.r_id);
        }
        if (GUILayout.Button("大boss", _buttonStyle))
        {
            var form = new SkillStonesBox.StoneFilterForm
            {
                Type = _focusingType,
                ExType = new[] { 2, 3 }
            };
            Random(form, _focusingUnitInfo.r_id);
        }
        if (GUILayout.Button("超boss", _buttonStyle))
        {
            var form = new SkillStonesBox.StoneFilterForm
            {
                Type = _focusingType,
                ExType = new[] { 1, 2, 3 }
            };
            Random(form, _focusingUnitInfo.r_id, true);
        }
        GUILayout.EndHorizontal();
                
        // 技能组评价
        SkillSetComment();
        
        if (_targetSlot == 0)
            goto A;
                
        // 技能选择
        if (GetFocusSkillId() == null)
        {
            SkillSelect();
        }
        else
        {
            if (GUILayout.Button("重选技能", _buttonStyle))
            {
                SetSkillId(null);
                NineSlotPart();// 为了刷新格子颜色
            }
        }
                        
        var defaultSkillConfig = SkillConfigTable.GetSkillConfigByRecordId(GetFocusSkillId());
        if (defaultSkillConfig == null)
        {
            goto A;
        }
        
        // 技能详细信息
        SkillInfo(defaultSkillConfig);
        
        A:
        
        // 基础进程
        GUILayout.Space(10);
        BasicStates(_focusingUnitInfo);
    }
}
#endif