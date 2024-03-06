using System;
using PlayFab;
using PlayFab.ClientModels;

public partial class PlayFabReadClient
{
    public static void GetAccountInfo(Action<bool> success)
    {
        PlayFabClientAPI.GetAccountInfo(
            new GetAccountInfoRequest
            {
                PlayFabId = PlayerAccountInfo.Me.PlayFabId
            },
            result =>
            {
                PlayerAccountInfo.Me.TitleDisplayName = result.AccountInfo.TitleInfo.DisplayName;
                PlayerAccountInfo.Me.PlayFabUserName = result.AccountInfo.Username;
                PlayerAccountInfo.Me.Email = result.AccountInfo.PrivateInfo.Email;
                
#if UNITY_IOS
                PlayerAccountInfo.Me.currentLinkedDeviceId = 
                    result.AccountInfo.IosDeviceInfo != null ? result.AccountInfo.IosDeviceInfo.IosDeviceId : null;
#endif
#if UNITY_ANDROID
                PlayerAccountInfo.Me.currentLinkedDeviceId = 
                    result.AccountInfo.AndroidDeviceInfo != null ? result.AccountInfo.AndroidDeviceInfo.AndroidDeviceId : null;
#endif
                success.Invoke(true);
            },
            errorCallback =>
            {
                success.Invoke(false);
                ErrorReport(errorCallback);
            }
        );
    }
}
