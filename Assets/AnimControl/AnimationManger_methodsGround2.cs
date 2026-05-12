using System.Collections.Generic;
using UnityEngine;

public partial class AnimationManger
{
    List<AnimationClip> _hurtClipsBack, _hurtClipsLow, _hurtClipsHigh, _hurtClipsPress, _hurtClipsLay;
    List<AnimationClip> knockoffAnimations;
    
    void PlayLayerAnim_clip(AnimationClip clip, float returnDuration)
    {
        AnimationFullBodyRuntimeUtility.PlayFullBodyClip(Animator, animatorOverride, clip, returnDuration);
    }
    
    public AnimationClip GetRandomHurtAnim(string hurtPos)
    {
        List<AnimationClip> target;
        switch (hurtPos)
        {
            case "back":
                target = _hurtClipsBack;
            break;
            case "lay":
                target = _hurtClipsLay;
            break;
            case "high":
                target = _hurtClipsHigh;
            break;
            case "low":
                target = _hurtClipsLow;
            break;
            case "press":
                target = _hurtClipsPress;
            break;
            default:
                target = _hurtClipsHigh;
            break;
        }
        return AnimationFullBodyRuntimeUtility.GetRandomClip(target);
    }
    
    public AnimationClip GetRandomKnockOffAnim()
    {
        return AnimationFullBodyRuntimeUtility.GetRandomClip(knockoffAnimations);
    }
}
