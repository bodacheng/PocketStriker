using System.Collections.Generic;
using UnityEngine;

public static class AudioResourceLoaderCore
{
    public const string AudioLabel = "audio";
    public const string EffectAudioPath = "effect";

    public static readonly IDictionary<string, AudioClip> SoundClipsDic = new Dictionary<string, AudioClip>();

    public static string AudioClipKey(string additionalPath, string clipName)
    {
        return Join(additionalPath, clipName);
    }

    public static bool HasAudioClip(string key)
    {
        return SoundClipsDic.ContainsKey(key);
    }

    public static bool TryGetAudioClip(string key, out AudioClip audioClip)
    {
        return SoundClipsDic.TryGetValue(key, out audioClip);
    }

    public static void AddOrReplaceAudioClip(string key, AudioClip audioClip)
    {
        if (string.IsNullOrEmpty(key) || audioClip == null)
            return;

        SoundClipsDic[key] = audioClip;
    }

    public static void Clear()
    {
        SoundClipsDic.Clear();
    }

    private static string Join(params string[] parts)
    {
        return NormalizePath(string.Join("/", parts));
    }

    private static string NormalizePath(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace('\\', '/').Trim('/');
    }
}
