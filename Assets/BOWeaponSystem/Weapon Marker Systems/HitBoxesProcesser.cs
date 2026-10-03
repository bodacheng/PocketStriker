using System.Collections.Generic;
using UnityEngine;
using HittingDetection;

public class HitBoxesProcesser : MonoBehaviour
{
    public static HitBoxesProcesser Instance;
    
    private static readonly Dictionary<Collider, HitBoxManager> ColliderHitBox = new Dictionary<Collider, HitBoxManager>();
    private readonly List<Decomposition> _processingDecompositions = new List<Decomposition>(64);
    private readonly HashSet<Decomposition> _processingMembership = new HashSet<Decomposition>();
    
    void Awake()
    {
        Instance = this;
    }

    public void Clear()
    {
        BattleEffectLifetime.InvalidateAll();
        _processingDecompositions.Clear();
        _processingMembership.Clear();
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

    public void AllProcessingFade()
    {
        BattleEffectLifetime.InvalidateAll();
    }

    void Update()
    {
        if (_processingDecompositions.Count > 0)
        {
            if (!Physics.autoSyncTransforms)
            {
                foreach (var item in _processingDecompositions)
                {
                    if (item != null && item.RequiresTransformSyncBeforePhysicsQuery)
                    {
                        Physics.SyncTransforms();
                        break;
                    }
                }
            }
            for (var i = 0; i < _processingDecompositions.Count; i++)
            {
                if (_processingDecompositions[i] != null)
                    _processingDecompositions[i].Step1();
            }
            for (var i = 0; i < _processingDecompositions.Count; i++)
            {
                if (_processingDecompositions[i] != null)
                    _processingDecompositions[i].Step2();
            }
            for (var i = 0; i < _processingDecompositions.Count; i++)
            {
                if (_processingDecompositions[i] != null)
                    _processingDecompositions[i].Life();
            }
            _processingDecompositions.Clear();
            _processingMembership.Clear();
        }
    }

    void AddToHitBoxesProcessorList(Decomposition poolObject)
    {
        if (poolObject != null && _processingMembership.Add(poolObject))
            _processingDecompositions.Add(poolObject);
    }
}
