using DG.Tweening;
using HittingDetection;
using MCombat.Shared.Behaviour;
using UnityEngine;
using Skill;

namespace Soul
{
    public abstract partial class Behavior : ICombatBehaviorRuntime
    {
        protected GameObject gameObject;
        protected Rigidbody _Rigidbody;
        protected BehaviorRunner _AIStateRunner;
        public Data_Center _DATA_CENTER;
        protected BO_Ani_E _BO_Ani_E;
        protected FightParamsReference FightParamsRef;
        protected ResistanceManager _ResistanceManager;
        protected BasicPhysicSupport _BasicPhysicSupport;
        protected Sensor Sensor;
        protected Animator _Animator;
        protected SkillCancelFlag _SkillCancelFlag;
        protected BO_Weapon_Animation_Events _Weapon_Animation_Events;
        protected ShaderManager shaderManager;
        protected AnimationManger AnimationManger;
        protected BuffsRunner _BuffsRunner;
        protected BlendShapeProxy blendShapeProxy;
        protected Personality_events pEvents;

        public SkillConfig SkillConfig;
        public float Attack;
        public string StateKey;
        protected string clip_name;
        public int spLevel;
        public float triggerAttackRangeMin, triggerAttackRangeMax;
        public int TriggerAttackHeight;
        public BehaviorType StateType;
        public bool nextAttackCanRushFirst;
        bool AbsorbEnergyFinished;
        int temp;
        const float MovementEpsilon = 0.0001f;

        BehaviorType ICombatBehaviorRuntime.StateType => StateType;
        Vector3 ICombatBehaviorRuntime.Position => gameObject.transform.position;
        Vector3 ICombatBehaviorRuntime.Forward => gameObject.transform.forward;
        float ICombatBehaviorRuntime.SensorRadius => Sensor.SensorRadius;
        float ICombatBehaviorRuntime.BattleRingRadius => BoundaryControlByGod._BattleRingRadius;
        float ICombatBehaviorRuntime.AnimationDuration => CommonSetting.CharacterAnimDuration[_DATA_CENTER.UnitConfig().TYPE];
        bool ICombatBehaviorRuntime.NearRing => _BasicPhysicSupport.NearRing;

        void ICombatBehaviorRuntime.HaltMotion(bool resetAnimatorSpeed) => HaltMotion(resetAnimatorSpeed);
        void ICombatBehaviorRuntime.StopVelocity() => BehaviorMotionUtility.StopVelocity(_Rigidbody);
        void ICombatBehaviorRuntime.SetRootMotion(bool enabled) => _Animator.applyRootMotion = enabled;
        void ICombatBehaviorRuntime.SetConstraints(RigidbodyConstraints constraints) => _Rigidbody.constraints = constraints;
        void ICombatBehaviorRuntime.SetRigidbodyInterpolation(RigidbodyInterpolation interpolation) => _BasicPhysicSupport.Rigidbody.interpolation = interpolation;
        void ICombatBehaviorRuntime.SetLinearDamping(float value) => _Rigidbody.linearDamping = value;
        void ICombatBehaviorRuntime.TriggerAnimation(string clipName, float duration) => AnimationManger.AnimationTrigger(clipName, duration);
        void ICombatBehaviorRuntime.TriggerAggressiveExpression() => AnimationManger.TriggerExpression(Facial.aggressive);
        void ICombatBehaviorRuntime.TriggerCasualFace() => AnimationManger.CasualFace();
        void ICombatBehaviorRuntime.TurnCancelOff() => _SkillCancelFlag.turn_off_flag();
        void ICombatBehaviorRuntime.TurnCancelOn() => _SkillCancelFlag.turn_on_flag();
        void ICombatBehaviorRuntime.TurnRotationAdjustmentStart() => _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
        void ICombatBehaviorRuntime.TurnRotationAdjustmentStartWithoutStepForward() => _SkillCancelFlag.TurnRotationAdjustmentStartFlagWithoutstepfoward(1);
        void ICombatBehaviorRuntime.OpenEnemyTouchingDrag(int mode) => _BasicPhysicSupport.OpenEnemyTouchingDrag(mode);
        void ICombatBehaviorRuntime.ClearTouchedEnemyBody() => _BasicPhysicSupport.hiddenMethods.ClearTouchedEnemyBody();
        void ICombatBehaviorRuntime.ClearMarkerManagers() => _Weapon_Animation_Events.ClearMarkerManagers();
        void ICombatBehaviorRuntime.ClosePersonalityEffects() => pEvents.CloseAllPersonalityEffects();
        void ICombatBehaviorRuntime.CloseEffectsOnBodyParts(bool includeBodyParts) => _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(includeBodyParts);
        void ICombatBehaviorRuntime.CloseOnProcessEnergyFromBodyWeapons() => _BO_Ani_E.CloseOnProcessEnergyFromBodyWeapons();
        void ICombatBehaviorRuntime.RecoverRootPositionChange() => _BasicPhysicSupport.hiddenMethods.RecoverRootPosChange();
        void ICombatBehaviorRuntime.CleanClear() => _DATA_CENTER.CleanClear();
        void ICombatBehaviorRuntime.SetUsingGravity(bool enabled) => _BasicPhysicSupport.SetUsingGravity(enabled);
        void ICombatBehaviorRuntime.RotateToTargetTween(Vector3 target, float duration) => RotateToTargetTween(target, duration);
        void ICombatBehaviorRuntime.RotateToTarget(Vector3 target, float turnSpeed, bool ignoreY) => RotateToTarget(target, turnSpeed, ignoreY);
        void ICombatBehaviorRuntime.Move(Vector3 relativePos, float acceleration, bool ignoreY) => Move(relativePos, acceleration, ignoreY);
        void ICombatBehaviorRuntime.AttackApproach(Vector3 target, float speed) => AttackApproach(target, speed);
        void ICombatBehaviorRuntime.PreventUnitOverlap() => PreventUnitOverlap();
        void ICombatBehaviorRuntime.RunStateSubCoroutine(object coroutine)
        {
            if (coroutine is CustomCoroutine customCoroutine)
            {
                _BuffsRunner.RunSubCoroutineOfState(customCoroutine);
            }
        }

        bool ICombatBehaviorRuntime.IsTouchingEnemy() => _BasicPhysicSupport.hiddenMethods.TouchingEnemy();
        bool ICombatBehaviorRuntime.IsGrounded() => _BasicPhysicSupport.hiddenMethods.Grounded;
        bool ICombatBehaviorRuntime.IsCurrentAnimationLooping() => AnimationManger._toUse.isLooping;
        bool ICombatBehaviorRuntime.AnimationCasualFinished() => AnimationCasualFinishedFlag();
        float ICombatBehaviorRuntime.DistanceToNearestEnemyXZ() => _BasicPhysicSupport.ToNearestEnemyXZ();
        bool ICombatBehaviorRuntime.IsAttackApproaching() => _SkillCancelFlag.hiddenMethods.GetAttackApproachingFlag();

        bool ICombatBehaviorRuntime.TryGetFirstEnemyPosition(bool includeDead, out Vector3 position)
        {
            var enemiesByDistance = Sensor.GetEnemiesByDistance(includeDead);
            if (enemiesByDistance.Count > 0 && enemiesByDistance[0] != null)
            {
                position = enemiesByDistance[0].transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        bool ICombatBehaviorRuntime.TryGetClosestEnemyColliderPosition(out Vector3 position)
        {
            var enemyCollider = Sensor.GetClosestEnemyColliderInSensorRange();
            if (enemyCollider != null)
            {
                position = enemyCollider.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        bool ICombatBehaviorRuntime.TryGetSuddenThreatPosition(float minDistance, float maxDistance, out Vector3 position)
        {
            var threat = Sensor.GetSuddenThreatInRange(minDistance, maxDistance);
            if (threat != null)
            {
                position = threat.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        bool ICombatBehaviorRuntime.TryGetLastDeadEnemyPosition(out Vector3 position)
        {
            var deadEnemy = Sensor.GetLastDeadEnemies();
            if (deadEnemy != null)
            {
                position = deadEnemy.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        public void EnergyAbsorb(CriticalGaugeMode gaugeMode, FightParamsReference victim)
        {
            if (!AbsorbEnergyFinished)
            {
                switch(spLevel)
                {
                    case 0:
                        temp = FightGlobalSetting._NormalSkillExGet;
                    break;
                    case 1:
                        temp = FightGlobalSetting._Sp1SkillExGet;
                    break;
                    case 2:
                        temp = FightGlobalSetting._Sp2SkillExGet;
                    break;
                    case 3:
                        temp = FightGlobalSetting._Sp3SkillExGet;
                    break;
                }
                
                victim.PlusEx(FightGlobalSetting._getHurtExGet);
                victim.PlusDreamGauge(FightGlobalSetting._NormalSkillExGet);
                switch(gaugeMode)
                {
                    case CriticalGaugeMode.DoubleGain:
                        temp *= 2;
                        break;
                    default:
                        break;
                }
                FightParamsRef.PlusEx(temp);
                FightParamsRef.PlusDreamGauge(FightGlobalSetting._NormalSkillExGet);
                AbsorbEnergyFinished = true;
            }
        }
        
        // Prepare for basic parameters here
        public virtual void Pre_process_before_enter()
        {
            this.gameObject = _DATA_CENTER.WholeT.gameObject;
            this.Sensor = _DATA_CENTER.Sensor;
            this.FightParamsRef = _DATA_CENTER.FightDataRef;
            this.shaderManager = _DATA_CENTER._ShaderManager;
            this._AIStateRunner = _DATA_CENTER._MyBehaviorRunner;
            this.AnimationManger = _DATA_CENTER.AnimationManger;
            this._SkillCancelFlag = _DATA_CENTER._SkillCancelFlag;
            this._BO_Ani_E = _DATA_CENTER._BO_Ani_E;
            this._Weapon_Animation_Events = _DATA_CENTER.bO_Weapon_Animation_Events;
            this._BasicPhysicSupport = _DATA_CENTER._BasicPhysicSupport;
            this._Animator = _BasicPhysicSupport.animator;
            this._Rigidbody = _BasicPhysicSupport.Rigidbody;
            this._ResistanceManager = _DATA_CENTER._ResistanceManager;
            this._BuffsRunner = _DATA_CENTER.buffsRunner;
            this.blendShapeProxy = _DATA_CENTER.blendShapeProxy;
            this.pEvents = _DATA_CENTER.Personality_events;
        }

        // On what condition can we exit this state 
        public virtual bool Capacity_Exit_Condition()
        {
            return true;
        }

        public virtual bool Strategic_exit_condition()
        {
            return CheckExitCondition(StateKey);
        }

        public virtual bool Capacity_enter_condition()
        {
            return FightParamsRef.HasPlentyGauge(spLevel);
        }

        // On what condition we have to enter this state
        public virtual bool Force_enter_condition()
        {
            return false;
        }

        // Process when entering the state 
        public virtual void AI_State_enter()
        {
            FightParamsRef.AT = Attack;
            FightParamsRef.CostCriticalGaugeBySPLevel(spLevel);
            BehaviorFrameCounter = 0;
            AbsorbEnergyFinished = false;
        }
        
        // Process when entering the state 
        public virtual void AI_State_enter(V_Damage newValue)
        {
            FightParamsRef.AT = Attack;
            FightParamsRef.CostCriticalGaugeBySPLevel(spLevel);
        }

        public virtual void C_State_enter()
        {
            AI_State_enter();
        }
        
        public virtual void C_State_enter(V_Damage newValue)
        {
            AI_State_enter(newValue);
        }

        // Process when exit the state 
        public virtual void AI_State_exit()
        {
            pEvents.CloseAllPersonalityEffects();
        }
        
        // Process when exit the state
        protected int BehaviorFrameCounter;
        public virtual void _State_Update()
        {
            BehaviorFrameCounter++;
        }

        // Local update of the state 
        public virtual void _State_FixedUpdate1()
        {
        }

        // Local update of the state 
        public virtual void _c_State_FixedUpdate1()
        {
            _State_FixedUpdate1();
        }

        // Local fixedupdate of the state 
        public virtual void _State_FixedUpdate2()
        {

        }

        // Local fixedupdate of the state 
        public virtual void _c_State_FixedUpdate2()
        {
            _State_FixedUpdate2();
        }
        
        #region state basic methods

        protected bool DetectApprovedEventAttack()
        {
            var boHealth = gameObject.GetComponent<FightParamsReference>();
            if (boHealth.ReturnApprovedEventAttackAttempts().Count > 0)
            {
                boHealth.SetManagingEventDamage(boHealth.ReturnApprovedEventAttackAttempts()[0]);
                boHealth.ReturnApprovedEventAttackAttempts().Clear();
                boHealth.GetManagingEventDamage().Position_set.run();
                return true;
            }
            else
            {
                return false;
            }
        }
        
        protected bool AnimationCasualFinishedFlag()
        {
            return _Animator.GetBool("in_transition") == false && _Animator.GetCurrentAnimatorStateInfo(1).normalizedTime >= 1f;
        }
        
        protected void EventAttackEnterProcess()
        {
            var BO_Health = gameObject.GetComponent<FightParamsReference>();
            if (BO_Health.GetManagingEventDamage() != null)
            {
                BO_Health.GetManagingEventDamage().Position_set.end();
            }
            BO_Health.SetManagingEventDamage(null);
        }

        protected bool TryGetPlanarDirection(ref Vector3 direction, bool ignoreY, out Vector3 planarDirection)
        {
            return BehaviorMotionUtility.TryGetPlanarDirection(direction, ignoreY, out planarDirection);
        }

        protected void HaltMotion(bool resetAnimatorSpeed = true)
        {
            BehaviorMotionUtility.HaltMotion(_Animator, _Rigidbody, resetAnimatorSpeed);
        }

        protected void ApplyMovementIntent(
            Vector3 direction,
            float moveSpeed,
            float rotateSpeed,
            float animatorSpeed = 10f,
            bool ignoreY = true,
            bool keepAnimatorOnIdle = false)
        {
            BehaviorMotionUtility.ApplyMovementIntent(
                _Animator,
                gameObject.transform,
                _Rigidbody,
                direction,
                moveSpeed,
                rotateSpeed,
                animatorSpeed,
                ignoreY,
                keepAnimatorOnIdle);
        }
        
        // Rotate to a target
        Vector3 _lookDir;
        Quaternion _dirQ;
        //返回带符号角度，用以判断往哪个方向摆头。
        protected float RotateToTarget(Vector3 target, float turnSpeed, bool ignoreY)
        {
            return BehaviorMotionUtility.RotateToTarget(gameObject.transform, _Rigidbody, target, turnSpeed, ignoreY);
        }
        
        public Tweener RotateToTargetTween(Vector3 target, float duration)
        {
            return _BasicPhysicSupport.RotateToTarget_Tween(target, duration);
        }

        //protected void RotateToDirection_Tween(Vector3 direction, float duration, bool ignoreY)
        //{
        //    if (ignoreY)
        //    {
        //        direction.y = 0;
        //    }
        //    _DATA_CENTER.WholeT.DORotate(direction, duration, RotateMode.Fast);
        //}

        // Rotate to a direction
        protected bool RotateToDirection(Vector3 direction, float turnSpeed, bool ignoreY)
        {
            return BehaviorMotionUtility.RotateToDirection(gameObject.transform, _Rigidbody, direction, turnSpeed, ignoreY);
        }

        // Move to direction
        //public float Move(Vector3 relativePos, float acceleration, bool ignoreY) {
        //    if (ignoreY)
        //        relativePos.y = 0;
        //    gameObject.GetComponent<Rigidbody>().AddForce(relativePos.normalized * acceleration * Time.deltaTime, ForceMode.VelocityChange);
        //    return gameObject.GetComponent<Rigidbody>().velocity.magnitude;
        //}

        protected void PreventUnitOverlap()
        {
            BehaviorMotionUtility.PreventUnitOverlap(
                _BasicPhysicSupport.hiddenMethods.TouchingEnemy(),
                _BasicPhysicSupport.hiddenMethods.Grounded,
                _BasicPhysicSupport.hiddenMethods.OverrideOnEnemyDrag,
                _BasicPhysicSupport.hiddenMethods.GetCenterOfTouchingEnemies(),
                _DATA_CENTER.WholeT,
                _Rigidbody);
        }

        Vector3 _v;
        protected float Move(Vector3 relativePos, float acceleration, bool ignoreY)
        {
            return BehaviorMotionUtility.Move(_Rigidbody, relativePos, acceleration, ignoreY);
        }

        float use_acc;
        public float Move_AddForce(Vector3 forcedirection, float acceleration, bool ignoreY)//废函数。
        {
            if (_Rigidbody == null)
                return 0f;
            if (ignoreY)
                forcedirection.y = 0;

            use_acc += (acceleration - _Rigidbody.linearVelocity.magnitude) * 2;
            _Rigidbody.AddForce(use_acc * forcedirection.normalized);
            return _Rigidbody.linearVelocity.magnitude;
        }

        void MoveByChangePosition(Vector3 relativePos, float acceleration, bool ignoreY)
        {
            if (ignoreY)
            {
                relativePos.y = 0;
            }
            _v = relativePos;
            gameObject.transform.position = Vector3.Lerp(gameObject.transform.position,
                                                         gameObject.transform.position + _v,
                                                         Time.deltaTime * acceleration);
        }

        protected Vector3 ClampPositionToBattleRing(Vector3 worldPos)
        {
            return BehaviorMotionUtility.ClampPositionToBattleRing(worldPos, BoundaryControlByGod._BattleRingRadius);
        }

        protected Vector3 CalcFixedPlanarMoveTarget(Vector3 startPos, Vector3 direction, float distance)
        {
            return BehaviorMotionUtility.CalcFixedPlanarMoveTarget(
                startPos,
                direction,
                distance,
                BoundaryControlByGod._BattleRingRadius);
        }

        protected Tweener StartFixedPlanarMoveTween(Transform mover, Rigidbody rigidbody, Vector3 targetPos, float duration)
        {
            if (mover == null || duration <= 0f)
            {
                return null;
            }

            targetPos = ClampPositionToBattleRing(targetPos);
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            return mover.DOMove(targetPos, duration).SetEase(Ease.Linear).OnUpdate(() =>
            {
                if (rigidbody == null)
                {
                    return;
                }

                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            });
        }

        // apply friction to rigidbody, and make sure it doesn't exceed its max speed
        public void ManageSpeed(Rigidbody rigidbody, float maxSpeed, bool ignoreY)
        {
            BehaviorMotionUtility.ManageSpeed(rigidbody, maxSpeed, ignoreY);
        }

        float current_speed;
        void ClampVelocity(float max_speed)
        {
            current_speed = _Rigidbody.linearVelocity.magnitude;
            if (current_speed > max_speed)
            {
                _v = (max_speed / current_speed) * _Rigidbody.linearVelocity;
                _Rigidbody.linearVelocity = _v;
            }
        }

        Vector3 dir;
        Quaternion slerp;
        void RotateToVelocity(float turnSpeed, bool ignoreY)
        {
            dir = _Rigidbody.linearVelocity;
            if (ignoreY)
                dir.y = 0;
            _dirQ = Quaternion.LookRotation(dir);
            _dirQ = Quaternion.Slerp(gameObject.transform.rotation, _dirQ, turnSpeed * Quaternion.Angle(_dirQ, gameObject.transform.rotation) * Time.fixedDeltaTime);
            _Rigidbody.MoveRotation(_dirQ);
        }

        // RotateToVelocity in reverse
        void RotateToVelocityNegative(float turnSpeed, bool ignoreY)
        {
            dir = ignoreY ? -new Vector3(this._Rigidbody.linearVelocity.x, 0f, this._Rigidbody.linearVelocity.z) : -this._Rigidbody.linearVelocity;

            if (dir.magnitude > 0.1)
            {
                _dirQ = Quaternion.LookRotation(dir);
                slerp = Quaternion.Slerp(gameObject.transform.rotation, _dirQ, dir.magnitude * turnSpeed * Time.deltaTime);
                _Rigidbody.MoveRotation(slerp);
            }
        }

        protected Vector3 CalFixPushVector(V_Damage damage, Vector3 victimTPos)
        {
            if (damage == null || damage.from_weapon == null)
                return Vector3.zero;

            var attackerPos = damage.attacker?.Center?.WholeT != null
                ? damage.attacker.Center.WholeT.position
                : victimTPos;
            var pushOrigin = damage.from_weapon.damage_type == DamageType.explosion
                ? damage.DamageEffectPoint
                : damage.impactComingPoint;

            if (damage.from_weapon.ShouldPreferAttackerLinePush() && damage.attacker?.Center?.WholeT != null)
            {
                pushOrigin = damage.attacker.Center.WholeT.position;
            }

            return CalFixPushVector(pushOrigin, attackerPos, victimTPos, damage.from_weapon.damage_type, damage.from_weapon._WeaponMode);
        }

        /// <summary>
        /// damageHappenPoint 是已经解析后的推移原点：
        /// 身体部位攻击传入攻击者位置，伤害物体攻击传入物体位置，爆炸传入爆点。
        /// </summary>
        protected Vector3 CalFixPushVector(Vector3 damageHappenPoint, Vector3 attackerTPos, Vector3 victimTPos, 
            DamageType damageType, WeaponMode weaponMode)
        {
            return BehaviorMotionUtility.ResolvePushDirection(
                damageHappenPoint,
                attackerTPos,
                victimTPos,
                gameObject.transform.forward,
                damageType);
        }

        Vector3 use_direction;
        protected void AttackApproach(Vector3 P, float speed)
        {
            if (_SkillCancelFlag.hiddenMethods.GetAttackApproachingFlag())
            {
                use_direction = P - gameObject.transform.position;
                use_direction.y = 0;
                Move(use_direction, speed, true);
                if (_BasicPhysicSupport.hiddenMethods.TouchingEnemy())
                {
                    _SkillCancelFlag.hiddenMethods.SetAttackApproachingFlag(false);
                }
            }
        }
        
        /// <summary>
        /// 获取某向量的垂直向量
        /// </summary>
        protected Vector3 GetVerticalDir(Vector3 _dir)
        {
            return BehaviorMotionUtility.GetVerticalDir(_dir);
        }
        
        #endregion
    }
}

//float ji;
//float lastFrameRotateAngle;
//float thisFrameRotateAngle;
//void SingleDirectionRotateProcess(Vector3 P, float speed)
//{
//    //底下这个是说，攻击状态里角色在一个1f周期里有0.3f时长会调整方向，但是在这0.3f时间段里，如果产生了旋转不定向(比如已经转到目标)，那么转向就会提前结束。
//    if (_SkillCancelFlag.hiddenMethods.GetRotationAdjustmentStartFlag())
//    {
//        thisFrameRotateAngle = this.RotateToTarget(P, 1f, true);
//        ji = thisFrameRotateAngle * lastFrameRotateAngle;
//        if (ji > 0)//同向
//        {
//            lastFrameRotateAngle = thisFrameRotateAngle;
//        }
//        else if (ji < 0)//反向
//        {
//            _SkillCancelFlag.TurnRotationAdjustmentStartFlag(0);
//        }
//        else //刚开始计
//        {
//            lastFrameRotateAngle = thisFrameRotateAngle;
//        }
//    }
//    else
//    {
//        lastFrameRotateAngle = 0;
//        thisFrameRotateAngle = 0;
//    }

//    if (_SkillCancelFlag.hiddenMethods.GetAttackApproachingFlag())
//    {
//        use_direction = P - gameObject.transform.position;
//        use_direction.y = 0;
//        Move(use_direction, speed, true);
//        if (_BasicPhysicSupport.hiddenMethods.ITouchedEnemyBody())
//        {
//            _SkillCancelFlag.hiddenMethods.SetAttackApproachingFlag(false);
//        }
//    }
//}
