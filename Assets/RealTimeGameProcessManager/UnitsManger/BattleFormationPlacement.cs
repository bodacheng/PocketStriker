using System.Collections.Generic;
using UnityEngine;

/// <summary>Preserves authored formations and expands larger teams behind their existing front edge.</summary>
public static class BattleFormationPlacement
{
    public static bool TryGetBodyBounds(Data_Center unit, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (unit?.WholeT == null) return false;
        foreach (var limb in unit.WholeT.GetComponentsInChildren<BO_Limb>(true))
        {
            var collider = limb.myColliderMustEquip != null ? limb.myColliderMustEquip : limb.GetComponent<Collider>();
            if (limb.Center != unit || collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if (!found) bounds = collider.bounds;
            else bounds.Encapsulate(collider.bounds);
            found = true;
        }
        return found;
    }

    public static float RequiredArenaRadius(IReadOnlyList<Bounds> bounds, float padding = 1f)
    {
        float radius = 0f;
        if (bounds != null)
            foreach (var box in bounds)
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = box.center.x + ((corner & 1) == 0 ? -box.extents.x : box.extents.x);
                    float z = box.center.z + ((corner & 2) == 0 ? -box.extents.z : box.extents.z);
                    radius = Mathf.Max(radius, new Vector2(x, z).magnitude);
                }
        return radius + Mathf.Max(0, padding);
    }

    public static float RequiredArenaRadius(IReadOnlyList<Pose> formation, float footprintPadding = 0f)
    {
        float radius = 0f;
        if (formation != null)
            for (int index = 0; index < formation.Count; index++)
            {
                var position = formation[index].position;
                radius = Mathf.Max(radius, new Vector2(position.x, position.z).magnitude);
            }
        return radius + Mathf.Max(0, footprintPadding);
    }

    public static Pose[] Build(IReadOnlyList<Pose> authored, int unitCount, float minimumSpacing = 1f)
    {
        unitCount = Mathf.Max(0, unitCount);
        var result = new Pose[unitCount];
        int available = authored?.Count ?? 0;
        if (unitCount <= available)
        {
            for (int i = 0; i < unitCount; i++) result[i] = authored[i];
            return result;
        }
        if (unitCount == 0) return result;
        if (float.IsNaN(minimumSpacing) || float.IsInfinity(minimumSpacing)) minimumSpacing = 1f;
        minimumSpacing = Mathf.Max(0.1f, minimumSpacing);

        var anchor = available > 0 ? authored[0] : new Pose(Vector3.zero, Quaternion.identity);
        var forward = anchor.rotation * Vector3.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);
        float minX = 0, maxX = 0, back = 0, front = 0;
        for (int i = 0; i < available; i++)
        {
            var offset = authored[i].position - anchor.position;
            float x = Vector3.Dot(offset, right);
            float z = Vector3.Dot(offset, forward);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            back = Mathf.Min(back, z); front = Mathf.Max(front, z);
        }
        float width = Mathf.Max(minimumSpacing, maxX - minX);
        float depth = Mathf.Max(minimumSpacing, front - back);
        float aspect = Mathf.Clamp(width / depth, 0.5f, 4f);
        int columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(unitCount * aspect)), 1, unitCount);
        int rows = Mathf.CeilToInt(unitCount / (float)columns);
        float stepX = columns > 1 ? Mathf.Max(minimumSpacing, width / (columns - 1)) : minimumSpacing;
        float stepZ = rows > 1 ? Mathf.Max(minimumSpacing, depth / (rows - 1)) : minimumSpacing;
        float centerX = (minX + maxX) * 0.5f;

        for (int index = 0; index < unitCount; index++)
        {
            int row = index / columns;
            int column = index % columns;
            int columnsInRow = Mathf.Min(columns, unitCount - row * columns);
            float x = centerX + (column - (columnsInRow - 1) * 0.5f) * stepX;
            float z = front - row * stepZ;
            var position = anchor.position + right * x + forward * z;
            position.y = anchor.position.y;
            result[index] = new Pose(position, anchor.rotation);
        }
        return result;
    }
}
