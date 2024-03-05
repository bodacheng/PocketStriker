using UnityEngine;

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
            _DATA_CENTER.CleanClear();
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePosition;
        }

        public override void AI_State_exit()
        {
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _BasicPhysicSupport.SetUsingGravity(true);
        }
    }
}