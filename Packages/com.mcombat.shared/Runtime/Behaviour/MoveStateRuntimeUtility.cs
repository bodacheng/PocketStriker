using Skill;
using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public enum CombatMoveDirection
    {
        Stay,
        TowardsEnemy,
        BackTowardsEnemy,
        TowardsEnemyRight,
        TowardsEnemyLeft,
        RunToBattleGroundCenter
    }

    public readonly struct AiMoveDecision
    {
        public readonly CombatMoveDirection Direction;
        public readonly Vector3 UseDirection;

        public AiMoveDecision(CombatMoveDirection direction, Vector3 useDirection)
        {
            Direction = direction;
            UseDirection = useDirection;
        }
    }

    public static class MoveStateRuntimeUtility
    {
        public static MoveStateSettings ResolveSettings(MoveType moveType, float fighterMoveSpeed)
        {
            return BehaviorMotionUtility.ResolveMoveStateSettings(moveType, fighterMoveSpeed);
        }

        public static AiMoveDecision ResolveAiMoveDecision(
            bool nearRing,
            Vector3 selfPosition,
            bool hasClosestEnemy,
            Vector3 closestEnemyPosition,
            float nextSkillMinRange,
            float closeDistance)
        {
            if (nearRing)
            {
                var direction = Vector3.zero - selfPosition;
                direction.y = 0f;
                return new AiMoveDecision(CombatMoveDirection.RunToBattleGroundCenter, direction);
            }

            if (!hasClosestEnemy)
            {
                return new AiMoveDecision(CombatMoveDirection.Stay, Vector3.zero);
            }

            var meToEnemyVector = closestEnemyPosition - selfPosition;
            CombatMoveDirection moveDirection;
            if (meToEnemyVector.magnitude < nextSkillMinRange)
            {
                moveDirection = CombatMoveDirection.BackTowardsEnemy;
            }
            else if (meToEnemyVector.magnitude <= closeDistance)
            {
                moveDirection = CombatMoveDirection.Stay;
            }
            else
            {
                switch (Random.Range(0, 3))
                {
                    case 0:
                        moveDirection = CombatMoveDirection.TowardsEnemy;
                        break;
                    case 1:
                        moveDirection = CombatMoveDirection.TowardsEnemyLeft;
                        break;
                    default:
                        moveDirection = CombatMoveDirection.TowardsEnemyRight;
                        break;
                }
            }

            return new AiMoveDecision(moveDirection, ResolveInitialUseDirection(moveDirection, meToEnemyVector));
        }

        public static bool IsAiMoveSegmentFinished(
            CombatMoveDirection moveDirection,
            float timeCounter,
            float timeLimit,
            bool hasClosestEnemy,
            Vector3 selfPosition,
            Vector3 closestEnemyPosition,
            float closeDistance)
        {
            switch (moveDirection)
            {
                case CombatMoveDirection.BackTowardsEnemy:
                case CombatMoveDirection.Stay:
                    return timeCounter > timeLimit / 2f;
                case CombatMoveDirection.TowardsEnemy:
                    if (timeCounter > timeLimit)
                    {
                        return true;
                    }

                    return hasClosestEnemy
                           && Vector3.Distance(selfPosition, closestEnemyPosition) < closeDistance;
                case CombatMoveDirection.TowardsEnemyLeft:
                case CombatMoveDirection.TowardsEnemyRight:
                case CombatMoveDirection.RunToBattleGroundCenter:
                    return timeCounter > timeLimit / 3f;
                default:
                    return false;
            }
        }

        public static Vector3 ResolveAiFrameDirection(
            CombatMoveDirection moveDirection,
            Vector3 currentUseDirection,
            bool hasClosestEnemy,
            Vector3 selfPosition,
            Vector3 closestEnemyPosition)
        {
            switch (moveDirection)
            {
                case CombatMoveDirection.Stay:
                    return Vector3.zero;
                case CombatMoveDirection.BackTowardsEnemy:
                    return hasClosestEnemy ? selfPosition - closestEnemyPosition : Vector3.zero;
                case CombatMoveDirection.TowardsEnemy:
                    return hasClosestEnemy ? closestEnemyPosition - selfPosition : Vector3.zero;
                default:
                    return currentUseDirection;
            }
        }

        public static Vector3 ResolveDirectionAroundBlockingUnits(
            Vector3 currentUseDirection,
            Vector3 selfPosition,
            Vector3 enemyPosition,
            Vector3 teammatePosition,
            float deltaTime,
            float rotateSpeed)
        {
            var detourDirection = (teammatePosition - selfPosition).normalized
                                  + (selfPosition - enemyPosition).normalized;
            detourDirection.y = 0f;
            return Vector3.RotateTowards(currentUseDirection, detourDirection, rotateSpeed * deltaTime, 0f).normalized;
        }

        public static Vector3 ResolveControlMoveDirection(
            float cameraYaw,
            float horizontal,
            float vertical)
        {
            var direction = SkillStateRuntimeUtility.ResolveCameraRelativeDirection(
                Vector3.forward,
                cameraYaw,
                horizontal,
                vertical);
            return NormalizePlanar(direction);
        }

        public static Vector3 NormalizePlanar(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > BehaviorMotionUtility.MovementEpsilon
                ? direction.normalized
                : Vector3.zero;
        }

        static Vector3 ResolveInitialUseDirection(CombatMoveDirection moveDirection, Vector3 meToEnemyVector)
        {
            switch (moveDirection)
            {
                case CombatMoveDirection.TowardsEnemyRight:
                    return BehaviorMotionUtility.GetVerticalDir(meToEnemyVector) + meToEnemyVector.normalized;
                case CombatMoveDirection.TowardsEnemyLeft:
                    return -BehaviorMotionUtility.GetVerticalDir(meToEnemyVector) + meToEnemyVector.normalized;
                default:
                    return Vector3.zero;
            }
        }
    }
}
