using System.Collections.Generic;
using UnityEngine;

public partial class AnimationManger
{
    List<AnimationClip> _hurtClipsBack, _hurtClipsLow, _hurtClipsHigh, _hurtClipsPress, _hurtClipsLay;
    List<AnimationClip> knockoffAnimations;
    
    void PlayLayerAnim_clip(AnimationClip clip, bool in_transition , float Duration)
    {
        AnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo(1);
        if (Animator.GetBool("in_transition"))
        {
            Animator.SetBool("in_transition", in_transition);
            Animator.Play("null", 1);//Animator.GetNextAnimatorStateInfo(1).fullPathHash
            Animator.Update(0);
            if (clip != null)
            {
                to_be_override_anim_name = "fullbody_empty1";
                animatorOverride[to_be_override_anim_name] = clip;
                Animator.CrossFade("full_body_state1", Duration);
            }
        }
        else
        {
            Animator.SetBool("in_transition", in_transition);
            if (AnimatorStateInfo.IsName("Full Body.null"))
            {
                if (clip != null)
                {
                    to_be_override_anim_name = "fullbody_empty1";
                }
                else
                {
                    Animator.SetBool("in_transition", false);
                    return;
                }
            }
            if (AnimatorStateInfo.IsName("Full Body.full_body_state1"))
            {
                to_be_override_anim_name = clip != null ? "fullbody_empty2" : null;
            }
            if (AnimatorStateInfo.IsName("Full Body.full_body_state2"))
            {
                to_be_override_anim_name = clip != null ? "fullbody_empty1" : null;
            }
            
            if (clip != null)
            {
                animatorOverride[to_be_override_anim_name] = clip;                    
                if (to_be_override_anim_name == "fullbody_empty2")
                {
                    Animator.CrossFade("full_body_state2", Duration);
                }
                if (to_be_override_anim_name == "fullbody_empty1")
                {
                    Animator.CrossFade("full_body_state1", Duration);
                }
            }else{
                Animator.CrossFade("null", Duration);
            }       
        }
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
        int ranDom = Random.Range(0, target.Count);
        return target[ranDom];
    }
    
    public AnimationClip GetRandomKnockOffAnim()
    {
        int ranDom = Random.Range(0, knockoffAnimations.Count);
        return knockoffAnimations[ranDom];
    }
}