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

    AnimationClip ResolveAnimationClip(string clipName)
    {
        return AnimationFullBodyRuntimeUtility.ResolveAnimationClip(toLoadAnims, clipName);
    }

    public AnimationFullBodyStateSnapshot CaptureAnimatorState()
    {
        return AnimationFullBodyRuntimeUtility.CaptureAnimatorState(Animator);
    }

    public bool RestoreAnimatorState(AnimationFullBodyStateSnapshot snapshot)
    {
        if (Animator == null)
            return false;

        Speed = AnimationFullBodyRuntimeUtility.ResolveRestoredSpeed(snapshot.Speed);
        var restored = AnimationFullBodyRuntimeUtility.RestoreAnimatorState(
            Animator,
            animatorOverride,
            snapshot,
            ResolveAnimationClip,
            out var restoredClip);
        if (restoredClip != null)
            _toUse = restoredClip;
        return restored;
    }

    public void AnimationTrigger(string clip, float returnDuration)
    {
        AnimationClip clipx = TryAnimationClip(clip);
        PlayLayerAnim_clip(clipx, returnDuration);
    }

    public void AnimationTrigger(AnimationClip clip, float returnDuration)
    {
        PlayLayerAnim_clip(clip, returnDuration);
    }

    public void AnimationTrigger(string clip, bool inTransition, float duration)
    {
        PlayLayerAnim(clip, inTransition, duration);
    }

    public void AnimationTrigger(AnimationClip clip, bool inTransition, float duration)
    {
        PlayLayerAnim_clip(clip, duration);
    }

    public void PlayLayerAnim(string clipName, bool inTransition, float duration)
    {
        PlayLayerAnim_clip(TryAnimationClip(clipName), duration);
    }

    AnimatorOverrideController animatorOverride;
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
