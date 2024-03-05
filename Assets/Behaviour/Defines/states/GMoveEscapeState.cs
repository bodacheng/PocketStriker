using UnityEngine;

namespace Soul
{
    public class GMoveEscapeState : Behavior
    {
        Transform mainCam;
        Quaternion screenMovementSpace;
        Vector3 screenMovementForward, screenMovementRight, use_direction;
        
        readonly UnityEngine.Events.UnityAction _breakFreeStart;
        readonly UnityEngine.Events.UnityAction _breakFreeEnd;
        readonly CustomCoroutine _breakFreeCoroutine;
        
        public GMoveEscapeState(string _clip_name)
        {
            clip_name = _clip_name;
            _breakFreeStart = () =>
            {
                //FightParamsRef.Resistance.Value += 2;
            };
            _breakFreeEnd = () =>
            {
                //FightParamsRef.Resistance.Value -= 2;
            };
            _breakFreeCoroutine = new CustomCoroutine(_breakFreeStart, 0.6f, _breakFreeEnd);
        }

        public override void _State_Update()
        {
            base._State_Update();
            if (BehaviorFrameCounter == 5)
                _BuffsRunner.RunSubCoroutineOfState(_breakFreeCoroutine);
        }
        
        public override bool Capacity_enter_condition()
        {
            return _BasicPhysicSupport.hiddenMethods.Grounded && base.Capacity_enter_condition();
        }

        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag();
        }

        void CommonEnter()
        {
            base.AI_State_enter();
            _Animator.SetFloat("speed", 0f);
            _SkillCancelFlag.turn_off_flag();
            _Rigidbody.velocity = Vector3.zero;
            pEvents.CloseAllPersonalityEffects();
            _Animator.applyRootMotion = true;
            AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
        }

        Vector3 damagingWeaponComingDirection;
        Collider threat;
        Collider ECollider;
        public override void AI_State_enter()
        {
            CommonEnter();
            use_direction = gameObject.transform.forward;
            threat = Sensor.GetSuddenThreatInRange(0, 5);
            
            if (_BasicPhysicSupport.AtRing)
            {
                use_direction = Vector3.zero - gameObject.transform.position;
                use_direction.y = 0;
            }
            else
            {
                if (threat != null)
                {
                    damagingWeaponComingDirection = gameObject.transform.position - threat.transform.position;
                    switch (Random.Range(0, 2))
                    {
                        case 0:
                            use_direction = Quaternion.Euler(0, -135, 0) * damagingWeaponComingDirection;
                            break;
                        case 1:
                            use_direction = Quaternion.Euler(0, 135, 0) * damagingWeaponComingDirection;
                            break;
                    }
                }
                else
                {
                    ECollider = Sensor.GetClosestEnemyColliderInSensorRange();
                    if (ECollider != null)
                        use_direction = -gameObject.transform.position + ECollider.transform.position;
                    switch (Random.Range(0, 2))
                    {
                        case 0:
                            use_direction = Quaternion.Euler(0, -90, 0) * use_direction;
                            break;
                        case 1:
                            use_direction = Quaternion.Euler(0, 90, 0) * use_direction;
                            break;
                    }
                }
            }

            RotateToTargetTween(gameObject.transform.position + use_direction, 0.1f);
        }
        
        float h;
        float v;
        public override void C_State_enter()
        {
            CommonEnter();
            mainCam = CameraManager._camera.transform;
            screenMovementSpace = Quaternion.Euler(0, mainCam.eulerAngles.y, 0);
            screenMovementForward = screenMovementSpace * Vector3.forward;
            screenMovementRight = screenMovementSpace * Vector3.right;

            h = UnityEngine.Input.GetAxis("Horizontal") + UltimateJoystick.GetHorizontalAxis("joystick");
            v = UnityEngine.Input.GetAxis("Vertical") + UltimateJoystick.GetVerticalAxis("joystick");

            if (System.Math.Abs(h) < 0.001f && System.Math.Abs(v) < 0.001f)
            {
                use_direction = gameObject.transform.forward;
            }
            else
            {
                use_direction = (screenMovementForward * v) + (screenMovementRight * h);
            }
            
            RotateToTargetTween(gameObject.transform.position + use_direction, 0.1f);
        }
    }
}