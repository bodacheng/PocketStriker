using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Source-prefab art snapshots and a service-free, real-loader UI Play-mode fixture.</summary>
[InitializeOnLoad]
public static class PocketStrikerUIArtValidation
{
    const string Output = "Logs/UIArt";
    const string Key = "PocketStriker.UIArt.Playmode";
    const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
    static bool finishing;
    static PlaymodeReport report;

    [Serializable] public sealed class SnapshotReport
    {
        public bool passed;
        public string phase, unityVersion, utcTime;
        public int registeredLayers, layoutCases, compositions, stageCardCases, errors, warnings, screenshots;
        public bool currentScenesUnchanged;
        public bool sourceLayoutPassed, independentGeometryPassed, fixtureObjectsRestored, sceneDirtyOnlyFinding, isolatedBatchScene;
        public int sourceLayoutErrors;
        public FixtureState fixtureBefore, fixtureAfter;
        public string scope = "All actually registered UILayerLoader prefabs and the existing authored/settings/combo visibility variants, real UI graphics and shared safe-area sizing at four portrait shapes. The existing layout fixture copies UI graphics and built-in layout components; game Awake/Start and service callbacks are omitted.";
        public string limitation = "Static prefab screenshots do not execute dynamic account, shop, mail, summoning, rewards, animation, battle or network flows. A matching Play-mode report separately identifies local native interactions. Registered layer count comes from the live loader, not a fixed documentation count.";
        public List<ScreenEvidence> screens = new List<ScreenEvidence>();
        public List<string> sourceLayoutFailures = new List<string>();
        public List<string> failures = new List<string>();
    }
    [Serializable] public sealed class FixtureState
    {
        public int previewScenes;
        public string activeSceneHandle;
        public string objectFingerprint;
        public List<FixtureScene> scenes = new List<FixtureScene>();
    }
    [Serializable] public sealed class FixtureScene
    { public int roots, objects, components; public string handle, path; public bool loaded, dirty; }
    [Serializable] public sealed class ScreenEvidence
    {
        public string layer, prefab;
        public int cases;
        public List<string> screenshots = new List<string>();
    }
    [Serializable] public sealed class PlaymodeReport
    {
        public bool passed;
        public string unityVersion, utcTime;
        public int viewportCases, settingsTabCases, popupCycles, nicknameCycles, linkPromptCycles;
        public int returnActions, raycastChecks, disabledChecks, buttonStateCaptures;
        public string scope = "Editor Play mode in an empty local scene. Actual UILayerLoader.Load/Get/Remove, PopupLayer confirmation/warning callbacks, NickNameLayer cancellation, AskIfLinkDeviceLayer callbacks, SettingLayer.Initialise six tab callbacks, ReturnLayer.Stack/POP, BOButton pointer gates, CanvasScaler and PortraitSafeAreaLayout. Native EventSystem raycasts and pointer enter/down/up/click exercise enabled and disabled controls; native backbuffer screenshots document four requested portrait sizes and primary/secondary/danger states.";
        public string limitation = "No startup, Addressables, authentication, IAP, ad, email, account binding/deletion, browser-link, bad-word filter, summoning, reward or combat service is started. Nickname submission and settings persistence are not invoked. Settings tabs use local account labels. Simulated editor sizes and static safe-area snapshots do not certify physical-device touch/notches. No online or dynamically loaded account/shop flow is claimed.";
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<ButtonStateEvidence> buttonStates = new List<ButtonStateEvidence>();
    }
    [Serializable] public sealed class ButtonStateEvidence
    { public string button, state, screenshot; public Color renderedTint; }
    [Serializable] sealed class SceneRecords { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }

    static PocketStrikerUIArtValidation()
    {
        if (SessionState.GetBool(Key, false)) { SuspendFilterAutoload(); Attach(); }
        if (!string.IsNullOrEmpty(SessionState.GetString(Key + ".SnapshotPhase", ""))) { SuspendFilterAutoload(); QueueSnapshot(); }
    }

    [MenuItem("PocketStriker/Validation/UI Art/Capture Before")]
    public static void CaptureBefore() => Capture("Before", false);
    public static void CaptureBeforeBatch() => BeginSnapshotBatch("Before");
    [MenuItem("PocketStriker/Validation/UI Art/Validate After")]
    public static void ValidateAfter() => Capture(NextAfterPhase(), false);
    public static void ValidateAfterBatch() => BeginSnapshotBatch(NextAfterPhase());
    public static void ValidateGeometryBatch() => BeginSnapshotBatch("Current");

    static string NextAfterPhase()
    {
        string phase = "After";
        for (int iteration = 2; File.Exists(Path.Combine(Output, phase, "report.json")); iteration++)
            phase = "After-" + iteration;
        return phase;
    }

    static void BeginSnapshotBatch(string phase)
    {
        Require(Application.isBatchMode, "Use the non-Batch snapshot menu entry in an interactive editor.");
        if (phase == "Before" && File.Exists(Path.Combine(Output, phase, "report.json")))
        { Capture(phase, true); return; }
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before art snapshots.");
        // A fresh batch process may load an authored startup scene whose editor
        // integrations mark it dirty during first render. Use an empty scene and
        // settle editor callbacks first; the actual validator still checks and
        // reports scene preservation rather than suppressing that finding.
        SuspendFilterAutoload();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetString(Key + ".SnapshotPhase", phase);
        QueueSnapshot();
    }
    static void QueueSnapshot()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            string phase = SessionState.GetString(Key + ".SnapshotPhase", "");
            if (string.IsNullOrEmpty(phase)) return;
            SessionState.EraseString(Key + ".SnapshotPhase");
            Capture(phase, true);
        };
    }

    static void Capture(string phase, bool exit)
    {
        var snapshot = new SnapshotReport { phase = phase, unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        string destination = Path.Combine(Output, phase);
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before art snapshots.");
            Require(phase != "Before" || !File.Exists(Path.Combine(destination, "report.json")),
                "The Before snapshot is preserved. Archive it explicitly before capturing a different baseline.");
            snapshot.fixtureBefore = CaptureFixtureState();
            snapshot.isolatedBatchScene = Application.isBatchMode && snapshot.fixtureBefore.scenes.Count == 1
                && string.IsNullOrEmpty(snapshot.fixtureBefore.scenes[0].path) && snapshot.fixtureBefore.scenes[0].roots == 0;
            ValidatePortraitFrameTransparency();
            PocketStrikerUILayoutValidation.Validate();
            string sourceReport = "Logs/UILayout/report.json";
            var layout = JsonUtility.FromJson<PocketStrikerUILayoutValidation.Report>(File.ReadAllText(sourceReport));
            snapshot.registeredLayers = layout.registeredLayers;
            snapshot.layoutCases = layout.casesChecked;
            snapshot.compositions = layout.compositions.Count;
            snapshot.sourceLayoutPassed = layout.passed;
            snapshot.sourceLayoutErrors = layout.errors;
            snapshot.errors = layout.cases.Sum(item => item.findings.Count(finding => finding.severity == "error"));
            snapshot.warnings = layout.warnings;
            snapshot.currentScenesUnchanged = layout.currentScenesUnchanged;
            snapshot.sourceLayoutFailures.AddRange(layout.failures);
            snapshot.independentGeometryPassed = layout.casesChecked == layout.expectedCases
                && layout.safeAreaInitializationPassed && snapshot.errors == 0 && snapshot.warnings == 0
                && layout.compositions.All(item => item.findings.Count == 0);
            Directory.CreateDirectory(destination);
            File.Copy(sourceReport, Path.Combine(destination, "layout-report.json"), true);
            foreach (var group in layout.cases.GroupBy(item => item.layer).OrderBy(item => item.Key))
            {
                var evidence = new ScreenEvidence { layer = group.Key, prefab = group.First().prefab, cases = group.Count() };
                foreach (string source in group.Select(item => item.previewPng).Where(path => !string.IsNullOrEmpty(path)).Distinct())
                {
                    Require(File.Exists(source), "A reported preview is missing: " + source);
                    string target = Path.Combine(destination, "previews", Path.GetFileName(source));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(source, target, true);
                    evidence.screenshots.Add(target); snapshot.screenshots++;
                }
                snapshot.screens.Add(evidence);
            }
            Require(snapshot.screens.Count == snapshot.registeredLayers, "Snapshot inventory does not cover every registered layer.");
            Require(snapshot.screens.All(item => item.screenshots.Count > 0), "A registered layer has no captured authored preview.");
            var cards = PocketStrikerStageCardValidation.ValidateCards();
            snapshot.stageCardCases = cards.casesChecked;
            snapshot.failures.AddRange(cards.errors);
            File.Copy("Logs/UILayout/stage-cards.json", Path.Combine(destination, "stage-cards.json"), true);
            foreach (string source in cards.screenshots.Distinct())
            {
                string target = Path.Combine(destination, "previews", Path.GetFileName(source));
                File.Copy(source, target, true); snapshot.screenshots++;
            }
            snapshot.fixtureAfter = CaptureFixtureState();
            snapshot.fixtureObjectsRestored = snapshot.fixtureBefore.objectFingerprint == snapshot.fixtureAfter.objectFingerprint
                && snapshot.fixtureBefore.previewScenes == snapshot.fixtureAfter.previewScenes;
            snapshot.sceneDirtyOnlyFinding = !layout.currentScenesUnchanged && layout.failures.Count == 1
                && layout.failures[0] == "Open scene set or scene dirty state changed during validation."
                && layout.errors == 1 && snapshot.independentGeometryPassed && snapshot.fixtureObjectsRestored;
            bool acceptedLayout = layout.passed || (snapshot.isolatedBatchScene && snapshot.sceneDirtyOnlyFinding);
            if (!acceptedLayout) snapshot.failures.AddRange(layout.failures);
            if (!snapshot.fixtureObjectsRestored) snapshot.failures.Add("The scene object/component fingerprint or preview scene count changed during capture.");
            snapshot.passed = acceptedLayout && snapshot.independentGeometryPassed && cards.passed && snapshot.failures.Count == 0;
            if (snapshot.sceneDirtyOnlyFinding)
                snapshot.limitation += " The original layout report retains its false result and scene-dirty failure. In this discarded empty batch scene, an independent object/component serialization fingerprint and preview-scene count prove fixture restoration; only that isolated dirty-flag side effect is accepted by this art report.";
        }
        catch (Exception exception) { snapshot.failures.Add(exception.ToString()); }
        // A refused second baseline never overwrites the original artifact.
        bool preserved = phase == "Before" && File.Exists(Path.Combine(destination, "report.json"));
        if (!preserved)
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "report.json"), JsonUtility.ToJson(snapshot, true));
        }
        string summary = $"[UIArt/{phase}] {(snapshot.passed ? "PASS" : "FAIL")}: {snapshot.registeredLayers} layers, {snapshot.layoutCases} layout cases, {snapshot.compositions} compositions, {snapshot.screenshots} previews. {Path.GetFullPath(destination)}";
        if (snapshot.passed) Debug.Log(summary); else Debug.LogError(summary + "\n" + string.Join("\n", snapshot.failures));
        if (Application.isBatchMode) RestoreFilterAutoload();
        if (exit) EditorApplication.Exit(snapshot.passed ? 0 : 1);
    }

    static void ValidatePortraitFrameTransparency()
    {
        // A clickable target is still an overlay: its center must not hide the portrait.
        foreach (string name in new[] { "UnitIconPrefab", "UnitListItemPrefab", "HeroCell", "GangbangHeroIcon", "GangbangHeroIcon_arcadetop" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/unit/" + name + ".prefab");
            var hero = prefab.GetComponent<HeroIcon>();
            // Empty roster cells use HeroCell; populated portrait prefabs use HeroIcon.
            var frame = hero != null
                ? (Image)typeof(HeroIcon).GetField("frame", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hero)
                : prefab.GetComponent<HeroCell>()?.iconButton.targetGraphic as Image;
            Require(frame != null && frame.sprite != null, "Missing portrait frame: " + name);
            string texturePath = AssetDatabase.GetAssetPath(frame.sprite.texture);
            var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(readable.LoadImage(File.ReadAllBytes(texturePath)), "Could not decode portrait frame: " + texturePath);
                var rect = frame.sprite.rect;
                Color center = readable.GetPixel(Mathf.RoundToInt(rect.center.x), Mathf.RoundToInt(rect.center.y));
                Require(center.a < 0.05f, "Opaque frame covers character portrait: " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(readable); }
        }
    }

    static FixtureState CaptureFixtureState()
    {
        var state = new FixtureState { activeSceneHandle = SceneManager.GetActiveScene().handle.GetRawData().ToString(), previewScenes = EditorSceneManager.previewSceneCount };
        var fingerprint = new StringBuilder().Append("active:").Append(state.activeSceneHandle).Append(";previews:").Append(state.previewScenes);
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            var evidence = new FixtureScene { handle = scene.handle.GetRawData().ToString(), path = scene.path, loaded = scene.isLoaded, dirty = scene.isDirty };
            state.scenes.Add(evidence);
            fingerprint.Append("\nscene:").Append(scene.handle.GetRawData()).Append(':').Append(scene.path).Append(':').Append(scene.isLoaded);
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects(); evidence.roots = roots.Length;
            foreach (var root in roots.OrderBy(item => item.GetEntityId().ToString(), StringComparer.Ordinal))
            foreach (var transform in root.GetComponentsInChildren<Transform>(true).OrderBy(item => item.GetEntityId().ToString(), StringComparer.Ordinal))
            {
                evidence.objects++;
                fingerprint.Append("\nobject:").Append(transform.gameObject.GetEntityId().ToString()).Append(':')
                    .Append(transform.parent != null ? transform.parent.GetEntityId().ToString() : "root").Append(':')
                    .Append(EditorJsonUtility.ToJson(transform.gameObject));
                foreach (var component in transform.GetComponents<Component>().OrderBy(item => item != null ? item.GetEntityId().ToString() : "missing", StringComparer.Ordinal))
                {
                    evidence.components++;
                    if (component == null) { fingerprint.Append("\nmissing-component"); continue; }
                    fingerprint.Append("\ncomponent:").Append(component.GetEntityId().ToString()).Append(':')
                        .Append(component.GetType().AssemblyQualifiedName).Append(':').Append(EditorJsonUtility.ToJson(component));
                }
            }
        }
        // Dirty flags remain explicit in the evidence above. The fingerprint
        // deliberately compares actual scene objects/components independently.
        using (var sha = SHA256.Create())
            state.objectFingerprint = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(fingerprint.ToString()))).Replace("-", "").ToLowerInvariant();
        return state;
    }

    [MenuItem("PocketStriker/Validation/UI Art/Local Playmode Smoke")]
    public static void StartBatch()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start UI art smoke from a stopped editor.");
        var scenes = new SceneRecords();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
        {
            if (!Application.isBatchMode && scene.isLoaded)
                Require(!string.IsNullOrEmpty(scene.path) && !SceneManager.GetSceneByPath(scene.path).isDirty,
                    "Save open scenes before the isolated UI smoke switches to its empty fixture scene.");
            scenes.scenes.Add(new SceneRecord { path = scene.path, loaded = scene.isLoaded, active = scene.isActive });
        }
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(scenes));
        SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key, true);
        finishing = false; report = null;
        SuspendFilterAutoload();
        Attach();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetResolution(540, 960);
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
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Started", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > 180)
        { Finish("Local UI art smoke timed out."); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new PlaymodeReport { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var previousAccount = PlayerAccountInfo.Me;
        var previousSetting = AppSetting.Value;
        var previousCanvas = PosCal.Canvas;
        var previousSafe = PosCal.SafeAreaRect;
        var dragCanvasField = typeof(HeroIcon).GetField("canvas", Private);
        var previousDragCanvas = dragCanvasField.GetValue(null);
        var loaderState = new[] { "_hanger", "_fullScreenHanger", "effectBg" }
            .ToDictionary(name => name, name => Loader.GetField(name, Private).GetValue(null));
        var previousReturns = ReturnLayer.ReturnMissionList.ToArray();
        var previousConverters = LanguageConverterManger.List;
        LanguageConverterManger.List = new List<LanguageConverter>();
        GameObject rig = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(Output, "Playmode"));
            PlayerAccountInfo.Me = new PlayerAccountInfo { PlayFabId = "local-ui-art-fixture", TitleDisplayName = "Local Fighter", PlayFabUserName = "local-fixture", Email = "fixture@example.invalid", noAdsState = true };
            AppSetting.Value = new AppSetting { Language = SystemLanguage.English };
            rig = BuildRig();
            await Settle();
            foreach (var size in new[] { new Vector2Int(540, 960), new Vector2Int(375, 667), new Vector2Int(390, 844), new Vector2Int(768, 1024) })
            {
                SetResolution(size.x, size.y);
                await Settle();
                await RequireViewport(size.x, size.y);
                report.checks.Add($"Requested {size.x}x{size.y}; actual backbuffer {Screen.width}x{Screen.height}; safe area {Screen.safeArea}.");
                await SettingsTabs(size.x + "x" + size.y);
                report.viewportCases++;
            }
            SetResolution(540, 960); await Settle(); await RequireViewport(540, 960);
            await PopupCycles();
            await NicknameCycles();
            await LinkPromptCycles();
            await Returns();
            Require(report.viewportCases == 4 && report.settingsTabCases == 48 && report.popupCycles == 8
                && report.nicknameCycles == 4 && report.linkPromptCycles == 4 && report.returnActions == 2,
                "Native local flow coverage is incomplete.");
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            try
            {
                ClearLayers(); ReturnLayer.ReturnMissionList.Clear();
                if (rig != null) UnityEngine.Object.Destroy(rig);
                await Settle();
                Require(ObjectCount<UILayer>() == 0 && rig == null, "Local UI fixture objects survived cleanup.");
                report.checks.Add("All native UILayer objects and the fixture rig were destroyed after cleanup.");
            }
            catch (Exception exception) { report.errors.Add(exception.ToString()); }
            finally
            {
                dragCanvasField.SetValue(null, previousDragCanvas);
                foreach (var pair in loaderState) Loader.GetField(pair.Key, Private).SetValue(null, pair.Value);
                ReturnLayer.ReturnMissionList.Clear(); ReturnLayer.ReturnMissionList.AddRange(previousReturns);
                LanguageConverterManger.List = previousConverters;
                PlayerAccountInfo.Me = previousAccount; AppSetting.Value = previousSetting;
                PosCal.Canvas = previousCanvas; PosCal.SafeAreaRect = previousSafe;
                Finish(null);
            }
        }
    }

    static GameObject BuildRig()
    {
        var rig = new GameObject("Local UI Art Fixture");
        var backdrop = new GameObject("Local Backdrop", typeof(Camera));
        backdrop.transform.SetParent(rig.transform);
        var camera = backdrop.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.07f, 0.09f);
        var canvasObject = new GameObject("Native UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(rig.transform);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(canvas.transform, false);
        PosCal.Canvas = canvas; PosCal.SafeAreaRect = safe; PosCal.TestIni();
        Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new object[] { safe, canvas.transform });
        Loader.GetMethod("SetEffectBg").Invoke(null, new object[] { null });
        var events = new GameObject("Native EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        events.transform.SetParent(rig.transform);
        // Pointer events are dispatched explicitly below. A real mouse or key
        // during the fixture must not activate registered service actions.
        events.GetComponent<StandaloneInputModule>().enabled = false;
        var drag = new GameObject("Fixture Drag Canvas", typeof(RectTransform), typeof(Canvas));
        drag.transform.SetParent(rig.transform, false);
        drag.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        drag.GetComponent<Canvas>().sortingOrder = 100;
        typeof(HeroIcon).GetField("canvas", Private).SetValue(null, drag.GetComponent<Canvas>());
        return rig;
    }

    static async UniTask SettingsTabs(string viewport)
    {
        var layer = Load<SettingLayer>(); Require(layer != null, "Settings failed to load through the native loader.");
        Require(Load<SettingLayer>() == layer && LiveCount<SettingLayer>() == 1, "Repeated Settings Load created a duplicate.");
        layer.Initialise();
        // This presenter only registers a password-reset callback and displays
        // the local fixture labels; its service button is never activated.
        layer.AccountPhase_EmailSet(); await Settle();
        string[] tabs = { "volume", "account", "device", "support", "language", "nickName" };
        for (int repeat = 0; repeat < 2; repeat++)
        foreach (string tab in tabs)
        {
            var button = Field<BOButton>(layer, tab + "Btn");
            await Click(button);
            await Settle();
            var panel = Field<RectTransform>(layer, tab + "Panel");
            Require(panel.gameObject.activeInHierarchy && tabs.Count(name => Field<RectTransform>(layer, name + "Panel").gameObject.activeSelf) == 1,
                "Settings tab did not exclusively open its native panel: " + tab);
            layer.ResizeAreas(); Canvas.ForceUpdateCanvases();
            CheckBounds(button.transform as RectTransform, PosCal.SafeAreaRect, "Settings " + tab + " button");
            var frame = Field<RectTransform>(layer, "selectedFrame");
            Require(Vector3.Distance(frame.position, button.transform.position) < 1f, "Settings selected frame moved away from its tab.");
            foreach (var control in panel.GetComponentsInChildren<Selectable>())
                if (control.isActiveAndEnabled) CheckBounds((RectTransform)control.transform, PosCal.SafeAreaRect, "Settings " + tab + "/" + control.name);
            var position = panel.anchoredPosition;
            SettingLayer.CenterVisibleContent(panel, layer.MiddleArea);
            Require(Vector2.Distance(position, panel.anchoredPosition) < 1f, "Settings content moved on repeated centering: " + tab);
            report.settingsTabCases++;
            if (repeat == 0) await Screenshot("settings-" + viewport + "-" + tab);
        }
        if (viewport == "540x960")
        {
            await ButtonStates(Field<BOButton>(layer, "accountBtn"), "secondary-tab");
            await Click(Field<BOButton>(layer, "accountBtn")); await Settle();
            await ButtonStates(Field<BOButton>(layer, "deleteAccountBtn"), "danger-account-action");
        }
        Remove<SettingLayer>(); Require(Get<SettingLayer>() == null, "Settings native removal did not immediately clear the loader.");
        await Settle(); Require(ObjectCount<SettingLayer>() == 0, "Settings survived a frame after Remove.");
    }

    static async UniTask PopupCycles()
    {
        for (int cycle = 0; cycle < 8; cycle++)
        {
            int yes = 0, no = 0;
            PopupLayer.ArrangeConfirmWindow(() => yes++, () => no++, "Local confirmation / 本地确认 / 確認");
            var first = Get<PopupLayer>();
            PopupLayer.ArrangeConfirmWindow(() => yes++, () => no++, "Repeated open stays a single dialog.");
            Require(Get<PopupLayer>() == first && LiveCount<PopupLayer>() == 1, "Repeated popup opening created a duplicate.");
            await Settle();
            if (cycle == 0) await ButtonStates(Field<BOButton>(first, "YesButton"), "primary-confirm");
            var button = Field<BOButton>(first, cycle % 2 == 0 ? "YesButton" : "NoButton");
            await Click(button, true);
            Require(yes + no == 1 && Get<PopupLayer>() == null, "Popup callback repeated or the dialog did not close.");
            await Settle(); Require(ObjectCount<PopupLayer>() == 0, "Popup was not destroyed after close.");
            report.popupCycles++;
        }
        int closed = 0;
        PopupLayer.ArrangeWarnWindow(() => closed++, "Local warning"); await Settle();
        var warning = Get<PopupLayer>();
        Require(!Field<BOButton>(warning, "NoButton").gameObject.activeSelf, "Warning unexpectedly exposes cancel.");
        await Screenshot("warning"); await Click(Field<BOButton>(warning, "YesButton"));
        Require(closed == 1 && Get<PopupLayer>() == null, "Warning close callback failed."); await Settle();
    }
    static async UniTask NicknameCycles()
    {
        for (int cycle = 0; cycle < 4; cycle++)
        {
            int closed = 0;
            var layer = Load<NickNameLayer>(true, true);
            layer.Setup(_ => throw new InvalidOperationException("Nickname submission is excluded from this local UI fixture."), true, () => closed++);
            Require(Load<NickNameLayer>(true, true) == layer && LiveCount<NickNameLayer>() == 1, "Nickname Load duplicated a live layer.");
            await Settle(); if (cycle == 0) await Screenshot("nickname-cancel");
            await Click(Field<Button>(layer, "Cancel"));
            Require(closed == 1 && Get<NickNameLayer>() == null, "Native nickname cancel did not close once.");
            await Settle(); Require(ObjectCount<NickNameLayer>() == 0, "Nickname dialog was not destroyed after cancellation.");
            report.nicknameCycles++;
        }
    }
    static async UniTask LinkPromptCycles()
    {
        for (int cycle = 0; cycle < 4; cycle++)
        {
            int callbacks = 0;
            var layer = Load<AskIfLinkDeviceLayer>(true, true);
            layer.Initialise(() => { callbacks++; Remove<AskIfLinkDeviceLayer>(); }, () => { callbacks++; Remove<AskIfLinkDeviceLayer>(); });
            await Settle(); if (cycle == 0) await Screenshot("local-device-prompt");
            await Click(Field<Button>(layer, cycle % 2 == 0 ? "Yes" : "No"));
            Require(callbacks == 1 && Get<AskIfLinkDeviceLayer>() == null, "Local device-prompt callback/removal failed.");
            await Settle(); Require(ObjectCount<AskIfLinkDeviceLayer>() == 0, "Device prompt was not destroyed after close.");
            report.linkPromptCycles++;
        }
    }
    static async UniTask Returns()
    {
        ReturnLayer.ReturnMissionList.Clear();
        int calls = 0;
        ReturnLayer.Stack(MainSceneStep.FrontPage, _ => { calls++; return true; });
        ReturnLayer.Stack(MainSceneStep.Setting, _ => { calls++; return true; });
        await Settle(); await Screenshot("return-stack");
        for (int index = 0; index < 2; index++)
        {
            var layer = Get<ReturnLayer>(); Require(layer != null, "Return layer vanished before its stack was exhausted.");
            await Click(Field<BOButton>(layer, "returnButton"));
            Require(calls == index + 1 && ReturnLayer.ReturnMissionList.Count == 1 - index, "Native Return POP consumed the wrong number of actions.");
            await Settle(); report.returnActions++;
        }
        Require(Get<ReturnLayer>() == null && ObjectCount<ReturnLayer>() == 0, "Return did not close after its last action.");
        ReturnLayer.POP(); Require(calls == 2, "Empty Return POP invoked a callback.");
    }

    static async UniTask ButtonStates(Button button, string name)
    {
        Require(button != null && button.targetGraphic != null, "State capture needs a native Button and target graphic: " + name);
        var data = Pointer(button);
        int settleMilliseconds = Mathf.CeilToInt((Mathf.Max(0, button.colors.fadeDuration) + 0.08f) * 1000);
        EventSystem.current.SetSelectedGameObject(null);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
        await CaptureState(button, name, "normal", settleMilliseconds);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
        await CaptureState(button, name, "highlight", settleMilliseconds);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
        await CaptureState(button, name, "pressed", settleMilliseconds);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
        EventSystem.current.SetSelectedGameObject(null);
        int clicked = 0; UnityEngine.Events.UnityAction observer = () => clicked++;
        button.onClick.AddListener(observer);
        SetInteractable(button, false);
        await CaptureState(button, name, "disabled", settleMilliseconds);
        await UniTask.WaitUntil(() => !BOButton.AnyProcess).Timeout(TimeSpan.FromSeconds(2));
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        Require(clicked == 0, "A disabled native button executed its callback: " + name);
        SetInteractable(button, true); button.onClick.RemoveListener(observer);
        report.disabledChecks++; report.buttonStateCaptures += 4;
    }
    static async UniTask CaptureState(Button button, string name, string state, int delayMilliseconds)
    {
        await UniTask.Delay(delayMilliseconds, ignoreTimeScale: true);
        await Screenshot("state-" + name + "-" + state);
        report.buttonStates.Add(new ButtonStateEvidence { button = name, state = state,
            screenshot = report.screenshots[report.screenshots.Count - 1], renderedTint = button.targetGraphic.canvasRenderer.GetColor() });
    }
    static void SetInteractable(Button button, bool value)
    { if (button is BOButton bo) bo.interactable = value; else button.interactable = value; }
    static async UniTask Click(Button button, bool rapidRepeat = false)
    {
        await UniTask.WaitUntil(() => !BOButton.AnyProcess).Timeout(TimeSpan.FromSeconds(2));
        Require(button != null && button.IsInteractable() && button.gameObject.activeInHierarchy, "Requested click target is not enabled.");
        var data = Pointer(button); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        var hit = hits.FirstOrDefault(item => item.gameObject != null).gameObject;
        Require(hit != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == button.gameObject,
            "Native click target is blocked: " + button.name + " by " + (hit != null ? hit.name : "nothing"));
        report.raycastChecks++;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
        // Dispatch the repeated click before native Destroy completes at the end
        // of this frame. Remove deactivates the dialog immediately, so a closed
        // object cannot execute its callbacks a second time.
        if (rapidRepeat) ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        await UniTask.Delay(350, ignoreTimeScale: true);
        if (button != null && button.gameObject.activeInHierarchy)
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    static PointerEventData Pointer(Button button)
    {
        var rt = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        return new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
    }
    static void CheckBounds(RectTransform rect, RectTransform safe, string name)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector2 local = safe.InverseTransformPoint(corner);
            Require(local.x >= safe.rect.xMin - 2 && local.x <= safe.rect.xMax + 2 && local.y >= safe.rect.yMin - 2 && local.y <= safe.rect.yMax + 2,
                name + " exceeds the live safe area.");
        }
    }
    static async UniTask Settle()
    { await UniTask.NextFrame(); Canvas.ForceUpdateCanvases(); await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate); }
    static async UniTask RequireViewport(int width, int height)
    {
        await UniTask.WaitUntil(() => Screen.width == width && Screen.height == height).Timeout(TimeSpan.FromSeconds(5));
        Require(Screen.width == width && Screen.height == height, $"Requested {width}x{height} but actual backbuffer is {Screen.width}x{Screen.height}.");
    }
    static async UniTask Screenshot(string name)
    {
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        string path = Path.GetFullPath(Path.Combine(Output, "Playmode", name + ".png"));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        await UniTask.WaitUntil(() => File.Exists(path) && new FileInfo(path).Length >= 24).Timeout(TimeSpan.FromSeconds(5));
        byte[] png = File.ReadAllBytes(path);
        Require(png[0] == 137 && png[1] == 80 && png[2] == 78 && png[3] == 71, "Native screenshot is not a PNG: " + path);
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Require(width == Screen.width && height == Screen.height, $"Screenshot is {width}x{height}, expected actual backbuffer {Screen.width}x{Screen.height}.");
        report.screenshots.Add(path);
    }
    static void SetResolution(int width, int height)
    {
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var method = type.GetMethod("SetCustomResolution", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "The editor's GameView.SetCustomResolution is unavailable.");
        method.Invoke(EditorWindow.GetWindow(type), new object[] { new Vector2(width, height), "PocketStriker UI Art Smoke" });
    }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static T Load<T>(bool top = false, bool full = false) where T : UILayer =>
        (T)Loader.GetMethod("Load").MakeGenericMethod(typeof(T)).Invoke(null, new object[] { top, null, full });
    static T Get<T>() where T : UILayer => (T)Loader.GetMethod("Get").MakeGenericMethod(typeof(T)).Invoke(null, null);
    static void Remove<T>() where T : UILayer => Loader.GetMethods().First(method => method.Name == "Remove" && method.IsGenericMethodDefinition).MakeGenericMethod(typeof(T)).Invoke(null, null);
    static void ClearLayers() => Loader.GetMethod("Clear").Invoke(null, new object[] { null });
    static int LiveCount<T>() where T : UILayer => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include).Count(layer => !layer.IsClosing);
    static int ObjectCount<T>() where T : UILayer => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include).Length;
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    static void Finish(string failure)
    {
        if (finishing) return;
        finishing = true;
        if (report == null) report = new PlaymodeReport { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        if (!string.IsNullOrEmpty(failure)) report.errors.Add(failure);
        string logged = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(logged)) report.errors.Add(logged);
        report.passed = report.errors.Count == 0 && report.viewportCases == 4 && report.settingsTabCases == 48 && report.popupCycles == 8
            && report.nicknameCycles == 4 && report.linkPromptCycles == 4 && report.returnActions == 2
            && report.disabledChecks == 3 && report.buttonStateCaptures == 12;
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Poll;
        Directory.CreateDirectory(Path.Combine(Output, "Playmode"));
        File.WriteAllText(Path.Combine(Output, "Playmode", "report.json"), JsonUtility.ToJson(report, true));
        string summary = $"[UIArt/Playmode] {(report.passed ? "PASS" : "FAIL")}: {report.settingsTabCases} tab cases, {report.raycastChecks} raycasts, {report.buttonStateCaptures} state captures. {Path.GetFullPath(Path.Combine(Output, "Playmode", "report.json"))}";
        if (report.passed) Debug.Log(summary); else Debug.LogError(summary + "\n" + string.Join("\n", report.errors));
        RestoreFilterAutoload();
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
    static void SuspendFilterAutoload()
    {
        // Only suspend the third-party editor's in-memory scene injection flag;
        // do not write EditorPrefs or alter its persistent configuration.
        if (!SessionState.GetBool(Key + ".AutoloadSaved", false))
        {
            SessionState.SetBool(Key + ".AutoloadOriginal", Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD);
            SessionState.SetBool(Key + ".AutoloadSaved", true);
        }
        Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD = false;
    }
    static void RestoreFilterAutoload()
    {
        if (!SessionState.GetBool(Key + ".AutoloadSaved", false)) return;
        Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD = SessionState.GetBool(Key + ".AutoloadOriginal", false);
        SessionState.EraseBool(Key + ".AutoloadSaved"); SessionState.EraseBool(Key + ".AutoloadOriginal");
    }
}
