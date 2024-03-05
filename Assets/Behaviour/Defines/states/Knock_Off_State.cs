using Cysharp.Threading.Tasks;
using UnityEngine;
using HittingDetection;
using Skill;

namespace Soul
{
    public class Knock_Off_State : Behavior
    {
        float _timeCounter;
        Vector3 _xz;
        bool _touchedBoundary;
        bool _canWakeUp;
        AnimationCurve _usedYCurve;
        AnimationCurve _usedZCurve;
        float _temp;

        public Knock_Off_State()
        {
            StateType = BehaviorType.KnockOff;
        }
        
        public override void AI_State_enter(V_Damage value)
        {
            base.AI_State_enter();
            //FightParamsRef.ChangeLayerForLimbs(14);
            
            FlyingStep = 0;
            _timeCounter = 0;
            _touchedBoundary = false;
            FightParamsRef.GettingDamage = true;
            _BasicPhysicSupport.SetUsingGravity(false);
            _BasicPhysicSupport.OpenEnemyTouchingDrag(0);
            _Animator.SetFloat("speed", 0f);
            _Animator.applyRootMotion = false;
            _Weapon_Animation_Events.ClearMarkerManagers();
            pEvents.CloseAllPersonalityEffects();
            _Rigidbody.velocity = Vector3.zero;
            AnimationManger.AnimationTrigger(AnimationManger.GetRandomKnockOffAnim(), true, 0.05f);
            //_xz = newValue.attacker._Center.WholeT.forward;
            var position = gameObject.transform.position;
            _xz = CalFixPushVector(value.impactComingPoint,  value.attacker.Center.WholeT.position, position, 
                value.from_weapon.damage_type, value.from_weapon._WeaponMode);
            RotateToTargetTween(position - _xz, 0f);
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(true);
            EffectsManager.GenerateEffect("super_hit", FightGlobalSetting.EffectPathDefine(value.from_weapon.element), value.DamageEffectPoint, value.CutRotation, null).Forget();
            _usedYCurve = value.from_weapon.damage_type == DamageType.high ? FightGlobalSetting.HDamageYAnimationCurve : FightGlobalSetting.KnockOffYAnimationCurve;
            _usedZCurve = value.from_weapon.damage_type == DamageType.high ? FightGlobalSetting.HDamageZAnimationCurve : FightGlobalSetting.KnockOffZAnimationCurve;
        }

        public override bool Capacity_Exit_Condition()
        {
            return false;
        }
        
        public override void AI_State_exit()
        {
            base.AI_State_exit();
            //FightParamsRef.ChangeLayerForLimbs(_DATA_CENTER._TeamConfig.mylayer);
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            FightParamsRef.GettingDamage = false;
            _SkillCancelFlag.turn_off_flag();
            _BasicPhysicSupport.SetUsingGravity(true);
        }
        
        Vector3 _effectP, _quaV;
        public int FlyingStep;// 0 拔地 1 曲线 2 落地以及躺地昏迷
        public override void _State_Update()
        {
            if (!_touchedBoundary)
            {
                if (_BasicPhysicSupport.AtRing)
                {
                    _touchedBoundary = true;
                    _xz = Vector3.zero - gameObject.transform.position;
                    _xz.y = 0;
                    _xz = _xz.normalized;
                    _effectP = _DATA_CENTER.geometryCenter.position.normalized * (BoundaryControlByGod._BattleRingRadius+ 0.5f); // 这个1.5f是因为一些演出上的原因
                    _effectP.y = gameObject.transform.position.y;
                    _quaV = Vector3.zero - gameObject.transform.position.normalized;
                    _quaV.y = 0;
                    if (_effectP.y > 1)
                        EffectsManager.GenerateEffect(CommonSetting.WallCrackEffectCode, null, _effectP, Quaternion.LookRotation(_quaV, Vector3.up), null).Forget();
                }
            }
            
            switch (FlyingStep)
            {
                case 0:
                    var yDiffer = _usedYCurve.Evaluate(_timeCounter + Time.deltaTime) - _usedYCurve.Evaluate(_timeCounter);
                    gameObject.transform.position +=
                        _xz * (_usedZCurve.Evaluate(_timeCounter + Time.deltaTime) - _usedZCurve.Evaluate(_timeCounter)) + Vector3.up * yDiffer;
                    if (_BasicPhysicSupport.hiddenMethods.Grounded && yDiffer < 0)
                    {
                        FlyingStep = 1;
                    }
                    break;
                case 1:
                    _timeCounter = 0;
                    _BasicPhysicSupport.SetUsingGravity(true);
                    _effectP = gameObject.transform.position;
                    _effectP.y = 0;
                    EffectsManager.GenerateEffect(CommonSetting.HitGroundEffectCode, null, _effectP, Quaternion.LookRotation(Vector3.right), null).Forget();
                    _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                    FlyingStep = 2;
                    break;
                case 2:
                    if (!_canWakeUp)
                    {
                        if (_timeCounter > FightGlobalSetting._CanGetUpAfterKnockoffToGround)
                        {
                            _canWakeUp = true;
                            _SkillCancelFlag.turn_on_flag();
                        }
                    }
                    if (_timeCounter > FightGlobalSetting._MaxKnockoffLaidGroundTime)
                    {
                        _AIStateRunner.ChangeState("getUp");
                    }
                    break;
            }
            _Rigidbody.velocity = Vector3.zero; //如果没有这一行的话会出现个非常意外的问题，就是产生一个向上的固定velocity，超过了角色自身下坠，导致FlyingStep一直为0
            _timeCounter += Time.deltaTime;
        }
    }
}

// 原先的MultiplyPoint3x4击飞曲线计划相关
//if (!touchedBoundary)
//{
//    if (_DATA_CENTER.onBattleGroundBundary)
//    {
//        touchedBoundary = true;
//        temp = gameObject.transform.position;
//        temp.y = 0;
//        startquaternion = Quaternion.LookRotation(Vector3.zero - temp, Vector3.up);
//        temp = gameObject.transform.position;
//        Matrix = Matrix4x4.TRS(temp, startquaternion, Vector3.one * 1);
//        alreadyFinishedZTranslation = FightGlobalSetting._knockOffzAnimationCurve.Evaluate(time_counter);
//        alreadyFinishedYTranslation = gameObject.transform.position.y;
//    }
//}else{
//    touchedBoundary = _DATA_CENTER.onBattleGroundBundary;
//}
//gameObject.transform.position = Matrix.MultiplyPoint3x4(new Vector3(0, 
//FightGlobalSetting._knockOffyAnimationCurve.Evaluate( time_counter ) * 1f - alreadyFinishedYTranslation, 
//FightGlobalSetting._knockOffzAnimationCurve.Evaluate( time_counter ) * 1f - alreadyFinishedZTranslation));

// Knock_off_state should not be super_canceled to revive state,here is the reason:
// While in knock off state, the character may also be transited into knock off state again,
// during the transition of knock off animation to self,the turn on flag you put there is triggerd,
// The AIStateRunner will recognise it as a permision to enter the revive state,then what you will see
// is that revive state is somehow triggerd immmediately. 1107 -- haku

    //if (BS_Main_Health.returnEventDamageList() != null)
   //     {
            //if (BS_Main_Health.returnEventDamageList().Count > 0)
    //        {
                //if (BS_Main_Health.returnEventDamageList()[0].Position_set.Child == null)
    //            {
                //  BS_Main_Health.returnEventDamageList()[0].Position_set.Child = this.gameObject;
    //            }
                //if (BS_Main_Health.returnEventDamageList()[0].Position_set.Parent == null)
    //            {
                //  BS_Main_Health.returnEventDamageList()[0].Position_set.Parent = this.gameObject;
    //            }
                //BS_Main_Health.returnEventDamageList()[0].getAttackerHealthBody().eventAttackHitApprove(BS_Main_Health.returnEventDamageList()[0]);
                //BS_Main_Health.returnEventDamageList().Clear();
                //BS_Main_Health.returnDamageList(damageType.supper_damage).Clear();
                //BS_Main_Health.returnDamageList(damageType.heavy_damage).Clear();
                //BS_Main_Health.returnDamageList(damageType.light_damage).Clear();
                //BS_Main_Health.returnDamageList(damageType.knockOff_damage).Clear();
        //    }
        //}
        //if (BS_Main_Health.returnApprovedEventAttackAttempts() != null)
   //     {
            //BS_Main_Health.returnApprovedEventAttackAttempts().Clear();
        //}
