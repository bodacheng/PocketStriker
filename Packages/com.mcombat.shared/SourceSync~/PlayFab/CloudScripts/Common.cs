using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;
using PlayFab.CloudScriptModels;
using ExecuteCloudScriptResult = PlayFab.ClientModels.ExecuteCloudScriptResult;

public partial class CloudScript
{
    public static void ExecuteCloudScriptMainSceneCommon(
        ExecuteCloudScriptRequest request, 
        Action<ExecuteCloudScriptResult> resultCallback, 
        Action<PlayFabError> errorCallback = null, 
        object customData = null, Dictionary<string, string> extraHeaders = null,
        bool showLoading = true, bool showErrorPopup = true)
    {
        if (showLoading && Application.isPlaying)
            ProgressLayer.Loading(string.Empty);

        void Attempt(int attempt)
        {
            PlayFabClientAPI.ExecuteCloudScript(
                request,
                (x)=>
                {
                    if (x.Error != null)
                    {
                        var scriptError = ToPlayFabError(x);
                        if (PlayFabReadClient.ShouldRetryPlayFabRequest(scriptError, attempt))
                        {
                            PlayFabReadClient.RetryPlayFabRequest(() => Attempt(attempt + 1), attempt, request.FunctionName);
                            return;
                        }

                        errorCallback?.Invoke(scriptError);
                        if (showLoading)
                            ProgressLayer.Close();
                        if (showErrorPopup)
                            PlayFabReadClient.ErrorReport(scriptError);
                        return;
                    }

                    resultCallback?.Invoke(x);
                    if (showLoading)
                        ProgressLayer.Close();
                },
                (x)=>
                {
                    if (PlayFabReadClient.ShouldRetryPlayFabRequest(x, attempt))
                    {
                        PlayFabReadClient.RetryPlayFabRequest(() => Attempt(attempt + 1), attempt, request.FunctionName);
                        return;
                    }

                    errorCallback?.Invoke(x);
                    if (showLoading)
                        ProgressLayer.Close();
                    if (showErrorPopup)
                        PlayFabReadClient.ErrorReport(x);
                },
                customData, extraHeaders);
        }

        Attempt(1);
    }

    static PlayFabError ToPlayFabError(ExecuteCloudScriptResult result)
    {
        var errorCode = PlayFabErrorCode.CloudScriptAPIRequestError;
        if (!string.IsNullOrEmpty(result?.Error?.Error))
        {
            Enum.TryParse(result.Error.Error, out errorCode);
        }

        var functionName = string.IsNullOrEmpty(result?.FunctionName) ? "CloudScript" : result.FunctionName;
        var message = result?.Error == null
            ? $"{functionName} failed."
            : $"{functionName}: {result.Error.Error} {result.Error.Message}";

        return new PlayFabError
        {
            Error = errorCode,
            ErrorMessage = message
        };
    }
    
    public static void ExecuteFunctionCommon(
        ExecuteFunctionRequest request, 
        Action<ExecuteFunctionResult> resultCallback, 
        Action<PlayFabError> errorCallback = null, 
        object customData = null, Dictionary<string, string> extraHeaders = null,
        bool showLoading = true, bool showErrorPopup = true)
    {
        if (showLoading && Application.isPlaying)
            ProgressLayer.Loading(string.Empty);

        void Attempt(int attempt)
        {
            PlayFabCloudScriptAPI.ExecuteFunction( request,
                (x)=>
                {
                    resultCallback?.Invoke(x);
                    if (showLoading)
                        ProgressLayer.Close();
                },
                (x)=>
                {
                    if (PlayFabReadClient.ShouldRetryPlayFabRequest(x, attempt))
                    {
                        PlayFabReadClient.RetryPlayFabRequest(() => Attempt(attempt + 1), attempt, request.FunctionName);
                        return;
                    }

                    errorCallback?.Invoke(x);
                    if (showLoading)
                        ProgressLayer.Close();
                    if (showErrorPopup)
                        PlayFabReadClient.ErrorReport(x);
                },
                customData, extraHeaders);
        }

        Attempt(1);
    }
}
