using HittingDetection;
using UnityEngine;
using Skill;

namespace Soul
{
    public abstract partial class Behavior
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
            Sensor.DetectionStart(-1, false);
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
        
        // Rotate to a target
        Vector3 _lookDir;
        Quaternion _dirQ;
        //返回带符号角度，用以判断往哪个方向摆头。
        protected float RotateToTarget(Vector3 target, float turnSpeed, bool ignoreY)
        {
            _lookDir = target - gameObject.transform.position;
            if (ignoreY)
            {
                _lookDir.y = 0;
            }
            _dirQ = Quaternion.LookRotation(_lookDir);
            _dirQ = Quaternion.Slerp(gameObject.transform.rotation, _dirQ, turnSpeed * Quaternion.Angle(_dirQ, gameObject.transform.rotation) * Time.fixedDeltaTime);
            _Rigidbody.MoveRotation(_dirQ);
            //gameObject.transform.rotation = Quaternion.Slerp(gameObject.transform.rotation, dirQ, turnSpeed * Quaternion.Angle(dirQ, gameObject.transform.rotation) * Time.fixedDeltaTime);
            return Vector3.SignedAngle(_Rigidbody.transform.forward, _lookDir, Vector3.up);
        }
        
        public void RotateToTargetTween(Vector3 target, float duration)
        {
            _BasicPhysicSupport.RotateToTarget_Tween(target, duration);
        }

        //protected void RotateToDirection_Tween(Vector3 direction, float duration, bool ignoreY)
        //{
        //    if (ignoreY)
        //    {
        //        direction.y = 0;
        //    }
        //    _DATA_CENTER.WholeT.DORotate(direction, duration, RotateMode.Fast);
        //}

        float _angle;
        // Rotate to a direction
        protected bool RotateToDirection(Vector3 direction, float turnSpeed, bool ignoreY)
        {
            if (ignoreY)
            {
                direction.y = 0;
            }
            _dirQ = Quaternion.LookRotation(direction);
            _angle = Quaternion.Angle(_dirQ, gameObject.transform.rotation);
            _dirQ = Quaternion.Slerp(gameObject.transform.rotation, _dirQ, _angle*(360-_angle)/(180*180/turnSpeed) * Time.fixedDeltaTime);
            _Rigidbody.MoveRotation(_dirQ);
            return Mathf.Approximately(Quaternion.Angle(_dirQ, gameObject.transform.rotation), 0f);
        }

        // Move to direction
        //public float Move(Vector3 relativePos, float acceleration, bool ignoreY) {
        //    if (ignoreY)
        //        relativePos.y = 0;
        //    gameObject.GetComponent<Rigidbody>().AddForce(relativePos.normalized * acceleration * Time.deltaTime, ForceMode.VelocityChange);
        //    return gameObject.GetComponent<Rigidbody>().velocity.magnitude;
        //}

        Vector3 _v;
        protected float Move(Vector3 relativePos, float acceleration, bool ignoreY)
        {
            if (_Rigidbody == null)
                return 0;
            if (ignoreY)
            {
                relativePos.y = 0;
            }
            _v = relativePos.normalized * acceleration;
            //_Rigidbody.AddForce(v, ForceMode.VelocityChange);
            _Rigidbody.velocity = _v;
            return _Rigidbody.velocity.magnitude;
        }

        float use_acc;
        public float Move_AddForce(Vector3 forcedirection, float acceleration, bool ignoreY)//废函数。
        {
            if (_Rigidbody == null)
                return 0f;
            if (ignoreY)
                forcedirection.y = 0;

            use_acc += (acceleration - _Rigidbody.velocity.magnitude) * 2;
            _Rigidbody.AddForce(use_acc * forcedirection.normalized);
            return _Rigidbody.velocity.magnitude;
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

        // apply friction to rigidbody, and make sure it doesn't exceed its max speed
        public void ManageSpeed(Rigidbody rigidbody, float maxSpeed, bool ignoreY)
        {
            if (rigidbody == null)
                return;
            Vector3 currentSpeed = rigidbody.velocity;
            if (ignoreY)
            {
                currentSpeed.y = 0;
            }
            if (currentSpeed.magnitude > maxSpeed)
            {
                rigidbody.AddForce((currentSpeed.magnitude / maxSpeed * -1) * currentSpeed.normalized * maxSpeed * Time.deltaTime, ForceMode.VelocityChange);
            }
        }

        float current_speed;
        void ClampVelocity(float max_speed)
        {
            current_speed = _Rigidbody.velocity.magnitude;
            if (current_speed > max_speed)
            {
                _v = (max_speed / current_speed) * _Rigidbody.velocity;
                _Rigidbody.velocity = _v;
            }
        }

        Vector3 dir;
        Quaternion slerp;
        void RotateToVelocity(float turnSpeed, bool ignoreY)
        {
            dir = _Rigidbody.velocity;
            if (ignoreY)
                dir.y = 0;
            _dirQ = Quaternion.LookRotation(dir);
            _dirQ = Quaternion.Slerp(gameObject.transform.rotation, _dirQ, turnSpeed * Quaternion.Angle(_dirQ, gameObject.transform.rotation) * Time.fixedDeltaTime);
            _Rigidbody.MoveRotation(_dirQ);
        }

        // RotateToVelocity in reverse
        void RotateToVelocityNegative(float turnSpeed, bool ignoreY)
        {
            dir = ignoreY ? -new Vector3(this._Rigidbody.velocity.x, 0f, this._Rigidbody.velocity.z) : -this._Rigidbody.velocity;

            if (dir.magnitude > 0.1)
            {
                _dirQ = Quaternion.LookRotation(dir);
                slerp = Quaternion.Slerp(gameObject.transform.rotation, _dirQ, dir.magnitude * turnSpeed * Time.deltaTime);
                _Rigidbody.MoveRotation(slerp);
            }
        }

        /// <summary>
        /// Get fixed Pos destination
        /// damageHappenPoint 伤害发生点
        /// attackerTransform_foward 攻击方“前方”的单位向量
        /// victimT_pos 受害者点
        /// _DamageType 攻击种类
        /// </summary>
        ///
        // 点积的计算方式为:  a·b=|a|·|b|cos<a,b>  其中|a|和|b|表示向量的模，<a,b>表示两个向量的夹角。另外在 点积 中，<a,b>和<b,a> 夹角是不分顺序的。 
        // 所以通过点积，我们其实是可以计算两个向量的夹角的。 
        // 另外通过点积的计算我们可以简单粗略的判断当前物体是否朝向另外一个物体: 只需要计算当前物体的transform.forward向量与 (otherObj.transform.position – transform.position)的点积即可， 大于0则面对，否则则背对着。当然这个计算也会有一点误差，但大致够用。 
        float f_temp;
        Vector3 v_temp;
        protected Vector3 CalFixPushVector(Vector3 damageHappenPoint, Vector3 attackerTPos, Vector3 victimTPos, 
            DamageType damageType, WeaponMode weaponMode)
        {
            f_temp = Vector3.Distance(attackerTPos, victimTPos);
            if (weaponMode == WeaponMode.EnergyFromBodyWeapon || f_temp < FightGlobalSetting._SureToPushForwardDis)
            {
                v_temp = victimTPos - attackerTPos;
                v_temp.y = 0;
                if (v_temp.normalized != Vector3.zero)
                    return v_temp.normalized;
            }
            
            if (damageType == DamageType.explosion)
            {
                v_temp = victimTPos - damageHappenPoint;
                v_temp.y = 0;
                return v_temp.normalized;
            }
            
            damageHappenPoint.y = 0;
            f_temp = Vector3.Dot(damageHappenPoint - attackerTPos, attackerTPos - victimTPos);
            if (f_temp > 0 && Vector3.Distance(attackerTPos, victimTPos) < FightGlobalSetting._attackDrawingDistance)
            {
                v_temp = f_temp * (attackerTPos - victimTPos);//+ (touchingEnemyBody ? attackerTransform_foward : Vector3.zero);
                return v_temp.normalized;
            }
            return CalFixPushVector(damageHappenPoint, attackerTPos, victimTPos, DamageType.explosion, weaponMode);
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
            //（_dir.x,_dir.z）与（？，1）垂直，则_dir.x * ？ + _dir.z * 1 = 0
            return Mathf.Approximately(_dir.z, 0) ? new Vector3(0, 0, -1) : new Vector3(-_dir.z / _dir.x, 0, 1).normalized;
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