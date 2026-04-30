using UnityEngine;
using MCombat.Shared.Behaviour;

namespace Soul
{
    public class Dash_Back_State : Behavior
    {
        readonly UnityEngine.Events.UnityAction breakFreeStart;
        readonly UnityEngine.Events.UnityAction breakFreeEnd;
        readonly CustomCoroutine breakFreeCoroutine;

        public Dash_Back_State(string dashClipName)
        {
            clip_name = dashClipName;
            breakFreeStart = () =>
            {
                FightParamsRef.Resistance.Value += 10;
            };
            breakFreeEnd = () =>
            {
                FightParamsRef.Resistance.Value -= 10;
            };
            breakFreeCoroutine = new CustomCoroutine(breakFreeStart, 0.6f, breakFreeEnd);
        }

        // public override void _State_Update()
        // {
        //     base._State_Update();
        //     if (BehaviorFrameCounter == 5)
        //     {
        //         _BuffsRunner.RunSubCoroutineOfState(breakFreeCoroutine);
        //     }
        // }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            SkillStateRuntimeUtility.EnterDashBack(this, clip_name);
        }

        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag();
        }
    }
}
