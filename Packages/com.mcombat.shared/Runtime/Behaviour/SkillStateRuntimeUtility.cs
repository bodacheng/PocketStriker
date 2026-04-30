using Skill;
using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public static class SkillStateRuntimeUtility
    {
        public static void EnterEmpty(ICombatBehaviorRuntime runtime)
        {
            runtime.CleanClear();
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePosition);
        }

        public static void ExitEmpty(ICombatBehaviorRuntime runtime)
        {
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
            runtime.SetUsingGravity(true);
        }

        public static void EnterMove(ICombatBehaviorRuntime runtime)
        {
            runtime.ClearMarkerManagers();
            runtime.TriggerAnimation(string.Empty, 0.1f);
            runtime.ClosePersonalityEffects();
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
            runtime.SetRigidbodyInterpolation(RigidbodyInterpolation.None);
            runtime.TriggerCasualFace();
        }

        public static void ExitMove(ICombatBehaviorRuntime runtime)
        {
            runtime.SetRigidbodyInterpolation(RigidbodyInterpolation.Extrapolate);
        }

        public static void EnterGetUp(ICombatBehaviorRuntime runtime, string clipName)
        {
            runtime.TurnCancelOn();
            runtime.HaltMotion();
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY);
            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
        }

        public static void ExitGetUp(ICombatBehaviorRuntime runtime)
        {
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
            runtime.TurnCancelOff();
        }

        public static void EnterIdle(
            ICombatBehaviorRuntime runtime,
            string clipName,
            float linearDamping)
        {
            runtime.HaltMotion();
            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
            runtime.SetLinearDamping(linearDamping);

            if (clipName == "victory")
            {
                if (runtime.TryGetLastDeadEnemyPosition(out var deadEnemyPosition))
                {
                    runtime.RotateToTargetTween(deadEnemyPosition, 0.01f);
                }
            }
            else
            {
                RotateToFirstEnemy(runtime, false, 0.01f);
            }
        }

        public static bool TryResetFinishedVictoryIdle(
            ICombatBehaviorRuntime runtime,
            string clipName,
            bool alreadyReset)
        {
            if (alreadyReset || clipName != "victory" || runtime.IsCurrentAnimationLooping() || !runtime.AnimationCasualFinished())
            {
                return alreadyReset;
            }

            runtime.TriggerAnimation(string.Empty, 0.05f);
            return true;
        }

        public static void ExitIdle(ICombatBehaviorRuntime runtime)
        {
            runtime.SetLinearDamping(0f);
        }

        public static void EnterAggressiveRootMotionAttack(
            ICombatBehaviorRuntime runtime,
            string clipName,
            CombatRotationAdjustment rotationAdjustment,
            bool openEnemyTouchingDrag)
        {
            if (openEnemyTouchingDrag)
            {
                runtime.OpenEnemyTouchingDrag(1);
            }

            runtime.HaltMotion();
            runtime.TriggerAggressiveExpression();
            runtime.TurnCancelOff();
            ApplyRotationAdjustment(runtime, rotationAdjustment);
            runtime.ClosePersonalityEffects();
            RotateToFirstEnemy(runtime, false, 0.01f);
            runtime.SetRootMotion(true);
            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
        }

        public static void ExitMovingAttack(
            ICombatBehaviorRuntime runtime,
            bool closeOnProcessEnergy,
            bool closeEffectsOnBodyParts)
        {
            if (closeOnProcessEnergy)
            {
                runtime.CloseOnProcessEnergyFromBodyWeapons();
            }

            runtime.OpenEnemyTouchingDrag(0);
            runtime.ClearTouchedEnemyBody();
            runtime.CloseEffectsOnBodyParts(closeEffectsOnBodyParts);
        }

        public static void EnterCounter(
            ICombatBehaviorRuntime runtime,
            string clipName)
        {
            runtime.TurnCancelOff();
            runtime.HaltMotion();
            runtime.TurnRotationAdjustmentStartWithoutStepForward();
            runtime.ClosePersonalityEffects();
            runtime.SetRootMotion(true);
            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
        }

        public static void EnterDashBack(
            ICombatBehaviorRuntime runtime,
            string clipName)
        {
            runtime.SetRootMotion(true);
            runtime.HaltMotion();
            runtime.TurnCancelOff();
            runtime.ClosePersonalityEffects();
            runtime.RotateToTargetTween(ResolveDashBackTarget(runtime), 0.01f);
            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
        }

        public static Vector3 ResolveDashBackTarget(ICombatBehaviorRuntime runtime)
        {
            if (runtime.NearRing)
            {
                var target = runtime.Position * 2f;
                target.y = 0f;
                return target;
            }

            if (runtime.TryGetSuddenThreatPosition(0f, 5f, out var threatPosition))
            {
                return threatPosition;
            }

            if (runtime.TryGetClosestEnemyColliderPosition(out var closestColliderPosition))
            {
                return closestColliderPosition;
            }

            if (runtime.TryGetFirstEnemyPosition(false, out var firstEnemyPosition))
            {
                return firstEnemyPosition;
            }

            return Vector3.zero;
        }

        public static void EnterEscapeCommon(ICombatBehaviorRuntime runtime, string clipName)
        {
            runtime.HaltMotion();
            runtime.TurnCancelOff();
            runtime.ClosePersonalityEffects();
            runtime.SetRootMotion(true);
            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
        }

        public static Vector3 ResolveAiEscapeDirection(ICombatBehaviorRuntime runtime)
        {
            var direction = Vector3.zero - runtime.Position;
            direction.y = 0f;
            if (direction.magnitude + 4f > runtime.BattleRingRadius)
            {
                return direction;
            }

            if (runtime.TryGetSuddenThreatPosition(0f, 5f, out var threatPosition))
            {
                var damagingWeaponComingDirection = runtime.Position - threatPosition;
                return Random.Range(0, 2) == 0
                    ? Quaternion.Euler(0f, -135f, 0f) * damagingWeaponComingDirection
                    : Quaternion.Euler(0f, 135f, 0f) * damagingWeaponComingDirection;
            }

            if (runtime.TryGetClosestEnemyColliderPosition(out var closestEnemyPosition))
            {
                direction = -runtime.Position + closestEnemyPosition;
            }

            return Random.Range(0, 2) == 0
                ? Quaternion.Euler(0f, -90f, 0f) * direction
                : Quaternion.Euler(0f, 90f, 0f) * direction;
        }

        public static Vector3 ResolveCameraRelativeDirection(
            Vector3 fallbackForward,
            float cameraYaw,
            float horizontal,
            float vertical,
            float deadZone = 0.001f)
        {
            if (Mathf.Abs(horizontal) < deadZone && Mathf.Abs(vertical) < deadZone)
            {
                return fallbackForward;
            }

            var screenMovementSpace = Quaternion.Euler(0f, cameraYaw, 0f);
            var screenMovementForward = screenMovementSpace * Vector3.forward;
            var screenMovementRight = screenMovementSpace * Vector3.right;
            return screenMovementForward * vertical + screenMovementRight * horizontal;
        }

        public static void EnterGeneralAttackStart(ICombatBehaviorRuntime runtime)
        {
            runtime.TriggerAggressiveExpression();
            runtime.OpenEnemyTouchingDrag(1);
            runtime.HaltMotion();
            runtime.TurnCancelOff();
            if (runtime.StateType == BehaviorType.GR)
            {
                runtime.TurnRotationAdjustmentStart();
            }

            if (runtime.StateType == BehaviorType.GI)
            {
                runtime.TurnRotationAdjustmentStartWithoutStepForward();
            }

            runtime.SetRootMotion(true);
        }

        public static GeneralAttackPhase ResolveGeneralAttackInitialPhase(
            bool hasEnemy,
            bool hasClosestCollider,
            float distanceToCollider,
            float sensorRadius)
        {
            if (!hasEnemy)
            {
                return GeneralAttackPhase.NoRushState;
            }

            if (!hasClosestCollider)
            {
                return GeneralAttackPhase.FarFromReach;
            }

            if (distanceToCollider < sensorRadius / 3f)
            {
                return GeneralAttackPhase.ReachedFromBeginning;
            }

            if (distanceToCollider < sensorRadius * 2f / 3f)
            {
                return GeneralAttackPhase.ReachedFromBeginning;
            }

            return GeneralAttackPhase.FarFromReach;
        }

        public static void OnGeneralAttackReached(ICombatBehaviorRuntime runtime, string clipName)
        {
            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
            runtime.TurnRotationAdjustmentStart();
            runtime.StopVelocity();
            RotateToFirstEnemy(runtime, false, 0.01f);
        }

        public static void RotateToFirstEnemy(ICombatBehaviorRuntime runtime, bool includeDead, float duration)
        {
            if (runtime.TryGetFirstEnemyPosition(includeDead, out var enemyPosition))
            {
                runtime.RotateToTargetTween(enemyPosition, duration);
            }
        }

        public static void ApplyRotationAdjustment(
            ICombatBehaviorRuntime runtime,
            CombatRotationAdjustment rotationAdjustment)
        {
            switch (rotationAdjustment)
            {
                case CombatRotationAdjustment.StepForward:
                    runtime.TurnRotationAdjustmentStart();
                    break;
                case CombatRotationAdjustment.WithoutStepForward:
                    runtime.TurnRotationAdjustmentStartWithoutStepForward();
                    break;
            }
        }
    }
}
