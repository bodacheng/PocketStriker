using System;

namespace Skill
{
    [Serializable]
    public enum BehaviorType
    {
        NONE = 0,
        MV = 7,
        AC = 4,
        GR = 1,
        GM = 2,
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
        public string RECORD_ID;//和Skills表id对应
        public string TYPE;
        public string REAL_NAME;
        public string SHOW_NAME;
        public float ATTACK_WEIGHT;
        public float HP_WEIGHT;
        public BehaviorType STATE_TYPE;
        public int SP_LEVEL;
        public string EVENT_CODE;
        public AIAttrs AIAttrs = new AIAttrs();
        
        public SkillConfig Clone()
        {
            return (SkillConfig)MemberwiseClone();
        }
                
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
        
        // 后面的三个bool变量意思是是否进行限制
        public static bool RangeLimit(float dis_min ,float dis_max, bool close, bool near, bool far)
        {
            if (dis_max > dis_min)
            {
                return (!close || (dis_min < 5 && dis_max >= 0f))
                && (!near || (dis_min < 10f && dis_max >= 5f))
                && (!far || dis_max >= 10f);
            }
            return false;
        }
    }
}

