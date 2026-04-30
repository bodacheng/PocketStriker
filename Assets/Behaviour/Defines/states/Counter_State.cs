using UnityEngine;
using MCombat.Shared.Behaviour;

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

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            SkillStateRuntimeUtility.EnterCounter(this, clip_name);
        }

        public override void _State_FixedUpdate1()
        {
            BehaviorMotionUtility.UpdateTouchingEnemyConstraints(this, FightGlobalSetting.ToEnemyNearestDis);
        }

        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag();
        }
    }
}
