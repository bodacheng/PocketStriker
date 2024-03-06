using UnityEngine.Animations;
using UnityEngine;
using UniRx.Toolkit;
using HittingDetection;
using System;
using System.Collections.Generic;
using Log;

public class DecompositionPool : ObjectPool<Decomposition> {

    static GameObject Marker;
    readonly GameObject Prefab;

    public DecompositionPool(GameObject prefab)
    {
        if (Marker == null)
        {
            Marker = new GameObject("Object Pools Container");
        }
        Prefab = prefab;
    }
    
    /// <summary>
    /// Return instance to pool.
    /// </summary>
    public override void Return(Decomposition instance)
    {
        if (isDisposed) throw new ObjectDisposedException("ObjectPool was already disposed.");
        if (instance == null) throw new ArgumentNullException("instance");
        if (q == null) q = new List<Decomposition>();       
        if ((q.Count + 1) == MaxPoolCount)
        {
            throw new InvalidOperationException("Reached Max PoolSize");
        }
        OnBeforeReturn(instance);
        if (!q.Contains(instance))
            q.Add(instance);
        else{
            Debug.Log(" 邪门了："+ instance);
        }
    }

    /// <summary>
    /// Get instance from pool.
    /// </summary>
    public override Decomposition Rent()
    {
        if (isDisposed) throw new ObjectDisposedException("ObjectPool was already disposed.");
        Decomposition instance = null;
        if (q.Count > 0)
        {
            instance = q[0];
        }
        if (instance == null)
        {
            instance = CreateInstance();
        }else{
            q.Remove(instance);
        }
        OnBeforeRent(instance);
        return instance;
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
        var a = UnityEngine.Object.Instantiate(Prefab);
        if (a == null)
        {
            Debug.Log("逻辑问题"+ Prefab);
            return null;
        }
        var decomposition = a.GetComponent<Decomposition>();
        if (decomposition == null)
        {
            Debug.Log("decomposition："+ Prefab.name);
            return null;
        }

        if (Marker == null)
        {
            Debug.Log("特效物体的parent已经被销毁？");
            GameObject.Destroy(a);
            return null;
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
            decomposition.AudioSource.volume = AppSetting.Value.EffectsVolume;
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
