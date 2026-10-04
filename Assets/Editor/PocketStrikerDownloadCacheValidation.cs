using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Actual loopback AssetBundleProvider/cache and production startup branch review. No production service access.</summary>
[InitializeOnLoad]
public static class PocketStrikerDownloadCacheValidation
{
    const string Key = "PocketStriker.ClientCacheReview";
    const string ScenePath = "Assets/Scene/ABLoadScene/Scene1.unity";
    const string GeneratedDirectory = "Assets/ValidationOwnedTemp/DownloadCache";
    const string Label = "ps_validation_required_resources";
    const string BundleProviderId = "PocketStriker.Validation.RealAssetBundleProvider";
    const string AssetProviderId = "PocketStriker.Validation.RealBundledAssetProvider";
    const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static string DataRoot => Environment.GetEnvironmentVariable("POCKETSTRIKER_CACHE_DATA_ROOT") ?? "/tmp/pocketstriker-client-cache-20261003";
    static string Stage => Environment.GetEnvironmentVariable("POCKETSTRIKER_CACHE_STAGE") ?? "Cold";
    static string Output => Environment.GetEnvironmentVariable("POCKETSTRIKER_CACHE_REVIEW_OUTPUT") ?? "Logs/ClientDownloadLoadingReview/Cache/" + Stage;
    static string Server => Environment.GetEnvironmentVariable("POCKETSTRIKER_CACHE_SERVER") ?? "http://127.0.0.1:18763";
    static bool DisposableClone => Directory.GetCurrentDirectory().StartsWith("/tmp/pocketstriker-download-loading-", StringComparison.Ordinal) || Directory.GetCurrentDirectory().StartsWith("/private/tmp/pocketstriker-download-loading-", StringComparison.Ordinal);
    static Report report;
    static bool finishing, expectedFailure;
    static readonly List<IResourceLocator> originals = new List<IResourceLocator>();
    static OwnedLocator locator;
    static CommonSetting common;
    static Action<UnityWebRequest> oldWebRequest;
    static Cache oldCache, ownedCache;
    // Keep the production loader internal; cross its assembly boundary only in this Editor fixture.
    static class UILayerLoader
    {
        static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
        public static T Get<T>() where T : UILayer => (T)Loader.GetMethod("Get").MakeGenericMethod(typeof(T)).Invoke(null, null);
        public static void SetHanger(Transform safe, Transform full) => Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new object[] { safe, full });
        public static void SetEffectBg(RectTransform rect) => Loader.GetMethod("SetEffectBg").Invoke(null, new object[] { rect });
    }

    [Serializable] public sealed class BundleRecord
    {
        public string variant, bundleName, bundlePath, assetKey, assetInternalName, hash, fileSha256, payloadSha256;
        public long bundleBytes;
        public uint crc;
    }
    [Serializable] public sealed class BundleManifest
    {
        public string utcTime, projectPath, unityVersion, buildTarget, scope;
        public bool complete, passed;
        public List<BundleRecord> bundles = new List<BundleRecord>();
        public List<string> errors = new List<string>();
    }
    [Serializable] public sealed class Frame
    {
        public int frame;
        public string utc, observation, infoText;
        public float elapsed, progress, slider;
        public long downloadedBytes;
        public bool popup, progressLayer, progressBarActiveInHierarchy, downloadUiText, postLateUpdateObserved;
    }
    [Serializable] public sealed class Request
    {
        public string utc, url;
    }
    [Serializable] public sealed class CacheReadiness
    {
        public string utc, purpose;
        public bool cachingReady, ownedCacheReady, versionCached;
        public long requiredBytes;
    }
    [Serializable] public sealed class Report
    {
        public string utcTime, finishedUtc, stage, phase, unityVersion, projectPath, cachePath;
        public bool complete, passed, cachedBefore, cachedAfter, previousHashStillCached, noConfirmation, retrySceneReloaded;
        public long requiredBefore, requiredAfter, automaticLimitBytes;
        public string bundleName, hash, previousHash, payloadSha256, observedPayloadSha256, provider, startupMethod;
        public int processId, nativePointerClicks, confirmations, warnings, expectedFailureLogs;
        public ulong sceneHandleBefore, sceneHandleAfter;
        public bool cacheOwned, externalServicesIsolated, startupGoGated, sourceAssetsUnchanged;
        public bool silentDownloadUiVerified, largeDownloadUiVerified, largeDownloadUiScreenshotVerified;
        public int silentUiObservedFrames, silentUiViolationFrames, largeDownloadUiVisibleFrames;
        public string scope = "Actual Addressables AssetBundleProvider and BundledAssetProvider use a fixture-built TextAsset bundle over loopback HTTP and Unity Caching in a dedicated /tmp directory. The real StartUpPresentation.OnStart/PrepareStartup/DownloadAndStart, real dependency downloader and real confirmation/warning callbacks execute. App-version and CommonSetting are fixture-owned local locator entries. Startup Go is gated before Starter initialization. Each cache stage is a new Editor process; no production catalog, authentication, account, purchase or advertising service is used.";
        public string limitation = "macOS Editor diagnostic TextAsset bundles and a synthetic localhost resource key do not identify the user's device-specific 20.2 KB missing bundle or prove physical-device cache persistence. The fixture waits for its dedicated cache to be ready before startup; observed readiness is evidence, not a production race diagnosis. The fixture gates Go before unrelated game/account initialization. Only this dedicated temporary cache is created; no global cache clear is performed.";
        public List<Frame> frames = new List<Frame>();
        public List<Request> requests = new List<Request>();
        public List<CacheReadiness> cacheReadiness = new List<CacheReadiness>();
        public List<string> screenshots = new List<string>(), checks = new List<string>(), controlledFailures = new List<string>(), errors = new List<string>();
    }

    static PocketStrikerDownloadCacheValidation()
    {
        EditorApplication.playModeStateChanged += ModeChanged;
        if (SessionState.GetBool(Key, false)) { SuspendAutoload(); Attach(); }
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Key + ".Restore", false)) EditorApplication.delayCall += Restore;
    }

    public static void BuildReviewBundlesBatch()
    {
        var manifest = new BundleManifest { utcTime = DateTime.UtcNow.ToString("O"), projectPath = Directory.GetCurrentDirectory(), unityVersion = Application.unityVersion, buildTarget = BuildTarget.StandaloneOSX.ToString(), scope = "Disposable macOS Editor diagnostic TextAsset bundles only; no production player or Addressables content build. Production player script compilation is a separate iOS run." };
        try
        {
            Require(DisposableClone, "Diagnostic asset creation is allowed only in the disposable test clone.");
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Diagnostic bundle build requires a stopped Editor.");
            Directory.CreateDirectory(GeneratedDirectory); Directory.CreateDirectory(DataRoot);
            foreach (string variant in new[] { "small-v1", "small-v2", "large", "failure" })
            {
                string asset = GeneratedDirectory + "/payload.bytes";
                var bytes = new byte[variant == "large" ? 110000 : 20480];
                new System.Random(variant == "small-v1" ? 101 : variant == "small-v2" ? 102 : variant == "large" ? 103 : 104).NextBytes(bytes);
                File.WriteAllBytes(asset, bytes); AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
                string bundleName = variant.StartsWith("small", StringComparison.Ordinal) ? "ps_review_small.bundle" : "ps_review_" + variant + ".bundle";
                string directory = Path.Combine(DataRoot, "bundles", variant); Directory.CreateDirectory(directory);
                var result = BuildPipeline.BuildAssetBundles(directory, new[] { new AssetBundleBuild { assetBundleName = bundleName, assetNames = new[] { asset } } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneOSX);
                Require(result != null, "Diagnostic bundle build failed: " + variant);
                string path = Path.Combine(directory, bundleName);
                Require(BuildPipeline.GetCRCForAssetBundle(path, out uint crc), "Bundle CRC unavailable.");
                long size = new FileInfo(path).Length;
                Require(variant == "large" ? size > PocketStrikerDownloadPolicy.AutomaticDownloadLimitBytes : size > 0 && size <= PocketStrikerDownloadPolicy.AutomaticDownloadLimitBytes, "Diagnostic bundle size does not exercise its intended policy branch: " + variant + "=" + size);
                manifest.bundles.Add(new BundleRecord { variant = variant, bundleName = bundleName, bundlePath = path, assetKey = "ps_review_asset_" + variant, assetInternalName = asset.ToLowerInvariant(), hash = result.GetAssetBundleHash(bundleName).ToString(), bundleBytes = size, crc = crc, fileSha256 = Hash(File.ReadAllBytes(path)), payloadSha256 = Hash(bytes) });
            }
            Require(manifest.bundles[0].hash != manifest.bundles[1].hash, "Changed payload must have a different bundle hash.");
            manifest.complete = manifest.passed = true;
        }
        catch (Exception exception) { manifest.errors.Add(exception.ToString()); }
        finally
        {
            AssetDatabase.DeleteAsset(GeneratedDirectory);
            Directory.CreateDirectory(DataRoot); File.WriteAllText(Path.Combine(DataRoot, "bundle-manifest.json"), JsonUtility.ToJson(manifest, true));
            Debug.Log("[ClientCacheBundleBuild] " + (manifest.passed ? "PASS" : "FAIL"));
            EditorApplication.Exit(manifest.passed ? 0 : 1);
        }
    }

    public static void StartBatch()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start from a stopped Editor.");
        Require(DisposableClone, "Cache review requires its isolated disposable clone.");
        Require(new Uri(Server).IsLoopback, "Only a loopback server is permitted.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        int fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode); Require(fast >= 0, "FastMode builder unavailable.");
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex); settings.ActivePlayModeDataBuilderIndex = fast;
        SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O")); SessionState.SetString(Key + ".Errors", "");
        SessionState.SetBool(Key + ".EarliestReady", Caching.ready);
        SessionState.SetString(Key + ".EarliestUtc", DateTime.UtcNow.ToString("O"));
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Running", false); SessionState.SetBool(Key + ".Restore", false);
        const string layout = "UserSettings/Layouts/default-6000.dwlt";
        SessionState.SetString(Key + ".Layout", File.Exists(layout) ? Convert.ToBase64String(File.ReadAllBytes(layout)) : "");
        SuspendAutoload(); Attach();
        var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView", true));
        view.GetType().GetMethod("SetCustomResolution", Fields).Invoke(view, new object[] { new Vector2(375, 667), "Client Cache Review" });
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>().enabled = false;
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        SceneManager.sceneLoaded -= Isolate; SceneManager.sceneLoaded += Isolate;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }
    static void Isolate(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        foreach (var startup in UnityEngine.Object.FindObjectsByType<StartUpPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None)) startup.enabled = false;
        foreach (var pre in UnityEngine.Object.FindObjectsByType<mainMenu.PreScene>(FindObjectsInactive.Include, FindObjectsSortMode.None)) pre.enabled = false;
        foreach (var iap in UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { iap.gameObject.SetActive(false); UnityEngine.Object.Destroy(iap.gameObject); }
        IAPManager.Target = null;
        foreach (var ads in UnityEngine.Object.FindObjectsByType<AdsInitializer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { ads.gameObject.SetActive(false); UnityEngine.Object.Destroy(ads.gameObject); }
        foreach (var input in UnityEngine.Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None)) input.enabled = false;
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (expectedFailure && (message.Contains("503") || message.Contains("ps_review_failure") || message.Contains("Required resources failed after") || message.Contains("Failed to download required game resources") || message.Contains("Dependency Exception")))
        { if (report != null) { report.expectedFailureLogs++; report.controlledFailures.Add(message); } return; }
        SessionState.SetString(Key + ".Errors", SessionState.GetString(Key + ".Errors", "") + message + "\n" + stack + "\n");
    }
    static void Poll()
    {
        if (finishing || !SessionState.GetBool(Key, false)) return;
        string error = SessionState.GetString(Key + ".Errors", "");
        if (error.Length > 0) { Finish(error); return; }
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Started", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > 360) { Finish("Cache review deadline exceeded: " + report?.phase); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        SessionState.SetBool(Key + ".Running", true); Run().Forget();
    }
    static async UniTask Run()
    {
        report = new Report { utcTime = DateTime.UtcNow.ToString("O"), stage = Stage, unityVersion = Application.unityVersion, processId = System.Diagnostics.Process.GetCurrentProcess().Id, projectPath = Directory.GetCurrentDirectory(), cachePath = Path.Combine(DataRoot, "cache"), automaticLimitBytes = PocketStrikerDownloadPolicy.AutomaticDownloadLimitBytes, provider = typeof(AssetBundleProvider).FullName, startupMethod = "StartUpPresentation.OnStart => PrepareStartup => DownloadAndStart; Go gated" };
        Directory.CreateDirectory(Output);
        report.cacheReadiness.Add(new CacheReadiness { utc = SessionState.GetString(Key + ".EarliestUtc", ""), purpose = "earliest-stopped-editor-before-play", cachingReady = SessionState.GetBool(Key + ".EarliestReady", false) });
        var protectedHashes = ProtectedPaths().ToDictionary(path => path, path => Hash(File.ReadAllBytes(path)));
        try
        {
            await UniTask.NextFrame(); Require(!PlayFab.PlayFabClientAPI.IsClientLoggedIn(), "Refuse a logged-in account.");
            report.phase = "local-provider-initialization"; Save();
            AppSetting.Value = new AppSetting { Language = SystemLanguage.Chinese };
            await Translate.LoadLanguageCodes(_ => UniTask.FromResult(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ExternalAssets/Config/LanguageCode.csv")));
            // A local prefab may already have started auto-releasing initialization.
            // Hold an explicit reference across its Task continuation.
            var initialization = Addressables.ResourceManager.Acquire(Addressables.InitializeAsync(false));
            await initialization.Task; Require(initialization.Status == AsyncOperationStatus.Succeeded, "Local Addressables initialization failed."); Addressables.Release(initialization);
            originals.AddRange(Addressables.ResourceLocators);
            common = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CommonSetting>("Assets/Setting/CommonSetting.asset"));
            common.DownLoadLabels.Clear(); common.DownLoadLabels.Add(Label);
            var localProvider = new LocalProvider(); Addressables.ResourceManager.ResourceProviders.Add(localProvider);
            var bundleProvider = new AssetBundleProvider(); bundleProvider.Initialize(BundleProviderId, null);
            var assetProvider = new BundledAssetProvider(); assetProvider.Initialize(AssetProviderId, null);
            AddProvider(bundleProvider); AddProvider(assetProvider);
            locator = new OwnedLocator(originals, common, localProvider.ProviderId);
            foreach (var original in originals) Addressables.RemoveResourceLocator(original); Addressables.AddResourceLocator(locator);
            oldWebRequest = Addressables.WebRequestOverride;
            Addressables.WebRequestOverride = request =>
            {
                var uri = new Uri(request.url); Require(uri.IsLoopback, "Blocked external web request: " + uri.Host);
                report.requests.Add(new Request { utc = DateTime.UtcNow.ToString("O"), url = request.url });
                // Calling a saved ConfigureRequest here would form a cycle after
                // Configure chains this guard. The real policy remains outermost.
            };
            PocketStrikerDownloadPolicy.Configure();
            Directory.CreateDirectory(report.cachePath); oldCache = Caching.currentCacheForWriting;
            ownedCache = Caching.GetCacheByPath(report.cachePath); if (!ownedCache.valid) ownedCache = Caching.AddCache(report.cachePath); Require(ownedCache.valid, "Dedicated cache registration failed.");
            report.cacheReadiness.Add(new CacheReadiness { utc = DateTime.UtcNow.ToString("O"), purpose = "owned-cache-registered-before-fixture-ready-wait", cachingReady = Caching.ready, ownedCacheReady = ownedCache.valid && Caching.ready });
            await Wait(() => Caching.ready, 20, "dedicated cache ready"); Caching.currentCacheForWriting = ownedCache;
            report.cacheOwned = true; report.externalServicesIsolated = true;
            var manifest = JsonUtility.FromJson<BundleManifest>(File.ReadAllText(Path.Combine(DataRoot, "bundle-manifest.json")));
            Require(manifest.passed && manifest.complete, "Diagnostic bundle manifest failed.");
            string variant = Stage == "HashChange" ? "small-v2" : Stage == "LargeConfirm" ? "large" : Stage == "FailureRecovery" ? "failure" : "small-v1";
            var bundle = manifest.bundles.Single(item => item.variant == variant); locator.SetBundle(bundle);
            report.bundleName = bundle.bundleName; report.hash = bundle.hash; report.payloadSha256 = bundle.payloadSha256;
            report.cachedBefore = Cached(bundle); report.requiredBefore = await Size();
            bool warm = Stage == "Restart1" || Stage == "Restart2";
            Require(warm ? report.cachedBefore && report.requiredBefore == 0 : !report.cachedBefore && report.requiredBefore == bundle.bundleBytes, "Fresh-process cache/remaining size does not match stage " + Stage);
            if (Stage == "HashChange") { var prior = manifest.bundles.Single(item => item.variant == "small-v1"); report.previousHash = prior.hash; report.previousHashStillCached = Cached(prior); Require(report.previousHashStillCached, "Old hash was not persisted across processes."); }
            var startup = Bind(); report.startupGoGated = true; report.phase = "actual-production-startup"; Save();
            report.cacheReadiness.Add(new CacheReadiness { utc = DateTime.UtcNow.ToString("O"), purpose = "before-production-PrepareStartup-size-and-download", cachingReady = Caching.ready, ownedCacheReady = ownedCache.valid && Caching.ready, versionCached = Cached(bundle), requiredBytes = report.requiredBefore });
            if (Stage == "FailureRecovery") await Control("failure");
            expectedFailure = Stage == "FailureRecovery";
            bool startupDone = false;
            var startupTask = Invoke(startup, "OnStart");
            var startupObservation = ObserveUntil(startupTask, () => startupDone = true, "initial-startup").Preserve();
            if (Stage == "LargeConfirm")
            {
                await Wait(() => Popup() != null, 25, "actual large-download confirmation"); report.confirmations++;
                await Wait(() => startupDone, 25, "confirmation startup task completion");
                await startupObservation;
                var popup = Popup(); Require(Get<BOButton>(popup, "NoButton").gameObject.activeInHierarchy, "Large-download popup is not a confirmation.");
                await Screenshot("large-confirm"); Click(Get<BOButton>(popup, "YesButton"));
                await ObserveUntil(
                    Wait(() => Cached(bundle) && UILayerLoader.Get<ProgressLayer>() == null && Popup() == null, 45, "confirmed real bundle download complete"),
                    null, "large-confirmed-download");
            }
            else
            {
                await Wait(() => startupDone, 75, "actual startup task completion");
                await startupObservation;
            }
            if (Stage == "FailureRecovery")
            {
                Require(!Cached(bundle) && Popup() != null, "Offline failure did not expose the real retry popup.");
                report.warnings++; Require(!Get<BOButton>(Popup(), "NoButton").gameObject.activeInHierarchy, "Failure popup unexpectedly confirms download.");
                await Screenshot("offline-warning"); report.sceneHandleBefore = SceneManager.GetActiveScene().handle.GetRawData();
                await Control("healthy"); Click(Get<BOButton>(Popup(), "YesButton"));
                await Wait(() => SceneManager.GetActiveScene().handle.GetRawData() != report.sceneHandleBefore && UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>() != null, 15, "native retry callback reloads actual scene zero");
                await UniTask.NextFrame(); report.sceneHandleAfter = SceneManager.GetActiveScene().handle.GetRawData(); report.retrySceneReloaded = true;
                expectedFailure = false; startup = Bind();
                bool recoveryDone = false;
                var recoveryObservation = ObserveUntil(Invoke(startup, "OnStart"), () => recoveryDone = true, "failure-recovery-startup").Preserve();
                await Wait(() => recoveryDone, 75, "actual recovery startup task completion");
                await recoveryObservation;
            }
            VerifyDownloadUiObservations();
            Require(report.errors.Count == 0, "Download UI observation failed; see the recorded errors.");
            report.cachedAfter = Cached(bundle); report.requiredAfter = await Size(); report.noConfirmation = report.confirmations == 0;
            Require(report.cachedAfter && report.requiredAfter == 0, "Actual bundle cache did not become available with zero remaining size.");
            Require(Popup() == null, "A popup remains after successful preparation.");
            if (warm) Require(report.requests.Count == 0, "Warm restart unexpectedly issued HTTP bundle requests.");
            else Require(report.requests.Count > 0, "Cold resource preparation did not issue an actual provider web request.");
            var loaded = Addressables.LoadAssetAsync<TextAsset>(bundle.assetKey); await loaded.Task;
            Require(loaded.Status == AsyncOperationStatus.Succeeded, "Actual cached TextAsset load failed.");
            report.observedPayloadSha256 = Hash(loaded.Result.bytes); Addressables.Release(loaded);
            Require(report.observedPayloadSha256 == bundle.payloadSha256, "Actual loaded bundle payload differs from its diagnostic source.");
            report.checks.Add("Actual startup policy branch, real provider payload, cache validity and zero remaining size verified.");
            await Screenshot("startup-complete");
            report.sourceAssetsUnchanged = protectedHashes.All(pair => Hash(File.ReadAllBytes(pair.Key)) == pair.Value);
            Require(report.sourceAssetsUnchanged, "Production source inputs changed during the cache review.");
            report.complete = report.passed = true;
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            expectedFailure = false;
            if (oldCache.valid) Caching.currentCacheForWriting = oldCache;
            if (ownedCache.valid) Caching.RemoveCache(ownedCache);
            Finish(null);
        }
    }
    static async UniTask ObserveUntil(UniTask task, Action done, string observation)
    {
        float started = Time.realtimeSinceStartup;
        bool capturedActualDownload = false;
        var converted = task.AsTask();
        System.Threading.Tasks.Task screenshot = null;
        try
        {
            // Capture entry and completion too: a fully cached synchronous route
            // must not pass the UI checks through an empty frame list.
            var frame = ObserveFrame(observation, started);
            while (!converted.IsCompleted && !finishing)
            {
                bool largeDownloadVisible = observation == "large-confirmed-download" && frame.postLateUpdateObserved && frame.progressBarActiveInHierarchy && frame.downloadUiText;
                bool smallTransferObserved = Stage != "LargeConfirm" && frame.progressLayer && frame.downloadedBytes > 0 && frame.progress < 1;
                if (!capturedActualDownload && (largeDownloadVisible || smallTransferObserved))
                {
                    capturedActualDownload = true;
                    // Do not suspend frame sampling while the screenshot awaits
                    // rendering/file completion. Its task is explicitly awaited.
                    screenshot = Screenshot(largeDownloadVisible ? "actual-confirmed-download-progress" : "actual-auto-download-progress", requireDownloadUi: largeDownloadVisible).AsTask();
                }
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                frame = ObserveFrame(observation, started, postLateUpdateObserved: true);
            }
            await converted;
            ObserveFrame(observation, started);
        }
        catch (Exception exception) { report.errors.Add("UI observation " + observation + ": " + exception); }
        finally
        {
            if (screenshot != null)
            {
                try { await screenshot; }
                catch (Exception exception) { report.errors.Add("UI screenshot " + observation + ": " + exception); }
            }
            done?.Invoke();
        }
    }
    static Frame ObserveFrame(string observation, float started, bool postLateUpdateObserved = false)
    {
        var progress = UILayerLoader.Get<ProgressLayer>();
        var slider = progress != null ? Get<Slider>(progress, "progressBar") : null;
        string infoText = progress != null ? Get<Text>(progress, "info").text : "";
        var frame = new Frame
        {
            frame = Time.frameCount, utc = DateTime.UtcNow.ToString("O"), observation = observation,
            elapsed = Time.realtimeSinceStartup - started, progress = AddressablesLogic.DownloadProgress,
            downloadedBytes = AddressablesLogic.DownloadedBytes, progressLayer = progress != null,
            popup = Popup() != null, slider = slider != null ? slider.value : 0,
            progressBarActiveInHierarchy = slider != null && slider.gameObject.activeInHierarchy,
            infoText = infoText, downloadUiText = HasDownloadUiText(infoText), postLateUpdateObserved = postLateUpdateObserved
        };
        report.frames.Add(frame);
        return frame;
    }
    static bool HasDownloadUiText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return Regex.IsMatch(text, @"\d+(?:[.,]\d+)?\s*(?:KB|MB|GB|B)(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            text.IndexOf("download", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.Contains("ダウンロード") || text.Contains("下载");
    }
    static void VerifyDownloadUiObservations()
    {
        if (Stage == "LargeConfirm")
        {
            report.largeDownloadUiVisibleFrames = report.frames.Count(frame => frame.observation == "large-confirmed-download" && frame.postLateUpdateObserved && frame.progressBarActiveInHierarchy && frame.downloadUiText);
            report.largeDownloadUiVerified = report.largeDownloadUiVisibleFrames > 0 && report.largeDownloadUiScreenshotVerified && report.screenshots.Any(path => Path.GetFileName(path) == "actual-confirmed-download-progress.png");
            Require(report.largeDownloadUiVerified, "Confirmed large download did not record its real visible progress UI and screenshot.");
            report.checks.Add("Native large-download confirmation retained real visible Downloading progress after Yes, with frame and screenshot evidence.");
            return;
        }
        report.silentUiObservedFrames = report.frames.Count;
        report.silentUiViolationFrames = report.frames.Count(frame => frame.progressBarActiveInHierarchy || frame.downloadUiText);
        bool initialObserved = report.frames.Any(frame => frame.observation == "initial-startup");
        bool recoveryObserved = Stage != "FailureRecovery" || report.frames.Any(frame => frame.observation == "failure-recovery-startup");
        report.silentDownloadUiVerified = initialObserved && recoveryObserved && report.silentUiObservedFrames > 0 && report.silentUiViolationFrames == 0;
        Require(report.silentDownloadUiVerified, "Small-resource startup exposed a download progress bar/text or lacked actual startup/recovery frame observations.");
        report.checks.Add("Every observed small-resource startup/recovery frame had no active download progress bar and no byte/download/retry-download text; ordinary resource-check Loading was allowed.");
    }
    static void AddProvider(IResourceProvider provider)
    { if (!Addressables.ResourceManager.ResourceProviders.Any(item => item.ProviderId == provider.ProviderId)) Addressables.ResourceManager.ResourceProviders.Add(provider); }
    static StartUpPresentation Bind()
    {
        var startup = UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>(); Require(startup != null && !startup.enabled, "Isolated actual startup is missing.");
        PosCal.Canvas = Get<Canvas>(startup, "canvas"); PosCal.SafeAreaRect = Get<RectTransform>(startup, "safeAreaRect"); PosCal.TestIni(); UILayerLoader.SetHanger(PosCal.SafeAreaRect, PosCal.Canvas.transform); UILayerLoader.SetEffectBg(null);
        AppSetting.BGMSource = Get<AudioSource>(startup, "audioSource"); AppSetting.UiAudioSource = Get<AudioSource>(startup, "uiAudioSource");
        typeof(StartUpPresentation).GetField("startupInProgress", Fields).SetValue(startup, true);
        Require(EventSystem.current != null, "Actual EventSystem unavailable."); return startup;
    }
    static UniTask Invoke(StartUpPresentation target, string method) => (UniTask)typeof(StartUpPresentation).GetMethod(method, Fields).Invoke(target, null);
    static async UniTask<long> Size()
    {
        var sample = new CacheReadiness { utc = DateTime.UtcNow.ToString("O"), purpose = "before-actual-GetDownloadSizeAsync", cachingReady = Caching.ready, ownedCacheReady = ownedCache.valid && Caching.ready };
        report.cacheReadiness.Add(sample);
        var handle = Addressables.GetDownloadSizeAsync(new[] { Label }); await handle.Task; Require(handle.Status == AsyncOperationStatus.Succeeded, "Actual required size inspection failed."); long bytes = handle.Result; sample.requiredBytes = bytes; Addressables.Release(handle); return bytes;
    }
    static bool Cached(BundleRecord record) => Caching.IsVersionCached(new CachedAssetBundle(record.bundleName, Hash128.Parse(record.hash)));
    static PopupLayer Popup() => UILayerLoader.Get<PopupLayer>();
    static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Fields).GetValue(value);
    static void Click(BOButton button)
    {
        Canvas.ForceUpdateCanvases(); var rect = button.GetComponent<RectTransform>();
        var point = RectTransformUtility.WorldToScreenPoint(PosCal.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : PosCal.Canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Native confirmation/retry raycast was covered: " + button.name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler); report.nativePointerClicks++;
    }
    static async UniTask Control(string mode)
    { using (var request = UnityWebRequest.Get(Server + "/control/" + mode)) { await request.SendWebRequest(); Require(request.result == UnityWebRequest.Result.Success, "Owned server control failed."); } }
    static async UniTask Wait(Func<bool> predicate, double seconds, string purpose)
    { double deadline = Time.realtimeSinceStartupAsDouble + seconds; while (!predicate()) { Require(!finishing, "Review interrupted."); if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(purpose); await UniTask.Delay(60, DelayType.Realtime); } }
    static async UniTask Screenshot(string name, bool requireDownloadUi = false)
    {
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        if (requireDownloadUi)
        {
            var progress = UILayerLoader.Get<ProgressLayer>();
            Require(progress != null && Get<Slider>(progress, "progressBar").gameObject.activeInHierarchy && HasDownloadUiText(Get<Text>(progress, "info").text),
                "Confirmed download progress UI was not actually visible when its screenshot was requested.");
        }
        string path = Path.GetFullPath(Path.Combine(Output, name + ".png")); ScreenCapture.CaptureScreenshot(path);
        await Wait(() => File.Exists(path) && new FileInfo(path).Length > 100, 8, "native screenshot"); Require(new FileInfo(path).Length < 10 * 1024 * 1024, "Native screenshot exceeds delivery size."); report.screenshots.Add(path); Save();
        if (requireDownloadUi) report.largeDownloadUiScreenshotVerified = true;
    }
    static IEnumerable<string> ProtectedPaths()
    { yield return "Assets/Launcher/StartUpPresentation.cs"; yield return "Assets/Addressables/PocketStrikerDownloadPolicy.cs"; yield return "Assets/Addressables/PocketStrikerDependencyDownloader.cs"; yield return "Assets/Resources/DummyLayerSystem/UnitInstructionLayer.prefab"; yield return "Assets/Editor/PocketStrikerDownloadCacheValidation.cs"; }
    static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Save() { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true)); }
    static void Finish(string error)
    {
        if (finishing) return; finishing = true; report ??= new Report { utcTime = DateTime.UtcNow.ToString("O"), stage = Stage };
        if (!string.IsNullOrEmpty(error)) report.errors.Add(error); report.finishedUtc = DateTime.UtcNow.ToString("O"); report.passed &= report.complete && report.errors.Count == 0; Save();
        SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Restore", true); SessionState.SetInt(Key + ".ExitCode", report.passed ? 0 : 1);
        Application.logMessageReceived -= OnLog; SceneManager.sceneLoaded -= Isolate; EditorApplication.update -= Poll;
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false; else EditorApplication.delayCall += Restore;
    }
    static void ModeChanged(PlayModeStateChange state)
    { if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".Restore", false)) EditorApplication.delayCall += Restore; }
    static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return; SessionState.SetBool(Key + ".Restore", false);
        var settings = AddressableAssetSettingsDefaultObject.Settings; if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        string layout = SessionState.GetString(Key + ".Layout", ""); if (layout.Length > 0) File.WriteAllBytes("UserSettings/Layouts/default-6000.dwlt", Convert.FromBase64String(layout));
        Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD = SessionState.GetBool(Key + ".Autoload", false);
        Debug.Log("[ClientCacheReview] " + (report != null && report.passed ? "PASS" : "completed") + " " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".ExitCode", 1));
    }
    static void SuspendAutoload()
    { if (!SessionState.GetBool(Key + ".AutoloadSaved", false)) { SessionState.SetBool(Key + ".Autoload", Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD); SessionState.SetBool(Key + ".AutoloadSaved", true); } Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD = false; }

    sealed class LocalProvider : ResourceProviderBase
    {
        public override Type GetDefaultType(IResourceLocation location) => location.ResourceType;
        public override void Provide(ProvideHandle handle) => handle.Complete(handle.Location.Data, true, null);
    }
    sealed class OwnedLocator : IResourceLocator
    {
        readonly List<IResourceLocator> fallback;
        readonly Dictionary<object, IList<IResourceLocation>> owned = new Dictionary<object, IList<IResourceLocation>>();
        public string LocatorId => "PocketStrikerOwnedClientCacheReview";
        public IEnumerable<object> Keys => owned.Keys.Concat(fallback.SelectMany(item => item.Keys));
#if !ENABLE_JSON_CATALOG
        public IEnumerable<IResourceLocation> AllLocations => owned.Values.SelectMany(value => value).Concat(fallback.SelectMany(value => value.AllLocations));
#endif
        public OwnedLocator(List<IResourceLocator> original, CommonSetting setting, string provider)
        {
            fallback = original;
            var version = new ResourceLocationBase(AddressablesResourcePolicy.AppVersionKey, "owned-version", provider, typeof(TextAsset)) { Data = new TextAsset("{\"version\":\"" + Application.version + "\"}") };
            var config = new ResourceLocationBase(AddressablesResourcePolicy.CommonSettingKey, "owned-common", provider, typeof(CommonSetting)) { Data = setting };
            owned[AddressablesResourcePolicy.AppVersionKey] = new[] { version }; owned[AddressablesResourcePolicy.CommonSettingKey] = new[] { config }; owned[AddressablesResourcePolicy.ConfigLabel] = new[] { config };
        }
        public void SetBundle(BundleRecord record)
        {
            var bundle = new ResourceLocationBase(record.bundleName, Server + "/bundles/" + record.variant + "/" + record.bundleName, BundleProviderId, typeof(IAssetBundleResource))
            { Data = new AssetBundleRequestOptions { Hash = record.hash, BundleName = record.bundleName, BundleSize = record.bundleBytes, Crc = record.crc, UseCrcForCachedBundle = true, ClearOtherCachedVersionsWhenLoaded = false } };
            var asset = new ResourceLocationBase(record.assetKey, record.assetInternalName, AssetProviderId, typeof(TextAsset), bundle);
            owned[Label] = new[] { asset }; owned[record.assetKey] = new[] { asset };
        }
        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            if (owned.TryGetValue(key, out var found)) { locations = found.Where(item => type == null || type.IsAssignableFrom(item.ResourceType)).ToArray(); return locations.Count > 0; }
            foreach (var item in fallback) if (item.Locate(key, type, out locations)) return true;
            locations = null; return false;
        }
    }
}
