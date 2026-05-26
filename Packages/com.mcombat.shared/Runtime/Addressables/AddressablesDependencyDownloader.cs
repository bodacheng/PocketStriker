using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AddressablesDependencyDownloader
{
    private static readonly IDictionary<string, long> RequiredDownloadSizes = new Dictionary<string, long>();
    private static readonly IDictionary<string, long> DownloadedByteCounts = new Dictionary<string, long>();

    public static long DownloadedBytes
    {
        get
        {
            long whole = 0;
            foreach (var kv in DownloadedByteCounts)
            {
                whole += kv.Value;
            }

            return whole;
        }
    }

    public static void Reset()
    {
        RequiredDownloadSizes.Clear();
        DownloadedByteCounts.Clear();
    }

    public static async UniTask<long> GetDownloadSize(string label, Action<string> exceptionProcess = null)
    {
        if (string.IsNullOrEmpty(label))
        {
            return 0;
        }

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
                    RequiredDownloadSizes[label] = result;
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
            {
                Addressables.Release(handle);
            }
        }
    }

    public static async UniTask<long> GetWholeDownloadSize(IEnumerable<string> labels, Action<string> exceptionProcess = null)
    {
        Reset();

        long wholeSize = 0;
        foreach (var label in NormalizeLabels(labels))
        {
            wholeSize += await GetDownloadSize(label, exceptionProcess);
        }

        return wholeSize;
    }

    public static async UniTask<bool> DownloadDependencies(
        string label,
        Action<string> progressUIRefresh = null,
        string progressText = null)
    {
        if (string.IsNullOrEmpty(label))
        {
            return true;
        }

        AsyncOperationHandle downloadHandle = default;
        try
        {
            // Keep the handle valid until Status and DownloadStatus have been read.
            downloadHandle = Addressables.DownloadDependenciesAsync(label, false);
            while (!downloadHandle.IsDone)
            {
                UpdateDownloadedBytes(label, downloadHandle);
                progressUIRefresh?.Invoke(progressText ?? label);
                await UniTask.DelayFrame(0);
            }

            if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                UpdateDownloadedBytes(label, downloadHandle);
                return true;
            }

            Debug.LogError(AddressablesResourcePolicy.DownloadMissionFailureMessage(
                label,
                downloadHandle.OperationException));
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError(AddressablesResourcePolicy.DownloadMissionExceptionMessage(ex));
            return false;
        }
        finally
        {
            if (downloadHandle.IsValid())
            {
                Addressables.Release(downloadHandle);
            }
        }
    }

    public static async UniTask<bool> DownloadRequiredDependencies(
        IEnumerable<string> labels,
        Action<string> progressUIRefresh = null,
        string progressText = null)
    {
        var downloadTasks = new List<UniTask<bool>>();
        foreach (var label in NormalizeLabels(labels))
        {
            AddressablesResourcePolicy.EnsureDownloadedBytesLabel(DownloadedByteCounts, label);
            if (RequiredDownloadSizes.ContainsKey(label))
            {
                downloadTasks.Add(DownloadDependencies(label, progressUIRefresh, progressText));
            }
        }

        if (downloadTasks.Count == 0)
        {
            return true;
        }

        var results = await UniTask.WhenAll(downloadTasks);
        return results.All(result => result);
    }

    private static IEnumerable<string> NormalizeLabels(IEnumerable<string> labels)
    {
        return labels == null
            ? Enumerable.Empty<string>()
            : labels.Where(label => !string.IsNullOrEmpty(label)).Distinct();
    }

    private static void UpdateDownloadedBytes(string label, AsyncOperationHandle downloadHandle)
    {
        if (!DownloadedByteCounts.ContainsKey(label))
        {
            return;
        }

        DownloadedByteCounts[label] = downloadHandle.GetDownloadStatus().DownloadedBytes;
    }
}
