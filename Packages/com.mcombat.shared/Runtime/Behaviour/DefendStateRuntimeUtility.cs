using HittingDetection;
using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public sealed class DefendRuntimeState
    {
        public float TimeCounter;
        public float UsedBlockLeastTime;
        public int DefendHp;
        public float LastExitTime;
        public bool Freezed;
    }

    public static class DefendStateRuntimeUtility
    {
        public const int DefaultDefendHp = 10;
        const float FreezeStartRatio = 0.8f;

        public static void ResetHp(DefendRuntimeState state, int maxHp = DefaultDefendHp)
        {
            state.DefendHp = maxHp;
        }

        public static bool CanEnter(DefendRuntimeState state, float now, float refreshTime, int maxHp = DefaultDefendHp)
        {
            if (now - state.LastExitTime > refreshTime)
            {
                state.DefendHp = maxHp;
            }

            return state.DefendHp > 0;
        }

        public static void EnterIdleBlock(
            ICombatBehaviorRuntime runtime,
            DefendRuntimeState state,
            string defendClipName,
            float lightBlockLastingTime)
        {
            state.Freezed = false;
            runtime.SetGettingDamage(false);
            runtime.SetResistanceValue(state.DefendHp > 0 ? 10 : 0);
            runtime.ClearMarkerManagers();
            runtime.HaltMotion();
            runtime.TriggerAnimation(defendClipName, runtime.AnimationDuration);
            state.UsedBlockLeastTime = lightBlockLastingTime;
            SetTimeCounter(runtime, state, defendClipName, lightBlockLastingTime);
            runtime.TurnCancelOff();
        }

        public static float EnterDamageBlock(
            ICombatBehaviorRuntime runtime,
            DefendRuntimeState state,
            string blockBreakClipName,
            string defendClipName,
            DamageType damageType,
            float lightBlockLastingTime,
            float heavyBlockLastingTime)
        {
            state.Freezed = false;
            runtime.SetGettingDamage(true);
            runtime.SetResistanceValue(state.DefendHp > 0 ? 10 : 0);
            runtime.ClearMarkerManagers();
            runtime.HaltMotion();
            runtime.TurnCancelOff();

            state.UsedBlockLeastTime = ResolveBlockDuration(damageType, lightBlockLastingTime, heavyBlockLastingTime);
            state.DefendHp -= 1;
            runtime.TriggerAnimation(blockBreakClipName, 0.05f);
            SetTimeCounter(runtime, state, defendClipName, state.UsedBlockLeastTime);
            return state.UsedBlockLeastTime;
        }

        public static void Exit(ICombatBehaviorRuntime runtime, DefendRuntimeState state, float now)
        {
            runtime.SetGettingDamage(false);
            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
            state.LastExitTime = now;
            runtime.ClearResistance();
        }

        public static void FixedUpdate(
            ICombatBehaviorRuntime runtime,
            DefendRuntimeState state,
            string defendClipName,
            float deltaTime)
        {
            runtime.SetResistanceValue(state.DefendHp > 0 ? 5 : 0);
            if (state.TimeCounter >= 0f)
            {
                SetTimeCounter(runtime, state, defendClipName, state.TimeCounter - deltaTime);
            }

            if (runtime.TryGetClosestEnemyColliderPosition(out var nearbyEnemyPosition))
            {
                runtime.RotateToTarget(nearbyEnemyPosition, 0.5f, true);
                return;
            }

            if (runtime.TryGetSuddenThreatPosition(0f, 5f, out var threatPosition))
            {
                runtime.RotateToTarget(threatPosition, 0.5f, true);
            }
        }

        public static float ResolveBlockDuration(
            DamageType damageType,
            float lightBlockLastingTime,
            float heavyBlockLastingTime)
        {
            switch (damageType)
            {
                case DamageType.heavy_damage_forward:
                case DamageType.supper_damage_forward:
                    return heavyBlockLastingTime;
                default:
                    return lightBlockLastingTime;
            }
        }

        public static float GetBlockPushDuration(DefendRuntimeState state, float pushMoveRatio)
        {
            return state.UsedBlockLeastTime * pushMoveRatio;
        }

        static void SetTimeCounter(
            ICombatBehaviorRuntime runtime,
            DefendRuntimeState state,
            string defendClipName,
            float value)
        {
            var wasNonNegative = state.TimeCounter >= 0f;
            state.TimeCounter = value;
            if (!state.Freezed && state.TimeCounter < state.UsedBlockLeastTime * FreezeStartRatio)
            {
                runtime.SetConstraints(RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation);
                state.Freezed = true;
            }

            if (wasNonNegative && state.TimeCounter < 0f)
            {
                runtime.TriggerAnimation(defendClipName, runtime.AnimationDuration);
            }
        }
    }
}
