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

    private const int FullBodyLayerIndex = 1;
    private static readonly int FullBodyState1Hash = UnityEngine.Animator.StringToHash("Full Body.full_body_state1");
    private static readonly int FullBodyState2Hash = UnityEngine.Animator.StringToHash("Full Body.full_body_state2");
    private static readonly int FullBodyNullHash = UnityEngine.Animator.StringToHash("Full Body.null");

    public struct AnimatorStateSnapshot
    {
        public bool IsValid;
        public string ClipName;
        public string AnimatorStateName;
        public string OverrideKey;
        public float NormalizedTime;
        public bool InTransition;
        public float Speed;
        public AnimationClip Clip;
        public int StateHash;
        public int LayerIndex;
    }

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

    AnimationClip ResolveAnimationClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
            return null;
        if (toLoadAnims != null && toLoadAnims.TryGetValue(clipName, out var clip) && clip != null)
        {
            return clip;
        }
        if (toLoadAnims != null)
        {
            foreach (var pair in toLoadAnims)
            {
                if (pair.Value != null && pair.Value.name == clipName)
                {
                    return pair.Value;
                }
            }
        }
        return null;
    }

    public AnimatorStateSnapshot CaptureAnimatorState()
    {
        var snapshot = new AnimatorStateSnapshot
        {
            IsValid = false,
            Clip = null,
            ClipName = null,
            AnimatorStateName = null,
            OverrideKey = null,
            NormalizedTime = 0f,
            InTransition = false,
            Speed = Animator != null ? Animator.speed : 1f,
            StateHash = 0,
            LayerIndex = FullBodyLayerIndex
        };

        if (Animator == null)
            return snapshot;

        snapshot.InTransition = Animator.GetBool("in_transition");

        var layerIndex = snapshot.LayerIndex;
        var currentStateInfo = Animator.GetCurrentAnimatorStateInfo(layerIndex);
        var selectedStateInfo = currentStateInfo;
        AnimationClip selectedClip = null;
        float selectedWeight = -1f;

        var currentClipInfos = Animator.GetCurrentAnimatorClipInfo(layerIndex);
        if (currentClipInfos != null && currentClipInfos.Length > 0)
        {
            foreach (var clipInfo in currentClipInfos)
            {
                if (clipInfo.clip == null)
                    continue;
                if (clipInfo.weight > selectedWeight)
                {
                    selectedClip = clipInfo.clip;
                    selectedWeight = clipInfo.weight;
                    selectedStateInfo = currentStateInfo;
                }
            }
        }

        if (Animator.IsInTransition(layerIndex))
        {
            var nextStateInfo = Animator.GetNextAnimatorStateInfo(layerIndex);
            var nextClipInfos = Animator.GetNextAnimatorClipInfo(layerIndex);
            if (nextClipInfos != null && nextClipInfos.Length > 0)
            {
                foreach (var clipInfo in nextClipInfos)
                {
                    if (clipInfo.clip == null)
                        continue;
                    if (clipInfo.weight >= selectedWeight)
                    {
                        selectedClip = clipInfo.clip;
                        selectedWeight = clipInfo.weight;
                        selectedStateInfo = nextStateInfo;
                    }
                }
            }
        }

        snapshot.StateHash = selectedStateInfo.fullPathHash;
        snapshot.AnimatorStateName = GetStateNameForHash(snapshot.StateHash);
        snapshot.OverrideKey = GetOverrideKeyForState(snapshot.StateHash);
        snapshot.NormalizedTime = selectedStateInfo.normalizedTime;

        if (selectedClip != null)
        {
            snapshot.Clip = selectedClip;
            snapshot.ClipName = selectedClip.name;
        }

        snapshot.IsValid = snapshot.Clip != null && !string.IsNullOrEmpty(snapshot.AnimatorStateName) && snapshot.OverrideKey != null;
        return snapshot;
    }

    public bool RestoreAnimatorState(AnimatorStateSnapshot snapshot)
    {
        if (Animator == null)
            return false;

        if (snapshot.Speed <= 0.05f)
        {
            Speed = 1f;
        }
        else
        {
            Speed = snapshot.Speed;
        }

        if (!snapshot.IsValid)
        {
            Animator.SetBool("in_transition", false);
            return false;
        }

        var clip = snapshot.Clip != null ? snapshot.Clip : ResolveAnimationClip(snapshot.ClipName);
        if (clip == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(snapshot.OverrideKey))
        {
            TryAssignOverrideClip(snapshot.OverrideKey, clip);
        }

        _toUse = clip;

        var normalizedTime = snapshot.NormalizedTime;
        if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
        {
            normalizedTime = 0f;
        }
        else
        {
            normalizedTime = Mathf.Repeat(normalizedTime, 1f);
        }

        Animator.Update(0f);
        var layerIndex = snapshot.LayerIndex >= 0 ? snapshot.LayerIndex : FullBodyLayerIndex;
        if (snapshot.StateHash != 0)
        {
            Animator.Play(snapshot.StateHash, layerIndex, normalizedTime);
        }
        else if (!string.IsNullOrEmpty(snapshot.AnimatorStateName))
        {
            Animator.Play(snapshot.AnimatorStateName, layerIndex, normalizedTime);
        }
        else
        {
            return false;
        }
        Animator.Update(0f);
        Animator.SetBool("in_transition", snapshot.InTransition);
        return true;
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

    AnimatorStateInfo AnimatorStateInfo;
    AnimatorOverrideController animatorOverride;

    static string GetOverrideKeyForState(int stateHash)
    {
        if (stateHash == FullBodyState1Hash)
            return "fullbody_empty1";
        if (stateHash == FullBodyState2Hash)
            return "fullbody_empty2";
        return null;
    }

    static string GetStateNameForHash(int stateHash)
    {
        if (stateHash == FullBodyState1Hash)
            return "Full Body.full_body_state1";
        if (stateHash == FullBodyState2Hash)
            return "Full Body.full_body_state2";
        if (stateHash == FullBodyNullHash)
            return "Full Body.null";
        return null;
    }

    bool TryAssignOverrideClip(string overrideKey, AnimationClip clip)
    {
        if (animatorOverride == null || string.IsNullOrEmpty(overrideKey) || clip == null)
        {
            return false;
        }

        try
        {
            animatorOverride[overrideKey] = clip;
            return true;
        }
        catch (System.ArgumentException)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            animatorOverride.GetOverrides(overrides);
            foreach (var pair in overrides)
            {
                if (pair.Key != null && pair.Key.name == overrideKey)
                {
                    animatorOverride[pair.Key] = clip;
                    return true;
                }
            }
        }

        return false;
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
