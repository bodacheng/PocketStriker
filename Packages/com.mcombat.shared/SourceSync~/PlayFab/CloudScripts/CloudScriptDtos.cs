using System;
using System.Collections.Generic;

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
