using PlayFab.ClientModels;
using System.Collections.Generic;
using System;

public partial class CloudScript
{
    public static void ClaimAllPresentMails(List<MailItemInstance> _myMailList, Action<ItemInstance> saveToLocal)
    {
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "claimAllPresentMails",
                GeneratePlayStreamEvent = true
            },
            (result) => {
                PlayFab.Json.JsonObject jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;
                jsonResult.TryGetValue("diamond", out var dm);
                jsonResult.TryGetValue("gold", out var gd);
                jsonResult.TryGetValue("UnlockedItemInstanceIds", out var unLockedIdList);
                
                int.TryParse(dm.ToString(), out int dmInt);
                int.TryParse(gd.ToString(), out int gdInt);
                
                Currencies.CoinCount.Value += gdInt;
                Currencies.DiamondCount.Value += dmInt;
                
                var claimedIds = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<List<string>>(unLockedIdList.ToString());
                foreach (var data in _myMailList)
                {
                    if (claimedIds.Contains(data.ItemInstanceId))
                    {
                        data.RemainingUses = 0;
                        data.Set();
                        saveToLocal(data);
                    }
                }
            }
        );
    }
}
