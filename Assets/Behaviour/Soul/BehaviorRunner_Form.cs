using UnityEngine;
using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using Skill;

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        public readonly IDictionary<string, List<string>> ConditionAndRespond = new Dictionary<string, List<string>>();
        public readonly MultiDic<string, string, int> ConditionAndRespondPriority = new MultiDic<string, string, int>();// 注意，这个字典是value越小代表有限度越高
        public readonly IDictionary<string, string> BehaviourAndStrategicExitCondition = new Dictionary<string, string>();
        public List<string> AllConditionCodes;

        public void FormFightingSetsByNineAndTwo(SkillSet nineAndTwo)
        {
            nineAndTwo.SortNineAndTwo();
            //这上下两个函数之间存在一个chuanEndCasualT0的问题，从而必须一前一后紧密连接，下次review时候可以看看代码能不能整更利索一些。
            SkillEntityDic = nineAndTwo.GenerateBehaviourSets();
            skillEntityList = nineAndTwo.SkillEntityList();
            _statesIncubator = new BehaviorsIncubator(_emptyState, nineAndTwo.MSkillEntity, SkillEntityDic);
            var behaviorDic = _statesIncubator.BehaviorDic; // 理解整个系统的关键
            AllConditionCodes = BehaviorRunnerBuildUtility.BindBehaviors(
                behaviorDic,
                SkillEntityDic,
                skillEntityList,
                BehaviourDic,
                fixedSkillSequence,
                ConditionAndRespond,
                ConditionAndRespondPriority,
                BehaviourAndStrategicExitCondition,
                ApplySkillEntityToBehavior);
            _commandWaitingState = BehaviourDic[nineAndTwo.MSkillEntity.REAL_NAME];
        }

        static void ApplySkillEntityToBehavior(Behavior behavior, SkillEntity skillEntity)
        {
            behavior.StateKey = skillEntity.REAL_NAME;
            behavior.spLevel = skillEntity.SP_LEVEL;
            behavior.triggerAttackRangeMin = skillEntity.AIAttrs.AI_MIN_DIS;
            behavior.triggerAttackRangeMax = skillEntity.AIAttrs.AI_MAX_DIS;
            behavior.TriggerAttackHeight = skillEntity.AIAttrs.height;
        }
        
        public void SetAt(float level)
        {
            foreach (var kv in BehaviourDic)
            {
                if (kv.Value.SkillConfig == null)
                    continue;
                kv.Value.Attack = FightGlobalSetting.ATCal(kv.Value.SkillConfig.ATTACK_WEIGHT, level);
            }
        }
    }
}
