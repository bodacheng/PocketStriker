using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Newtonsoft.Json.Linq;

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

    public static async UniTask<bool> VersionConfirm() // false : need to update
    {
        bool needToUpdate = false;
        await DownLoadMission("app_version", (x)=>{});
        AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>("app_version");
        while (!handle.IsDone)
        {
            await UniTask.DelayFrame(0);
        }
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            TextAsset appVersionJson = handle.Result;
            ParseVersion(appVersionJson.text);
        }
        
        void ParseVersion(string jsonText)
        {
            JObject jsonNode = JObject.Parse(jsonText);
            string serverVersion = jsonNode["version"].ToString();
            CompareVersions(serverVersion);
        }
        
        void CompareVersions(string serverVersion)
        {
            string currentVersion = Application.version;
            Debug.Log("currentVersion:" + currentVersion);
            Debug.Log("serverVersion:" + serverVersion);
            
            string[] serverVersionParts = serverVersion.Split('.');
            string[] currentVersionParts = currentVersion.Split('.');
            
            for (int i = 0; i < serverVersionParts.Length && i < currentVersionParts.Length; i++)
            {
                int serverVersionPart = int.Parse(serverVersionParts[i]);
                int currentVersionPart = int.Parse(currentVersionParts[i]);

                if (serverVersionPart > currentVersionPart)
                {
                    needToUpdate = true;
                    break;
                }
                else if (serverVersionPart < currentVersionPart)
                {
                    needToUpdate = false;
                    break;
                }
            }
        }
        Addressables.Release(handle);
        return needToUpdate;
    }

    public static async UniTask DownLoadConfig()
    {
        await DownLoadMission("config", (x)=>{});

    }

    public static async UniTask<CommonSetting> GetCommonSetting()
    {
        AsyncOperationHandle<CommonSetting> handle = Addressables.LoadAssetAsync<CommonSetting>("Config/commonSetting");
        while (!handle.IsDone)
        {
            await UniTask.DelayFrame(0);
        }
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            CommonSetting commonSetting = handle.Result;
            return commonSetting;
        }
        return null;
    }
    
    public static async UniTask Essentials()
    {
        await UniTask.WhenAll(new List<UniTask>()
        {
            CheckExistedKey("weapon"),
            CheckExistedKey("effect"),
            CheckExistedKey("unit_image")
        });
    }
    
    static async UniTask<long> DownLoadSize(string label, Action exceptionProcess)
    {
        var handle = Addressables.GetDownloadSizeAsync(label);
        await handle.Task;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            var result = handle.Result;
            if (result > 0)
            {
                DicAdd<string,long>.Add(Sizes, label, result);
            }
            Addressables.Release(handle);
            return result;
        }
        else
        {
            Debug.Log($"Failed to get size : {label}");
            Addressables.Release(handle);
            exceptionProcess.Invoke();
            return default;
        }
    }
    
    static async UniTask DownLoadMission(string label, Action<string> progressUIRefresh)
    {
        var handle = Addressables.DownloadDependenciesAsync(label);
        while (!handle.IsDone)
        {
            if (downloadedBytes.ContainsKey(label))
            {
                downloadedBytes[label] = handle.GetDownloadStatus().DownloadedBytes;
            }

            var text = string.Empty;
            switch (AppSetting.Value.Language)
            {
                case SystemLanguage.English:
                    text = "Downloading Assets";
                    break;
                case SystemLanguage.Japanese:
                    text = "リソースをダウンロード中です";
                    break;
                case SystemLanguage.Chinese:
                    text = "正在下载资源";
                    break;
                default:
                    text = "リソースをダウンロード中です";
                    break;
            }
            progressUIRefresh(text);
            await UniTask.DelayFrame(0);
        }
        Addressables.Release(handle);
    }
    
    public static async UniTask<long> GetWholeDownLoadSize(Action exception, List<string> downLoadLabel)
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
            if (!downloadedBytes.ContainsKey(label))
                downloadedBytes.Add(label, 0);
        }
        
        var downLoadTasks = new List<UniTask>();
        foreach (var label in downLoadLabel)
        {
            if (Sizes.ContainsKey(label))
                downLoadTasks.Add(DownLoadMission(label, progressUIRefresh));
        }
        await UniTask.WhenAll(downLoadTasks);
        complete.Invoke();
    }
    
    public static async UniTask<GameObject> LoadObject(string prefabPathName, Vector3 pos = new Vector3())
    {
        var handle = Addressables.InstantiateAsync(prefabPathName, pos, Quaternion.identity);
        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"Failed to load : {prefabPathName}");
            Addressables.ReleaseInstance(handle);
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
            Debug.Log($"Failed to load : {prefabPathName}");
            Addressables.ReleaseInstance(handle);
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
        try
        {
            var handle = Addressables.InstantiateAsync(prefabPathName);
            if (_cancellationTokenSource != null)
            {
                await handle.ToUniTask(cancellationToken: _cancellationTokenSource.Token);
                // 检查是否已取消
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
        
            await handle.Task;
            if (handle.IsValid() && handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"Failed to load : {prefabPathName}");
                Addressables.ReleaseInstance(handle);
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
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
        return default;
    }

    private static readonly List<AsyncOperationHandle> LoadingHandlerList = new List<AsyncOperationHandle>();
    
    public static async UniTask<T> LoadT<T>(string prefabPathName, GameObject memoryReleaseTarget = null)
    {
        var handle = Addressables.LoadAssetAsync<T>(prefabPathName);
        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"Failed to load : {prefabPathName}");
            Addressables.Release(handle);
            return default;
        }
        else
        {
            if (memoryReleaseTarget == null)
            {
                LoadingHandlerList.Add(handle);
            }
            else
            {
                memoryReleaseTarget.AddOnDestroyCallback( () =>
                {
                    Addressables.ReleaseInstance(handle);
                });
            }
            return handle.Result;
        }
    }
    
    public static async UniTask<T> LoadT<T>(IResourceLocation location, GameObject memoryReleaseTarget = null)
    {
        var handle = Addressables.LoadAssetAsync<T>(location);
        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"Failed to load : {location}");
            Addressables.Release(handle);
            return default;
        }
        else
        {
            if (memoryReleaseTarget == null)
            {
                LoadingHandlerList.Add(handle);
            }
            else
            {
                memoryReleaseTarget.AddOnDestroyCallback( () =>
                {
                    Addressables.ReleaseInstance(handle);
                });
            }
            return handle.Result;
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
}
