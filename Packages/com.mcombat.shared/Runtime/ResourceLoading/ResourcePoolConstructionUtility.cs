using System;
using Cysharp.Threading.Tasks;

public static class ResourcePoolConstructionUtility
{
    public static async UniTask<TPool> GetOrCreatePool<TPool, TPrefab>(
        ResourcePoolRegistry<TPool> registry,
        string key,
        TPrefab prefab,
        int preloadCount,
        Func<TPrefab, TPool> createPool,
        Func<TPool, int, UniTask> preloadPool,
        Action<TPool> clearPool = null)
        where TPool : class
        where TPrefab : class
    {
        if (registry == null || string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (registry.TryGet(key, out var existingPool))
        {
            var nextPreloadCount = registry.IncrementPreloadCount(key, preloadCount);
            await Preload(existingPool, nextPreloadCount, preloadPool);
            return existingPool;
        }

        if (prefab == null || createPool == null)
        {
            return null;
        }

        var newPool = createPool(prefab);
        if (newPool == null)
        {
            return null;
        }

        await Preload(newPool, preloadCount, preloadPool);
        if (registry.TryGet(key, out existingPool))
        {
            clearPool?.Invoke(newPool);
            return existingPool;
        }

        if (registry.TryAdd(key, newPool, preloadCount))
        {
            return newPool;
        }

        clearPool?.Invoke(newPool);
        return registry.TryGet(key, out existingPool) ? existingPool : null;
    }

    private static UniTask Preload<TPool>(TPool pool, int preloadCount, Func<TPool, int, UniTask> preloadPool)
        where TPool : class
    {
        return preloadPool != null ? preloadPool(pool, preloadCount) : default;
    }
}
