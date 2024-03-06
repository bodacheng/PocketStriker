using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using dataAccess;
using DummyLayerSystem;
using mainMenu;

public partial class SSLevelUpManager : MonoBehaviour
{
    private static readonly List<SkillStoneLevelUpForm> UpdateAllStoneForms = new List<SkillStoneLevelUpForm>();
    private static int needGoldWhole;

    public static bool HasStoneToBeUpdate()
    {
        return UpdateAllStoneForms.Count > 0;
    }
    
    public static SkillStoneLevelUpForm DecideForm(string skillRecordId, string targetStoneInstanceId = null)
    {
        var instanceIds = Stones.GetMyStonesBySkillID(skillRecordId);
        if (targetStoneInstanceId == null)
        {
            int hightestLevel = 0;
            foreach (var instanceId in instanceIds)
            {
                var stoneInfo = Stones.Get(instanceId);
                if (dataAccess.Units.Get(stoneInfo.unitInstanceId) != null) // 尽量升级装备中技能
                {
                    targetStoneInstanceId = stoneInfo.InstanceId;
                    break;
                }
                if (stoneInfo.Level > hightestLevel)
                {
                    targetStoneInstanceId = stoneInfo.InstanceId;
                }
            }
        }
        
        if (targetStoneInstanceId == null)
            return null;
        
        var wholeMInstanceIds = new List<string>();
        var mSet = new List<string>();
        foreach (var instanceId in instanceIds)
        {
            var info = Stones.Get(instanceId);
            if (instanceId != targetStoneInstanceId && 
                dataAccess.Units.Get(info.unitInstanceId) == null)
            {
                mSet.Add(instanceId);
                if (mSet.Count == 4)
                {
                    if (instanceIds.Count - wholeMInstanceIds.Count - 4 < 3)
                    {
                        break;
                    }
                    else
                    {
                        foreach (var id in mSet)
                        {
                            wholeMInstanceIds.Add(id);
                        }
                        mSet = new List<string>();
                    }
                }
            }
        }
        
        if (wholeMInstanceIds.Count < 4)
        {
            return null;
        }
        
        var form = new SkillStoneLevelUpForm
        {
            targetStoneID = targetStoneInstanceId,
            stoneInstances = wholeMInstanceIds,
            needGD = 10 * (wholeMInstanceIds.Count / 4)
        };
        
        return form;
    }
    
    public static void CalUpdateAllForms()
    {
        needGoldWhole = 0;
        UpdateAllStoneForms.Clear();
        foreach (var kv in SkillConfigTable.SkillConfigRefDic)
        {
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(kv.Key);
            var updateForm = DecideForm(skillConfig.RECORD_ID);
            if (updateForm != null)
            {
                UpdateAllStoneForms.Add(updateForm);
                needGoldWhole += updateForm.needGD;
            }
        }
    }
    
    void ConfirmUpdateAll(Action<string> refreshStoneData)
    {
        _stoneListLayer.box._tabEffects.TurnShowingTagEffects(false);
        var stoneUpdatesConfirm = UILayerLoader.Load<StoneUpdatesConfirm>();
        stoneUpdatesConfirm.ShowInfo(
            ()=>
            {
                UILayerLoader.Remove<StoneUpdatesConfirm>();
                ExecuteUpdateAll(refreshStoneData);
                _stoneListLayer.box._tabEffects.TurnShowingTagEffects(true);
            },
            ()=>
            {
                _stoneListLayer.box._tabEffects.TurnShowingTagEffects(true);
                UILayerLoader.Remove<StoneUpdatesConfirm>();
            },
            needGoldWhole,
            UpdateAllStoneForms
        );
    }

    async void ExecuteUpdateAll(Action<string> refreshStoneData)
    {
        if (Currencies.CoinCount.Value < needGoldWhole)
        {
            PopupLayer.ArrangeWarnWindow(Translate.Get("NoEnoughGD"));
            return;
        }
        
        var _returnLayer = UILayerLoader.Get<ReturnLayer>();
        if (_returnLayer != null)
            _returnLayer.gameObject.SetActive(false);
        bool canNext = true;
        void Next(string x)
        {
            refreshStoneData(x);
            canNext = true;
        }
        foreach (var updateAllStoneForm in UpdateAllStoneForms)
        {
            await UniTask.WaitUntil(()=> canNext);
            canNext = false;
            LevelUpStone(updateAllStoneForm.targetStoneID, updateAllStoneForm.stoneInstances, Next);
        }
        
        PopupLayer.ArrangeWarnWindow(Translate.Get("AutoMergeFinished"));
        if (_returnLayer != null)
            _returnLayer.gameObject.SetActive(true);

        CalUpdateAllForms();
        LevelUpAllStonesBtn.interactable = HasStoneToBeUpdate();
        LevelUpAllStonesBtnAnimator.SetBool("on", false);
    }
}
