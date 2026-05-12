using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AnimationResourceLoader
{
    private static AnimationResourceLoader instance;

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

    public void Clear()
    {
        AnimationResourceLoaderCore.Clear();
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

        var result = await AddressablesLogic.LoadT<AnimationClip>(AnimationResourceKeyUtility.SkillAnimationAddress(type, key));
        AnimationResourceLoaderCore.AddAnimationClip(clipKey, result);
    }
}
