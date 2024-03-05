using System.Collections.Generic;
using UnityEngine;
using HittingDetection;

public class HitBoxesProcesser : MonoBehaviour
{
    public static HitBoxesProcesser Instance;
    
    private static readonly Dictionary<Collider, HitBoxManager> ColliderHitBox = new Dictionary<Collider, HitBoxManager>();
    private readonly List<Decomposition> _processingDecompositions = new List<Decomposition>();
    
    void Awake()
    {
        Instance = this;
    }

    public void Clear()
    {
        _processingDecompositions.Clear();
    }

    public HitBoxManager GetHitBox(Collider c)
    {
        ColliderHitBox.TryGetValue(c, out var hitBox);
        return hitBox;
    }
    
    public static void AddToDecompositionProcessorList(Decomposition poolObject)
    {
        if (Instance != null)
            Instance.AddToHitBoxesProcessorList(poolObject);
    }
    
    // 用于靠collider索引对应的BO_Marker_Manager，与update内功能无关。
    public static void AddToColliderHitBoxDic(Collider collider, HitBoxManager boHitbox)
    {
        if (!ColliderHitBox.ContainsKey(collider))
        {
            ColliderHitBox.Add(collider, boHitbox);
        }
    }

    void Update()
    {
        if (_processingDecompositions.Count > 0)
        {
            for (var i = 0; i < _processingDecompositions.Count; i++)
            {
                _processingDecompositions[i].Step1();
            }
            for (var i = 0; i < _processingDecompositions.Count; i++)
            {
                _processingDecompositions[i].Step2();
            }
            for (var i = 0; i < _processingDecompositions.Count; i++)
            {
                _processingDecompositions[i].Life();
            }
            _processingDecompositions.Clear();
        }
    }

    void AddToHitBoxesProcessorList(Decomposition poolObject)
    {
        if (!_processingDecompositions.Contains(poolObject))
            _processingDecompositions.Add(poolObject);
    }
}
