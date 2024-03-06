using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

public class TweenTextScaleManager
{
    private readonly Dictionary<Transform, Sequence> _dic = new Dictionary<Transform, Sequence>();
    private readonly Dictionary<Transform, TweenerCore<Vector3, Vector3, VectorOptions>> _moveDic = new Dictionary<Transform, TweenerCore<Vector3, Vector3, VectorOptions>>();
    
    public void AddNew(Transform target, Vector3 targetScale, Vector3 endScale, float halfDuration)
    {
        _dic.TryGetValue(target, out var sequence);
        if (sequence != null)
        {
            if (sequence.IsActive())
            {
                sequence.Kill();
            }
        }
        sequence = DOTween.Sequence().Append(target.DOScale(targetScale, halfDuration)).Append(target.DOScale(endScale, halfDuration));
        DicAdd<Transform, Sequence>.Add(_dic, target, sequence);
    }

    public void AddNew(Transform target,TweenerCore<Vector3, Vector3, VectorOptions> move)
    {
        _moveDic.TryGetValue(target, out var sequence);
        if (sequence != null)
        {
            if (sequence.IsActive())
            {
                sequence.Kill();
            }
        }
        DicAdd<Transform, TweenerCore<Vector3, Vector3, VectorOptions>>.Add(_moveDic, target, move);
    }

    public void Clear()
    {
        foreach (var kv in _dic)
        {
            if (kv.Value != null)
            {
                if (kv.Value.IsActive())
                {
                    kv.Value.Kill();
                }
            }
        }
        _dic.Clear();
        
        foreach (var kv in _moveDic)
        {
            if (kv.Value != null)
            {
                if (kv.Value.IsActive())
                {
                    kv.Value.Kill();
                }
            }
        }
        _moveDic.Clear();
    }
}
