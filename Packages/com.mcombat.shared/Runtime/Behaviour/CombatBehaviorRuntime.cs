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
        bool AtRing { get; }
        Vector3 GeometryCenterPosition { get; }

        void HaltMotion(bool resetAnimatorSpeed = true);
        void StopVelocity();
        void SetPlanarVelocityOnly();
        void AddPosition(Vector3 delta);
        void SetMass(float value);
        void SetRootMotion(bool enabled);
        void SetAnimatorSpeed(float value);
        void SetConstraints(RigidbodyConstraints constraints);
        void SetRigidbodyInterpolation(RigidbodyInterpolation interpolation);
        void SetLinearDamping(float value);
        void TriggerAnimation(string clipName, float duration);
        void TriggerAnimationClip(AnimationClip clip, float duration);
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
        void SetGettingDamage(bool value);
        void SetResistanceValue(int value);
        void ClearResistance();
        void MarkDead();
        void ChangeLayerForLimbs(int layer);
        void ResolveAllDecompositions();
        void EnableAllLimbs(bool enabled);
        AnimationClip GetRandomKnockOffAnimation();
        void ChangeState(string stateKey);
        void RotateToTargetTween(Vector3 target, float duration);
        void RotateToTarget(Vector3 target, float turnSpeed, bool ignoreY);
        void Move(Vector3 relativePos, float acceleration, bool ignoreY);
        void AttackApproach(Vector3 target, float speed);
        void PreventUnitOverlap();
        void RunStateSubCoroutine(object coroutine);
        void EndStateSubCoroutine(object coroutine);
        void ClearApprovedEventAttackAttempts();
        void FinishManagingEventAttack();

        bool IsTouchingEnemy();
        bool IsGrounded();
        bool IsFreezing();
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
