using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;

public partial class CloudScript
{
    public static void SubtractVirtualCurrency(string code, int count, Action success)
    {
        if (Application.isPlaying)
            ProgressLayer.Loading(string.Empty);
        
        PlayFabClientAPI.ExecuteCloudScript(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "SubtractVirtualCurrency",
                FunctionParameter = new
                {
                    VirtualCurrencyCode = code,
                    Count = count
                },
                GeneratePlayStreamEvent = true
            },
            (x)=>
            {
                ProgressLayer.Close();
                Debug.Log(x.FunctionResult);
                var returnValue = (PlayFab.Json.JsonObject) x.FunctionResult;
                if (returnValue == null)
                {
                    Debug.Log("返回值为null?");
                    return;
                }
                returnValue.TryGetValue("result", out var result);
                if (result == null)
                {
                    Debug.Log("返回值为null");
                    return;
                }
                var resultJson = (PlayFab.Json.JsonObject) result;
                resultJson.TryGetValue("Balance", out var Balance);
                int.TryParse(Balance.ToString(), out int intBalance);
                resultJson.TryGetValue("VirtualCurrency", out var VirtualCurrency);
                switch (VirtualCurrency.ToString())
                {
                    case "TK":
                        Currencies.ArenaTicket.Value = intBalance;
                        break;
                    case "DM":
                        Currencies.DiamondCount.Value = intBalance;
                        break;
                    case "GD":
                        Currencies.CoinCount.Value = intBalance;
                        break;
                    case "AD":
                        Currencies.AdTicket.Value = intBalance;
                        break;
                }
                success.Invoke();
            },
            PlayFabReadClient.ErrorReport
        );
    }
}
