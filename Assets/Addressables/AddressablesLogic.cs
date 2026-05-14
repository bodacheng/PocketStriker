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
    private static readonly IDictionary<string, long> Sizes = new Dictionary<string, long>();
    private static readonly IDictionary<string, List<string>> KeyExists = new Dictionary<string, List<string>>();

    public static async UniTask CheckExistedKey(string tag)
    {
        if (KeyExists.ContainsKey(tag))
        {
            return;
        }
        KeyExists.Add(tag, new List<string>());
        var locationHandle = Addressables.LoadResourceLocationsAsync(tag);
        await locationHandle.Task;
        if (locationHandle.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var weapon in locationHandle.Result)
            {
                if (!KeyExists[tag].Contains(weapon.PrimaryKey))
                {
                    KeyExists[tag].Add(weapon.PrimaryKey);
                }
            }
        }
        else
        {
            Debug.Log(" error ");
        }
        Addressables.Release(locationHandle);
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

    public static async UniTask<bool> VersionConfirm() // false : need to update
    {
        await DownLoadMission(AddressablesResourcePolicy.AppVersionKey, (x)=>{});
        AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(AddressablesResourcePolicy.AppVersionKey);
        try
        {
            while (!handle.IsDone)
            {
                await UniTask.DelayFrame(0);
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                return false;
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
        await DownLoadMission(AddressablesResourcePolicy.ConfigLabel, (x)=>{});
    }

    public static async UniTask<CommonSetting> GetCommonSetting()
    {
        AsyncOperationHandle<CommonSetting> handle = Addressables.LoadAssetAsync<CommonSetting>(AddressablesResourcePolicy.CommonSettingKey);
        while (!handle.IsDone)
        {
            await UniTask.DelayFrame(0);
        }
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            CommonSetting commonSetting = handle.Result;
            LoadingHandlerList.Add(handle);
            return commonSetting;
        }
        if (handle.IsValid())
            Addressables.Release(handle);
        return null;
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
    
    static async UniTask<long> DownLoadSize(string label, Action<string> exceptionProcess)
    {
        AsyncOperationHandle<long> handle = default;
        try
        {
            handle = Addressables.GetDownloadSizeAsync(label);
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var result = handle.Result;
                if (result > 0)
                {
                    DicAdd<string,long>.Add(Sizes, label, result);
                }
                return result;
            }
            Debug.LogError(AddressablesResourcePolicy.DownloadSizeFailureMessage(label));
            exceptionProcess?.Invoke(label);
            return 0;
        }
        catch (Exception ex)
        {
            Debug.LogError(AddressablesResourcePolicy.DownloadSizeExceptionMessage(label, ex));
            exceptionProcess?.Invoke(label);
            return 0;
        }
        finally
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
    }
    
    static async UniTask<bool> DownLoadMission(string label, Action<string> progressUIRefresh)
    {
        AsyncOperationHandle downloadHandle = default;
        try
        {
            downloadHandle = Addressables.DownloadDependenciesAsync(label, true);
            while (!downloadHandle.IsDone)
            {
                if (downloadedBytes.ContainsKey(label))
                {
                    downloadedBytes[label] = downloadHandle.GetDownloadStatus().DownloadedBytes;
                }

                progressUIRefresh(AddressablesResourcePolicy.DownloadProgressText(AppSetting.Value.Language));
                await UniTask.DelayFrame(0);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(AddressablesResourcePolicy.DownloadMissionExceptionMessage(ex));
            return false;
        }
        finally
        {
            if (downloadHandle.IsValid())
                Addressables.Release(downloadHandle);
        }
    }
    
    public static async UniTask<long> GetWholeDownLoadSize(Action<string> exception, List<string> downLoadLabel)
    {
        //Caching.ClearCache();
        
        long wholeSize = 0;
        var downLoadSizeCal = new List<UniTask<long>>();
        foreach (var label in downLoadLabel)
        {
            downLoadSizeCal.Add(DownLoadSize(label, exception));
        }
        await UniTask.WhenAll(downLoadSizeCal);

        foreach (var kv in Sizes)
        {
            wholeSize += kv.Value;
        }
        return wholeSize;
    }
    
    public static long DownloadedBytes
    {
        get {
            long whole = 0;
            foreach (var kv in downloadedBytes)
            {
                whole += kv.Value;
            }
            return whole;
        }
    }
    
    private static readonly Dictionary<string, long> downloadedBytes = new Dictionary<string, long>();
    public static async UniTask ResourcePrepareProcess(Action complete, Action<string> progressUIRefresh, List<string> downLoadLabel)
    {
        // Clear all cached AssetBundles
        // WARNING: This will cause all asset bundles to be re-downloaded at startup every time and should not be used in a production game
        //Addressables.ClearDependencyCacheAsync(label);
        //var unitInstructionLayer = UILayerLoader.Load<UnitInstructionLayer>();
        //unitInstructionLayer.LoadUnitImage();
        foreach (var label in downLoadLabel)
        {
            AddressablesResourcePolicy.EnsureDownloadedBytesLabel(downloadedBytes, label);
        }
        
        var downLoadTasks = new List<UniTask<bool>>();
        foreach (var label in downLoadLabel)
        {
            if (Sizes.ContainsKey(label))
                downLoadTasks.Add(DownLoadMission(label, progressUIRefresh));
        }
        var results = await UniTask.WhenAll(downLoadTasks);
        if (results.Any(result => result == false))
        {
            await LoadErrorThenBackToStart();
            return;
        }
        complete.Invoke();
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
