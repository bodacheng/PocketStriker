using UnityEngine;

/// <summary>Keeps a rotating category gem inside its actual UI button, after safe-area changes.</summary>
[DisallowMultipleComponent]
public sealed class SkillStoneTabEffectLayout : MonoBehaviour
{
    const float CameraDepth = 5f;
    readonly Vector3[] corners = new Vector3[4];
    RectTransform target;
    Camera effectCamera;
    Canvas canvas;
    float radius;
    float fill;

    public RectTransform Target => target;
    public Camera EffectCamera => effectCamera;

    public void Initialize(RectTransform button, Camera camera, float fillFraction = 0.9f)
    {
        target = button;
        effectCamera = camera;
        canvas = button.GetComponentInParent<Canvas>()?.rootCanvas;
        fill = fillFraction;
        if (radius <= 0f) radius = RotationRadius();
        // The glow must scale and move with the gem instead of keeping its authored world size.
        foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }
        RefreshLayout();
    }

    float RotationRadius()
    {
        float result = 0f;
        foreach (var mesh in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null) continue;
            var bounds = mesh.sharedMesh.bounds;
            // A sphere around each rotating mesh's pivot also contains intermediate rotations.
            var matrix = transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            var scale = Mathf.Max(matrix.MultiplyVector(Vector3.right).magnitude,
                matrix.MultiplyVector(Vector3.up).magnitude, matrix.MultiplyVector(Vector3.forward).magnitude);
            result = Mathf.Max(result, matrix.MultiplyPoint3x4(Vector3.zero).magnitude
                + (bounds.center.magnitude + bounds.extents.magnitude) * scale);
        }
        if (result > 0f) return result;
        // The fallback element and selected-tab artwork contain particles only.
        // Include their emission volume, travel and child scale, not just one billboard.
        foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            var matrix = transform.worldToLocalMatrix * particles.transform.localToWorldMatrix;
            var childScale = Mathf.Max(matrix.MultiplyVector(Vector3.right).magnitude,
                matrix.MultiplyVector(Vector3.up).magnitude, matrix.MultiplyVector(Vector3.forward).magnitude);
            var particleSize = main.startSize3D
                ? Mathf.Max(CurveMaximum(main.startSizeX), CurveMaximum(main.startSizeY), CurveMaximum(main.startSizeZ))
                : CurveMaximum(main.startSize);
            var sizeOverLifetime = particles.sizeOverLifetime;
            if (sizeOverLifetime.enabled)
                particleSize *= sizeOverLifetime.separateAxes
                    ? Mathf.Max(CurveMaximum(sizeOverLifetime.x), CurveMaximum(sizeOverLifetime.y), CurveMaximum(sizeOverLifetime.z))
                    : CurveMaximum(sizeOverLifetime.size);
            var localRadius = particleSize * 0.707107f;
            var shape = particles.shape;
            if (shape.enabled)
            {
                var shapeScale = Mathf.Max(Mathf.Abs(shape.scale.x), Mathf.Abs(shape.scale.y), Mathf.Abs(shape.scale.z));
                localRadius += shape.position.magnitude + Mathf.Max(shape.radius * shapeScale, shape.scale.magnitude * .5f);
            }
            localRadius += CurveMaximum(main.startSpeed) * CurveMaximum(main.startLifetime);
            result = Mathf.Max(result, matrix.MultiplyPoint3x4(Vector3.zero).magnitude + localRadius * childScale);
        }
        return Mathf.Max(result, 0.001f);
    }

    static float CurveMaximum(ParticleSystem.MinMaxCurve curve)
    {
        if (curve.mode == ParticleSystemCurveMode.Constant) return Mathf.Abs(curve.constant);
        if (curve.mode == ParticleSystemCurveMode.TwoConstants) return Mathf.Max(Mathf.Abs(curve.constantMin), Mathf.Abs(curve.constantMax));
        float result = 0f;
        for (int i = 0; i <= 32; i++)
        {
            var time = i / 32f;
            result = Mathf.Max(result, Mathf.Abs(curve.Evaluate(time, 0f)), Mathf.Abs(curve.Evaluate(time, 1f)));
        }
        return result * 1.05f;
    }

    void LateUpdate() => RefreshLayout();

    public void RefreshLayout()
    {
        if (target == null || effectCamera == null || canvas == null || radius <= 0f) return;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        target.GetWorldCorners(corners);
        for (int i = 0; i < corners.Length; i++)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            corners[i] = effectCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, CameraDepth));
        }
        var size = Mathf.Min(Vector3.Distance(corners[0], corners[1]),
            Vector3.Distance(corners[0], corners[3])) * fill;
        var worldScale = size / (2f * radius);
        var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(worldScale / Mathf.Abs(parentScale.x),
            worldScale / Mathf.Abs(parentScale.y), worldScale / Mathf.Abs(parentScale.z));
        transform.position = (corners[0] + corners[2]) * 0.5f;
    }
}
