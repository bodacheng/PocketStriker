using UnityEngine;

namespace Soul
{
    public class GetUp : Behavior
    {
        float counter;
        public GetUp(string clipName)
        {
            clip_name = clipName;
        }
        
        // On what condition can we exit this state 
        public override bool Capacity_Exit_Condition()
        {
            return counter > FightGlobalSetting._GetupTime;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            _SkillCancelFlag.turn_on_flag();
            counter = 0f;
            _Animator.SetFloat("speed", 0f);
            Sensor.DetectionStart(5, false);
            _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
        }

        public override void C_State_enter()
        {
            AI_State_enter();
        }

        public override void _State_FixedUpdate1()
        {
            counter += Time.fixedDeltaTime;
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _SkillCancelFlag.turn_off_flag();
        }
    }
}