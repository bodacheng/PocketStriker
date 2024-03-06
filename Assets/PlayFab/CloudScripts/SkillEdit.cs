using PlayFab.ClientModels;
using System.Collections.Generic;
using System;
using dataAccess;
using Newtonsoft.Json;

public partial class CloudScript
{
    // k v : stoneid , equipingMonster, slot
    public static void UpdateSkillEdit(IDictionary<string, Tuple<string, string>> ToEditStones, Action<IDictionary<string, Tuple<string, string>>> success)
    {
        var Items = new List<PlayFab.ServerModels.UpdateUserInventoryItemDataRequest>();
        foreach (var pair in ToEditStones)
        {
            var itemUpdate = new PlayFab.ServerModels.UpdateUserInventoryItemDataRequest
            {
                //PlayFabId = AccountSet._AccInfo.playerID,
                ItemInstanceId = pair.Key,
                Data = new Dictionary<string, string>()
                {
                    { "unitInstanceId", string.IsNullOrEmpty(pair.Value.Item1) ? null : pair.Value.Item1  },
                    { "slot", string.IsNullOrEmpty(pair.Value.Item2) ? null : pair.Value.Item2 }
                },
            };
            Items.Add(itemUpdate);
        }

        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest()
            {
                FunctionName = "skillEdit", // Arbitrary function name (must exist in your uploaded cloud.js file)
                FunctionParameter = new { inputValue = Items }, // The parameter provided to your function
                GeneratePlayStreamEvent = true, // Optional - Shows this event in PlayStream
            },
        (ExecuteCloudScriptResult result) => {
                var jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;
                jsonResult.TryGetValue("changedStone", out var changedStone); // note how "messageValue" directly corresponds to the JSON values set in CloudScript
                var ChangedDic = new Dictionary<string, Tuple<string, string>>();
                var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(changedStone);
                var changedStoneList = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<List<StoneOfPlayerInfo>>(json);
                foreach (var stone in changedStoneList)
                {
                    ChangedDic.Add(stone.InstanceId, new Tuple<string, string>(stone.unitInstanceId, stone.slot));
                }
                success.Invoke(ChangedDic);
            }
        );
    }
}
