using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using MCombat.Shared.AI;
using Skill;

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        public readonly IDictionary<string, List<string>> ConditionAndRespond = new Dictionary<string, List<string>>();
        public readonly MultiDic<string, string, int> ConditionAndRespondPriority = new MultiDic<string, string, int>();// 注意，这个字典是value越小代表有限度越高
        public readonly IDictionary<string, string> BehaviourAndStrategicExitCondition = new Dictionary<string, string>();
        public List<string> AllConditionCodes;

        static readonly AiConditionResponseProfile PocketAiConditionProfile = new AiConditionResponseProfile
        {
            MovePriority = 10,
            IncludeRushBackWithApproach = false,
            ApproachDangerousVeryClosePriority = 3,
            AddApproachEnemyClose = false,
            IncludeGangbangMeleeAttack = false
        };

        void AddAITriggerConditionToBehavior(SkillEntity behaviorDefine)
        {
            AiConditionResponseUtility.AddTriggerCondition(
                behaviorDefine.StateType,
                behaviorDefine.REAL_NAME,
                ConditionAndRespond,
                ConditionAndRespondPriority,
                BehaviourAndStrategicExitCondition,
                PocketAiConditionProfile);
        }

        public void FormFightingSetsByNineAndTwo(SkillSet nineAndTwo)
        {
            nineAndTwo.SortNineAndTwo();
            //这上下两个函数之间存在一个chuanEndCasualT0的问题，从而必须一前一后紧密连接，下次review时候可以看看代码能不能整更利索一些。
            SkillEntityDic = nineAndTwo.GenerateBehaviourSets();
            skillEntityList = nineAndTwo.SkillEntityList();
            _statesIncubator = new BehaviorsIncubator(_emptyState, SkillEntityDic);
            var behaviorDic = _statesIncubator.BehaviorDic; // 理解整个系统的关键
            BehaviourDic.Clear();
            ConditionAndRespondPriority.Clear();
            BehaviourAndStrategicExitCondition.Clear();
            foreach (var s in behaviorDic)
            {
                if (SkillEntityDic.ContainsKey(s.Key))
                {
                    s.Value.StateKey = SkillEntityDic[s.Key].REAL_NAME;
                    s.Value.spLevel = SkillEntityDic[s.Key].SP_LEVEL;
                    s.Value.triggerAttackRangeMin = SkillEntityDic[s.Key].AIAttrs.AI_MIN_DIS;
                    s.Value.triggerAttackRangeMax = SkillEntityDic[s.Key].AIAttrs.AI_MAX_DIS;
                    s.Value.TriggerAttackHeight = SkillEntityDic[s.Key].AIAttrs.height;
                    AddAITriggerConditionToBehavior(SkillEntityDic[s.Key]);
                    BehaviourDic.Add(new KeyValuePair<string, Behavior>(s.Key, s.Value));

                }
            }
            
            fixedSkillSequence.Clear();
            for (var index = 0; index < skillEntityList.Count; index++)
            {
                var skill = skillEntityList[index];
                if (skill.SP_LEVEL > 0)
                {
                    fixedSkillSequence.Add(skill);
                }
            }
            
            AllConditionCodes = ConditionAndRespond.Keys.ToList();
            _commandWaitingState = BehaviourDic[nineAndTwo.GetM_STS().REAL_NAME];
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
