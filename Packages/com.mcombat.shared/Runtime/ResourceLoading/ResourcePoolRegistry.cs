using System;
using System.Collections.Generic;

public sealed class ResourcePoolRegistry<TPool> where TPool : class
{
    private readonly IDictionary<string, TPool> pools = new Dictionary<string, TPool>();
    private readonly IDictionary<string, int> preloadCounts = new Dictionary<string, int>();

    public bool TryGet(string key, out TPool pool)
    {
        pool = null;
        return !string.IsNullOrEmpty(key) && pools.TryGetValue(key, out pool) && pool != null;
    }

    public bool Contains(string key)
    {
        return !string.IsNullOrEmpty(key) && pools.ContainsKey(key);
    }

    public void AddOrReplace(string key, TPool pool, int preloadCount = 0)
    {
        if (string.IsNullOrEmpty(key) || pool == null)
            return;

        pools[key] = pool;
        preloadCounts[key] = preloadCount;
    }

    public bool TryAdd(string key, TPool pool, int preloadCount = 0)
    {
        if (string.IsNullOrEmpty(key) || pool == null || pools.ContainsKey(key))
            return false;

        pools.Add(key, pool);
        preloadCounts[key] = preloadCount;
        return true;
    }

    public int IncrementPreloadCount(string key, int initialCount = 0)
    {
        if (string.IsNullOrEmpty(key))
            return initialCount;

        if (!preloadCounts.TryGetValue(key, out var currentCount))
            currentCount = initialCount;

        currentCount++;
        preloadCounts[key] = currentCount;
        return currentCount;
    }

    public void Clear(Action<TPool> clearPool)
    {
        foreach (var pool in pools.Values)
        {
            clearPool?.Invoke(pool);
        }

        pools.Clear();
        preloadCounts.Clear();
    }
}
