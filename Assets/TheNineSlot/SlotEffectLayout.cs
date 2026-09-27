using UnityEngine;

/// <summary>Fits a SlotEffects particle frame to its UI cell in the effects camera.</summary>
[DisallowMultipleComponent]
public sealed class SlotEffectLayout : MonoBehaviour
{
    // The 5 x 6 portal flipbooks include transparent margins around each frame.
    const float FrameWidthFraction = 2f / 3f;
    const float FrameHeightFraction = 5f / 6f;
    const float CameraDepth = 5f;

    readonly Vector3[] _corners = new Vector3[4];
    RectTransform _target;
    Camera _effectCamera;
    Canvas _canvas;
    float _particleSize;
    float _depthScale;

    public void Initialize(RectTransform target, Camera effectCamera)
    {
        _target = target;
        _effectCamera = effectCamera;
        var canvas = target.GetComponentInParent<Canvas>();
        _canvas = canvas != null ? canvas.rootCanvas : null;
        _depthScale = transform.localScale.z;

        // Local particle scaling ignores ancestors. Parenting with worldPositionStays
        // would divide this scale by the Canvas scale and enlarge the frame again.
        transform.SetParent(target, false);
        var main = GetComponent<ParticleSystem>().main;
        _particleSize = main.startSize.constant;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        RefreshLayout();
    }

    void LateUpdate() => RefreshLayout();

    public void RefreshLayout()
    {
        if (_target == null || _effectCamera == null || _canvas == null || _particleSize <= 0f) return;

        var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        _target.GetWorldCorners(_corners);
        for (var i = 0; i < _corners.Length; i++)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, _corners[i]);
            _corners[i] = _effectCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, CameraDepth));
        }

        var width = Vector3.Distance(_corners[0], _corners[3]);
        var height = Vector3.Distance(_corners[0], _corners[1]);
        transform.position = (_corners[0] + _corners[2]) * 0.5f;
        transform.localScale = new Vector3(
            width / (_particleSize * FrameWidthFraction),
            height / (_particleSize * FrameHeightFraction),
            _depthScale);
    }
}
