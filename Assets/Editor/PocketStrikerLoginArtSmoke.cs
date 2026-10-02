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

/// <summary>Rendered 2D title branch, responsive layouts and production device login. No remote configuration changes.</summary>
[InitializeOnLoad]
public static class PocketStrikerLoginArtSmoke
{
    const string Key = "PocketStriker.LoginArtSmoke";
    const string Startup = "Assets/Scene/ABLoadScene/Scene1.unity";
    const double MaximumSeconds = 360;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static bool finishing;
    static Report report;
    static CancellationTokenSource cancellation;
    static string Output => SessionState.GetString(Key + ".Output", "Logs/LoginArt/after");

    [Serializable] public sealed class Report
    {
        public bool passed, baseline, realAccountLogin, returnedToHome;
        public string unityVersion, utcTime, phase, outcome;
        public int pointerClicks, textResponseLogs, imageResponseLogs, providerErrorLogs, otherErrorLogs;
        public string limitation = "Actual startup with local Addressables. Baseline selects the prior 2D branch in memory; after uses the saved production default without an override. No remote title data changed. Editor dimensions, not physical device safe areas. Real production device login, password open/cancel only; no credentials or rewards modified.";
        public List<string> screenshots = new List<string>();
        public List<string> failures = new List<string>();
        public List<string> activeEffects = new List<string>();
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

    static void Begin(string label, bool baseline)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start the story smoke from a stopped editor.");
        foreach (var index in Enumerable.Range(0, SceneManager.sceneCount))
            Require(!SceneManager.GetSceneAt(index).isDirty, "Save open scenes before the story smoke.");
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
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key, true);
        finishing = false;
        report = null;
        settings.ActivePlayModeDataBuilderIndex = fast;
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var view = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetMethod("SetCustomResolution", Fields)?.Invoke(view,
            new object[] { new Vector2(540, 960), "PocketStriker AI Story Live Smoke" });
        Attach();
        EditorSceneManager.OpenScene(Startup, OpenSceneMode.Single);
        var presentation = UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>();
        var presentationField = typeof(StartUpPresentation).GetField("frontSceneFight", Fields);
        // Baseline selects the previous authored 2D branch; after must use the saved default.
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
        if (!EditorApplication.isPlaying || !Starter.ConfigInitialised || SessionState.GetBool(Key + ".Running", false)) return;
        var title = Layer<TitleScreenLayer>();
        if (title == null) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { baseline = SessionState.GetBool(Key + ".Baseline", false),
            unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O"), phase = "title" };
        Directory.CreateDirectory(Output);
        cancellation = new CancellationTokenSource();
        try
        {
            await Stable();
            await UniTask.Delay(11000, DelayType.Realtime, cancellationToken: cancellation.Token);
            Require(Layer<TitleBgLayer>() != null, "Actual 2D startup background did not load.");
            report.activeEffects.AddRange(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.name.IndexOf("bgEffect", StringComparison.OrdinalIgnoreCase) >= 0).Select(t => t.name));
            if (!report.baseline) Require(report.activeEffects.Count == 0, "Login geometric overlay is still active.");
            foreach (var size in new[]{new Vector2(540,960), new Vector2(390,844), new Vector2(768,1024)})
            {
                var t = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
                var v = EditorWindow.GetWindow(t);
                t.GetMethod("SetCustomResolution", Fields).Invoke(v, new object[]{size,"Login Art Review"});
                await Stable();
                await Screenshot("title-" + (int)size.x + "x" + (int)size.y);
                var title = Layer<TitleScreenLayer>();
                await NativeClick(Field<Button>(title,"accountLoginBtn"));
                await Stable();
                await Screenshot("password-" + (int)size.x + "x" + (int)size.y);
                await NativeClick(Field<Button>(title,"cancelBtn"));
                await Stable();
            }
            await UniTask.Delay(5000, DelayType.Realtime, cancellationToken: cancellation.Token);
            await Screenshot("title-settled");
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
