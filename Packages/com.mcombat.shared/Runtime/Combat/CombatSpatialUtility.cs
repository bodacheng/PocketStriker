using System;
using System.Collections.Generic;
using UnityEngine;

namespace MCombat.Shared.Combat
{
    public static class CombatSpatialUtility
    {
        public static float HorizontalDistanceSqr(Vector3 position, Vector3 center)
        {
            var dx = position.x - center.x;
            var dz = position.z - center.z;
            return dx * dx + dz * dz;
        }

        public static void SortByHorizontalDistance<T>(IList<T> items, Vector3 center, Func<T, Vector3?> getPosition)
        {
            if (items == null || items.Count < 2)
            {
                return;
            }

            for (var i = 1; i < items.Count; i++)
            {
                var current = items[i];
                var currentDistance = DistanceOrMax(current, center, getPosition);
                var j = i - 1;

                while (j >= 0)
                {
                    var comparing = items[j];
                    var comparingDistance = DistanceOrMax(comparing, center, getPosition);
                    if (comparingDistance <= currentDistance)
                    {
                        break;
                    }

                    items[j + 1] = comparing;
                    j--;
                }

                items[j + 1] = current;
            }
        }

        public static T FindNearest<T>(IList<T> items, Vector3 center, Func<T, Vector3?> getPosition)
            where T : class
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            T nearest = null;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var position = getPosition(item);
                if (!position.HasValue)
                {
                    continue;
                }

                var sqr = HorizontalDistanceSqr(position.Value, center);
                if (sqr < nearestDistance)
                {
                    nearestDistance = sqr;
                    nearest = item;
                }
            }

            return nearest;
        }

        static float DistanceOrMax<T>(T item, Vector3 center, Func<T, Vector3?> getPosition)
        {
            var position = getPosition(item);
            return position.HasValue ? HorizontalDistanceSqr(position.Value, center) : float.MaxValue;
        }
    }
}
