using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PlayFab.ClientModels;

// Runs against the installed PlayFab serializer without starting Unity or making
// network requests. Only the request dispatcher and Unity logger are substituted.
internal static class CloudScriptContractTests
{
    private static int checks;

    public static int Main()
    {
        try
        {
            var edit = new CloudScriptUpdateUserInventoryItemDataRequest
            {
                ItemInstanceId = "stone-1",
                Data = new Dictionary<string, string> { { "unitInstanceId", null }, { "slot", "2" } }
            };
            var editJson = JObject.Parse(PlayFab.Json.PlayFabSimpleJson.SerializeObject(edit));
            Check((string)editJson["ItemInstanceId"] == "stone-1", "inventory item ID wire name");
            Check(editJson["Data"]["unitInstanceId"].Type == JTokenType.Null && (string)editJson["Data"]["slot"] == "2",
                "skill unequip null and slot survive SDK serialization");

            var revoke = new CloudScriptRevokeInventoryItem { ItemInstanceId = "stone-2", PlayFabId = "player-1" };
            var revokeJson = JObject.Parse(PlayFab.Json.PlayFabSimpleJson.SerializeObject(revoke));
            Check((string)revokeJson["ItemInstanceId"] == "stone-2" && (string)revokeJson["PlayFabId"] == "player-1",
                "stone level-up resource wire names");

            const string rewards = "[{\"ItemId\":\"unit-1\"}]";
            var parsedRewards = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<object>(rewards);
            Check(CloudScriptPayloadUtility.Deserialize<List<CloudScriptGrantedItemInstance>>(parsedRewards)[0].ItemId == "unit-1",
                "nested reward array response");
            Check(CloudScriptPayloadUtility.Deserialize<List<CloudScriptGrantedItemInstance>>(rewards)[0].ItemId == "unit-1",
                "JSON string reward response");
            Check(CloudScriptPayloadUtility.Deserialize<CloudScriptRandomResultTablesResult>(null) == null,
                "absent optional response");

            const string tableJson = "{\"Tables\":{\"Other\":{\"Nodes\":[{\"ResultItem\":\"wrong\",\"Weight\":99}]},\"Requested\":{\"Nodes\":[{\"ResultItem\":\"skill-1\",\"Weight\":7}]}}}";
            var parsedTable = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<object>(tableJson);
            Check(CloudScriptPayloadUtility.Deserialize<CloudScriptRandomResultTablesResult>(parsedTable).Tables["Requested"].Nodes[0].Weight == 7,
                "nested drop table response");
            Check(CloudScriptPayloadUtility.Deserialize<CloudScriptRandomResultTablesResult>(tableJson).Tables["Requested"].Nodes[0].ResultItem == "skill-1",
                "JSON string drop table response");

            CloudScript.Response = new PlayFab.Json.JsonObject { { "result", parsedTable } };
            int calls = 0;
            CloudScriptRandomResultTableListing selected = null;
            CloudScript.GetDropTableInfo(table => { calls++; selected = table; }, "Requested");
            Check(calls == 1 && selected.Nodes[0].ResultItem == "skill-1", "only requested table is displayed once");

            CloudScript.GetDropTableInfo(table => { calls++; selected = table; }, "Missing");
            Check(calls == 2 && selected == null, "missing table clears stale UI");
            CloudScript.Response = null;
            CloudScript.GetDropTableInfo(table => { calls++; selected = table; }, "Requested");
            Check(calls == 3 && selected == null, "absent function result clears stale UI");

            CloudScript.Response = new PlayFab.Json.JsonObject { { "result", "invalid JSON" } };
            CloudScript.GetDropTableInfo(table => { calls++; selected = table; }, "Requested");
            Check(calls == 4 && selected == null, "malformed JSON result clears stale UI without throwing");
            CloudScript.Response = new PlayFab.Json.JsonObject { { "result", new List<object>() } };
            CloudScript.GetDropTableInfo(table => { calls++; selected = table; }, "Requested");
            Check(calls == 5 && selected == null, "unexpected array result clears stale UI without throwing");
            CloudScript.Response = new PlayFab.Json.JsonObject { { "result", "{\"Tables\":null}" } };
            CloudScript.GetDropTableInfo(table => { calls++; selected = table; }, "Requested");
            Check(calls == 6 && selected == null, "null tables result clears stale UI without throwing");

            Console.WriteLine("PASS: " + checks + " CloudScript contract checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("FAILED: " + message);
        checks++;
    }
}

public partial class CloudScript
{
    internal static object Response;

    public static void ExecuteCloudScriptMainSceneCommon(ExecuteCloudScriptRequest request, Action<ExecuteCloudScriptResult> callback)
    {
        if (request.FunctionName != "stoneDropTableInfo")
            throw new InvalidOperationException("Unexpected CloudScript request");
        callback(new ExecuteCloudScriptResult { FunctionResult = Response });
    }
}

namespace UnityEngine
{
    internal static class Debug
    {
        public static void LogWarning(object message) { }
    }
}
