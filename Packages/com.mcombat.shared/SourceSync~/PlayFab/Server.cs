//using System;
//using System.Collections.Generic;
//using Api.Dto.Model;
//using dataAccess;
//using PlayFab;
//using PlayFab.AdminModels;
//using UnityEngine;

//namespace ServerAPI
//{
//    public static class Server
//    {
//        public static void Grant(string CatalogVersion, List<string> ItemIds, Action<PlayFab.ServerModels.GrantItemsToUserResult> action)
//        {
//            PlayFabServerAPI.GrantItemsToUser(
//                new PlayFab.ServerModels.GrantItemsToUserRequest()
//                {
//                    CatalogVersion = CatalogVersion,
//                    PlayFabId = Account._AccInfo.playerID,
//                    ItemIds = ItemIds
//                },
//                action,
//                //(PlayFab.ServerModels.GrantItemsToUserResult result) =>
//                //{
//                //    foreach (PlayFab.ServerModels.GrantedItemInstance grantedItemInstance in result.ItemGrantResults)
//                //    {
//                //        Debug.Log("get this thing:" + grantedItemInstance.ItemInstanceId);
//                //    }
//                //},
//                OnError);
//        }

//        public static void RandomRemove25Items()
//        {
//            List<RevokeInventoryItem> Items = new List<RevokeInventoryItem>();

//            foreach (KeyValuePair<string, StoneOfPlayerInfo> keyValuePair in Stones.Dic)
//            {
//                Items.Add(
//                    new RevokeInventoryItem()
//                    {
//                        PlayFabId = Account._AccInfo.playerID,
//                        ItemInstanceId = keyValuePair.Key
//                    });
//                if (Items.Count == 25)
//                {
//                    break;
//                }
//            }

//            PlayFabAdminAPI.RevokeInventoryItems(
//                new PlayFab.AdminModels.RevokeInventoryItemsRequest()
//                {
//                    Items = Items
//                },
//                (PlayFab.AdminModels.RevokeInventoryItemsResult result) => {
//                    for (int i =0; i < Items.Count; i++)
//                    {
//                        Debug.Log("this stone is deleted:" + Items[i].ItemInstanceId + " (" + Stones.Get(Items[i].ItemInstanceId).skillId + ")");
//                        Stones.RemoveStoneLocal(Items[i].ItemInstanceId);
//                    }
//                },
//                OnError);
//        }

//        public static void TestGotcha(Action<PlayFab.ServerModels.GrantItemsToUserResult> action)
//        {
//            PlayFabServerAPI.GetRandomResultTables(
//                new PlayFab.ServerModels.GetRandomResultTablesRequest
//                {
//                    CatalogVersion = "stoneTest2",
//                },
//                result => {
//                    foreach (KeyValuePair<string, PlayFab.ServerModels.RandomResultTableListing> kv in result.Tables)
//                    {
//                        List<string> ItemIds = new List<string>();
//                        foreach (PlayFab.ServerModels.ResultTableNode r in kv.Value.Nodes)
//                        {
//                            ItemIds.Add(r.ResultItem);
//                            Debug.Log("尝试获取这个技能石："+ r.ResultItem);
//                        }
//                        Grant("stoneTest2", ItemIds, action);
//                    }
                    
//                    Debug.Log("Completed getting drop tables");
//                },
//                error => {
//                    Debug.LogError(error.GenerateErrorReport());
//                }
//            );
//        }

//        static void OnError(PlayFabError playFabError)
//        {
//            Debug.Log(playFabError);
//        }
//    }
//}