using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using mainMenu;
using ModelView;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Offline Play-mode diagnostic of the production menu preview's first skill.</summary>
[InitializeOnLoad]
public static class PocketStrikerMenuSkillPreviewValidation
{
    const string Key = "PocketStriker.MenuSkillPreview";
    const string ScenePath = "Assets/Scene/MainScene/MainMenuScene.unity";
    const double MaximumSeconds = 120;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
    static bool finishing;
    static Report report;
    static string Output => SessionState.GetString(Key + ".Output", "Logs/MenuSkillPreview/after");

    [Serializable] public sealed class Report
    {
        public bool baseline, complete, passed, externalServicesIsolated;
        public string unityVersion, startedUtc, phase;
        public string scope = "Actual MainMenuScene Awake/canvas/camera stack, local Addressables Fast Mode, production UILayerLoader.Load<FrontLayer>/ResizeAreas/FrontLayer.Initialise layout, and its authored DedicatedCameraConnector.ShowModel and SkillShowRunWithPrepare. Four newly loaded models each execute a first and repeated skill, followed by two cached-model switches, a freshly reloaded adam first/repeated frank_smashground, and two pending-request lifecycle checks. Every observed frame retains model, Animator, physics, renderer bounds, geometry-center and camera data.";
        public string limitation = "The real FrontLayer layout and button setup run against an in-memory account stub. It bypasses FrontLayer.ShowMyModel/inventory/random timer and the account process, chooses deterministic examples of the random skills, and does not certify all skills/device performance. No model pose, Rigidbody, root motion or camera is frozen or corrected by this fixture. Bounds corners are conservative world AABB projections. Pass/fail requires geometryCenter and the renderer AABB vertical edges to remain on the screen throughout every sampled frame (vertical edge tolerance 0.001); raw bounds-center visibility is reported independently. Gross whole-model jumps and stale-request playback are also rejected. frank/jack are not published mst_unit records; current human representatives are adam/tetsuya/yuta, exercising highsmashground/upanddown/frictionkick/frank_smashground and an onetwo comparison. Lifecycle checks use real cold requests: synchronous completions are explicitly reported as a limitation instead of mocked.";
        public int screenWidth, screenHeight, isolatedIAPObjects, isolatedAdsObjects, configuredFrameRate, vSyncCount;
        public Rect connectorScreenRect;
        public Vector2 connectorAnchoredPosition, connectorSize, connectorAnchorMin, connectorAnchorMax, connectorPivot;
        public List<Case> cases = new List<Case>();
        public List<LifecycleCheck> lifecycleChecks = new List<LifecycleCheck>();
        public List<string> errors = new List<string>();
    }
    [Serializable] public sealed class Case
    {
        public string recordId, model, skill, kind, framesFile;
        public bool passed, cachedInstanceReused, skillTriggered;
        public int frames, offscreenFrames, boundsCenterOffscreenFrames, rendererVerticalOffscreenFrames, maxConsecutiveOffscreenFrames, grossRootJumps;
        public float clipLength, minRendererViewportY = float.PositiveInfinity, maxRendererViewportY = float.NegativeInfinity;
        public float minGeometryViewportY = float.PositiveInfinity, maxGeometryViewportY = float.NegativeInfinity;
        public float maxRootStep, maxRootDistance, maxAnimatorDelta, initialBodyHeight, maxBodySpeed;
        public Vector3 initialRootPosition, initialCameraPosition;
        public float initialCameraSize;
        public List<Frame> samples = new List<Frame>();
    }
    [Serializable] public sealed class Frame
    {
        public int unityFrame, animatorState, nextAnimatorState;
        public string phase;
        public double elapsed;
        public float deltaTime, normalizedTime, nextNormalizedTime, layerWeight, animatorSpeed, cameraSize;
        public bool rootMotion, animatorTransition, hasRigidbody, kinematic, useGravity, geometryOnScreen, boundsCenterOnScreen;
        public Vector3 wholePosition, wholeLocalPosition, wholeScale, animatorDeltaPosition, animatorRootPosition, geometryPosition;
        public Vector3 bodyPosition, bodyVelocity, cameraPosition, rendererBoundsCenter, rendererBoundsSize;
        public Vector3 geometryViewport, boundsCenterViewport;
        public float rendererViewportBottom, rendererViewportTop, rendererViewportLeft, rendererViewportRight;
    }
    [Serializable] public sealed class LifecycleCheck
    {
        public string operation, skill, limitation, sourceModel, targetModel, sourceClipBefore, sourceClipAfter, targetClipAfter;
        public bool coldSkill, pendingObserved, sourceTriggered, targetTriggered, connectorShowingSkill, passed;
        public double elapsed;
    }
    [Serializable] sealed class SceneBackup { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }

    static PocketStrikerMenuSkillPreviewValidation()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key, false)) { SuspendFilterAutoload(); Attach(); }
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Key + ".RestoreScenes", false))
            EditorApplication.delayCall += RestoreScenes;
    }
    [MenuItem("PocketStriker/Validation/Menu Skill Preview Baseline")]
    public static void StartBaselineBatch() => Begin(true);
    [MenuItem("PocketStriker/Validation/Menu Skill Preview Verify")]
    public static void StartAfterBatch() => Begin(false);
    [MenuItem("PocketStriker/Validation/Menu Skill Preview Verify at 30 FPS")]
    public static void StartAfter30Batch() => Begin(false, 30, "after-30");

    static void Begin(bool baseline, int targetFrameRate = 60, string outputLabel = null)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start the preview diagnostic from a stopped editor.");
        if (!Application.isBatchMode)
            for (int index = 0; index < SceneManager.sceneCount; index++)
                Require(!SceneManager.GetSceneAt(index).isDirty, "Save open scenes before preview diagnostic.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Require(settings != null, "Addressables settings are missing.");
        int fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        Require(fast >= 0, "Addressables Fast Mode is missing.");
        var backup = new SceneBackup();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            backup.scenes.Add(new SceneRecord { path = scene.path, loaded = scene.isLoaded, active = scene.isActive });
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(backup));
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex);
        SessionState.SetInt(Key + ".OriginalFrameRate", Application.targetFrameRate);
        SessionState.SetInt(Key + ".OriginalVSync", QualitySettings.vSyncCount);
        SessionState.SetBool(Key + ".Baseline", baseline);
        SessionState.SetInt(Key + ".TargetFrameRate", targetFrameRate);
        // Override this exact SessionState key before invoking an entry to choose a report directory.
        if (!SessionState.GetBool(Key + ".PreserveOutput", false))
            SessionState.SetString(Key + ".Output", Path.Combine("Logs/MenuSkillPreview", outputLabel ?? (baseline ? "before" : "after")));
        SessionState.SetString(Key + ".Start", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key + ".RestoreScenes", false);
        SessionState.SetInt(Key + ".IAP", 0); SessionState.SetInt(Key + ".Ads", 0);
        SessionState.SetBool(Key, true); finishing = false; report = null;
        settings.ActivePlayModeDataBuilderIndex = fast;
        SuspendFilterAutoload(); Attach();
        var gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        gameView.GetMethod("SetCustomResolution", Fields).Invoke(EditorWindow.GetWindow(gameView), new object[] { new Vector2(390, 844), "Menu Skill Preview" });
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var pre = UnityEngine.Object.FindAnyObjectByType<PreScene>();
        Require(pre != null, "Authored MainMenuScene PreScene is missing.");
        pre.enabled = false;
        EditorApplication.isPlaying = true;
    }
    static void Attach()
    {
        Application.logMessageReceived -= CaptureError; Application.logMessageReceived += CaptureError;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        SceneManager.sceneLoaded -= IsolateServices; SceneManager.sceneLoaded += IsolateServices;
    }
    static void IsolateServices(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        foreach (var pre in UnityEngine.Object.FindObjectsByType<PreScene>(FindObjectsInactive.Include, FindObjectsSortMode.None)) pre.enabled = false;
        foreach (var startup in UnityEngine.Object.FindObjectsByType<StartUpPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None)) startup.enabled = false;
        foreach (var shop in UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            shop.gameObject.SetActive(false); UnityEngine.Object.Destroy(shop.gameObject);
            SessionState.SetInt(Key + ".IAP", SessionState.GetInt(Key + ".IAP", 0) + 1);
        }
        IAPManager.Target = null;
        foreach (var ads in UnityEngine.Object.FindObjectsByType<AdsInitializer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ads.gameObject.SetActive(false); UnityEngine.Object.Destroy(ads.gameObject);
            SessionState.SetInt(Key + ".Ads", SessionState.GetInt(Key + ".Ads", 0) + 1);
        }
        foreach (var input in UnityEngine.Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None)) input.enabled = false;
    }
    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string errors = SessionState.GetString(Key + ".Errors", "");
        if (errors.Length < 16000) SessionState.SetString(Key + ".Errors", errors + message + "\n" + stack + "\n");
    }
    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        string errors = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(errors)) { Finish(errors); return; }
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > MaximumSeconds)
        { Finish("Preview diagnostic exceeded its 120-second deadline; phase=" + report?.phase); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false) || PreScene.target == null) return;
        SessionState.SetBool(Key + ".Running", true); Run().Forget();
    }
    static async UniTask Run()
    {
        report = new Report { baseline = SessionState.GetBool(Key + ".Baseline", false), unityVersion = Application.unityVersion,
            startedUtc = DateTime.UtcNow.ToString("O"), phase = "local-configuration" };
        var oldSetting = AppSetting.Value;
        var oldAccount = PlayerAccountInfo.Me;
        var oldWebRequest = Addressables.WebRequestOverride;
        GameObject fixture = null;
        try
        {
            Directory.CreateDirectory(Output);
            Require(!PreScene.target.enabled && !AdsInitializer.ShouldEnableAds(), "Offline startup isolation is missing.");
            await UniTask.NextFrame();
            Require(UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Original IAP startup survived isolation.");
            Require(!PlayFab.PlayFabClientAPI.IsClientLoggedIn(), "Refuse to run this fixture with a real PlayFab login.");
            Addressables.WebRequestOverride = request =>
            {
                if (Uri.TryCreate(request.url, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                    throw new InvalidOperationException("The offline preview fixture blocked an external Addressables request.");
            };
            AppSetting.Value = new AppSetting { Language = SystemLanguage.Chinese };
            PlayerAccountInfo.Me = new PlayerAccountInfo { tutorialProgress = "Finished", noAdsState = true };
            AssetDatabase.LoadAssetAtPath<CommonSetting>("Assets/Setting/CommonSetting.asset").Initialise();
            report.configuredFrameRate = Mathf.Max(1, SessionState.GetInt(Key + ".TargetFrameRate", 60));
            Application.targetFrameRate = report.configuredFrameRate; QualitySettings.vSyncCount = 0; report.vSyncCount = QualitySettings.vSyncCount;
            CommonSetting.DevMode = false; FightGlobalSetting.SceneStep = 0; Time.timeScale = 1;
            await Addressables.InitializeAsync().Task;
            await UniTask.WhenAll(Units.LoadUnitConfigs(), SkillConfigTable.LoadAllSkillConfigs(), PowerEstimateTable.LoadFile(),
                Translate.LoadLanguageCodes(_ => UniTask.FromResult(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ExternalAssets/Config/LanguageCode.csv"))));
            await FightGlobalSetting.LoadFightParams();
            await AddressablesLogic.Essentials();
            CallLoader("Clear", new object[] { null });
            CallLoader("SetHanger", new object[] { PosCal.SafeAreaRect, PosCal.Canvas.transform });
            CallLoader("SetEffectBg", new object[] { Field<RawImage>(PreScene.target, "effectBg").rectTransform });
            report.externalServicesIsolated = true;
            report.isolatedIAPObjects = SessionState.GetInt(Key + ".IAP", 0); report.isolatedAdsObjects = SessionState.GetInt(Key + ".Ads", 0);
            report.screenWidth = Screen.width; report.screenHeight = Screen.height;
            var front = (FrontLayer)Loader.GetMethod("Load", Fields).MakeGenericMethod(typeof(FrontLayer))
                .Invoke(null, new object[] { false, null, false });
            fixture = front.gameObject;
            Canvas.ForceUpdateCanvases();
            front.Initialise(PreScene.target);
            var connector = Field<DedicatedCameraConnector>(front, "camConnector");
            connector.gameObject.SetActive(true);
            Field<Image>(front, "view2D").gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)connector.transform;
            report.connectorAnchoredPosition = rect.anchoredPosition; report.connectorSize = rect.rect.size;
            report.connectorAnchorMin = rect.anchorMin; report.connectorAnchorMax = rect.anchorMax; report.connectorPivot = rect.pivot;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, point)).ToArray();
            report.connectorScreenRect = Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
            var cached = new Dictionary<string, Data_Center>();
            var records = new[] { "1", "2", "7", "17" };
            var skills = new[] { "highsmashground", "upanddown", "frictionkick", "El_Attack_Bite_Left" };
            for (int index = 0; index < records.Length; index++)
            {
                await connector.ShowModel(records[index]);
                cached.Add(records[index], connector.FocusingC);
                await ObserveSkill(connector, records[index], skills[index], "first", false);
                await ObserveSkill(connector, records[index], skills[index], "repeat", true);
            }
            foreach (var index in new[] { 0, 3 })
            {
                await connector.ShowModel(records[index]);
                Require(connector.FocusingC == cached[records[index]], "Cached model switch instantiated a new model.");
                await ObserveSkill(connector, records[index], index == 0 ? "onetwo" : skills[index], "cached-switch", true);
            }
            await ObserveLifecycle(connector, "disable", "mirrorcannon", "2");
            await ObserveLifecycle(connector, "cached-switch", "forwardpush_long", "2");
            connector.ClearBackUpModels();
            await connector.ShowModel("1");
            await ObserveSkill(connector, "1", "frank_smashground", "first-reloaded", false);
            await ObserveSkill(connector, "1", "frank_smashground", "repeat-reloaded", true);
            Require(report.cases.Count == 12 && report.cases.All(item => item.skillTriggered && item.frames >= 2)
                && report.lifecycleChecks.Count == 2, "Preview coverage is incomplete.");
            Require(!PlayFab.PlayFabClientAPI.IsClientLoggedIn(), "A real account login was activated.");
            report.complete = true;
            report.passed = report.baseline || report.cases.All(item => item.passed) && report.lifecycleChecks.All(item => item.passed);
            if (!report.passed) report.errors.Add("A preview body left the screen, made a gross whole-model jump, or a stale skill request triggered a model; inspect the case JSON files and lifecycleChecks.");
            report.phase = "complete";
        }
        catch (Exception exception) { if (!finishing) report.errors.Add(exception.ToString()); }
        finally
        {
            Addressables.WebRequestOverride = oldWebRequest; AppSetting.Value = oldSetting; PlayerAccountInfo.Me = oldAccount;
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
            CallLoader("Clear", new object[] { null });
            Finish(null);
        }
    }
    static async UniTask ObserveLifecycle(DedicatedCameraConnector connector, string operation, string skill, string targetRecord)
    {
        await connector.ShowModel("1");
        var source = connector.FocusingC;
        var item = new LifecycleCheck { operation = operation, skill = skill, sourceModel = "adam",
            targetModel = operation == "cached-switch" ? Units.GetUnitConfig(targetRecord).REAL_NAME : "disabled adam",
            sourceClipBefore = source.AnimationManger._toUse != null ? source.AnimationManger._toUse.name : "",
            coldSkill = !AnimationResourceLoaderCore.HasAnimationClip(AnimationResourceKeyUtility.SkillClipKey("human", skill)) };
        report.lifecycleChecks.Add(item); report.phase = "lifecycle/" + operation; Save();
        double started = Time.realtimeSinceStartupAsDouble;
        var request = connector.SkillShowRunWithPrepare(skill).Preserve();
        item.pendingObserved = request.Status == UniTaskStatus.Pending;
        if (!item.pendingObserved || !item.coldSkill)
        {
            await request;
            item.limitation = !item.pendingObserved ? "The real request completed synchronously; no preload race was available to exercise."
                : "The clip was already globally cached; this run cannot certify a cold preload race.";
            item.passed = true; item.elapsed = Time.realtimeSinceStartupAsDouble - started;
            // Restore idle via the production selection API before the next case.
            await connector.ShowModel("1"); Save(); return;
        }
        Data_Center target = null;
        if (operation == "disable")
        {
            connector.gameObject.SetActive(false);
            try
            {
                await request;
                item.connectorShowingSkill = (bool)typeof(DedicatedCameraConnector).GetProperty("IfShowingSkill", Fields).GetValue(connector);
            }
            finally { connector.gameObject.SetActive(true); }
        }
        else
        {
            // ShowModel updates the cached selection before its five-frame bounds wait.
            var selection = connector.ShowModel(targetRecord).Preserve();
            target = connector.FocusingC;
            await request;
            item.connectorShowingSkill = (bool)typeof(DedicatedCameraConnector).GetProperty("IfShowingSkill", Fields).GetValue(connector);
            await selection;
        }
        item.sourceClipAfter = source.AnimationManger._toUse != null ? source.AnimationManger._toUse.name : "";
        item.sourceTriggered = item.sourceClipAfter == skill || source.AnimationManger.GetBool("in_transition");
        if (target != null)
        {
            item.targetClipAfter = target.AnimationManger._toUse != null ? target.AnimationManger._toUse.name : "";
            item.targetTriggered = item.targetClipAfter == skill || target.AnimationManger.GetBool("in_transition");
            item.passed = target != source && !item.sourceTriggered && !item.targetTriggered && !item.connectorShowingSkill;
        }
        else item.passed = !item.sourceTriggered && !item.connectorShowingSkill;
        item.elapsed = Time.realtimeSinceStartupAsDouble - started;
        await connector.ShowModel("1"); Save();
        Debug.Log("[MenuSkillPreview] lifecycle " + operation + " pending=" + item.pendingObserved + " cold=" + item.coldSkill + " passed=" + item.passed);
    }
    static async UniTask ObserveSkill(DedicatedCameraConnector connector, string record, string skill, string kind, bool reused)
    {
        var data = connector.FocusingC;
        var camera = Field<Camera>(connector, "camera");
        var animator = data.AnimationManger.AnimatorRef;
        var item = new Case { recordId = record, model = Units.GetUnitConfig(record).REAL_NAME, skill = skill, kind = kind,
            cachedInstanceReused = reused, initialRootPosition = data.WholeT.position, initialCameraPosition = camera.transform.position,
            initialCameraSize = camera.orthographicSize, initialBodyHeight = BodyBounds(data).size.y };
        report.cases.Add(item); report.phase = item.model + "/" + skill + "/" + kind;
        item.framesFile = Path.GetFullPath(Path.Combine(Output, report.cases.Count.ToString("00") + "-" + item.model + "-" + kind + ".json"));
        double started = Time.realtimeSinceStartupAsDouble;
        Sample(item, data, camera, started, "before-trigger"); Save();
        var operation = connector.SkillShowRunWithPrepare(skill).Preserve();
        while (operation.Status == UniTaskStatus.Pending)
        {
            await FrameBoundary(); Sample(item, data, camera, started, "preparation");
        }
        await operation;
        item.clipLength = data.AnimationManger._toUse != null ? data.AnimationManger._toUse.length : 0;
        Require(item.clipLength > 0, "The selected skill did not load: " + skill);
        item.skillTriggered = true;
        double until = Time.realtimeSinceStartupAsDouble + item.clipLength / Mathf.Max(.01f, animator.speed) + 1.7;
        while (Time.realtimeSinceStartupAsDouble < until)
        {
            await FrameBoundary(); Sample(item, data, camera, started, "skill-and-return");
        }
        item.frames = item.samples.Count;
        item.passed = item.offscreenFrames == 0 && item.rendererVerticalOffscreenFrames == 0 && item.grossRootJumps == 0;
        File.WriteAllText(item.framesFile, JsonUtility.ToJson(item, true));
        // The frame stream lives in the case file; the summary stays small and easy to compare.
        item.samples.Clear(); Save();
        Debug.Log("[MenuSkillPreview] " + item.model + " " + kind + " offscreen=" + item.offscreenFrames + " maxRootStep=" + item.maxRootStep + " maxDelta=" + item.maxAnimatorDelta);
    }
    static async UniTask FrameBoundary()
    {
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        if (finishing) throw new OperationCanceledException();
    }
    static Bounds BodyBounds(Data_Center data)
    {
        var renderers = data.WholeT.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy
            && renderer.GetComponent<ParticleSystem>() == null).ToArray();
        Require(renderers.Length > 0, "Preview model has no renderer.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static bool OnScreen(Vector3 point) => point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
    static void Sample(Case item, Data_Center data, Camera camera, double started, string phase)
    {
        var animator = data.AnimationManger.AnimatorRef;
        var bounds = BodyBounds(data);
        var body = data._BasicPhysicSupport != null ? data._BasicPhysicSupport.Rigidbody : data.WholeT.GetComponent<Rigidbody>();
        var state = animator.GetCurrentAnimatorStateInfo(1);
        var next = animator.GetNextAnimatorStateInfo(1);
        var sample = new Frame { unityFrame = Time.frameCount, phase = phase, elapsed = Time.realtimeSinceStartupAsDouble - started, deltaTime = Time.deltaTime,
            wholePosition = data.WholeT.position, wholeLocalPosition = data.WholeT.localPosition, wholeScale = data.WholeT.lossyScale,
            animatorDeltaPosition = animator.deltaPosition, animatorRootPosition = animator.rootPosition, rootMotion = animator.applyRootMotion,
            animatorState = state.fullPathHash, normalizedTime = state.normalizedTime, nextAnimatorState = next.fullPathHash, nextNormalizedTime = next.normalizedTime,
            animatorTransition = animator.IsInTransition(1), animatorSpeed = animator.speed, layerWeight = animator.GetLayerWeight(1),
            cameraPosition = camera.transform.position, cameraSize = camera.orthographicSize,
            rendererBoundsCenter = bounds.center, rendererBoundsSize = bounds.size, boundsCenterViewport = camera.WorldToViewportPoint(bounds.center),
            geometryPosition = data.geometryCenter.position, geometryViewport = camera.WorldToViewportPoint(data.geometryCenter.position),
            hasRigidbody = body != null, rendererViewportBottom = float.PositiveInfinity, rendererViewportTop = float.NegativeInfinity,
            rendererViewportLeft = float.PositiveInfinity, rendererViewportRight = float.NegativeInfinity };
        if (body != null)
        {
            sample.bodyPosition = body.position; sample.bodyVelocity = body.linearVelocity;
            sample.kinematic = body.isKinematic; sample.useGravity = body.useGravity;
            item.maxBodySpeed = Mathf.Max(item.maxBodySpeed, sample.bodyVelocity.magnitude);
        }
        for (int corner = 0; corner < 8; corner++)
        {
            var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            var point = camera.WorldToViewportPoint(world);
            sample.rendererViewportBottom = Mathf.Min(sample.rendererViewportBottom, point.y); sample.rendererViewportTop = Mathf.Max(sample.rendererViewportTop, point.y);
            sample.rendererViewportLeft = Mathf.Min(sample.rendererViewportLeft, point.x); sample.rendererViewportRight = Mathf.Max(sample.rendererViewportRight, point.x);
        }
        sample.geometryOnScreen = OnScreen(sample.geometryViewport); sample.boundsCenterOnScreen = OnScreen(sample.boundsCenterViewport);
        bool visible = sample.geometryOnScreen;
        if (!visible) item.offscreenFrames++;
        if (!sample.boundsCenterOnScreen) item.boundsCenterOffscreenFrames++;
        if (sample.rendererViewportBottom < -.001f || sample.rendererViewportTop > 1.001f) item.rendererVerticalOffscreenFrames++;
        int consecutive = 0;
        if (!visible)
        {
            consecutive = 1;
            for (int index = item.samples.Count - 1; index >= 0 && !item.samples[index].geometryOnScreen; index--) consecutive++;
        }
        item.maxConsecutiveOffscreenFrames = Mathf.Max(item.maxConsecutiveOffscreenFrames, consecutive);
        if (item.samples.Count > 0)
        {
            float step = Vector3.Distance(sample.wholePosition, item.samples[item.samples.Count - 1].wholePosition);
            item.maxRootStep = Mathf.Max(item.maxRootStep, step);
            if (step > Mathf.Max(2f, item.initialBodyHeight * .75f)) item.grossRootJumps++;
        }
        item.maxRootDistance = Mathf.Max(item.maxRootDistance, Vector3.Distance(sample.wholePosition, item.initialRootPosition));
        item.maxAnimatorDelta = Mathf.Max(item.maxAnimatorDelta, sample.animatorDeltaPosition.magnitude);
        item.minRendererViewportY = Mathf.Min(item.minRendererViewportY, sample.rendererViewportBottom); item.maxRendererViewportY = Mathf.Max(item.maxRendererViewportY, sample.rendererViewportTop);
        item.minGeometryViewportY = Mathf.Min(item.minGeometryViewportY, sample.geometryViewport.y); item.maxGeometryViewportY = Mathf.Max(item.maxGeometryViewportY, sample.geometryViewport.y);
        item.samples.Add(sample);
    }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    static object CallLoader(string method, object[] arguments) => Loader.GetMethods(Fields)
        .Single(item => item.Name == method && item.GetParameters().Length == arguments.Length).Invoke(null, arguments);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Save()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        foreach (var item in report.cases.Where(item => item.samples.Count > 0 && !string.IsNullOrEmpty(item.framesFile)))
            File.WriteAllText(item.framesFile, JsonUtility.ToJson(item, true));
    }
    static void Finish(string error)
    {
        if (finishing) return; finishing = true;
        report ??= new Report { baseline = SessionState.GetBool(Key + ".Baseline", false), unityVersion = Application.unityVersion, startedUtc = DateTime.UtcNow.ToString("O"), phase = "startup" };
        if (!string.IsNullOrEmpty(error)) report.errors.Add(error);
        report.passed &= report.complete && report.errors.Count == 0; Save();
        SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".RestoreScenes", true);
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Poll; SceneManager.sceneLoaded -= IsolateServices;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        RestoreFilterAutoload();
        Application.targetFrameRate = SessionState.GetInt(Key + ".OriginalFrameRate", -1);
        QualitySettings.vSyncCount = SessionState.GetInt(Key + ".OriginalVSync", 0);
        Debug.Log("[MenuSkillPreview] " + (report.passed ? "PASS" : "FAIL") + " " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        SessionState.SetBool(Key + ".Exit", Application.isBatchMode); SessionState.SetInt(Key + ".ExitCode", report.passed ? 0 : 1);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else EditorApplication.delayCall += RestoreScenes;
    }
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (SessionState.GetBool(Key, false)) Finish("Play mode stopped before the preview diagnostic completed.");
        if (SessionState.GetBool(Key + ".RestoreScenes", false)) EditorApplication.delayCall += RestoreScenes;
    }
    static void RestoreScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Key + ".RestoreScenes", false);
        var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".Scenes", "{}"));
        var setup = backup?.scenes.Where(scene => !string.IsNullOrEmpty(scene.path)).Select(scene => new SceneSetup
            { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
        if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        if (SessionState.GetBool(Key + ".Exit", false))
        { SessionState.SetBool(Key + ".Exit", false); EditorApplication.Exit(SessionState.GetInt(Key + ".ExitCode", 1)); }
    }
    static void SuspendFilterAutoload()
    {
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
