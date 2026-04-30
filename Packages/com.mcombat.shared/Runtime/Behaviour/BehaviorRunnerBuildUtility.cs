using System;
using System.Collections.Generic;
using System.Linq;
using MCombat.Shared.AI;
using Skill;

namespace MCombat.Shared.Behaviour
{
    public static class BehaviorRunnerBuildUtility
    {
        public static List<string> BindBehaviors<TBehavior>(
            IDictionary<string, TBehavior> sourceBehaviors,
            IDictionary<string, SkillEntity> skillEntityDic,
            IEnumerable<SkillEntity> skillEntityList,
            IDictionary<string, TBehavior> targetBehaviors,
            IList<SkillEntity> fixedSkillSequence,
            IDictionary<string, List<string>> conditionAndRespond,
            MultiDic<string, string, int> conditionAndRespondPriority,
            IDictionary<string, string> behaviourAndStrategicExitCondition,
            Action<TBehavior, SkillEntity> applySkillEntity)
            where TBehavior : class
        {
            targetBehaviors.Clear();
            conditionAndRespondPriority.Clear();
            behaviourAndStrategicExitCondition.Clear();

            foreach (var source in sourceBehaviors)
            {
                if (!skillEntityDic.TryGetValue(source.Key, out var skillEntity))
                {
                    continue;
                }

                applySkillEntity?.Invoke(source.Value, skillEntity);
                AiConditionResponseUtility.AddTriggerCondition(
                    skillEntity.StateType,
                    skillEntity.REAL_NAME,
                    conditionAndRespond,
                    conditionAndRespondPriority,
                    behaviourAndStrategicExitCondition);
                targetBehaviors.Add(source.Key, source.Value);
            }

            fixedSkillSequence.Clear();
            foreach (var skill in skillEntityList)
            {
                if (skill.SP_LEVEL > 0)
                {
                    fixedSkillSequence.Add(skill);
                }
            }

            return conditionAndRespond.Keys.ToList();
        }
    }
}
