using System;
using System.Collections.Generic;
using Skill;

namespace MCombat.Shared.Behaviour
{
    public enum BehaviorStateKind
    {
        None,
        Empty,
        Victory,
        Death,
        Defend,
        Move,
        Hit,
        KnockOff,
        GetUp,
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
        public readonly string PrimaryAnimation;
        public readonly string SecondaryAnimation;
        public readonly MoveType MoveType;
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
            string primaryAnimation = null,
            string secondaryAnimation = null,
            MoveType moveType = MoveType.normal,
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
            PrimaryAnimation = primaryAnimation;
            SecondaryAnimation = secondaryAnimation;
            MoveType = moveType;
            ApproachAnimation = approachAnimation;
            ApproachSpeed = approachSpeed;
            ApproachDuration = approachDuration;
            RotateSpeed = rotateSpeed;
        }
    }

    public sealed class BehaviorStateFactorySet<TBehavior>
        where TBehavior : class
    {
        public Func<BehaviorStateDefinition, TBehavior> CreateVictory { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateDeath { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateDefend { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateMove { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateHit { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateKnockOff { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateGetUp { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateRush { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateGeneralAttack { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateMoveAttack { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateMoveBodyAttack { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateCounter { get; set; }
        public Func<BehaviorStateDefinition, TBehavior> CreateDashBack { get; set; }
        public Action<TBehavior, BehaviorStateDefinition> Configure { get; set; }
    }

    public readonly struct BehaviorDictionaryBuildResult<TBehavior>
        where TBehavior : class
    {
        public readonly IDictionary<string, TBehavior> BehaviorDic;
        public readonly List<string> SkillTypeKeys;

        public BehaviorDictionaryBuildResult(IDictionary<string, TBehavior> behaviorDic, List<string> skillTypeKeys)
        {
            BehaviorDic = behaviorDic;
            SkillTypeKeys = skillTypeKeys;
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

        public static List<string> CreateEditorBehaviorOptions(bool hasDefend, bool includeRush)
        {
            var options = new List<string>
            {
                "Empty",
                "Move",
                "Victory",
                "Death"
            };

            if (includeRush)
            {
                options.Add("rush");
            }

            options.Add("Hit");
            options.Add("KnockOff");
            options.Add("getUp");

            if (hasDefend)
            {
                options.Add("Defend");
            }

            return options;
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

        public static BehaviorDictionaryBuildResult<TBehavior> BuildBehaviorDictionary<TBehavior>(
            TBehavior emptyState,
            SkillEntity moveState,
            IDictionary<string, SkillEntity> skillEntities,
            bool hasDefend,
            Func<string, SkillConfig> resolveSkillConfig,
            BehaviorStateFactorySet<TBehavior> factories)
            where TBehavior : class
        {
            var behaviorDic = new Dictionary<string, TBehavior>();
            if (emptyState != null)
            {
                behaviorDic.Add("Empty", emptyState);
            }

            AddCoreBehavior(behaviorDic, CreateVictoryDefinition(), factories);
            AddCoreBehavior(behaviorDic, CreateDeathDefinition(), factories);
            if (hasDefend)
            {
                AddCoreBehavior(behaviorDic, CreateDefendDefinition(), factories);
            }

            AddCoreBehavior(behaviorDic, CreateMoveDefinition(moveState), factories);
            AddCoreBehavior(behaviorDic, CreateHitDefinition(), factories);
            AddCoreBehavior(behaviorDic, CreateKnockOffDefinition(), factories);
            AddCoreBehavior(behaviorDic, CreateGetUpDefinition(), factories);

            var skillTypeKeys = new List<string>();
            AddSkillBehaviors(
                behaviorDic,
                skillTypeKeys,
                skillEntities,
                resolveSkillConfig,
                definition => CreateBehavior(definition, factories));

            return new BehaviorDictionaryBuildResult<TBehavior>(behaviorDic, skillTypeKeys);
        }

        public static TBehavior CreateBehavior<TBehavior>(
            BehaviorStateDefinition definition,
            BehaviorStateFactorySet<TBehavior> factories)
            where TBehavior : class
        {
            if (factories == null)
            {
                return null;
            }

            TBehavior behavior;
            switch (definition.Kind)
            {
                case BehaviorStateKind.Victory:
                    behavior = factories.CreateVictory?.Invoke(definition);
                    break;
                case BehaviorStateKind.Death:
                    behavior = factories.CreateDeath?.Invoke(definition);
                    break;
                case BehaviorStateKind.Defend:
                    behavior = factories.CreateDefend?.Invoke(definition);
                    break;
                case BehaviorStateKind.Move:
                    behavior = factories.CreateMove?.Invoke(definition);
                    break;
                case BehaviorStateKind.Hit:
                    behavior = factories.CreateHit?.Invoke(definition);
                    break;
                case BehaviorStateKind.KnockOff:
                    behavior = factories.CreateKnockOff?.Invoke(definition);
                    break;
                case BehaviorStateKind.GetUp:
                    behavior = factories.CreateGetUp?.Invoke(definition);
                    break;
                case BehaviorStateKind.Rush:
                    behavior = factories.CreateRush?.Invoke(definition);
                    break;
                case BehaviorStateKind.GeneralAttack:
                    behavior = factories.CreateGeneralAttack?.Invoke(definition);
                    break;
                case BehaviorStateKind.MoveAttack:
                    behavior = factories.CreateMoveAttack?.Invoke(definition);
                    break;
                case BehaviorStateKind.MoveBodyAttack:
                    behavior = factories.CreateMoveBodyAttack?.Invoke(definition);
                    break;
                case BehaviorStateKind.Counter:
                    behavior = factories.CreateCounter?.Invoke(definition);
                    break;
                case BehaviorStateKind.DashBack:
                    behavior = factories.CreateDashBack?.Invoke(definition);
                    break;
                default:
                    return null;
            }

            factories.Configure?.Invoke(behavior, definition);
            return behavior;
        }

        static void AddCoreBehavior<TBehavior>(
            IDictionary<string, TBehavior> behaviorDic,
            BehaviorStateDefinition definition,
            BehaviorStateFactorySet<TBehavior> factories)
            where TBehavior : class
        {
            if (behaviorDic == null || behaviorDic.ContainsKey(definition.RealName))
            {
                return;
            }

            var behavior = CreateBehavior(definition, factories);
            if (behavior != null)
            {
                behaviorDic.Add(definition.RealName, behavior);
            }
        }

        static BehaviorStateDefinition CreateVictoryDefinition()
        {
            return new BehaviorStateDefinition(
                BehaviorStateKind.Victory,
                "Victory",
                BehaviorType.NONE,
                false,
                false,
                null,
                "victory");
        }

        static BehaviorStateDefinition CreateDeathDefinition()
        {
            return new BehaviorStateDefinition(
                BehaviorStateKind.Death,
                "Death",
                BehaviorType.KnockOff,
                false,
                false,
                null);
        }

        static BehaviorStateDefinition CreateDefendDefinition()
        {
            return new BehaviorStateDefinition(
                BehaviorStateKind.Defend,
                "Defend",
                BehaviorType.Def,
                false,
                false,
                null,
                "block",
                "block_break");
        }

        static BehaviorStateDefinition CreateMoveDefinition(SkillEntity moveState)
        {
            var skillId = moveState != null ? moveState.SkillID : null;
            return new BehaviorStateDefinition(
                BehaviorStateKind.Move,
                "Move",
                BehaviorType.MV,
                false,
                false,
                null,
                moveType: ResolveMoveType(skillId));
        }

        static BehaviorStateDefinition CreateHitDefinition()
        {
            return new BehaviorStateDefinition(
                BehaviorStateKind.Hit,
                "Hit",
                BehaviorType.Hit,
                false,
                false,
                null);
        }

        static BehaviorStateDefinition CreateKnockOffDefinition()
        {
            return new BehaviorStateDefinition(
                BehaviorStateKind.KnockOff,
                "KnockOff",
                BehaviorType.KnockOff,
                true,
                false,
                null);
        }

        static BehaviorStateDefinition CreateGetUpDefinition()
        {
            return new BehaviorStateDefinition(
                BehaviorStateKind.GetUp,
                "getUp",
                BehaviorType.GetUp,
                false,
                false,
                null,
                "getup");
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
                        approachAnimation: "dash",
                        approachSpeed: 40f,
                        approachDuration: 1.4f,
                        rotateSpeed: 10f);
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
