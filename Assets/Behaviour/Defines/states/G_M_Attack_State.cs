using UnityEngine;
using MCombat.Shared.Behaviour;

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
            SkillStateRuntimeUtility.EnterAggressiveRootMotionAttack(
                this,
                clip_name,
                CombatRotationAdjustment.StepForward,
                true);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            SkillStateRuntimeUtility.ExitMovingAttack(this, false, false);
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
            ((ICombatBehaviorRuntime)this).RecoverRootPositionChange();
        }

        public override void _State_FixedUpdate1()
        {
            ((ICombatBehaviorRuntime)this).PreventUnitOverlap();
        }
    }
}
