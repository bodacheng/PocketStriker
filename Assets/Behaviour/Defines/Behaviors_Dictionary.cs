using System.Collections.Generic;
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

            MoveType moveType;
            switch (moveState.SkillID)
            {
                case "slow":
                    moveType = MoveType.slow;
                    break;
                case "fast":
                    moveType = MoveType.fast;
                    break;
                default:
                    moveType = MoveType.normal;
                    break;
            }

            var move = new Move_State(moveType)
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
            foreach (KeyValuePair<string, SkillEntity> valuePair in toFormAttackStateList)
            {
                var _set = valuePair.Value;
                if (_set == null)
                    continue;

                SkillConfig skillConfig = SkillConfigTable.GetSkillConfigByRecordId(_set.SkillID);

                if (!BehaviorDic.Keys.Contains(_set.REAL_NAME))
                {
                    switch (_set.StateType)
                    {
                        case BehaviorType.AC:
                            GMoveEscapeState rush = new GMoveEscapeState(_set.REAL_NAME)
                            {
                                nextAttackCanRushFirst = true,
                                StateType = BehaviorType.AC
                            };
                            BehaviorDic.Add("rush", rush);
                            break;
                        case BehaviorType.GI:
                            G_Attack_State _GI_Attack = new G_Attack_State(null, 0f, 0f, 10f, _set.REAL_NAME)
                            {
                                StateType = BehaviorType.GI,
                                nextAttackCanRushFirst = false,
                                SkillConfig = skillConfig
                            };
                            BehaviorDic.Add(_set.REAL_NAME, _GI_Attack);
                            if (!SkillTypeKeys.Contains(_set.REAL_NAME)) SkillTypeKeys.Add(_set.REAL_NAME);
                            break;
                        case BehaviorType.GM:
                            G_M_Attack_State _GM_Attack = new G_M_Attack_State(_set.REAL_NAME)
                            {
                                StateType = BehaviorType.GM,
                                nextAttackCanRushFirst = false,
                                SkillConfig = skillConfig
                            };
                            BehaviorDic.Add(_set.REAL_NAME, _GM_Attack);
                            if (!SkillTypeKeys.Contains(_set.REAL_NAME)) SkillTypeKeys.Add(_set.REAL_NAME);
                            break;
                        case BehaviorType.GMB:
                            G_M_B_State _GMB_Attack = new G_M_B_State(_set.REAL_NAME)
                            {
                                StateType = BehaviorType.GMB,
                                nextAttackCanRushFirst = false,
                                SkillConfig = skillConfig
                            };
                            BehaviorDic.Add(_set.REAL_NAME, _GMB_Attack);
                            if (!SkillTypeKeys.Contains(_set.REAL_NAME)) SkillTypeKeys.Add(_set.REAL_NAME);
                            break;
                        case BehaviorType.GR:
                            G_Attack_State _GR_Attack = new G_Attack_State("dash", 40f, 1.4f, 10f, _set.REAL_NAME)
                            {
                                StateType = BehaviorType.GR,
                                nextAttackCanRushFirst = false,
                                SkillConfig = skillConfig
                            };
                            BehaviorDic.Add(_set.REAL_NAME, _GR_Attack);
                            if (!SkillTypeKeys.Contains(_set.REAL_NAME)) SkillTypeKeys.Add(_set.REAL_NAME);
                            break;
                        case BehaviorType.CT:
                            Counter_State counter = new Counter_State(_set.REAL_NAME)
                            {
                                StateType = BehaviorType.CT,
                                nextAttackCanRushFirst = false,
                                SkillConfig = skillConfig
                            };
                            BehaviorDic.Add(_set.REAL_NAME, counter);
                            if (!SkillTypeKeys.Contains(_set.REAL_NAME)) SkillTypeKeys.Add(_set.REAL_NAME);
                            break;
                        case BehaviorType.RB:
                            Dash_Back_State dashBackState = new Dash_Back_State(_set.REAL_NAME)
                            {
                                StateType = BehaviorType.RB,
                                nextAttackCanRushFirst = false,
                                SkillConfig = skillConfig
                            };
                            BehaviorDic.Add(_set.REAL_NAME, dashBackState);
                            if (!SkillTypeKeys.Contains(_set.REAL_NAME)) SkillTypeKeys.Add(_set.REAL_NAME);
                            break;
                        case BehaviorType.NONE:
                            // 除了我们特别例举出来的那些基础状态外按说都是攻击性状
                            // 另外脚本保存函数中，被带入toFormAttackStateList参数的是一个全部state的列表。
                            // 所以可能存在none状态
                            break;
                    }
                }
            }
        }
    }
}