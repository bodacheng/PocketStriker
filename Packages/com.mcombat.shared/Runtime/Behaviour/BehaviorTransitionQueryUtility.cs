using System.Collections.Generic;
using Skill;

namespace MCombat.Shared.Behaviour
{
    public readonly struct BehaviorRangeInfo
    {
        public readonly BehaviorType StateType;
        public readonly float Min;
        public readonly float Max;

        public BehaviorRangeInfo(BehaviorType stateType, float min, float max)
        {
            StateType = stateType;
            Min = min;
            Max = max;
        }
    }

    public delegate bool TryResolveSkillEntity(string key, out SkillEntity skillEntity);
    public delegate bool TryResolveBehaviorRange(string key, out BehaviorRangeInfo rangeInfo);

    public static class BehaviorTransitionQueryUtility
    {
        public static List<SkillEntity> GetAvailableNextSkills(
            SkillEntity currentSkillEntity,
            TryResolveSkillEntity tryResolveSkillEntity,
            System.Func<string, bool> canEnter)
        {
            var result = new List<SkillEntity>();
            if (currentSkillEntity?.CasualTo == null || tryResolveSkillEntity == null || canEnter == null)
            {
                return result;
            }

            foreach (var key in currentSkillEntity.CasualTo)
            {
                if (!canEnter(key))
                {
                    continue;
                }

                if (tryResolveSkillEntity(key, out var skillEntity) && skillEntity != null)
                {
                    result.Add(skillEntity);
                }
            }

            return result;
        }

        public static (float min, float max) CalculateAdviceDistance(
            SkillEntity currentSkillEntity,
            TryResolveBehaviorRange tryResolveRange)
        {
            float min = 9999f;
            float max = 0f;
            if (currentSkillEntity?.CasualTo == null || tryResolveRange == null)
            {
                return (min, max);
            }

            for (var i = 0; i < currentSkillEntity.CasualTo.Length; i++)
            {
                if (!tryResolveRange(currentSkillEntity.CasualTo[i], out var range))
                {
                    continue;
                }

                if (!IsAdviceAttackState(range.StateType))
                {
                    continue;
                }

                if (min > range.Min)
                    min = range.Min;
                if (max < range.Max)
                    max = range.Max;
            }

            return (min, max);
        }

        public static bool IsAdviceAttackState(BehaviorType stateType)
        {
            return stateType == BehaviorType.CT
                   || stateType == BehaviorType.GM
                   || stateType == BehaviorType.GMB
                   || stateType == BehaviorType.GI
                   || stateType == BehaviorType.GR;
        }
    }
}
