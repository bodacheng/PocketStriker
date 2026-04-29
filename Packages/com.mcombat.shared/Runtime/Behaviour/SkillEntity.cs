using UnityEngine;

namespace Skill
{
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

        public SkillEntity(
            string skillID,
            string realName,
            BehaviorType attackType,
            AIAttrs aiAttrs,
            string[] casualTo,
            string[] forcedTransitions,
            InputKey enterInput,
            InputKey exitInput,
            int spMove)
        {
            SkillID = skillID;
            REAL_NAME = realName;
            StateType = attackType;
            CasualTo = casualTo ?? new string[] { };
            ForcedTransitions = forcedTransitions ?? new string[] { };
            EnterInput = enterInput;
            ExitInput = exitInput;
            SP_LEVEL = spMove;
            AIAttrs = aiAttrs;
        }

        public SkillEntity(
            string realName,
            BehaviorType behaviorType,
            AIAttrs aiAttrs,
            bool canBeCancelledTo,
            InputKey enterInput,
            InputKey exitInput,
            int spMove)
        {
            SkillID = null;
            REAL_NAME = realName;
            StateType = behaviorType;
            CAN_BE_CANCELLED_TO = canBeCancelledTo;
            EnterInput = enterInput;
            ExitInput = exitInput;
            SP_LEVEL = spMove;
            AIAttrs = aiAttrs;
        }

        public SkillEntity(
            string realName,
            BehaviorType attackType,
            AIAttrs aiAttrs,
            string[] casualTo,
            string[] forcedTransitions,
            InputKey enterInput,
            InputKey exitInput,
            int spMove)
        {
            SkillID = null;
            REAL_NAME = realName;
            StateType = attackType;
            CasualTo = casualTo ?? new string[] { };
            ForcedTransitions = forcedTransitions ?? new string[] { };
            EnterInput = enterInput;
            ExitInput = exitInput;
            SP_LEVEL = spMove;
            AIAttrs = aiAttrs;
        }

        public SkillEntity(
            string skillID,
            string realName,
            BehaviorType behaviorType,
            float aiTriggerDistanceMin,
            float aiTriggerDistanceMax,
            bool canBeCancelledTo,
            InputKey enterInput,
            InputKey exitInput,
            int spMove)
        {
            SkillID = skillID;
            REAL_NAME = realName;
            StateType = behaviorType;
            CAN_BE_CANCELLED_TO = canBeCancelledTo;
            EnterInput = enterInput;
            ExitInput = exitInput;
            SP_LEVEL = spMove;
            AIAttrs.AI_MIN_DIS = aiTriggerDistanceMin;
            AIAttrs.AI_MAX_DIS = aiTriggerDistanceMax;
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
            return new SkillEntity
            {
                SkillID = moveType.ToString(),
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
        }

        public static SkillEntity GetR_SE(RushType rushType)
        {
            return null;
        }
    }

    public enum MoveType
    {
        normal = 1,
        slow = 2,
        fast = 3,

        Move_normal = normal,
        Move_slow = slow,
        Move_fast = fast
    }

    public enum RushType
    {
        None = -1,
        RushBack = 2,
        Rush = 3
    }
}
