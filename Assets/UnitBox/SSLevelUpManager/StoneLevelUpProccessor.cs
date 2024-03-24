using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using mainMenu;
using UnityEngine;

public static class StoneLevelUpProccessor
{
    public static readonly List<SkillStoneLevelUpForm> UpdateAllStoneForms = new List<SkillStoneLevelUpForm>();
    public static int needGoldWhole;
    
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
    
    public static void LevelUpStone(string InstanceId, List<string> mInstanceIds, Action<string> refreshStoneData)
    {
        var target = Stones.Get(InstanceId);
        if (target.Born == "true")
        {
            Debug.Log("原生技能石不需升级");
            return;
        }
        
        var materialInstanceIds = new List<string>();
        var form = new SkillStoneLevelUpForm
        {
            targetStoneID = InstanceId
        };
        
        foreach (var instanceId in mInstanceIds)
        {
            form.stoneInstances.Add(instanceId);
            materialInstanceIds.Add(instanceId);
        }
        
        // 以下是远程那边计算技能石升到等级的逻辑：
        var materialLevels = new List<int>();
        var addLevel = 0; // 增加的等级
        void Temp(string instanceID)
        {
            var ssInfo = Stones.Get(instanceID);
            if (ssInfo.Born == "true")
            {
                Debug.Log("操作终止。被动技能正在被用作材料："+ssInfo.InstanceId);
                return;
            }
            
            materialLevels.Add(ssInfo.Level);
            addLevel += (ssInfo.Level - 1);
            if (materialLevels.Count == 4)
                addLevel += 1;
        }
        
        foreach (var instanceId in materialInstanceIds)
        {
            Temp(instanceId);
        }
        
        if (materialLevels.Count < 4)
        {
            Debug.Log("逻辑错误，material count :"+ materialLevels.Count);
            return;
        }
        
        form.addLevel = addLevel.ToString();
        form.needGD = 10 * (materialInstanceIds.Count / 4);
        
        CloudScript.UpdateStone(
            form,
            async (targetInstanceId,x) =>
            {
                foreach (var instanceId in x)
                {
                    await Stones.RemoveStoneLocal(instanceId);
                }
                
                Currencies.CoinCount.Value -= form.needGD;
                // RemoveStoneLocal会销毁作为材料的技能石模型，
                // 而CloseLevelUpPage内部有对材料的操作，
                // 他们在同一帧执行的话会有一定错误
                await UniTask.DelayFrame(1);
                refreshStoneData.Invoke(targetInstanceId);
            }
        );
    }
}
