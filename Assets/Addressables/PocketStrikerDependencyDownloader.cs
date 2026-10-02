using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// One union operation counts shared bundles once and leaves successful bundles
// cached across a retry. The imported MCombat downloader stays untouched.
public static class PocketStrikerDependencyDownloader
{
    public static long DownloadedBytes { get; private set; }
    public static long RequiredBytes { get; private set; }
    public static float Progress => RequiredBytes > 0 ? (float)((double)DownloadedBytes / RequiredBytes) : 0f;

    static string[] NormalizeLabels(IEnumerable<string> labels) => labels == null
        ? Array.Empty<string>()
        : labels.Where(label => !string.IsNullOrEmpty(label)).Distinct().ToArray();

    public static async UniTask<long> GetWholeDownloadSize(IEnumerable<string> labels, Action<string> onFailure = null)
    {
        DownloadedBytes = 0;
        RequiredBytes = 0;
        var keys = NormalizeLabels(labels);
        try
        {
            RequiredBytes = await GetRemainingSize(keys);
            return RequiredBytes;
        }
        catch (Exception error)
        {
            Debug.LogError($"[Download] Resource inspection failed ({string.Join(", ", keys)}): {error}");
            onFailure?.Invoke(string.Join(", ", keys));
            throw;
        }
    }

    static async UniTask<long> GetRemainingSize(string[] keys)
    {
        if (keys.Length == 0) return 0;
        Exception failure = null;
        for (var attempt = 1; attempt <= PocketStrikerDownloadPolicy.DownloadAttempts; attempt++)
        {
            PocketStrikerDownloadPolicy.Configure();
            AsyncOperationHandle<long> handle = default;
            try
            {
                handle = Addressables.GetDownloadSizeAsync(keys);
                await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded) return handle.Result;
                failure = handle.OperationException;
            }
            catch (Exception error) { failure = error; }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
                PocketStrikerDownloadPolicy.Configure();
            }
            if (attempt < PocketStrikerDownloadPolicy.DownloadAttempts)
                await WaitBeforeRetry();
        }
        throw new InvalidOperationException("Could not inspect resource download size.", failure);
    }

    static UniTask WaitBeforeRetry() => UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true);

    public static UniTask<bool> DownloadDependencies(string label, Action<string> onProgress, SystemLanguage language)
    {
        return DownloadRequiredDependencies(new[] { label }, onProgress, language);
    }

    public static async UniTask<bool> DownloadRequiredDependencies(
        IEnumerable<string> labels, Action<string> onProgress, SystemLanguage language)
    {
        var keys = NormalizeLabels(labels);
        DownloadedBytes = 0;
        RequiredBytes = await GetRemainingSize(keys);
        if (RequiredBytes == 0) return true;

        Exception failure = null;
        for (var attempt = 1; attempt <= PocketStrikerDownloadPolicy.DownloadAttempts; attempt++)
        {
            var remaining = attempt == 1 ? RequiredBytes : await GetRemainingSize(keys);
            if (remaining == 0)
            {
                DownloadedBytes = RequiredBytes;
                onProgress?.Invoke(ProgressText(language, false));
                return true;
            }

            var cachedBytes = Math.Max(0, RequiredBytes - remaining);
            DownloadedBytes = cachedBytes;
            AsyncOperationHandle handle = default;
            try
            {
                handle = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false);
                var nextRefresh = 0f;
                while (!handle.IsDone)
                {
                    var status = handle.GetDownloadStatus();
                    DownloadedBytes = Math.Min(RequiredBytes, cachedBytes + status.DownloadedBytes);
                    if (Time.realtimeSinceStartup >= nextRefresh)
                    {
                        onProgress?.Invoke(ProgressText(language, status.DownloadedBytes == 0));
                        nextRefresh = Time.realtimeSinceStartup + 0.1f;
                    }
                    await UniTask.NextFrame();
                }
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    DownloadedBytes = RequiredBytes;
                    onProgress?.Invoke(ProgressText(language, false));
                    return true;
                }
                failure = handle.OperationException;
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                // Release the failed dependency tree before starting another
                // operation, otherwise Addressables can reuse its failed handles.
                if (handle.IsValid()) Addressables.Release(handle);
            }

            if (attempt < PocketStrikerDownloadPolicy.DownloadAttempts)
            {
                Debug.LogWarning($"[Download] Attempt {attempt} failed; retrying cached remainder: {failure}");
                onProgress?.Invoke(RetryText(language, attempt + 1));
                await WaitBeforeRetry();
            }
        }
        Debug.LogError($"[Download] Required resources failed after {PocketStrikerDownloadPolicy.DownloadAttempts} attempts: {failure}");
        return false;
    }

    static string RetryText(SystemLanguage language, int attempt) => language switch
    {
        SystemLanguage.Chinese => $"正在重试下载（{attempt}/{PocketStrikerDownloadPolicy.DownloadAttempts}）...",
        SystemLanguage.Japanese => $"ダウンロードを再試行中（{attempt}/{PocketStrikerDownloadPolicy.DownloadAttempts}）...",
        _ => $"Retrying download ({attempt}/{PocketStrikerDownloadPolicy.DownloadAttempts})..."
    };

    static string ProgressText(SystemLanguage language, bool connecting)
    {
        if (connecting && DownloadedBytes == 0)
            return language switch
            {
                SystemLanguage.Chinese => "正在连接资源服务器...",
                SystemLanguage.Japanese => "リソースサーバーに接続中...",
                _ => "Connecting to resource server..."
            };
        return PocketStrikerDownloadText.Progress(DownloadedBytes, RequiredBytes, language);
    }
}
