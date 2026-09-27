using System;
using System.Collections.Generic;
using Newtonsoft.Json;

// Client-side wire contracts: CloudScript payloads must not depend on the disabled
// PlayFab Admin or Server SDK assemblies.
internal static class CloudScriptPayloadUtility
{
    public static T Deserialize<T>(object payload)
    {
        if (payload == null)
            return default(T);

        // Some CloudScript functions return JSON strings; others return nested
        // objects/arrays, whose .ToString() is not necessarily JSON.
        var json = payload as string ?? PlayFab.Json.PlayFabSimpleJson.SerializeObject(payload);
        return JsonConvert.DeserializeObject<T>(json);
    }
}

[Serializable]
public class CloudScriptGrantedItemInstance
{
    public string ItemId;
}

[Serializable]
public class CloudScriptRandomResultTablesResult
{
    public Dictionary<string, CloudScriptRandomResultTableListing> Tables;
}

[Serializable]
public class CloudScriptRandomResultTableListing
{
    public List<CloudScriptResultTableNode> Nodes;
}

[Serializable]
public class CloudScriptResultTableNode
{
    public string ResultItem;
    public int Weight;
}

[Serializable]
public class CloudScriptUpdateUserInventoryItemDataRequest
{
    public string ItemInstanceId;
    public Dictionary<string, string> Data;
}

[Serializable]
public class CloudScriptRevokeInventoryItem
{
    public string ItemInstanceId;
    public string PlayFabId;
}
