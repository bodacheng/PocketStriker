using Cysharp.Threading.Tasks;
using UnityEngine;
using HittingDetection;
using Skill;

namespace Soul
{
    //死亡状态下关于怎么将死亡角色从战场正式排除需要重新研究。详见Data_Center.FindTargetsByDistance（直接从游戏物体获取tag意外的浪费时间）
    public class Death_State : Behavior
    {
        float _timeCounter;
        Vector3 _xz;
        bool _touchedBoundary;
        AnimationCurve _usedYCurve;
        AnimationCurve _usedZCurve;
        float _temp;
        
        public Death_State()
        {
            StateType = BehaviorType.KnockOff;
        }
        
        public override void AI_State_enter(V_Damage newValue)
        {
            base.AI_State_enter();
            _DATA_CENTER.FightDataRef.IsDead.Value = true;
            FightParamsRef.ChangeLayerForLimbs(14);
            
            _flyingStep = 0;
            _timeCounter = 0;
            _touchedBoundary = false;
            FightParamsRef.GettingDamage = true;
            _BasicPhysicSupport.SetUsingGravity(false);
            _BasicPhysicSupport.OpenEnemyTouchingDrag(0);
            _Animator.SetFloat(Speed, 0f);
            _Animator.applyRootMotion = false;
            _Weapon_Animation_Events.ClearMarkerManagers();
            pEvents.CloseAllPersonalityEffects();
            _Rigidbody.velocity = Vector3.zero;
            AnimationManger.AnimationTrigger(AnimationManger.GetRandomKnockOffAnim(), true, 0.05f);
            //_xz = newValue.attacker._Center.WholeT.forward;
            _xz = CalFixPushVector(newValue.impactComingPoint,  newValue.attacker.Center.WholeT.position, gameObject.transform.position, 
                newValue.from_weapon.damage_type, newValue.from_weapon._WeaponMode);
            RotateToTargetTween(gameObject.transform.position - _xz, 0f);
            
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(true);
            EffectsManager.GenerateEffect("super_hit", FightGlobalSetting.EffectPathDefine(newValue.from_weapon.element), newValue.DamageEffectPoint, newValue.CutRotation, null).Forget();
            _usedYCurve = newValue.from_weapon.damage_type == DamageType.high ? FightGlobalSetting.HDamageYAnimationCurve : FightGlobalSetting.KnockOffYAnimationCurve;
            _usedZCurve = newValue.from_weapon.damage_type == DamageType.high ? FightGlobalSetting.HDamageZAnimationCurve : FightGlobalSetting.KnockOffZAnimationCurve;
            FightParamsRef.EnableAllLimbs(false);
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
            FightParamsRef.EnableAllLimbs(true);
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            FightParamsRef.GettingDamage = false;
            _SkillCancelFlag.turn_off_flag();
            _BasicPhysicSupport.SetUsingGravity(true);
        }
        
        Vector3 _effectP, _quaV;
        private int _flyingStep;// 0 拔地 1 曲线 2 落地以及躺地昏迷
        private static readonly int Speed = Animator.StringToHash("speed");

        public override void _State_Update()
        {
            if (!_touchedBoundary)
            {
                if (_BasicPhysicSupport.AtRing)
                {
                    _touchedBoundary = true;
                    var pos = _DATA_CENTER.geometryCenter.position;
                    _xz = Vector3.zero - pos;
                    _xz.y = 0;
                    _xz = _xz.normalized;
                    _effectP = pos.normalized * (BoundaryControlByGod._BattleRingRadius+ 0.5f); // 这个0.5f是因为一些演出上的原因
                    _effectP.y = pos.y;
                    _quaV = Vector3.zero - pos.normalized;
                    _quaV.y = 0;
                    if (_effectP.y > 1)
                        EffectsManager.GenerateEffect(CommonSetting.WallCrackEffectCode, null, _effectP, Quaternion.LookRotation(_quaV, Vector3.up), null).Forget();
                }
            }
            
            switch (_flyingStep)
            {
                case 0:
                    var yDiffer = _usedYCurve.Evaluate(_timeCounter + Time.deltaTime) -
                                  _usedYCurve.Evaluate(_timeCounter);
                    gameObject.transform.position +=
                        _xz * (_usedZCurve.Evaluate(_timeCounter + Time.deltaTime) - _usedZCurve.Evaluate(_timeCounter)) + Vector3.up * yDiffer;
                    if (_BasicPhysicSupport.hiddenMethods.Grounded && _timeCounter > 0.5f)
                        // time_counter > 0.5f 这个数字是为了确保角色真能飞起来。
                        // 否则很有可能因为动画本身等复杂缘故，刚飞起来就被判断落地
                    {
                        _flyingStep = 1;
                    }
                    break;
                case 1:
                    _timeCounter = 0;
                    _BasicPhysicSupport.SetUsingGravity(true);
                    _effectP = gameObject.transform.position;
                    _effectP.y = 0;
                    EffectsManager.GenerateEffect(CommonSetting.HitGroundEffectCode, null, _effectP, Quaternion.LookRotation(Vector3.right), null).Forget();
                    _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                    _flyingStep = 2;
                    break;
                case 2:
                    break;
            }
            _timeCounter += Time.deltaTime;
        }
    }
}