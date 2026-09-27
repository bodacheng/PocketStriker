using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public static class AddressablesAssetLoader
{
    static readonly IDictionary<string, List<string>> IndexedKeys = new Dictionary<string, List<string>>();
    static readonly List<AsyncOperationHandle> RetainedHandles = new List<AsyncOperationHandle>();

    public static async UniTask IndexTag(string tag)
    {
        if (IndexedKeys.ContainsKey(tag))
            return;

        IndexedKeys.Add(tag, new List<string>());
        var handle = Addressables.LoadResourceLocationsAsync(tag);
        try
        {
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"[Addressables] Failed to index tag: {tag}");
                return;
            }

            foreach (var location in handle.Result)
            {
                if (!IndexedKeys[tag].Contains(location.PrimaryKey))
                    IndexedKeys[tag].Add(location.PrimaryKey);
            }
        }
        finally
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
    }

    public static bool HasKey(string tag, string primaryKey)
    {
        return IndexedKeys.TryGetValue(tag, out var keys) && keys.Contains(primaryKey);
    }

    public static bool HasIndexedTag(string tag)
    {
        return IndexedKeys.ContainsKey(tag);
    }

    public static async UniTask<GameObject> LoadObject(
        string prefabPathName,
        Vector3 position,
        Func<string, UniTask> onFailure = null,
        Action<float> onProgress = null)
    {
        var handle = Addressables.InstantiateAsync(prefabPathName, position, Quaternion.identity);
        while (!handle.IsDone)
        {
            onProgress?.Invoke(Mathf.Clamp01(handle.PercentComplete));
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log(AddressablesResourcePolicy.InstantiateFailureMessage(prefabPathName));
            if (handle.IsValid())
                Addressables.ReleaseInstance(handle);
            await InvokeFailure(onFailure, prefabPathName);
            return default;
        }

        onProgress?.Invoke(1f);
        var instance = handle.Result;
        instance.AddOnDestroyCallback(() =>
        {
            if (handle.IsValid())
                Addressables.ReleaseInstance(handle);
        });
        return instance;
    }

    public static async UniTask<T> LoadTOnObject<T>(
        string prefabPathName,
        GameObject memoryReleaseTarget = null,
        CancellationTokenSource cancellationTokenSource = null,
        Func<string, UniTask> onFailure = null)
    {
        AsyncOperationHandle<GameObject> handle = default;
        try
        {
            handle = Addressables.InstantiateAsync(prefabPathName);
            if (cancellationTokenSource != null)
                await handle.ToUniTask(cancellationToken: cancellationTokenSource.Token);
            else
                await handle.Task;

            if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.Log(AddressablesResourcePolicy.InstantiateFailureMessage(prefabPathName));
                if (handle.IsValid())
                    Addressables.ReleaseInstance(handle);
                await InvokeFailure(onFailure, prefabPathName);
                return default;
            }

            var instance = handle.Result;
            var releaseTarget = memoryReleaseTarget != null ? memoryReleaseTarget : instance;
            releaseTarget.AddOnDestroyCallback(() =>
            {
                if (handle.IsValid())
                    Addressables.ReleaseInstance(handle);
            });
            return instance.GetComponent<T>();
        }
        catch (OperationCanceledException)
        {
            if (handle.IsValid())
                Addressables.ReleaseInstance(handle);
            return default;
        }
        catch (Exception exception)
        {
            if (handle.IsValid())
                Addressables.ReleaseInstance(handle);
            Debug.LogWarning(AddressablesResourcePolicy.ExceptionMessage(prefabPathName, exception));
            await InvokeFailure(onFailure, prefabPathName);
            return default;
        }
    }

    public static UniTask<T> LoadT<T>(
        string prefabPathName,
        GameObject memoryReleaseTarget = null,
        Func<string, UniTask> onFailure = null)
    {
        return LoadAsset(
            () => Addressables.LoadAssetAsync<T>(prefabPathName),
            prefabPathName,
            memoryReleaseTarget,
            onFailure);
    }

    public static UniTask<T> LoadT<T>(
        IResourceLocation location,
        GameObject memoryReleaseTarget = null,
        Func<string, UniTask> onFailure = null)
    {
        return LoadAsset(
            () => Addressables.LoadAssetAsync<T>(location),
            location?.PrimaryKey,
            memoryReleaseTarget,
            onFailure);
    }

    public static void ReleaseRetainedHandles()
    {
        foreach (var handle in RetainedHandles)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        RetainedHandles.Clear();
    }

    static async UniTask<T> LoadAsset<T>(
        Func<AsyncOperationHandle<T>> startLoad,
        string key,
        GameObject memoryReleaseTarget,
        Func<string, UniTask> onFailure)
    {
        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = startLoad();
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                await InvokeFailure(onFailure, key);
                return default;
            }

            if (memoryReleaseTarget == null)
            {
                RetainedHandles.Add(handle);
            }
            else
            {
                memoryReleaseTarget.AddOnDestroyCallback(() =>
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                });
            }
            return handle.Result;
        }
        catch (Exception exception)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            Debug.LogWarning(AddressablesResourcePolicy.ExceptionMessage(key, exception));
            await InvokeFailure(onFailure, key);
            return default;
        }
    }

    static UniTask InvokeFailure(Func<string, UniTask> onFailure, string key)
    {
        return onFailure != null ? onFailure(key) : UniTask.CompletedTask;
    }
}
