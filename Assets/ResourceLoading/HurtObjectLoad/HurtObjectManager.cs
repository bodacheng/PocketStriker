using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class HurtObjectManager
{
    static DecompositionPool _defaultHitBoxPool;
    static readonly ResourcePoolRegistry<DecompositionPool> HurtPools = new ResourcePoolRegistry<DecompositionPool>();
    static readonly HashSet<string> ReadyPools = new HashSet<string>();
    static int cacheVersion;
    static readonly IDictionary<string, UniTaskCompletionSource<bool>> PendingPoolLoads =
        new Dictionary<string, UniTaskCompletionSource<bool>>();
    
    static UniTask<GameObject> TryLoadWeaponPrefab(string key)
    {
        return IndexedResourceLoadUtility.LoadIfKeyExists<GameObject>(
            EffectResourceKeyUtility.WeaponLabel,
            key,
            AddressablesLogic.CheckKeyExist,
            assetKey => AddressablesLogic.LoadT<GameObject>(assetKey));
    }
    
    public static DecompositionPool GetDPool()
    {
        return _defaultHitBoxPool;
    }
    
    public static void Clear()
    {
        cacheVersion++;
        var pending = new List<UniTaskCompletionSource<bool>>(PendingPoolLoads.Values);
        PendingPoolLoads.Clear();
        ReadyPools.Clear();
        _defaultHitBoxPool?.Clear();
        _defaultHitBoxPool = null;
        HurtPools.Clear(pool => pool.Clear());
        foreach (var source in pending)
            source.TrySetCanceled();
    }
    
    // 默认攻击物件池的创建
    public static async UniTask ConstructDPool()
    {
        var version = cacheVersion;
        _defaultHitBoxPool?.Clear();
        _defaultHitBoxPool = null;
        DecompositionPool createdPool = null;
        try
        {
            var resultObject = await AddressablesLogic.LoadT<GameObject>(
                EffectResourceKeyUtility.DefaultHitBoxAddress(FightGlobalSetting.EffectPathDefine()));
            if (version != cacheVersion)
                throw new OperationCanceledException("Default hitbox cache was cleared during loading.");
            if (resultObject == null)
                return;
            createdPool = new DecompositionPool(resultObject);
            await createdPool.PreloadAsync(10, 1).ToUniTask();
            if (version != cacheVersion)
                throw new OperationCanceledException("Default hitbox cache was cleared during prewarming.");
            _defaultHitBoxPool = createdPool;
        }
        catch
        {
            createdPool?.Clear();
            throw;
        }
    }

    public static UniTask ConstructHurtObjectPool(string resourceName, Element element, int preloadCount)
    {
        return ConstructHurtObjectPool(resourceName, element, preloadCount, false);
    }

    static async UniTask ConstructHurtObjectPool(string resourceName, Element element, int preloadCount, bool isAttachment)
    {
        var defaultEffectPath = FightGlobalSetting.EffectPathDefine();
        foreach (var resourcePath in EffectResourceKeyUtility.ResourcePathFallbacks(
                     FightGlobalSetting.EffectPathDefine(element),
                     defaultEffectPath))
        {
            if (await TryConstructHurtObjectPoolAtPath(resourceName, resourcePath, element, preloadCount, isAttachment))
            {
                return;
            }
        }
    }

    static async UniTask<bool> TryConstructHurtObjectPoolAtPath(string resourceName, string resourcePath, Element element, int preloadCount, bool isAttachment)
    {
        var resourceKey = EffectResourceKeyUtility.ResourceKey(resourcePath, resourceName);
        // A constructed parent can satisfy a back-edge in an attachment cycle.
        // External callers only accept the pool after all dependencies succeeded.
        if (ReadyPools.Contains(resourceKey) || (isAttachment && PendingPoolLoads.ContainsKey(resourceKey) && HurtPools.TryGet(resourceKey, out _)))
            return true;

        if (PendingPoolLoads.TryGetValue(resourceKey, out var pendingLoad))
            return await pendingLoad.Task;

        var version = cacheVersion;
        DecompositionPool createdPool = null;
        var loadSource = new UniTaskCompletionSource<bool>();
        PendingPoolLoads.Add(resourceKey, loadSource);
        try
        {
            var weaponPrefab = await TryLoadWeaponPrefab(EffectResourceKeyUtility.PrefabAddress(resourcePath, resourceName));
            if (version != cacheVersion)
                throw new OperationCanceledException("Weapon cache was cleared during loading.");
            if (weaponPrefab == null)
            {
                loadSource.TrySetResult(false);
                return false;
            }

            await ResourcePoolConstructionUtility.GetOrCreatePool(
                HurtPools,
                resourceKey,
                weaponPrefab,
                preloadCount,
                prefab => createdPool = new DecompositionPool(prefab),
                async (pool, count) =>
                {
                    await pool.PreloadAsync(count, 1).ToUniTask();
                    if (version != cacheVersion)
                        throw new OperationCanceledException("Weapon cache was cleared during prewarming.");
                },
                pool => pool.Clear());
            await ConstructAttachmentPools(resourceName, weaponPrefab.GetComponent<Decomposition>(), element, preloadCount);
            if (version != cacheVersion)
                throw new OperationCanceledException("Weapon cache was cleared while preparing attachments.");
            ReadyPools.Add(resourceKey);
            loadSource.TrySetResult(true);
            return true;
        }
        catch (Exception exception)
        {
            if (createdPool != null &&
                (!HurtPools.TryGet(resourceKey, out var registeredPool) || !ReferenceEquals(registeredPool, createdPool)))
                createdPool.Clear();
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

    static async UniTask ConstructAttachmentPools(string resourceName, Decomposition decomposition, Element element, int preloadCount)
    {
        if (decomposition == null)
        {
            Debug.Log(resourceName + "没有Decompositioner！？");
            return;
        }

        if (decomposition.Attachments == null || decomposition.Attachments.Length == 0)
            return;

        for (var i = 0; i < decomposition.Attachments.Length; i++)
        {
            await ConstructHurtObjectPool(decomposition.Attachments[i], element, preloadCount, true);
        }
    }
    
    static DecompositionPool _hurtObjectPool;
    public static DecompositionPool GetHurtObjectPool(string resource_name, string myDefaultMagicPath)
    {
        _hurtObjectPool = null;
        
        foreach (var resourcePath in EffectResourceKeyUtility.ResourcePathFallbacks(
                     myDefaultMagicPath,
                     FightGlobalSetting.EffectPathDefine()))
        {
            var resourceKey = EffectResourceKeyUtility.ResourceKey(resourcePath, resource_name);
            if (HurtPools.TryGet(resourceKey, out _hurtObjectPool))
            {
                return _hurtObjectPool;
            }
        }
        return null;
    }
}
