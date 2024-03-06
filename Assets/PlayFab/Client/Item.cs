using PlayFab;
using PlayFab.ClientModels;
using dataAccess;
using System;
using System.Linq;

public partial class PlayFabReadClient
{
    public static void LoadItems(Action<bool> finished)
    {
        dataAccess.Units.Dic.Clear();
        Stones.ClearData();
        Stones.ClearRender();
        
        PlayFabClientAPI.GetUserInventory(
            new GetUserInventoryRequest(),
            result =>
            {
                OnGetUserInventory(result, finished);
            },
            errorCallback => {
                finished?.Invoke(false);
                ErrorReport(errorCallback);
            }
        );
    }
    
    static void OnGetUserInventory(GetUserInventoryResult result, Action<bool> finished)
    {
        void _AddMailData(ItemInstance item)
        {
            var mailData = new MailItemInstance();
            Copier<ItemInstance,MailItemInstance>.Copy(item, mailData);
            AddMailData(mailData);
        }
        
        foreach (var item in result.Inventory)
        {
            if (item.CatalogVersion == PlayFabSetting._UnitCatalog)
            {
                if (item.ItemClass == "GiftBox")
                {
                    _AddMailData(item);
                }
                else
                {
                    var info = new UnitInfo
                    {
                        id = item.ItemInstanceId,
                        r_id = item.ItemId
                    };
                    DicAdd<string, UnitInfo>.Add(dataAccess.Units.Dic, item.ItemInstanceId, info);
                }
            }
            else if (item.CatalogVersion == PlayFabSetting._StoneCatalog)
            {
                if (SkillConfigTable.GetSkillConfigByRecordId(item.ItemId) != null)
                {
                    var info = new StoneOfPlayerInfo
                    {
                        InstanceId = item.ItemInstanceId,
                        SkillId = item.ItemId,
                        Level = Convert.ToInt32(item.CustomData.GetOrDefault("level", "1")),
                        unitInstanceId = item.CustomData.GetOrDefault("unitInstanceId"),
                        slot = item.CustomData.GetOrDefault("slot"),
                        Born = item.CustomData.GetOrDefault("born")
                    };
                    Stones.Add(info);
                }
                else
                {
                    // 可能是stone类型的bundle等等
                }
            }
            else if (item.CatalogVersion == PlayFabSetting._MailCatalog)
            {
                _AddMailData(item);
            }
        }
        
        MyMailList = MyMailList.OrderByDescending(x => x.NotClaimed()).ToList();
        
        foreach (var kv in result.VirtualCurrency)
        {
            if (kv.Key == PlayFabSetting._GoldCode)
            {
                Currencies.CoinCount.Value = kv.Value;
            }
            else if (kv.Key == PlayFabSetting._DiamondCode)
            {
                Currencies.DiamondCount.Value = kv.Value;
            }
            else if (kv.Key == PlayFabSetting._ArenaTicketCode)
            {
                Currencies.ArenaTicket.Value = kv.Value;
            }
            else if (kv.Key == PlayFabSetting._AdTicketCode)
            {
                Currencies.AdTicket.Value = kv.Value;
            }
        }
        
        foreach (var kv in result.VirtualCurrencyRechargeTimes)
        {
            if (kv.Key == PlayFabSetting._ArenaTicketCode)
            {
                Currencies.SecondsToRechargeArenaTicket = kv.Value.SecondsToRecharge;
                Currencies.ArenaTicketRechargeMax = kv.Value.RechargeMax;
            }
            if (kv.Key == PlayFabSetting._AdTicketCode)
            {
                Currencies.SecondsToRechargeAdTicket = kv.Value.SecondsToRecharge;
                Currencies.AdTicketRechargeMax = kv.Value.RechargeMax;
            }
        }
        
        finished?.Invoke(true);
    }
}
