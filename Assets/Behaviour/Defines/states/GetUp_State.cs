using UnityEngine;
using MCombat.Shared.Behaviour;

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
            counter = 0f;
            SkillStateRuntimeUtility.EnterGetUp(this, clip_name);
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
            SkillStateRuntimeUtility.ExitGetUp(this);
        }
    }
}
