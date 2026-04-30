using UnityEngine;
using Skill;

namespace MCombat.Shared.Behaviour
{
    public enum CombatRotationAdjustment
    {
        None,
        StepForward,
        WithoutStepForward
    }

    public enum GeneralAttackPhase
    {
        NoRushState = 0,
        FarFromReach = 1,
        NeedToRush = 2,
        Reached = 3,
        ReachedFromBeginning = 4
    }

    public interface ICombatBehaviorRuntime
    {
        BehaviorType StateType { get; }
        Vector3 Position { get; }
        Vector3 Forward { get; }
        float SensorRadius { get; }
        float BattleRingRadius { get; }
        float AnimationDuration { get; }
        bool NearRing { get; }

        void HaltMotion(bool resetAnimatorSpeed = true);
        void StopVelocity();
        void SetRootMotion(bool enabled);
        void SetConstraints(RigidbodyConstraints constraints);
        void SetRigidbodyInterpolation(RigidbodyInterpolation interpolation);
        void SetLinearDamping(float value);
        void TriggerAnimation(string clipName, float duration);
        void TriggerAggressiveExpression();
        void TriggerCasualFace();
        void TurnCancelOff();
        void TurnCancelOn();
        void TurnRotationAdjustmentStart();
        void TurnRotationAdjustmentStartWithoutStepForward();
        void OpenEnemyTouchingDrag(int mode);
        void ClearTouchedEnemyBody();
        void ClearMarkerManagers();
        void ClosePersonalityEffects();
        void CloseEffectsOnBodyParts(bool includeBodyParts);
        void CloseOnProcessEnergyFromBodyWeapons();
        void RecoverRootPositionChange();
        void CleanClear();
        void SetUsingGravity(bool enabled);
        void RotateToTargetTween(Vector3 target, float duration);
        void RotateToTarget(Vector3 target, float turnSpeed, bool ignoreY);
        void Move(Vector3 relativePos, float acceleration, bool ignoreY);
        void AttackApproach(Vector3 target, float speed);
        void PreventUnitOverlap();
        void RunStateSubCoroutine(object coroutine);

        bool IsTouchingEnemy();
        bool IsGrounded();
        bool IsCurrentAnimationLooping();
        bool AnimationCasualFinished();
        float DistanceToNearestEnemyXZ();
        bool IsAttackApproaching();
        bool TryGetFirstEnemyPosition(bool includeDead, out Vector3 position);
        bool TryGetClosestEnemyColliderPosition(out Vector3 position);
        bool TryGetSuddenThreatPosition(float minDistance, float maxDistance, out Vector3 position);
        bool TryGetLastDeadEnemyPosition(out Vector3 position);
    }
}
