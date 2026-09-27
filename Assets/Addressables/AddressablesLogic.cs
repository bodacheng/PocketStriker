using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;

public static class AddressablesLogic
{
    private static readonly IDictionary<string, List<string>> KeyExists = new Dictionary<string, List<string>>();
    private static AsyncOperationHandle<CommonSetting> commonSettingHandle;

    public static async UniTask CheckExistedKey(string tag)
    {
        if (KeyExists.ContainsKey(tag))
        {
            return;
        }
        var locationHandle = Addressables.LoadResourceLocationsAsync(tag);
        try
        {
            await locationHandle.Task;
            if (locationHandle.Status != AsyncOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"Failed to index resource label: {tag}", locationHandle.OperationException);
            }
            KeyExists[tag] = locationHandle.Result.Select(location => location.PrimaryKey).Distinct().ToList();
        }
        finally
        {
            if (locationHandle.IsValid())
                Addressables.Release(locationHandle);
        }
    }

    public static bool CheckKeyExist(string tag, string primaryKey)
    {
        if (!KeyExists.ContainsKey(tag))
        {
            return false;
        }
        return KeyExists[tag].Contains(primaryKey);
    }

    public static bool HasIndexedTag(string tag)
    {
        return KeyExists.ContainsKey(tag);
    }

    public static async UniTask<bool> VersionConfirm()
    {
        if (!await DownLoadMission(AddressablesResourcePolicy.AppVersionKey, (x)=>{}))
        {
            throw new InvalidOperationException("Failed to download the app version configuration.");
        }
        AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(AddressablesResourcePolicy.AppVersionKey);
        try
        {
            while (!handle.IsDone)
            {
                await UniTask.DelayFrame(0);
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                throw new InvalidOperationException("Failed to load the app version configuration.", handle.OperationException);
            }

            var appVersionJson = handle.Result;
            var jsonNode = JObject.Parse(appVersionJson.text);
            var serverVersion = jsonNode[AddressablesResourcePolicy.VersionJsonProperty]?.ToString();
            var currentVersion = Application.version;
            Debug.Log("currentVersion:" + currentVersion);
            Debug.Log("serverVersion:" + serverVersion);

            return AddressablesResourcePolicy.IsServerVersionNewer(currentVersion, serverVersion);
        }
        finally
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
    }

    public static async UniTask DownLoadConfig()
    {
        if (!await DownLoadMission(AddressablesResourcePolicy.ConfigLabel, (x)=>{}))
        {
            throw new InvalidOperationException("Failed to download the game configuration.");
        }
    }

    public static async UniTask<CommonSetting> GetCommonSetting()
    {
        AsyncOperationHandle<CommonSetting> handle = Addressables.LoadAssetAsync<CommonSetting>(AddressablesResourcePolicy.CommonSettingKey);
        try
        {
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                throw new InvalidOperationException("Failed to load the common game settings.", handle.OperationException);
            }
            // CommonSetting owns audio and material references used across scene changes.
            // Keep its dependency bundle alive when temporary combat assets are released.
            if (commonSettingHandle.IsValid())
                Addressables.Release(commonSettingHandle);
            commonSettingHandle = handle;
            return handle.Result;
        }
        catch
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            throw;
        }
    }
    
    public static async UniTask Essentials()
    {
        await UniTask.WhenAll(AddressablesResourcePolicy.FullCombatEssentialLabels.Select(CheckExistedKey));
    }

    static async UniTask HandleLoadFailure<T>(string key)
    {
        Debug.LogWarning(AddressablesResourcePolicy.LoadFailureMessage(key));
        if (AddressablesResourcePolicy.ShouldReturnToStartOnLoadFailure<T>())
        {
            await LoadErrorThenBackToStart();
        }
    }
    
    static UniTask<bool> DownLoadMission(string label, Action<string> progressUIRefresh)
    {
        return AddressablesDependencyDownloader.DownloadDependencies(
            label,
            progressUIRefresh,
            AddressablesResourcePolicy.DownloadProgressText(AppSetting.Value.Language));
    }
    
    public static UniTask<long> GetWholeDownLoadSize(Action<string> exception, List<string> downLoadLabel)
    {
        return AddressablesDependencyDownloader.GetWholeDownloadSize(downLoadLabel, exception);
    }
    
    public static long DownloadedBytes => AddressablesDependencyDownloader.DownloadedBytes;
    
    public static async UniTask ResourcePrepareProcess(Action complete, Action<string> progressUIRefresh, List<string> downLoadLabel)
    {
        var success = await AddressablesDependencyDownloader.DownloadRequiredDependencies(
            downLoadLabel,
            progressUIRefresh,
            AddressablesResourcePolicy.DownloadProgressText(AppSetting.Value.Language));

        if (!success)
        {
            throw new InvalidOperationException("Failed to download required game resources.");
        }
        complete?.Invoke();
    }
    
    public static async UniTask<GameObject> LoadObject(string prefabPathName, Vector3 pos = new Vector3())
    {
        var handle = Addressables.InstantiateAsync(prefabPathName, pos, Quaternion.identity);
        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log(AddressablesResourcePolicy.InstantiateFailureMessage(prefabPathName));
            Addressables.ReleaseInstance(handle);
            await LoadErrorThenBackToStart();
            return default;
        }
        else
        {
            var _object = handle.Result; // インスタンス化されたもの
            _object.AddOnDestroyCallback( () =>
            {
                Addressables.ReleaseInstance(handle);
            });
            return _object;
        }
    }
    
    public static async UniTask<T> LoadTOnObject<T>(string prefabPathName)
    {
        var handle = Addressables.InstantiateAsync(prefabPathName);
        await handle.Task;
        if (handle.IsValid() && handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log(AddressablesResourcePolicy.InstantiateFailureMessage(prefabPathName));
            Addressables.ReleaseInstance(handle);
            await LoadErrorThenBackToStart();
            return default;
        }
        else
        {
            if (!handle.IsValid())
            {
                return default;
            }
            var _object = handle.Result; // インスタンス化されたもの
            _object.AddOnDestroyCallback( () =>
            {
                Addressables.ReleaseInstance(handle);
            });
            var returnValue = _object.GetComponent<T>();
            return returnValue;
        }
    }
    
    public static async UniTask<T> LoadTOnObject<T>(string prefabPathName, GameObject memoryReleaseTarget = null, CancellationTokenSource _cancellationTokenSource = null)
    {
        AsyncOperationHandle<GameObject> handle = default;
        try
        {
            handle = Addressables.InstantiateAsync(prefabPathName);
            if (_cancellationTokenSource != null)
            {
                await handle.ToUniTask(cancellationToken: _cancellationTokenSource.Token);
            }
            else
            {
                await handle.Task;
            }

            if (handle.IsValid() && handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.Log(AddressablesResourcePolicy.InstantiateFailureMessage(prefabPathName));
                Addressables.ReleaseInstance(handle);
                await LoadErrorThenBackToStart();
                return default;
            }
            else
            {
                var _object = handle.Result; // インスタンス化されたもの
                if (memoryReleaseTarget == null)
                {
                    _object.AddOnDestroyCallback( () =>
                    {
                        Addressables.ReleaseInstance(handle);
                    });
                }
                else
                {
                    memoryReleaseTarget.AddOnDestroyCallback( () =>
                    {
                        Addressables.ReleaseInstance(handle);
                    });
                }
                var returnValue = _object.GetComponent<T>();
                return returnValue;
            }
        }
        catch (OperationCanceledException)
        {
            if (handle.IsValid())
                Addressables.ReleaseInstance(handle);
        }
        catch (Exception e)
        {
            if (handle.IsValid())
                Addressables.ReleaseInstance(handle);
            Debug.LogWarning(AddressablesResourcePolicy.ExceptionMessage(prefabPathName, e));
            await LoadErrorThenBackToStart();
        }
        return default;
    }

    private static readonly List<AsyncOperationHandle> LoadingHandlerList = new List<AsyncOperationHandle>();
    
    public static async UniTask<T> LoadT<T>(string prefabPathName, GameObject memoryReleaseTarget = null)
    {
        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(prefabPathName);
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                await HandleLoadFailure<T>(prefabPathName);
                return default;
            }
            if (memoryReleaseTarget == null)
            {
                LoadingHandlerList.Add(handle);
            }
            else
            {
                memoryReleaseTarget.AddOnDestroyCallback( () =>
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                });
            }
            return handle.Result;
        }
        catch (Exception e)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            Debug.LogWarning(AddressablesResourcePolicy.ExceptionMessage(prefabPathName, e));
            await HandleLoadFailure<T>(prefabPathName);
            return default;
        }
    }
    
    public static async UniTask<T> LoadT<T>(IResourceLocation location, GameObject memoryReleaseTarget = null)
    {
        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(location);
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                await HandleLoadFailure<T>(location?.PrimaryKey);
                return default;
            }
            if (memoryReleaseTarget == null)
            {
                LoadingHandlerList.Add(handle);
            }
            else
            {
                memoryReleaseTarget.AddOnDestroyCallback( () =>
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                });
            }
            return handle.Result;
        }
        catch (Exception e)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            Debug.LogWarning(AddressablesResourcePolicy.ExceptionMessage(location?.PrimaryKey, e));
            await HandleLoadFailure<T>(location?.PrimaryKey);
            return default;
        }
    }
    
    public static void ReleaseAsyncOperationHandles()
    {
        foreach (var handle in LoadingHandlerList)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        LoadingHandlerList.Clear();
        AddressablesAssetLoader.ReleaseRetainedHandles();
    }

    static async UniTask LoadErrorThenBackToStart()
    {
        ProgressLayer.Loading("download error");
        await UniTask.Delay(TimeSpan.FromSeconds(2));
        if (SceneManager.GetActiveScene().buildIndex != 0)
        {
            SceneManager.LoadScene(0);
        }
    }
}
