using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

// 大体的image就是能够直接通过动画文件名来随时决定animator当中各个层动画的播放。而不再需要复杂的animator。。
// 然而这和unity的这个animator系统的初衷产生矛盾
// 整个系统建立在animator本身这样的前提下：
// 1. animator在状态迁移过程中，“当前状态”为迁移出发状态
// 2. 在迁移过程中，如果激发了迁移终点状态向另一状态（或返回至出发状态）迁移的条件，（起码我们知道trigger满足这点），则状态机会在按原节奏到达终点状态后，
// 再去向第三者状态迁移，之前的迁移过程并不会受干扰，后触发的迁移条件也并不会被遗忘，一切会按顺序进行
// 从而也就是说对最后要触发那个状态来说，从条件激活到开始进入会发生一点延迟。

public partial class AnimationManger
{
    Animator Animator;
    public AnimationClip _toUse;

    List<string> parameters = new List<string>();
    IDictionary<string, AnimationClip> toLoadAnims;
    string to_be_override_anim_name;

    float speed = 1;

    public float Speed
    {
        get => speed;
        private set
        {
            speed = value;
            Animator.speed = speed;
        }
    }

    public class SpeedBuff
    {
        public readonly string reasonKey;
        public readonly float speed;

        public SpeedBuff(string reasonKey, float speed)
        {
            this.reasonKey = reasonKey;
            this.speed = speed;
        }
    }

    private readonly List<SpeedBuff> _speedBuffs = new List<SpeedBuff>();

    public void AddSpeedBuff(string reasonKey, float speed)
    {
        _speedBuffs.Add(new SpeedBuff(reasonKey,speed));
        SpeedBuff maxSpeedBuff = _speedBuffs.OrderByDescending(buff => buff.speed).FirstOrDefault();
        Speed = maxSpeedBuff.speed;
    }
    
    public void RemoveSpeedBuff(string reasonKey)
    {
        _speedBuffs.RemoveAll(x=> x.reasonKey == reasonKey);
        if (_speedBuffs.Count > 0)
        {
            SpeedBuff maxSpeedBuff = _speedBuffs.OrderByDescending(buff => buff.speed).FirstOrDefault();
            Speed = maxSpeedBuff.speed;
        }
        else
        {
            Speed = 1;
        }
    }

    public Animator AnimatorRef
    {
        get => Animator;
        set => Animator = value;
    }

    private Sequence animFreezeSequence;
    public Sequence AnimFreezeSequence => animFreezeSequence;
    public void FrameFreeze()
    {
        animFreezeSequence = DOTween.Sequence();
        animFreezeSequence.Append(DOTween.To(() => Animator.speed, x => Animator.speed = x, Speed - 1, FightGlobalSetting.HurtFreezeInDuration))
            .Append(DOTween.To(() => Animator.speed, x => Animator.speed = x, Speed, FightGlobalSetting.HurtFreezeOutDuration).SetEase(Ease.InExpo));
    }
    
    public AnimationClip TryAnimationClip(string clip_name)
    {
        if (!string.IsNullOrEmpty(clip_name))
        {
            toLoadAnims.TryGetValue(clip_name, out _toUse);
            if (_toUse != null)
            {
                return _toUse;
            }
            Debug.Log("邪门了." + clip_name);
        }
        return null;
    }
    
    public bool GetBool(string anim)
    {
        return Animator.GetBool(anim);
    }

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
    {
        return Animator.GetCurrentAnimatorStateInfo(layerIndex);
    }
    
    public void SetTrigger(string anim)
    {
        if (parameters.Contains(anim))
        {
            Animator.SetTrigger(anim);
        }
    }

    public void AnimationTrigger(string clip, bool in_Transition,float Duration)
    {
        PlayLayerAnim(clip, in_Transition, Duration);
    }
    
    public void AnimationTrigger(AnimationClip clip, bool in_Transition , float Duration)
    {
        PlayLayerAnim_clip(clip,in_Transition,Duration);
    }
    
    AnimatorStateInfo AnimatorStateInfo;
    AnimatorOverrideController animatorOverride;
    
    public void PlayLayerAnim(string clip_name, bool in_transition,float Duration)
    {
        AnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo(1);
        if (Animator.GetBool("in_transition"))
        {
            Animator.SetBool("in_transition", in_transition);
            Animator.Play("null", 1);//Animator.GetNextAnimatorStateInfo(1).fullPathHash
            Animator.Update(0);
            if (!string.IsNullOrEmpty(clip_name))
            {
                to_be_override_anim_name = "fullbody_empty1";
                animatorOverride[to_be_override_anim_name] = TryAnimationClip(clip_name);
                Animator.CrossFade("full_body_state1", Duration);
            }else{
            }
        }
        else
        {
            Animator.SetBool("in_transition", in_transition);
            if (AnimatorStateInfo.IsName("Full Body.null"))
            {
                if (!string.IsNullOrEmpty(clip_name))
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
                to_be_override_anim_name = !string.IsNullOrEmpty(clip_name) ? "fullbody_empty2" : null;
            }
            if (AnimatorStateInfo.IsName("Full Body.full_body_state2"))
            {
                to_be_override_anim_name = !string.IsNullOrEmpty(clip_name) ? "fullbody_empty1" : null;
            }
            
            if (!string.IsNullOrEmpty(clip_name))
            {
                animatorOverride[to_be_override_anim_name] = TryAnimationClip(clip_name);                    
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
}

// 下面这些不用了。改使用了crossfade后animator不会被判定为迁移。
//public bool GetOnAniTransitionFlag()
//{
//    return Animator.GetAnimatorTransitionInfo(1).IsName("Full Body.full_body_state1 -> Full Body.full_body_state2") 
//            || Animator.GetAnimatorTransitionInfo(1).IsName("Full Body.full_body_state2 -> Full Body.full_body_state1");
//}

//public bool GetOnAniTransitionFlag2()
//{
//        return !Animator.GetAnimatorTransitionInfo(1).IsName("Full Body.full_body_state1 -> Full Body.full_body_state2") 
//            && !Animator.GetAnimatorTransitionInfo(1).IsName("Full Body.full_body_state2 -> Full Body.full_body_state1")
//            && !Animator.GetAnimatorTransitionInfo(1).IsName("Full Body.null -> Full Body.full_body_state1");
//}

//public bool GetOnAniFinishingFlag()
//{
//    return Animator.GetAnimatorTransitionInfo(1).IsName("Full Body.full_body_state1 -> Full Body.null") || Animator.GetAnimatorTransitionInfo(1).IsName("Full Body.full_body_state2 -> Full Body.null");
//}