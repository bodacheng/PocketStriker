using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public static class GeneralAttackStateRuntimeUtility
    {
        public static GeneralAttackPhase EnterGeneralAttack(
            ICombatBehaviorRuntime runtime,
            string clipName,
            bool hasEnemy,
            bool hasClosestCollider,
            float distanceToClosestCollider)
        {
            SkillStateRuntimeUtility.EnterGeneralAttackStart(runtime);

            if (!hasEnemy)
            {
                runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
                return GeneralAttackPhase.NoRushState;
            }

            if (!hasClosestCollider)
            {
                runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
                return GeneralAttackPhase.FarFromReach;
            }

            var phase = SkillStateRuntimeUtility.ResolveGeneralAttackInitialPhase(
                true,
                true,
                distanceToClosestCollider,
                runtime.SensorRadius);

            if (phase == GeneralAttackPhase.ReachedFromBeginning)
            {
                runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
                SkillStateRuntimeUtility.RotateToFirstEnemy(runtime, false, 0.01f);
                return phase;
            }

            runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
            return GeneralAttackPhase.FarFromReach;
        }

        public static void ExitGeneralAttack(
            ICombatBehaviorRuntime runtime,
            object rushCoroutine,
            bool isEventAttackLaunchState,
            bool isEventAttackEndState)
        {
            runtime.ClearTouchedEnemyBody();
            runtime.OpenEnemyTouchingDrag(0);
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
            runtime.ClearMarkerManagers();
            runtime.ClosePersonalityEffects();
            runtime.EndStateSubCoroutine(rushCoroutine);
            runtime.CloseEffectsOnBodyParts(true);

            if (isEventAttackLaunchState)
            {
                runtime.ClearApprovedEventAttackAttempts();
            }

            if (isEventAttackEndState)
            {
                runtime.FinishManagingEventAttack();
            }
        }

        public static GeneralAttackPhase UpdateGeneralAttack(
            ICombatBehaviorRuntime runtime,
            GeneralAttackPhase phase,
            string clipName,
            bool hasRushTarget,
            Vector3 rushTarget,
            float rushSpeed,
            float rushTimeCounter,
            float maxRushTime,
            float approachSpeed,
            object rushCoroutine,
            float touchingEnemyFreezeDistance)
        {
            switch (phase)
            {
                case GeneralAttackPhase.NeedToRush:
                    phase = UpdateRush(
                        runtime,
                        phase,
                        clipName,
                        hasRushTarget,
                        rushTarget,
                        rushSpeed,
                        rushTimeCounter,
                        maxRushTime,
                        rushCoroutine);
                    break;
                case GeneralAttackPhase.Reached:
                case GeneralAttackPhase.ReachedFromBeginning:
                    ApproachFirstEnemy(runtime, approachSpeed);
                    break;
            }

            BehaviorMotionUtility.UpdateTouchingEnemyConstraints(runtime, touchingEnemyFreezeDistance);
            runtime.PreventUnitOverlap();
            return phase;
        }

        static GeneralAttackPhase UpdateRush(
            ICombatBehaviorRuntime runtime,
            GeneralAttackPhase phase,
            string clipName,
            bool hasRushTarget,
            Vector3 rushTarget,
            float rushSpeed,
            float rushTimeCounter,
            float maxRushTime,
            object rushCoroutine)
        {
            if (!hasRushTarget)
            {
                runtime.StopVelocity();
                phase = GeneralAttackPhase.Reached;
            }
            else
            {
                runtime.Move(rushTarget - runtime.Position, rushSpeed, true);
                if (Vector3.Distance(runtime.Position, rushTarget) < 2f)
                {
                    phase = GeneralAttackPhase.Reached;
                }

                if (phase == GeneralAttackPhase.Reached)
                {
                    SkillStateRuntimeUtility.OnGeneralAttackReached(runtime, clipName);
                    runtime.EndStateSubCoroutine(rushCoroutine);
                }
            }

            if (rushTimeCounter > maxRushTime)
            {
                phase = GeneralAttackPhase.Reached;
            }

            if (phase == GeneralAttackPhase.Reached)
            {
                runtime.TriggerAnimation(clipName, runtime.AnimationDuration);
                runtime.TurnRotationAdjustmentStart();
                runtime.StopVelocity();
                runtime.EndStateSubCoroutine(rushCoroutine);
            }

            return phase;
        }

        static void ApproachFirstEnemy(ICombatBehaviorRuntime runtime, float approachSpeed)
        {
            if (runtime.TryGetFirstEnemyPosition(false, out var enemyPosition))
            {
                runtime.AttackApproach(enemyPosition, approachSpeed);
            }
        }
    }
}
