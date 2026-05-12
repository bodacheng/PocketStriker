using System.Collections.Generic;
using UnityEngine;

public static class AnimationResourceLoaderCore
{
    private static readonly IDictionary<string, AnimationClip> AnimationClipDic = new Dictionary<string, AnimationClip>();
    public static readonly IDictionary<string, List<AnimationClip>> SeriesAnimationClipsDic = new Dictionary<string, List<AnimationClip>>();

    public static void Clear()
    {
        AnimationClipDic.Clear();
        SeriesAnimationClipsDic.Clear();
    }

    public static bool HasAnimationClip(string key)
    {
        return AnimationClipDic.ContainsKey(key);
    }

    public static AnimationClip GetAnimationClip(string key)
    {
        AnimationClipDic.TryGetValue(key, out var animationClip);
        return animationClip;
    }

    public static void AddAnimationClip(string key, AnimationClip clip)
    {
        if (clip == null || string.IsNullOrEmpty(key) || AnimationClipDic.ContainsKey(key))
        {
            return;
        }

        AnimationClipDic.Add(key, clip);
    }
}
