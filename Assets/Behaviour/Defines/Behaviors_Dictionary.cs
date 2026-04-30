using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using Skill;

namespace Soul
{
    public class BehaviorsIncubator
    {
        public readonly IDictionary<string, Behavior> BehaviorDic;
        // 技能动画列表（不包括基础动画）
        public readonly List<string> SkillTypeKeys;

        public BehaviorsIncubator(Empty_State empty_State, SkillEntity moveState, IDictionary<string, SkillEntity> toFormAttackStateList)
        {
            var result = BehaviorStateDefinitionUtility.BuildBehaviorDictionary(
                empty_State,
                moveState,
                toFormAttackStateList,
                FightGlobalSetting.HasDefend,
                SkillConfigTable.GetSkillConfigByRecordId,
                CreateFactories());
            BehaviorDic = result.BehaviorDic;
            SkillTypeKeys = result.SkillTypeKeys;
        }

        static BehaviorStateFactorySet<Behavior> CreateFactories()
        {
            return new BehaviorStateFactorySet<Behavior>
            {
                CreateVictory = definition => new Idle_State(definition.PrimaryAnimation),
                CreateDeath = definition => new Death_State(),
                CreateDefend = definition => new Defend_State(definition.PrimaryAnimation, definition.SecondaryAnimation),
                CreateMove = definition => new Move_State(definition.MoveType),
                CreateHit = definition => new Hurt_State(),
                CreateKnockOff = definition => new Knock_Off_State(),
                CreateGetUp = definition => new GetUp(definition.PrimaryAnimation),
                CreateRush = definition => new GMoveEscapeState(definition.RealName),
                CreateGeneralAttack = definition => new G_Attack_State(
                        definition.ApproachAnimation,
                        definition.ApproachSpeed,
                        definition.ApproachDuration,
                        definition.RotateSpeed,
                        definition.RealName),
                CreateMoveAttack = definition => new G_M_Attack_State(definition.RealName),
                CreateMoveBodyAttack = definition => new G_M_B_State(definition.RealName),
                CreateCounter = definition => new Counter_State(definition.RealName),
                CreateDashBack = definition => new Dash_Back_State(definition.RealName),
                Configure = ApplyDefinition
            };
        }

        static void ApplyDefinition(Behavior behavior, BehaviorStateDefinition definition)
        {
            if (behavior == null)
            {
                return;
            }

            behavior.StateType = definition.StateType;
            behavior.nextAttackCanRushFirst = definition.NextAttackCanRushFirst;
            behavior.SkillConfig = definition.SkillConfig;
        }
    }
}
