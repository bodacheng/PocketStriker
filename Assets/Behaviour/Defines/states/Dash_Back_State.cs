using UnityEngine;

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
            _Animator.applyRootMotion = true;
            HaltMotion();
            _SkillCancelFlag.turn_off_flag();
            pEvents.CloseAllPersonalityEffects();
            Vector3 threatsComingPosition = Vector3.zero;

            if (_BasicPhysicSupport.NearRing)
            {
                threatsComingPosition = gameObject.transform.position * 2;
                threatsComingPosition.y = 0;
            }
            else
            {
                if (Sensor.GetEnemiesByDistance(false).Count > 0)
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
            }

            RotateToTargetTween(threatsComingPosition, 0.01f);

            AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
        }

        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag();
        }
    }
}
