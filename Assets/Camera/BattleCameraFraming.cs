using System.Collections.Generic;
using UnityEngine;

/// <summary>Perspective framing against an actual, possibly asymmetric HUD-free viewport.</summary>
public static class BattleCameraFraming
{
    public struct Pose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Distance;
        public Vector3 Center;
        public Rect Viewport;
    }

    /// <summary>A rotation-invariant horizontal silhouette with a finite vertical span.</summary>
    public readonly struct BodyEnvelope
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float Height;

        public BodyEnvelope(Vector3 center, float radius, float height)
        {
            Center = center;
            Radius = Mathf.Max(0, radius);
            Height = Mathf.Max(0, height);
        }
    }

    readonly struct SupportPlane
    {
        readonly Vector3 _direction;
        readonly float _horizontalSupport;
        readonly float _verticalSupport;
        readonly float _offset;

        public SupportPlane(Vector3 direction, float offset = 0)
        {
            _direction = direction;
            _horizontalSupport = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
            _verticalSupport = Mathf.Abs(direction.y);
            _offset = offset;
        }

        public float RequiredDistance(BodyEnvelope envelope, Vector3 center) => _offset
            + Vector3.Dot(envelope.Center - center, _direction)
            + envelope.Radius * _horizontalSupport + envelope.Height * 0.5f * _verticalSupport;
    }

    public static Pose CalculatePose(IReadOnlyList<Bounds> bounds, float aspect, float fieldOfView,
        Rect usableViewport, float pitch, float yaw = 0, float minimumDistance = 6, float nearClip = 0.3f)
    {
        var center = Vector3.zero;
        if (bounds != null && bounds.Count > 0)
        {
            var whole = bounds[0];
            for (var i = 1; i < bounds.Count; i++) whole.Encapsulate(bounds[i]);
            center = whole.center;
        }
        return CalculatePoseAtCenter(bounds, aspect, fieldOfView, usableViewport,
            Quaternion.Euler(Mathf.Clamp(pitch, 15, 90), yaw, 0), center, minimumDistance, nearClip);
    }

    public static Pose CalculatePoseAtCenter(IReadOnlyList<Bounds> bounds, float aspect, float fieldOfView,
        Rect usableViewport, Quaternion rotation, Vector3 center, float minimumDistance = 6, float nearClip = 0.3f,
        IReadOnlyList<BodyEnvelope> bodyEnvelopes = null)
    {
        var viewport = ClampViewport(usableViewport);
        float tanVertical = Mathf.Tan(Mathf.Clamp(fieldOfView, 10, 100) * 0.5f * Mathf.Deg2Rad);
        float tanHorizontal = tanVertical * Mathf.Max(0.1f, aspect);
        var right = rotation * Vector3.right;
        var up = rotation * Vector3.up;
        var forward = rotation * Vector3.forward;
        float offsetX = viewport.center.x * 2 - 1;
        float offsetY = viewport.center.y * 2 - 1;
        float distance = Mathf.Max(0, minimumDistance);
        if (bounds != null)
        {
            for (var i = 0; i < bounds.Count; i++)
            {
                var box = bounds[i];
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = box.center + Vector3.Scale(box.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var delta = point - center;
                    float depth = Vector3.Dot(delta, forward);
                    // Lateral displacement centers fighters in the usable rectangle
                    // without changing the lens or cropping the camera viewport.
                    float x = Vector3.Dot(delta, right) - offsetX * tanHorizontal * depth;
                    float y = Vector3.Dot(delta, up) - offsetY * tanVertical * depth;
                    float horizontal = Mathf.Abs(x) / (viewport.width * tanHorizontal) - depth;
                    float vertical = Mathf.Abs(y) / (viewport.height * tanVertical) - depth;
                    distance = Mathf.Max(distance, Mathf.Max(horizontal,
                        Mathf.Max(vertical, nearClip + 0.1f - depth)));
                }
            }
        }
        if (bodyEnvelopes != null && bodyEnvelopes.Count > 0)
        {
            // Each perspective edge is a linear half-space after accounting
            // for the asymmetric viewport offset. Maximize that expression on
            // the cylinder itself, avoiding the extra corners of an XZ square.
            var horizontalPositive = new SupportPlane(right / (viewport.width * tanHorizontal)
                - forward * (1 + offsetX / viewport.width));
            var horizontalNegative = new SupportPlane(-right / (viewport.width * tanHorizontal)
                - forward * (1 - offsetX / viewport.width));
            var verticalPositive = new SupportPlane(up / (viewport.height * tanVertical)
                - forward * (1 + offsetY / viewport.height));
            var verticalNegative = new SupportPlane(-up / (viewport.height * tanVertical)
                - forward * (1 - offsetY / viewport.height));
            var nearPlane = new SupportPlane(-forward, nearClip + 0.1f);
            for (var i = 0; i < bodyEnvelopes.Count; i++)
            {
                var body = bodyEnvelopes[i];
                distance = Mathf.Max(distance, horizontalPositive.RequiredDistance(body, center));
                distance = Mathf.Max(distance, horizontalNegative.RequiredDistance(body, center));
                distance = Mathf.Max(distance, verticalPositive.RequiredDistance(body, center));
                distance = Mathf.Max(distance, verticalNegative.RequiredDistance(body, center));
                distance = Mathf.Max(distance, nearPlane.RequiredDistance(body, center));
            }
        }
        distance += 0.02f;
        return WithDistance(new Pose { Rotation = rotation, Center = center, Viewport = viewport },
            distance, aspect, fieldOfView);
    }

    public static Pose WithDistance(Pose pose, float distance, float aspect, float fieldOfView)
    {
        float tanVertical = Mathf.Tan(Mathf.Clamp(fieldOfView, 10, 100) * 0.5f * Mathf.Deg2Rad);
        float offsetX = pose.Viewport.center.x * 2 - 1;
        float offsetY = pose.Viewport.center.y * 2 - 1;
        pose.Distance = distance;
        pose.Position = pose.Center - pose.Rotation * Vector3.forward * distance
            - pose.Rotation * Vector3.right * (offsetX * tanVertical * Mathf.Max(0.1f, aspect) * distance)
            - pose.Rotation * Vector3.up * (offsetY * tanVertical * distance);
        return pose;
    }

    /// <summary>Separation is visible immediately; each profile controls its inward settling time.</summary>
    public static float SmoothDistance(float current, float required, float deltaTime, float smoothTime = 2f)
    {
        return required >= current ? required
            : Mathf.Lerp(current, required, 1 - Mathf.Exp(-Mathf.Max(0, deltaTime) / Mathf.Max(0.01f, smoothTime)));
    }

    public static bool ShouldIncludeUnit(TeamMode mode, bool active, bool dead, bool isRotationFighter)
    {
        return active && !dead && (mode != TeamMode.Rotation || isRotationFighter);
    }

    public static Rect CalculateUsableViewport(Rect normalizedSafeArea, Rect normalizedMiddleArea,
        Rect normalizedPortraitRail, bool excludePortraitRail, float padding = 0.02f)
    {
        var safe = ClampViewport(normalizedSafeArea);
        var middle = ClampViewport(normalizedMiddleArea);
        float left = Mathf.Max(safe.xMin, middle.xMin);
        float right = Mathf.Min(safe.xMax, middle.xMax);
        float bottom = Mathf.Max(safe.yMin, middle.yMin);
        float top = Mathf.Min(safe.yMax, middle.yMax);
        if (excludePortraitRail && normalizedPortraitRail.yMax > bottom && normalizedPortraitRail.yMin < top)
            left = Mathf.Max(left, normalizedPortraitRail.xMax + padding);
        return ClampViewport(Rect.MinMaxRect(left + padding, bottom + padding, right - padding, top - padding));
    }

    /// <summary>Center a duel under the corner portraits instead of pushing it toward the enemy.</summary>
    public static Rect CalculateDuelViewport(Rect safe, Rect middle, Rect rail, bool hasPortraits)
    {
        var usable = CalculateUsableViewport(safe, middle, rail, false);
        if (!hasPortraits || !usable.Overlaps(rail)) return usable;
        float belowPortraits = rail.yMin - .02f;
        if (belowPortraits - usable.yMin >= .35f)
        {
            // Portrait phones have ample height below the short corner rail.
            // Retain the full width for the two fighters and their separation.
            usable.yMax = Mathf.Min(usable.yMax, belowPortraits);
        }
        else
        {
            // A short landscape/safe-area viewport cannot use the lower band.
            // Reserve equal side margins so the remaining duel is still centered.
            float inset = Mathf.Max(0, rail.xMax + .02f - usable.xMin);
            inset = Mathf.Min(inset, Mathf.Max(0, (usable.width - .2f) * .5f));
            usable.xMin += inset;
            usable.xMax -= inset;
        }
        return usable;
    }

    public static bool TryGetModelBounds(Transform root, out Bounds bounds)
    {
        return TryGetModelBounds(root != null ? root.GetComponentsInChildren<Renderer>(true) : null, out bounds);
    }

    public static bool TryGetModelBounds(IReadOnlyList<Renderer> renderers, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (renderers == null) return false;
        for (var i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            // Particle/trail effects must not send the camera chasing a projectile.
            if (renderer == null || !(renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            var box = renderer.bounds;
            if (!Finite(box.center) || !Finite(box.size) || box.size.sqrMagnitude < 0.0001f) continue;
            if (!found) bounds = box;
            else bounds.Encapsulate(box);
            found = true;
        }
        return found;
    }

    static Rect ClampViewport(Rect viewport)
    {
        float left = Mathf.Clamp(viewport.xMin, 0, 0.9f);
        float bottom = Mathf.Clamp(viewport.yMin, 0, 0.9f);
        return Rect.MinMaxRect(left, bottom, Mathf.Clamp(viewport.xMax, left + 0.1f, 1),
            Mathf.Clamp(viewport.yMax, bottom + 0.1f, 1));
    }

    static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
        && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
}
