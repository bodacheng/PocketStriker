using System;
using System.Collections.Generic;
using System.Linq;
using Skill;

namespace MCombat.Shared.Behaviour
{
    public sealed class SkillSetBuildResult
    {
        public SkillEntity A1, A2, A3;
        public SkillEntity B1, B2, B3;
        public SkillEntity C1, C2, C3;
        public SkillEntity D, M, R;

        public SkillEntity Empty, Victory, Death, Hit, GetUp, KnockOff;

        public readonly List<SkillEntity> H1Entities = new List<SkillEntity>();
        public readonly List<SkillEntity> H2Entities = new List<SkillEntity>();
        public readonly List<SkillEntity> H3Entities = new List<SkillEntity>();
        public readonly List<string> H1Names = new List<string>();
        public readonly List<string> H2Names = new List<string>();
        public readonly List<string> H3Names = new List<string>();
    }

    public static class SkillSetBuildUtility
    {
        public static SkillSetBuildResult BuildNineAndTwo(
            string a1,
            string a2,
            string a3,
            string b1,
            string b2,
            string b3,
            string c1,
            string c2,
            string c3,
            bool canDefend,
            MoveType moveType,
            bool globalHasDefend,
            Func<string, SkillConfig> resolveSkillConfig)
        {
            var result = new SkillSetBuildResult
            {
                A1 = CreateSkillEntity(a1, resolveSkillConfig),
                A2 = CreateSkillEntity(a2, resolveSkillConfig),
                A3 = CreateSkillEntity(a3, resolveSkillConfig),
                B1 = CreateSkillEntity(b1, resolveSkillConfig),
                B2 = CreateSkillEntity(b2, resolveSkillConfig),
                B3 = CreateSkillEntity(b3, resolveSkillConfig),
                C1 = CreateSkillEntity(c1, resolveSkillConfig),
                C2 = CreateSkillEntity(c2, resolveSkillConfig),
                C3 = CreateSkillEntity(c3, resolveSkillConfig),
                D = canDefend ? SkillEntity.GetD_SE() : null,
                M = SkillEntity.GetM_SE(moveType),
                R = CreateRushEntity()
            };

            result.M.CAN_BE_CANCELLED_TO = false;

            AddAttackEntity(result.H1Entities, result.H1Names, result.A1, InputKey.Attack1);
            AddAttackEntity(result.H2Entities, result.H2Names, result.A2, InputKey.Attack1);
            AddAttackEntity(result.H3Entities, result.H3Names, result.A3, InputKey.Attack1);

            AddAttackEntity(result.H1Entities, result.H1Names, result.B1, InputKey.Attack2);
            AddAttackEntity(result.H2Entities, result.H2Names, result.B2, InputKey.Attack2);
            AddAttackEntity(result.H3Entities, result.H3Names, result.B3, InputKey.Attack2);

            AddAttackEntity(result.H1Entities, result.H1Names, result.C1, InputKey.Attack3);
            AddAttackEntity(result.H2Entities, result.H2Names, result.C2, InputKey.Attack3);
            AddAttackEntity(result.H3Entities, result.H3Names, result.C3, InputKey.Attack3);

            AddToAllChains(result, result.R);

            if (result.D != null && globalHasDefend)
            {
                AddToAllChains(result, result.D);
            }

            LinkCasualTransitions(result.H1Entities, result.H2Names);
            LinkCasualTransitions(result.H2Entities, result.H3Names);
            LinkCasualTransitions(result.H3Entities, result.H1Names);

            result.M.CasualTo = result.H1Names.ToArray();
            if (result.D != null)
                result.D.CasualTo = result.H1Names.ToArray();
            if (result.R != null)
                result.R.CasualTo = result.H1Names.ToArray();

            return result;
        }

        public static IDictionary<string, SkillEntity> GenerateBehaviourSets(
            SkillSetBuildResult result,
            bool globalHasDefend,
            Action<string> log = null)
        {
            var seDic = new Dictionary<string, SkillEntity>();
            var stateTransitionSetList = new List<SkillEntity>();
            var startList = result.H1Names.Concat(new[] { "rush" }).ToList();

            result.Empty = new SkillEntity("Empty", BehaviorType.NONE, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, -1);
            result.Victory = new SkillEntity("Victory", BehaviorType.NONE, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, -1);
            result.Death = new SkillEntity("Death", BehaviorType.NONE, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, -1);
            result.Hit = new SkillEntity("Hit", BehaviorType.Hit, new AIAttrs(), startList.ToArray(), null, InputKey.Null, InputKey.Null, -1);
            result.GetUp = new SkillEntity("getUp", BehaviorType.GetUp, new AIAttrs(), startList.ToArray(), null, InputKey.Any, InputKey.Null, -1);
            result.KnockOff = new SkillEntity("KnockOff", BehaviorType.KnockOff, new AIAttrs(), startList.ToArray(), null, InputKey.Null, InputKey.Null, -1);

            if (globalHasDefend && result.D != null)
            {
                result.D.CasualTo = result.H1Names.ToArray();
                stateTransitionSetList.Add(result.D);
            }

            stateTransitionSetList.Add(result.GetUp);
            stateTransitionSetList.Add(result.KnockOff);
            stateTransitionSetList.Add(result.Empty);
            stateTransitionSetList.Add(result.Victory);
            stateTransitionSetList.Add(result.Death);
            stateTransitionSetList.Add(result.Hit);
            stateTransitionSetList.Add(result.M);

            if (result.D != null && globalHasDefend)
            {
                stateTransitionSetList.Add(result.D);
            }
            if (result.R != null)
            {
                stateTransitionSetList.Add(result.R);
            }

            AddIfNotNull(stateTransitionSetList, result.A1);
            AddIfNotNull(stateTransitionSetList, result.A2);
            AddIfNotNull(stateTransitionSetList, result.A3);
            AddIfNotNull(stateTransitionSetList, result.B1);
            AddIfNotNull(stateTransitionSetList, result.B2);
            AddIfNotNull(stateTransitionSetList, result.B3);
            AddIfNotNull(stateTransitionSetList, result.C1);
            AddIfNotNull(stateTransitionSetList, result.C2);
            AddIfNotNull(stateTransitionSetList, result.C3);

            foreach (var skillEntity in stateTransitionSetList)
            {
                if (skillEntity != result.M
                    && skillEntity != result.KnockOff
                    && skillEntity != result.Empty
                    && skillEntity != result.Death
                    && skillEntity != result.Victory)
                {
                    var toOptions = skillEntity.CasualTo.ToList();
                    if (!toOptions.Contains(result.M.REAL_NAME))
                    {
                        toOptions.Add(result.M.REAL_NAME);
                    }

                    skillEntity.CasualTo = toOptions.ToArray();
                }

                if (skillEntity.REAL_NAME != null && !seDic.ContainsKey(skillEntity.REAL_NAME))
                {
                    seDic.Add(skillEntity.REAL_NAME, skillEntity);
                }
                else
                {
                    log?.Invoke(skillEntity.REAL_NAME == null
                        ? "键值为空？？"
                        : "角色自身技能产生键值重复：" + skillEntity.REAL_NAME);
                }
            }

            return seDic;
        }

        public static List<SkillEntity> SkillEntityList(SkillSetBuildResult result)
        {
            var behaviorTransitionSets = new List<SkillEntity>();
            if (result == null)
            {
                return behaviorTransitionSets;
            }

            AddIfNotNull(behaviorTransitionSets, result.A1);
            AddIfNotNull(behaviorTransitionSets, result.A2);
            AddIfNotNull(behaviorTransitionSets, result.A3);
            AddIfNotNull(behaviorTransitionSets, result.B1);
            AddIfNotNull(behaviorTransitionSets, result.B2);
            AddIfNotNull(behaviorTransitionSets, result.B3);
            AddIfNotNull(behaviorTransitionSets, result.C1);
            AddIfNotNull(behaviorTransitionSets, result.C2);
            AddIfNotNull(behaviorTransitionSets, result.C3);
            AddIfNotNull(behaviorTransitionSets, result.D);
            AddIfNotNull(behaviorTransitionSets, result.M);
            AddIfNotNull(behaviorTransitionSets, result.R);
            AddIfNotNull(behaviorTransitionSets, result.Empty);
            AddIfNotNull(behaviorTransitionSets, result.Victory);
            AddIfNotNull(behaviorTransitionSets, result.Death);
            AddIfNotNull(behaviorTransitionSets, result.Hit);
            AddIfNotNull(behaviorTransitionSets, result.GetUp);
            AddIfNotNull(behaviorTransitionSets, result.KnockOff);
            return behaviorTransitionSets;
        }

        public static IDictionary<int, SkillEntity> AttackChain(SkillSetBuildResult result, int attackIndex)
        {
            switch (attackIndex)
            {
                case 1:
                    return Chain(result?.A1, result?.A2, result?.A3);
                case 2:
                    return Chain(result?.B1, result?.B2, result?.B3);
                case 3:
                    return Chain(result?.C1, result?.C2, result?.C3);
                default:
                    return new Dictionary<int, SkillEntity>();
            }
        }

        static SkillEntity CreateSkillEntity(string skillId, Func<string, SkillConfig> resolveSkillConfig)
        {
            var skillConfig = skillId != null ? resolveSkillConfig?.Invoke(skillId) : null;
            if (skillConfig == null)
            {
                return null;
            }

            return new SkillEntity(
                skillConfig.RECORD_ID,
                skillConfig.REAL_NAME,
                skillConfig.STATE_TYPE,
                skillConfig.AIAttrs,
                null,
                null,
                InputKey.Null,
                InputKey.Null,
                skillConfig.SP_LEVEL);
        }

        static SkillEntity CreateRushEntity()
        {
            return new SkillEntity
            {
                REAL_NAME = "rush",
                StateType = BehaviorType.AC,
                AIAttrs = new AIAttrs
                {
                    AI_MIN_DIS = -1,
                    AI_MAX_DIS = -1
                },
                CasualTo = null,
                ForcedTransitions = null,
                EnterInput = InputKey.Acc,
                ExitInput = InputKey.Null,
                SP_LEVEL = -1
            };
        }

        static void AddAttackEntity(List<SkillEntity> entities, List<string> names, SkillEntity entity, InputKey enterInput)
        {
            if (entity == null)
            {
                return;
            }

            entities.Add(entity);
            names.Add(entity.REAL_NAME);
            entity.EnterInput = enterInput;
            entity.ExitInput = InputKey.Null;
        }

        static void AddToAllChains(SkillSetBuildResult result, SkillEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            result.H1Entities.Add(entity);
            result.H2Entities.Add(entity);
            result.H3Entities.Add(entity);
            result.H1Names.Add(entity.REAL_NAME);
            result.H2Names.Add(entity.REAL_NAME);
            result.H3Names.Add(entity.REAL_NAME);
        }

        static void LinkCasualTransitions(List<SkillEntity> entities, List<string> nextNames)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                entities[i].CasualTo = nextNames.ToArray();
            }
        }

        static void AddIfNotNull(List<SkillEntity> list, SkillEntity entity)
        {
            if (entity != null)
            {
                list.Add(entity);
            }
        }

        static IDictionary<int, SkillEntity> Chain(SkillEntity first, SkillEntity second, SkillEntity third)
        {
            return new Dictionary<int, SkillEntity>
            {
                { 1, first },
                { 2, second },
                { 3, third }
            };
        }
    }
}
