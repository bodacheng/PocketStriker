using UnityEngine;
using MCombat.Shared.Behaviour;

namespace Soul
{
    public class Empty_State : Behavior
    {
        public override bool Capacity_Exit_Condition()
        {
            return false;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            SkillStateRuntimeUtility.EnterEmpty(this);
        }

        public override void AI_State_exit()
        {
            SkillStateRuntimeUtility.ExitEmpty(this);
        }
    }
}
