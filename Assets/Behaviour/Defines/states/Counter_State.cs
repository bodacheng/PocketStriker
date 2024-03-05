using UnityEngine;

namespace Soul
{
    public class Counter_State : Behavior
    {
        public Counter_State(string _clip_name)
        {
            clip_name = _clip_name;
        }

        public override void Pre_process_before_enter()
        {
            base.Pre_process_before_enter();
            nextAttackCanRushFirst = true;
        }

        Collider threat;
        public override void AI_State_enter()
        {
            base.AI_State_enter();
            _SkillCancelFlag.turn_off_flag();
            _Animator.SetFloat("speed", 0f);
            _SkillCancelFlag.TurnRotationAdjustmentStartFlagWithoutstepfoward(1);
            pEvents.CloseAllPersonalityEffects();
            AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
            _Rigidbody.velocity = Vector3.zero;
            _Animator.applyRootMotion = true;
            if (threat != null)
            {
                RotateToTargetTween(threat.transform.position, 0.02f);
            }
        }

        public override void _State_FixedUpdate1()
        {
            if (_BasicPhysicSupport.hiddenMethods.TouchingEnemy() && _BasicPhysicSupport.hiddenMethods.Grounded)
            {
                if (_BasicPhysicSupport.ToNearestEnemyXZ() >= FightGlobalSetting.ToEnemyNearestDis)
                {
                    _Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                }
            }
            else
            {
                _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag();
        }
    }
}