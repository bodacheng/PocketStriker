using System;
using System.Collections.Generic;
using UnityEngine;

public struct AnimationFullBodyStateSnapshot
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

public static class AnimationFullBodyRuntimeUtility
{
    public const int FullBodyLayerIndex = 1;
    public const string InTransitionParameter = "in_transition";
    public const string FullBodyNullStateName = "Full Body.null";
    public const string FullBodyState1Name = "Full Body.full_body_state1";
    public const string FullBodyState2Name = "Full Body.full_body_state2";
    public const string FullBodyNullPlayName = "null";
    public const string FullBodyState1OverrideKey = "fullbody_empty1";
    public const string FullBodyState2OverrideKey = "fullbody_empty2";
    public const string FullBodyState1Trigger = "fullbody_trigger1";
    public const string FullBodyState2Trigger = "fullbody_trigger2";

    private static readonly int FullBodyState1Hash = Animator.StringToHash(FullBodyState1Name);
    private static readonly int FullBodyState2Hash = Animator.StringToHash(FullBodyState2Name);
    private static readonly int FullBodyNullHash = Animator.StringToHash(FullBodyNullStateName);

    public static AnimationClip ResolveAnimationClip(IDictionary<string, AnimationClip> clips, string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || clips == null)
            return null;

        if (clips.TryGetValue(clipName, out var clip) && clip != null)
            return clip;

        foreach (var pair in clips)
        {
            if (pair.Value != null && pair.Value.name == clipName)
                return pair.Value;
        }

        return null;
    }

    public static AnimationFullBodyStateSnapshot CaptureAnimatorState(Animator animator)
    {
        var snapshot = new AnimationFullBodyStateSnapshot
        {
            IsValid = false,
            Clip = null,
            ClipName = null,
            AnimatorStateName = null,
            OverrideKey = null,
            NormalizedTime = 0f,
            InTransition = false,
            Speed = animator != null ? animator.speed : 1f,
            StateHash = 0,
            LayerIndex = FullBodyLayerIndex
        };

        if (animator == null)
            return snapshot;

        snapshot.InTransition = animator.GetBool(InTransitionParameter);

        var layerIndex = snapshot.LayerIndex;
        var currentStateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
        var selectedStateInfo = currentStateInfo;
        AnimationClip selectedClip = null;
        float selectedWeight = -1f;

        SelectWeightedClip(animator.GetCurrentAnimatorClipInfo(layerIndex), currentStateInfo, ref selectedClip, ref selectedStateInfo, ref selectedWeight);

        if (animator.IsInTransition(layerIndex))
        {
            var nextStateInfo = animator.GetNextAnimatorStateInfo(layerIndex);
            SelectWeightedClip(animator.GetNextAnimatorClipInfo(layerIndex), nextStateInfo, ref selectedClip, ref selectedStateInfo, ref selectedWeight, true);
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

    public static float ResolveRestoredSpeed(float speed)
    {
        return speed <= 0.05f ? 1f : speed;
    }

    public static bool RestoreAnimatorState(
        Animator animator,
        AnimatorOverrideController animatorOverride,
        AnimationFullBodyStateSnapshot snapshot,
        Func<string, AnimationClip> resolveAnimationClip,
        out AnimationClip restoredClip)
    {
        restoredClip = null;
        if (animator == null)
            return false;

        if (!snapshot.IsValid)
        {
            animator.SetBool(InTransitionParameter, false);
            return false;
        }

        var clip = snapshot.Clip != null ? snapshot.Clip : resolveAnimationClip?.Invoke(snapshot.ClipName);
        if (clip == null)
            return false;

        if (!string.IsNullOrEmpty(snapshot.OverrideKey))
            TryAssignOverrideClip(animatorOverride, snapshot.OverrideKey, clip);

        var normalizedTime = NormalizeTime(snapshot.NormalizedTime);

        animator.Update(0f);
        var layerIndex = snapshot.LayerIndex >= 0 ? snapshot.LayerIndex : FullBodyLayerIndex;
        if (snapshot.StateHash != 0)
        {
            animator.Play(snapshot.StateHash, layerIndex, normalizedTime);
        }
        else if (!string.IsNullOrEmpty(snapshot.AnimatorStateName))
        {
            animator.Play(snapshot.AnimatorStateName, layerIndex, normalizedTime);
        }
        else
        {
            return false;
        }

        animator.Update(0f);
        animator.SetBool(InTransitionParameter, snapshot.InTransition);
        restoredClip = clip;
        return true;
    }

    public static void PlayFullBodyClip(Animator animator, AnimatorOverrideController animatorOverride, AnimationClip clip, float returnDuration)
    {
        if (animator == null)
            return;

        var animatorStateInfo = animator.GetCurrentAnimatorStateInfo(FullBodyLayerIndex);
        if (animator.GetBool(InTransitionParameter))
        {
            animator.SetBool(InTransitionParameter, clip != null);
            animator.Play(FullBodyNullPlayName, FullBodyLayerIndex);
            animator.Update(0);
            if (clip != null && TryAssignOverrideClip(animatorOverride, FullBodyState1OverrideKey, clip))
                animator.SetTrigger(FullBodyState1Trigger);
            return;
        }

        animator.SetBool(InTransitionParameter, clip != null);
        var overrideKey = ResolveNextOverrideKey(animatorStateInfo, clip != null);
        if (clip == null)
        {
            animator.CrossFade(FullBodyNullPlayName, returnDuration);
            return;
        }

        if (!TryAssignOverrideClip(animatorOverride, overrideKey, clip))
            return;

        if (overrideKey == FullBodyState2OverrideKey)
            animator.SetTrigger(FullBodyState2Trigger);
        if (overrideKey == FullBodyState1OverrideKey)
            animator.SetTrigger(FullBodyState1Trigger);
    }

    public static AnimationClip GetRandomClip(IList<AnimationClip> clips)
    {
        if (clips == null || clips.Count == 0)
            return null;

        return clips[UnityEngine.Random.Range(0, clips.Count)];
    }

    public static bool TryAssignOverrideClip(AnimatorOverrideController animatorOverride, string overrideKey, AnimationClip clip)
    {
        if (animatorOverride == null || string.IsNullOrEmpty(overrideKey) || clip == null)
            return false;

        try
        {
            animatorOverride[overrideKey] = clip;
            return true;
        }
        catch (ArgumentException)
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

    private static void SelectWeightedClip(
        AnimatorClipInfo[] clipInfos,
        AnimatorStateInfo stateInfo,
        ref AnimationClip selectedClip,
        ref AnimatorStateInfo selectedStateInfo,
        ref float selectedWeight,
        bool includeEqualWeight = false)
    {
        if (clipInfos == null || clipInfos.Length == 0)
            return;

        foreach (var clipInfo in clipInfos)
        {
            if (clipInfo.clip == null)
                continue;

            var shouldSelect = includeEqualWeight
                ? clipInfo.weight >= selectedWeight
                : clipInfo.weight > selectedWeight;
            if (!shouldSelect)
                continue;

            selectedClip = clipInfo.clip;
            selectedWeight = clipInfo.weight;
            selectedStateInfo = stateInfo;
        }
    }

    private static string ResolveNextOverrideKey(AnimatorStateInfo animatorStateInfo, bool hasClip)
    {
        if (!hasClip)
            return null;

        if (animatorStateInfo.IsName(FullBodyNullStateName))
            return FullBodyState1OverrideKey;
        if (animatorStateInfo.IsName(FullBodyState1Name))
            return FullBodyState2OverrideKey;
        if (animatorStateInfo.IsName(FullBodyState2Name))
            return FullBodyState1OverrideKey;

        return FullBodyState1OverrideKey;
    }

    private static string GetOverrideKeyForState(int stateHash)
    {
        if (stateHash == FullBodyState1Hash)
            return FullBodyState1OverrideKey;
        if (stateHash == FullBodyState2Hash)
            return FullBodyState2OverrideKey;
        return null;
    }

    private static string GetStateNameForHash(int stateHash)
    {
        if (stateHash == FullBodyState1Hash)
            return FullBodyState1Name;
        if (stateHash == FullBodyState2Hash)
            return FullBodyState2Name;
        if (stateHash == FullBodyNullHash)
            return FullBodyNullStateName;
        return null;
    }

    private static float NormalizeTime(float normalizedTime)
    {
        if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
            return 0f;

        return Mathf.Repeat(normalizedTime, 1f);
    }
}
