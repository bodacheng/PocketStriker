using Skill;

namespace MCombat.Shared.Behaviour
{
    public static class BehaviorTypeUtility
    {
        static readonly BehaviorType[] StandardSkillStateOptions =
        {
            BehaviorType.GR,
            BehaviorType.GI,
            BehaviorType.GM,
            BehaviorType.GMB,
            BehaviorType.CT,
            BehaviorType.RB
        };

        public static BehaviorType[] CreateSkillStateOptions(bool includeNone)
        {
            var extra = includeNone ? 1 : 0;
            var result = new BehaviorType[StandardSkillStateOptions.Length + extra];
            for (var i = 0; i < StandardSkillStateOptions.Length; i++)
            {
                result[i] = StandardSkillStateOptions[i];
            }

            if (includeNone)
            {
                result[result.Length - 1] = BehaviorType.NONE;
            }

            return result;
        }

        public static bool IsTunableSkillState(BehaviorType stateType)
        {
            switch (stateType)
            {
                case BehaviorType.GI:
                case BehaviorType.GM:
                case BehaviorType.GMB:
                case BehaviorType.GR:
                case BehaviorType.CT:
                case BehaviorType.RB:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsCloseRangeSkillState(BehaviorType stateType)
        {
            return stateType == BehaviorType.GI
                   || stateType == BehaviorType.GR
                   || stateType == BehaviorType.CT;
        }

        public static bool IsRangedSkillState(BehaviorType stateType)
        {
            return stateType == BehaviorType.GM
                   || stateType == BehaviorType.GMB
                   || stateType == BehaviorType.RB;
        }

        public static bool IsAdviceAttackState(BehaviorType stateType)
        {
            return stateType == BehaviorType.CT
                   || stateType == BehaviorType.GM
                   || stateType == BehaviorType.GMB
                   || stateType == BehaviorType.GI
                   || stateType == BehaviorType.GR;
        }

        public static bool IsAttackIconState(BehaviorType stateType)
        {
            return stateType == BehaviorType.GI
                   || stateType == BehaviorType.GM
                   || stateType == BehaviorType.GR
                   || stateType == BehaviorType.GMB;
        }

        public static bool IsDefenceIconState(BehaviorType stateType)
        {
            return stateType == BehaviorType.CT
                   || stateType == BehaviorType.Def;
        }

        public static bool ShouldWriteBehaviorDecisionLog(BehaviorType stateType)
        {
            return stateType == BehaviorType.AC
                   || stateType == BehaviorType.CT
                   || stateType == BehaviorType.Def
                   || stateType == BehaviorType.GI
                   || stateType == BehaviorType.GM
                   || stateType == BehaviorType.GMB
                   || stateType == BehaviorType.GR;
        }
    }
}
