#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public partial class SKillAnalyzer
{
    // 技能的速度和范围这两个要素，我们合并理解为“时空优势度”。
    // 我们希望按这样的原则来安排技能：
    // 1. 时空优势度越高的技能，必杀技等级应该越高
    // 2. 一切技能能带来的总伤害应该接近，必杀技等级越高，伤害越高但高不了多少
    // 3. 技能的星级和必杀技等级一致

    // 范围评分标准 
    //攻击框肉体环绕 （60分）
    //大范围 + 包括自身肉体附近 (40分)
    //大范围 + 不包括自身肉体附近(30分)
    //小范围 + 包括自身肉体附近 (20分)
    //小范围 + 不包括自身肉体附近(10分)

    // 范围评分加上时间评分也就是总的时间评分，这个计算里是趋向于认为时间优势比范围优势更大
    public void EvaluateSKill(AnimationClip _clip)
    {
        // 所有攻击类事件的开始帧，包括了所有普通攻击类事件的开始帧和所有魔法攻击类事件的开始帧
        var allAttackFrames = new List<EventNameAndAtFrame>();
        // 所有普通攻击类事件的开始帧
        var allNormalAttackFrames = new List<EventNameAndAtFrame>();
        // 所有魔法攻击类事件的开始帧
        var allMagicAttackFrames = new List<EventNameAndAtFrame>();
        
        // 可迁移点的帧数
        float cancelFlagFrame = -1;
        
        // 最早的攻击帧，包括了普攻和魔法
        var mostQuick = new EventNameAndAtFrame
        {
            name = "",
            startFrame = 999
        };
        
        // 最晚的普通攻击结束帧
        var latestNormalAttackFrameClearEvent = new EventNameAndAtFrame // 最晚的攻击帧结束事件，可能是普工的“收手”事件也可能是最后一个魔法攻击 
        {
            name = "",
            startFrame = -1
        };
        // 最晚的魔法攻击开始帧 （魔法攻击没有结束帧）
        var latestSpecialAttackEvent = new EventNameAndAtFrame // 最晚的魔法攻击事件
        {
            name = "",
            startFrame = -1
        };
        
        foreach (var e in _clip.events)
        {
            var set = new EventNameAndAtFrame()
            {
                name = e.functionName,
                startFrame = e.time
            };
            if (EffectsAttackFrameStartMethodNames.Contains(e.functionName))
            {
                if (e.time < mostQuick.startFrame)
                    mostQuick = set;
                if (e.time > latestSpecialAttackEvent.startFrame)
                    latestSpecialAttackEvent = set;
                allAttackFrames.Add(set);
                allMagicAttackFrames.Add(set);
            }
            if ((AttackFrameStartMethodNames.Contains(e.functionName) && e.intParameter != 0) || e.functionName == "SetAllBodyMarkerManagersIn")
            {
                if (e.time < mostQuick.startFrame)
                    mostQuick = set;
                allAttackFrames.Add(set);
                allNormalAttackFrames.Add(set);
            }
            if ((AttackClearMethodNames.Contains(e.functionName) && e.intParameter == 0) || e.functionName == "ClearMarkerManagers")
            {
                if (e.time > latestNormalAttackFrameClearEvent.startFrame)
                    latestNormalAttackFrameClearEvent = set;
            }
            
            if (e.functionName == "turn_on_flag")
            {
                cancelFlagFrame = e.time;
            }
        }

        if (allNormalAttackFrames.Count > 0)
        {
            Debug.Log("该技能共有"+ allNormalAttackFrames.Count+ "次攻击帧事件" );
            for (int i = 0; i < allNormalAttackFrames.Count; i++)
            {
                //Debug.Log(allNormalAttackFrames[i].name +", start at:" + allNormalAttackFrames[i].startframe);
            }
        }
        if (allMagicAttackFrames.Count > 0)
        {
            Debug.Log("共有"+allMagicAttackFrames.Count+"次特殊攻击帧事件" );
            for (int i = 0; i < allMagicAttackFrames.Count; i++)
            {
                //Debug.Log(allMagicAttackFrames[i].name +", start at:" + allMagicAttackFrames[i].startframe);
            }
        }
        
        if (latestSpecialAttackEvent.startFrame > 0 && latestNormalAttackFrameClearEvent.startFrame > 0)
        {
            Debug.Log("该技能同时拥有普通攻击和魔法攻击，难以进行机械评估，请进行主管判断");
            return;
        }

        float attackingLastFrame = -1;
        if (latestNormalAttackFrameClearEvent.startFrame > 0)
        {
            attackingLastFrame = latestNormalAttackFrameClearEvent.startFrame;
            //Debug.Log("最晚的攻击帧清理事件是"+lastestNormalAttackFrameClearEvent.name +",start at :"+ lastestNormalAttackFrameClearEvent.startframe); 
        }
        else if (latestSpecialAttackEvent.startFrame > 0)
        {
            attackingLastFrame = latestSpecialAttackEvent.startFrame;
            //Debug.Log("最晚的魔法攻击事件是"+lastestSpecialAttackEvent.name +",start at :"+ lastestSpecialAttackEvent.startframe); 
        }
        
        if (cancelFlagFrame >= 0)
        {
            //Debug.Log("该技能可在" + cancelflagFrame + "秒取消，");
        }
        else
        {
            Debug.Log("该技能没有取消帧，无法继续进行分析");
            return;
        }
        
        if (cancelFlagFrame < attackingLastFrame)
        {
            Debug.Log("该技能的取消帧在最后的攻击帧之前，没法继续进行机械分析");
            return;
        }else{
            Debug.Log("该技能最早的攻击事件是："+ mostQuick.name + ", start at:" + mostQuick.startFrame);
            Debug.Log("最终攻击帧距离取消帧" + (cancelFlagFrame-attackingLastFrame));
            Debug.Log("攻击延迟与后摆的总和是："+ (mostQuick.startFrame + (cancelFlagFrame-attackingLastFrame)));
            Debug.Log("本技能的时间评分是："+ (100- 100 * (mostQuick.startFrame + (cancelFlagFrame-attackingLastFrame) * 1/2)));// 加那个1/2是因为技能后摆没有出手速度重要。
        }
    }
}
#endif