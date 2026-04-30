using HittingDetection;
using Skill;
using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public readonly struct MoveStateSettings
    {
        public readonly float Speed;
        public readonly float TimeLimit;

        public MoveStateSettings(float speed, float timeLimit)
        {
            Speed = speed;
            TimeLimit = timeLimit;
        }
    }

    public static class BehaviorMotionUtility
    {
        public const float MovementEpsilon = 0.0001f;

        public static MoveStateSettings ResolveMoveStateSettings(MoveType moveType, float fighterMoveSpeed)
        {
            switch (moveType)
            {
                case MoveType.slow:
                    return new MoveStateSettings(fighterMoveSpeed / 2f, 3f);
                case MoveType.fast:
                    return new MoveStateSettings(fighterMoveSpeed * 2f, 1f);
                default:
                    return new MoveStateSettings(fighterMoveSpeed, 1f);
            }
        }

        public static bool TryGetPlanarDirection(Vector3 direction, bool ignoreY, out Vector3 planarDirection)
        {
            if (ignoreY)
            {
                direction.y = 0f;
            }

            var sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude < MovementEpsilon)
            {
                planarDirection = Vector3.zero;
                return false;
            }

            planarDirection = direction / Mathf.Sqrt(sqrMagnitude);
            return true;
        }

        public static void HaltMotion(Animator animator, Rigidbody rigidbody, bool resetAnimatorSpeed = true)
        {
            if (resetAnimatorSpeed && animator != null)
            {
                animator.SetFloat("speed", 0f);
            }

            StopVelocity(rigidbody);
        }

        public static void StopVelocity(Rigidbody rigidbody)
        {
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
            }
        }

        public static void ApplyMovementIntent(
            Animator animator,
            Transform mover,
            Rigidbody rigidbody,
            Vector3 direction,
            float moveSpeed,
            float rotateSpeed,
            float animatorSpeed = 10f,
            bool ignoreY = true,
            bool keepAnimatorOnIdle = false)
        {
            if (!TryGetPlanarDirection(direction, ignoreY, out var planarDirection))
            {
                HaltMotion(animator, rigidbody, !keepAnimatorOnIdle);
                return;
            }

            if (animator != null)
            {
                animator.SetFloat("speed", animatorSpeed);
            }

            Move(rigidbody, planarDirection, moveSpeed, false);
            RotateToDirection(mover, rigidbody, planarDirection, rotateSpeed, false);
        }

        public static float Move(Rigidbody rigidbody, Vector3 relativePos, float acceleration, bool ignoreY)
        {
            if (rigidbody == null)
            {
                return 0f;
            }

            if (ignoreY)
            {
                relativePos.y = 0f;
            }

            var velocity = relativePos.normalized * acceleration;
            rigidbody.linearVelocity = velocity;
            return rigidbody.linearVelocity.magnitude;
        }

        public static float RotateToTarget(Transform mover, Rigidbody rigidbody, Vector3 target, float turnSpeed, bool ignoreY)
        {
            if (mover == null || rigidbody == null)
            {
                return 0f;
            }

            var lookDir = target - mover.position;
            if (ignoreY)
            {
                lookDir.y = 0f;
            }

            if (lookDir.sqrMagnitude < MovementEpsilon)
            {
                return 0f;
            }

            var targetRotation = Quaternion.LookRotation(lookDir);
            var nextRotation = Quaternion.Slerp(
                mover.rotation,
                targetRotation,
                turnSpeed * Quaternion.Angle(targetRotation, mover.rotation) * Time.fixedDeltaTime);
            rigidbody.MoveRotation(nextRotation);
            return Vector3.SignedAngle(rigidbody.transform.forward, lookDir, Vector3.up);
        }

        public static bool RotateToDirection(Transform mover, Rigidbody rigidbody, Vector3 direction, float turnSpeed, bool ignoreY)
        {
            if (mover == null || rigidbody == null)
            {
                return false;
            }

            if (ignoreY)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < MovementEpsilon)
            {
                return false;
            }

            var normalizedDirection = direction.normalized;
            var targetRotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);
            var angle = Quaternion.Angle(mover.rotation, targetRotation);
            if (angle < 0.1f)
            {
                return true;
            }

            var speed = Mathf.Max(turnSpeed, 0f);
            var step = Mathf.Clamp01((speed + angle) * Time.fixedDeltaTime);
            var nextRotation = Quaternion.Slerp(mover.rotation, targetRotation, step);
            rigidbody.MoveRotation(nextRotation);
            return Quaternion.Angle(nextRotation, targetRotation) < 0.1f;
        }

        public static void PreventUnitOverlap(
            bool touchingEnemy,
            bool grounded,
            float overrideOnEnemyDrag,
            Vector3 touchingEnemiesCenter,
            Transform mover,
            Rigidbody rigidbody)
        {
            if (!touchingEnemy || grounded || overrideOnEnemyDrag >= 0f || mover == null || rigidbody == null)
            {
                return;
            }

            if (touchingEnemiesCenter != Vector3.zero)
            {
                rigidbody.AddForce(mover.position - touchingEnemiesCenter, ForceMode.VelocityChange);
            }
        }

        public static void UpdateTouchingEnemyConstraints(
            ICombatBehaviorRuntime runtime,
            float freezeDistanceThreshold)
        {
            if (runtime.IsTouchingEnemy() && runtime.IsGrounded())
            {
                runtime.SetConstraints(runtime.DistanceToNearestEnemyXZ() >= freezeDistanceThreshold
                    ? RigidbodyConstraints.FreezeAll
                    : RigidbodyConstraints.FreezeRotation);
                return;
            }

            runtime.SetConstraints(RigidbodyConstraints.FreezeRotation);
        }

        public static Vector3 ClampPositionToBattleRing(Vector3 worldPos, float battleRingRadius)
        {
            var clampedPos = worldPos;
            var originY = clampedPos.y;
            clampedPos.y = 0f;
            if (battleRingRadius > 0f && clampedPos.sqrMagnitude > battleRingRadius * battleRingRadius)
            {
                clampedPos = clampedPos.normalized * battleRingRadius;
            }

            clampedPos.y = originY < 0f ? 0f : originY;
            return clampedPos;
        }

        public static Vector3 CalcFixedPlanarMoveTarget(
            Vector3 startPos,
            Vector3 direction,
            float distance,
            float battleRingRadius)
        {
            var planarDirection = direction;
            planarDirection.y = 0f;
            if (planarDirection.sqrMagnitude <= MovementEpsilon * MovementEpsilon || distance <= 0f)
            {
                return ClampPositionToBattleRing(startPos, battleRingRadius);
            }

            var targetPos = startPos + planarDirection.normalized * distance;
            targetPos.y = startPos.y;
            return ClampPositionToBattleRing(targetPos, battleRingRadius);
        }

        public static void ManageSpeed(Rigidbody rigidbody, float maxSpeed, bool ignoreY)
        {
            if (rigidbody == null || maxSpeed <= 0f)
            {
                return;
            }

            var velocity = rigidbody.linearVelocity;
            var planarVelocity = ignoreY ? new Vector3(velocity.x, 0f, velocity.z) : velocity;
            var clampedPlanar = Vector3.ClampMagnitude(planarVelocity, maxSpeed);
            if ((planarVelocity - clampedPlanar).sqrMagnitude < MovementEpsilon)
            {
                return;
            }

            if (ignoreY)
            {
                velocity.x = clampedPlanar.x;
                velocity.z = clampedPlanar.z;
            }
            else
            {
                velocity = clampedPlanar;
            }

            rigidbody.linearVelocity = velocity;
        }

        public static Vector3 ResolvePushDirection(
            Vector3 damageHappenPoint,
            Vector3 attackerPosition,
            Vector3 victimPosition,
            Vector3 fallbackForward,
            DamageType damageType)
        {
            var resolvedDir = victimPosition - damageHappenPoint;
            resolvedDir.y = 0f;
            if (resolvedDir.sqrMagnitude > MovementEpsilon * MovementEpsilon)
            {
                return resolvedDir.normalized;
            }

            resolvedDir = victimPosition - attackerPosition;
            resolvedDir.y = 0f;
            if (resolvedDir.sqrMagnitude > MovementEpsilon * MovementEpsilon)
            {
                return resolvedDir.normalized;
            }

            resolvedDir = fallbackForward;
            resolvedDir.y = 0f;
            if (resolvedDir.sqrMagnitude > MovementEpsilon * MovementEpsilon)
            {
                return resolvedDir.normalized;
            }

            return damageType == DamageType.explosion ? Vector3.back : Vector3.forward;
        }

        public static Vector3 GetVerticalDir(Vector3 direction)
        {
            return Mathf.Approximately(direction.z, 0f)
                ? new Vector3(0f, 0f, -1f)
                : new Vector3(-direction.z / direction.x, 0f, 1f).normalized;
        }
    }
}
