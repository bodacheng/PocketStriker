using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;

public partial class CloudScript
{
    public static void ExecuteCloudScriptMainSceneCommon(
        ExecuteCloudScriptRequest request, 
        Action<ExecuteCloudScriptResult> resultCallback, 
        Action<PlayFabError> errorCallback = null, 
        object customData = null, Dictionary<string, string> extraHeaders = null)
    {
        if (Application.isPlaying)
            ProgressLayer.Loading(string.Empty);
        
        PlayFabClientAPI.ExecuteCloudScript(
            request,
            (x)=>
            {
                resultCallback(x);
                ProgressLayer.Close();
            },
            (x)=>
            {
                errorCallback?.Invoke(x);
                PlayFabReadClient.ErrorReport(x);
            },
            customData, extraHeaders);
    }
}
