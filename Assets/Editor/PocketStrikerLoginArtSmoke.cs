using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using FightScene;
using mainMenu;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Rendered title art and responsive native controls. Explicit offline entries omit all account requests.</summary>
[InitializeOnLoad]
public static class PocketStrikerLoginArtSmoke
{
    const string Key = "PocketStriker.LoginArtSmoke";
    const string Startup = "Assets/Scene/ABLoadScene/Scene1.unity";
    const string PreviousArtwork = "Assets/AIStory/Art/PocketStrikerLogin.png";
    const string CurrentArtwork = "Assets/AIStory/Art/PocketStrikerLoginMultiverse-v2.png";
    const double MaximumSeconds = 360;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
    static bool finishing;
    static Report report;
    static CancellationTokenSource cancellation;
    static string Output => SessionState.GetString(Key + ".Output", "Logs/LoginArt/after");

    [Serializable] public sealed class Report
    {
        public bool passed, baseline, titleOnly, realAccountLogin, returnedToHome;
        public string unityVersion, utcTime, phase, outcome, artworkPath;
        public int pointerClicks, textResponseLogs, imageResponseLogs, providerErrorLogs, otherErrorLogs;
        public string limitation;
        public List<string> screenshots = new List<string>();
        public List<string> failures = new List<string>();
        public List<string> activeEffects = new List<string>();
        public List<string> checks = new List<string>();
        public List<ArtworkLayout> layouts = new List<ArtworkLayout>();
    }
    [Serializable] public sealed class ArtworkLayout
    {
        public string viewport;
        public Rect artworkRect, titleRect, accountRect, primaryStoneRect;
    }
    [Serializable] sealed class SceneBackup { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded; public bool active; }

    static PocketStrikerLoginArtSmoke()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key, false)) Attach();
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Key + ".RestoreScenes", false))
            EditorApplication.delayCall += RestoreScenes;
    }

    public static void StartBaselineBatch() => Begin("baseline", true);
    public static void StartAfterBatch() => Begin("after", false);
    public static void StartOfflineBaselineBatch() => Begin("Multiverse20261002/baseline", true, true);
    public static void StartOfflineAfterBatch() => Begin("Multiverse20261002/after", false, true);

    static void Begin(string label, bool baseline, bool titleOnly = false)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start the login artwork smoke from a stopped editor.");
        if (!Application.isBatchMode)
            foreach (var index in Enumerable.Range(0, SceneManager.sceneCount))
                Require(!SceneManager.GetSceneAt(index).isDirty, "Save open scenes before the login artwork smoke.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Require(settings != null, "Addressables settings are missing.");
        int fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        Require(fast >= 0, "Addressables Fast Mode is unavailable.");
        var backup = new SceneBackup();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            backup.scenes.Add(new SceneRecord { path = scene.path, loaded = scene.isLoaded, active = scene.isActive });
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(backup));
        SessionState.SetBool(Key + ".RestoreScenes", false);
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex);
        SessionState.SetString(Key + ".Start", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Output", "Logs/LoginArt/" + label);
        SessionState.SetBool(Key + ".Baseline", baseline);
        SessionState.SetBool(Key + ".TitleOnly", titleOnly);
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key, true);
        finishing = false;
        report = null;
        settings.ActivePlayModeDataBuilderIndex = fast;
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var view = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetMethod("SetCustomResolution", Fields)?.Invoke(view,
            new object[] { new Vector2(540, 960), "PocketStriker Login Artwork" });
        Attach();
        EditorSceneManager.OpenScene(Startup, OpenSceneMode.Single);
        var presentation = UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>();
        Require(presentation != null, "Actual startup presentation is missing.");
        var presentationField = typeof(StartUpPresentation).GetField("frontSceneFight", Fields);
        // Offline still uses Scene1's actual camera, canvas, EventSystem, layer loader
        // and title prefabs, but disables network startup before Start can run.
        if (titleOnly) presentation.enabled = false;
        if (baseline) presentationField.SetValue(presentation, false);
        else Require(!(bool)presentationField.GetValue(presentation), "The saved startup default must show the login artwork.");
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    static void CaptureLog(string message, string stack, LogType type)
    {
        if (report == null) return;
        // Count categories only. Never copy a provider response, URL or login identifier.
        if (message.Contains("[Gemini] generateGeminiText raw result")) report.textResponseLogs++;
        if (message.Contains("[Gemini] generateGeminiImages raw result")) report.imageResponseLogs++;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message.Contains("AI story") || message.Contains("AI Story") || message.Contains("from AI") || message.Contains("Gemini")
            || message.Contains("Azure Function") || message.Contains("[AIServiceManager]")) report.providerErrorLogs++;
        else report.otherErrorLogs++;
    }

    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        var started = DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime();
        if ((DateTime.UtcNow - started).TotalSeconds > MaximumSeconds) { Finish("Overall 360-second deadline exceeded."); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        if (SessionState.GetBool(Key + ".TitleOnly", false))
        {
            SessionState.SetBool(Key + ".Running", true);
            Run().Forget();
            return;
        }
        if (!Starter.ConfigInitialised) return;
        var title = Layer<TitleScreenLayer>();
        if (title == null) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { baseline = SessionState.GetBool(Key + ".Baseline", false),
            titleOnly = SessionState.GetBool(Key + ".TitleOnly", false),
            unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O"), phase = "title" };
        report.limitation = report.titleOnly
            ? "Editor Play mode in Scene1 with its production camera/canvas/EventSystem and actual Resources title layers. StartUpPresentation is disabled before Start; only local settings, language CSV and Addressables Fast Mode artwork load. Baseline explicitly injects retained old artwork in memory; after uses production LoginArt without an override. Native account-modal open/cancel only; no touch-start, credentials, device login, account, story, shop or rewards requests. Editor dimensions, not physical device safe areas."
            : "Actual startup and local Addressables. Baseline explicitly swaps retained old artwork in memory; after uses production LoginArt. Includes real production device login and home. Editor dimensions, not physical device safe areas.";
        Directory.CreateDirectory(Output);
        cancellation = new CancellationTokenSource();
        try
        {
            if (report.titleOnly) await SetupOfflineTitle();
            await Stable();
            await UniTask.Delay(11000, DelayType.Realtime, cancellationToken: cancellation.Token);
            Require(Layer<TitleBgLayer>() != null, "Actual 2D startup background did not load.");
            var background = Layer<TitleBgLayer>();
            var artwork = Field<Image>(background, "targetImage");
            Require(artwork.sprite != null && AssetDatabase.GetAssetPath(artwork.sprite) == CurrentArtwork,
                "Production LoginArt did not load the new versioned artwork.");
            report.checks.Add("Production LoginArt resolves to " + CurrentArtwork);
            if (report.baseline)
            {
                artwork.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PreviousArtwork);
                Require(artwork.sprite != null, "Retained previous artwork is missing.");
                typeof(TitleBgLayer).GetMethod("FitLoginArtwork", Fields).Invoke(background, null);
            }
            report.artworkPath = AssetDatabase.GetAssetPath(artwork.sprite);
            report.activeEffects.AddRange(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.name.IndexOf("bgEffect", StringComparison.OrdinalIgnoreCase) >= 0).Select(t => t.name));
            if (!report.baseline) Require(report.activeEffects.Count == 0, "Login geometric overlay is still active.");
            foreach (var size in new[]{new Vector2(540,960), new Vector2(375,667), new Vector2(390,844), new Vector2(768,1024)})
            {
                var t = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
                var v = EditorWindow.GetWindow(t);
                t.GetMethod("SetCustomResolution", Fields).Invoke(v, new object[]{size,"Login Art Review"});
                await Stable();
                CheckArtworkLayout();
                await Screenshot("title-" + (int)size.x + "x" + (int)size.y);
                var title = Layer<TitleScreenLayer>();
                await NativeClick(Field<Button>(title,"accountLoginBtn"));
                await Stable();
                Require(Field<RectTransform>(title, "loginByPwTab").gameObject.activeInHierarchy,
                    "Native account button did not open the production password modal.");
                foreach (var name in new[] {"id", "password", "loginBtn", "cancelBtn"})
                    Require(InViewport(ScreenRect((RectTransform)Field<Component>(title, name).transform)),
                        "Password control is outside the viewport: " + name);
                await Screenshot("password-" + (int)size.x + "x" + (int)size.y);
                await NativeClick(Field<Button>(title,"cancelBtn"));
                await Stable();
                Require(Field<RectTransform>(title, "mainTab").gameObject.activeInHierarchy,
                    "Native cancel did not restore the production title tab.");
                report.checks.Add("Native account modal opened/cancelled at " + (int)size.x + "x" + (int)size.y);
            }
            await UniTask.Delay(5000, DelayType.Realtime, cancellationToken: cancellation.Token);
            await Screenshot("title-settled");
            if (report.titleOnly)
            {
                report.passed = report.otherErrorLogs == 0 && report.providerErrorLogs == 0 && report.pointerClicks == 8
                    && report.layouts.Count == 4;
                report.outcome = "Offline old/new artwork selection, four rendered title/modal sizes and eight native pointer clicks completed; no account request executed.";
                report.phase = "complete";
                Finish(null);
                return;
            }
            report.phase = "login";
            await NativeClick(Field<Button>(Layer<TitleScreenLayer>(), "touchScreenBtn"));
            await Wait(() => PreScene.target != null && PlayerAccountInfo.Me != null
                && ProcessesRunner.Main.currentProcess != null, 90, "production login");
            report.realAccountLogin = true;
            await Wait(() => Layer<FrontLayer>() != null && ProcessesRunner.Main.currentProcess?.CanEnterOtherProcess() == true
                && Layer<ProgressLayer>() == null, 45, "main menu readiness");
            await Stable();
            await DismissStartupModals();
            await Screenshot("home");
            report.returnedToHome = true;
            report.passed = report.otherErrorLogs == 0;
            report.outcome = "Title, repeated password open/cancel at three sizes, real device login and home completed.";
            report.phase = "complete";
            Finish(null);
        }
        catch(Exception e) { Finish(e.GetType().Name + ": " + e.Message); }
    }

    static async UniTask SetupOfflineTitle()
    {
        var presentation = UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>();
        Require(presentation != null && !presentation.enabled, "Offline smoke must disable network startup before Start.");
        var common = AssetDatabase.LoadAssetAtPath<CommonSetting>(
            AssetDatabase.GUIDToAssetPath("cc8bc4431333a4b94b1f8148807004f0"));
        Require(common != null, "Local registered CommonSetting is missing.");
        common.Initialise();
        AppSetting.Value.Language = SystemLanguage.Chinese;
        await Translate.LoadLanguageCodes(_ => UniTask.FromResult(
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ExternalAssets/Config/LanguageCode.csv")));
        PosCal.Canvas = Field<Canvas>(presentation, "canvas");
        PosCal.SafeAreaRect = Field<RectTransform>(presentation, "safeAreaRect");
        PosCal.TestIni();
        Loader.GetMethod("SetHanger", new[] {typeof(Transform), typeof(Transform)}).Invoke(null,
            new object[] {PosCal.SafeAreaRect, PosCal.Canvas.transform});
        var background = (TitleBgLayer)Loader.GetMethod("Load").MakeGenericMethod(typeof(TitleBgLayer)).Invoke(null,
            new object[] {true, null, true});
        await background.SetupLogin();
        var title = (TitleScreenLayer)Loader.GetMethod("Load").MakeGenericMethod(typeof(TitleScreenLayer)).Invoke(null,
            new object[] {true, null, true});
        title.Initialise(true);
        report.checks.Add("Offline Scene1 canvas/camera/EventSystem with production title layers; network startup disabled");
    }

    static void CheckArtworkLayout()
    {
        var image = Field<Image>(Layer<TitleBgLayer>(), "targetImage");
        var title = Layer<TitleScreenLayer>();
        var layout = new ArtworkLayout {viewport = Screen.width + "x" + Screen.height,
            artworkRect = ScreenRect(image.rectTransform), titleRect = ScreenRect(Field<Image>(title, "title").rectTransform),
            accountRect = ScreenRect((RectTransform)Field<Button>(title, "accountLoginBtn").transform)};
        Require(layout.artworkRect.xMin <= 1 && layout.artworkRect.yMin <= 1
            && layout.artworkRect.xMax >= Screen.width - 1 && layout.artworkRect.yMax >= Screen.height - 1,
            "Login artwork leaves an uncovered screen edge.");
        Require(InViewport(layout.titleRect) && InViewport(layout.accountRect), "Title or account control is clipped.");
        if (!report.baseline)
        {
            // Measured primary golden stone rectangle in the selected generated
            // source, using texture coordinates whose origin is bottom-left.
            layout.primaryStoneRect = new Rect(layout.artworkRect.x + .33f * layout.artworkRect.width,
                layout.artworkRect.y + .46f * layout.artworkRect.height,
                .36f * layout.artworkRect.width, .22f * layout.artworkRect.height);
            Require(InViewport(layout.primaryStoneRect), "Aspect fill cropped the main skill stone.");
            Require(!layout.primaryStoneRect.Overlaps(layout.titleRect)
                && !layout.primaryStoneRect.Overlaps(layout.accountRect), "Title or account control obscures the main skill stone.");
        }
        report.layouts.Add(layout);
    }

    static Rect ScreenRect(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
            points.Max(point => point.x), points.Max(point => point.y));
    }

    static bool InViewport(Rect rect) => rect.xMin >= -1 && rect.yMin >= -1
        && rect.xMax <= Screen.width + 1 && rect.yMax <= Screen.height + 1;

    static async UniTask DismissStartupModals()
    {
        var link = Layer<AskIfLinkDeviceLayer>();
        if (link != null) await NativeClick(Field<Button>(link, "No"));
        var popup = Layer<PopupLayer>();
        if (popup != null)
        {
            var no = Field<Button>(popup, "NoButton");
            await NativeClick(no.gameObject.activeInHierarchy ? no : Field<Button>(popup, "YesButton"));
        }
    }

    static async UniTask NativeClick(Button button)
    {
        if (button is BOButton) await Wait(() => !BOButton.AnyProcess, 2, "native button debounce");
        Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Native button is unavailable.");
        Require(EventSystem.current != null, "Production EventSystem is unavailable.");
        var rect = (RectTransform)button.transform;
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
            : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        Require(point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height, "Native button is outside the viewport.");
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Native pointer click is blocked.");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        report.pointerClicks++;
        await Stable();
    }

    static async UniTask Stable()
    {
        await UniTask.Delay(350, DelayType.Realtime, cancellationToken: cancellation?.Token ?? default);
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellation?.Token ?? default);
        Canvas.ForceUpdateCanvases();
    }

    static async UniTask Wait(Func<bool> predicate, double seconds, string purpose)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate())
        {
            cancellation?.Token.ThrowIfCancellationRequested();
            if (finishing) throw new OperationCanceledException();
            if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException(purpose);
            await UniTask.Delay(100, DelayType.Realtime, cancellationToken: cancellation?.Token ?? default);
        }
    }

    static async UniTask Screenshot(string name)
    {
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellation?.Token ?? default);
        string path = Path.GetFullPath(Path.Combine(Output, name + ".png"));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        await Wait(() => File.Exists(path) && new FileInfo(path).Length > 100, 5, "native screenshot");
        byte[] png = File.ReadAllBytes(path);
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Require(width == Screen.width && height == Screen.height, "Screenshot differs from the actual Game view size.");
        report.screenshots.Add(path);
        Save();
    }

    static T Layer<T>() where T : UILayer => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude)
        .FirstOrDefault(layer => layer != null && !layer.IsClosing);

    static T Field<T>(object target, string name)
    {
        Require(target != null, "Reflection target is unavailable.");
        var field = target.GetType().GetField(name, Fields);
        Require(field != null, "Expected production field is unavailable: " + name);
        return (T)field.GetValue(target);
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Save()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
    }

    static void Finish(string error)
    {
        if (finishing) return;
        finishing = true;
        report ??= new Report { baseline = SessionState.GetBool(Key + ".Baseline", false),
            titleOnly = SessionState.GetBool(Key + ".TitleOnly", false),
            unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O"), phase = "startup" };
        if (!string.IsNullOrEmpty(error)) report.failures.Add(error);
        report.passed &= report.failures.Count == 0;
        Save();
        SessionState.SetBool(Key, false);
        SessionState.SetBool(Key + ".RestoreScenes", true);
        Application.logMessageReceived -= CaptureLog;
        EditorApplication.update -= Poll;
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        Debug.Log("[LoginArtSmoke] " + (report.passed ? "PASS" : "INCOMPLETE/FAIL") + ": "
            + Path.GetFullPath(Path.Combine(Output, "report.json")));
        SessionState.SetBool(Key + ".ExitAfterRestore", Application.isBatchMode);
        SessionState.SetInt(Key + ".ExitCode", report.passed ? 0 : 1);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else EditorApplication.delayCall += RestoreScenes;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (SessionState.GetBool(Key, false)) Finish("Play mode stopped before coverage completed.");
        if (SessionState.GetBool(Key + ".RestoreScenes", false)) EditorApplication.delayCall += RestoreScenes;
    }

    static void RestoreScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Key + ".RestoreScenes", false);
        var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".Scenes", "{}"));
        if (backup?.scenes != null && backup.scenes.Count > 0)
        {
            var setup = backup.scenes.Where(scene => !string.IsNullOrEmpty(scene.path)).Select(scene => new SceneSetup
                { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        if (SessionState.GetBool(Key + ".ExitAfterRestore", false))
        {
            SessionState.SetBool(Key + ".ExitAfterRestore", false);
            EditorApplication.Exit(SessionState.GetInt(Key + ".ExitCode", 1));
        }
    }
}
