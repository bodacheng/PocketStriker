using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using HittingDetection;
using MCombat.Shared.Behaviour;
using Skill;
using UniRx;
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
            SkillStateRuntimeUtility.EnterEmpty(this);
        }

        public override void AI_State_exit()
        {
            SkillStateRuntimeUtility.ExitEmpty(this);
        }
    }

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

    public class GetUp : Behavior
    {
        float counter;

        public GetUp(string clipName)
        {
            clip_name = clipName;
        }

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

    public class Dash_Back_State : Behavior
    {
        public Dash_Back_State(string dashClipName)
        {
            clip_name = dashClipName;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            SkillStateRuntimeUtility.EnterDashBack(this, clip_name);
        }

        public override bool Capacity_Exit_Condition()
        {
            return MovingAttackStateRuntimeUtility.ShouldExitMovingAttack(this);
        }
    }

    public class Counter_State : Behavior
    {
        public Counter_State(string clipName)
        {
            clip_name = clipName;
        }

        public override void Pre_process_before_enter()
        {
            base.Pre_process_before_enter();
            nextAttackCanRushFirst = true;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            MovingAttackStateRuntimeUtility.EnterCounter(this, clip_name);
        }

        public override void _State_FixedUpdate1()
        {
            MovingAttackStateRuntimeUtility.UpdateCounter(this, FightGlobalSetting.ToEnemyNearestDis);
        }

        public override bool Capacity_Exit_Condition()
        {
            return MovingAttackStateRuntimeUtility.ShouldExitMovingAttack(this);
        }
    }

    public class GMoveEscapeState : Behavior
    {
        Transform mainCam;
        Vector3 useDirection;

        readonly UnityEngine.Events.UnityAction breakFreeStart;
        readonly UnityEngine.Events.UnityAction breakFreeEnd;
        readonly CustomCoroutine breakFreeCoroutine;

        public GMoveEscapeState(string clipName)
        {
            clip_name = clipName;
            breakFreeStart = () =>
            {
                // Reserved for project-specific resistance hooks.
            };
            breakFreeEnd = () =>
            {
                // Reserved for project-specific resistance hooks.
            };
            breakFreeCoroutine = new CustomCoroutine(breakFreeStart, 0.6f, breakFreeEnd);
        }

        public override void _State_Update()
        {
            base._State_Update();
            if (BehaviorFrameCounter == 5)
                _BuffsRunner.RunSubCoroutineOfState(breakFreeCoroutine);
        }

        public override bool Capacity_enter_condition()
        {
            return _BasicPhysicSupport.hiddenMethods.Grounded && base.Capacity_enter_condition();
        }

        public override bool Capacity_Exit_Condition()
        {
            return MovingAttackStateRuntimeUtility.ShouldExitMovingAttack(this);
        }

        void CommonEnter()
        {
            base.AI_State_enter();
            SkillStateRuntimeUtility.EnterEscapeCommon(this, clip_name);
        }

        public override void AI_State_enter()
        {
            CommonEnter();
            useDirection = SkillStateRuntimeUtility.ResolveAiEscapeDirection(this);
            RotateToTargetTween(gameObject.transform.position + useDirection, 0.1f);
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

            useDirection = SkillStateRuntimeUtility.ResolveCameraRelativeDirection(
                gameObject.transform.forward,
                mainCam.eulerAngles.y,
                h,
                v);
            RotateToTargetTween(gameObject.transform.position + useDirection, 0.1f);
        }
    }

    public class G_Attack_State : Behavior
    {
        readonly bool isEventAttackLaunchState;
        readonly bool isEventAttackEndState;
        readonly float rushSpeed;
        readonly float approachSpeed;
        readonly float maxRushTime;
        float rushTimeCounter;
        GeneralAttackPhase phase;
        CustomCoroutine rushCoroutine;
        Collider collider;

        public G_Attack_State(string dashClipName, float rushSpeed, float maxRushTime, float approachingSpeed, string clipName)
        {
            this.rushSpeed = rushSpeed;
            this.maxRushTime = maxRushTime;
            approachSpeed = approachingSpeed;
            this.clip_name = clipName;
        }

        public G_Attack_State(string dashClipName, float rushSpeed, float maxRushTime, string clipName, bool eventLauncherOrEnder)
        {
            this.maxRushTime = maxRushTime;
            this.rushSpeed = rushSpeed;
            this.clip_name = clipName;
            isEventAttackLaunchState = eventLauncherOrEnder;
            isEventAttackEndState = !eventLauncherOrEnder;
        }

        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag() && this.AnimationManger._toUse.name == clip_name;
        }

        public override void Pre_process_before_enter()
        {
            base.Pre_process_before_enter();
            UnityEngine.Events.UnityAction rushStart = () =>
            {
                FightParamsRef.Resistance.Value += 1;
            };
            UnityEngine.Events.UnityAction rushEnd = () =>
            {
                FightParamsRef.Resistance.Value -= 1;
            };
            rushCoroutine = new CustomCoroutine(rushStart, 5f, rushEnd);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            GeneralAttackStateRuntimeUtility.ExitGeneralAttack(
                this,
                rushCoroutine,
                isEventAttackLaunchState,
                isEventAttackEndState);
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            collider = null;
            rushTimeCounter = 0f;
            var enemiesByDistance = Sensor.GetEnemiesByDistance(false);
            var hasEnemy = enemiesByDistance.Count > 0;
            collider = hasEnemy ? Sensor.GetClosestEnemyColliderInSensorRange() : null;
            phase = GeneralAttackStateRuntimeUtility.EnterGeneralAttack(
                this,
                clip_name,
                hasEnemy,
                collider != null,
                collider != null ? Vector3.Distance(gameObject.transform.position, collider.transform.position) : 0f);
        }

        public override void _State_FixedUpdate1()
        {
            phase = GeneralAttackStateRuntimeUtility.UpdateGeneralAttack(
                this,
                phase,
                clip_name,
                collider != null,
                collider != null ? collider.transform.position : Vector3.zero,
                rushSpeed,
                rushTimeCounter,
                maxRushTime,
                approachSpeed,
                rushCoroutine,
                FightGlobalSetting.ToEnemyNearestDis);
        }
    }

    public class G_M_Attack_State : Behavior
    {
        public G_M_Attack_State(string clipName)
        {
            this.clip_name = clipName;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            MovingAttackStateRuntimeUtility.EnterMovingAttack(this, clip_name);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            MovingAttackStateRuntimeUtility.ExitMovingAttack(this, false);
        }

        public override bool Capacity_Exit_Condition()
        {
            return MovingAttackStateRuntimeUtility.ShouldExitMovingAttack(this);
        }

        public override void _State_Update()
        {
            MovingAttackStateRuntimeUtility.UpdateMovingAttack(this, false);
        }

        public override void _State_FixedUpdate1()
        {
            MovingAttackStateRuntimeUtility.PreventUnitOverlap(this);
        }
    }

    public class G_M_B_State : Behavior
    {
        private MoveBodyAttackRuntimeState attackState;
        private IDisposable disposable;

        public G_M_B_State(string clipName)
        {
            this.clip_name = clipName;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            attackState = MovingAttackStateRuntimeUtility.EnterMoveBodyAttack(
                this,
                clip_name,
                this.FightParamsRef._comboHitCount.HitCount.Value);
            void CheckDamage(int damage)
            {
                MovingAttackStateRuntimeUtility.UpdateMoveBodyAttackDamage(this, ref attackState, damage);
            }
            disposable = this.FightParamsRef._comboHitCount.HitCount.
                ObserveEveryValueChanged(x => x.Value).
                Subscribe(CheckDamage);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            MovingAttackStateRuntimeUtility.ExitMoveBodyAttack(this, ref attackState, disposable);
            disposable = null;
        }

        public override bool Capacity_Exit_Condition()
        {
            return MovingAttackStateRuntimeUtility.ShouldExitMoveBodyAttack(this, attackState);
        }

        public override void _State_Update()
        {
            MovingAttackStateRuntimeUtility.UpdateMovingAttack(this, false);
        }
    }

    public class Move_State : Behavior
    {
        readonly float speed;
        readonly float timeLimit;
        float timeCounter;
        Vector3 useDirection;
        CombatMoveDirection moveDirection;
        Transform mainCam;
        GameObject closestEnemy;
        float h;
        float v;

        public Move_State(MoveType moveType)
        {
            var settings = MoveStateRuntimeUtility.ResolveSettings(moveType, FightGlobalSetting._fighterMoveSpeed);
            speed = settings.Speed;
            timeLimit = settings.TimeLimit;
        }

        public override bool Capacity_enter_condition()
        {
            return true;
        }

        void CommonMoveEnter()
        {
            timeCounter = 0f;
            SkillStateRuntimeUtility.EnterMove(this);
            mainCam = CameraManager._camera.transform;
        }

        public override void C_State_enter()
        {
            CommonMoveEnter();
        }

        public override void AI_State_enter()
        {
            CommonMoveEnter();
            DecideMoveDirection();
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            SkillStateRuntimeUtility.ExitMove(this);
        }

        bool MoveSegmentFinished()
        {
            var returnValue = MoveStateRuntimeUtility.IsAiMoveSegmentFinished(
                moveDirection,
                timeCounter,
                timeLimit,
                closestEnemy != null,
                gameObject.transform.position,
                closestEnemy != null ? closestEnemy.transform.position : Vector3.zero,
                FightGlobalSetting._closeDis);
            if (returnValue)
                timeCounter = 0f;
            return returnValue;
        }

        void DecideMoveDirection()
        {
            var enemiesByDistance = Sensor.GetEnemiesByDistance(true);
            closestEnemy = enemiesByDistance.Count > 0 ? enemiesByDistance[0] : null;
            var nextSkillRange = _AIStateRunner.CalAdviceDistanceFromEnemy();
            var decision = MoveStateRuntimeUtility.ResolveAiMoveDecision(
                _BasicPhysicSupport.NearRing,
                gameObject.transform.position,
                closestEnemy != null,
                closestEnemy != null ? closestEnemy.transform.position : Vector3.zero,
                nextSkillRange.Item1,
                FightGlobalSetting._closeDis);
            moveDirection = decision.Direction;
            useDirection = decision.UseDirection;
        }

        void UpdateAiMove()
        {
            timeCounter += Time.fixedDeltaTime;
            if (_BasicPhysicSupport.NearRing)
            {
                DecideMoveDirection();
            }

            if (MoveSegmentFinished())
            {
                DecideMoveDirection();
            }

            switch (moveDirection)
            {
                case CombatMoveDirection.Stay:
                case CombatMoveDirection.BackTowardsEnemy:
                case CombatMoveDirection.TowardsEnemy:
                    useDirection = MoveStateRuntimeUtility.ResolveAiFrameDirection(
                        moveDirection,
                        useDirection,
                        closestEnemy != null,
                        gameObject.transform.position,
                        closestEnemy != null ? closestEnemy.transform.position : Vector3.zero);
                    break;
            }

            var blockingPair = Sensor.EnemyAndTeammateBetweenMeAndEnemy();
            if (blockingPair != null)
            {
                useDirection = MoveStateRuntimeUtility.ResolveDirectionAroundBlockingUnits(
                    useDirection,
                    gameObject.transform.position,
                    blockingPair[0].transform.position,
                    blockingPair[1].transform.position,
                    Time.fixedDeltaTime,
                    10f);
            }

            useDirection = MoveStateRuntimeUtility.NormalizePlanar(useDirection);
        }

        void UpdateControlledMove()
        {
            if (!CommonSetting.PcMode)
            {
                h = (Input.GetKey(AppSetting.Value.LeftKeyCode) ? -1f : 0f) +
                    (Input.GetKey(AppSetting.Value.RightKeyCode) ? 1f : 0f) +
                    UltimateJoystick.GetHorizontalAxis("joystick");
                v = (Input.GetKey(AppSetting.Value.UpKeyCode) ? 1f : 0f) +
                    (Input.GetKey(AppSetting.Value.DownKeyCode) ? -1f : 0f) +
                    UltimateJoystick.GetVerticalAxis("joystick");
            }
            else
            {
                h = (Input.GetKey(AppSetting.Value.LeftKeyCode) ? -1f : 0f) +
                    (Input.GetKey(AppSetting.Value.RightKeyCode) ? 1f : 0f);
                v = (Input.GetKey(AppSetting.Value.UpKeyCode) ? 1f : 0f) +
                    (Input.GetKey(AppSetting.Value.DownKeyCode) ? -1f : 0f);
            }

            useDirection = MoveStateRuntimeUtility.ResolveControlMoveDirection(mainCam.eulerAngles.y, h, v);
        }

        public override void _c_State_FixedUpdate1()
        {
            UpdateControlledMove();
            if (!_AIStateRunner.BeingControl())
            {
                useDirection = Vector3.zero;
            }
            ApplyMovementIntent(useDirection, speed, 20f);
            PreventUnitOverlap();
        }

        public override void _State_FixedUpdate1()
        {
            UpdateAiMove();
            ApplyMovementIntent(useDirection, speed, 20f);
        }
    }

    public class Defend_State : Behavior
    {
        readonly string defendClipName;
        readonly string blockBreakName;
        readonly DefendRuntimeState runtimeState = new DefendRuntimeState();
        readonly float defendHpRefreshTime = 5f;

        Tweener blockDisplacementTweener;
        const float BlockPushMoveRatio = 0.2f;

        public Defend_State(string defendClipName, string blockBreakName)
        {
            this.defendClipName = defendClipName;
            this.blockBreakName = blockBreakName;
        }

        public override void Pre_process_before_enter()
        {
            base.Pre_process_before_enter();
            DefendStateRuntimeUtility.ResetHp(runtimeState);
        }

        public override bool Capacity_enter_condition()
        {
            return DefendStateRuntimeUtility.CanEnter(runtimeState, Time.time, defendHpRefreshTime);
        }

        public override bool Capacity_Exit_Condition()
        {
            return runtimeState.TimeCounter <= 0f;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            blockDisplacementTweener?.Kill();
            DefendStateRuntimeUtility.EnterIdleBlock(
                this,
                runtimeState,
                defendClipName,
                FightGlobalSetting.LightBlockLastingTime);
        }

        public override void AI_State_enter(V_Damage newValue)
        {
            base.AI_State_enter();
            blockDisplacementTweener?.Kill();
            var pushDirection = CalFixPushVector(newValue, gameObject.transform.position);
            DefendStateRuntimeUtility.EnterDamageBlock(
                this,
                runtimeState,
                blockBreakName,
                defendClipName,
                newValue.from_weapon.damage_type,
                FightGlobalSetting.LightBlockLastingTime,
                FightGlobalSetting.HeavyBlockLastingTime);
            StartFixedBlockPush(pushDirection);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            blockDisplacementTweener?.Kill();
            DefendStateRuntimeUtility.Exit(this, runtimeState, Time.time);
        }

        void StartFixedBlockPush(Vector3 pushDirection)
        {
            var moveDuration = DefendStateRuntimeUtility.GetBlockPushDuration(runtimeState, BlockPushMoveRatio);
            var targetPos = CalcFixedPlanarMoveTarget(gameObject.transform.position, pushDirection, moveDuration);
            blockDisplacementTweener = StartFixedPlanarMoveTween(_DATA_CENTER.WholeT, _Rigidbody, targetPos, moveDuration);
        }

        public override void _State_FixedUpdate1()
        {
            DefendStateRuntimeUtility.FixedUpdate(this, runtimeState, defendClipName, Time.fixedDeltaTime);
        }
    }

    public class Death_State : Behavior
    {
        readonly KnockbackFlightRuntimeState flight = new KnockbackFlightRuntimeState();
        Tweener rotateTween;

        public Death_State()
        {
            StateType = BehaviorType.KnockOff;
        }

        public override void AI_State_enter()
        {
            base.AI_State_enter();
            KnockbackStateRuntimeUtility.EnterDeath(this);
        }

        public override void AI_State_enter(V_Damage newValue)
        {
            AI_State_enter();
            var pushDirection = CalFixPushVector(newValue, gameObject.transform.position);
            rotateTween = RotateToTargetTween(gameObject.transform.position - pushDirection, 0f);

            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(true);
            EffectsManager.GenerateEffect("super_hit", FightGlobalSetting.EffectPathDefine(newValue.from_weapon.element),
                newValue.DamageEffectPoint, newValue.CutRotation, null).Forget();
            var yCurve = newValue.from_weapon.damage_type == DamageType.high
                ? FightGlobalSetting.HDamageYAnimationCurve
                : FightGlobalSetting.KnockOffYAnimationCurve;
            var zCurve = newValue.from_weapon.damage_type == DamageType.high
                ? FightGlobalSetting.HDamageZAnimationCurve
                : FightGlobalSetting.KnockOffZAnimationCurve;
            KnockbackStateRuntimeUtility.BeginFlight(flight, pushDirection, yCurve, zCurve);
        }

        public override bool Capacity_Exit_Condition()
        {
            return false;
        }

        public override bool Force_enter_condition()
        {
            return false;
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            KnockbackStateRuntimeUtility.ExitDeath(this);
            if (rotateTween != null && rotateTween.active && rotateTween.IsPlaying())
            {
                rotateTween.Kill();
            }
        }

        public override void _State_Update()
        {
            KnockbackStateRuntimeUtility.UpdateDeathFlight(
                flight,
                this,
                new KnockbackFlightSettings(0.5f, 0f, 0f, 0.5f),
                GenerateWallCrackEffect,
                GenerateHitGroundEffect);
        }

        static void GenerateHitGroundEffect(Vector3 position, Quaternion rotation)
        {
            EffectsManager.GenerateEffect(CommonSetting.HitGroundEffectCode, null, position, rotation, null).Forget();
        }

        static void GenerateWallCrackEffect(Vector3 position, Quaternion rotation)
        {
            EffectsManager.GenerateEffect(CommonSetting.WallCrackEffectCode, null, position, rotation, null).Forget();
        }
    }

    public class Knock_Off_State : Behavior
    {
        readonly KnockbackFlightRuntimeState flight = new KnockbackFlightRuntimeState();
        Tweener rotateTween;

        public int FlyingStep => flight.FlyingStep;

        public Knock_Off_State()
        {
            StateType = BehaviorType.KnockOff;
        }

        public override void AI_State_enter(V_Damage value)
        {
            if (_DATA_CENTER.TryChangeToSub(StateKey, value))
            {
                return;
            }

            base.AI_State_enter();
            KnockbackStateRuntimeUtility.EnterKnockOff(this);
            var position = gameObject.transform.position;
            var pushDirection = CalFixPushVector(value, position);
            rotateTween = RotateToTargetTween(position - pushDirection, 0f);
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(true);
            EffectsManager.GenerateEffect("super_hit", FightGlobalSetting.EffectPathDefine(value.from_weapon.element),
                value.DamageEffectPoint, value.CutRotation, null).Forget();
            var yCurve = value.from_weapon.damage_type == DamageType.high
                ? FightGlobalSetting.HDamageYAnimationCurve
                : FightGlobalSetting.KnockOffYAnimationCurve;
            var zCurve = value.from_weapon.damage_type == DamageType.high
                ? FightGlobalSetting.HDamageZAnimationCurve
                : FightGlobalSetting.KnockOffZAnimationCurve;
            KnockbackStateRuntimeUtility.BeginFlight(flight, pushDirection, yCurve, zCurve);
        }

        public override bool Capacity_Exit_Condition()
        {
            return false;
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            KnockbackStateRuntimeUtility.ExitKnockOff(this);
            if (rotateTween != null && rotateTween.active && rotateTween.IsPlaying())
            {
                rotateTween.Kill();
            }
        }

        public override void _State_Update()
        {
            KnockbackStateRuntimeUtility.UpdateKnockOffFlight(
                flight,
                this,
                new KnockbackFlightSettings(
                    0.5f,
                    FightGlobalSetting._CanGetUpAfterKnockoffToGround,
                    FightGlobalSetting._MaxKnockoffLaidGroundTime,
                    0.5f),
                GenerateWallCrackEffect,
                GenerateHitGroundEffect);
        }

        static void GenerateHitGroundEffect(Vector3 position, Quaternion rotation)
        {
            EffectsManager.GenerateEffect(CommonSetting.HitGroundEffectCode, null, position, rotation, null).Forget();
        }

        static void GenerateWallCrackEffect(Vector3 position, Quaternion rotation)
        {
            EffectsManager.GenerateEffect(CommonSetting.WallCrackEffectCode, null, position, rotation, null).Forget();
        }
    }
}
