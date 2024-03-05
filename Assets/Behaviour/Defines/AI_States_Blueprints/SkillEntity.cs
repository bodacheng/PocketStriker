using UnityEngine;

namespace Skill
{
    public class AIAttrs
    {
        public float AI_MIN_DIS;
        public float AI_MAX_DIS;
        public int height = 1; // 0:低 1:中 2:高
    }

    [System.Serializable]
    public class SkillEntity
    {
        public string SkillID;
        public string REAL_NAME;
        public BehaviorType StateType;
        public AIAttrs AIAttrs = new AIAttrs();
        public string[] CasualTo = { };
        public bool CAN_BE_CANCELLED_TO = true;
        public InputKey EnterInput = InputKey.Null;
        public InputKey ExitInput = InputKey.Null;
        public int SP_LEVEL;
        
        [HideInInspector]
        public string[] ForcedTransitions = { };
        
        public SkillEntity()
        {
        }
        
        public SkillEntity( string SkillID,
                            string REAL_NAME,
                            BehaviorType _attackType,
                            AIAttrs AIAttrs,
                            string[] _casual_to_state_nums,
                            string[] _forced_to_state_nums,
                            InputKey _enterInput, InputKey _exitInput,
                            int _SPMove)
        {
            this.SkillID = SkillID;
            this.REAL_NAME = REAL_NAME;
            this.StateType = _attackType;
            this.CasualTo = _casual_to_state_nums;
            this.ForcedTransitions = _forced_to_state_nums;
            this.EnterInput = _enterInput;
            this.ExitInput = _exitInput;
            this.SP_LEVEL = _SPMove;
            this.AIAttrs = AIAttrs;
            
            if (this.CasualTo == null)
            {
                this.CasualTo = new string[] { };
            }
            if (this.ForcedTransitions == null)
            {
                this.ForcedTransitions = new string[] { };
            }
        }
        
        public SkillEntity( string REAL_NAME,
                            BehaviorType _BType,
                            AIAttrs aIAttrs,
                            bool can_be_cancelled_to,
                            InputKey enterInput, InputKey exitInput,
                            int SPMove)
        {
            SkillID = null;
            this.REAL_NAME = REAL_NAME;
            StateType = _BType;
            CAN_BE_CANCELLED_TO = can_be_cancelled_to;
            EnterInput = enterInput;
            ExitInput = exitInput;
            SP_LEVEL = SPMove;
            AIAttrs = aIAttrs;
        }
        
        public SkillEntity( string REAL_NAME,
                            BehaviorType _attackType,
                            AIAttrs aIAttrs,
                            string[] _casual_to_state_nums,
                            string[] _forced_to_state_nums,
                            InputKey _enterInput, InputKey _exitInput,
                            int _SPMove)
        {
            this.SkillID = null;
            this.REAL_NAME = REAL_NAME;
            this.StateType = _attackType;
            this.CasualTo = _casual_to_state_nums;
            this.ForcedTransitions = _forced_to_state_nums;
            this.EnterInput = _enterInput;
            this.ExitInput = _exitInput;
            this.SP_LEVEL = _SPMove;
            AIAttrs = aIAttrs;
            
            if (this.CasualTo == null)
            {
                this.CasualTo = new string[] { };
            }
            if (this.ForcedTransitions == null)
            {
                this.ForcedTransitions = new string[] { };
            }
        }
        
        public SkillEntity( string SkillID,
                            string REAL_NAME,
                            BehaviorType _BType,
                            float AITriggerDistanceMin,float AITriggerDistanceMax,
                            bool can_be_cancelled_to,
                            InputKey enterInput, InputKey exitInput,
                            int SPMove)
        {
            this.SkillID = SkillID;
            this.REAL_NAME = REAL_NAME;
            StateType = _BType;
            CAN_BE_CANCELLED_TO = can_be_cancelled_to;
            EnterInput = enterInput;
            ExitInput = exitInput;
            SP_LEVEL = SPMove;
            AIAttrs.AI_MIN_DIS = AITriggerDistanceMin;
            AIAttrs.AI_MAX_DIS = AITriggerDistanceMax;
        }
        
        public static SkillEntity GetR_SE(RushType RStyle)
        {
            SkillEntity R_SE = null;
            switch (RStyle)
            {
                case RushType.Rush:
                    R_SE = new SkillEntity
                    {
                        REAL_NAME = "Rush",
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
                    break;
                case RushType.RushBack:
                    R_SE = new SkillEntity
                    {
                        REAL_NAME = "RushBack",
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
                    break;
                case RushType.None:
                    R_SE = null;
                    break;
            }
            return R_SE;
        }

        public static SkillEntity GetD_SE()
        {
            return new SkillEntity
            {
                REAL_NAME = "Defend",
                StateType = BehaviorType.Def,
                AIAttrs = new AIAttrs
                {
                    AI_MIN_DIS = -1,
                    AI_MAX_DIS = -1
                },
                CasualTo = null,
                ForcedTransitions = null,
                EnterInput = InputKey.Defend,
                ExitInput = InputKey.Defend_Cancel,
                SP_LEVEL = -1
            };
        }

        public static SkillEntity GetM_SE(MoveType moveType)
        {
            SkillEntity M_SE;
            switch (moveType)
            {
                case MoveType.Move_normal:
                    M_SE = new SkillEntity
                    {
                        REAL_NAME = "Move",
                        StateType = BehaviorType.MV,
                        AIAttrs = new AIAttrs
                        {
                            AI_MIN_DIS = -1,
                            AI_MAX_DIS = -1
                        },
                        CasualTo = null,
                        ForcedTransitions = null,
                        EnterInput = InputKey.Null,
                        ExitInput = InputKey.Null,
                        SP_LEVEL = -1
                    };
                    break;
                case MoveType.Move_slow:
                    M_SE = new SkillEntity
                    {
                        REAL_NAME = "Move",
                        StateType = BehaviorType.MV,
                        AIAttrs = new AIAttrs
                        {
                            AI_MIN_DIS = -1,
                            AI_MAX_DIS = -1
                        },
                        CasualTo = null,
                        ForcedTransitions = null,
                        EnterInput = InputKey.Null,
                        ExitInput = InputKey.Null,
                        SP_LEVEL = -1
                    };
                    break;
                case MoveType.Move_fast:
                    M_SE = new SkillEntity()
                    {
                        REAL_NAME = "Move",
                        StateType = BehaviorType.MV,
                        AIAttrs = new AIAttrs
                        {
                            AI_MIN_DIS = -1,
                            AI_MAX_DIS = -1
                        },
                        CasualTo = null,
                        ForcedTransitions = null,
                        EnterInput = InputKey.Null,
                        ExitInput = InputKey.Null,
                        SP_LEVEL = -1
                    };
                    break;
                default:
                    M_SE = new SkillEntity
                    {
                        REAL_NAME = "Move",
                        StateType = BehaviorType.MV,
                        AIAttrs = new AIAttrs
                        {
                            AI_MIN_DIS = -1,
                            AI_MAX_DIS = -1
                        },
                        CasualTo = null,
                        ForcedTransitions = null,
                        EnterInput = InputKey.Null,
                        ExitInput = InputKey.Null,
                        SP_LEVEL = -1
                    };
                    break;
            }
            return M_SE;
        }
    }
    
    public enum MoveType
    {
        Move_normal = 1,
        Move_slow = 2,
        Move_fast = 3
    }
    
    public enum RushType
    {
        None = -1,
        RushBack = 2,
        Rush = 3
    }
}