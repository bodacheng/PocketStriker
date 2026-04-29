using System;
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
            
            _BasicPhysicSupport.OpenEnemyTouchingDrag(1);
            HaltMotion();
            AnimationManger.TriggerExpression(Facial.aggressive);
            _SkillCancelFlag.turn_off_flag();
            _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
            pEvents.CloseAllPersonalityEffects();
            if (Sensor.GetEnemiesByDistance(false).Count > 0)
            {
                if (Sensor.GetEnemiesByDistance(false)[0] != null)
                    RotateToTargetTween(Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
            }
            _Animator.applyRootMotion = true;
            AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            hasCausedDamege = false;
            _BO_Ani_E.CloseOnProcessEnergyFromBodyWeapons();
            _BasicPhysicSupport.OpenEnemyTouchingDrag(0);
            _BasicPhysicSupport.hiddenMethods.ClearTouchedEnemyBody();
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(false);
            disposable.Dispose();
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
            _BasicPhysicSupport.hiddenMethods.RecoverRootPosChange();
        }
    }
}
