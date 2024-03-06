using System.Collections.Generic;
using Json;
using Newtonsoft.Json;
using System.Linq;

public partial class MasterDataTool
{
    
#if UNITY_EDITOR
    
    public void OutputMonstersCatalog()
    {
        var pFSKDefine = new PFDefine
        {
            CatalogVersion = "unit"
        };
        var items = new List<PFDefine.Item>();
        var charsConfigs = Units.Dic.Values.ToList();
        for (var i = 0; i < charsConfigs.Count; i++)
        {
            var item = new PFDefine.Item()
            {
                ItemId = charsConfigs[i].RECORD_ID,
                DisplayName = charsConfigs[i].REAL_NAME
            };
            var c_CustomData = new PFDefine.C_CustomData();
            c_CustomData.zokusei = ((int)charsConfigs[i].element).ToString();
            item.CustomData = c_CustomData.AsPlayFabVer();
            items.Add(item);
        }
        pFSKDefine.Catalog = items.ToArray();
        var json = JsonConvert.SerializeObject(pFSKDefine, Formatting.Indented);
        LocalJson.SaveToJsonFile_persistentDataPath("PlayFab", "MonsterDefinitionJson.json", json);
    }

    public void OutputMonsterStore()
    {
        var pFSKDefine = new PFStoreDefine();
        pFSKDefine.StoreId = "unit";
        var storeItems = new List<PFStoreDefine.StoreItem>();

        var unitConfigs = Units.Dic.Values.ToList();
        for (var i = 0; i < unitConfigs.Count; i++)
        {
            PFStoreDefine.StoreItem storeItem = new PFStoreDefine.StoreItem()
            {
                ItemId = unitConfigs[i].RECORD_ID,
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
            DisplayName = "unit_store"
        };

        string json = JsonConvert.SerializeObject(pFSKDefine, Formatting.Indented);
        json = "[" + json + "]";
        LocalJson.SaveToJsonFile_persistentDataPath("PlayFab", "UnitStoreDefinitionJson.json", json);
    }
    
#endif
}
