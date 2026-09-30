using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

internal static class DownloadTests
{
    static int checks;
    public static int Main()
    {
        UniTaskScheduler.DispatchUnityMainThread = false;
        try
        {
            Run().AsTask().GetAwaiter().GetResult();
            Console.WriteLine($"PASS: {checks} startup download checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        checks++;
    }

    static async UniTask Run()
    {
        Addressables.InternalIdTransformFunc = location => "preserved:" + location.InternalId;
        Addressables.WebRequestOverride = request => request.headers["X-Existing"] = "retained";
        PocketStrikerDownloadPolicy.Configure();
        PocketStrikerDownloadPolicy.Configure();
        var options = new AssetBundleRequestOptions();
        Check(Addressables.InternalIdTransformFunc(new Location { InternalId = "url", Data = options }) == "preserved:url",
            "configuration is idempotent and retains prior location transformation");
        Check(options.Timeout == 30 && options.RetryCount == 2, "old catalog bundle options acquire idle timeout and retries");
        var request = new UnityWebRequest { url = "https://example.test/catalog_3.0.2.hash?cache=0" };
        Addressables.WebRequestOverride(request);
        Check(request.timeout == 30 && request.headers["X-Existing"] == "retained", "catalog request timeout preserves existing request override");
        request = new UnityWebRequest { url = "https://example.test/large.bundle" };
        Addressables.WebRequestOverride(request);
        Check(request.timeout == 0, "large bundles retain provider idle timeout instead of whole-transfer deadline");

        Addressables.Reset();
        var size = await PocketStrikerDependencyDownloader.GetWholeDownloadSize(new[] { "unit", "effect", "unit", null, "" });
        Check(size == 100 && Addressables.Sizes == 1 && Addressables.LastKeys.SequenceEqual(new[] { "unit", "effect" }),
            "combined size query normalizes labels and counts a shared bundle once");
        Check(UnityEngine.ResourceManagement.WebRequestQueue.MaxRequests == 4, "initialization restoring an old queue value is overridden");
        Check(Addressables.Releases == 1, "size inspection releases its operation");

        Addressables.Reset();
        Addressables.Downloads.Enqueue(new DownloadOperation(false, 40, 3));
        Addressables.Downloads.Enqueue(new DownloadOperation(true, 60, 3));
        var progress = new List<float>();
        var text = new List<string>();
        var downloaded = await PocketStrikerDependencyDownloader.DownloadRequiredDependencies(new[] { "unit", "effect", "unit" }, message =>
        {
            progress.Add(PocketStrikerDependencyDownloader.Progress);
            text.Add(message);
        }, SystemLanguage.Chinese);
        Check(downloaded && Addressables.DownloadCalls == 2 && DownloadTestClock.RetryDelays == 1,
            "one failed download automatically retries without a user action");
        Check(Addressables.UnionCalls == 2 && Addressables.LastKeys.Length == 2, "download combines normalized labels through Union");
        Check(Addressables.FailedReleasedBeforeRetry, "retry releases failed dependency tree before rebuilding it");
        Check(Addressables.RetryStartingSize == 60, "retry downloads only uncached remainder");
        Check(PocketStrikerDependencyDownloader.DownloadedBytes == 100 && PocketStrikerDependencyDownloader.Progress == 1,
            "successful retry counts earlier cached bytes and reaches complete progress");
        Check(progress.Any(value => value > 0 && value < 1) && progress.Last() == 1,
            "byte progress is visible while the operation is pending and completes");
        Check(text.Any(value => value.Contains("连接")) && text.Any(value => value.Contains("重试")) && text.Any(value => value.Contains("MB")),
            "localized status distinguishes connection, automatic retry, and transferred bytes");
        Check(Addressables.Releases == Addressables.Sizes + Addressables.DownloadCalls, "success and failed handles are all released exactly once");

        Addressables.Reset();
        Addressables.Downloads.Enqueue(new DownloadOperation(false, 0, 1));
        Addressables.Downloads.Enqueue(new DownloadOperation(false, 0, 1));
        Check(!await PocketStrikerDependencyDownloader.DownloadRequiredDependencies(new[] { "unit" }, null, SystemLanguage.English),
            "persistent failure is reported after bounded automatic retries");
        Check(Addressables.DownloadCalls == 2 && Addressables.Releases == Addressables.Sizes + 2,
            "persistent failure neither loops indefinitely nor retains failed handles");

        Addressables.Reset();
        Addressables.Cached = 100;
        Check(await PocketStrikerDependencyDownloader.DownloadRequiredDependencies(new[] { "unit" }, null, SystemLanguage.English) && Addressables.DownloadCalls == 0,
            "fully cached resources skip a download");
        Check(await PocketStrikerDependencyDownloader.GetWholeDownloadSize(Array.Empty<string>()) == 0, "empty labels need no Addressables request");

        Addressables.Reset();
        Addressables.SizeFailuresRemaining = 1;
        Check(await PocketStrikerDependencyDownloader.GetWholeDownloadSize(new[] { "unit" }) == 100 && Addressables.Sizes == 2,
            "first inspection/initialization exception automatically retries before download");
        Check(Addressables.Releases == 2 && DownloadTestClock.RetryDelays == 1,
            "failed inspection handle is released before retrying");

        Addressables.Reset();
        Addressables.SizeFailure = true;
        var failureCallback = false;
        var failed = false;
        try { await PocketStrikerDependencyDownloader.GetWholeDownloadSize(new[] { "missing" }, _ => failureCallback = true); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed && failureCallback && Addressables.Releases == 2, "inspection failures remain failures and release their handles");
    }
}

// Only the two Unity PlayerLoop waits are replaced by the runner. The actual
// downloader and network policy otherwise compile and execute unchanged.
public static class DownloadTestClock
{
    public static DownloadOperation Active;
    public static int RetryDelays;
    public static UniTask NextFrame()
    {
        Time.realtimeSinceStartup += 0.2f;
        Active?.Tick();
        return UniTask.CompletedTask;
    }
    public static UniTask Delay() { RetryDelays++; return UniTask.CompletedTask; }
}

public sealed class DownloadOperation
{
    readonly bool succeeds;
    readonly long bytes;
    readonly int frames;
    int elapsed;
    public bool Done => elapsed >= frames;
    public long Bytes => frames == 0 ? bytes : bytes * elapsed / frames;
    public long CachedAtStart;
    public bool Released;
    public AsyncOperationStatus Status => !Done ? AsyncOperationStatus.None : succeeds ? AsyncOperationStatus.Succeeded : AsyncOperationStatus.Failed;
    public Exception Error => new InvalidOperationException("controlled network failure");
    public DownloadOperation(bool succeeds, long bytes, int frames) { this.succeeds = succeeds; this.bytes = bytes; this.frames = frames; }
    public void Tick()
    {
        elapsed++;
        if (Done) Addressables.Cached = CachedAtStart + bytes;
    }
}

public sealed class Location : IResourceLocation { public string InternalId { get; set; } public object Data { get; set; } }
namespace UnityEngine
{
    public enum SystemLanguage { Chinese, Japanese, English }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {} }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Debug { public static void LogError(object message) {} public static void LogWarning(object message) {} }
}
namespace UnityEngine.Networking
{
    public sealed class UnityWebRequest { public string url; public int timeout; public Dictionary<string, string> headers = new Dictionary<string, string>(); }
}
namespace UnityEngine.ResourceManagement
{
    public static class WebRequestQueue { public static int MaxRequests; public static void SetMaxConcurrentRequests(int max) { MaxRequests = max; } }
}
namespace UnityEngine.ResourceManagement.ResourceLocations
{
    public interface IResourceLocation { string InternalId { get; } object Data { get; } }
}
namespace UnityEngine.ResourceManagement.ResourceProviders
{
    public class AssetBundleRequestOptions { public int Timeout; public int RetryCount; }
}
namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { None, Succeeded, Failed }
    public struct DownloadStatus { public long DownloadedBytes; }
    public sealed class SizeOperation { public long Result; public bool Failed; public bool Released; public bool Throws; }
    public struct AsyncOperationHandle<T>
    {
        public SizeOperation Operation;
        public T Result => (T)(object)Operation.Result;
        public Task<T> Task => Operation.Throws ? System.Threading.Tasks.Task.FromException<T>(OperationException) : System.Threading.Tasks.Task.FromResult(Result);
        public bool IsValid() => Operation != null && !Operation.Released;
        public AsyncOperationStatus Status => Operation.Failed ? AsyncOperationStatus.Failed : AsyncOperationStatus.Succeeded;
        public Exception OperationException => new InvalidOperationException("size failure");
    }
    public struct AsyncOperationHandle
    {
        public DownloadOperation Operation;
        public bool IsDone => Operation.Done;
        public bool IsValid() => Operation != null && !Operation.Released;
        public AsyncOperationStatus Status => Operation.Status;
        public Exception OperationException => Operation.Error;
        public DownloadStatus GetDownloadStatus() => new DownloadStatus { DownloadedBytes = Operation.Bytes };
    }
}
namespace UnityEngine.AddressableAssets
{
    public static class Addressables
    {
        public enum MergeMode { Union }
        public static Func<IResourceLocation, string> InternalIdTransformFunc;
        public static Action<UnityWebRequest> WebRequestOverride;
        public static Queue<DownloadOperation> Downloads = new Queue<DownloadOperation>();
        public static int Sizes, Releases, DownloadCalls, UnionCalls, SizeFailuresRemaining;
        public static long Cached, RetryStartingSize;
        public static bool FailedReleasedBeforeRetry, SizeFailure;
        public static string[] LastKeys;
        static DownloadOperation previous;
        public static void Reset()
        {
            Sizes = Releases = DownloadCalls = UnionCalls = SizeFailuresRemaining = DownloadTestClock.RetryDelays = 0;
            Cached = RetryStartingSize = 0;
            FailedReleasedBeforeRetry = SizeFailure = false;
            Downloads.Clear(); previous = null; DownloadTestClock.Active = null;
        }
        public static AsyncOperationHandle<long> GetDownloadSizeAsync(IEnumerable keys)
        {
            LastKeys = keys.Cast<string>().ToArray(); Sizes++;
            // Reproduce initialization overwriting the previously configured queue.
            UnityEngine.ResourceManagement.WebRequestQueue.MaxRequests = 500;
            var throwThisTime = SizeFailuresRemaining > 0;
            if (throwThisTime) SizeFailuresRemaining--;
            return new AsyncOperationHandle<long> { Operation = new SizeOperation { Result = 100 - Cached, Failed = SizeFailure, Throws = throwThisTime } };
        }
        public static AsyncOperationHandle DownloadDependenciesAsync(IEnumerable keys, MergeMode mode, bool autoRelease)
        {
            if (autoRelease) throw new Exception("handle must stay valid until status is observed");
            LastKeys = keys.Cast<string>().ToArray(); DownloadCalls++;
            if (mode == MergeMode.Union) UnionCalls++;
            if (previous != null) { FailedReleasedBeforeRetry = previous.Released; RetryStartingSize = 100 - Cached; }
            var operation = Downloads.Dequeue(); operation.CachedAtStart = Cached;
            previous = operation; DownloadTestClock.Active = operation;
            return new AsyncOperationHandle { Operation = operation };
        }
        public static void Release(AsyncOperationHandle<long> handle) { if (handle.Operation.Released) throw new Exception("double release"); handle.Operation.Released = true; Releases++; }
        public static void Release(AsyncOperationHandle handle) { if (handle.Operation.Released) throw new Exception("double release"); handle.Operation.Released = true; Releases++; }
    }
}
public static class AddressablesResourcePolicy
{
    public static string DownloadProgressText(SystemLanguage language) => language == SystemLanguage.Chinese ? "正在下载资源" : "Downloading resources";
}
