using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
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

    static void CheckDownloadText()
    {
        var examples = new Dictionary<long, string>
        {
            [0] = "0 B",
            [1] = "1 B",
            [1023] = "1023 B",
            [1024] = "1 KB",
            [1075] = "1 KB",
            [1076] = "1.1 KB",
            [51200] = "50 KB",
            [52428] = "51.2 KB",
            [1048575] = "1024 KB",
            [1048576] = "1 MB",
            [1572864] = "1.5 MB",
            [1073741823] = "1024 MB",
            [1073741824] = "1 GB",
            [1610612736] = "1.5 GB",
        };
        foreach (var example in examples)
            Check(PocketStrikerDownloadText.FormatSize(example.Key) == example.Value,
                $"download size {example.Key} bytes displays as {example.Value}");
        Check(Enumerable.Range(1, 1023).All(bytes => PocketStrikerDownloadText.FormatSize(bytes) == $"{bytes} B"),
            "every positive size below one KB remains visibly nonzero");

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Check(PocketStrikerDownloadText.FormatSize(52428) == "51.2 KB"
                && PocketStrikerDownloadText.FormatSize(1572864) == "1.5 MB",
                "download sizes retain a stable decimal separator across device cultures");
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }

        foreach (var language in new[] { SystemLanguage.Chinese, SystemLanguage.Japanese, SystemLanguage.English })
        {
            var confirmation = PocketStrikerDownloadText.Confirmation(51200, language);
            Check(confirmation.Contains("50 KB"), $"{language} confirmation shows the actual small download size");
            Check(PocketStrikerDownloadText.Progress(40, 100, language)
                == AddressablesResourcePolicy.DownloadProgressText(language) + "\n40 B / 100 B",
                $"{language} progress uses readable units for transferred and required bytes");
        }
        Check(PocketStrikerDownloadText.Confirmation(1, SystemLanguage.Chinese).Contains("下载")
            && PocketStrikerDownloadText.Confirmation(1, SystemLanguage.Japanese).Contains("ダウンロード")
            && PocketStrikerDownloadText.Confirmation(1, SystemLanguage.English).IndexOf("download", StringComparison.OrdinalIgnoreCase) >= 0,
            "download confirmation remains localized in Chinese, Japanese, and English");
    }

    static async UniTask Run()
    {
        CheckDownloadText();
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
        Check(PocketStrikerDownloadText.Confirmation(size, SystemLanguage.Chinese).Contains("100 B"),
            "a nonzero Addressables requirement produces a visibly nonzero confirmation");
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
        Check(text.Any(value => value.Contains("连接")) && text.Any(value => value.Contains("重试")) && text.Any(value => value.Contains("100 B / 100 B")),
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
        Check(await PocketStrikerDependencyDownloader.GetWholeDownloadSize(new[] { "unit" }) == 0,
            "a fully cached resource inspection returns exactly zero required bytes");
        var cachedProgressCalls = 0;
        Check(await PocketStrikerDependencyDownloader.DownloadRequiredDependencies(new[] { "unit" }, _ => cachedProgressCalls++, SystemLanguage.English) && Addressables.DownloadCalls == 0,
            "fully cached resources skip a download");
        Check(cachedProgressCalls == 0 && PocketStrikerDependencyDownloader.RequiredBytes == 0,
            "fully cached resources do not enter download progress");
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

        await CheckStartupRoutes();
    }

    static async UniTask PrepareStartup()
    {
        var startup = new StartUpPresentation();
        typeof(StartUpPresentation).GetField("starter", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(startup, new Starter());
        var task = (UniTask)typeof(StartUpPresentation).GetMethod("OnStart", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(startup, null);
        await task;
    }

    static void ResetStartup(long requiredBytes, long cachedBytes = 0)
    {
        Addressables.Reset();
        Addressables.TotalBytes = requiredBytes;
        Addressables.Cached = cachedBytes;
        StartupDoubles.Reset();
    }

    sealed class StartupFrame
    {
        public string text;
        public bool loadingOpen;
        public int downloadingCalls, loadingPercentCalls, initializations, confirmations;
    }

    static List<StartupFrame> ObserveSmallDownload()
    {
        var frames = new List<StartupFrame>();
        DownloadTestClock.ObserveDownloadWait = () => frames.Add(new StartupFrame
        {
            text = ProgressLayer.Messages.LastOrDefault(),
            loadingOpen = ProgressLayer.Open,
            downloadingCalls = ProgressLayer.DownloadingCalls,
            loadingPercentCalls = ProgressLayer.LoadingPercentCalls,
            initializations = Starter.Initializations,
            confirmations = PopupLayer.ConfirmCalls
        });
        return frames;
    }

    static bool ContainsDownloadStatus(string text) => text != null &&
        (text.Contains("下载") || text.Contains("ダウンロード") || text.Contains("重试") || text.Contains("再試行")
         || text.IndexOf("download", StringComparison.OrdinalIgnoreCase) >= 0
         || text.IndexOf("retry", StringComparison.OrdinalIgnoreCase) >= 0
         || text.Contains(" B") || text.Contains(" KB") || text.Contains(" MB") || text.Contains(" GB"));

    static void CheckSilentSmallDownload(List<StartupFrame> frames, SystemLanguage language, string description)
    {
        var inspectingText = language switch
        {
            SystemLanguage.Japanese => "リソースを検査中",
            SystemLanguage.English => "Inspecting resources",
            _ => "检查资源中"
        };
        Check(frames.Count > 0, description + " observes the pending download rather than only its final state");
        Check(frames.All(frame => frame.loadingOpen && frame.text == inspectingText),
            description + " retains the localized resource-inspection loading text during every download/retry wait");
        Check(frames.All(frame => frame.downloadingCalls == 0 && frame.loadingPercentCalls == 0 && frame.confirmations == 0)
            && ProgressLayer.DownloadingCalls == 0 && ProgressLayer.LoadingPercentCalls == 0 && PopupLayer.ConfirmCalls == 0,
            description + " never opens download UI or sends a download-percent update");
        Check(!ProgressLayer.Messages.Any(ContainsDownloadStatus),
            description + " never exposes download, byte-count or retry status text");
        Check(frames.All(frame => frame.initializations == 0) && Starter.PrematureInitializations == 0,
            description + " cannot initialize the game before required resources complete");
        Check(!ProgressLayer.Open, description + " closes ordinary loading after completion or recoverable failure");
    }

    static async UniTask CheckStartupRoutes()
    {
        foreach (var bytes in new[] { 0L, 1L, 20700L, 65535L, 65536L })
            Check(!PocketStrikerDownloadPolicy.RequiresDownloadConfirmation(bytes),
                $"{bytes} bytes do not require startup download consent");
        foreach (var bytes in new[] { 65537L, 1048576L, 1073741824L })
            Check(PocketStrikerDownloadPolicy.RequiresDownloadConfirmation(bytes),
                $"{bytes} bytes preserve startup download consent");

        // Fresh startup instances exercise actual routing, while the doubles
        // retain the completed cache. This does not simulate a device cache file.
        for (var restart = 0; restart < 3; restart++)
        {
            ResetStartup(20700, 20700);
            await PrepareStartup();
            Check(Addressables.DownloadCalls == 0 && PopupLayer.ConfirmCalls == 0 && Starter.Initializations == 1,
                $"fully cached cold-start routing {restart + 1} skips download and confirmation");
        }

        foreach (var language in new[] { SystemLanguage.Chinese, SystemLanguage.Japanese, SystemLanguage.English })
        {
            for (var restart = 0; restart < 3; restart++)
            {
                ResetStartup(20700);
                AppSetting.Value.Language = language;
                var frames = ObserveSmallDownload();
                Addressables.Downloads.Enqueue(new DownloadOperation(true, 20700, 3));
                await PrepareStartup();
                var description = $"{language} recurring 20.2 KB startup {restart + 1}";
                Check(Addressables.DownloadCalls == 1 && Starter.Initializations == 1 && Addressables.Cached == 20700
                    && PocketStrikerDependencyDownloader.DownloadedBytes == 20700 && PopupLayer.WarningCalls == 0,
                    description + " actually completes the required download and then initializes once");
                CheckSilentSmallDownload(frames, language, description);
            }
            var persistedCache = Addressables.Cached;
            ResetStartup(20700, persistedCache);
            AppSetting.Value.Language = language;
            await PrepareStartup();
            Check(Addressables.DownloadCalls == 0 && ProgressLayer.DownloadingCalls == 0 && ProgressLayer.LoadingPercentCalls == 0
                && PopupLayer.ConfirmCalls == 0 && Starter.Initializations == 1,
                $"{language} next startup checks completed cache and needs no second download");

            foreach (var bytes in new[] { 1L, 65535L, 65536L })
            {
                ResetStartup(bytes);
                AppSetting.Value.Language = language;
                var frames = ObserveSmallDownload();
                Addressables.Downloads.Enqueue(new DownloadOperation(true, bytes, 3));
                await PrepareStartup();
                var description = $"{language} {bytes}-byte automatic boundary download";
                Check(Addressables.DownloadCalls == 1 && Addressables.Cached == bytes && Starter.Initializations == 1
                    && PopupLayer.WarningCalls == 0, description + " downloads the positive remainder without bypassing initialization");
                CheckSilentSmallDownload(frames, language, description);
            }
        }

        foreach (var bytes in new[] { 65537L, 1048576L, 1073741824L })
        {
            ResetStartup(bytes);
            await PrepareStartup();
            Check(PopupLayer.ConfirmCalls == 1 && Addressables.DownloadCalls == 0 && Starter.Initializations == 0,
                $"{bytes} byte fresh or larger update waits for original confirmation");
            Check(PopupLayer.ConfirmationText == PocketStrikerDownloadText.Confirmation(bytes, AppSetting.Value.Language),
                "large download confirmation retains actual size and localized text");
            PopupLayer.CancelAction();
            Check(Application.QuitCalls == 1 && Addressables.DownloadCalls == 0,
                "large download cancellation retains native quit and performs no download");
            ResetStartup(bytes);
            await PrepareStartup();
            Addressables.Downloads.Enqueue(new DownloadOperation(true, bytes, 3));
            PopupLayer.ConfirmAction();
            Check(Addressables.DownloadCalls == 1 && Addressables.Cached == bytes && Starter.Initializations == 1
                && Starter.PrematureInitializations == 0,
                "large download confirmation still enters the real downloader and initialization");
            Check(ProgressLayer.DownloadingCalls == 1 && ProgressLayer.LoadingPercentCalls > 1
                && ProgressLayer.Percentages.Any(value => value > 0 && value < 1) && ProgressLayer.Percentages.Last() == 1,
                "confirmed large downloads preserve visible byte progress through completion");
            Check(ProgressLayer.Messages.Any(message => message.Contains(PocketStrikerDownloadText.FormatSize(bytes)
                + " / " + PocketStrikerDownloadText.FormatSize(bytes))) && !ProgressLayer.Open,
                "confirmed large downloads display their actual final byte count and close progress");
        }

        ResetStartup(1048576, 1048576 - 20700);
        var remainderFrames = ObserveSmallDownload();
        Addressables.Downloads.Enqueue(new DownloadOperation(true, 20700, 3));
        await PrepareStartup();
        Check(PopupLayer.ConfirmCalls == 0 && Addressables.Cached == 1048576 && Starter.Initializations == 1,
            "necessary 20.2 KB content change uses actual remaining size rather than the full resource set");
        CheckSilentSmallDownload(remainderFrames, SystemLanguage.Chinese, "20.2 KB remaining in a larger resource set");
        ResetStartup(1048576 + 65537, 1048576);
        await PrepareStartup();
        Check(PopupLayer.ConfirmCalls == 1 && Addressables.DownloadCalls == 0 && Starter.Initializations == 0,
            "larger content change keeps consent even when old resources were completely downloaded");
        Check(PopupLayer.ConfirmationText.Contains("64 KB"),
            "larger change confirmation shows only its uncached remainder");

        foreach (var language in new[] { SystemLanguage.Chinese, SystemLanguage.Japanese, SystemLanguage.English })
        {
            ResetStartup(20700);
            AppSetting.Value.Language = language;
            var retryFrames = ObserveSmallDownload();
            Addressables.Downloads.Enqueue(new DownloadOperation(false, 7000, 2));
            Addressables.Downloads.Enqueue(new DownloadOperation(true, 13700, 2));
            await PrepareStartup();
            Check(Addressables.DownloadCalls == 2 && Addressables.RetryStartingSize == 13700 && DownloadTestClock.RetryDelays == 1
                && Addressables.Cached == 20700 && Starter.Initializations == 1 && PopupLayer.WarningCalls == 0,
                $"{language} automatic small update silently retries the real uncached remainder then initializes");
            CheckSilentSmallDownload(retryFrames, language, language + " transient small-download failure");

            ResetStartup(20700);
            AppSetting.Value.Language = language;
            var failureFrames = ObserveSmallDownload();
            Addressables.Downloads.Enqueue(new DownloadOperation(false, 0, 2));
            Addressables.Downloads.Enqueue(new DownloadOperation(false, 0, 2));
            await PrepareStartup();
            Check(Addressables.DownloadCalls == 2 && DownloadTestClock.RetryDelays == 1 && PopupLayer.WarningCalls == 1
                && PopupLayer.WarningAction != null, $"{language} offline automatic update exposes a recoverable warning after bounded retries");
            Check(Starter.Initializations == 0 && Addressables.Cached == 0,
                $"{language} offline failure does not fake download completion or game initialization");
            CheckSilentSmallDownload(failureFrames, language, language + " persistent small-download failure");
            PopupLayer.WarningAction();
            Check(UnityEngine.SceneManagement.SceneManager.LoadedScene == 0,
                $"{language} failure warning retries via the original startup scene reload");
            ResetStartup(20700);
            AppSetting.Value.Language = language;
            var recoveryFrames = ObserveSmallDownload();
            Addressables.Downloads.Enqueue(new DownloadOperation(true, 20700, 3));
            await PrepareStartup();
            Check(Addressables.Cached == 20700 && Starter.Initializations == 1 && PopupLayer.WarningCalls == 0,
                $"{language} startup actually completes after network recovery");
            CheckSilentSmallDownload(recoveryFrames, language, language + " network recovery");
        }

        ResetStartup(20700);
        Addressables.SizeFailure = true;
        await PrepareStartup();
        Check(PopupLayer.WarningCalls == 1 && PopupLayer.ConfirmCalls == 0 && Addressables.DownloadCalls == 0 && Starter.Initializations == 0 && !ProgressLayer.Open,
            "resource inspection failure remains recoverable and never starts a hidden download");
    }
}

// Only the two Unity PlayerLoop waits are replaced by the runner. The actual
// downloader and network policy otherwise compile and execute unchanged.
public static class DownloadTestClock
{
    public static DownloadOperation Active;
    public static int RetryDelays;
    public static Action ObserveDownloadWait;
    public static UniTask NextFrame()
    {
        Time.realtimeSinceStartup += 0.2f;
        if (Active != null && !Active.Done)
        {
            ObserveDownloadWait?.Invoke();
            Active.Tick();
        }
        return UniTask.CompletedTask;
    }
    public static UniTask Delay() { ObserveDownloadWait?.Invoke(); RetryDelays++; return UniTask.CompletedTask; }
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
    public static class Debug { public static void LogError(object message, object context = null) {} public static void LogWarning(object message) {} }
    public sealed class SerializeField : Attribute {}
    public class MonoBehaviour {}
    public class Transform {}
    public class RectTransform : Transform {}
    public sealed class Canvas { public Transform transform = new Transform(); public T GetComponent<T>() where T : new() => new T(); }
    public sealed class AudioSource { public float volume; }
    public enum RuntimePlatform { IPhonePlayer, Android, OSXEditor }
    public static class Application
    {
        public static RuntimePlatform platform = RuntimePlatform.OSXEditor;
        public static int targetFrameRate, QuitCalls;
        public static void Quit() { QuitCalls++; }
        public static void OpenURL(string url) {}
    }
}
namespace UnityEngine.Serialization
{
    public sealed class FormerlySerializedAsAttribute : Attribute { public FormerlySerializedAsAttribute(string name) {} }
}
namespace UnityEngine.SceneManagement
{
    public static class SceneManager { public static int LoadedScene = -1; public static void LoadScene(int index) { LoadedScene = index; } }
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
        public static long Cached, RetryStartingSize, TotalBytes = 100;
        public static bool FailedReleasedBeforeRetry, SizeFailure;
        public static string[] LastKeys;
        static DownloadOperation previous;
        public static void Reset()
        {
            Sizes = Releases = DownloadCalls = UnionCalls = SizeFailuresRemaining = DownloadTestClock.RetryDelays = 0;
            Cached = RetryStartingSize = 0;
            TotalBytes = 100;
            FailedReleasedBeforeRetry = SizeFailure = false;
            Downloads.Clear(); previous = null; DownloadTestClock.Active = null; DownloadTestClock.ObserveDownloadWait = null;
        }
        public static AsyncOperationHandle<long> GetDownloadSizeAsync(IEnumerable keys)
        {
            LastKeys = keys.Cast<string>().ToArray(); Sizes++;
            // Reproduce initialization overwriting the previously configured queue.
            UnityEngine.ResourceManagement.WebRequestQueue.MaxRequests = 500;
            var throwThisTime = SizeFailuresRemaining > 0;
            if (throwThisTime) SizeFailuresRemaining--;
            return new AsyncOperationHandle<long> { Operation = new SizeOperation { Result = TotalBytes - Cached, Failed = SizeFailure, Throws = throwThisTime } };
        }
        public static AsyncOperationHandle DownloadDependenciesAsync(IEnumerable keys, MergeMode mode, bool autoRelease)
        {
            if (autoRelease) throw new Exception("handle must stay valid until status is observed");
            LastKeys = keys.Cast<string>().ToArray(); DownloadCalls++;
            if (mode == MergeMode.Union) UnionCalls++;
            if (previous != null) { FailedReleasedBeforeRetry = previous.Released; RetryStartingSize = TotalBytes - Cached; }
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

// These adapters expose the actual StartUpPresentation branches without Unity,
// PlayFab, character art, a network connection, or an operating-system cache.
public static class StartupDoubles
{
    public static void Reset()
    {
        Starter.Initializations = Starter.PrematureInitializations = Application.QuitCalls = PopupLayer.ConfirmCalls
            = PopupLayer.WarningCalls = ProgressLayer.DownloadingCalls = ProgressLayer.LoadingPercentCalls = 0;
        PopupLayer.ConfirmAction = PopupLayer.CancelAction = PopupLayer.WarningAction = null;
        PopupLayer.ConfirmationText = null;
        ProgressLayer.Messages.Clear();
        ProgressLayer.Percentages.Clear();
        ProgressLayer.Open = false;
        AppSetting.Value.Language = SystemLanguage.Chinese;
        UnityEngine.SceneManagement.SceneManager.LoadedScene = -1;
    }
}
public sealed class Starter
{
    public static int Initializations, PrematureInitializations;
    public UniTask Initialise()
    {
        if (Addressables.Cached < Addressables.TotalBytes) PrematureInitializations++;
        Initializations++;
        return UniTask.CompletedTask;
    }
    public void EnterFrontScene() {}
}
public sealed class CommonSetting
{
    public List<string> DownLoadLabels = new List<string> { "unit", "effect" };
    public static string StartThemeAddressKey = "start";
    public void Initialise() {}
}
public static class AddressablesLogic
{
    public static UniTask<bool> VersionConfirm() => UniTask.FromResult(false);
    public static UniTask DownLoadConfig() => UniTask.CompletedTask;
    public static UniTask<CommonSetting> GetCommonSetting() => UniTask.FromResult(new CommonSetting());
    public static UniTask<long> GetWholeDownLoadSize(Action<string> onFailure, List<string> labels) =>
        PocketStrikerDependencyDownloader.GetWholeDownloadSize(labels, onFailure);
    public static float DownloadProgress => PocketStrikerDependencyDownloader.Progress;
    public static async UniTask ResourcePrepareProcess(Action complete, Action<string> onProgress, List<string> labels)
    {
        if (!await PocketStrikerDependencyDownloader.DownloadRequiredDependencies(labels, onProgress, AppSetting.Value.Language))
            throw new InvalidOperationException("Failed to download required game resources.");
        complete?.Invoke();
    }
}
public static class AppSetting
{
    public sealed class Settings { public SystemLanguage Language = SystemLanguage.Chinese; public float BgmVolume = 1, EffectsVolume = 1; }
    public static Settings Value = new Settings();
    public static AudioSource BGMSource, UiAudioSource;
    public static UniTask PlayBGM(string address) => UniTask.CompletedTask;
}
public static class PocketStrikerAppSettings { public static void Load() {} }
public static class PosCal { public static Canvas Canvas; public static RectTransform SafeAreaRect; public static void TestIni() {} }
public static class FightGlobalSetting { public static int SceneStep; }
public static class PlayFabReadClient { public static string DontShowFrontFight = "False"; }
public sealed class TitleBgLayer { public UniTask SetupLogin() => UniTask.CompletedTask; }
public sealed class TitleScreenLayer { public void Initialise(bool enabled) {} }
namespace DummyLayerSystem
{
    public static class UILayerLoader
    {
        public static void SetHanger(Transform safeArea, Transform canvas) {}
        public static T Load<T>(bool first, object parent, bool persistent) where T : new() => new T();
    }
    public static class HighLightLayer { public static void Close() {} }
    public static class ProgressLayer
    {
        public static int DownloadingCalls, LoadingPercentCalls;
        public static bool Open;
        public static List<string> Messages = new List<string>();
        public static List<float> Percentages = new List<float>();
        public static void Loading(string text) { Messages.Add(text); Open = true; }
        public static void Downloading(string text) { DownloadingCalls++; Loading(text); }
        public static void LoadingPercent(string text, float percent, bool animate = true)
        { LoadingPercentCalls++; Percentages.Add(percent); Loading(text); }
        public static void Close() { Open = false; }
    }
    public static class PopupLayer
    {
        public static int ConfirmCalls, WarningCalls;
        public static Action ConfirmAction, CancelAction, WarningAction;
        public static string ConfirmationText;
        public static void ArrangeConfirmWindow(Action yes, Action no, string text)
        {
            ConfirmCalls++; ConfirmAction = yes; CancelAction = no; ConfirmationText = text;
        }
        public static void ArrangeWarnWindow(Action action, string text) { WarningCalls++; WarningAction = action; }
    }
}
