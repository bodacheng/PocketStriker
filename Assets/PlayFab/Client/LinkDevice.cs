using System;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

/// <summary>
/// 这一部分我们的目标是不用它。link设备的流程应该靠playfab那边的自动化
/// </summary>
public partial class PlayFabReadClient
{
    public static void LinkDevice(Action success)
    {
#if UNITY_IOS
        PlayFabClientAPI.LinkIOSDeviceID(
            new LinkIOSDeviceIDRequest
            {
                DeviceId = CustomId,
                ForceLink = true
            },
            (x) =>
            {
                Debug.Log(x);
                PlayerAccountInfo.Me.currentLinkedDeviceId = CustomId;
                success.Invoke();
            },
            ErrorReport
        );
#endif
        
#if UNITY_ANDROID
        PlayFabClientAPI.LinkAndroidDeviceID(
            new LinkAndroidDeviceIDRequest
            {
                AndroidDeviceId = CustomId,
                ForceLink = true
            },
            (x) =>
            {
                Debug.Log(x);
                PlayerAccountInfo.Me.currentLinkedDeviceId = CustomId;
                success.Invoke();
            },
            ErrorReport
        );
#endif
    }

    static void UnLinkDevice(string unlinkDeviceId, Action success)
    {
#if UNITY_IOS
        PlayFabClientAPI.UnlinkIOSDeviceID(
            new UnlinkIOSDeviceIDRequest
            {
                DeviceId = unlinkDeviceId
            },
            (x) =>
            {
                Debug.Log(x);
                PlayerAccountInfo.Me.currentLinkedDeviceId = null;
                success.Invoke();
            },
            ErrorReport
        );
#endif
        
#if UNITY_ANDROID
        PlayFabClientAPI.UnlinkAndroidDeviceID(
            new UnlinkAndroidDeviceIDRequest
            {
                AndroidDeviceId = unlinkDeviceId
            },
            (x) =>
            {
                Debug.Log(x);
                PlayerAccountInfo.Me.currentLinkedDeviceId = null;
                success.Invoke();
            },
            ErrorReport
        );
#endif
    }
}
