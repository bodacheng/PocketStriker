using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class AudioResourceLoadingCore
{
    public delegate UniTask<AudioClip> LoadAudioClipDelegate(string key);

    public static async UniTask LoadAudioClipIntoCache(
        string additionalPath,
        string clipName,
        Func<string, bool> hasIndexedTag,
        Func<string, string, bool> checkKeyExist,
        LoadAudioClipDelegate loadAudioClip,
        Action<string> logWarning = null)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return;

        var key = AudioResourceLoaderCore.AudioClipKey(additionalPath, clipName);
        if (AudioResourceLoaderCore.HasAudioClip(key))
            return;

        if (hasIndexedTag?.Invoke(AudioResourceLoaderCore.AudioLabel) == true &&
            checkKeyExist != null &&
            !checkKeyExist(AudioResourceLoaderCore.AudioLabel, key))
        {
            logWarning?.Invoke($"[AudioResourceLoading] Missing audio addressable key: {key}");
            return;
        }

        if (loadAudioClip == null)
        {
            logWarning?.Invoke($"[AudioResourceLoading] Missing audio loader for key: {key}");
            return;
        }

        var audioClip = await loadAudioClip(key);
        if (audioClip == null)
        {
            logWarning?.Invoke($"[AudioResourceLoading] Failed to load audio clip: {key}");
            return;
        }

        AudioResourceLoaderCore.AddOrReplaceAudioClip(key, audioClip);
    }

    public static UniTask LoadEffectAudioClipIntoCache(
        string clipName,
        Func<string, bool> hasIndexedTag,
        Func<string, string, bool> checkKeyExist,
        LoadAudioClipDelegate loadAudioClip,
        Action<string> logWarning = null)
    {
        return LoadAudioClipIntoCache(
            AudioResourceLoaderCore.EffectAudioPath,
            clipName,
            hasIndexedTag,
            checkKeyExist,
            loadAudioClip,
            logWarning);
    }
}
