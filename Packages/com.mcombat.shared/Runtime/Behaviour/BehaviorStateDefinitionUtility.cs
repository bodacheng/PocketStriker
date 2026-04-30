using System;
using System.Collections.Generic;
using Skill;

namespace MCombat.Shared.Behaviour
{
    public enum BehaviorStateKind
    {
        None,
        Rush,
        GeneralAttack,
        MoveAttack,
        MoveBodyAttack,
        Counter,
        DashBack
    }

    public readonly struct BehaviorStateDefinition
    {
        public readonly BehaviorStateKind Kind;
        public readonly string RealName;
        public readonly BehaviorType StateType;
        public readonly bool NextAttackCanRushFirst;
        public readonly bool AddToSkillTypeKeys;
        public readonly SkillConfig SkillConfig;
        public readonly string ApproachAnimation;
        public readonly float ApproachSpeed;
        public readonly float ApproachDuration;
        public readonly float RotateSpeed;

        public BehaviorStateDefinition(
            BehaviorStateKind kind,
            string realName,
            BehaviorType stateType,
            bool nextAttackCanRushFirst,
            bool addToSkillTypeKeys,
            SkillConfig skillConfig,
            string approachAnimation = null,
            float approachSpeed = 0f,
            float approachDuration = 0f,
            float rotateSpeed = 10f)
        {
            Kind = kind;
            RealName = realName;
            StateType = stateType;
            NextAttackCanRushFirst = nextAttackCanRushFirst;
            AddToSkillTypeKeys = addToSkillTypeKeys;
            SkillConfig = skillConfig;
            ApproachAnimation = approachAnimation;
            ApproachSpeed = approachSpeed;
            ApproachDuration = approachDuration;
            RotateSpeed = rotateSpeed;
        }
    }

    public static class BehaviorStateDefinitionUtility
    {
        public static MoveType ResolveMoveType(string skillId)
        {
            switch (skillId)
            {
                case "slow":
                case "Move_slow":
                    return MoveType.slow;
                case "fast":
                case "Move_fast":
                    return MoveType.fast;
                default:
                    return MoveType.normal;
            }
        }

        public static void AddSkillBehaviors<TBehavior>(
            IDictionary<string, TBehavior> behaviorDic,
            IList<string> skillTypeKeys,
            IDictionary<string, SkillEntity> skillEntities,
            Func<string, SkillConfig> resolveSkillConfig,
            Func<BehaviorStateDefinition, TBehavior> createBehavior)
            where TBehavior : class
        {
            if (behaviorDic == null || skillTypeKeys == null || skillEntities == null || createBehavior == null)
            {
                return;
            }

            foreach (var valuePair in skillEntities)
            {
                var skillEntity = valuePair.Value;
                if (skillEntity == null || behaviorDic.ContainsKey(skillEntity.REAL_NAME))
                {
                    continue;
                }

                var skillConfig = resolveSkillConfig?.Invoke(skillEntity.SkillID);
                if (!TryCreateSkillDefinition(skillEntity, skillConfig, out var definition))
                {
                    continue;
                }

                var behavior = createBehavior(definition);
                if (behavior == null)
                {
                    continue;
                }

                behaviorDic.Add(definition.RealName, behavior);
                if (definition.AddToSkillTypeKeys && !skillTypeKeys.Contains(definition.RealName))
                {
                    skillTypeKeys.Add(definition.RealName);
                }
            }
        }

        public static bool TryCreateSkillDefinition(
            SkillEntity skillEntity,
            SkillConfig skillConfig,
            out BehaviorStateDefinition definition)
        {
            definition = default;
            if (skillEntity == null)
            {
                return false;
            }

            switch (skillEntity.StateType)
            {
                case BehaviorType.AC:
                    definition = new BehaviorStateDefinition(
                        BehaviorStateKind.Rush,
                        skillEntity.REAL_NAME,
                        BehaviorType.AC,
                        true,
                        false,
                        skillConfig);
                    return true;
                case BehaviorType.GI:
                    definition = new BehaviorStateDefinition(
                        BehaviorStateKind.GeneralAttack,
                        skillEntity.REAL_NAME,
                        BehaviorType.GI,
                        false,
                        true,
                        skillConfig);
                    return true;
                case BehaviorType.GM:
                    definition = new BehaviorStateDefinition(
                        BehaviorStateKind.MoveAttack,
                        skillEntity.REAL_NAME,
                        BehaviorType.GM,
                        false,
                        true,
                        skillConfig);
                    return true;
                case BehaviorType.GMB:
                    definition = new BehaviorStateDefinition(
                        BehaviorStateKind.MoveBodyAttack,
                        skillEntity.REAL_NAME,
                        BehaviorType.GMB,
                        false,
                        true,
                        skillConfig);
                    return true;
                case BehaviorType.GR:
                    definition = new BehaviorStateDefinition(
                        BehaviorStateKind.GeneralAttack,
                        skillEntity.REAL_NAME,
                        BehaviorType.GR,
                        false,
                        true,
                        skillConfig,
                        "dash",
                        40f,
                        1.4f,
                        10f);
                    return true;
                case BehaviorType.CT:
                    definition = new BehaviorStateDefinition(
                        BehaviorStateKind.Counter,
                        skillEntity.REAL_NAME,
                        BehaviorType.CT,
                        false,
                        true,
                        skillConfig);
                    return true;
                case BehaviorType.RB:
                    definition = new BehaviorStateDefinition(
                        BehaviorStateKind.DashBack,
                        skillEntity.REAL_NAME,
                        BehaviorType.RB,
                        false,
                        true,
                        skillConfig);
                    return true;
                default:
                    return false;
            }
        }
    }
}
