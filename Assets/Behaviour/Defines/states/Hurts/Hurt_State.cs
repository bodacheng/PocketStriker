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
        Tween _tween;
        
        void PlayHurtAnim(V_Damage newValue)
        {
            if (_AIStateRunner.GetLastState().StateKey == "KnockOff" && _BasicPhysicSupport.hiddenMethods.Grounded)
            {
                AnimationManger.AnimationTrigger(AnimationManger.GetRandomHurtAnim("lay"), true, hurtAnimDuration);
                return;
            }
            var point = newValue.DamageEffectPoint;
            point.y = 0;
            
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
            AnimationManger.AnimationTrigger(AnimationManger.GetRandomHurtAnim(hurtAnimKey), true, hurtAnimDuration);
            AnimationManger.TriggerExpression(Facial.hit);
            RotateToTargetTween(rotateToTarget, 0.1f);
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            _Rigidbody.mass = FightGlobalSetting.FighterRigidMass;
            _BasicPhysicSupport.OpenEnemyTouchingDrag(0);
            FightParamsRef.GettingDamage = false;
            if (_tween != null && _tween.active && _tween.IsPlaying())
                _tween.Kill();
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
            target = newValue;
            base.AI_State_enter();
            if (_AIStateRunner.GetLastState().StateKey == "KnockOff")
            {
                //var knockOffState = (Knock_Off_State)_AIStateRunner.GetLastState();
                //if (knockOffState.FlyingStep == 0 || knockOffState.FlyingStep == 1)
                    _AIStateRunner.ChangeState("KnockOff", target);
                return;
            }
            
            _Animator.applyRootMotion = false;
            PlayHurtAnim(newValue);
            FightParamsRef.GettingDamage = true;
            _Weapon_Animation_Events.ClearMarkerManagers();
            _BO_Ani_E.hiddenMethods.CloseEffectsOnBodyParts(true);
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
            
            switch (target.from_weapon.damage_type)
            {
                case DamageType.slight_damage_forward:
                    _usedDizzyTime = FightGlobalSetting.SlightHitLastingTime;
                    NormalStart(target);
                    break;
                case DamageType.light_damage_forward:
                    _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                    NormalStart(target);
                    break;
                case DamageType.pull_slight:
                    PushToMidStart(target, 0.01f, true, false);
                    break;
                case DamageType.stable_damage:
                    _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                    NormalStart(target);
                    break;
                case DamageType.stable_damage_forward:
                    _usedDizzyTime = FightGlobalSetting.LightHitLastingTime;
                    HeavyStart(target);
                    break;
                case DamageType.heavy_damage_forward:
                    _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
                    HeavyStart(target);
                    break;
                case DamageType.supper_damage_forward:
                    _usedDizzyTime = FightGlobalSetting.SuperHitLastingTime;
                    HeavyStart(target);
                    EffectsManager.GenerateEffect("electric_s_e", FightGlobalSetting.EffectPathDefine(newValue.from_weapon.element), newValue.DamageEffectPoint, newValue.CutRotation, _DATA_CENTER.geometryCenter).Forget();
                    break;
                case DamageType.draw:
                case DamageType.stable_draw:
                    DrawDamageStart(target);
                    break;
                case DamageType.explosion:
                    ExplosionDamageStart(target);
                    break;
                case DamageType.push_to_mid:
                    PushToMidStart(target, 10f, true);
                    break;
                case DamageType.push_to_mid_slight:
                    PushToMidStart(target, 4f, true);
                    break;
                case DamageType.same_height_to_mid:
                    PushToMidStart(target, 4f, false);
                    break;
                case DamageType.sekka:
                    SekkaStart(target.from_weapon.element);
                    break;
                case DamageType.time_pause:
                    TimePauseStart();
                    return;
                case DamageType.high:
                    // 20201008 修改。high攻击不外乎是直接让对手被击飞，那么击飞状态里确实有相应的一切。
                    _AIStateRunner.ChangeState("KnockOff", target);//HighDamgeStart(target);
                    return;
            }
            
            AnimationManger.TriggerExpression(Facial.hit);
        }

        public override void _State_FixedUpdate1()
        {
            TimeCounter += Time.fixedDeltaTime;
            switch (target.from_weapon.damage_type)
            {
                case DamageType.high:
                    HighDamageUpdate();
                    break;
                case DamageType.draw:
                case DamageType.stable_draw:
                    DrawDamageUpdate(target);
                    break;
            }
        }

        public override bool Capacity_Exit_Condition()
        {
            return TimeCounter > _usedDizzyTime && !_BuffsRunner.Freezing;
        }
    }
}