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
            BehaviorDic = new Dictionary<string, Behavior>
            {
                { "Empty", empty_State }
            };
            var victory = new Idle_State("victory");
            var death = new Death_State();
            BehaviorDic.Add("Victory", victory);
            BehaviorDic.Add("Death", death);

            if (FightGlobalSetting.HasDefend)
            {
                var defend = new Defend_State("block", "block_break")
                {
                    StateType = BehaviorType.Def,
                    nextAttackCanRushFirst = false
                };
                BehaviorDic.Add("Defend", defend);
            }

            var move = new Move_State(BehaviorStateDefinitionUtility.ResolveMoveType(moveState.SkillID))
            {
                StateType = BehaviorType.MV,
                nextAttackCanRushFirst = false
            };
            BehaviorDic.Add("Move", move);

            var hit = new Hurt_State()
            {
                nextAttackCanRushFirst = false,
                StateType = BehaviorType.Hit
            };

            var knock_off = new Knock_Off_State()
            {
                StateType = BehaviorType.KnockOff,
                nextAttackCanRushFirst = true
            };
            var getUp = new GetUp("getup")
            {
                StateType = BehaviorType.GetUp
            };
            BehaviorDic.Add("Hit", hit);
            BehaviorDic.Add("KnockOff", knock_off);
            BehaviorDic.Add("getUp", getUp);

            SkillTypeKeys = new List<string>();
            BehaviorStateDefinitionUtility.AddSkillBehaviors(
                BehaviorDic,
                SkillTypeKeys,
                toFormAttackStateList,
                SkillConfigTable.GetSkillConfigByRecordId,
                CreateSkillBehavior);
        }

        static Behavior CreateSkillBehavior(BehaviorStateDefinition definition)
        {
            switch (definition.Kind)
            {
                case BehaviorStateKind.Rush:
                    return new GMoveEscapeState(definition.RealName)
                    {
                        nextAttackCanRushFirst = definition.NextAttackCanRushFirst,
                        StateType = definition.StateType
                    };
                case BehaviorStateKind.GeneralAttack:
                    return new G_Attack_State(
                        definition.ApproachAnimation,
                        definition.ApproachSpeed,
                        definition.ApproachDuration,
                        definition.RotateSpeed,
                        definition.RealName)
                    {
                        StateType = definition.StateType,
                        nextAttackCanRushFirst = definition.NextAttackCanRushFirst,
                        SkillConfig = definition.SkillConfig
                    };
                case BehaviorStateKind.MoveAttack:
                    return new G_M_Attack_State(definition.RealName)
                    {
                        StateType = definition.StateType,
                        nextAttackCanRushFirst = definition.NextAttackCanRushFirst,
                        SkillConfig = definition.SkillConfig
                    };
                case BehaviorStateKind.MoveBodyAttack:
                    return new G_M_B_State(definition.RealName)
                    {
                        StateType = definition.StateType,
                        nextAttackCanRushFirst = definition.NextAttackCanRushFirst,
                        SkillConfig = definition.SkillConfig
                    };
                case BehaviorStateKind.Counter:
                    return new Counter_State(definition.RealName)
                    {
                        StateType = definition.StateType,
                        nextAttackCanRushFirst = definition.NextAttackCanRushFirst,
                        SkillConfig = definition.SkillConfig
                    };
                case BehaviorStateKind.DashBack:
                    return new Dash_Back_State(definition.RealName)
                    {
                        StateType = definition.StateType,
                        nextAttackCanRushFirst = definition.NextAttackCanRushFirst,
                        SkillConfig = definition.SkillConfig
                    };
                default:
                    return null;
            }
        }
    }
}
