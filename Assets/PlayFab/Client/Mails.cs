using UnityEngine;
using PlayFab;
using System.Collections.Generic;
using PlayFab.ClientModels;
using System;
using Newtonsoft.Json;
using Json;
using System.IO;
using Cysharp.Threading.Tasks;

public partial class PlayFabReadClient
{
    #region MAIL

    private static List<MailItemInstance> MyMailList = new List<MailItemInstance>();
    private static readonly Dictionary<string, CatalogItem> CatalogItems = new Dictionary<string, CatalogItem>();
    
    public static List<MailItemInstance> GetMailsData(bool onlyUnRead = false)
    {
        if (!onlyUnRead)
            return MyMailList;
        return MyMailList.FindAll(x=> x.NotClaimed());
    }

    public static CatalogItem GetCatalogItemByDisplayName(string displayName)
    {
        if (!CatalogItems.ContainsKey(displayName)) return null;
        var item = CatalogItems[displayName];
        return item;
    }

    public static void GetMailCatalogItems(string itemCatalog, Action<bool> finished)
    {
        PlayFabClientAPI.GetCatalogItems(
            new GetCatalogItemsRequest
            {
                CatalogVersion = itemCatalog
            },
            (x)=>
            {
                foreach (var v in x.Catalog)
                {
                    DicAdd<string, CatalogItem>.Add(CatalogItems, v.DisplayName, v);
                }
                finished?.Invoke(true);
            },
            (x) =>
            {
                finished?.Invoke(false);
                ErrorReport(x);
            }
        );
    }
    
    /// <summary>
    /// 点击某邮件后打开邮件会使用此函数
    /// </summary>
    public static MailItemInstance Get(string itemInstanceId)
    {
        for (var i = 0; i < MyMailList.Count; i++)
        {
            if (MyMailList[i].ItemInstanceId == itemInstanceId)
                return MyMailList[i];
        }
        return null;
    }
    
    /// <summary>
    /// 添加实际邮件信息
    /// </summary>
    /// <param name="mailData"></param>
    static void AddMailData(MailItemInstance mailData)
    {
        var exist = MyMailList.Find(x=> x.ItemInstanceId == mailData.ItemInstanceId);
        if (exist == null)
            MyMailList.Add(mailData);
    }
    
    public static void SaveReadMailAsJson(ItemInstance mailOfPlayer)
    {
        var json = JsonConvert.SerializeObject(mailOfPlayer);
        LocalJson.SaveToJsonFile_persistentDataPath("readmail", mailOfPlayer.ItemInstanceId + ".json", json);
    }

    /// <summary>
    /// 已读取邮件的获取
    /// </summary>
    public static async UniTask LoadReadMailsAsync()
    {
        var path = Application.persistentDataPath + "/readmail";
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    // Run the IO operation on a background thread
                    var dataAsJson = await UniTask.Run(() => File.ReadAllText(file));
                    var mailOfPlayerModel = JsonConvert.DeserializeObject<MailItemInstance>(dataAsJson);
                    // Switch back to the main thread to modify Unity objects
                    await UniTask.Yield(); // Switch back to the main thread
                    AddMailData(mailOfPlayerModel);
                }
                catch (Exception e)
                {
                    Debug.Log(e.ToString());
                }
            }
        }
    }

    public static void DeleteAllLocalMails()
    {
        var toDelete = MyMailList.FindAll(x => (x.RemainingUses.Value <= 0));
        foreach (var d in toDelete)
        {
            MyMailList.Remove(d);
        }
        
        string path = Application.persistentDataPath + "/readmail";
        if (Directory.Exists(path))
        {
            foreach (string file in Directory.GetFiles(path))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Debug.Log(e.ToString());
                }
            }
        }
    }
    
    public static void ClaimPresent(string itemId, string itemCatalog, Action<ItemInstance> saveToLocal)
    {
        PlayFabClientAPI.UnlockContainerItem(
            new UnlockContainerItemRequest
            {
                CatalogVersion = itemCatalog,
                ContainerItemId = itemId
            },
            resultCallback => {
                Debug.Log(":"+ resultCallback.UnlockedItemInstanceId);
                ItemInstance target = null;

                if (resultCallback.VirtualCurrency != null)
                {
                    foreach (var kv in resultCallback.VirtualCurrency)
                    {
                        if (kv.Key == PlayFabSetting._GoldCode)
                        {
                            Currencies.CoinCount.Value += (int)kv.Value;
                        }
                        if (kv.Key == PlayFabSetting._DiamondCode)
                        {
                            Currencies.DiamondCount.Value += (int)kv.Value;
                        }
                    }
                }
                
                foreach (var data in MyMailList)
                {
                    if (data.ItemInstanceId == resultCallback.UnlockedItemInstanceId)
                    {
                        data.RemainingUses = 0;
                        data.Set();
                        target = data;
                    }
                }
                if (target != null)
                    saveToLocal.Invoke(target);
            },
            ErrorReport
        );
    }
    
    public static void ClaimAllPresentMails(Action<ItemInstance> saveToLocal)
    {
        CloudScript.ClaimAllPresentMails(MyMailList, saveToLocal);
    }
    
    #endregion
}
