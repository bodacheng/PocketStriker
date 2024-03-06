using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;

public partial class PlayFabReadClient
{
    // 当前玩家会显示的石头包商品。因为如果这个玩家够买过对应商品的话，则不会再显示
    // ReadOnlyUserData主要是用来管购买记录
    public static readonly List<string> ShowStoneBundleIds = new List<string>();
    public static void GetAllReadOnlyUserData(List<string> keys, Action<bool> finished)
    {
        PlayFabClientAPI.GetUserReadOnlyData
        (
            new GetUserDataRequest()
            {
                PlayFabId = PlayerAccountInfo.Me.PlayFabId,
                Keys = keys
            },
            (obj) =>
            {
                // 石头包购买
                ShowStoneBundleIds.Clear();
                foreach (var productId in keys)
                {
                    if (!obj.Data.ContainsKey(productId))
                    {
                        ShowStoneBundleIds.Add(productId);
                    }
                }
                finished.Invoke(true);
            },
            errorCallback =>
            {
                finished.Invoke(false);
                PlayFabReadClient.ErrorReport(errorCallback);
            }
        );
    }
}
