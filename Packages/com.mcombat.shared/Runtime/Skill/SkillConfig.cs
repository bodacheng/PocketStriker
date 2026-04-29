using System;

namespace Skill
{
    public class AIAttrs
    {
        public float AI_MIN_DIS;
        public float AI_MAX_DIS;
        public int height = 1;
    }

    [Serializable]
    public enum BehaviorType
    {
        NONE = 0,
        MV = 7,
        AC = 4,
        RB = 11,
        GR = 1,
        GM = 2,
        GMB = 12,
        GI = 3,
        CT = 9,
        Def = 8,
        Hit = 6,
        KnockOff = 5,
        GetUp = 10,
    }

    [Serializable]
    public class SkillConfig
    {
        public string RECORD_ID;
        public string TYPE;
        public string REAL_NAME;
        public string SHOW_NAME;
        public float ATTACK_WEIGHT;
        public float HP_WEIGHT;
        public BehaviorType STATE_TYPE;
        public int SP_LEVEL;
        public string EVENT_CODE;
        public AIAttrs AIAttrs = new AIAttrs();

        public SkillConfig()
        {
            RECORD_ID = null;
            TYPE = null;
            REAL_NAME = null;
            SHOW_NAME = null;
            ATTACK_WEIGHT = 1;
            HP_WEIGHT = 1;
            STATE_TYPE = BehaviorType.NONE;
            SP_LEVEL = 0;
            EVENT_CODE = null;
        }

        public SkillConfig Clone()
        {
            return (SkillConfig)MemberwiseClone();
        }

        public static bool RangeLimit(float disMin, float disMax, bool close, bool near, bool far)
        {
            if (disMax <= disMin)
            {
                return false;
            }

            return (!close || (disMin < 5 && disMax >= 0f))
                   && (!near || (disMin < 10f && disMax >= 5f))
                   && (!far || disMax >= 10f);
        }
    }
}
