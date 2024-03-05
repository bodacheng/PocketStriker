using UnityEngine;

namespace Soul
{
    public class Dash_Back_State : Behavior
    {
        readonly UnityEngine.Events.UnityAction breakFreeStart;
        readonly UnityEngine.Events.UnityAction breakFreeEnd;
        readonly CustomCoroutine breakFreeCoroutine;

        public Dash_Back_State()
        {
            clip_name = "rushback";
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
        
        public override void _State_Update()
        {
            base._State_Update();
            if (BehaviorFrameCounter == 5)
            {
                _BuffsRunner.RunSubCoroutineOfState(breakFreeCoroutine);
            }
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            _Animator.applyRootMotion = true;
            _Animator.SetFloat("speed", 0f);
            Sensor.DetectionStart(2, true);
            _SkillCancelFlag.turn_off_flag();
            pEvents.CloseAllPersonalityEffects();
            Vector3 threatsComingPosition = Vector3.zero;
            if (Sensor.GetEnemiesByDistance(true).Count > 0)
                threatsComingPosition = Sensor.GetEnemiesByDistance(false)[0].transform.position;

            Collider threat = Sensor.GetSuddenThreatInRange(0, 5);
            if (threat != null)
            {
                threatsComingPosition = threat.transform.position;
            }
            else
            {
                Collider temp = Sensor.GetClosestEnemyColliderInSensorRange();
                if (temp != null)
                    threatsComingPosition = temp.transform.position;
            }
            RotateToTargetTween(threatsComingPosition, 0.01f);
            AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
        }

        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag();
        }
    }
}