using UnityEngine;
using MCombat.Shared.Behaviour;

namespace Soul
{
    public class GMoveEscapeState : Behavior
    {
        Transform mainCam;
        Vector3 use_direction;

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
            SkillStateRuntimeUtility.EnterEscapeCommon(this, clip_name);
        }

        public override void AI_State_enter()
        {
            CommonEnter();
            use_direction = SkillStateRuntimeUtility.ResolveAiEscapeDirection(this);
            RotateToTargetTween(gameObject.transform.position + use_direction, 0.1f);
        }

        float h;
        float v;
        public override void C_State_enter()
        {
            CommonEnter();
            mainCam = CameraManager._camera.transform;
            h = (Input.GetKey(AppSetting.Value.LeftKeyCode) ? -1f : 0f) +
                    (Input.GetKey(AppSetting.Value.RightKeyCode) ? 1f : 0f) +
                    UltimateJoystick.GetHorizontalAxis("joystick");
            v = (Input.GetKey(AppSetting.Value.UpKeyCode) ? 1f : 0f) +
                    (Input.GetKey(AppSetting.Value.DownKeyCode) ? -1f : 0f) +
                    UltimateJoystick.GetVerticalAxis("joystick");

            use_direction = SkillStateRuntimeUtility.ResolveCameraRelativeDirection(
                gameObject.transform.forward,
                mainCam.eulerAngles.y,
                h,
                v);
            RotateToTargetTween(gameObject.transform.position + use_direction, 0.1f);
        }
    }
}
