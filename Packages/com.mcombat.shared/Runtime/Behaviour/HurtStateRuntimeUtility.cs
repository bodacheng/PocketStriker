using HittingDetection;
using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public enum HurtReactionKind
    {
        None,
        Normal,
        Heavy,
        Draw,
        Explosion,
        PushToMid,
        Sekka,
        TimePause,
        KnockOff
    }

    public readonly struct HurtTimingSettings
    {
        public readonly float SlightHitLastingTime;
        public readonly float LightHitLastingTime;
        public readonly float HeavyHitLastingTime;
        public readonly float SuperHitLastingTime;

        public HurtTimingSettings(
            float slightHitLastingTime,
            float lightHitLastingTime,
            float heavyHitLastingTime,
            float superHitLastingTime)
        {
            SlightHitLastingTime = slightHitLastingTime;
            LightHitLastingTime = lightHitLastingTime;
            HeavyHitLastingTime = heavyHitLastingTime;
            SuperHitLastingTime = superHitLastingTime;
        }
    }

    public readonly struct HurtReaction
    {
        public readonly HurtReactionKind Kind;
        public readonly float DizzyTime;
        public readonly float PushDistance;
        public readonly bool ShouldShake;
        public readonly bool ShouldGenerateElementShockEffect;
        public readonly bool ReturnAfterStart;

        public HurtReaction(
            HurtReactionKind kind,
            float dizzyTime,
            float pushDistance,
            bool shouldShake,
            bool shouldGenerateElementShockEffect,
            bool returnAfterStart)
        {
            Kind = kind;
            DizzyTime = dizzyTime;
            PushDistance = pushDistance;
            ShouldShake = shouldShake;
            ShouldGenerateElementShockEffect = shouldGenerateElementShockEffect;
            ReturnAfterStart = returnAfterStart;
        }
    }

    public readonly struct HurtAnimationDecision
    {
        public readonly string HurtAnimationKey;
        public readonly Vector3 RotateTarget;
        public readonly bool UseLayAnimation;

        public HurtAnimationDecision(string hurtAnimationKey, Vector3 rotateTarget, bool useLayAnimation)
        {
            HurtAnimationKey = hurtAnimationKey;
            RotateTarget = rotateTarget;
            UseLayAnimation = useLayAnimation;
        }
    }

    public static class HurtStateRuntimeUtility
    {
        public static bool ShouldRedirectToKnockOffOnEnter(
            DamageType damageType,
            bool atRing,
            bool lastStateIsKnockOff,
            bool normalWeight)
        {
            return (damageType == DamageType.stable_draw && atRing)
                   || (lastStateIsKnockOff && normalWeight);
        }

        public static bool ShouldKnockOffByGauge(DamageType damageType, float gauge, float knockOffExtent)
        {
            return gauge >= knockOffExtent
                   && damageType != DamageType.stable_damage
                   && damageType != DamageType.stable_damage_forward
                   && damageType != DamageType.stable_draw;
        }

        public static HurtReaction ResolveReaction(
            DamageType damageType,
            bool heavyWeight,
            HurtTimingSettings timings)
        {
            if (heavyWeight)
            {
                if (damageType == DamageType.supper_damage_forward)
                {
                    return new HurtReaction(HurtReactionKind.Heavy, timings.SuperHitLastingTime, 0f, true, true, false);
                }

                return new HurtReaction(HurtReactionKind.Normal, timings.LightHitLastingTime, 0f, true, false, false);
            }

            switch (damageType)
            {
                case DamageType.slight_damage_forward:
                case DamageType.light_damage_forward:
                case DamageType.stable_damage:
                    return new HurtReaction(HurtReactionKind.Normal, timings.LightHitLastingTime, 0f, true, false, false);
                case DamageType.pull_slight:
                    return new HurtReaction(HurtReactionKind.PushToMid, timings.LightHitLastingTime, 1f, false, false, false);
                case DamageType.stable_damage_forward:
                    return new HurtReaction(HurtReactionKind.Heavy, timings.LightHitLastingTime, 0f, true, false, false);
                case DamageType.heavy_damage_forward:
                    return new HurtReaction(HurtReactionKind.Heavy, timings.HeavyHitLastingTime, 0f, true, false, false);
                case DamageType.supper_damage_forward:
                    return new HurtReaction(HurtReactionKind.Heavy, timings.SuperHitLastingTime, 0f, true, true, false);
                case DamageType.draw:
                case DamageType.stable_draw:
                    return new HurtReaction(HurtReactionKind.Draw, timings.HeavyHitLastingTime, 0f, false, false, false);
                case DamageType.explosion:
                    return new HurtReaction(HurtReactionKind.Explosion, timings.HeavyHitLastingTime, 0f, true, false, false);
                case DamageType.push_to_mid:
                    return new HurtReaction(HurtReactionKind.PushToMid, timings.HeavyHitLastingTime, 10f, false, false, false);
                case DamageType.push_to_mid_slight:
                    return new HurtReaction(HurtReactionKind.PushToMid, timings.LightHitLastingTime, 4f, false, false, false);
                case DamageType.same_height_to_mid:
                    return new HurtReaction(HurtReactionKind.PushToMid, timings.HeavyHitLastingTime, 4f, false, false, false);
                case DamageType.sekka:
                    return new HurtReaction(HurtReactionKind.Sekka, 0f, 0f, true, false, false);
                case DamageType.time_pause:
                    return new HurtReaction(HurtReactionKind.TimePause, 0f, 0f, true, false, true);
                case DamageType.high:
                    return new HurtReaction(HurtReactionKind.KnockOff, 0f, 0f, false, false, true);
                default:
                    return new HurtReaction(HurtReactionKind.None, 0f, 0f, false, false, false);
            }
        }

        public static void EnterHurtPreAnimation(ICombatBehaviorRuntime runtime)
        {
            runtime.CloseEffectsOnBodyParts(true);
            runtime.CloseOnProcessEnergyFromBodyWeapons();
        }

        public static HurtAnimationDecision ResolveHurtAnimation(
            bool lastStateIsKnockOff,
            bool grounded,
            Vector3 selfPosition,
            Vector3 attackerPosition,
            Vector3 damageEffectPoint,
            float closeDistance,
            float headY,
            float geometryCenterY)
        {
            if (lastStateIsKnockOff && grounded)
            {
                return new HurtAnimationDecision("lay", Vector3.zero, true);
            }

            var distanceToAttacker = Vector3.Distance(selfPosition, attackerPosition);
            var rotateTarget = distanceToAttacker <= closeDistance ? attackerPosition : damageEffectPoint;
            var hurtAnimKey = damageEffectPoint.y > headY + 0.1f
                ? "press"
                : damageEffectPoint.y > geometryCenterY ? "high" : "low";
            return new HurtAnimationDecision(hurtAnimKey, rotateTarget, false);
        }

        public static void EnterHurtAfterAnimation(ICombatBehaviorRuntime runtime)
        {
            runtime.SetGettingDamage(true);
            runtime.ClearMarkerManagers();
            runtime.ClosePersonalityEffects();
        }

        public static void ExitHurt(ICombatBehaviorRuntime runtime, float fighterRigidMass)
        {
            runtime.SetMass(fighterRigidMass);
            runtime.OpenEnemyTouchingDrag(0);
            runtime.SetGettingDamage(false);
            if (runtime.IsFreezing())
            {
                return;
            }

            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
            runtime.TriggerCasualFace();
        }

        public static void StartNormalHit(ICombatBehaviorRuntime runtime)
        {
            runtime.OpenEnemyTouchingDrag(1);
            if (runtime.IsGrounded())
            {
                runtime.SetConstraints(RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation);
            }
            else
            {
                runtime.StopVelocity();
            }
        }

        public static bool ShouldCompleteFixedDisplacement(float timeCounter, float fixingTime)
        {
            return timeCounter > fixingTime;
        }

        public static void CompleteHeavyDisplacement(ICombatBehaviorRuntime runtime)
        {
            if (runtime.IsGrounded())
            {
                runtime.SetConstraints(RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation);
            }
            else
            {
                runtime.StopVelocity();
            }
        }

        public static void CompleteExplosionDisplacement(ICombatBehaviorRuntime runtime)
        {
            runtime.SetConstraints(RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation);
        }

        public static Vector3 ResolvePushToMidTarget(
            Vector3 attackerGeometryCenter,
            Vector3 attackerForward,
            float originY,
            float distance,
            float battleRingRadius)
        {
            var target = attackerGeometryCenter + attackerForward * distance;
            var planar = target;
            planar.y = 0f;
            if (battleRingRadius > 0f && planar.magnitude > battleRingRadius)
            {
                target = planar.normalized * battleRingRadius;
            }

            target.y = originY < 0f ? 0f : originY;
            return BehaviorMotionUtility.ClampPositionToBattleRing(target, battleRingRadius);
        }

        public static void StartDrawDamage(ICombatBehaviorRuntime runtime, float fighterRigidMass)
        {
            runtime.SetMass(fighterRigidMass / 2f);
        }

        public static bool ShouldSkipDrawUpdate(float weaponHp, float currentHp)
        {
            return weaponHp > 0f && currentHp <= 0f;
        }
    }
}
