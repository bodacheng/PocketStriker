using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.Json;
using UnityEngine;

/// <summary>Same registered synchronous functions/envelope as GeminiClient, with bounded project-owned image consumption.</summary>
public sealed class PocketStrikerLegacyStoryClient
{
    readonly Func<ExecuteFunctionRequest, CancellationToken, UniTask<PocketStrikerStoryJobClient.Result>> execute;
    public PocketStrikerLegacyStoryClient(Func<ExecuteFunctionRequest, CancellationToken, UniTask<PocketStrikerStoryJobClient.Result>> execute = null)
    { this.execute = execute ?? Execute; }

    public async UniTask<PocketStrikerStoryJobClient.Result> Generate(string kind, string prompt, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var result = await execute(BuildRequest(kind, prompt), token).AttachExternalCancellation(token);
        bool valid = kind == "text" ? !string.IsNullOrWhiteSpace(result?.text)
            : result?.images?.Length == 1 && !string.IsNullOrWhiteSpace(result.images[0]?.url);
        if (!valid) throw new InvalidOperationException("Story function returned no usable result.");
        return result;
    }

    public static ExecuteFunctionRequest BuildRequest(string kind, string prompt)
    {
        if (kind != "text" && kind != "image") throw new ArgumentException("Invalid story request kind.");
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Missing story prompt.");
        string cacheKey;
        using (var hash = SHA256.Create())
            cacheKey = "ps_story_v2_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(kind + ":" + prompt))).Replace("-", "").ToLowerInvariant();
        object input = kind == "text"
            ? (object)new { prompt, model = "gemini-2.5-flash-lite", timeoutMs = 20000, cacheKey }
            : new { prompt, imageModel = "gemini-3.1-flash-image", sampleCount = 1, aspectRatio = "16:9", timeoutMs = 60000, cacheKey, storyId = cacheKey, sceneIndex = 1 };
        return new ExecuteFunctionRequest
        {
            FunctionName = kind == "text" ? "generateGeminiText" : "generateGeminiImages",
            FunctionParameter = input,
            GeneratePlayStreamEvent = false
        };
    }

    static UniTask<PocketStrikerStoryJobClient.Result> Execute(ExecuteFunctionRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var source = new UniTaskCompletionSource<PocketStrikerStoryJobClient.Result>();
        PlayFabCloudScriptAPI.ExecuteFunction(request, response =>
        {
            if (token.IsCancellationRequested) { source.TrySetCanceled(token); return; }
            if (response.Error != null || response.FunctionResult == null)
            { source.TrySetException(new InvalidOperationException("Story function failed.")); return; }
            try { source.TrySetResult(JsonUtility.FromJson<PocketStrikerStoryJobClient.Result>(PlayFabSimpleJson.SerializeObject(response.FunctionResult))); }
            catch { source.TrySetException(new InvalidOperationException("Invalid story function response.")); }
        }, _ =>
        {
            if (token.IsCancellationRequested) source.TrySetCanceled(token);
            else source.TrySetException(new InvalidOperationException("Story transport failed."));
        });
        // No implicit paid retry; a new battle owns the next attempt.
        return source.Task.AttachExternalCancellation(token);
    }
}
