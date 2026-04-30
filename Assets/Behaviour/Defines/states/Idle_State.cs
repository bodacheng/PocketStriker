using UnityEngine;
using MCombat.Shared.Behaviour;

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
            SkillStateRuntimeUtility.EnterIdle(this, clip_name, FightGlobalSetting.OnTouchEnemyBodyRigidDrag);
        }

        public override bool Capacity_Exit_Condition()
        {
            return false;
        }
        
        public override void _State_Update()
        {
            motionReset = SkillStateRuntimeUtility.TryResetFinishedVictoryIdle(this, clip_name, motionReset);
        }
        
        public override void AI_State_exit()
        {
            base.AI_State_exit();
            SkillStateRuntimeUtility.ExitIdle(this);
        }
    }
}
