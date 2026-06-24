using System;
using PlayFab;
using PlayFab.ClientModels;

public partial class CloudScript
{
    public static void RequestAdReward(string virtualCurrency, int amount, Action success = null, int markAsAdWatched = -1)
    {
        ProgressLayer.Loading(string.Empty);
        PlayFabClientAPI.ExecuteCloudScript(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "advertisementReward",
                FunctionParameter = markAsAdWatched == -1 ? 
                    new
                    {
                        Amount = amount,
                        VirtualCurrency = virtualCurrency
                    }:
                    new
                    {
                        Amount = amount,
                        VirtualCurrency = virtualCurrency,
                        stage = markAsAdWatched
                    }
            },
            (x)=>
            {
                ProgressLayer.Close();
                PopupLayer.ArrangeWarnWindow(Translate.Get("GotAdWatchedReward"));
                var returnValue = (PlayFab.Json.JsonObject) x.FunctionResult;
                returnValue.TryGetValue("result", out var result);
                var resultJson = (PlayFab.Json.JsonObject) result;
                resultJson.TryGetValue("Balance", out var Balance);
                int.TryParse(Balance.ToString(), out var intBalance);
                resultJson.TryGetValue("BalanceChange", out var BalanceChange);
                int.TryParse(BalanceChange.ToString(), out var intBalanceChange);
                
                resultJson.TryGetValue("VirtualCurrency", out var VirtualCurrency);
                switch (VirtualCurrency.ToString())
                {
                    case "DM":
                        Currencies.DiamondCount.Value = intBalance;
                        break;
                    case "GD":
                        Currencies.CoinCount.Value = intBalance;
                        break;
                }
                success?.Invoke();
                //PopupLayer.ArrangeWarnWindow("YOU GOT "+ intBalanceChange+ " " + VirtualCurrency);
            },
            PlayFabReadClient.ErrorReport
        );
    }
}
