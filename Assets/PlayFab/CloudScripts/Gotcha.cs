using UnityEngine;
using PlayFab.ClientModels;
using System;
using Newtonsoft.Json;
using PlayFab.ServerModels;

public partial class CloudScript
{
    public static void GetDropTableInfo(Action<RandomResultTableListing> success, string tableID)
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
                var jsonResult = (PlayFab.Json.JsonObject)x.FunctionResult;
                jsonResult.TryGetValue("result", out var messageValue);
                var result = JsonConvert.DeserializeObject<GetRandomResultTablesResult>(messageValue.ToString());
                foreach (var tableInfo in result.Tables)
                {
                    // Debug.Log("Table:"+ tableInfo.Key);
                    // foreach (var stoneRate in tableInfo.Value.Nodes)
                    // {
                    //     Debug.Log(stoneRate.ResultItem + ":"+ stoneRate.Weight);
                    // }
                    success(tableInfo.Value);
                }
            }
        );
    }
}