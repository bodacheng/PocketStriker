using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Cysharp.Threading.Tasks;
public partial class SKillAnalyzer
{
    public async UniTask SkillsAnalyzeByFrames(string type, string targetEventName, float start_min, float start_max, float end_min, float end_max)
    {
        var animDic = await AllSkillAnims(type);
        List<AnimationClip> AnimationClips = animDic.Values.ToList();
        foreach (AnimationClip _clip in AnimationClips)
        {
            if (SkillFrameFilter(_clip, targetEventName, start_min, start_max, end_min, end_max))
            {
                Debug.Log("符合：" + _clip.name);
            }
        }
    }
    
    bool SkillFrameFilter(AnimationClip _clip, string targetEventName, float start_min, float start_max, float end_min, float end_max)
    {
        float earlieststartframe = 0, latestendframe;
        float cancelflagFrame = 0;
        List<float> allAttackStartFrames = new List<float>();
        
        bool hasTargetEventName = false;
        
        foreach (AnimationEvent e in _clip.events)
        {
            if (e.functionName == "SetAllBodyMarkerManagersIn")
            {
                allAttackStartFrames.Add(e.time);
            }
            if (AttackFrameStartMethodNames.Contains(e.functionName))
            {
                if (e.intParameter != 0)
                    allAttackStartFrames.Add(e.time);
            }
            if (EffectsAttackFrameStartMethodNames.Contains(e.functionName))
            {
                allAttackStartFrames.Add(e.time);
            }
            if (e.functionName == "turn_on_flag")
            {
                cancelflagFrame = e.time;
            }
            hasTargetEventName |= ((!string.IsNullOrEmpty(targetEventName)&& e.functionName == targetEventName) || string.IsNullOrEmpty(targetEventName));
        }
        
        if (!hasTargetEventName)
        {
            return false;
        }
        
        if (allAttackStartFrames.Count == 0)
        {
            Debug.Log(_clip.name + "貌似缺乏有效攻击帧控制类函数，需检查");
            return false;
        }
        
        if (Mathf.Approximately(cancelflagFrame, 0))
        {
            Debug.Log(_clip.name + "貌似没有取消flag,应做单独分析");
            return false;
        }
        earlieststartframe = allAttackStartFrames.Min();
        latestendframe = allAttackStartFrames.Max();
        
        float attackendtocancelstart = cancelflagFrame - latestendframe;
        bool startfilteroutcome = (earlieststartframe > start_min) && (earlieststartframe <= start_max);
        bool endfiltercoutcome = (attackendtocancelstart > end_min) && (attackendtocancelstart <= end_max);
        return startfilteroutcome && endfiltercoutcome;
    }
}
