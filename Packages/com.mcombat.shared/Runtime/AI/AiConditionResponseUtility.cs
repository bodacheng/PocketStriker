using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using Skill;

namespace MCombat.Shared.AI
{
    public sealed class AiConditionResponseProfile
    {
        public int MovePriority = 5;
        public bool IncludeRushBackWithApproach = true;
        public int ApproachDangerousVeryClosePriority = 4;
        public bool AddApproachEnemyClose = true;
        public int ApproachEnemyClosePriority = 5;
        public bool IncludeGangbangMeleeAttack = true;
    }

    public static class AiConditionResponseUtility
    {
        public static readonly AiConditionResponseProfile DefaultProfile = new AiConditionResponseProfile();

        public static void AddTriggerCondition(
            BehaviorType stateType,
            string stateKey,
            IDictionary<string, List<string>> conditionAndRespond,
            global::MultiDic<string, string, int> conditionAndRespondPriority,
            IDictionary<string, string> strategicExitCondition,
            AiConditionResponseProfile profile = null)
        {
            profile ??= DefaultProfile;

            switch (stateType)
            {
                case BehaviorType.MV:
                    Register(conditionAndRespond, conditionAndRespondPriority, "SpareOption", stateKey, profile.MovePriority);
                    break;

                case BehaviorType.RB:
                    if (!profile.IncludeRushBackWithApproach)
                    {
                        break;
                    }

                    AddApproachConditions(stateKey, conditionAndRespond, conditionAndRespondPriority, strategicExitCondition, profile);
                    break;

                case BehaviorType.AC:
                    AddApproachConditions(stateKey, conditionAndRespond, conditionAndRespondPriority, strategicExitCondition, profile);
                    break;

                case BehaviorType.CT:
                    Register(conditionAndRespond, conditionAndRespondPriority, "CT", stateKey, 1);
                    Register(conditionAndRespond, conditionAndRespondPriority, "EnemyClose", stateKey, 3);
                    strategicExitCondition.Add(stateKey, null);
                    break;

                case BehaviorType.Def:
                    Register(conditionAndRespond, conditionAndRespondPriority, "DangerousVeryClose", stateKey, 2);
                    strategicExitCondition.Add(stateKey, "TimeToRespond");
                    break;

                case BehaviorType.GR:
                case BehaviorType.GI:
                    AddGroundAttackConditions(stateKey, conditionAndRespond, conditionAndRespondPriority, strategicExitCondition);
                    break;

                case BehaviorType.GMB:
                    if (!profile.IncludeGangbangMeleeAttack)
                    {
                        break;
                    }

                    AddGroundAttackConditions(stateKey, conditionAndRespond, conditionAndRespondPriority, strategicExitCondition);
                    break;

                case BehaviorType.GM:
                    AddGroundAttackConditions(stateKey, conditionAndRespond, conditionAndRespondPriority, strategicExitCondition);
                    break;
            }
        }

        public static bool ShouldWriteBehaviorDecisionLog(BehaviorType stateType)
        {
            return BehaviorTypeUtility.ShouldWriteBehaviorDecisionLog(stateType);
        }

        static void AddApproachConditions(
            string stateKey,
            IDictionary<string, List<string>> conditionAndRespond,
            global::MultiDic<string, string, int> conditionAndRespondPriority,
            IDictionary<string, string> strategicExitCondition,
            AiConditionResponseProfile profile)
        {
            Register(conditionAndRespond, conditionAndRespondPriority, "DangerousVeryClose", stateKey, profile.ApproachDangerousVeryClosePriority);
            if (profile.AddApproachEnemyClose)
            {
                Register(conditionAndRespond, conditionAndRespondPriority, "EnemyClose", stateKey, profile.ApproachEnemyClosePriority);
            }

            strategicExitCondition.Add(stateKey, null);
        }

        static void AddGroundAttackConditions(
            string stateKey,
            IDictionary<string, List<string>> conditionAndRespond,
            global::MultiDic<string, string, int> conditionAndRespondPriority,
            IDictionary<string, string> strategicExitCondition)
        {
            Register(conditionAndRespond, conditionAndRespondPriority, "TimeToAttack", stateKey, 2);
            Register(conditionAndRespond, conditionAndRespondPriority, "TimeToAttack_Reluctant", stateKey, 3);
            strategicExitCondition.Add(stateKey, null);
        }

        static void Register(
            IDictionary<string, List<string>> conditionAndRespond,
            global::MultiDic<string, string, int> conditionAndRespondPriority,
            string condition,
            string stateKey,
            int priority)
        {
            if (conditionAndRespond.ContainsKey(condition))
            {
                conditionAndRespond[condition].Add(stateKey);
            }
            else
            {
                conditionAndRespond.Add(condition, new List<string> { stateKey });
            }

            conditionAndRespondPriority.Set(condition, stateKey, priority);
        }
    }
}
