using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Keep the fielded fighters inside URP's shadow range as the battle camera pulls back.</summary>
public sealed class BattleShadowCoverage : IDisposable
{
    sealed class QualityLease
    {
        public RenderPipelineAsset OriginalOverride;
        public UniversalRenderPipelineAsset Source;
        public UniversalRenderPipelineAsset Runtime;
    }

    readonly Dictionary<int, QualityLease> _qualities = new Dictionary<int, QualityLease>();

    public float RequiredDistance { get; private set; }
    public float AppliedDistance { get; private set; }

    public void Update(Camera camera, float receiverDistance, float deltaTime)
    {
        if (camera == null || receiverDistance <= 0) return;
        int quality = QualitySettings.GetQualityLevel();
        var current = QualitySettings.renderPipeline;
        if (!_qualities.TryGetValue(quality, out var lease) || current != lease.Runtime)
        {
            // Preserve changes made by another owner, such as a quality/settings
            // screen. Each quality receives its own unsaved runtime copy.
            if (lease != null) Destroy(lease.Runtime);
            var source = (current != null ? current : GraphicsSettings.defaultRenderPipeline)
                as UniversalRenderPipelineAsset;
            if (source == null) return;
            lease = new QualityLease { OriginalOverride = current, Source = source,
                Runtime = UnityEngine.Object.Instantiate(source) };
            lease.Runtime.name = source.name + " (Battle Shadow Coverage)";
            lease.Runtime.hideFlags = HideFlags.HideAndDontSave;
            // Leave atlas resolution/cascade count at the selected quality's
            // budget. Reduce only the final fade band so distant fighters stay
            // visibly grounded without rendering an unnecessarily large arena.
            lease.Runtime.cascadeBorder = Mathf.Min(source.cascadeBorder, .1f);
            _qualities[quality] = lease;
            QualitySettings.renderPipeline = lease.Runtime;
        }

        RequiredDistance = Mathf.Max(lease.Source.shadowDistance,
            Mathf.Ceil(receiverDistance / Mathf.Max(.1f, 1 - lease.Runtime.cascadeBorder) / 5) * 5);
        // Grow immediately for separation/large teams; shrink gently as the
        // camera returns to melee so the shadow projection doesn't jump.
        AppliedDistance = RequiredDistance >= lease.Runtime.shadowDistance ? RequiredDistance
            : Mathf.Lerp(lease.Runtime.shadowDistance, RequiredDistance,
                1 - Mathf.Exp(-Mathf.Max(0, deltaTime) / 2));
        lease.Runtime.shadowDistance = AppliedDistance;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, AppliedDistance + 1);
    }

    public static float ReceiverDistance(Vector3 cameraPosition, IReadOnlyList<Bounds> fighters)
    {
        float squared = 0;
        if (fighters == null) return 0;
        foreach (var body in fighters)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                var point = body.center + Vector3.Scale(body.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                    (corner & 4) == 0 ? -1 : 1));
                squared = Mathf.Max(squared, (point - cameraPosition).sqrMagnitude);
                // Airborne models still cast onto the arena floor (y = 0).
                // Testing only their animated bounds can omit that receiver.
                point.y = 0;
                squared = Mathf.Max(squared, (point - cameraPosition).sqrMagnitude);
            }
        }
        return fighters.Count > 0 ? Mathf.Sqrt(squared) + 2 : 0;
    }

    public void Dispose()
    {
        if (_qualities.Count == 0) return;
        // ForEach temporarily selects each quality and restores the caller's
        // active quality. Inactive tiers must not retain a destroyed copy after
        // a player changes quality while a battle is open.
        QualitySettings.ForEach((quality, name) =>
        {
            if (_qualities.TryGetValue(quality, out var lease)
                && QualitySettings.renderPipeline == lease.Runtime)
                QualitySettings.renderPipeline = lease.OriginalOverride;
        });
        foreach (var lease in _qualities.Values) Destroy(lease.Runtime);
        _qualities.Clear();
        RequiredDistance = AppliedDistance = 0;
    }

    static void Destroy(UniversalRenderPipelineAsset pipeline)
    {
        if (pipeline == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(pipeline);
        else UnityEngine.Object.DestroyImmediate(pipeline);
    }
}
