using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Production return history and loader regression checks without account or network services.</summary>
[InitializeOnLoad]
public static class PocketStrikerReturnValidation
{
    const string Key = "PocketStriker.ReturnValidation";
    const string Output = "Logs/UIArt/return-navigation.json";
    const int ExpectedCases = 9;
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
    static Report report;
    static bool finishing;

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion, utcTime;
        public int casesChecked, refusedAttempts, successfulReturns, callbackReentries, observedExceptions;
        public string scope = "Empty local Editor Play-mode scene, production ReturnLayer.Stack/POP/Clear and UILayerLoader with the actual Resources ReturnLayer prefab. Actual Button.onClick listeners execute repeated loading-gate refusals and retry; a local ProcessesRunner and MSceneProcess exercise the production CanEnterOtherProcess gate. LIFO, missing destinations, thrown callbacks, successful/refused callback reentry, clear/rebuild and append during callbacks preserve history.";
        public string limitation = "Service-free regression fixture. Native Button.onClick invocation and direct production POP calls are used, without screen pointer raycasts, account authentication, real asynchronous data requests or full menu/game navigation.";
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }
    [Serializable] sealed class SceneRecords { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }

    static PocketStrikerReturnValidation()
    {
        if (SessionState.GetBool(Key, false)) Attach();
    }

    [MenuItem("PocketStriker/Validation/Return Navigation")]
    public static void Validate() => Begin();

    // Run without -quit; the fixture exits batch mode after its Play-mode checks complete.
    public static void ValidateBatch() => Begin();

    static void Begin()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before isolated return validation.");
        var records = new SceneRecords();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
        {
            if (!Application.isBatchMode && scene.isLoaded)
                Require(!string.IsNullOrEmpty(scene.path) && !SceneManager.GetSceneByPath(scene.path).isDirty,
                    "Save open scenes before return validation uses its empty fixture scene.");
            records.scenes.Add(new SceneRecord { path = scene.path, loaded = scene.isLoaded, active = scene.isActive });
        }
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(records));
        SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key, true);
        finishing = false; report = null;
        Attach();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= CaptureError;
        Application.logMessageReceived += CaptureError;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }
    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string errors = SessionState.GetString(Key + ".Errors", "");
        if (errors.Length < 24000) SessionState.SetString(Key + ".Errors", errors + message + "\n" + stack + "\n");
    }
    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Started", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > 90)
        { Finish("Return navigation validation timed out."); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var previousCanvas = PosCal.Canvas;
        var previousSafe = PosCal.SafeAreaRect;
        var previousLogs = MainSceneLogger.Logs;
        var previousHistory = ReturnLayer.ReturnMissionList.ToArray();
        var previousHanger = Loader.GetField("_hanger", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var previousFullHanger = Loader.GetField("_fullScreenHanger", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var previousEffect = Loader.GetField("effectBg", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        GameObject rig = null;
        try
        {
            MainSceneLogger.Logs = new List<MainSceneLog>();
            ReturnLayer.ReturnMissionList.Clear();
            rig = new GameObject("Return Navigation Fixture", typeof(RectTransform), typeof(Canvas));
            var canvas = rig.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            PosCal.Canvas = canvas; PosCal.SafeAreaRect = (RectTransform)rig.transform;
            PosCal.TestIni();
            Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new object[] { rig.transform, rig.transform });
            Loader.GetMethod("SetEffectBg").Invoke(null, new object[] { null });
            await Settle();

            await LoadingGate();
            await Reset();
            MissingDestination();
            await Reset();
            Lifo();
            await Reset();
            CallbackException();
            await Reset();
            CallbackReentry(true);
            await Reset();
            CallbackReentry(false);
            await Reset();
            await ClearAndRebuild();
            await Reset();
            ClearAll();
            await Reset();
            AppendDuringReturn();
            await Reset();
            Require(GetLayer() == null && RawLayers() == 0, "Return layers remained after the completed checks.");
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            ReturnLayer.Clear();
            ReturnLayer.ReturnMissionList.AddRange(previousHistory);
            if (rig != null) UnityEngine.Object.Destroy(rig);
            await Settle();
            if (rig != null || RawLayers() != 0) report.errors.Add("Fixture objects survived their cleanup frames.");
            MainSceneLogger.Logs = previousLogs;
            PosCal.Canvas = previousCanvas; PosCal.SafeAreaRect = previousSafe;
            Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new[] { previousHanger, previousFullHanger });
            Loader.GetMethod("SetEffectBg").Invoke(null, new[] { previousEffect });
            Finish(null);
        }
    }

    static async UniTask LoadingGate()
    {
        var runner = new ProcessesRunner();
        var front = new LocalProcess(MainSceneStep.FrontPage, true);
        var settings = new LocalProcess(MainSceneStep.Setting, true);
        var loading = new LocalProcess(MainSceneStep.MailBox, false);
        runner.Add(front.Step, front); runner.Add(settings.Step, settings); runner.Add(loading.Step, loading);
        Require(runner.ChangeProcess(loading.Step), "Loading fixture could not enter its initial process.");
        int topCalls = 0;
        ReturnLayer.Stack(front.Step, step => runner.ChangeProcess(step));
        ReturnLayer.Stack(settings.Step, step => { topCalls++; return runner.ChangeProcess(step); });
        var top = ReturnLayer.ReturnMissionList[1];
        var firstLayer = GetLayer();
        var button = (BOButton)typeof(ReturnLayer).GetField("returnButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(firstLayer);
        Require(button != null, "The production return prefab has no button.");
        for (int attempt = 0; attempt < 4; attempt++)
        {
            button.onClick.Invoke(); report.refusedAttempts++;
            Require(topCalls == attempt + 1 && ReturnLayer.ReturnMissionList.Count == 2 && ReferenceEquals(ReturnLayer.ReturnMissionList[1], top),
                "Loading-gate refusal consumed or changed return history.");
            Require(ReferenceEquals(runner.currentProcess, loading) && loading.ends == 0 && GetLayer() == firstLayer && !firstLayer.IsClosing,
                "Refused return changed the active process or return layer.");
        }
        loading.Ready(true);
        button.onClick.Invoke(); report.successfulReturns++;
        Require(topCalls == 5 && ReturnLayer.ReturnMissionList.Count == 1 && ReferenceEquals(runner.currentProcess, settings)
            && ReferenceEquals(runner.lastProcess, loading) && loading.ends == 1 && settings.enters == 1,
            "Retry after loading did not return once to the original destination.");
        button.onClick.Invoke(); report.successfulReturns++;
        Require(ReturnLayer.ReturnMissionList.Count == 0 && ReferenceEquals(runner.currentProcess, front) && GetLayer() == null,
            "Final return did not exhaust history and remove its layer.");
        ReturnLayer.POP(); Require(topCalls == 5, "Empty POP invoked a previously consumed callback.");
        await Settle(); Require(RawLayers() == 0, "Exhausted return prefab survived its destroy frame.");
        Check("Four actual button refusals during MSceneProcess loading retained both history levels; retry and final return succeeded once each.");
    }

    static void MissingDestination()
    {
        var runner = new ProcessesRunner();
        ReturnLayer.Stack(MainSceneStep.Setting, step => runner.ChangeProcess(step));
        var target = ReturnLayer.ReturnMissionList[0];
        ReturnLayer.POP(); report.refusedAttempts++;
        Require(ReturnLayer.ReturnMissionList.Count == 1 && ReferenceEquals(ReturnLayer.ReturnMissionList[0], target) && GetLayer() != null,
            "An unavailable process destination erased return history.");
        runner.Add(MainSceneStep.Setting, new LocalProcess(MainSceneStep.Setting, true));
        ReturnLayer.POP(); report.successfulReturns++;
        Require(ReturnLayer.ReturnMissionList.Count == 0 && GetLayer() == null, "Retry to an available destination failed.");
        Check("Missing process destinations retain their action, which can be retried successfully after registration.");
    }

    static void Lifo()
    {
        var order = new List<int>();
        ReturnLayer.Stack(MainSceneStep.FrontPage, _ => { order.Add(1); return true; });
        ReturnLayer.Stack(MainSceneStep.Setting, _ => { order.Add(2); return true; });
        ReturnLayer.Stack(MainSceneStep.MailBox, _ => { order.Add(3); return true; });
        for (int remaining = 2; remaining >= 0; remaining--)
        {
            ReturnLayer.POP(); report.successfulReturns++;
            Require(ReturnLayer.ReturnMissionList.Count == remaining && (GetLayer() != null) == (remaining > 0),
                "Multi-level POP changed an unexpected number of actions or layer lifetime.");
        }
        Require(order.SequenceEqual(new[] { 3, 2, 1 }), "Return history was not LIFO.");
        Check("Three-level history returns in order 3, 2, 1 and closes exactly when exhausted.");
    }

    static void CallbackException()
    {
        var expected = new InvalidOperationException("Expected local return callback failure.");
        bool throwNow = true;
        ReturnLayer.Stack(MainSceneStep.FrontPage, _ => true);
        ReturnLayer.Stack(MainSceneStep.Setting, _ => { if (throwNow) throw expected; return true; });
        var target = ReturnLayer.ReturnMissionList[1];
        try { ReturnLayer.POP(); throw new Exception("Return callback exception did not propagate."); }
        catch (InvalidOperationException exception) { Require(ReferenceEquals(exception, expected), "POP replaced the original callback exception."); report.observedExceptions++; }
        Require(ReturnLayer.ReturnMissionList.Count == 2 && ReferenceEquals(ReturnLayer.ReturnMissionList[1], target) && GetLayer() != null,
            "Callback exception erased the attempted destination.");
        throwNow = false; ReturnLayer.POP(); report.successfulReturns++;
        Require(ReturnLayer.ReturnMissionList.Count == 1, "The exception left POP permanently locked.");
        Check("A thrown callback preserves history, propagates its original exception and releases the guard for retry.");
    }

    static void CallbackReentry(bool success)
    {
        int lowerCalls = 0, topCalls = 0;
        bool allow = success;
        ReturnLayer.Stack(MainSceneStep.FrontPage, _ => { lowerCalls++; return true; });
        ReturnLayer.Stack(MainSceneStep.Setting, _ =>
        {
            topCalls++; ReturnLayer.POP(); report.callbackReentries++;
            Require(ReturnLayer.ReturnMissionList.Count == 2 && lowerCalls == 0 && topCalls == 1,
                "Reentrant POP invoked another action during an outstanding return.");
            return allow;
        });
        ReturnLayer.POP();
        Require(lowerCalls == 0 && topCalls == 1 && ReturnLayer.ReturnMissionList.Count == (success ? 1 : 2),
            "Reentrant callback consumed an extra history level.");
        if (success) report.successfulReturns++; else report.refusedAttempts++;
        if (!success)
        {
            allow = true;
            // Replace only the test assertion's first-invocation counter before retry.
            topCalls = 0;
            ReturnLayer.POP(); report.successfulReturns++;
            Require(ReturnLayer.ReturnMissionList.Count == 1 && topCalls == 1, "Refused reentrant callback prevented a later retry.");
        }
        ReturnLayer.POP(); report.successfulReturns++;
        Require(lowerCalls == 1 && ReturnLayer.ReturnMissionList.Count == 0, "Lower action was not available after callback reentry.");
        Check((success ? "Successful" : "Refused") + " callback reentry invokes the active action once and retains the lower destination.");
    }

    static async UniTask ClearAndRebuild()
    {
        int oldCalls = 0, newCalls = 0;
        ReturnLayer.Stack(MainSceneStep.FrontPage, _ => { oldCalls++; return true; });
        ReturnAction replacement = null;
        ReturnLayer.Stack(MainSceneStep.Setting, _ =>
        {
            ReturnLayer.Clear();
            ReturnLayer.Stack(MainSceneStep.MailBox, __ => { newCalls++; return true; });
            replacement = ReturnLayer.ReturnMissionList[0];
            ReturnLayer.POP(); report.callbackReentries++;
            return true;
        });
        var oldLayer = GetLayer();
        ReturnLayer.POP(); report.successfulReturns++;
        Require(ReturnLayer.ReturnMissionList.Count == 1 && ReferenceEquals(ReturnLayer.ReturnMissionList[0], replacement) && oldCalls == 0 && newCalls == 0,
            "Successful callback removed the replacement history created after Clear.");
        Require(oldLayer.IsClosing && GetLayer() != null && GetLayer() != oldLayer && LiveLayers() == 1,
            "Clear/rebuild did not replace its old return layer once.");
        await Settle();
        Require(oldLayer == null && RawLayers() == 1, "Clear/rebuild left its old return object alive after the destroy frames.");
        ReturnLayer.POP(); report.successfulReturns++;
        Require(oldCalls == 0 && newCalls == 1 && ReturnLayer.ReturnMissionList.Count == 0,
            "The rebuilt history returned to a retired destination or lost its replacement.");
        Check("A successful callback may clear old history and register new history; POP retains the replacement action and layer.");
    }

    static void ClearAll()
    {
        ReturnLayer.Stack(MainSceneStep.FrontPage, _ => true);
        ReturnLayer.Stack(MainSceneStep.Setting, _ => { ReturnLayer.Clear(); return true; });
        ReturnLayer.POP(); report.successfulReturns++;
        Require(ReturnLayer.ReturnMissionList.Count == 0 && GetLayer() == null, "POP resurrected a history cleared by its callback.");
        Check("Successful callback Clear without replacement leaves history and return UI empty.");
    }

    static void AppendDuringReturn()
    {
        var order = new List<int>();
        ReturnLayer.Stack(MainSceneStep.FrontPage, _ => { order.Add(1); return true; });
        ReturnLayer.Stack(MainSceneStep.Setting, _ =>
        {
            order.Add(2);
            ReturnLayer.Stack(MainSceneStep.MailBox, __ => { order.Add(3); return true; });
            return true;
        });
        ReturnLayer.POP(); report.successfulReturns++;
        Require(ReturnLayer.ReturnMissionList.Count == 2 && ReturnLayer.ReturnMissionList[0].returnToStep == MainSceneStep.FrontPage
            && ReturnLayer.ReturnMissionList[1].returnToStep == MainSceneStep.MailBox, "POP consumed the newly appended action instead of the completed one.");
        ReturnLayer.POP(); ReturnLayer.POP(); report.successfulReturns += 2;
        Require(order.SequenceEqual(new[] { 2, 3, 1 }) && ReturnLayer.ReturnMissionList.Count == 0, "Appended return history ran out of order.");
        Check("An action appended during a successful callback remains above the lower history, while only the completed action is consumed.");
    }

    sealed class LocalProcess : MSceneProcess
    {
        public int enters, ends;
        public LocalProcess(MainSceneStep step, bool loaded) { Step = step; SetLoaded(loaded); }
        public void Ready(bool ready) => SetLoaded(ready);
        public override void ProcessEnter() => enters++;
        public override void ProcessEnd() => ends++;
    }

    static ReturnLayer GetLayer() => (ReturnLayer)Loader.GetMethod("Get").MakeGenericMethod(typeof(ReturnLayer)).Invoke(null, null);
    static int LiveLayers() => UnityEngine.Object.FindObjectsByType<ReturnLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(layer => !layer.IsClosing);
    static int RawLayers() => UnityEngine.Object.FindObjectsByType<ReturnLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    static async UniTask Settle() { await UniTask.NextFrame(); await UniTask.NextFrame(); }
    static async UniTask Reset() { ReturnLayer.Clear(); await Settle(); }
    static void Check(string message) { report.casesChecked++; report.checks.Add(message); }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    static void Finish(string failure)
    {
        if (finishing) return;
        finishing = true;
        if (report == null) report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        if (!string.IsNullOrEmpty(failure)) report.errors.Add(failure);
        string logged = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(logged)) report.errors.Add(logged);
        report.passed = report.errors.Count == 0 && report.casesChecked == ExpectedCases;
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Poll;
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        string summary = $"[ReturnNavigation] {(report.passed ? "PASS" : "FAIL")}: {report.casesChecked} cases, {report.refusedAttempts} refused attempts, {report.successfulReturns} successful returns. {Path.GetFullPath(Output)}";
        if (report.passed) Debug.Log(summary); else Debug.LogError(summary + "\n" + string.Join("\n", report.errors));
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) { EditorApplication.Exit(report.passed ? 0 : 1); return; }
        EditorApplication.delayCall += RestoreScenes;
    }
    static void RestoreScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += RestoreScenes; return; }
        var records = JsonUtility.FromJson<SceneRecords>(SessionState.GetString(Key + ".Scenes", "{}"));
        var setup = records.scenes.Where(scene => !string.IsNullOrEmpty(scene.path))
            .Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
    }
}
