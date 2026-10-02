using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using ModelView;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Isolated Play-mode regression for interrupted UI transitions. No account or network.</summary>
[InitializeOnLoad]
public static class PocketStrikerUIInterruptionValidation
{
    const string Key = "PocketStriker.UIInterruption";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool finishing;
    static string Output => "Logs/UIQuality/" + (Environment.GetEnvironmentVariable("POCKETSTRIKER_UI_QUALITY") ?? "latest");
    [Serializable] public sealed class Report
    {
        public bool passed;
        public string utcTime, unityVersion;
        public string scope = "Actual Resources prefabs in isolated Editor Play mode; production ProgressLayer/HighLightLayer fades interrupted by newer requests, repeated title initialization and material lifecycle. Actual solo/group preparation prefabs and real haruka meshes also verify that production UILayerLoader.Remove synchronously hides every model and camera before a fixture-owned idle clip is released. Preparation/model behaviour scripts are disabled before activation. No account, network, scene load, purchases or gameplay initialization.";
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
    static T Layer<T>() where T : UILayer => (T)Loader.GetMethod("Get").MakeGenericMethod(typeof(T)).Invoke(null, null);
    static Report report;

    [Serializable] public sealed class PreparationCloseReport
    {
        public bool passed, previewsInactiveBeforeResourceRelease;
        public int casesChecked, camerasChecked, modelsChecked;
        public string utcTime, unityVersion;
        public string scope = "Play mode only: actual solo/group preparation prefabs, their authored connector cameras, and copies of the real haruka model. Production UILayerLoader.Remove is called without a frame yield; roots, models and cameras must already be inactive before releasing the copied idle animation clip. The loader queue and hangers are restored synchronously before yielding for deferred destruction. All preparation/model behaviour scripts are disabled before activation. No account, Addressables, production resource release, scene load or network.";
        public List<string> errors = new List<string>();
    }

    // Can also be run independently in an existing Play-mode validation session
    // without starting a scene load or the rest of the batch suite.
    public static async UniTask<PreparationCloseReport> ValidatePreparationCloseInPlayMode()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Preparation close lifecycle validation requires Play mode.");
        var result = new PreparationCloseReport { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        var queues = (List<UILayer>)Loader.GetField("Queues", StaticPrivate).GetValue(null);
        var hanger = Loader.GetField("_hanger", StaticPrivate);
        var fullScreenHanger = Loader.GetField("_fullScreenHanger", StaticPrivate);
        var remove = Loader.GetMethod("Remove", Type.EmptyTypes).MakeGenericMethod(typeof(FightPrepareLayer));
        foreach (var group in new[] { false, true })
        {
            var oldQueues = queues.ToArray();
            var oldHanger = hanger.GetValue(null);
            var oldFullScreenHanger = fullScreenHanger.GetValue(null);
            var stage = new GameObject("Preparation close fixture", typeof(RectTransform));
            stage.SetActive(false);
            GameObject instance = null;
            AnimationClip ownedIdle = null;
            try
            {
                var source = Resources.Load<GameObject>(group
                    ? "DummyLayerSystem/FightPrepareLayer/FightPrepareLayer_gb"
                    : "DummyLayerSystem/FightPrepareLayer");
                Require(source != null, "Preparation prefab is unavailable.");
                instance = UnityEngine.Object.Instantiate(source, stage.transform, false);
                var layer = instance.GetComponent<FightPrepareLayer>();
                layer.Index = nameof(FightPrepareLayer);
                layer.IsClosing = false;
                var connectors = instance.GetComponentsInChildren<DedicatedCameraConnector>(true);
                Require(connectors.Length == (group ? 2 : 1), "Unexpected authored preparation connector count.");
                var cameras = connectors.Select(connector => Get<Camera>(connector, "camera")).ToArray();
                var modelSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/Unit/human/haruka.prefab");
                var idleSource = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/haruka/idle.anim");
                Require(modelSource != null && idleSource != null, "Bundled real-model/idle fixtures are unavailable.");
                ownedIdle = UnityEngine.Object.Instantiate(idleSource);
                var models = connectors.Select(connector => UnityEngine.Object.Instantiate(modelSource, connector.transform, false)).ToArray();
                // The inactive parent prevents OnEnable/Start while disabling
                // model and UI logic; only the real hierarchy lifecycle is used.
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var camera in cameras)
                {
                    Require(camera != null && camera.transform.IsChildOf(instance.transform), "An authored preview camera escapes the preparation hierarchy.");
                    camera.enabled = false;
                    for (var node = camera.transform; node != instance.transform; node = node.parent) node.gameObject.SetActive(true);
                }
                instance.SetActive(true);
                stage.SetActive(true);
                foreach (var model in models)
                    foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                        if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) ownedIdle.SampleAnimation(animator.gameObject, 0);
                Require(instance.activeInHierarchy && models.All(model => model.activeInHierarchy)
                    && cameras.All(camera => camera.gameObject.activeInHierarchy), "Preparation closure fixture did not begin with active preview hierarchies.");
                queues.Clear();
                queues.Add(layer);
                hanger.SetValue(null, stage.transform);
                fullScreenHanger.SetValue(null, stage.transform);
                remove.Invoke(null, null);
                // Destroy is deferred in Play mode, so inspect the actual
                // objects synchronously before any clip is released or yielded.
                Require(instance != null && layer != null && layer.IsClosing && !instance.activeSelf && !instance.activeInHierarchy,
                    "Remove did not synchronously hide the preparation root before destruction.");
                Require(models.All(model => model != null && !model.activeInHierarchy)
                    && cameras.All(camera => camera != null && !camera.gameObject.activeInHierarchy),
                    "Remove left a preview model or camera active before resource release.");
                Require(ownedIdle != null && queues.Count == 0, "Fixture released animation early or Remove left its queue entry behind.");
                result.previewsInactiveBeforeResourceRelease = true;
                result.camerasChecked += cameras.Length;
                result.modelsChecked += models.Length;
                UnityEngine.Object.Destroy(ownedIdle);
            }
            catch (Exception exception) { result.errors.Add((group ? "group" : "solo") + ": " + exception.GetBaseException()); }
            finally
            {
                // Restore the production loader before any await, preserving an
                // existing live scene while its fixture is destroyed separately.
                queues.Clear();
                queues.AddRange(oldQueues.Where(layer => layer != null));
                hanger.SetValue(null, oldHanger);
                fullScreenHanger.SetValue(null, oldFullScreenHanger);
                if (instance != null) UnityEngine.Object.Destroy(instance);
                if (ownedIdle != null) UnityEngine.Object.Destroy(ownedIdle);
                UnityEngine.Object.Destroy(stage);
            }
            await UniTask.DelayFrame(1);
            if (instance != null || ownedIdle != null || stage != null)
                result.errors.Add((group ? "group" : "solo") + ": deferred preparation/animation fixture destruction did not complete.");
            else result.casesChecked++;
        }
        result.passed = result.errors.Count == 0 && result.casesChecked == 2 && result.camerasChecked == 3
            && result.modelsChecked == 3 && result.previewsInactiveBeforeResourceRelease;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "preparation-close-report.json"), JsonUtility.ToJson(result, true));
        return result;
    }
    static PocketStrikerUIInterruptionValidation()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Poll;
    }
    public static void StartBatch()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run in a separate, stopped batch editor.");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O"));
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        typeof(Editor).Assembly.GetType("UnityEditor.GameView", true).GetMethod("SetCustomResolution", Private)
            .Invoke(EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")),
                new object[] { new Vector2(540, 960), "PocketStriker UI interruptions" });
        EditorApplication.isPlaying = true;
    }
    static void Poll()
    {
        if (finishing || !SessionState.GetBool(Key, false)) return;
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Started", DateTime.UtcNow.ToString("O")))).TotalSeconds > 90)
        { report ??= new Report(); report.errors.Add("UI interruption deadline exceeded."); Finish(); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        SessionState.SetBool(Key + ".Running", true); Run().Forget();
    }
    static void CaptureError(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.errors.Add(text + "\n" + stack);
    }
    static async UniTask Run()
    {
        report = new Report { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        Directory.CreateDirectory(Output);
        Application.logMessageReceived += CaptureError;
        var root = new GameObject("UI interruption fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        // An active camera clears the previous frame even when every UI layer
        // has been removed; otherwise an empty fixture can leave a stale image.
        var cameraObject = new GameObject("UI fixture camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.10f, .15f, .19f, 1);
        PosCal.Canvas = canvas; PosCal.SafeAreaRect = (RectTransform)root.transform;
        Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new object[] { root.transform, root.transform });
        try
        {
            await Check("loading-fade-replaced-by-loading", async () =>
            {
                ProgressLayer.Loading("Previous operation"); await Pause(600);
                ProgressLayer.LightUp(.2f); await Pause(50);
                ProgressLayer.Loading("New operation still running"); await Pause(650);
                await Screenshot("new-loading-survives");
                var layer = Layer<ProgressLayer>();
                Require(layer != null && Get<Image>(layer, "bigCurtain").raycastTarget,
                    "An old fade closed the newer loading operation and released input.");
            });
            await Check("loading-fade-replaced-by-download", async () =>
            {
                ProgressLayer.Loading("Previous operation"); await Pause(600);
                ProgressLayer.LightUp(.2f); await Pause(50);
                ProgressLayer.Downloading("Downloading current resources");
                ProgressLayer.LoadingPercent("Downloading current resources", .6f, false); await Pause(650);
                var layer = Layer<ProgressLayer>();
                Require(layer != null && Get<Image>(layer, "bigCurtain").raycastTarget
                    && Mathf.Abs(Get<Slider>(layer, "progressBar").value - .6f) < .01f,
                    "A previous loading fade removed or reset the active download.");
                ProgressLayer.LightUp(.1f); await Pause(250);
                Require(Layer<ProgressLayer>() == null, "Completed download did not close normally.");
            });
            await Check("highlight-fade-replaced-by-darkening", async () =>
            {
                HighLightLayer.DarkOff(Color.black, .05f); await Pause(100);
                HighLightLayer.LightUp(.2f); await Pause(50);
                HighLightLayer.DarkOff(new Color(0, 0, 0, .75f), .1f); await Pause(400);
                var layer = Layer<HighLightLayer>();
                Require(layer != null && Get<Image>(layer, "bigCurtain").raycastTarget,
                    "An old highlight fade closed the newer interaction mask.");
            });
            await Check("highlight-pending-target-replaced-by-darkening", async () =>
            {
                HighLightLayer.HighLightRect((RectTransform)root.transform);
                HighLightLayer.DarkOff(Color.black, .1f); await Pause(250);
                var layer = Layer<HighLightLayer>();
                Require(layer != null && Get<Image>(layer, "bigCurtain").raycastTarget,
                    "A delayed old target request disabled the newer full-screen input mask.");
            });
            await Check("title-material-and-reinitialization", async () =>
            {
                var source = Resources.Load<TitleScreenLayer>("DummyLayerSystem/TitleScreenLayer");
                var shared = Get<Image>(source, "title").material;
                float initial = shared.GetFloat("_Animation_Factor");
                var a = (TitleScreenLayer)Loader.GetMethod("Load").MakeGenericMethod(typeof(TitleScreenLayer)).Invoke(null, new object[] { false, null, false }); a.Initialise();
                var image = Get<Image>(a, "title"); var instance = image.material;
                await Pause(250);
                bool isolated = instance != shared && Mathf.Approximately(shared.GetFloat("_Animation_Factor"), initial);
                a.Initialise();
                Require(image.material == instance, "Repeated initialization allocated another title material.");
                Loader.GetMethod("Remove", Type.EmptyTypes).MakeGenericMethod(typeof(TitleScreenLayer)).Invoke(null, null); await Pause(100);
                bool disposed = instance == null;
                // Restore baseline shared shader value in memory; never save the material fixture.
                shared.SetFloat("_Animation_Factor", initial);
                Require(isolated, "Title animation writes into the shared project material.");
                Require(disposed, "The owned title material survived title destruction.");
            });
            await Check("preparation-previews-hidden-before-resource-release", async () =>
            {
                var closed = await ValidatePreparationCloseInPlayMode();
                Require(closed.passed, string.Join("\n", closed.errors));
            });
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            Loader.GetMethod("Clear").Invoke(null, new object[] { null }); await Pause(150);
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(cameraObject);
            Application.logMessageReceived -= CaptureError;
            Finish();
        }
    }
    static async UniTask Check(string name, Func<UniTask> action)
    {
        try { await action(); report.checks.Add(name); }
        catch (Exception e) { report.errors.Add(name + ": " + e.Message); }
        Loader.GetMethod("Clear").Invoke(null, new object[] { null }); await Pause(150);
    }
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static UniTask Pause(int milliseconds) => UniTask.Delay(milliseconds, DelayType.Realtime);
    static async UniTask Screenshot(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Output, name + ".png")));
        await Pause(100);
    }
    static void Finish()
    {
        if (finishing) return; finishing = true;
        report.passed = report.errors.Count == 0 && report.checks.Count == 6;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        SessionState.SetBool(Key, false); EditorApplication.update -= Poll;
        Debug.Log("[UIInterruption] " + (report.passed ? "PASS" : "FAIL") + ": " + report.checks.Count + "/6");
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
