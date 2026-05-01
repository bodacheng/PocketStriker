using Cysharp.Threading.Tasks;
using DG.Tweening;
using HittingDetection;
using MCombat.Shared.Behaviour;
using UniRx;
using UnityEngine;

namespace Soul
{
    public class Hurt_State : Behavior
    {
        float _usedDizzyTime;
        float TimeCounter { set; get; }
        V_Damage target;
        SingleAssignmentDisposable _physicMissionDisposable;
        readonly float hurtAnimDuration = 0.05f;
        Sequence mySequence;
        Tweener _fixedDisplacementTweener;
        Tweener _shakeTweener;
        readonly float duration = 0.3f;
        readonly float magnitude = 0.6f;
        readonly int vibrato = 80;
        readonly float randomness = 20f;

        bool dropped;
        Vector3 _xz;
        readonly Color stone = new Color(0.3f, 0.3f, 0.3f);
        readonly Color freeze = new Color(0.1f, 0.1f, 0.8f);
        readonly Color gold = new Color(1f, 1f, 0.2f);
        UnityEngine.Events.UnityAction pasuestart;
        UnityEngine.Events.UnityAction pasueend;
        CustomCoroutine pasueCoroutine;

        void Shake()
        {
            _shakeTweener = _DATA_CENTER.WholeT.DOPunchPosition(-_DATA_CENTER.WholeT.forward * magnitude, duration,
                vibrato, randomness);
        }

        void PlayHurtAnim(V_Damage newValue)
        {
            var decision = HurtStateRuntimeUtility.ResolveHurtAnimation(
                _AIStateRunner.GetLastState().StateKey == "KnockOff",
                _BasicPhysicSupport.hiddenMethods.Grounded,
                _DATA_CENTER.WholeT.position,
                newValue.attacker.Center.WholeT.position,
                newValue.DamageEffectPoint,
                FightGlobalSetting._closeDis,
                _DATA_CENTER.head_t.position.y,
                _DATA_CENTER.geometryCenter.position.y);
            if (decision.UseLayAnimation)
            {
                AnimationManger.AnimationTrigger(AnimationManger.GetRandomHurtAnim("lay"), hurtAnimDuration);
                return;
            }

            var obj = AnimationManger.GetRandomHurtAnim(decision.HurtAnimationKey);
            AnimationManger.AnimationTrigger(obj, hurtAnimDuration);
            AnimationManger.TriggerExpression(Facial.hit);
            mySequence = DOTween.Sequence();
            mySequence.Append(RotateToTargetTween(decision.RotateTarget, 0.1f));
        }

        public override void AI_State_exit()
        {
            base.AI_State_exit();
            _shakeTweener?.Kill();
            if (mySequence != null && mySequence.active && mySequence.IsPlaying())
                mySequence.Kill();
            _fixedDisplacementTweener?.Kill();
            _physicMissionDisposable?.Dispose();
            HurtStateRuntimeUtility.ExitHurt(this, FightGlobalSetting.FighterRigidMass);
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
            HurtStateRuntimeUtility.EnterHurtPreAnimation(this);
            if (HurtStateRuntimeUtility.ShouldRedirectToKnockOffOnEnter(
                    newValue.from_weapon.damage_type,
                    _BasicPhysicSupport.AtRing,
                    _AIStateRunner.GetLastState().StateKey == "KnockOff",
                    _BasicPhysicSupport.Weight == Weight.normal))
            {
                _AIStateRunner.ChangeState("KnockOff", target);
                return;
            }

            PlayHurtAnim(newValue);
            HurtStateRuntimeUtility.EnterHurtAfterAnimation(this);
            TimeCounter = 0f;

            if (_BuffsRunner.Freezing)
                return;

            if (target.from_weapon.effectSpreadOnBody)
            {
                FightParamsRef.RunShaderChangeProcess(target.from_weapon.element, 0.1f);
            }

            FightParamsRef.GetKnockOffCount().PlusGauge(1f);
            FightParamsRef.GetKnockOffCount().PlusTimeCounter(0.2f);
            if (HurtStateRuntimeUtility.ShouldKnockOffByGauge(
                    target.from_weapon.damage_type,
                    FightParamsRef.GetKnockOffCount().GetGauge(),
                    FightGlobalSetting.KnockOffExtent))
            {
                FightParamsRef.GetKnockOffCount().SetGauge(0f);
                _AIStateRunner.ChangeState("KnockOff", target);
                return;
            }

            var reaction = HurtStateRuntimeUtility.ResolveReaction(
                target.from_weapon.damage_type,
                _BasicPhysicSupport.Weight == Weight.heavy,
                new HurtTimingSettings(
                    FightGlobalSetting.SlightHitLastingTime,
                    FightGlobalSetting.LightHitLastingTime,
                    FightGlobalSetting.HeavyHitLastingTime,
                    FightGlobalSetting.SuperHitLastingTime));
            if (RunReaction(reaction, newValue))
            {
                return;
            }

            AnimationManger.TriggerExpression(Facial.hit);
        }

        bool RunReaction(HurtReaction reaction, V_Damage newValue)
        {
            _usedDizzyTime = reaction.DizzyTime;
            switch (reaction.Kind)
            {
                case HurtReactionKind.Normal:
                    NormalStart(target);
                    break;
                case HurtReactionKind.Heavy:
                    HeavyStart(target);
                    break;
                case HurtReactionKind.Draw:
                    DrawDamageStart(target);
                    break;
                case HurtReactionKind.Explosion:
                    ExplosionDamageStart(target);
                    break;
                case HurtReactionKind.PushToMid:
                    PushToMidStart(target, reaction.PushDistance);
                    break;
                case HurtReactionKind.Sekka:
                    SekkaStart(target.from_weapon.element);
                    break;
                case HurtReactionKind.TimePause:
                    TimePauseStart();
                    break;
                case HurtReactionKind.KnockOff:
                    _AIStateRunner.ChangeState("KnockOff", target);
                    return true;
            }

            if (reaction.ShouldShake)
            {
                Shake();
            }

            if (reaction.ShouldGenerateElementShockEffect)
            {
                EffectsManager.GenerateEffect("electric_s_e", FightGlobalSetting.EffectPathDefine(newValue.from_weapon.element),
                    newValue.DamageEffectPoint, newValue.CutRotation, _DATA_CENTER.geometryCenter).Forget();
            }

            return reaction.ReturnAfterStart;
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

        void NormalStart(V_Damage newValue)
        {
            HurtStateRuntimeUtility.StartNormalHit(this);
        }

        void HeavyStart(V_Damage newValue)
        {
            var startPos = gameObject.transform.position;
            var pushDir = CalFixPushVector(newValue, startPos);
            var targetPos = CalcFixedPlanarMoveTarget(startPos, pushDir, FightGlobalSetting.NormalAttackPosFixingTime);
            _fixedDisplacementTweener?.Kill();
            _fixedDisplacementTweener = StartFixedPlanarMoveTween(_DATA_CENTER.WholeT, _Rigidbody, targetPos,
                FightGlobalSetting.NormalAttackPosFixingTime);

            _physicMissionDisposable = new SingleAssignmentDisposable();
            _physicMissionDisposable.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (HurtStateRuntimeUtility.ShouldCompleteFixedDisplacement(TimeCounter, FightGlobalSetting.NormalAttackPosFixingTime))
                    {
                        HurtStateRuntimeUtility.CompleteHeavyDisplacement(this);
                        _physicMissionDisposable.Dispose();
                    }
                }
            ).AddTo(gameObject);
        }

        void ExplosionDamageStart(V_Damage newValue)
        {
            _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
            _physicMissionDisposable = new SingleAssignmentDisposable();
            _physicMissionDisposable.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (HurtStateRuntimeUtility.ShouldCompleteFixedDisplacement(TimeCounter, FightGlobalSetting.NormalAttackPosFixingTime))
                    {
                        HurtStateRuntimeUtility.CompleteExplosionDisplacement(this);
                        _physicMissionDisposable.Dispose();
                    }
                }
            ).AddTo(gameObject);

            var startPos = gameObject.transform.position;
            var pushDir = CalFixPushVector(newValue, startPos);
            var targetPos = CalcFixedPlanarMoveTarget(startPos, pushDir, FightGlobalSetting.NormalAttackPosFixingTime);
            _fixedDisplacementTweener?.Kill();
            _fixedDisplacementTweener = StartFixedPlanarMoveTween(_DATA_CENTER.WholeT, _Rigidbody, targetPos,
                FightGlobalSetting.NormalAttackPosFixingTime);
        }

        void PushToMidStart(V_Damage newValue, float dis)
        {
            _BasicPhysicSupport.OpenEnemyTouchingDrag(1);
            var targetPos = HurtStateRuntimeUtility.ResolvePushToMidTarget(
                newValue.attacker.Center.geometryCenter.transform.position,
                newValue.attacker.Center.WholeT.transform.forward,
                _DATA_CENTER.WholeT.position.y,
                dis,
                BoundaryControlByGod._BattleRingRadius);
            mySequence.Append(StartFixedPlanarMoveTween(_DATA_CENTER.WholeT, _Rigidbody, targetPos, 0.3f));
        }

        void DrawDamageStart(V_Damage newValue)
        {
            HurtStateRuntimeUtility.StartDrawDamage(this, FightGlobalSetting.FighterRigidMass);
        }

        void DrawDamageUpdate(V_Damage newValue)
        {
            if (HurtStateRuntimeUtility.ShouldSkipDrawUpdate(newValue.from_weapon.weaponHP, newValue.from_weapon.CurrentHP))
                return;

            Vector3 Destination()
            {
                var vector3 = newValue.from_weapon_marker.transform.position;
                vector3.y = gameObject.transform.position.y;
                return vector3;
            }
            _Rigidbody.MovePosition(Destination());
        }

        void HighDamgeStart(V_Damage newValue)
        {
            dropped = false;
            HaltMotion();
            _usedDizzyTime = FightGlobalSetting.HighHitLastingTime;
            _xz = newValue.attacker.Center.WholeT.forward;
            FightParamsRef.GetKnockOffCount().PlusTimeCounter(0.2f);
            AnimationManger.AnimationTrigger(AnimationManger.GetRandomKnockOffAnim(), 0.1f);
        }

        void HighDamageUpdate()
        {
            if (dropped)
                return;

            if (TimeCounter > 0.1f && _BasicPhysicSupport.hiddenMethods.Grounded)
            {
                dropped = true;
                _Rigidbody.linearVelocity = Vector3.zero;
                return;
            }

            gameObject.transform.position +=
                _xz * (FightGlobalSetting.HDamageZAnimationCurve.Evaluate(TimeCounter + Time.fixedDeltaTime) -
                       FightGlobalSetting.HDamageZAnimationCurve.Evaluate(TimeCounter)) +
                Vector3.up * (FightGlobalSetting.HDamageYAnimationCurve.Evaluate(TimeCounter + Time.fixedDeltaTime) -
                              FightGlobalSetting.HDamageYAnimationCurve.Evaluate(TimeCounter));
        }

        void SekkaStart(Element element)
        {
            pasuestart = () =>
            {
                _BuffsRunner.Freezing = true;
                AnimationManger.AddSpeedBuff("sekka", 0);
                _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            };
            pasueend = () =>
            {
                AnimationManger.RemoveSpeedBuff("sekka");
                shaderManager.FlatColor(Color.white, 0);
                _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                _BuffsRunner.Freezing = false;
            };
            pasueCoroutine = new CustomCoroutine(
                pasuestart,
                FightGlobalSetting.SuperHitLastingTime * 2,
                () => this.target.from_weapon.damage_type != DamageType.sekka,
                pasueend);
            _BuffsRunner.RunSubCoroutineOfState(pasueCoroutine);

            switch (element)
            {
                case Element.blueMagic:
                case Element.lightMagic:
                    shaderManager.FlatColor(freeze, 0.5f);
                    break;
                default:
                    shaderManager.FlatColor(stone, 0.5f);
                    break;
            }
        }

        void TimePauseStart()
        {
            pasuestart = () =>
            {
                _BuffsRunner.Freezing = true;
                AnimationManger.AddSpeedBuff("pasue", 0);
                _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                shaderManager.FlatColor(gold, 0.5f);
            };
            pasueend = () =>
            {
                AnimationManger.RemoveSpeedBuff("pasue");
                shaderManager.FlatColor(Color.white, 0);
                _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                _BuffsRunner.Freezing = false;
            };
            pasueCoroutine = new CustomCoroutine(pasuestart, FightGlobalSetting.SuperHitLastingTime * 3, pasueend);
            _BuffsRunner.RunSubCoroutineOfState(pasueCoroutine);
        }
    }
}
