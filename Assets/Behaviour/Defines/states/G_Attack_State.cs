using UnityEngine;
using MCombat.Shared.Behaviour;
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
        GeneralAttackPhase _phase;
        UnityEngine.Events.UnityAction _rushStart;
        UnityEngine.Events.UnityAction _rushEnd;
        CustomCoroutine _rushCoroutine;

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
            SkillStateRuntimeUtility.EnterGeneralAttackStart(this);
            _rushTimeCounter = 0f;
            if (Sensor.GetEnemiesByDistance(false).Count == 0)
            {
                //一般来说下面这些情况不跑？
                _phase = GeneralAttackPhase.NoRushState;
                AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
                return;
            }

            collider = Sensor.GetClosestEnemyColliderInSensorRange();
            if (collider == null)
            {
                AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
                _phase = GeneralAttackPhase.FarFromReach;
                return;
            }
            float distance = Vector3.Distance(gameObject.transform.position, collider.transform.position);
            _phase = SkillStateRuntimeUtility.ResolveGeneralAttackInitialPhase(
                true,
                true,
                distance,
                Sensor.SensorRadius);

            if (_phase == GeneralAttackPhase.ReachedFromBeginning && distance < Sensor.SensorRadius / 3)//内环检测结果
            {
                AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
                if (Sensor.GetEnemiesByDistance(false).Count > 0)
                {
                    if (Sensor.GetEnemiesByDistance(false)[0] != null)
                    {
                        RotateToTargetTween(Sensor.GetEnemiesByDistance(false)[0].transform.position, 0.01f);
                    }
                }
                return;
            }

            if (_phase == GeneralAttackPhase.ReachedFromBeginning)
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
                    //这个环节最绕脑子，大概指的是如果外环也有敌人，就当“已经到达”。但其实从出发点将，一般的普通近距离攻击在中距离下也不会触发才对
                    AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
                    return;
                }
            }

            AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
            _phase = GeneralAttackPhase.FarFromReach;
            return;
        }

        public override void _State_FixedUpdate1()
        {
            switch (_phase)
            {
                case GeneralAttackPhase.NoRushState://这里面可能还有一些远距离攻击什么的。哦。。。除非都没敌人了现在才可能会进入noRushState
                    break;
                case GeneralAttackPhase.FarFromReach:
                    break;
                case GeneralAttackPhase.NeedToRush://也就是说冲刺中。
                    if (collider == null)
                    {
                        _Rigidbody.linearVelocity = Vector3.zero;
                        _phase = GeneralAttackPhase.Reached;
                    }
                    else
                    {
                        Move(collider.transform.position - gameObject.transform.position, _rushSpeed, true);
                        if (Vector3.Distance(gameObject.transform.position, collider.transform.position) < 2f)
                        {
                            _phase = GeneralAttackPhase.Reached;
                        }
                        if (_phase == GeneralAttackPhase.Reached)
                        {
                            AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
                            _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
                            _Rigidbody.linearVelocity = Vector3.zero;
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
                        _phase = GeneralAttackPhase.Reached;
                    }
                    if (_phase == GeneralAttackPhase.Reached)
                    {
                        AnimationManger.AnimationTrigger(clip_name, CommonSetting.CharacterAnimDuration[this._DATA_CENTER.UnitConfig().TYPE]);
                        _SkillCancelFlag.TurnRotationAdjustmentStartFlag(1);
                        _Rigidbody.linearVelocity = Vector3.zero;
                        _BuffsRunner.EndSubCoroutineOfState(_rushCoroutine);
                    }
                    break;
                case GeneralAttackPhase.Reached:
                    if (Sensor.GetEnemiesByDistance(false).Count > 0)
                    {
                        if (Sensor.GetEnemiesByDistance(false)[0] != null)
                            AttackApproach(Sensor.GetEnemiesByDistance(false)[0].transform.position, _approachSpeed);
                    }
                    break;
                case GeneralAttackPhase.ReachedFromBeginning://reachedFromThebeginning现在其实是两种情况：1. 冲刺状态一开始内环就有敌人 2.非冲刺状态一开始外环有敌人
                    if (Sensor.GetEnemiesByDistance(false).Count > 0)
                    {
                        if (Sensor.GetEnemiesByDistance(false)[0] != null)
                        {
                            AttackApproach(Sensor.GetEnemiesByDistance(false)[0].transform.position, _approachSpeed);
                        }
                    }
                    break;
            }

            BehaviorMotionUtility.UpdateTouchingEnemyConstraints(this, FightGlobalSetting.ToEnemyNearestDis);

            PreventUnitOverlap();

            //if (isEventAttackLaunchState)
            //{
            //    this.DetectApprovedEventAttack();
            //}
        }
    }
}
