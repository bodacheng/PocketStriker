using System.Collections.Generic;
using Json;
using Newtonsoft.Json;
using System.Linq;

public partial class MasterDataTool
{
    
#if UNITY_EDITOR
    
    public void OutputSKStonesCatalog()
    {
        var pFSKDefine = new PFDefine();
        pFSKDefine.CatalogVersion = "stone";
        var items = new List<PFDefine.Item>();
        var stoneDefinitionList = SkillConfigTable.SkillConfigRefDic.Values.ToList();
        for (var i = 0; i < stoneDefinitionList.Count; i++)
        {
            var item = new PFDefine.Item()
            {
                ItemId = stoneDefinitionList[i].RECORD_ID,
                DisplayName = stoneDefinitionList[i].REAL_NAME
            };
            item.CustomData = null;
            items.Add(item);
        }
        pFSKDefine.Catalog = items.ToArray();
        var json = JsonConvert.SerializeObject(pFSKDefine, Formatting.Indented);

        LocalJson.SaveToJsonFile_persistentDataPath("PlayFab", "StoneDefinationsJson.json", json);
    }

    public void OutputSKStonesStore()
    {
        var pFSKDefine = new PFStoreDefine();
        pFSKDefine.StoreId = "stone";
        var storeItems = new List<PFStoreDefine.StoreItem>();
        var stoneDefinitionList = SkillConfigTable.SkillConfigRefDic.Values.ToList();
        for (var i = 0; i < stoneDefinitionList.Count; i++)
        {
            var storeItem = new PFStoreDefine.StoreItem()
            {
                ItemId = stoneDefinitionList[i].RECORD_ID,
                VirtualCurrencyPrices = new PFStoreDefine.VirtualCurrencyPrices
                {
                    GD = 0
                }
            };
            storeItems.Add(storeItem);
        }
        pFSKDefine.Store = storeItems.ToArray();
        pFSKDefine.MarketingData = new PFStoreDefine._MarketingData
        {
            DisplayName = "stonestore"
        };

        string json = JsonConvert.SerializeObject(pFSKDefine, Formatting.Indented);
        json = "[" + json + "]";
        LocalJson.SaveToJsonFile_persistentDataPath("PlayFab", "StoneStoreDefinationsJson.json", json);
    }
#endif
}
