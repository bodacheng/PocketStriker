using UnityEngine.Animations;
using UnityEngine;
using UniRx.Toolkit;
using HittingDetection;
using System;
using Log;

public class DecompositionPool : ObjectPool<Decomposition> {

    static GameObject Marker;
    readonly GameObject Prefab;

    static void EnsureMarker()
    {
        if (Marker == null)
        {
            Marker = new GameObject("Object Pools Container");
        }
    }

    public DecompositionPool(GameObject prefab)
    {
        EnsureMarker();
        Prefab = prefab;
    }
    
    protected override void OnBeforeReturn(Decomposition instance)
    {
        if (FightGlobalSetting.HitBoxLogger)
        {
            if (instance.IsWeapon)
            {
                HitBoxLogger.Instance.AddLog(instance._HitBox.GeneratedByStateKey, instance._HitBox.HitBoxLifeEnding);
                instance._HitBox.GeneratedByStateKey = null;
            }
        }
        instance.Phase = 0;
        base.OnBeforeReturn(instance);
    }

    protected override void OnBeforeRent(Decomposition instance)
    {
        base.OnBeforeRent(instance);
        instance.OnEnableProcess();
    }
    
    // オブジェクトが空のときにInstantiateする関数
    protected override Decomposition CreateInstance()
    {
        EnsureMarker();
        var a = UnityEngine.Object.Instantiate(Prefab);
        if (a == null)
        {
            throw new InvalidOperationException($"Could not instantiate pooled prefab: {Prefab}");
        }
        var decomposition = a.GetComponent<Decomposition>();
        if (decomposition == null)
        {
            UnityEngine.Object.Destroy(a);
            throw new InvalidOperationException($"Pooled prefab has no Decomposition component: {Prefab.name}");
        }

        a.transform.SetParent(Marker.transform);
        
        var bbmm = a.GetComponent<HitBoxManager>();
        var danMuTest = a.GetComponent<TrackControl>();
        var PC = a.GetComponent<PositionConstraint>();
        if (PC == null)
        {
            PC = a.AddComponent<PositionConstraint>();
            PC.translationOffset = Vector3.zero;
            PC.weight = 1;
        }
        
        var RG = a.GetComponent<Rigidbody>();//不加刚体的话很多情况下collider的检测类物理函数检测不到
        if (RG == null)
        {
            RG = a.AddComponent<Rigidbody>();
        }
        RG.isKinematic = true;//这个刚体不受物理影响
        
        decomposition.AudioSource = decomposition.transform.GetComponent<AudioSource>();
        if (decomposition.AudioSource != null)
        {
            decomposition.AudioSource.playOnAwake = false;
            decomposition.AudioSource.minDistance = 20;
            decomposition.AudioSource.maxDistance = 80;
        }
        
        if (bbmm != null)
        {
            bbmm.CurrentHP = bbmm.weaponHP;
            decomposition._HitBox = bbmm;
        }
        decomposition.IsWeapon = decomposition._HitBox != null;
        decomposition.SetPositionConstraint(PC);
        decomposition.TrackControl = danMuTest;
        decomposition.SetPool(this);
        return decomposition;
    }
}
