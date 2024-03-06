using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using mainMenu;
using UnityEngine;

// 执行
public partial class SSLevelUpManager : MonoBehaviour
{
    void LevelUpStone(string InstanceId, List<string> mInstanceIds, Action<string> refreshStoneData)
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
                var info = Stones.Get(targetInstanceId);
                var config = SkillConfigTable.GetSkillConfigByRecordId(info.SkillId);
                _stoneListLayer.box.PressTab(config.SP_LEVEL);
                
                foreach (var instanceId in x)
                {
                    await Stones.RemoveStoneLocal(instanceId);
                }
                
                Currencies.CoinCount.Value -= form.needGD;
                // RemoveStoneLocal会销毁作为材料的技能石模型，
                // 而CloseLevelUpPage内部有对材料的操作，
                // 他们在同一帧执行的话会有一定错误
                await UniTask.DelayFrame(1);
                _stoneListLayer.box.RestFilter();
                CloseLevelUpPage();
                var renderModel = Stones.GetRenderModel(targetInstanceId);
                renderModel.Shine(PreScene.target.postProcessCamera);
                refreshStoneData.Invoke(targetInstanceId);
                _stoneListLayer.TargetStoneID = targetInstanceId;
                Stones.ShowAllStonesLevel();
            }
        );
    }
}