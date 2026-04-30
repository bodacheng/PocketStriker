using System;
using MCombat.Shared.Behaviour;
using UniRx;
using UnityEngine;

namespace Soul
{
    public class G_M_B_State : Behavior
    {
        private int damageCount = 0;
        private bool hasCausedDamege = false;
        private IDisposable disposable;
        #region Constructor
        public G_M_B_State(string clip_name)
        {
            this.clip_name = clip_name;
        }
        #endregion
        
        #region Capacity Enter Exit
        public override void AI_State_enter()
        {
            base.AI_State_enter();
            hasCausedDamege = false;
            damageCount = this.FightParamsRef._comboHitCount.HitCount.Value;
            void CheckDamage(int damage)
            {
                if (damage > damageCount)
                {
                    hasCausedDamege = true;
                    _SkillCancelFlag.turn_on_flag();
                }
                damageCount = damage;
            }
            disposable = this.FightParamsRef._comboHitCount.HitCount.
                ObserveEveryValueChanged(x => x.Value).
                Subscribe(CheckDamage);

            SkillStateRuntimeUtility.EnterAggressiveRootMotionAttack(
                this,
                clip_name,
                CombatRotationAdjustment.StepForward,
                true);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            hasCausedDamege = false;
            SkillStateRuntimeUtility.ExitMovingAttack(this, true, false);
            disposable?.Dispose();
        }
        #endregion

        #region Capacity Enter Exit
        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag() || hasCausedDamege;
        }
        #endregion

        public override void _State_Update()
        {
            ((ICombatBehaviorRuntime)this).RecoverRootPositionChange();
        }
    }
}
