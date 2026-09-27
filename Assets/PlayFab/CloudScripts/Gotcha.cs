using UnityEngine;
using PlayFab.ClientModels;
using System;
using Newtonsoft.Json;

public partial class CloudScript
{
    public static void GetDropTableInfo(Action<CloudScriptRandomResultTableListing> success, string tableID)
    {
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "stoneDropTableInfo",
                FunctionParameter = new
                {
                    TableID = tableID//"GotchaX9"
                }
            } ,
            (x) =>
            {
                var jsonResult = x.FunctionResult as PlayFab.Json.JsonObject;
                if (jsonResult == null || !jsonResult.TryGetValue("result", out var messageValue))
                {
                    Debug.LogWarning("CloudScript returned no drop table result for " + tableID);
                    success?.Invoke(null);
                    return;
                }

                CloudScriptRandomResultTablesResult result;
                try
                {
                    result = CloudScriptPayloadUtility.Deserialize<CloudScriptRandomResultTablesResult>(messageValue);
                }
                catch (JsonException error)
                {
                    Debug.LogWarning("CloudScript returned an invalid drop table for " + tableID + ": " + error.Message);
                    success?.Invoke(null);
                    return;
                }
                if (result?.Tables == null || !result.Tables.TryGetValue(tableID, out var tableInfo))
                {
                    Debug.LogWarning("CloudScript returned no drop table named " + tableID);
                    success?.Invoke(null);
                    return;
                }

                success?.Invoke(tableInfo);
            }
        );
    }
}
