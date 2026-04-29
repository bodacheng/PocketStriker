using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SKillAnalyzer
{
    private const string SetAllBodyMarkerManagersIn = "SetAllBodyMarkerManagersIn";
    private const string ClearMarkerManagers = "ClearMarkerManagers";
    private const string TurnOnFlag = "turn_on_flag";
    private const string SkillAnimationLabel = "skill_anim";

    public static readonly HashSet<string> AttackFrameStartMethodNames = new HashSet<string>
    {
        "SetRightHandMarkerManager",
        "SetLeftHandMarkerManager",
        "SetRightFootMarkerManager",
        "SetLeftFootMarkerManager",
        "SetRightHandWeaponMarkerManager",
        "SetLeftHandWeaponMarkerManager",
        "SetHeadMarkerManager",
        "SetTailMarkerManager"
    };

    public static readonly HashSet<string> AttackClearMethodNames = new HashSet<string>
    {
        "SetRightHandMarkerManager",
        "SetLeftHandMarkerManager",
        "SetRightFootMarkerManager",
        "SetLeftFootMarkerManager",
        "SetRightHandWeaponMarkerManager",
        "SetLeftHandWeaponMarkerManager",
        "SetHeadMarkerManager",
        "SetTailMarkerManager"
    };

    public static readonly HashSet<string> EffectsAttackFrameStartMethodNames = new HashSet<string>
    {
        "MagicForward",
        "MagicForwardOnBody",
        "Bullet_shoot_from_body_part",
        "Bullet_shoot_from_body_part_TD",
        "BlastAttack",
        "ReleasePreparedMagic",
        "ReleasePreparedMagicToAir",
        "MagicToEnemy"
    };

    private sealed class EventNameAndAtFrame
    {
        public string Name;
        public float StartFrame;
    }

    public static bool IsNormalAttackStartEvent(AnimationEvent animationEvent)
    {
        return (AttackFrameStartMethodNames.Contains(animationEvent.functionName) && animationEvent.intParameter != 0)
               || animationEvent.functionName == SetAllBodyMarkerManagersIn;
    }

    public static bool IsNormalAttackClearEvent(AnimationEvent animationEvent)
    {
        return (AttackClearMethodNames.Contains(animationEvent.functionName) && animationEvent.intParameter == 0)
               || animationEvent.functionName == ClearMarkerManagers;
    }

    public static bool IsEffectAttackStartEvent(AnimationEvent animationEvent)
    {
        return EffectsAttackFrameStartMethodNames.Contains(animationEvent.functionName);
    }

    public static bool IsAttackStartEvent(AnimationEvent animationEvent)
    {
        return IsNormalAttackStartEvent(animationEvent) || IsEffectAttackStartEvent(animationEvent);
    }

    public static async UniTask<IDictionary<string, AnimationClip>> AllSkillAnims(string type)
    {
        var animationClips = new Dictionary<string, AnimationClip>();
        var loadPath = Addressables.LoadResourceLocationsAsync(SkillAnimationLabel);
        await loadPath.Task;

        if (loadPath.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var path in loadPath.Result)
            {
                var handle = Addressables.LoadAssetAsync<AnimationClip>(path);
                await handle.Task;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }

                    continue;
                }

                var animationClip = handle.Result;
                if (!animationClips.ContainsKey(animationClip.name))
                {
                    animationClips.Add(animationClip.name, animationClip);
                }
            }
        }

        Addressables.Release(loadPath);
        return animationClips;
    }

    public async UniTask SkillsAnalyzeByFrames(
        string type,
        string targetEventName,
        float startMin,
        float startMax,
        float endMin,
        float endMax)
    {
        var animDic = await AllSkillAnims(type);
        var animationClips = animDic.Values.ToList();
        foreach (var clip in animationClips)
        {
            if (SkillFrameFilter(clip, targetEventName, startMin, startMax, endMin, endMax))
            {
                Debug.Log("符合：" + clip.name);
            }
        }
    }

    public void EvaluateSKill(AnimationClip clip)
    {
        var allNormalAttackFrames = new List<EventNameAndAtFrame>();
        var allMagicAttackFrames = new List<EventNameAndAtFrame>();
        var cancelFlagFrame = -1f;

        var mostQuick = new EventNameAndAtFrame
        {
            Name = string.Empty,
            StartFrame = 999f
        };

        var latestNormalAttackFrameClearEvent = new EventNameAndAtFrame
        {
            Name = string.Empty,
            StartFrame = -1f
        };

        var latestSpecialAttackEvent = new EventNameAndAtFrame
        {
            Name = string.Empty,
            StartFrame = -1f
        };

        foreach (var animationEvent in clip.events)
        {
            var eventFrame = new EventNameAndAtFrame
            {
                Name = animationEvent.functionName,
                StartFrame = animationEvent.time
            };

            if (IsEffectAttackStartEvent(animationEvent))
            {
                if (animationEvent.time < mostQuick.StartFrame)
                {
                    mostQuick = eventFrame;
                }

                if (animationEvent.time > latestSpecialAttackEvent.StartFrame)
                {
                    latestSpecialAttackEvent = eventFrame;
                }

                allMagicAttackFrames.Add(eventFrame);
            }

            if (IsNormalAttackStartEvent(animationEvent))
            {
                if (animationEvent.time < mostQuick.StartFrame)
                {
                    mostQuick = eventFrame;
                }

                allNormalAttackFrames.Add(eventFrame);
            }

            if (IsNormalAttackClearEvent(animationEvent) && animationEvent.time > latestNormalAttackFrameClearEvent.StartFrame)
            {
                latestNormalAttackFrameClearEvent = eventFrame;
            }

            if (animationEvent.functionName == TurnOnFlag)
            {
                cancelFlagFrame = animationEvent.time;
            }
        }

        if (allNormalAttackFrames.Count > 0)
        {
            Debug.Log("该技能共有" + allNormalAttackFrames.Count + "次攻击帧事件");
        }

        if (allMagicAttackFrames.Count > 0)
        {
            Debug.Log("共有" + allMagicAttackFrames.Count + "次特殊攻击帧事件");
        }

        if (latestSpecialAttackEvent.StartFrame > 0 && latestNormalAttackFrameClearEvent.StartFrame > 0)
        {
            Debug.Log("该技能同时拥有普通攻击和魔法攻击，难以进行机械评估，请进行主管判断");
            return;
        }

        var attackingLastFrame = -1f;
        if (latestNormalAttackFrameClearEvent.StartFrame > 0)
        {
            attackingLastFrame = latestNormalAttackFrameClearEvent.StartFrame;
        }
        else if (latestSpecialAttackEvent.StartFrame > 0)
        {
            attackingLastFrame = latestSpecialAttackEvent.StartFrame;
        }

        if (cancelFlagFrame < 0)
        {
            Debug.Log("该技能没有取消帧，无法继续进行分析");
            return;
        }

        if (cancelFlagFrame < attackingLastFrame)
        {
            Debug.Log("该技能的取消帧在最后的攻击帧之前，没法继续进行机械分析");
            return;
        }

        var attackEndToCancelStart = cancelFlagFrame - attackingLastFrame;
        Debug.Log("该技能最早的攻击事件是：" + mostQuick.Name + ", start at:" + mostQuick.StartFrame);
        Debug.Log("最终攻击帧距离取消帧" + attackEndToCancelStart);
        Debug.Log("攻击延迟与后摆的总和是：" + (mostQuick.StartFrame + attackEndToCancelStart));
        Debug.Log("本技能的时间评分是：" + (100 - 100 * (mostQuick.StartFrame + attackEndToCancelStart * 1 / 2)));
    }

    public void ReplaceAnimEventName(string type, string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
        {
            return;
        }

        var animationClips = new List<Object>();
        animationClips.AddRange(Resources.LoadAll("Animations/" + type + "/BasicPack", typeof(AnimationClip)));
        animationClips.AddRange(Resources.LoadAll("Animations/" + type + "/G_Attack_State", typeof(AnimationClip)));
        animationClips.AddRange(Resources.LoadAll("Animations/" + type + "/G_Attack_State_Stay", typeof(AnimationClip)));
        animationClips.AddRange(Resources.LoadAll("Animations/" + type + "/GMStates", typeof(AnimationClip)));

        foreach (var clipObject in animationClips)
        {
            var animationClip = clipObject as AnimationClip;
            if (animationClip == null)
            {
                continue;
            }

            foreach (var animationEvent in animationClip.events)
            {
                if (animationEvent.functionName == oldName)
                {
                    Debug.Log("讲动画片段：" + animationClip.name + "的函数" + oldName + "换了新名字" + newName);
                }
            }
        }
    }

    private static bool SkillFrameFilter(
        AnimationClip clip,
        string targetEventName,
        float startMin,
        float startMax,
        float endMin,
        float endMax)
    {
        var cancelFlagFrame = 0f;
        var allAttackStartFrames = new List<float>();
        var hasTargetEventName = false;

        foreach (var animationEvent in clip.events)
        {
            if (IsAttackStartEvent(animationEvent))
            {
                allAttackStartFrames.Add(animationEvent.time);
            }

            if (animationEvent.functionName == TurnOnFlag)
            {
                cancelFlagFrame = animationEvent.time;
            }

            hasTargetEventName |= string.IsNullOrEmpty(targetEventName) || animationEvent.functionName == targetEventName;
        }

        if (!hasTargetEventName)
        {
            return false;
        }

        if (allAttackStartFrames.Count == 0)
        {
            Debug.Log(clip.name + "貌似缺乏有效攻击帧控制类函数，需检查");
            return false;
        }

        if (Mathf.Approximately(cancelFlagFrame, 0))
        {
            Debug.Log(clip.name + "貌似没有取消flag,应做单独分析");
            return false;
        }

        var earliestStartFrame = allAttackStartFrames.Min();
        var latestEndFrame = allAttackStartFrames.Max();
        var attackEndToCancelStart = cancelFlagFrame - latestEndFrame;
        var startFilterOutcome = earliestStartFrame > startMin && earliestStartFrame <= startMax;
        var endFilterOutcome = attackEndToCancelStart > endMin && attackEndToCancelStart <= endMax;
        return startFilterOutcome && endFilterOutcome;
    }
}
