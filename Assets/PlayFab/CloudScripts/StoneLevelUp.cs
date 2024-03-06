using UnityEngine;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;
using dataAccess;

public partial class CloudScript
{
    public static void UpdateStone(SkillStoneLevelUpForm form, Action<string, List<string>> success)
    {
        var Items = new List<PlayFab.AdminModels.RevokeInventoryItem>();
        var itemInstanceIds = new List<string>();
        if (form.stoneInstances.Count < 4)
        {
            Debug.Log("error");
            return;
        }
        
        foreach (var stoneInstanceId in form.stoneInstances)
        {
            var resource = new PlayFab.AdminModels.RevokeInventoryItem()
            {
                ItemInstanceId = stoneInstanceId,
                PlayFabId = PlayerAccountInfo.Me.PlayFabId
            };
            Items.Add(resource);
            itemInstanceIds.Add(resource.ItemInstanceId);
        }
        
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest()
            {
                FunctionName = "updateStone",
                FunctionParameter = new
                {
                    targetItemInstanceId = form.targetStoneID,
                    resources = Items,
                    addLevel = form.addLevel
                },
                GeneratePlayStreamEvent = true,
            },
            (result) => {
                var jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;
                jsonResult.TryGetValue("success", out var successReturn);
                jsonResult.TryGetValue("level", out var level);
                var newLevel = Convert.ToInt32(level);
                if ((bool)successReturn)
                {
                    var targetInfo = Stones.Get(form.targetStoneID);
                    targetInfo.Level = newLevel;
                    success.Invoke(
                        form.targetStoneID,
                        itemInstanceIds
                    );
                }
            }
        );
    }
}
