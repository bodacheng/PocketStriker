using System.Collections.Generic;
using UnityEngine;

public class VisibilityControl : MonoBehaviour
{
    [SerializeField] float radius = 5f;
    [SerializeField] float detectDis = 50f;
    [SerializeField] LayerMask layerMask;
    [SerializeField] LayerMask unitLayer;
    [SerializeField] int wallLayer;
    [SerializeField] int detectInterval = 10;
    [SerializeField] int rayCastMax = 20;

    readonly List<Renderer> _hiddenObjects = new List<Renderer>();
    readonly List<Renderer> _thisFrameDetected = new List<Renderer>();
    readonly HashSet<Renderer> _thisFrameDetectedSet = new HashSet<Renderer>();
    readonly Dictionary<Transform, Renderer[]> _wallRendererCache = new Dictionary<Transform, Renderer[]>();

    RaycastHit[] _hitColliders;
    int _frameCounter;

    void Start()
    {
        _hitColliders = new RaycastHit[Mathf.Max(1, rayCastMax)];
    }

    public void LocalUpdate()
    {
        if (_frameCounter < detectInterval)
        {
            _frameCounter++;
            return;
        }

        _frameCounter = 0;
        if (_hitColliders == null || _hitColliders.Length == 0)
        {
            _hitColliders = new RaycastHit[Mathf.Max(1, rayCastMax)];
        }

        var origin = transform.position;
        var ray = new Ray(origin, transform.forward);
        var detectedCount = Physics.SphereCastNonAlloc(ray, radius, _hitColliders, detectDis, layerMask, QueryTriggerInteraction.Collide);
        var nearestUnitSqr = float.PositiveInfinity;
        var unitLayerMaskValue = unitLayer.value;

        for (var i = 0; i < detectedCount; i++)
        {
            var collider = _hitColliders[i].collider;
            if (collider == null || (unitLayerMaskValue & (1 << collider.gameObject.layer)) == 0)
            {
                continue;
            }

            var unitDistanceSqr = (collider.transform.position - origin).sqrMagnitude;
            if (unitDistanceSqr < nearestUnitSqr)
            {
                nearestUnitSqr = unitDistanceSqr;
            }
        }

        if (!float.IsPositiveInfinity(nearestUnitSqr))
        {
            for (var i = 0; i < detectedCount; i++)
            {
                var collider = _hitColliders[i].collider;
                if (collider == null || collider.gameObject.layer != wallLayer)
                {
                    continue;
                }

                var wallDistanceSqr = (collider.transform.position - origin).sqrMagnitude;
                if (wallDistanceSqr < nearestUnitSqr)
                {
                    HideWallRenderers(collider.transform);
                }
            }
        }

        for (var i = 0; i < _hiddenObjects.Count; i++)
        {
            var renderer = _hiddenObjects[i];
            if (renderer != null && !_thisFrameDetectedSet.Contains(renderer))
            {
                renderer.enabled = true;
            }
        }

        _hiddenObjects.Clear();
        _hiddenObjects.AddRange(_thisFrameDetected);
        _thisFrameDetected.Clear();
        _thisFrameDetectedSet.Clear();
    }

    void HideWallRenderers(Transform wallTransform)
    {
        if (!_wallRendererCache.TryGetValue(wallTransform, out var renderers) || renderers == null)
        {
            renderers = wallTransform.GetComponentsInChildren<Renderer>(true);
            _wallRendererCache[wallTransform] = renderers;
        }

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (_thisFrameDetectedSet.Add(renderer))
            {
                renderer.enabled = false;
                _thisFrameDetected.Add(renderer);
            }
        }
    }

    public void Clear()
    {
        for (var i = 0; i < _hiddenObjects.Count; i++)
        {
            var renderer = _hiddenObjects[i];
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        _hiddenObjects.Clear();
        _thisFrameDetected.Clear();
        _thisFrameDetectedSet.Clear();
        _wallRendererCache.Clear();
    }
}
