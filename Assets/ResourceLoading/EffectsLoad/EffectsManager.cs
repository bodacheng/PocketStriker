using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;

public static class EffectsManager
{
    // 以下的重点是主界面和战斗界面通用问题
    static readonly ResourcePoolRegistry<DecompositionPool> EffectPools = new ResourcePoolRegistry<DecompositionPool>();
    static readonly IDictionary<string, UniTaskCompletionSource<DecompositionPool>> PendingPoolLoads =
        new Dictionary<string, UniTaskCompletionSource<DecompositionPool>>();
    
    static UniTask<GameObject> TryLoadEffectPrefab(string key)
    {
        return IndexedResourceLoadUtility.LoadIfKeyExists<GameObject>(
            EffectResourceKeyUtility.EffectLabel,
            key,
            AddressablesLogic.CheckKeyExist,
            assetKey => AddressablesLogic.LoadT<GameObject>(assetKey));
    }
    
    static int cacheVersion;
    public static void Clear()
    {
        cacheVersion++;
        var pending = new List<UniTaskCompletionSource<DecompositionPool>>(PendingPoolLoads.Values);
        PendingPoolLoads.Clear();
        EffectPools.Clear(pool => pool.Clear());
        foreach (var source in pending)
            source.TrySetCanceled();
    }
    
    public static async UniTask<Decomposition> GenerateEffect(string resourceName, string effectPath, Vector3 pos, Quaternion qua, Transform parentT)
    {
        if (string.IsNullOrEmpty(resourceName))
            return default;
        var effectPool = await IniEffectsPool(resourceName, effectPath, 3);
        if (effectPool == null)
            return default;
        var processingEffectObj = effectPool.Rent();
        var myConstraintSource = new ConstraintSource();
        if (parentT != null)
        {
            myConstraintSource.sourceTransform = parentT;
            myConstraintSource.weight = 1;
            processingEffectObj.GetPositionConstraint().SetSources(new List<ConstraintSource> { myConstraintSource });
            processingEffectObj.GetPositionConstraint().locked = true;
            processingEffectObj.GetPositionConstraint().translationOffset = Vector3.zero;
            processingEffectObj.GetPositionConstraint().constraintActive = true;
        }else{
            myConstraintSource.weight = 0;
            processingEffectObj.GetPositionConstraint().constraintActive = false;
        }
        processingEffectObj.transform.position = pos;
        processingEffectObj.transform.rotation = qua;
        return processingEffectObj;
    }
    
    public static async UniTask<DecompositionPool> IniEffectsPool(string resourceName, string effectPath, int objectCount)
    {
        if (string.IsNullOrEmpty(resourceName))
            return null;
        foreach (var path in EffectResourceKeyUtility.ResourcePathFallbacks(effectPath, FightGlobalSetting.EffectPathDefine()))
        {
            var effectPool = await LoadPool(resourceName, path, objectCount);
            if (effectPool != null)
                return effectPool;
        }
        return null;
    }

    static async UniTask<DecompositionPool> LoadPool(string resourceName, string effectPath, int objectCount)
    {
        var resourceKey = EffectResourceKeyUtility.ResourceKey(effectPath, resourceName);
        if (EffectPools.TryGet(resourceKey, out var effectPool))
            return effectPool;
        if (PendingPoolLoads.TryGetValue(resourceKey, out var pendingLoad))
            return await pendingLoad.Task;

        var version = cacheVersion;
        DecompositionPool createdPool = null;
        var loadSource = new UniTaskCompletionSource<DecompositionPool>();
        PendingPoolLoads.Add(resourceKey, loadSource);
        try
        {
            var effectPrefab = await TryLoadEffectPrefab(EffectResourceKeyUtility.PrefabAddress(effectPath, resourceName));
            if (version != cacheVersion)
                throw new OperationCanceledException("Effect cache was cleared during loading.");
            if (effectPrefab != null)
            {
                effectPool = await ResourcePoolConstructionUtility.GetOrCreatePool(
                    EffectPools,
                    resourceKey,
                    effectPrefab,
                    objectCount,
                    prefab => createdPool = new DecompositionPool(prefab),
                    async (pool, count) =>
                    {
                        await pool.PreloadAsync(count, 1).ToUniTask();
                        if (version != cacheVersion)
                            throw new OperationCanceledException("Effect cache was cleared during prewarming.");
                    },
                    pool => pool.Clear());
            }
            loadSource.TrySetResult(effectPool);
            return effectPool;
        }
        catch (Exception exception)
        {
            createdPool?.Clear();
            if (exception is OperationCanceledException canceled)
                loadSource.TrySetCanceled(canceled.CancellationToken);
            else
                loadSource.TrySetException(exception);
            return await loadSource.Task;
        }
        finally
        {
            if (PendingPoolLoads.TryGetValue(resourceKey, out var current) && ReferenceEquals(current, loadSource))
                PendingPoolLoads.Remove(resourceKey);
        }
    }
}
