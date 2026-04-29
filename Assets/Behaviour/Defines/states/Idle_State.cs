using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Soul
{
    public class Idle_State : Behavior
    {
        private bool motionReset = false;
        public Idle_State(string clipName)
        {
            this.clip_name = clipName;
        }
        
        public override void AI_State_enter()
        {
            base.AI_State_enter();
            motionReset = false;
            HaltMotion();
            AnimationManger.AnimationTrigger(clip_name,  CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
            this._Rigidbody.linearDamping = FightGlobalSetting.OnTouchEnemyBodyRigidDrag;
            
            if (clip_name == "victory")
            {
                var deadEnemy = Sensor.GetLastDeadEnemies();
                if (deadEnemy != null)
                    RotateToTargetTween(deadEnemy.transform.position, 0.01f);
            }
            else
            {
                if (Sensor.GetEnemiesByDistance(true).Count > 0)
                {
                    if (Sensor.GetEnemiesByDistance(false)[0] != null)
                    {
                        RotateToTargetTween(Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
                    }
                }
            }
        }

        public override bool Capacity_Exit_Condition()
        {
            return false;
        }
        
        public override void _State_Update()
        {
            if (!motionReset && clip_name == "victory" && !AnimationManger._toUse.isLooping && AnimationCasualFinishedFlag())
            {
                AnimationManger.AnimationTrigger(string.Empty,  0.05f);
                motionReset = true;
            }
        }
        
        public override void AI_State_exit()
        {
            base.AI_State_exit();
            this._Rigidbody.linearDamping = 0;
        }
    }
}
