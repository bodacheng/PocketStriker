using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AnimationResourceLoader
{
    private static AnimationResourceLoader instance;
    private static readonly IDictionary<string, UniTaskCompletionSource<AnimationClip>> PendingAnimationLoads =
        new Dictionary<string, UniTaskCompletionSource<AnimationClip>>();

    public static AnimationResourceLoader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new AnimationResourceLoader();
            }

            return instance;
        }
    }

    public static IDictionary<string, List<AnimationClip>> SeriesAnimationClipsDic => AnimationResourceLoaderCore.SeriesAnimationClipsDic;

    public static int CacheVersion { get; private set; }

    public void Clear()
    {
        CacheVersion++;
        var pending = new List<UniTaskCompletionSource<AnimationClip>>(PendingAnimationLoads.Values);
        PendingAnimationLoads.Clear();
        AnimationResourceLoaderCore.Clear();
        AnimationManger.ClearResourceLoadCaches();
        foreach (var source in pending)
            source.TrySetCanceled();
    }

    public AnimationClip GetAnimationClip(string key)
    {
        return AnimationResourceLoaderCore.GetAnimationClip(key);
    }

    public static async UniTask LoadAnim(string type, string key)
    {
        var clipKey = AnimationResourceKeyUtility.SkillClipKey(type, key);
        if (AnimationResourceLoaderCore.HasAnimationClip(clipKey))
        {
            return;
        }

        if (PendingAnimationLoads.TryGetValue(clipKey, out var pendingLoad))
        {
            await pendingLoad.Task;
            return;
        }

        var cacheVersion = CacheVersion;
        var loadSource = new UniTaskCompletionSource<AnimationClip>();
        PendingAnimationLoads.Add(clipKey, loadSource);
        try
        {
            var result = await AddressablesLogic.LoadT<AnimationClip>(
                AnimationResourceKeyUtility.SkillAnimationAddress(type, key));
            if (cacheVersion != CacheVersion)
                throw new OperationCanceledException("Animation cache was cleared during loading.");
            AnimationResourceLoaderCore.AddAnimationClip(clipKey, result);
            loadSource.TrySetResult(result);
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException canceled)
                loadSource.TrySetCanceled(canceled.CancellationToken);
            else
                loadSource.TrySetException(exception);
            await loadSource.Task;
        }
        finally
        {
            if (PendingAnimationLoads.TryGetValue(clipKey, out var current) && ReferenceEquals(current, loadSource))
                PendingAnimationLoads.Remove(clipKey);
        }
    }
}
