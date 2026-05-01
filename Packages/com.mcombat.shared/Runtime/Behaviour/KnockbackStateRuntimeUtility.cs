using System;
using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public sealed class KnockbackFlightRuntimeState
    {
        public float TimeCounter;
        public Vector3 Direction;
        public bool TouchedBoundary;
        public bool CanWakeUp;
        public int FlyingStep;
        public AnimationCurve YCurve;
        public AnimationCurve ZCurve;
        public float CurveEndTime;
        public bool Active;
    }

    public readonly struct KnockbackFlightSettings
    {
        public readonly float BoundaryEffectOffset;
        public readonly float CanGetUpAfterGrounded;
        public readonly float MaxLaidGroundTime;
        public readonly float DeathGroundConfirmTime;

        public KnockbackFlightSettings(
            float boundaryEffectOffset,
            float canGetUpAfterGrounded,
            float maxLaidGroundTime,
            float deathGroundConfirmTime)
        {
            BoundaryEffectOffset = boundaryEffectOffset;
            CanGetUpAfterGrounded = canGetUpAfterGrounded;
            MaxLaidGroundTime = maxLaidGroundTime;
            DeathGroundConfirmTime = deathGroundConfirmTime;
        }
    }

    public static class KnockbackStateRuntimeUtility
    {
        public static void EnterKnockOff(ICombatBehaviorRuntime runtime)
        {
            runtime.SetGettingDamage(true);
            runtime.SetUsingGravity(false);
            runtime.OpenEnemyTouchingDrag(0);
            runtime.HaltMotion();
            runtime.SetRootMotion(false);
            runtime.ClearMarkerManagers();
            runtime.ClosePersonalityEffects();
            runtime.TriggerAnimationClip(runtime.GetRandomKnockOffAnimation(), 0.05f);
        }

        public static void ExitKnockOff(ICombatBehaviorRuntime runtime)
        {
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
            runtime.SetGettingDamage(false);
            runtime.TurnCancelOff();
            runtime.SetUsingGravity(true);
        }

        public static void EnterDeath(ICombatBehaviorRuntime runtime)
        {
            runtime.MarkDead();
            runtime.ChangeLayerForLimbs(14);
            runtime.SetGettingDamage(true);
            runtime.SetUsingGravity(false);
            runtime.OpenEnemyTouchingDrag(0);
            runtime.SetAnimatorSpeed(0f);
            runtime.SetRootMotion(false);
            runtime.ClearMarkerManagers();
            runtime.CloseOnProcessEnergyFromBodyWeapons();
            runtime.ResolveAllDecompositions();
            runtime.ClosePersonalityEffects();
            runtime.StopVelocity();
            runtime.TriggerAnimationClip(runtime.GetRandomKnockOffAnimation(), 0.05f);
            runtime.EnableAllLimbs(false);
        }

        public static void ExitDeath(ICombatBehaviorRuntime runtime)
        {
            runtime.EnableAllLimbs(true);
            ExitKnockOff(runtime);
        }

        public static void BeginFlight(
            KnockbackFlightRuntimeState state,
            Vector3 direction,
            AnimationCurve yCurve,
            AnimationCurve zCurve)
        {
            state.FlyingStep = 0;
            state.TimeCounter = 0f;
            state.CanWakeUp = false;
            state.TouchedBoundary = false;
            state.Direction = direction;
            state.YCurve = yCurve;
            state.ZCurve = zCurve;
            state.CurveEndTime = zCurve != null && zCurve.length > 0 ? zCurve.keys[zCurve.length - 1].time : 0f;
            state.Active = yCurve != null && zCurve != null;
        }

        public static void UpdateKnockOffFlight(
            KnockbackFlightRuntimeState state,
            ICombatBehaviorRuntime runtime,
            KnockbackFlightSettings settings,
            Action<Vector3, Quaternion> onWallHit,
            Action<Vector3, Quaternion> onGroundHit)
        {
            if (!state.Active)
            {
                return;
            }

            ResolveBoundaryHit(state, runtime, settings.BoundaryEffectOffset, false, onWallHit);

            switch (state.FlyingStep)
            {
                case 0:
                    var yDiffer = CurveDelta(state.YCurve, state.TimeCounter, Time.deltaTime);
                    runtime.AddPosition(state.Direction * CurveDelta(state.ZCurve, state.TimeCounter, Time.deltaTime) + Vector3.up * yDiffer);
                    if ((runtime.IsGrounded() && yDiffer < 0f) || state.TimeCounter >= state.CurveEndTime)
                    {
                        state.FlyingStep = 1;
                    }
                    break;
                case 1:
                    state.TimeCounter = 0f;
                    runtime.SetUsingGravity(true);
                    onGroundHit?.Invoke(Planar(runtime.Position), Quaternion.LookRotation(Vector3.right));
                    runtime.SetConstraints(RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY);
                    state.FlyingStep = 2;
                    break;
                case 2:
                    if (!state.CanWakeUp && state.TimeCounter > settings.CanGetUpAfterGrounded)
                    {
                        state.CanWakeUp = true;
                        runtime.TurnCancelOn();
                    }

                    if (state.TimeCounter > settings.MaxLaidGroundTime)
                    {
                        runtime.ChangeState("getUp");
                    }
                    break;
            }

            UpdateVelocityAfterFlightStep(state, runtime);
            state.TimeCounter += Time.deltaTime;
        }

        public static void UpdateDeathFlight(
            KnockbackFlightRuntimeState state,
            ICombatBehaviorRuntime runtime,
            KnockbackFlightSettings settings,
            Action<Vector3, Quaternion> onWallHit,
            Action<Vector3, Quaternion> onGroundHit)
        {
            if (!state.Active)
            {
                return;
            }

            ResolveBoundaryHit(state, runtime, settings.BoundaryEffectOffset, true, onWallHit);

            switch (state.FlyingStep)
            {
                case 0:
                    var yDiffer = CurveDelta(state.YCurve, state.TimeCounter, Time.deltaTime);
                    runtime.AddPosition(state.Direction * CurveDelta(state.ZCurve, state.TimeCounter, Time.deltaTime) + Vector3.up * yDiffer);
                    if (runtime.IsGrounded() && state.TimeCounter > settings.DeathGroundConfirmTime)
                    {
                        state.FlyingStep = 1;
                    }
                    break;
                case 1:
                    state.TimeCounter = 0f;
                    runtime.SetUsingGravity(true);
                    onGroundHit?.Invoke(Planar(runtime.Position), Quaternion.LookRotation(Vector3.right));
                    runtime.SetConstraints(RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation);
                    state.FlyingStep = 2;
                    break;
            }

            state.TimeCounter += Time.deltaTime;
        }

        static void ResolveBoundaryHit(
            KnockbackFlightRuntimeState state,
            ICombatBehaviorRuntime runtime,
            float boundaryEffectOffset,
            bool useGeometryCenter,
            Action<Vector3, Quaternion> onWallHit)
        {
            if (state.TouchedBoundary || !runtime.AtRing)
            {
                return;
            }

            state.TouchedBoundary = true;
            var referencePosition = useGeometryCenter ? runtime.GeometryCenterPosition : runtime.Position;
            state.Direction = Vector3.zero - referencePosition;
            state.Direction.y = 0f;
            state.Direction = state.Direction.normalized;

            var effectPosition = referencePosition.normalized * (runtime.BattleRingRadius + boundaryEffectOffset);
            effectPosition.y = useGeometryCenter ? referencePosition.y : runtime.Position.y;
            var lookDirection = Vector3.zero - referencePosition.normalized;
            lookDirection.y = 0f;
            if (effectPosition.y > 1f)
            {
                onWallHit?.Invoke(effectPosition, Quaternion.LookRotation(lookDirection, Vector3.up));
            }
        }

        static void UpdateVelocityAfterFlightStep(KnockbackFlightRuntimeState state, ICombatBehaviorRuntime runtime)
        {
            if (state.FlyingStep < 2)
            {
                runtime.StopVelocity();
                return;
            }

            runtime.SetPlanarVelocityOnly();
        }

        static Vector3 Planar(Vector3 position)
        {
            position.y = 0f;
            return position;
        }

        static float CurveDelta(AnimationCurve curve, float time, float deltaTime)
        {
            return curve == null ? 0f : curve.Evaluate(time + deltaTime) - curve.Evaluate(time);
        }
    }
}
