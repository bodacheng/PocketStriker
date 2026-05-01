using System;

namespace MCombat.Shared.Behaviour
{
    public struct MoveBodyAttackRuntimeState
    {
        public int DamageCount;
        public bool HasCausedDamage;
    }

    public static class MovingAttackStateRuntimeUtility
    {
        public static void EnterMovingAttack(ICombatBehaviorRuntime runtime, string clipName)
        {
            SkillStateRuntimeUtility.EnterAggressiveRootMotionAttack(
                runtime,
                clipName,
                CombatRotationAdjustment.StepForward,
                true);
        }

        public static void ExitMovingAttack(ICombatBehaviorRuntime runtime, bool closeOnProcessEnergy)
        {
            SkillStateRuntimeUtility.ExitMovingAttack(runtime, closeOnProcessEnergy, false);
        }

        public static bool ShouldExitMovingAttack(ICombatBehaviorRuntime runtime)
        {
            return runtime.AnimationCasualFinished();
        }

        public static void UpdateMovingAttack(ICombatBehaviorRuntime runtime, bool preventUnitOverlap)
        {
            runtime.RecoverRootPositionChange();
            if (preventUnitOverlap)
            {
                PreventUnitOverlap(runtime);
            }
        }

        public static void PreventUnitOverlap(ICombatBehaviorRuntime runtime)
        {
            runtime.PreventUnitOverlap();
        }

        public static MoveBodyAttackRuntimeState EnterMoveBodyAttack(
            ICombatBehaviorRuntime runtime,
            string clipName,
            int currentDamageCount)
        {
            EnterMovingAttack(runtime, clipName);
            return new MoveBodyAttackRuntimeState
            {
                DamageCount = currentDamageCount,
                HasCausedDamage = false
            };
        }

        public static void UpdateMoveBodyAttackDamage(
            ICombatBehaviorRuntime runtime,
            ref MoveBodyAttackRuntimeState state,
            int damageCount)
        {
            if (damageCount > state.DamageCount)
            {
                state.HasCausedDamage = true;
                runtime.TurnCancelOn();
            }

            state.DamageCount = damageCount;
        }

        public static void ExitMoveBodyAttack(
            ICombatBehaviorRuntime runtime,
            ref MoveBodyAttackRuntimeState state,
            IDisposable damageSubscription)
        {
            state.HasCausedDamage = false;
            ExitMovingAttack(runtime, true);
            damageSubscription?.Dispose();
        }

        public static bool ShouldExitMoveBodyAttack(
            ICombatBehaviorRuntime runtime,
            MoveBodyAttackRuntimeState state)
        {
            return runtime.AnimationCasualFinished() || state.HasCausedDamage;
        }

        public static void EnterCounter(ICombatBehaviorRuntime runtime, string clipName)
        {
            SkillStateRuntimeUtility.EnterCounter(runtime, clipName);
        }

        public static void UpdateCounter(ICombatBehaviorRuntime runtime, float touchingEnemyFreezeDistance)
        {
            BehaviorMotionUtility.UpdateTouchingEnemyConstraints(runtime, touchingEnemyFreezeDistance);
        }
    }
}
