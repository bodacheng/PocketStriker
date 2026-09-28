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
    
    public static UniTask<GameObject> LoadObject(
        string prefabPathName,
        Vector3 pos = default,
        Action<float> onProgress = null)
    {
        return AddressablesAssetLoader.LoadObject(
            prefabPathName,
            pos,
            _ => LoadErrorThenBackToStart(),
            onProgress);
    }

    public static UniTask<T> LoadTOnObject<T>(string prefabPathName)
    {
        return AddressablesAssetLoader.LoadTOnObject<T>(
            prefabPathName,
            onFailure: _ => LoadErrorThenBackToStart());
    }

    public static UniTask<T> LoadTOnObject<T>(
        string prefabPathName,
        GameObject memoryReleaseTarget = null,
        CancellationTokenSource _cancellationTokenSource = null)
    {
        return AddressablesAssetLoader.LoadTOnObject<T>(
            prefabPathName,
            memoryReleaseTarget,
            _cancellationTokenSource,
            _ => LoadErrorThenBackToStart());
    }

    public static UniTask<T> LoadT<T>(string prefabPathName, GameObject memoryReleaseTarget = null)
    {
        return AddressablesAssetLoader.LoadT<T>(
            prefabPathName,
            memoryReleaseTarget,
            HandleLoadFailure<T>);
    }

    public static UniTask<T> LoadT<T>(IResourceLocation location, GameObject memoryReleaseTarget = null)
    {
        return AddressablesAssetLoader.LoadT<T>(
            location,
            memoryReleaseTarget,
            HandleLoadFailure<T>);
    }

    public static void ReleaseAsyncOperationHandles()
    {
        AddressablesAssetLoader.ReleaseRetainedHandles();
        AudioResourceLoading.Clear();
    }

    static async UniTask LoadErrorThenBackToStart()
    {
        // The launcher owns its failure popup while scene 0 is active.
        if (SceneManager.GetActiveScene().buildIndex == 0)
            return;

        ProgressLayer.Loading("download error");
        await UniTask.Delay(TimeSpan.FromSeconds(2));
        SceneManager.LoadScene(0);
    }
}
