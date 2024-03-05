using UnityEngine;
using Skill;

//本状态是最复杂的一个攻击种类状态，牵扯到攻击前冲刺
// 在3月21日我们对这个状态进行了进一步改进，现在针对冲刺阶段本状态有以下机制
// 1.基于位置计算位移速度，如果该速度低于0.2f则判断为被什么阻挡，停止冲刺
// 2.在冲刺阶段临时将自身质量调整为0.1从而避免冲刺到敌人身边还不停的情况下对敌人进行推挤
// 3.冲刺阶段将锁定目标敌人调整方向
// 以以上改动为基础，角色在设置方面增加了以下注意点：
// AttackRangeMarker组件最好是细长的capsulecollider，并且可以依据角色的体型尽可能的贴身（细），从而被攻击时敌人会冲刺到尽可能近的位置，不至于一些短手技能打不到
// 并且留下了一些问题：必须重新权衡此类攻击的AI进入范围，对整个系统的距离分段也要重新衡量，以及本状态的进入冲刺距离也都要重新仔细考虑。

namespace Soul
{
    public class G_Attack_State : Behavior
    {
        readonly string _dashClipName;
        readonly int _skillEmergentLevel;
        readonly bool _isEventAttackLaunchState;
        readonly bool _isEventAttackEndState;
        readonly float _rushSpeed;
        readonly float _approachSpeed;
        readonly float _maxRushTime;
        float _rushTimeCounter;
        Phase _phase;
        UnityEngine.Events.UnityAction _rushStart;
        UnityEngine.Events.UnityAction _rushEnd;
        CustomCoroutine _rushCoroutine;
        
        enum Phase
        {
            noRushState = 0,
            farFromReach = 1,
            needToRush = 2,
            reached = 3,
            reachedFromBeginning = 4
        }

        #region Constructor
        public G_Attack_State(string dashClipName, float rushSpeed, float maxRushTime, float approachingSpeed, string clipName)
        {
            this._rushSpeed = rushSpeed;
            this._maxRushTime = maxRushTime;
            _approachSpeed = approachingSpeed;
            this.clip_name = clipName;
            this._dashClipName = dashClipName;
        }

        public G_Attack_State(string dashClipName, float rushSpeed, float maxRushTime, string clipName, bool EventLauncher_Or_Ender)
        {
            this._maxRushTime = maxRushTime;
            this._dashClipName = dashClipName;
            this._rushSpeed = rushSpeed;
            this.clip_name = clipName;
            _isEventAttackLaunchState = EventLauncher_Or_Ender;
            _isEventAttackEndState = !EventLauncher_Or_Ender;
        }
        #endregion

        #region Capacity Enter Exit
        public override bool Capacity_Exit_Condition()
        {
            return AnimationCasualFinishedFlag() && this.AnimationManger._toUse.name == clip_name;
        }

        #endregion

        public override void Pre_process_before_enter()
        {
            base.Pre_process_before_enter();
            _rushStart = () =>
            {
                FightParamsRef.Resistance.Value += 1;
            };
            _rushEnd = () =>
            {
                FightParamsRef.Resistance.Value -= 1;
            };
            _rushCoroutine = new CustomCoroutine(_rushStart, 5f, _rushEnd);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            _BasicPhysicSupport.hiddenMethods.ClearTouchedEnemyBody();
            _BasicPhysicSupport.OpenEnemyTouchingDrag(0);
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _Weapon_Animation_Events.ClearMarkerManagers();
            pEvents.CloseAllPersonalityEffects();
            _BuffsRunner.EndSubCoroutineOfState(_rushCoroutine);//冲刺阶段有可能没有正常结束就被强制离开当前技能状态
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(true);
            if (_isEventAttackLaunchState)
            {
                if (FightParamsRef != null)
                {
                    FightParamsRef.ReturnApprovedEventAttackAttempts().Clear();
                }
            }
            if (_isEventAttackEndState)
                EventAttackEnterProcess();
        }

        Collider collider;
        public override void AI_State_enter()
        {
            base.AI_State_enter();
            collider = null;
            AnimationManger.TriggerExpression(Facial.aggressive);
            _Animator.SetFloat("speed", 0f);
            _BasicPhysicSupport.OpenEnemyTouchingDrag(1);
            _SkillCancelFlag.turn_off_flag();
            if (StateType == BehaviorType.GR)
                _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
            if (StateType == BehaviorType.GI)
                _SkillCancelFlag.TurnRotationAdjustmentStartFlagWithoutstepfoward(1);
            _Rigidbody.velocity = Vector3.zero;
            _rushTimeCounter = 0f;
            _Animator.applyRootMotion = true;
            Sensor.DetectionStart(2, true);
            Sensor.GetEnemiesByDistance(true);
            if (Sensor.GetEnemiesByDistance(false).Count == 0)
            {
                //一般来说下面这些情况不跑？
                _phase = Phase.noRushState;
                AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
                return;
            }

            collider = Sensor.GetClosestEnemyColliderInSensorRange();
            if (collider == null)
            {
                AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
                _phase = Phase.farFromReach;
                return;
            }
            float distance = Vector3.Distance(gameObject.transform.position, collider.transform.position);
            if (distance < Sensor.SensorRadius / 3)//内环检测结果
            {
                _phase = Phase.reachedFromBeginning;
                AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
                if (Sensor.GetEnemiesByDistance(false).Count > 0)
                {
                    if (Sensor.GetEnemiesByDistance(false)[0] != null)
                    {
                        RotateToTargetTween(Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
                    }
                }
                return;
            }

            if (distance < Sensor.SensorRadius * 2 / 3)
            {
                if (Sensor.GetEnemiesByDistance(false).Count > 0)
                {
                    if (Sensor.GetEnemiesByDistance(false)[0] != null)
                    {
                        RotateToTargetTween(Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
                    }
                }
                //也就是说能不能可不可能发生冲刺，完全取决于上一个状态了。如果我们想完全关闭这个功能，那确保所有状态nextAttackStateCanRushFirst是fale就行
                // if (_AIStateRunner.GetLastState().nextAttackCanRushFirst && StateType == BehaviorType.GR)
                // {
                //     _phase = Phase.needToRush;
                //     if (AnimationManger.TryAnimationClip(_dashClipName) != null)
                //         AnimationManger.AnimationTrigger(_dashClipName, true, CommonSetting.CharacterAnimDuration);
                //     else
                //     {
                //         AnimationManger.PlayLayerAnim(null, true, 0f);
                //     }
                //     _BuffsRunner.RunSubCoroutineOfState(_rushCoroutine);
                // }
                // else
                {
                    _phase = Phase.reachedFromBeginning;//这个环节最绕脑子，大概指的是如果外环也有敌人，就当“已经到达”。但其实从出发点将，一般的普通近距离攻击在中距离下也不会触发才对
                    AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
                    return;
                }
            }

            AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
            _phase = Phase.farFromReach;
            return;
        }

        public override void _State_FixedUpdate1()
        {
            switch (_phase)
            {
                case Phase.noRushState://这里面可能还有一些远距离攻击什么的。哦。。。除非都没敌人了现在才可能会进入noRushState
                    break;
                case Phase.farFromReach:
                    break;
                case Phase.needToRush://也就是说冲刺中。
                    if (collider == null)
                    {
                        _Rigidbody.velocity = Vector3.zero;
                        _phase = Phase.reached;
                    }
                    else
                    {
                        Move(collider.transform.position - gameObject.transform.position, _rushSpeed, true);
                        if (Vector3.Distance(gameObject.transform.position, collider.transform.position) < 2f)
                        {
                            _phase = Phase.reached;
                        }
                        if (_phase == Phase.reached)
                        {
                            AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
                            _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
                            _Rigidbody.velocity = Vector3.zero;
                            Sensor.GetEnemiesByDistance(true);
                            _BuffsRunner.EndSubCoroutineOfState(_rushCoroutine);
                            if (Sensor.GetEnemiesByDistance(false).Count > 0)
                            {
                                if (Sensor.GetEnemiesByDistance(false)[0] != null)
                                {
                                    RotateToTargetTween(Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
                                }
                            }
                        }
                    }
                    if (_rushTimeCounter > _maxRushTime)
                    {
                        _phase = Phase.reached;
                    }
                    if (_phase == Phase.reached)
                    {
                        AnimationManger.AnimationTrigger(clip_name, true, CommonSetting.CharacterAnimDuration);
                        _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
                        _Rigidbody.velocity = Vector3.zero;
                        Sensor.DetectionStart(5, false);
                        _BuffsRunner.EndSubCoroutineOfState(_rushCoroutine);
                    }
                    break;
                case Phase.reached:
                    if (Sensor.GetEnemiesByDistance(false).Count > 0)
                    {
                        if (Sensor.GetEnemiesByDistance(false)[0] != null)
                            AttackApproach(Sensor.GetEnemiesByDistance(false)[0].transform.position, _approachSpeed);
                    }
                    break;
                case Phase.reachedFromBeginning://reachedFromThebeginning现在其实是两种情况：1. 冲刺状态一开始内环就有敌人 2.非冲刺状态一开始外环有敌人
                    if (Sensor.GetEnemiesByDistance(false).Count > 0)
                    {
                        if (Sensor.GetEnemiesByDistance(false)[0] != null)
                        {
                            AttackApproach(Sensor.GetEnemiesByDistance(false)[0].transform.position, _approachSpeed);
                        }
                    }
                    break;
            }

            if (_BasicPhysicSupport.hiddenMethods.TouchingEnemy() && _BasicPhysicSupport.hiddenMethods.Grounded)
            {
                if (_BasicPhysicSupport.ToNearestEnemyXZ() >= FightGlobalSetting.ToEnemyNearestDis)
                {
                    _Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                }
            }
            else
            {
                _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }
            
            //if (isEventAttackLaunchState)
            //{
            //    this.DetectApprovedEventAttack();
            //}
        }
    }
}