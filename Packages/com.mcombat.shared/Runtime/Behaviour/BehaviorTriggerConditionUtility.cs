using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MCombat.Shared.Behaviour
{
    public static class BehaviorTriggerConditionUtility
    {
        public static bool IsLosingDefendStrength(bool currentStateIsDefend, int resistance)
        {
            return currentStateIsDefend && resistance < 2;
        }

        public static bool IsDangerousNearby(bool hasThreat, int resistance)
        {
            return hasThreat && resistance == 0;
        }

        public static bool IsCounterComingEnergy(int nearestEnemyBodyCount, bool hasThreat)
        {
            return nearestEnemyBodyCount == 0 && hasThreat;
        }

        public static bool CanCounter(bool onBuff, bool dangerousVeryClose)
        {
            return !onBuff && dangerousVeryClose;
        }

        public static bool IsDangerousVeryClose(int resistance, bool hasThreat)
        {
            return resistance <= 0 && hasThreat;
        }

        public static bool HasEnemyClose(int colliderCount)
        {
            return colliderCount > 0;
        }

        public static bool HasColliderForAttackHeight(IList<Collider> colliders, int triggerAttackHeight)
        {
            if (colliders == null)
            {
                return false;
            }

            switch (triggerAttackHeight)
            {
                case -1:
                    return HasLowCollider(colliders);
                case 0:
                    return HasMidCollider(colliders);
                case 1:
                    return HasHighCollider(colliders);
                default:
                    return colliders.Count > 0;
            }
        }

        public static bool TimeToRespond(bool hasThreat)
        {
            return !hasThreat;
        }

        public static bool ShouldStopRunning(bool hasNearestEnemyBody, float nearestEnemyDistance, bool hasThreat)
        {
            return hasNearestEnemyBody && nearestEnemyDistance < 5f || hasThreat;
        }

        public static bool InvokeCondition<TTarget>(
            TTarget target,
            IDictionary<string, Func<TTarget, bool>> methodCache,
            string conditionFunctionName)
            where TTarget : class
        {
            if (target == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(conditionFunctionName))
            {
                return true;
            }

            if (!methodCache.TryGetValue(conditionFunctionName, out var methodDelegate))
            {
                var methodInfo = typeof(TTarget).GetMethod(
                    conditionFunctionName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (methodInfo == null)
                {
                    throw new InvalidOperationException($"方法 '{conditionFunctionName}' 未找到。");
                }

                methodDelegate = (Func<TTarget, bool>)Delegate.CreateDelegate(
                    typeof(Func<TTarget, bool>),
                    null,
                    methodInfo);
                methodCache[conditionFunctionName] = methodDelegate;
            }

            return methodDelegate(target);
        }

        public static bool CheckExitCondition(
            string exitCondition,
            Func<bool> timeToRespond,
            Func<bool> timeToStopRunning)
        {
            switch (exitCondition)
            {
                case "TimeToRespond":
                    return timeToRespond == null || timeToRespond();
                case "TimeToStopRunning":
                    return timeToStopRunning == null || timeToStopRunning();
                default:
                    return true;
            }
        }

        static bool HasLowCollider(IList<Collider> colliders)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];
                if (collider != null && collider.transform.position.y < 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasMidCollider(IList<Collider> colliders)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];
                if (collider != null && collider.transform.position.y >= 0.8f)
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasHighCollider(IList<Collider> colliders)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];
                if (collider != null && collider.transform.position.y >= 1f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
