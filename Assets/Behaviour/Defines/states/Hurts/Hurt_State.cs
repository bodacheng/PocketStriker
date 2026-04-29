using Cysharp.Threading.Tasks;
using UnityEngine;
using HittingDetection;
using UniRx;
using DG.Tweening;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        float _usedDizzyTime;
        float TimeCounter { set; get; }
        V_Damage target;
        SingleAssignmentDisposable _physicMissionDisposable;
        private float hurtAnimDuration = 0.05f;
        private Sequence mySequence;
        private Tweener _fixedDisplacementTweener;

        #region Shake
        private Tweener _shakeTweener;
        private readonly float duration   = 0.3f;
        private readonly float magnitude  = 0.6f;   // 位移幅度（米）
        private readonly int   vibrato    = 80;       // 抖动次数
        private readonly float randomness = 20f;     // 抖动方向离散度
        #endregion

        void Shake()
        {
            _shakeTweener = _DATA_CENTER.WholeT.DOPunchPosition(-_DATA_CENTER.WholeT.forward * magnitude, duration,
                vibrato, randomness);
        }

        void PlayHurtAnim(V_Damage newValue)
        {
            if (_AIStateRunner.GetLastState().StateKey == "KnockOff" && _BasicPhysicSupport.hiddenMethods.Grounded)
            {
                AnimationManger.AnimationTrigger(AnimationManger.GetRandomHurtAnim("lay"), hurtAnimDuration);
                return;
            }

            string hurtAnimKey;
            var meToAttacker = Vector3.Distance(_DATA_CENTER.WholeT.position, newValue.attacker.Center.WholeT.position);
            var rotateToTarget = meToAttacker <= FightGlobalSetting._closeDis
                ? newValue.attacker.Center.WholeT.position : newValue.DamageEffectPoint;
            if (newValue.DamageEffectPoint.y > _DATA_CENTER.head_t.position.y + 0.1)
            {
                hurtAnimKey = "press";
            }
            else
            {
                hurtAnimKey = newValue.DamageEffectPoint.y > _DATA_CENTER.geometryCenter.position.y ? "high" : "low";
            }

            var obj = AnimationManger.GetRandomHurtAnim(hurtAnimKey);
            AnimationManger.AnimationTrigger(obj, hurtAnimDuration);
            AnimationManger.TriggerExpression(Facial.hit);
            mySequence = DOTween.Sequence();
            mySequence.Append(RotateToTargetTween(rotateToTarget, 0.1f));
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            _shakeTweener?.Kill();
            _Rigidbody.mass = FightGlobalSetting.FighterRigidMass;
            _BasicPhysicSupport.OpenEnemyTouchingDrag(0);
            FightParamsRef.GettingDamage = false;
            if (mySequence != null && mySequence.active && mySequence.IsPlaying())
                mySequence.Kill();
            _fixedDisplacementTweener?.Kill();
            _physicMissionDisposable?.Dispose();
            if (_BuffsRunner.Freezing)
            {
                return;
            }
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            AnimationManger.CasualFace();
        }

        public override void AI_State_enter(V_Damage newValue)
        {
            _shakeTweener?.Kill();

            if (_DATA_CENTER.TryChangeToSub(StateKey, newValue))
            {
                return;
            }

            target = newValue;
            base.AI_State_enter();
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(true);
            _BO_Ani_E.CloseOnProcessEnergyFromBodyWeapons();
            if ((newValue.from_weapon.damage_type == DamageType.stable_draw && _BasicPhysicSupport.AtRing) ||
                (_AIStateRunner.GetLastState().StateKey == "KnockOff" && _BasicPhysicSupport.Weight == Weight.normal))
            {
                //var knockOffState = (Knock_Off_State)_AIStateRunner.GetLastState();
                //if (knockOffState.FlyingStep == 0 || knockOffState.FlyingStep == 1)
                    _AIStateRunner.ChangeState("KnockOff", target);
                return;
            }

            //_Animator.applyRootMotion = false;
            PlayHurtAnim(newValue);
            FightParamsRef.GettingDamage = true;
            _Weapon_Animation_Events.ClearMarkerManagers();
            TimeCounter = 0f;
            pEvents.CloseAllPersonalityEffects();

            if (_BuffsRunner.Freezing)
                return;

            if (target.from_weapon.effectSpreadOnBody)
            {
                FightParamsRef.RunShaderChangeProcess(target.from_weapon.element, 0.1f);
            }

            FightParamsRef.GetKnockOffCount().PlusGauge(1f);
            FightParamsRef.GetKnockOffCount().PlusTimeCounter(0.2f);
            if (FightParamsRef.GetKnockOffCount().GetGauge() >= FightGlobalSetting.KnockOffExtent
            && target.from_weapon.damage_type != DamageType.stable_damage
            && target.from_weapon.damage_type != DamageType.stable_damage_forward
            && target.from_weapon.damage_type != DamageType.stable_draw)
            {
                FightParamsRef.GetKnockOffCount().SetGauge(0f);
                _AIStateRunner.ChangeState("KnockOff", target);
                return;
            }

            if (_BasicPhysicSupport.Weight == Weight.heavy)
            {
                _usedDizzyTime = FightGlobalSetting.SlightHitLastingTime;
                switch (target.from_weapon.damage_type)
                {
                    case DamageType.supper_damage_forward:
                        _usedDizzyTime = FightGlobalSetting.SuperHitLastingTime;
                        HeavyStart(target);
                        Shake();
                        EffectsManager.GenerateEffect("electric_s_e", FightGlobalSetting.EffectPathDefine(newValue.from_weapon.element), newValue.DamageEffectPoint, newValue.CutRotation, _DATA_CENTER.geometryCenter).Forget();
                        break;
                    default:
                        _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                        NormalStart(target);
                        Shake();
                        break;
                }
            }
            else
            {
                switch (target.from_weapon.damage_type)
                {
                    case DamageType.slight_damage_forward:
                        _usedDizzyTime = FightGlobalSetting.SlightHitLastingTime;
                        NormalStart(target);
                        Shake();
                        break;
                    case DamageType.light_damage_forward:
                        _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                        NormalStart(target);
                        Shake();
                        break;
                    case DamageType.pull_slight:
                        _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                        PushToMidStart(target, 1f);
                        break;
                    case DamageType.stable_damage:
                        _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                        NormalStart(target);
                        Shake();
                        break;
                    case DamageType.stable_damage_forward:
                        _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                        HeavyStart(target);
                        Shake();
                        break;
                    case DamageType.heavy_damage_forward:
                        _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
                        HeavyStart(target);
                        Shake();
                        break;
                    case DamageType.supper_damage_forward:
                        _usedDizzyTime = FightGlobalSetting.SuperHitLastingTime;
                        HeavyStart(target);
                        Shake();
                        EffectsManager.GenerateEffect("electric_s_e", FightGlobalSetting.EffectPathDefine(newValue.from_weapon.element), newValue.DamageEffectPoint, newValue.CutRotation, _DATA_CENTER.geometryCenter).Forget();
                        break;
                    case DamageType.draw:
                    case DamageType.stable_draw:
                        DrawDamageStart(target);
                        break;
                    case DamageType.explosion:
                        ExplosionDamageStart(target);
                        Shake();
                        break;
                    case DamageType.push_to_mid:
                        _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
                        PushToMidStart(target, 10f);
                        break;
                    case DamageType.push_to_mid_slight:
                        _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                        PushToMidStart(target, 4f);
                        break;
                    case DamageType.same_height_to_mid:
                        _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
                        PushToMidStart(target, 4f);
                        break;
                    case DamageType.sekka:
                        SekkaStart(target.from_weapon.element);
                        Shake();
                        break;
                    case DamageType.time_pause:
                        TimePauseStart();
                        Shake();
                        return;
                    case DamageType.high:
                        // 20201008 修改。high攻击不外乎是直接让对手被击飞，那么击飞状态里确实有相应的一切。
                        _AIStateRunner.ChangeState("KnockOff", target);//HighDamgeStart(target);
                        return;
                }
            }

            AnimationManger.TriggerExpression(Facial.hit);
        }

        public override void _State_FixedUpdate1()
        {
            TimeCounter += Time.fixedDeltaTime;
            if (_BasicPhysicSupport.Weight == Weight.normal)
            {
                switch (target.from_weapon.damage_type)
                {
                    case DamageType.high:
                        HighDamageUpdate();
                        break;
                    case DamageType.draw:
                    case DamageType.stable_draw:
                        if (target.from_weapon.CurrentHP == 0 && target.from_weapon.weaponHP > 0)
                            return;
                        DrawDamageUpdate(target);
                        break;
                }
            }
            PreventUnitOverlap();
        }

        public override bool Capacity_Exit_Condition()
        {
            return TimeCounter > _usedDizzyTime && !_BuffsRunner.Freezing;
        }
    }
}
