using UnityEngine;

public class SkillCancelFlag : MonoBehaviour {

    public class HiddenMethods
    {
        readonly SkillCancelFlag skillCancelFlag;
        public HiddenMethods(SkillCancelFlag SkillCancelFlag)
        {
            skillCancelFlag = SkillCancelFlag;
        }
        
        public void SkillCancelFlagFixedUpdate()
        {
            if (skillCancelFlag.attackApproaching)
            {
                skillCancelFlag.AttackApproachLoopCounter += Time.fixedDeltaTime;
                if (skillCancelFlag.AttackApproachLoopCounter > 0.08f)
                {
                    skillCancelFlag.attackApproaching = false;
                }
            }
        }
        
        public void SetAttackApproachingFlag(bool startorend)
        {
            skillCancelFlag.attackApproaching = startorend;
        }
        public bool GetAttackApproachingFlag()
        {
            return skillCancelFlag.attackApproaching;
        }
    }

    public Data_Center _C;
    public HiddenMethods hiddenMethods;
    public bool Cancel_Flag;
    bool attackApproaching;
    float AttackApproachLoopCounter;

    void Awake()
    {
        hiddenMethods = new HiddenMethods(this);
    }
    
    public void turn_on_flag()
    {
        _C.Sensor.DetectionStart(1, true);
        Cancel_Flag = true;
    }

    public void turn_off_flag()
    {
        _C.bO_Weapon_Animation_Events.ClearMarkerManagers();
        Cancel_Flag = false;
    }

    public void TurnRotationAdjustmentStartFlagWithoutstepfoward(int i = 1)
    {
        if (i == 1)
        {
            _C.Sensor.GetEnemiesByDistance(true);
            if (_C.Sensor.GetEnemiesByDistance(false).Count > 0)
            {
                if (_C.Sensor.GetEnemiesByDistance(false)[0] != null)
                {
                    _C._MyBehaviorRunner.GetNowState().RotateToTargetTween(_C.Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
                }
            }
            AttackApproachLoopCounter = 0f;
            attackApproaching = false;//与校准方向一起 开始校准迈步
        }
    }

    public void TurnRotationAdjustmentStartFlag(int i = 1)
    {
        if (i == 1)
        {
            _C.Sensor.GetEnemiesByDistance(true);
            if (_C.Sensor.GetEnemiesByDistance(false).Count > 0)
            {
                if (_C.Sensor.GetEnemiesByDistance(false)[0] != null)
                {
                    _C._MyBehaviorRunner.GetNowState().RotateToTargetTween(_C.Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
                }
            }
            AttackApproachLoopCounter = 0f;
            attackApproaching = true;//与校准方向一起 开始校准迈步
        }
    }
}
