using System;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;

public class GeminiClient : IAIClient
{
    private readonly GeminiConfig config;
    private readonly Imagen4Service imagenService;
    public Imagen4Service Imagen4Service => imagenService;
    
    public string ProviderName => "Gemini";
    public bool IsConfigured => config != null && config.IsValid();
    
    public GeminiClient(GeminiConfig config)
    {
        this.config = config;
        this.imagenService = new Imagen4Service(config, ExecuteFunctionAsync<Imagen4Service.CloudScriptImageResponse>);
        Debug.Log("GeminiClient Generated:" + this.config);
    }

    [Serializable]
    private class GeminiTextResponse
    {
        public string text;
    }

    [Serializable]
    private class GeminiTextCacheResponse
    {
        public string text;
        public string cacheKey;
        public bool cached;
    }
    
    /// <summary>
    /// Send a "question" and return Gemini's text response
    /// </summary>
    public async System.Threading.Tasks.Task<string> AskAsync(string question, int? timeoutMs = null)
    {
        var response = await ExecuteFunctionAsync<GeminiTextResponse>(
            config.TextFunctionName,
            new
            {
                prompt = question,
                model = config.Model,
                timeoutMs = timeoutMs ?? config.DefaultTimeoutMs
            });

        return response?.text ?? string.Empty;
    }

    public async System.Threading.Tasks.Task<string> AskWithCacheAsync(string question, string cacheKey, int? timeoutMs = null)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return await AskAsync(question, timeoutMs);
        }

        var response = await ExecuteFunctionAsync<GeminiTextCacheResponse>(
            config.TextFunctionName,
            new
            {
                prompt = question,
                model = config.Model,
                timeoutMs = timeoutMs ?? config.DefaultTimeoutMs,
                cacheKey = cacheKey
            });

        return response?.text ?? string.Empty;
    }

    public async System.Threading.Tasks.Task<Texture2D[]> GeneratePic(string prompt, int? count = null, string aspectRatio = null)
    {
        var actualCount = count ?? 1;
        var actualAspectRatio = aspectRatio ?? "1:1";
        
        var data = await imagenService.GenerateImagesImagenAsync(prompt, actualCount, actualAspectRatio);
        return data;
    }

    public async System.Threading.Tasks.Task<Texture2D[]> GeneratePicWithCache(string prompt, string cacheKey, string storyId, int? sceneIndex, int? count = null, string aspectRatio = null)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return await GeneratePic(prompt, count, aspectRatio);
        }

        var actualCount = count ?? 1;
        var actualAspectRatio = aspectRatio ?? "1:1";
        var data = await imagenService.GenerateImagesImagenAsync(prompt, actualCount, actualAspectRatio, null, cacheKey, storyId, sceneIndex);
        return data;
    }

    private Task<T> ExecuteFunctionAsync<T>(string functionName, object parameters)
    {
        var tcs = new TaskCompletionSource<T>();
        PlayFabCloudScriptAPI.ExecuteFunction(
            new ExecuteFunctionRequest
            {
                FunctionName = functionName,
                FunctionParameter = parameters,
                GeneratePlayStreamEvent = false
            },
            result =>
            {
                if (result.Error != null)
                {
                    var errorMessage = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result.Error);
                    tcs.TrySetException(new Exception($"Azure Function '{functionName}' error: {errorMessage}"));
                    return;
                }

                if (result.FunctionResult == null)
                {
                    tcs.TrySetException(new Exception($"Azure Function '{functionName}' returned no result"));
                    return;
                }

                try
                {
                    var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result.FunctionResult);
                    Debug.Log($"[Gemini] {functionName} raw result: {json}");
                    var data = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<T>(json);
                    tcs.TrySetResult(data);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(new Exception($"Failed to parse Azure Function '{functionName}' result: {e.Message}"));
                }
            },
            error =>
            {
                var message = error?.GenerateErrorReport() ?? "Unknown PlayFab error";
                tcs.TrySetException(new Exception($"Azure Function '{functionName}' failed: {message}"));
            });

        return tcs.Task;
    }
}
