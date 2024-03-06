using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;
using dataAccess;
using Newtonsoft.Json;

public partial class PlayFabReadClient
{
    public static readonly IDictionary<string, string> MyTimeLimitBundleBoughtLog = new Dictionary<string, string>();
    
    public static void UpdateUserData(UpdateUserDataRequest req, Action finished, Action fail = null)
    {
        ProgressLayer.Loading(Translate.Get("Updating"));
        PlayFabClientAPI.UpdateUserData
        (
            req,
            obj => {
                finished.Invoke();
                ProgressLayer.Close();
            },
            errorCallback => {
                fail?.Invoke();
                ProgressLayer.Close();
                ErrorReport(errorCallback);
            }
        );
    }
    
    public static void GetAllUserData(List<string> keys, Action<bool> finished)
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest()
            {
                PlayFabId = PlayerAccountInfo.Me.PlayFabId,
                Keys = keys
            },
            obj => {
                foreach (var key in keys)
                {
                    if (obj.Data.ContainsKey(key))
                    {
                        var userData = obj.Data[key];
                        PosKeySet value;
                        switch (key)
                        {
                            case "arcade":
                                value = JsonConvert.DeserializeObject<TeamPos>(userData.Value).ToPosKeySet();
                                TeamSet.Default = value;
                                break;
                            case "arena": // 因为一些特殊处理这个地方现在其实没用。
                                value = JsonConvert.DeserializeObject<TeamPos>(userData.Value).ToPosKeySet();
                                TeamSet.Arena3V3 = value;
                                break;
                            case "gangbang":
                                value = JsonConvert.DeserializeObject<TeamPos>(userData.Value).ToPosKeySet();
                                TeamSet.Gangbang = value;
                                break;
                            case "noAds":
                                Int32.TryParse(userData.Value, out var state);
                                PlayerAccountInfo.Me.noAdsState = state == 1;
                                break;
                        }
                        
                        if (key == PlayFabSetting._timeLimitBuyCode)
                        {
                            DicAdd<string,string>.Add(MyTimeLimitBundleBoughtLog, key, userData.Value);
                        }
                    }
                    else
                    {
                        switch (key)
                        {
                            case "arcade":
                                TeamSet.Default = new PosKeySet();
                                break;
                            case "arena":
                                TeamSet.Arena3V3 = new PosKeySet();
                                break;
                            case "gangbang":
                                TeamSet.Gangbang = new PosKeySet();
                                break;
                            case "noAds":
                                PlayerAccountInfo.Me.noAdsState = false;
                                break;
                        }
                    }
                }
                
                finished.Invoke(true);
            },
            errorCallback => {
                finished.Invoke(false);
                ErrorReport(errorCallback);
            }
        );
    }
    
    public static void UpdateUserTitleDisplayName(string displayName, Action<UpdateUserTitleDisplayNameResult> finished, Action<PlayFabError> error)
    {
        PlayFabClientAPI.UpdateUserTitleDisplayName(
            new UpdateUserTitleDisplayNameRequest
            {
                DisplayName = displayName
            },
            finished.Invoke,
            (x)=>
            {
                error(x);
                ErrorReport(x);
            });
    }
}

[Serializable]
public class TimeLimitedBuyData // 針對的title
{
    public string startTime;
    public string endTime;
    public string message;
    public string eventID;
    public int dmAmount;
}


