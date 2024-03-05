using UnityEngine;

namespace Soul
{
    public class G_M_Attack_State : Behavior
    {
        #region Constructor
        public G_M_Attack_State(string clip_name)
        {
            this.clip_name = clip_name;
        }
        #endregion
        
        #region Capacity Enter Exit
        public override void AI_State_enter()
        {
            base.AI_State_enter();
            _BasicPhysicSupport.OpenEnemyTouchingDrag(1);
            _Rigidbody.velocity = Vector3.zero;
            AnimationManger.TriggerExpression(Facial.aggressive);
            _Animator.SetFloat("speed", 0f);
            _SkillCancelFlag.turn_off_flag();
            _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
            pEvents.CloseAllPersonalityEffects();
            Sensor.GetEnemiesByDistance(true);
            if (Sensor.GetEnemiesByDistance(false).Count > 0)
            {
                if (Sensor.GetEnemiesByDistance(false)[0] != null)
                    RotateToTargetTween(Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
            }
            _Animator.applyRootMotion = true;
            AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            _BasicPhysicSupport.OpenEnemyTouchingDrag(0);
            _BasicPhysicSupport.hiddenMethods.ClearTouchedEnemyBody();
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(false);
        }
        #endregion

        #region Capacity Enter Exit
        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag();
        }
        #endregion

        public override void _State_Update()
        {
            _BasicPhysicSupport.hiddenMethods.RecoverRootPosChange();
        }
    }
}