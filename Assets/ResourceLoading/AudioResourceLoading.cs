using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AudioResourceLoading
{
    private static AudioResourceLoading instance;
    private static readonly IDictionary<string, UniTaskCompletionSource<bool>> PendingAudioLoads =
        new Dictionary<string, UniTaskCompletionSource<bool>>();
    public static AudioResourceLoading Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new AudioResourceLoading();
            }
            return instance;
        }
    }
    static int cacheVersion;
    public static void Clear()
    {
        cacheVersion++;
        var pending = new List<UniTaskCompletionSource<bool>>(PendingAudioLoads.Values);
        PendingAudioLoads.Clear();
        AudioResourceLoaderCore.Clear();
        foreach (var source in pending)
            source.TrySetCanceled();
    }

    public IDictionary<string, AudioClip> SoundClipsDic => AudioResourceLoaderCore.SoundClipsDic;
    
    public async UniTask LoadAudioClipFromResourceAndPutItIntoDic(string additionalPath, string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return;

        var key = AudioResourceLoaderCore.AudioClipKey(additionalPath, clipName);
        if (AudioResourceLoaderCore.HasAudioClip(key))
            return;

        if (PendingAudioLoads.TryGetValue(key, out var pendingLoad))
        {
            await pendingLoad.Task;
            return;
        }

        var version = cacheVersion;
        var loadSource = new UniTaskCompletionSource<bool>();
        PendingAudioLoads.Add(key, loadSource);
        try
        {
            await AudioResourceLoadingCore.LoadAudioClipIntoCache(
                additionalPath,
                clipName,
                AddressablesLogic.HasIndexedTag,
                AddressablesLogic.CheckKeyExist,
                async address =>
                {
                    var clip = await AddressablesLogic.LoadT<AudioClip>(address);
                    if (version != cacheVersion)
                        throw new OperationCanceledException("Audio cache was cleared during loading.");
                    return clip;
                },
                Debug.LogWarning);
            loadSource.TrySetResult(true);
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
            if (PendingAudioLoads.TryGetValue(key, out var current) && ReferenceEquals(current, loadSource))
                PendingAudioLoads.Remove(key);
        }
    }
}
