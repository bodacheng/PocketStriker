using System;
using System.Collections.Generic;
using UnityEngine;

namespace MCombat.Shared.Combat
{
    public static class CombatPlacementUtility
    {
        const float DirectionEpsilon = 0.0001f;

        public static Vector3 AnchorPosition(Transform root, Transform geometryCenter)
        {
            if (geometryCenter != null)
            {
                return geometryCenter.position;
            }

            return root != null ? root.position : Vector3.zero;
        }

        public static void PlaceRootByGeometryCenter(
            Transform root,
            Transform geometryCenter,
            Vector3 targetGeometryCenterPosition,
            Quaternion targetRotation)
        {
            PlaceRootByGeometryCenter(root, null, geometryCenter, targetGeometryCenterPosition, targetRotation);
        }

        public static void PlaceRootByGeometryCenter(
            Transform root,
            Rigidbody rigidbody,
            Transform geometryCenter,
            Vector3 targetGeometryCenterPosition,
            Quaternion targetRotation)
        {
            if (root == null)
            {
                return;
            }

            if (geometryCenter == null)
            {
                SetRootPose(root, rigidbody, targetGeometryCenterPosition, targetRotation);
                return;
            }

            var rootScale = root.lossyScale;
            var localCenterPoint = root.InverseTransformPoint(geometryCenter.position);
            var centerOffset = Vector3.Scale(localCenterPoint, rootScale);
            SetRootPose(root, rigidbody, targetGeometryCenterPosition - targetRotation * centerOffset, targetRotation);
        }

        public static bool FaceRootTowards(Transform root, Transform geometryCenter, Vector3 targetPosition)
        {
            return FaceRootTowards(root, null, geometryCenter, targetPosition);
        }

        public static bool FaceRootTowards(
            Transform root,
            Rigidbody rigidbody,
            Transform geometryCenter,
            Vector3 targetPosition)
        {
            if (root == null)
            {
                return false;
            }

            var anchorPosition = AnchorPosition(root, geometryCenter);
            if (!TryPlanarLookRotation(anchorPosition, targetPosition, out var lookRotation))
            {
                return false;
            }

            PlaceRootByGeometryCenter(root, rigidbody, geometryCenter, anchorPosition, lookRotation);
            return true;
        }

        public static bool FaceRootsTowards<TUnit>(
            IEnumerable<TUnit> units,
            IEnumerable<TUnit> targets,
            Func<TUnit, bool> isValidUnit,
            Func<TUnit, Transform> rootResolver,
            Func<TUnit, Transform> geometryCenterResolver,
            Func<TUnit, Rigidbody> rigidbodyResolver = null,
            Action<TUnit> beforeApplyUnit = null)
        {
            if (units == null)
            {
                return false;
            }

            if (!TryAverageAnchor(targets, isValidUnit, rootResolver, geometryCenterResolver, out var targetPosition))
            {
                return false;
            }

            foreach (var unit in units)
            {
                if (!IsPlacementUnitValid(unit, isValidUnit, rootResolver, out var root))
                {
                    continue;
                }

                beforeApplyUnit?.Invoke(unit);
                FaceRootTowards(
                    root,
                    rigidbodyResolver?.Invoke(unit),
                    geometryCenterResolver?.Invoke(unit),
                    targetPosition);
            }

            return true;
        }

        static void SetRootPose(Transform root, Rigidbody rigidbody, Vector3 position, Quaternion rotation)
        {
            if (rigidbody != null)
            {
                rigidbody.position = position;
                rigidbody.rotation = rotation;
            }

            root.SetPositionAndRotation(position, rotation);
        }

        public static bool TryAverageAnchor<TUnit>(
            IEnumerable<TUnit> units,
            Func<TUnit, bool> isValidUnit,
            Func<TUnit, Transform> rootResolver,
            Func<TUnit, Transform> geometryCenterResolver,
            out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            if (units == null || rootResolver == null)
            {
                return false;
            }

            var count = 0;
            foreach (var unit in units)
            {
                if (!IsPlacementUnitValid(unit, isValidUnit, rootResolver, out var root))
                {
                    continue;
                }

                targetPosition += AnchorPosition(root, geometryCenterResolver?.Invoke(unit));
                count++;
            }

            if (count <= 0)
            {
                return false;
            }

            targetPosition /= count;
            return true;
        }

        static bool IsPlacementUnitValid<TUnit>(
            TUnit unit,
            Func<TUnit, bool> isValidUnit,
            Func<TUnit, Transform> rootResolver,
            out Transform root)
        {
            root = null;
            if (rootResolver == null || (isValidUnit != null && !isValidUnit(unit)))
            {
                return false;
            }

            root = rootResolver(unit);
            return root != null;
        }

        public static bool TryPlanarLookRotation(Vector3 from, Vector3 to, out Quaternion rotation)
        {
            var direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < DirectionEpsilon)
            {
                rotation = Quaternion.identity;
                return false;
            }

            rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return true;
        }
    }
}
