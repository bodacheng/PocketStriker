using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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

/// <summary>Offline, rendered review of the six authored MainMenuScene backgrounds.</summary>
[InitializeOnLoad]
public static class PocketStrikerMenuBackgroundValidation
{
    const string Key = "PocketStriker.MenuBackgroundReview";
    const string MenuScene = "Assets/Scene/MainScene/MainMenuScene.unity";
    const double MaximumSeconds = 900;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
    static readonly Element[] Elements = { Element.redMagic, Element.greenMagic, Element.blueMagic, Element.lightMagic, Element.darkMagic, Element.Null };
    static readonly string[] Names = { "red", "green", "blue", "light", "dark", "null" };
    static readonly string[] TextureGuids = { "eeefd2f03ff5f4541bbbef04d1738e92", "e8e966676cb5e46fdb42579bb766cc9d", "b3fe5a1162ddb4abb81010d278b820c7", "ac31607d78a8a4677b2dd1a525c3e5da", "404e72dee7db1419299acd1370911f1a", "532a8e9fc730c44af96fd53fbe0a41d8" };
    static readonly Vector2Int[] Viewports = { new Vector2Int(375,667), new Vector2Int(390,844), new Vector2Int(540,960), new Vector2Int(768,1024) };
    static bool finishing;
    static Report report;
    static string selectedId;
    static bool nullFocus;
    static readonly Dictionary<RawImage, Vector2> authoredUvCenters = new Dictionary<RawImage, Vector2>();
    static string Output => SessionState.GetString(Key + ".Output", "Logs/MenuBackgroundReview/Before/Runtime");

    [Serializable] public sealed class Report
    {
        public bool passed, complete, baseline, artRefresh, previewOnly, dynamicReview, staticGradient, expectedVerticalFlip, externalServicesIsolated, sourceAssetsUnchanged;
        public string unityVersion, utcTime, sourceHead, phase, reviewLabel, sourceSet;
        public string scope = "Editor Play mode in the actual MainMenuScene, production PreScene Awake/canvas/cameras, BackGroundPS, OffsetScrolling, Resources UILayerLoader, FrontLayer, UnitListPage, LowerMainBar and ReturnLayer. A local FrontPage subclass calls its production EnterProcess after local inventory/configuration initialization, bypassing only PreScene.DataLoading. Native EventSystem pointers exercise fighter selection, return and HOME layer reload. No login, account refresh, check-in, story, mail request, advertising or purchase action is executed.";
        public string limitation = "PreScene.Start is disabled before Start. Original IAP GameObjects are destroyed before Start; a never-active IAP stub supplies the false IsInitialized property required by UpperInfoBar. Ads are disabled in macOS Editor and removed. Inventory contains one authored representative per case to make random HOME focus deterministic. Null has no authored fighter: SetFocusingUnit(null) verifies the real fallback while the previous red preview remains frozen. Static comparisons freeze idle pose and preview camera, and restore each authored background UV center recorded before fixture phase tests; separate samples exercise scrolling and wrap. HOME reload recreates production UI layers, rather than reloading the scene or contacting account services. Editor viewport sizes, not device safe areas or GPU performance.";
        public int isolatedIAPObjects, isolatedAdsObjects, pointerClicks, navigationCycles, homeReloads, nativeTabCycles, viewportCases, semanticChecks, scrollingChecks;
        public int aspectChecks, preservedPhaseChecks, resizeChecks, zeroRectRecoveryChecks, lateTextureRecoveryChecks;
        public int legacyReferenceChecks, invalidReferenceChecks, invalidReferenceRecoveryChecks, scrollSpeedChecks;
        public int approvedScaleChecks, dynamicFrameChecks;
        public int staticGpuPaletteChecks, staticBadUvRecoveryChecks, staticScrollerRestartGuardChecks, legacyDefaultModeChecks;
        public List<StaticPaletteSample> staticPaletteSamples = new List<StaticPaletteSample>();
        public int actualSceneReloads;
        public List<StaticSceneReload> staticSceneReloads = new List<StaticSceneReload>();
        public int staticEndpointChecks, staticWaitChecks, staticNavigationChecks, staticSceneReloadChecks, staticScrollerChecks, modelAnimationChecks;
        public List<StaticObservation> staticObservations = new List<StaticObservation>();
        public Vector2 approvedPortraitSpan, approvedRepeatUnit;
        public Vector2 referenceUvSize;
        public List<Vector2> authoredUvCenters = new List<Vector2>();
        public int unitSize, unitWidth, unitHeight, nativeEdgeChecks, identicalCellChecks, densityChecks;
        public string densityDefinition = "visibleCellsX/Y = decoded PNG width/height * actual UV width/height / native repeat-unit width/height; screenCellPixelsX/Y = full background screen-rectangle width/height / visibleCellsX/Y. These are full-viewport repeat-unit equivalents including UI occlusion and partial units, not unobscured complete units or diamond-motif counts.";
        public List<NativeTile> nativeTileChecks = new List<NativeTile>();
        public List<DynamicSequence> dynamicSequences = new List<DynamicSequence>();
        public List<Case> cases = new List<Case>();
        public List<string> checks = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
    }
    [Serializable] public sealed class Case
    {
        public string viewport, color, element, recordId, texturePath, textureGuid, textureSha256, screenshot, reloadedScreenshot, unitRgbaSha256;
        public int backgroundIndex, activeBackgrounds, textureWidth, textureHeight, poseHash;
        public bool focusIsNull, nativeReturn, nativeReload, backgroundCoversViewport, textureRepeat, sourceRaycastDisabled;
        public Rect backgroundScreenRect, uvRect;
        public Vector2 expectedUvSize, referenceUvSize, effectiveReferenceUvSize, authoredUvCenter;
        public Vector2 approvedActualSpan, approvedExpectedSampledPixels, approvedActualVisibleUnits, approvedExpectedVisibleUnits;
        public Vector2 approvedExpectedScreenNativePixelScale;
        public Color backgroundTint;
        public Vector3 cameraPosition, cameraRotation, modelPosition, modelRotation;
        public Vector3 modelWorldScale, uiCanvasWorldScale, frontLayerWorldScale;
        public bool onlyBackgroundSamplingVerticallyFlipped;
        public float cameraSize, poseNormalizedTime, rendererViewportArea, textureAspect, rectangleAspect;
        public int unitSize, unitWidth, unitHeight, cellRows, cellColumns;
        public int nativeTileCheckIndex = -1;
        public float visibleCellsX, visibleCellsY, screenCellPixelsX, screenCellPixelsY;
        public string materialPath, materialSha256, shader, samplerU, samplerV, filterMode;
        public Vector2 sampledNativePixels, screenNativePixelScale, approximateBlockScreenPixelsRange;
        public string staticBlockScaleDefinition = "Native source boundaries vary approximately 42-44 pixels; this range is observational and is not an exact repeat unit. Screen block scale follows the independent full-height native-pixel scale. Six byte-identical PNGs preserve the same geometry across palettes.";
        public ScrollSample scroll;
        public StaticObservation staticWait, nativeReturnStatic, nativeReloadStatic;
    }
    [Serializable] public sealed class NativeTile
    {
        public string color, texturePath, pngSha256, unitRgbaSha256;
        public string cellByteFormat = "RGBA32 (four 8-bit channels), top-left row order";
        public int width, height, unitSize, unitWidth, unitHeight, rows, columns, uniqueCells, cellByteCount;
        public bool horizontalEdgesEqual, verticalEdgesEqual, everyCellIdentical;
    }
    [Serializable] public sealed class ScrollSample
    {
        public Vector2 configuredDirection, configuredUvPerSecond, measuredUvPerSecond, expectedUvDelta, actualUvDelta;
        public Vector2 startUvPosition, endUvPosition, patternScreenPixelsPerSecond;
        public float configuredSpeed, scaledElapsed, uvDeltaError;
        public double realtimeElapsed, scaledClockElapsed;
        public int frames;
        public Rect wrapBeforeUv, wrapAfterUv;
        public string wrapBeforeScreenshot, wrapAfterScreenshot;
        public string timing = "Actual production Update interval, aligned at LastPostLateUpdate; expected UV delta sums each sampled frame's scaled Time.deltaTime. Screen pattern motion has the opposite sign to texture sampling motion.";
    }
    [Serializable] public sealed class DynamicSequence
    {
        public string viewport, color;
        public string timing = "Native ScreenCapture requests at LastPostLateUpdate, approximately every 0.2 realtime seconds for 7 seconds. UV, UTC and frame clocks are recorded at request time; the actual screenshot dimensions are read back. Fighter idle pose/camera are frozen by the offline fixture; background OffsetScrolling runs at its authored speed.";
        public Vector2 configuredDirection, startUvPosition, endUvPosition, expectedUvDelta, actualUvDelta;
        public float configuredSpeed, scaledElapsed, uvDeltaError;
        public double realtimeElapsed;
        public int updateFrames;
        public List<DynamicFrame> samples = new List<DynamicFrame>();
    }
    [Serializable] public sealed class DynamicFrame
    {
        public int index, unityFrame;
        public string utcTime, screenshot;
        public float deltaTime, scaledElapsed;
        public double realtimeElapsed, scaledClockElapsed;
        public Rect uvRect;
        public Vector2 expectedUvPosition;
    }
    [Serializable] public sealed class StaticSceneReload
    {
        public string scenePath, viewport, startedUtc, finishedUtc;
        public bool productionSceneReplaced, serviceIsolationRetained;
        public List<Rect> beforeUv = new List<Rect>(), afterUv = new List<Rect>();
    }
    [Serializable] public sealed class StaticPaletteSample
    {
        public string color, sourcePngSha256, materialPath, materialSha256, shader, screenshot, colorSpace;
        public string scope = "Temporary white RawImage in an isolated plain overlay Canvas, rendered through the actual authored UI shader/material and captured by native ScreenCapture. Separate HOME screenshots retain the complete real UI/model.";
        public bool preserveSource;
        public Vector3 displaySrgbPalette;
        public Rect uvRect;
        public float maxChannelError8Bit;
        public List<PalettePixel> samples = new List<PalettePixel>();
    }
    [Serializable] public sealed class PalettePixel
    {
        public Vector2Int screenPixel, sourcePixel;
        public Color32 sourceRgba, actualRgba;
        public Vector3 expectedDisplaySrgb8Bit;
        public float maxChannelError8Bit;
    }
    [Serializable] public sealed class StaticObservation
    {
        public string purpose, viewport, color, startedUtc, finishedUtc;
        public string timing = "Actual production frames; the fixture does not disable OffsetScrolling, write UVs, freeze Animator, or change Time.timeScale in static mode.";
        public List<Rect> beforeUv = new List<Rect>(), afterUv = new List<Rect>();
        public List<bool> scrollerEnabledBefore = new List<bool>(), scrollerEnabledAfter = new List<bool>();
        public float scaledElapsed, maxUvError, animatorSpeed, animatorNormalizedTimeBefore, animatorNormalizedTimeAfter;
        public double realtimeElapsed;
        public int frames, animatorStateBefore, animatorStateAfter;
        public bool modelAnimatorAdvanced;
        public List<string> uiAnimatorNames = new List<string>();
        public List<bool> uiAnimatorEnabledBefore = new List<bool>(), uiAnimatorEnabledAfter = new List<bool>();
    }
    [Serializable] sealed class SceneBackup { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }

    // Only the account fetch is substituted. EnterProcess, model loader, UI,
    // busy state, navigation callbacks and ProcessEnd are the production code.
    sealed class OfflineFrontPage : FrontPage
    {
        public override void ProcessEnter()
        {
            typeof(FrontPage).GetField("_askedIfLinkDevice", Fields).SetValue(this, true);
            typeof(FrontPage).GetMethod("EnterProcess", Fields).Invoke(this, null);
        }
    }

    static PocketStrikerMenuBackgroundValidation()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key, false)) { SuspendFilterAutoload(); Attach(); }
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Key + ".RestoreScenes", false))
            EditorApplication.delayCall += RestoreScenes;
    }

    public static void StartBaselineBatch() => Begin(true);
    [MenuItem("PocketStriker/Validation/Menu Background Offline Review")]
    public static void StartAfterBatch() => Begin(false);
    public static void StartArtRefreshBatch() => Begin(false, true);
    public static void StartRedArtPreviewBatch() => Begin(false, true, true);
    public static void StartStaticGradientBatch() => Begin(false, false, false, true);
    public static void StartStaticGradientRedPreviewBatch() => Begin(false, false, true, true);
    public static void StartStaticFlippedGradientBatch() => Begin(false, false, false, true, true);

    static void Begin(bool baseline, bool artRefresh = false, bool previewOnly = false, bool staticGradient = false, bool expectedVerticalFlip = false)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start the menu background review from a stopped editor.");
        string unitSetting = Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_UNIT_SIZE");
        int unitSize = 0;
        if (!string.IsNullOrEmpty(unitSetting))
            Require(int.TryParse(unitSetting, out unitSize) && unitSize > 0 && unitSize <= 4096,
                "POCKETSTRIKER_MENU_UNIT_SIZE must be a positive pixel size no larger than 4096.");
        string widthSetting = Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_UNIT_WIDTH");
        string heightSetting = Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_UNIT_HEIGHT");
        int unitWidth = unitSize, unitHeight = unitSize;
        if (!string.IsNullOrEmpty(widthSetting) || !string.IsNullOrEmpty(heightSetting))
        {
            Require(int.TryParse(widthSetting, out unitWidth) && int.TryParse(heightSetting, out unitHeight)
                && unitWidth > 0 && unitWidth <= 4096 && unitHeight > 0 && unitHeight <= 4096,
                "POCKETSTRIKER_MENU_UNIT_WIDTH and UNIT_HEIGHT must both be positive pixel sizes no larger than 4096.");
            Require(unitSize == 0 || unitSize == unitWidth && unitSize == unitHeight,
                "UNIT_SIZE conflicts with the explicitly supplied rectangular unit dimensions.");
            unitSize = unitWidth == unitHeight ? unitWidth : 0;
        }
        if (!Application.isBatchMode)
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scenes before review.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Require(settings != null, "Addressables settings are missing.");
        int fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        Require(fast >= 0, "Addressables Fast Mode is missing.");
        var backup = new SceneBackup();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            backup.scenes.Add(new SceneRecord { path = scene.path, loaded = scene.isLoaded, active = scene.isActive });
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(backup));
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex);
        foreach (var pref in new[] { "showUnit", "PLAYFAB_CUSTOM_ID" })
        {
            SessionState.SetBool(Key + ".Had." + pref, PlayerPrefs.HasKey(pref));
            SessionState.SetString(Key + ".Pref." + pref, PlayerPrefs.GetString(pref, ""));
        }
        SessionState.SetString(Key + ".Start", DateTime.UtcNow.ToString("O"));
        string label = Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_REVIEW");
        string defaultLabel = staticGradient ? (previewOnly ? "Preview-red" : "After") : previewOnly ? "Preview-v3" : artRefresh ? "After-v3" : baseline ? "Before" : "After";
        string reviewLabel = string.IsNullOrEmpty(label) ? defaultLabel : label;
        string outputRoot = Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_REVIEW_ROOT");
        if (string.IsNullOrEmpty(outputRoot)) outputRoot = expectedVerticalFlip ? "Logs/MenuStaticPixelGradientFlipReview" : staticGradient ? "Logs/MenuStaticPixelGradientReview" : "Logs/MenuBackgroundReview";
        SessionState.SetString(Key + ".Output", Path.Combine(outputRoot, reviewLabel, "Runtime"));
        SessionState.SetString(Key + ".ReviewLabel", reviewLabel);
        SessionState.SetInt(Key + ".UnitSize", unitSize);
        SessionState.SetInt(Key + ".UnitWidth", unitWidth); SessionState.SetInt(Key + ".UnitHeight", unitHeight);
        SessionState.SetBool(Key + ".Baseline", baseline);
        SessionState.SetBool(Key + ".ArtRefresh", artRefresh); SessionState.SetBool(Key + ".PreviewOnly", previewOnly);
        SessionState.SetBool(Key + ".StaticGradient", staticGradient);
        SessionState.SetBool(Key + ".ExpectedVerticalFlip", expectedVerticalFlip);
        SessionState.SetBool(Key + ".DynamicReview", !staticGradient && Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_DYNAMIC_REVIEW") == "1");
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key + ".RestoreScenes", false);
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetInt(Key + ".IAP", 0); SessionState.SetInt(Key + ".Ads", 0);
        SessionState.SetBool(Key, true); finishing = false; report = null;
        settings.ActivePlayModeDataBuilderIndex = fast;
        SuspendFilterAutoload(); SetResolution(Viewports[0]); Attach();
        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        var pre = UnityEngine.Object.FindAnyObjectByType<PreScene>();
        Require(pre != null, "Authored MainMenuScene PreScene is missing.");
        pre.enabled = false; // Prevent its async network startup before Play mode.
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
        // Preserve the authored phase before any Update, including the frames
        // used to load local configuration before the review starts.
        if (!SessionState.GetBool(Key + ".StaticGradient", false))
            foreach (var scroll in UnityEngine.Object.FindObjectsByType<OffsetScrolling>(FindObjectsInactive.Include, FindObjectsSortMode.None)) scroll.enabled = false;
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        // No response payload or account identifier is copied into the report.
        string errors = SessionState.GetString(Key + ".Errors", "");
        if (errors.Length < 16000) SessionState.SetString(Key + ".Errors", errors + message + "\n" + stack + "\n");
    }

    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        string errors = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(errors)) { Finish(errors); return; }
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > MaximumSeconds)
        { Finish("Menu background review exceeded its 900-second deadline; phase=" + report?.phase); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        if (PreScene.target == null || BackGroundPS.target == null) return;
        SessionState.SetBool(Key + ".Running", true); Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { baseline = SessionState.GetBool(Key + ".Baseline", false),
            artRefresh = SessionState.GetBool(Key + ".ArtRefresh", false), previewOnly = SessionState.GetBool(Key + ".PreviewOnly", false), unityVersion = Application.unityVersion,
            dynamicReview = SessionState.GetBool(Key + ".DynamicReview", false), staticGradient = SessionState.GetBool(Key + ".StaticGradient", false),
            expectedVerticalFlip = SessionState.GetBool(Key + ".ExpectedVerticalFlip", false),
            unitSize = SessionState.GetInt(Key + ".UnitSize", 0),
            unitWidth = SessionState.GetInt(Key + ".UnitWidth", 0), unitHeight = SessionState.GetInt(Key + ".UnitHeight", 0),
            reviewLabel = SessionState.GetString(Key + ".ReviewLabel", ""), sourceSet = Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_SOURCE_SET") ?? "",
            utcTime = DateTime.UtcNow.ToString("O"), sourceHead = Environment.GetEnvironmentVariable("POCKETSTRIKER_MENU_SOURCE_HEAD") ?? "", phase = "local-configuration" };
        if (report.sourceSet == "Pattern-v7")
        {
            report.approvedPortraitSpan = new Vector2(941, 1672);
            report.approvedRepeatUnit = new Vector2(260, 262);
        }
        if (report.unitWidth > 0)
            report.limitation += " Native PNG decoding verifies uncompressed source edge/cell bytes in a temporary readable texture; it does not assert pixel equality after platform GPU compression. Per-viewport density uses the actual texture dimensions, UV crop and screen rectangle.";
        if (report.artRefresh)
            report.limitation += " Art-refresh entry omits the previously passed component phase/resize/zero-rectangle/late-texture suite; actual texture aspect, rendered HOME/navigation, active scrolling and wrap screenshots are rechecked against the current art.";
        if (report.previewOnly)
        {
            report.scope = "Red artwork preview only: actual MainMenuScene and production HOME/collection/return/UI layer reload at 375x667 and 768x1024 with authored fighter 1/adam. Offline isolation matches the full fixture.";
            report.limitation += " Preview-only coverage is two red cases at two sizes. This report does not certify the other five final artworks or a complete six-color art refresh.";
        }
        if (report.staticGradient)
        {
            report.scope = "StaticGradient: actual MainMenuScene and native HOME/collection/return/HOME layer reload at four portrait viewports, plus actual isolated SceneManager Single reload. Production background activation, aspect callbacks and scroller configuration are observed without fixture UV normalization or disabling scrolling. Separate explicit fault-injection tests challenge bad UV/scroller recovery. Native pointers use the production EventSystem and callbacks.";
            if (report.expectedVerticalFlip) report.scope += " Requested vertical flip changes only background sampling to y=1,height=-1; source PNGs, palettes, UI and fighter transforms remain upright.";
            if (report.previewOnly) report.scope += " Preview-only entry limits actual HOME cases to Red at 375x667 and 768x1024; six material GPU diagnostics remain technical checks, not six-color HOME coverage.";
            report.limitation = "Editor Play mode with local account/config/inventory and an OfflineFrontPage adapter; production PreScene.Start, original IAP and ads are isolated before Start. No real login or external account action. Null uses the previous red preview with actual SetFocusingUnit(null). Model animation is sampled while the background remains static. Viewport observations do not certify physical device safe areas, GPU compression or device performance.";
            Require(report.unitWidth == 0, "Static gradients do not satisfy the dynamic identical-repeat-unit checker; leave MENU_UNIT dimensions unset.");
            Require(!report.dynamicReview, "StaticGradient cannot request the dynamic scrolling capture mode.");
        }
        var activeViewports = report.previewOnly ? new[] { new Vector2Int(375,667), new Vector2Int(768,1024) } : Viewports;
        int colorCount = report.previewOnly ? 1 : 6;
        var oldAccount = PlayerAccountInfo.Me; var oldSetting = AppSetting.Value;
        var oldUnits = dataAccess.Units.Dic.ToArray();
        var oldIAP = IAPManager.Target; GameObject shopStub = null;
        var initialHashes = TextureGuids.Select(guid => Hash(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        try
        {
            Directory.CreateDirectory(Output);
            Require(!PreScene.target.enabled && !AdsInitializer.ShouldEnableAds(), "Offline service isolation was not established before startup.");
            await UniTask.NextFrame();
            Require(UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Original IAP objects or startup subscriptions survived isolation.");
            Require(!PlayFab.PlayFabClientAPI.IsClientLoggedIn(), "A real PlayFab login is present; refuse to run the local fixture.");
            shopStub = CreateShopStub();
            PlayerPrefs.SetString("PLAYFAB_CUSTOM_ID", "offline-menu-background-review");
            PlayerAccountInfo.Me = new PlayerAccountInfo { PlayFabId = "offline-menu-background-review", TitleDisplayName = "本地勇士",
                tutorialProgress = "Finished", noAdsState = true, currentLinkedDeviceId = "offline-menu-background-review" };
            AppSetting.Value = new AppSetting { Language = SystemLanguage.Chinese };
            AssetDatabase.LoadAssetAtPath<CommonSetting>(AssetDatabase.GUIDToAssetPath("cc8bc4431333a4b94b1f8148807004f0")).Initialise();
            CommonSetting.DevMode = false;
            AssetDatabase.LoadAssetAtPath<DefaultIconSetting>("Assets/Setting/DefaultIconSetting.asset").Initialise();
            await Addressables.InitializeAsync().Task;
            await UniTask.WhenAll(Units.LoadUnitConfigs(), SkillConfigTable.LoadAllSkillConfigs(), PowerEstimateTable.LoadFile(),
                Translate.LoadLanguageCodes(_ => UniTask.FromResult(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ExternalAssets/Config/LanguageCode.csv"))));
            await FightGlobalSetting.LoadFightParams();
            dataAccess.Stones.ClearData(); Currencies.CoinCount.Value = 0; Currencies.DiamondCount.Value = 0;
            CallLoader("Clear", new object[] { null });
            CallLoader("SetHanger", new object[] { PosCal.SafeAreaRect, PosCal.Canvas.transform }, new[] { typeof(Transform), typeof(Transform) });
            CallLoader("SetEffectBg", new object[] { Field<RawImage>(PreScene.target, "effectBg").rectTransform });
            ReturnLayer.Clear();
            ProcessesRunner.Main.Clear();
            ProcessesRunner.Main.Add(MainSceneStep.FrontPage, new OfflineFrontPage());
            ProcessesRunner.Main.Add(MainSceneStep.UnitList, new UnitListPage());
            var backgrounds = Field<List<GameObject>>(BackGroundPS.target, "BCPs");
            Require(backgrounds.Count == 6, "Authored BGManager does not contain six backgrounds.");
            if (!report.staticGradient)
                foreach (var image in backgrounds.Select(BackgroundImage))
                    foreach (var scroll in image.GetComponents<OffsetScrolling>()) scroll.enabled = false;
            ValidateSemantics(backgrounds);
            authoredUvCenters.Clear();
            foreach (var image in backgrounds.Select(BackgroundImage))
            {
                authoredUvCenters.Add(image, image.uvRect.center);
                report.authoredUvCenters.Add(image.uvRect.center);
            }
            report.referenceUvSize = RawReferenceUVSize(BackgroundImage(backgrounds[0]));
            Require(backgrounds.Select(BackgroundImage).All(image => RawReferenceUVSize(image) == report.referenceUvSize),
                "The six authored backgrounds use different reference UV windows.");
            if (report.unitWidth > 0) ValidateNativeTiles(backgrounds, report.unitWidth, report.unitHeight);
            report.externalServicesIsolated = true;
            report.isolatedIAPObjects = SessionState.GetInt(Key + ".IAP", 0); report.isolatedAdsObjects = SessionState.GetInt(Key + ".Ads", 0);
            report.checks.Add("Real MainMenuScene loaded; PreScene.Start, original IAP startup and advertisements isolated; local Fast Mode configuration only.");

            if (report.staticGradient)
                backgrounds = await RunStaticCases(backgrounds, activeViewports, colorCount, () =>
                {
                    shopStub = CreateShopStub();
                });
            else
            {
            foreach (var viewport in activeViewports)
            {
                SetResolution(viewport); await Stable();
                await Wait(() => Screen.width == viewport.x && Screen.height == viewport.y, 5, "actual viewport " + viewport);
                report.viewportCases++;
                if (!report.baseline && !report.artRefresh) await ValidatePhasePreservation(backgrounds);
                for (int index = 0; index < colorCount; index++)
                {
                    report.phase = viewport.x + "x" + viewport.y + "/" + Names[index]; Save();
                    UnitConfig config = Units.Dic.Values.Where(unit => unit.element == (index == 5 ? Element.redMagic : Elements[index]))
                        .OrderBy(unit => int.TryParse(unit.RECORD_ID, out int number) ? number : int.MaxValue).FirstOrDefault();
                    Require(config != null, "No authored local fighter exists for " + Elements[index]);
                    selectedId = "offline-preview-" + config.RECORD_ID; nullFocus = index == 5;
                    dataAccess.Units.Dic.Clear(); dataAccess.Units.Dic.Add(selectedId, new UnitInfo { id = selectedId, r_id = config.RECORD_ID });
                    Require(PreScene.target.trySwitchToStep(MainSceneStep.FrontPage, false), "HOME transition was refused.");
                    var connector = await HomeReady();
                    if (nullFocus) PreScene.target.SetFocusingUnit(null);
                    CheckBackground(backgrounds, index);
                    await FreezePreview(connector);
                    var image = BackgroundImage(backgrounds[index]);
                    CenterUV(image);
                    await Stable();
                    var item = MakeCase(viewport, index, config, image, connector);
                    item.screenshot = await Screenshot(viewport, Names[index] + "-home");
                    report.cases.Add(item); Save();
                    Debug.Log("[MenuBackgroundReview] CAPTURE " + item.screenshot);

                    // Actual forward transition creates the production return
                    // mission; the visible return button dispatches native events.
                    Require(PreScene.target.trySwitchToStep(MainSceneStep.UnitList, true), "Collection transition was refused.");
                    await CollectionReady();
                    var icon = Layer<UnitsLayer>().GetComponentsInChildren<HeroIcon>().Single(hero => hero.InstanceID == selectedId);
                    await NativeClick(icon.iconButton);
                    Require(PreScene.target.Focusing?.id == selectedId, "Native fighter selection did not bind the actual local unit.");
                    CheckBackground(backgrounds, index == 5 ? 0 : index);
                    int stack = ReturnLayer.ReturnMissionList.Count;
                    Require(stack == 1, "Forward collection transition did not create exactly one return mission.");
                    await NativeClick(Field<BOButton>(Layer<ReturnLayer>(), "returnButton"));
                    await HomeReady();
                    Require(ReturnLayer.ReturnMissionList.Count == stack - 1 && Layer<ReturnLayer>() == null,
                        "Native return failed to consume the completed production mission.");
                    if (nullFocus) PreScene.target.SetFocusingUnit(null);
                    CheckBackground(backgrounds, index); item.nativeReturn = true; report.navigationCycles++;

                    // HOME goes through LowerMainBar.Go and the registered
                    // process, removing/reloading FrontLayer and UpperInfoBar.
                    var previousFront = Layer<FrontLayer>();
                    await NativeClick(Field<LowerBarIcon>(Layer<LowerMainBar>(), "playTab").BOButton);
                    connector = await HomeReady();
                    Require(Layer<FrontLayer>() != previousFront, "Native HOME reload reused the closing FrontLayer.");
                    if (nullFocus) PreScene.target.SetFocusingUnit(null);
                    CheckBackground(backgrounds, index);
                    await FreezePreview(connector); CenterUV(image); await Stable();
                    item.reloadedScreenshot = await Screenshot(viewport, Names[index] + "-reloaded");
                    item.nativeReload = true; report.homeReloads++;
                    item.scroll = await CheckScrollAndWrap(viewport, index, image, backgrounds);
                    if (report.dynamicReview && index == 0 && (viewport == new Vector2Int(375,667) || viewport == new Vector2Int(768,1024)))
                        await CaptureDynamicSequence(viewport, image, backgrounds);
                    Save();
                }
                await NativeClick(Field<LowerBarIcon>(Layer<LowerMainBar>(), "fighterTab").BOButton);
                await CollectionReady();
                Require(ReturnLayer.ReturnMissionList.Count == 0, "Native fighter tab unexpectedly retained forward return history.");
                await NativeClick(Field<LowerBarIcon>(Layer<LowerMainBar>(), "playTab").BOButton);
                await HomeReady();
                // This last case is the deliberately null focus, whereas HOME
                // first resolves the real local inventory's red representative.
                CheckBackground(backgrounds, 0);
                if (!report.previewOnly) { PreScene.target.SetFocusingUnit(null); CheckBackground(backgrounds, 5); }
                report.nativeTabCycles++; Save();
            }
            if (!report.baseline && !report.artRefresh) await ValidateResizeAndRecovery(backgrounds);
            }
            report.sourceAssetsUnchanged = TextureGuids.Select(guid => Hash(AssetDatabase.GUIDToAssetPath(guid))).SequenceEqual(initialHashes);
            Require(report.cases.Count == activeViewports.Length * colorCount && report.navigationCycles == report.cases.Count
                && report.homeReloads == report.cases.Count && report.cases.All(item => item.nativeReturn && item.nativeReload)
                && report.nativeTabCycles == activeViewports.Length && report.pointerClicks == report.cases.Count * 3 + activeViewports.Length * 2
                && (report.staticGradient ? report.staticWaitChecks == report.cases.Count
                    && report.staticNavigationChecks == report.cases.Count * 2
                    && report.modelAnimationChecks == report.cases.Count
                    && report.staticSceneReloadChecks == activeViewports.Length * colorCount && report.actualSceneReloads == activeViewports.Length && report.staticGpuPaletteChecks == 6
                    && (report.previewOnly || report.resizeChecks == 30 && report.zeroRectRecoveryChecks == 6 && report.lateTextureRecoveryChecks == 6
                        && report.staticBadUvRecoveryChecks == 18 && report.staticScrollerRestartGuardChecks == 6 && report.legacyDefaultModeChecks == 3)
                    : report.scrollSpeedChecks == report.cases.Count)
                && (report.sourceSet != "Pattern-v7" || report.approvedScaleChecks == report.cases.Count)
                && (!report.dynamicReview || report.dynamicSequences.Count == 2 && report.dynamicSequences.All(sequence => sequence.realtimeElapsed >= 7
                    && sequence.samples.Count >= 20) && report.dynamicFrameChecks == report.dynamicSequences.Sum(sequence => sequence.samples.Count))
                && (report.staticGradient || report.baseline || report.artRefresh || report.legacyReferenceChecks == 6
                    && report.invalidReferenceChecks == 24 && report.invalidReferenceRecoveryChecks == 6)
                && (report.unitWidth == 0 || report.nativeTileChecks.Count == 6 && report.nativeEdgeChecks == 12
                    && report.identicalCellChecks == 6 && report.densityChecks == report.cases.Count)
                && report.sourceAssetsUnchanged, "Six-color viewport/navigation coverage is incomplete or source images changed.");
            Require(!PlayFab.PlayFabClientAPI.IsClientLoggedIn() && !IAPManager.Target.IsInitialized.Value,
                "Offline fixture activated a real account or IAP service.");
            report.complete = true; report.passed = true; report.phase = "complete";
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            CallLoader("Clear", new object[] { null }); ReturnLayer.Clear(); ProcessesRunner.Main.Clear();
            dataAccess.Units.Dic.Clear(); foreach (var unit in oldUnits) dataAccess.Units.Dic.Add(unit.Key, unit.Value);
            PlayerAccountInfo.Me = oldAccount; AppSetting.Value = oldSetting;
            if (shopStub != null) UnityEngine.Object.Destroy(shopStub); IAPManager.Target = oldIAP;
            Finish(null);
        }
    }

    static readonly Vector3[] StaticPalettes = {
        new Vector3(1,0,0), new Vector3(.12f,.60f,.22f), new Vector3(.10f,.32f,.78f),
        new Vector3(.82f,.58f,.10f), new Vector3(.48f,.13f,.65f), new Vector3(.56f,.56f,.56f) };
    const string ApprovedStaticPngSha256 = "d69af0e4378b3b61abd9eb3b1481faa9ce2135c619cae7e42218bf2b3e7b753f";
    static async UniTask ValidateStaticGpuPalettes(List<GameObject> backgrounds, Vector2Int viewport)
    {
        Require(backgrounds.Select(BackgroundImage).Select(image => AssetDatabase.GetAssetPath(image.material)).Distinct().Count() == 6,
            "Six StaticGradient states are not bound to six distinct authored materials.");
        var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Require(decoded.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(BackgroundImage(backgrounds[0]).texture)), false), "Approved static PNG could not be decoded.");
        try
        {
            var sourcePixels = decoded.GetPixels32();
            for (int index = 0; index < 6; index++)
            {
                var authored = BackgroundImage(backgrounds[index]); var material = authored.material;
                string path = AssetDatabase.GetAssetPath(authored.texture);
                Require(Hash(path) == ApprovedStaticPngSha256, "Static portrait bytes differ from the approved V3 source.");
                Require(material != null && material.shader.name == "PocketStriker/UI/StaticPixelGradient" && !ShaderUtil.ShaderHasError(material.shader),
                    "StaticGradient shader is missing or failed compilation.");
                var vector = material.GetVector("_PaletteSRGB");
                Require(Vector3.Distance(new Vector3(vector.x, vector.y, vector.z), StaticPalettes[index]) < .000001f
                    && Mathf.Abs(material.GetFloat("_PreserveSource") - (index == 0 ? 1 : 0)) < .000001f
                    && material.GetColor("_Color") == Color.white, "Authored static palette differs from the independently approved property-color contract: " + Names[index]);
                var item = new StaticPaletteSample { color = Names[index], sourcePngSha256 = Hash(path), materialPath = AssetDatabase.GetAssetPath(material),
                    materialSha256 = Hash(AssetDatabase.GetAssetPath(material)), shader = material.shader.name, displaySrgbPalette = StaticPalettes[index],
                    preserveSource = index == 0, uvRect = ExpectedStaticUv(authored), colorSpace = QualitySettings.activeColorSpace.ToString() };
                // A temporary white RawImage renders through the actual UI shader
                // in a plain overlay Canvas. It covers the UI for this diagnostic
                // capture only; no production background, model or Animator is altered.
                var overlay = new GameObject("Static Palette GPU Diagnostic Canvas", typeof(RectTransform), typeof(Canvas));
                Texture2D captured = null;
                try
                {
                    var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 32760;
                    var layer = new GameObject("Approved White Vertex RawImage", typeof(RectTransform), typeof(RawImage)); layer.transform.SetParent(overlay.transform, false);
                    var rect = (RectTransform)layer.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                    var image = layer.GetComponent<RawImage>(); image.texture = authored.texture; image.material = material; image.color = Color.white; image.uvRect = item.uvRect; image.raycastTarget = false;
                    await Stable(); item.screenshot = await Screenshot(viewport, Names[index] + "-palette-gpu-only");
                    captured = new Texture2D(2, 2, TextureFormat.RGBA32, false); Require(captured.LoadImage(File.ReadAllBytes(item.screenshot), false), "GPU diagnostic PNG could not be decoded.");
                    var pixels = captured.GetPixels32();
                    // Includes the first/last screen row, plus interior samples.
                    foreach (int y in new[] { 0, 1, viewport.y / 4, viewport.y / 2, viewport.y * 3 / 4, viewport.y - 2, viewport.y - 1 })
                    foreach (int x in new[] { viewport.x / 11, viewport.x / 3, viewport.x / 2, viewport.x * 7 / 9 })
                    {
                        float u = item.uvRect.x + (x + .5f) / viewport.x * item.uvRect.width;
                        float v = item.uvRect.y + (y + .5f) / viewport.y * item.uvRect.height;
                        int sx = Mathf.Min(941, Mathf.FloorToInt(Mathf.Repeat(u, 1) * 942));
                        int sy = Mathf.Clamp(Mathf.FloorToInt(v * 1670), 0, 1669);
                        Color32 source = sourcePixels[sy * 942 + sx]; Color32 actual = pixels[y * viewport.x + x];
                        var expected = index == 0 ? new Vector3(source.r, source.g, source.b) : source.r * StaticPalettes[index];
                        float error = Mathf.Max(Mathf.Abs(actual.r - expected.x), Mathf.Abs(actual.g - expected.y), Mathf.Abs(actual.b - expected.z));
                        item.maxChannelError8Bit = Mathf.Max(item.maxChannelError8Bit, error);
                        item.samples.Add(new PalettePixel { screenPixel = new Vector2Int(x, y), sourcePixel = new Vector2Int(sx, sy), sourceRgba = source,
                            actualRgba = actual, expectedDisplaySrgb8Bit = expected, maxChannelError8Bit = error });
                    }
                    Require(item.maxChannelError8Bit <= 3, "Actual white-vertex Canvas GPU palette differs from the approved display-sRGB formula: " + Names[index] + "; max8bitError=" + item.maxChannelError8Bit);
                    report.staticPaletteSamples.Add(item); report.staticGpuPaletteChecks++; Save();
                }
                finally { if (captured != null) UnityEngine.Object.Destroy(captured); UnityEngine.Object.Destroy(overlay); }
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            }
        }
        finally { UnityEngine.Object.Destroy(decoded); }
    }
    static async UniTask ValidateStaticFaultRecovery(List<GameObject> backgrounds)
    {
        for (int index = 0; index < 6; index++)
        {
            BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable(); var image = BackgroundImage(backgrounds[index]);
            // Explicit recovery challenge, distinct from natural static waits.
            foreach (var invalid in new[] { new Rect(.23f,.37f,.8f,.9f), new Rect(float.NaN,0,1,1), new Rect(0,0,float.PositiveInfinity,1) })
            {
                image.uvRect = invalid; RefreshAspect(image); await Stable(); CheckStaticContract(image, "fault-injected UV recovery"); report.staticBadUvRecoveryChecks++;
            }
            foreach (var scroll in image.GetComponents<OffsetScrolling>()) scroll.enabled = true;
            RefreshAspect(image); CheckStaticContract(image, "fault-injected scroller refresh guard"); CheckStaticScrollers(backgrounds, "fault-injected scroller refresh guard");
            image.gameObject.SetActive(false); image.gameObject.SetActive(true); await Stable(); CheckStaticContract(image, "same RawImage OnEnable guard"); CheckStaticScrollers(backgrounds, "same RawImage OnEnable guard");
            report.staticScrollerRestartGuardChecks++;
        }
        // An isolated default-mode component checks the new source still keeps
        // the legacy crop/phase policy; no authored static object is reconfigured.
        var probe = new GameObject("Legacy Default Aspect Probe", typeof(RectTransform), typeof(RawImage)); probe.SetActive(false);
        try
        {
            var image = probe.GetComponent<RawImage>(); image.texture = BackgroundImage(backgrounds[0]).texture;
            var component = probe.AddComponent<ScrollingBackgroundAspectFill>();
            Require(!(bool)component.GetType().GetProperty("StaticGradient").GetValue(component)
                && !(bool)component.GetType().GetProperty("FlipStaticVertically").GetValue(component), "New aspect component defaults to static/flipped instead of preserving legacy mode.");
            probe.SetActive(true);
            foreach (var size in new[] { new Vector2(375,667), new Vector2(768,1024), new Vector2(900,600) })
            {
                image.rectTransform.sizeDelta = size; image.uvRect = new Rect(.27f,.31f,.8f,.9f); var center = image.uvRect.center;
                RefreshAspect(image); float targetAspect = size.x / size.y, textureAspect = 942f / 1670f;
                var expected = targetAspect > textureAspect ? new Vector2(1, textureAspect / targetAspect) : new Vector2(targetAspect / textureAspect, 1);
                Require(Vector2.Distance(image.uvRect.size, expected) < .00001f && Vector2.Distance(image.uvRect.center, center) < .00001f,
                    "Default legacy mode changed its crop/retained center while StaticGradient was added."); report.legacyDefaultModeChecks++;
            }
        }
        finally { UnityEngine.Object.Destroy(probe); }
    }

    static GameObject CreateShopStub()
    {
        var stub = new GameObject("Menu Review IAP Property Stub"); stub.SetActive(false);
        IAPManager.Target = stub.AddComponent<IAPManager>(); IAPManager.Target.enabled = false;
        return stub;
    }
    static void SetupLocalMenuProcesses()
    {
        CallLoader("Clear", new object[] { null });
        CallLoader("SetHanger", new object[] { PosCal.SafeAreaRect, PosCal.Canvas.transform }, new[] { typeof(Transform), typeof(Transform) });
        CallLoader("SetEffectBg", new object[] { Field<RawImage>(PreScene.target, "effectBg").rectTransform });
        ReturnLayer.Clear(); ProcessesRunner.Main.Clear();
        ProcessesRunner.Main.Add(MainSceneStep.FrontPage, new OfflineFrontPage());
        ProcessesRunner.Main.Add(MainSceneStep.UnitList, new UnitListPage());
    }
    static Rect ExpectedStaticUv(RawImage image)
    {
        // Approved complete portrait, independent of production reference fields
        // or the StaticGradient implementation. Only horizontal sampling varies.
        float width = image.rectTransform.rect.width / image.rectTransform.rect.height * 1670f / 942f;
        return report.expectedVerticalFlip ? new Rect((1 - width) * .5f, 1, width, -1) : new Rect((1 - width) * .5f, 0, width, 1);
    }
    static void CheckStaticContract(RawImage image, string purpose)
    {
        Require(image.texture != null && image.texture.width == 942 && image.texture.height == 1670,
            "StaticGradient source differs from the approved complete 942x1670 portrait: " + purpose);
        var component = image.GetComponent("ScrollingBackgroundAspectFill");
        var property = component?.GetType().GetProperty("StaticGradient", BindingFlags.Public | BindingFlags.Instance);
        Require(property != null && (bool)property.GetValue(component), "Authored background is not configured in production StaticGradient mode: " + purpose);
        Require(RawReferenceUVSize(image) == Vector2.one, "StaticGradient retains a dynamic reference window: " + purpose);
        var flipProperty = component.GetType().GetProperty("FlipStaticVertically", BindingFlags.Public | BindingFlags.Instance);
        Require(flipProperty != null && (bool)flipProperty.GetValue(component) == report.expectedVerticalFlip,
            "Authored static vertical flip differs from the independently requested entry: " + purpose);
        var expected = ExpectedStaticUv(image);
        Require(RectError(image.uvRect, expected) < .00001f,
            "StaticGradient UV differs from the independent full-height portrait contract: " + purpose + "; actual=" + image.uvRect + "; expected=" + expected);
        Require(Mathf.Abs(image.uvRect.y - (report.expectedVerticalFlip ? 1 : 0)) < .000001f
            && Mathf.Abs(image.uvRect.y + image.uvRect.height - (report.expectedVerticalFlip ? 0 : 1)) < .000001f,
            "StaticGradient crops, shifts or reverses the wrong complete top/bottom endpoint: " + purpose);
        Require(image.texture.filterMode == FilterMode.Point && image.texture.wrapModeU == TextureWrapMode.Repeat
            && image.texture.wrapModeV == TextureWrapMode.Clamp, "StaticGradient sampler differs from Point/U-Repeat/V-Clamp: " + purpose);
        Require(image.color == Color.white && !image.raycastTarget, "StaticGradient is tinted or blocks a native pointer: " + purpose);
        float scaleX = ScreenRect(image.rectTransform).width / (image.texture.width * image.uvRect.width);
        float scaleY = ScreenRect(image.rectTransform).height / image.texture.height;
        Require(Mathf.Abs(scaleX - scaleY) < .00001f, "StaticGradient pixels are stretched in the actual viewport: " + purpose);
        report.staticEndpointChecks++;
    }
    static float RectError(Rect a, Rect b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.width - b.width), Mathf.Abs(a.height - b.height));
    static void CheckStaticScrollers(List<GameObject> backgrounds, string purpose)
    {
        foreach (var root in backgrounds)
            foreach (var scroll in root.GetComponentsInChildren<OffsetScrolling>(true))
                Require(!scroll.enabled && !scroll.isActiveAndEnabled, "OffsetScrolling is enabled/restarted during StaticGradient: " + purpose + "/" + root.name);
        report.staticScrollerChecks++;
    }
    static StaticObservation BeginStaticObservation(List<GameObject> backgrounds, string purpose, int index)
    {
        CheckStaticScrollers(backgrounds, purpose + " before");
        return new StaticObservation { purpose = purpose, viewport = Screen.width + "x" + Screen.height, color = Names[index],
            startedUtc = DateTime.UtcNow.ToString("O"), beforeUv = backgrounds.Select(BackgroundImage).Select(image => image.uvRect).ToList(),
            scrollerEnabledBefore = backgrounds.SelectMany(root => root.GetComponentsInChildren<OffsetScrolling>(true)).Select(scroll => scroll.enabled).ToList() };
    }
    static void EndStaticObservation(List<GameObject> backgrounds, StaticObservation item, bool requireUnchanged = true)
    {
        CheckStaticScrollers(backgrounds, item.purpose + " after");
        item.finishedUtc = DateTime.UtcNow.ToString("O");
        item.afterUv = backgrounds.Select(BackgroundImage).Select(image => image.uvRect).ToList();
        item.scrollerEnabledAfter = backgrounds.SelectMany(root => root.GetComponentsInChildren<OffsetScrolling>(true)).Select(scroll => scroll.enabled).ToList();
        Require(item.afterUv.Count == item.beforeUv.Count, "Static observation lost authored background slots.");
        item.maxUvError = item.beforeUv.Zip(item.afterUv, RectError).Max();
        if (requireUnchanged) Require(item.maxUvError < .000001f, "Background UV moved during static " + item.purpose + ": " + item.maxUvError);
        foreach (var image in backgrounds.Where(root => root.activeInHierarchy).Select(BackgroundImage)) CheckStaticContract(image, item.purpose);
        report.staticObservations.Add(item); Save();
    }
    static async UniTask<StaticObservation> CheckStaticWait(List<GameObject> backgrounds, int index, DedicatedCameraConnector connector, bool sampleAnimation)
    {
        var item = BeginStaticObservation(backgrounds, sampleAnimation ? "natural animation and stationary background" : "stationary background after scene reload", index);
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        var animator = connector.FocusingC.AnimationManger.AnimatorRef;
        var uiAnimators = Layer<LowerMainBar>().GetComponentsInChildren<Animator>(true);
        item.uiAnimatorNames = uiAnimators.Select(ui => ui.name).ToList();
        item.uiAnimatorEnabledBefore = uiAnimators.Select(ui => ui.enabled).ToList();
        var startState = animator.GetCurrentAnimatorStateInfo(0);
        item.animatorStateBefore = startState.fullPathHash; item.animatorNormalizedTimeBefore = startState.normalizedTime;
        item.animatorSpeed = animator.speed;
        double started = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - started < .6)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            item.frames++; item.scaledElapsed += Time.deltaTime;
            CheckStaticScrollers(backgrounds, "wait frame " + item.frames);
        }
        item.realtimeElapsed = Time.realtimeSinceStartupAsDouble - started;
        item.uiAnimatorEnabledAfter = uiAnimators.Select(ui => ui != null && ui.enabled).ToList();
        Require(item.uiAnimatorEnabledBefore.SequenceEqual(item.uiAnimatorEnabledAfter) && uiAnimators.Length >= 4, "Static background observation changed the same native UI Animator enabled states.");
        var endState = animator.GetCurrentAnimatorStateInfo(0);
        item.animatorStateAfter = endState.fullPathHash; item.animatorNormalizedTimeAfter = endState.normalizedTime;
        item.modelAnimatorAdvanced = animator.isActiveAndEnabled && animator.runtimeAnimatorController != null && animator.speed > 0 && Time.timeScale > 0
            && (startState.fullPathHash != endState.fullPathHash || Mathf.Abs(endState.normalizedTime - startState.normalizedTime) > .00001f);
        if (sampleAnimation)
        {
            Require(item.frames >= 2 && item.scaledElapsed > 0 && item.modelAnimatorAdvanced,
                "Natural fighter animation stopped while verifying StaticGradient; state=" + startState.fullPathHash + ", time=" + startState.normalizedTime + "->" + endState.normalizedTime + ", speed=" + animator.speed);
            report.modelAnimationChecks++;
        }
        EndStaticObservation(backgrounds, item); return item;
    }
    static async UniTask ValidateStaticSwitches(List<GameObject> backgrounds)
    {
        for (int index = 0; index < 6; index++)
        {
            BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable(); CheckBackground(backgrounds, index);
            CheckStaticContract(BackgroundImage(backgrounds[index]), "six-state activation"); CheckStaticScrollers(backgrounds, "six-state activation");
        }
        var uv = backgrounds.Select(BackgroundImage).Select(image => image.uvRect).ToArray();
        BackGroundPS.target.Off(); await Stable(); Require(backgrounds.All(root => !root.activeSelf), "Static Off retained an active background.");
        CheckStaticScrollers(backgrounds, "Off");
        int initial = Field<int>(BackGroundPS.target, "playingNo");
        for (int step = 1; step <= 6; step++)
        {
            BackGroundPS.target.Next(); await Stable(); int index = (initial + step) % 6; CheckBackground(backgrounds, index);
            CheckStaticContract(BackgroundImage(backgrounds[index]), "Off/Next recovery"); CheckStaticScrollers(backgrounds, "Off/Next recovery");
            Require(RectError(BackgroundImage(backgrounds[index]).uvRect, uv[index]) < .000001f, "Static Next changed its previous full-height UV.");
        }
        BackGroundPS.target.ChangeBGByElement((Element)999); await Stable(); CheckBackground(backgrounds, 5);
        CheckStaticContract(BackgroundImage(backgrounds[5]), "unknown-element fallback"); CheckStaticScrollers(backgrounds, "unknown-element fallback");
    }
    static async UniTask<List<GameObject>> RunStaticCases(List<GameObject> backgrounds, Vector2Int[] viewports, int colorCount, Action rebuildStub)
    {
        Require(backgrounds.Select(BackgroundImage).Select(image => Hash(AssetDatabase.GetAssetPath(image.texture))).Distinct().Count() == 1,
            "StaticGradient six-color layouts do not use byte-identical approved portrait images.");
        foreach (var viewport in viewports)
        {
            SetResolution(viewport); await Stable();
            await Wait(() => Screen.width == viewport.x && Screen.height == viewport.y, 5, "actual static viewport " + viewport);
            report.viewportCases++; await ValidateStaticSwitches(backgrounds);
            for (int index = 0; index < colorCount; index++)
            {
                report.phase = viewport.x + "x" + viewport.y + "/static/" + Names[index]; Save();
                var config = Units.Dic.Values.Where(unit => unit.element == (index == 5 ? Element.redMagic : Elements[index]))
                    .OrderBy(unit => int.TryParse(unit.RECORD_ID, out int number) ? number : int.MaxValue).First();
                selectedId = "offline-preview-" + config.RECORD_ID; nullFocus = index == 5;
                dataAccess.Units.Dic.Clear(); dataAccess.Units.Dic.Add(selectedId, new UnitInfo { id = selectedId, r_id = config.RECORD_ID });
                Require(PreScene.target.trySwitchToStep(MainSceneStep.FrontPage, false), "Static HOME transition was refused.");
                var connector = await HomeReady(); if (nullFocus) PreScene.target.SetFocusingUnit(null);
                CheckBackground(backgrounds, index); await Stable();
                var image = BackgroundImage(backgrounds[index]); CheckStaticContract(image, "initial HOME");
                var item = MakeCase(viewport, index, config, image, connector);
                item.staticWait = await CheckStaticWait(backgrounds, index, connector, true); report.staticWaitChecks++;
                item.screenshot = await Screenshot(viewport, Names[index] + "-home"); report.cases.Add(item); Save();
                Debug.Log("[MenuBackgroundReview] CAPTURE " + item.screenshot);
                if (index == 0 && viewport == viewports[0]) await ValidateStaticGpuPalettes(backgrounds, viewport);
                var nativeReturn = BeginStaticObservation(backgrounds, "native collection selection and Return", index);
                Require(PreScene.target.trySwitchToStep(MainSceneStep.UnitList, true), "Static collection transition was refused.");
                await CollectionReady();
                var icon = Layer<UnitsLayer>().GetComponentsInChildren<HeroIcon>().Single(hero => hero.InstanceID == selectedId);
                await NativeClick(icon.iconButton); Require(PreScene.target.Focusing?.id == selectedId, "Native static fighter selection lost local unit.");
                CheckBackground(backgrounds, index == 5 ? 0 : index);
                Require(ReturnLayer.ReturnMissionList.Count == 1, "Static collection forward transition lost its one return mission.");
                await NativeClick(Field<BOButton>(Layer<ReturnLayer>(), "returnButton")); connector = await HomeReady();
                Require(ReturnLayer.ReturnMissionList.Count == 0 && Layer<ReturnLayer>() == null, "Static native Return retained completed history.");
                EndStaticObservation(backgrounds, nativeReturn); // before any fixture focus correction or image writes
                item.nativeReturnStatic = nativeReturn; report.staticNavigationChecks++;
                if (nullFocus) PreScene.target.SetFocusingUnit(null); CheckBackground(backgrounds, index);
                item.nativeReturn = true; report.navigationCycles++;
                var nativeReload = BeginStaticObservation(backgrounds, "native HOME UI reconstruction", index);
                var previousFront = Layer<FrontLayer>();
                await NativeClick(Field<LowerBarIcon>(Layer<LowerMainBar>(), "playTab").BOButton); connector = await HomeReady();
                Require(Layer<FrontLayer>() != previousFront, "Static native HOME reused the closing layer.");
                EndStaticObservation(backgrounds, nativeReload); // before any fixture focus correction or image writes
                item.nativeReloadStatic = nativeReload; report.staticNavigationChecks++;
                if (nullFocus) PreScene.target.SetFocusingUnit(null); CheckBackground(backgrounds, index);
                item.reloadedScreenshot = await Screenshot(viewport, Names[index] + "-reloaded"); item.nativeReload = true; report.homeReloads++; Save();
            }
            await NativeClick(Field<LowerBarIcon>(Layer<LowerMainBar>(), "fighterTab").BOButton); await CollectionReady();
            Require(ReturnLayer.ReturnMissionList.Count == 0, "Static native fighter tab retained forward history.");
            await NativeClick(Field<LowerBarIcon>(Layer<LowerMainBar>(), "playTab").BOButton); await HomeReady();
            report.nativeTabCycles++;
            backgrounds = await ReloadStaticScene(backgrounds, viewport, colorCount, rebuildStub);
        }
        if (!report.previewOnly) { await ValidateStaticResizeAndRecovery(backgrounds); await ValidateStaticFaultRecovery(backgrounds); }
        report.checks.Add("Static full-height endpoint contract, undistorted common portrait layout, native Return/HOME reconstruction, actual Single scene reload and natural fighter animation verified without fixture UV normalization or scroller disabling in natural observations; separate fault injection verifies bad-UV and restart guards.");
        return backgrounds;
    }
    static async UniTask<List<GameObject>> ReloadStaticScene(List<GameObject> backgrounds, Vector2Int viewport, int colorCount, Action rebuildStub)
    {
        var reload = new StaticSceneReload { scenePath = MenuScene, viewport = viewport.x + "x" + viewport.y, startedUtc = DateTime.UtcNow.ToString("O"),
            beforeUv = backgrounds.Select(BackgroundImage).Select(image => image.uvRect).ToList() };
        var oldPre = PreScene.target; var oldBackgroundManager = BackGroundPS.target;
        ProcessesRunner.Main.currentProcess?.ProcessEnd(); ReturnLayer.Clear(); CallLoader("Clear", new object[] { null }); ProcessesRunner.Main.Clear();
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        // PreScene creates a temporary effects RT but has no release callback.
        // Release only this fixture-owned scene resource after its consumers end.
        var oldEffects = Field<RawImage>(oldPre, "effectBg").texture as RenderTexture;
        if (oldEffects != null) { oldPre.noPostProcessCamera.targetTexture = null; oldEffects.Release(); UnityEngine.Object.Destroy(oldEffects); }
        await SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Single).ToUniTask();
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        Require(PreScene.target != null && PreScene.target != oldPre && BackGroundPS.target != oldBackgroundManager && !PreScene.target.enabled,
            "Actual Single scene reload did not replace/re-isolate MainMenuScene.");
        Require(UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
            "IAP survived actual static Scene reload.");
        reload.productionSceneReplaced = true; reload.serviceIsolationRetained = true;
        rebuildStub(); SetupLocalMenuProcesses();
        backgrounds = Field<List<GameObject>>(BackGroundPS.target, "BCPs"); Require(backgrounds.Count == 6, "Reloaded authored scene lost backgrounds.");
        authoredUvCenters.Clear(); foreach (var image in backgrounds.Select(BackgroundImage)) authoredUvCenters.Add(image, image.uvRect.center);
        Require(PreScene.target.trySwitchToStep(MainSceneStep.FrontPage, false), "Reloaded static HOME transition refused.");
        var connector = await HomeReady(); await ValidateStaticSwitches(backgrounds);
        for (int index = 0; index < colorCount; index++)
        {
            BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable(); CheckBackground(backgrounds, index);
            CheckStaticContract(BackgroundImage(backgrounds[index]), "actual Single scene reload");
            await CheckStaticWait(backgrounds, index, connector, false); report.staticSceneReloadChecks++;
            await Screenshot(viewport, Names[index] + "-scene-reloaded");
        }
        reload.afterUv = backgrounds.Select(BackgroundImage).Select(image => image.uvRect).ToList();
        Require(reload.beforeUv.Zip(reload.afterUv, RectError).Max() < .000001f, "Actual Scene reload changed the complete static gradient UV contract.");
        reload.finishedUtc = DateTime.UtcNow.ToString("O"); report.staticSceneReloads.Add(reload); report.actualSceneReloads++; Save();
        return backgrounds;
    }
    static async UniTask ValidateStaticResizeAndRecovery(List<GameObject> backgrounds)
    {
        foreach (var viewport in new[] { new Vector2Int(834,1194), new Vector2Int(768,1024), new Vector2Int(540,960), new Vector2Int(390,844), new Vector2Int(375,667) })
        {
            SetResolution(viewport); await Stable(); await Wait(() => Screen.width == viewport.x && Screen.height == viewport.y, 5, "static reverse viewport " + viewport);
            for (int index = 0; index < 6; index++)
            {
                BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable(); CheckBackground(backgrounds, index);
                CheckStaticContract(BackgroundImage(backgrounds[index]), "reverse resize " + viewport); CheckStaticScrollers(backgrounds, "reverse resize"); report.resizeChecks++;
            }
        }
        for (int index = 0; index < 6; index++)
        {
            BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable();
            var image = BackgroundImage(backgrounds[index]); var uv = image.uvRect;
            var rect = image.rectTransform; var size = rect.sizeDelta; var position = rect.anchoredPosition;
            try
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0); rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);
                RefreshAspect(image); Require(RectError(image.uvRect, uv) < .000001f, "Static zero rectangle corrupted prior valid UV.");
            }
            finally { rect.sizeDelta = size; rect.anchoredPosition = position; }
            await Stable(); CheckStaticContract(image, "zero rectangle restored"); CheckStaticScrollers(backgrounds, "zero rectangle recovery"); report.zeroRectRecoveryChecks++;
            var texture = image.texture; uv = image.uvRect;
            try { image.texture = null; RefreshAspect(image); await Stable(); Require(RectError(image.uvRect, uv) < .000001f, "Static late texture cleared prior valid UV."); }
            finally { image.texture = texture; }
            await Stable(); CheckStaticContract(image, "late texture restored"); CheckStaticScrollers(backgrounds, "late texture recovery"); report.lateTextureRecoveryChecks++;
        }
    }

    static void ValidateSemantics(List<GameObject> backgrounds)
    {
        for (int i = 0; i < Elements.Length; i++) { BackGroundPS.target.ChangeBGByElement(Elements[i]); CheckBackground(backgrounds, i); report.semanticChecks++; }
        BackGroundPS.target.ChangeBGByElement((Element)999); CheckBackground(backgrounds, 5); report.semanticChecks++;
        BackGroundPS.target.Off(); Require(backgrounds.All(background => !background.activeSelf), "Off did not hide all six roots."); report.semanticChecks++;
        PreScene.target.SetFocusingUnit(null); CheckBackground(backgrounds, 5); report.semanticChecks++;
        // Next owns an independent counter; ChangeBGByElement does not reset it.
        int initial = Field<int>(BackGroundPS.target, "playingNo");
        for (int i = 1; i <= 6; i++) { BackGroundPS.target.Next(); CheckBackground(backgrounds, (initial + i) % 6); report.semanticChecks++; }
        BackGroundPS.target.ChangeBGByElement(Element.Null); CheckBackground(backgrounds, 5);
        report.checks.Add("Six mapped states each select one root; Null and unknown select slot 5; Off selects none; independent Next counter completes a six-state cycle.");
    }

    static void CheckBackground(List<GameObject> backgrounds, int index)
    {
        Require(backgrounds.Count(background => background.activeSelf) == 1 && backgrounds[index].activeInHierarchy,
            "Background mapping/activation differs from expected slot " + index + ".");
        var image = BackgroundImage(backgrounds[index]);
        Require(image.texture != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.texture)) == TextureGuids[index],
            "Actual background texture is missing or mapped to the wrong GUID at slot " + index + ".");
    }

    static Case MakeCase(Vector2Int viewport, int index, UnitConfig config, RawImage image, DedicatedCameraConnector connector)
    {
        var camera = Field<Camera>(connector, "camera");
        var root = connector.FocusingC.WholeT;
        var animator = connector.FocusingC.AnimationManger.AnimatorRef;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        var rect = ScreenRect(image.rectTransform);
        Require(rect.xMin <= 1 && rect.yMin <= 1 && rect.xMax >= Screen.width - 1 && rect.yMax >= Screen.height - 1,
            "Authored background leaves an uncovered screen edge.");
        Require(!image.raycastTarget && (report.staticGradient || image.texture.wrapMode == TextureWrapMode.Repeat),
            "Background blocks UI pointers or does not repeat its scroll texture.");
        Require(LiveCount<FrontLayer>() == 1 && LiveCount<UpperInfoBar>() == 1 && LiveCount<LowerMainBar>() == 1,
            "HOME composition contains missing or duplicate layers.");
        var expected = ExpectedUVSize(image);
        if (!report.baseline) CheckAspect(image);
        string path = AssetDatabase.GetAssetPath(image.texture);
        var item = new Case { viewport = viewport.x + "x" + viewport.y, color = Names[index], element = Elements[index].ToString(), recordId = config.RECORD_ID,
            texturePath = path, textureGuid = TextureGuids[index], textureSha256 = Hash(path), backgroundIndex = index, activeBackgrounds = 1,
            textureWidth = image.texture.width, textureHeight = image.texture.height, backgroundScreenRect = rect, uvRect = image.uvRect,
            expectedUvSize = expected, referenceUvSize = RawReferenceUVSize(image), effectiveReferenceUvSize = EffectiveReferenceUVSize(image),
            authoredUvCenter = authoredUvCenters[image],
            backgroundTint = image.color,
            textureAspect = (float)image.texture.width / image.texture.height,
            rectangleAspect = image.rectTransform.rect.width / image.rectTransform.rect.height,
            backgroundCoversViewport = true, textureRepeat = image.texture.wrapMode == TextureWrapMode.Repeat, sourceRaycastDisabled = true, focusIsNull = PreScene.target.Focusing == null,
            cameraPosition = camera.transform.position, cameraRotation = camera.transform.eulerAngles, cameraSize = camera.orthographicSize,
            modelPosition = root.position, modelRotation = root.eulerAngles, poseHash = state.fullPathHash, poseNormalizedTime = state.normalizedTime,
            rendererViewportArea = ModelViewportArea(connector, camera) };
        if (report.staticGradient)
        {
            CheckStaticContract(image, "case layout");
            item.modelWorldScale = root.lossyScale; item.uiCanvasWorldScale = PosCal.Canvas.transform.lossyScale; item.frontLayerWorldScale = Layer<FrontLayer>().transform.lossyScale;
            Require(item.modelWorldScale.y > 0 && item.uiCanvasWorldScale.y > 0 && item.frontLayerWorldScale.y > 0,
                "Static UV flip inverted the fighter or native UI transform instead of only the background sampling.");
            item.onlyBackgroundSamplingVerticallyFlipped = report.expectedVerticalFlip;
            item.materialPath = AssetDatabase.GetAssetPath(image.material); item.materialSha256 = Hash(item.materialPath); item.shader = image.material.shader.name;
            item.samplerU = image.texture.wrapModeU.ToString(); item.samplerV = image.texture.wrapModeV.ToString(); item.filterMode = image.texture.filterMode.ToString();
            item.sampledNativePixels = new Vector2(image.texture.width * image.uvRect.width, image.texture.height);
            item.screenNativePixelScale = new Vector2(rect.width / item.sampledNativePixels.x, rect.height / item.sampledNativePixels.y);
            Require(Mathf.Abs(item.screenNativePixelScale.x - viewport.y / 1670f) < .00001f && Mathf.Abs(item.screenNativePixelScale.y - viewport.y / 1670f) < .00001f,
                "Static native block scale differs from the independent full-height portrait scale.");
            item.approximateBlockScreenPixelsRange = new Vector2(42, 44) * item.screenNativePixelScale.y;
        }
        if (report.unitWidth > 0)
        {
            var tile = report.nativeTileChecks.Single(check => check.color == Names[index]);
            Require(image.texture.width == tile.width && image.texture.height == tile.height,
                "Imported texture dimensions differ from the decoded PNG; cannot claim exact pixel-cell density.");
            item.unitSize = tile.unitSize; item.unitWidth = tile.unitWidth; item.unitHeight = tile.unitHeight;
            item.cellRows = tile.rows; item.cellColumns = tile.columns;
            item.nativeTileCheckIndex = index; item.unitRgbaSha256 = tile.unitRgbaSha256;
            item.visibleCellsX = tile.width * image.uvRect.width / tile.unitWidth;
            item.visibleCellsY = tile.height * image.uvRect.height / tile.unitHeight;
            Require(item.visibleCellsX > 0 && item.visibleCellsY > 0, "Visible cell density is not positive.");
            item.screenCellPixelsX = rect.width / item.visibleCellsX;
            item.screenCellPixelsY = rect.height / item.visibleCellsY;
            Require(Mathf.Abs(item.screenCellPixelsX / tile.unitWidth - item.screenCellPixelsY / tile.unitHeight) < .001f,
                "The native pixel repeat units are stretched in the actual viewport.");
            report.densityChecks++;
        }
        if (report.sourceSet == "Pattern-v7") CheckApprovedScale(image, rect, item);
        return item;
    }
    static void CheckApprovedScale(RawImage image, Rect screenRect, Case item)
    {
        // These constants come from the approved portrait and independently
        // measured native unit, rather than the production component's fields.
        var approvedSpan = new Vector2(941, 1672);
        var approvedUnit = new Vector2(260, 262);
        item.approvedActualSpan = new Vector2(image.texture.width * item.referenceUvSize.x, image.texture.height * item.referenceUvSize.y);
        Require(Vector2.Distance(item.approvedActualSpan, approvedSpan) < .001f,
            "Production reference span differs from the approved 941x1672 portrait: " + item.approvedActualSpan);
        float targetAspect = screenRect.width / screenRect.height;
        float approvedAspect = approvedSpan.x / approvedSpan.y;
        item.approvedExpectedSampledPixels = targetAspect > approvedAspect
            ? new Vector2(approvedSpan.x, approvedSpan.x / targetAspect)
            : new Vector2(approvedSpan.y * targetAspect, approvedSpan.y);
        var actualSampledPixels = new Vector2(image.texture.width * image.uvRect.width, image.texture.height * image.uvRect.height);
        Require(Vector2.Distance(actualSampledPixels, item.approvedExpectedSampledPixels) < .002f,
            "Actual viewport sampling differs from the independently approved portrait crop.");
        item.approvedActualVisibleUnits = new Vector2(actualSampledPixels.x / approvedUnit.x, actualSampledPixels.y / approvedUnit.y);
        item.approvedExpectedVisibleUnits = new Vector2(item.approvedExpectedSampledPixels.x / approvedUnit.x, item.approvedExpectedSampledPixels.y / approvedUnit.y);
        item.approvedExpectedScreenNativePixelScale = new Vector2(screenRect.width / item.approvedExpectedSampledPixels.x,
            screenRect.height / item.approvedExpectedSampledPixels.y);
        Require(Vector2.Distance(item.approvedActualVisibleUnits, item.approvedExpectedVisibleUnits) < .00001f,
            "Repeat-unit density differs from the approved portrait's independently measured period.");
        if (report.unitWidth > 0)
        {
            Require(item.unitWidth == 260 && item.unitHeight == 262, "Configured native unit differs from the approved 260x262 period.");
            Require(Vector2.Distance(new Vector2(item.visibleCellsX, item.visibleCellsY), item.approvedExpectedVisibleUnits) < .00001f,
                "Reported cell density differs from the independently approved repeat-unit crop.");
            Require(Vector2.Distance(new Vector2(item.screenCellPixelsX / 260, item.screenCellPixelsY / 262),
                item.approvedExpectedScreenNativePixelScale) < .00001f, "Native pixel scale differs from the approved portrait crop.");
        }
        Require(image.color == Color.white, "The approved color-block source is still multiplied by a non-white RawImage tint.");
        report.approvedScaleChecks++;
    }

    static void ValidateNativeTiles(List<GameObject> backgrounds, int unitWidth, int unitHeight)
    {
        for (int index = 0; index < backgrounds.Count; index++)
        {
            var image = BackgroundImage(backgrounds[index]);
            string path = AssetDatabase.GetAssetPath(image.texture);
            byte[] png = File.ReadAllBytes(path);
            Require(png.Length >= 24 && png.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                "Native tile source is not a PNG: " + path);
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Require(ImageConversion.LoadImage(decoded, png, false), "Unity could not natively decode PNG: " + path);
                int width = decoded.width, height = decoded.height;
                Require(width >= unitWidth && height >= unitHeight && width % unitWidth == 0 && height % unitHeight == 0,
                    "PNG dimensions do not contain whole " + unitWidth + "x" + unitHeight + "-pixel units: " + Names[index] + " " + width + "x" + height);
                var check = new NativeTile { color = Names[index], texturePath = path, pngSha256 = Hash(path),
                    width = width, height = height, unitSize = unitWidth == unitHeight ? unitWidth : 0,
                    unitWidth = unitWidth, unitHeight = unitHeight, rows = height / unitHeight, columns = width / unitWidth,
                    cellByteCount = checked(unitWidth * unitHeight * 4), horizontalEdgesEqual = true, verticalEdgesEqual = true };
                report.nativeTileChecks.Add(check);
                Color32[] pixels = decoded.GetPixels32();
                Require(pixels.Length == width * height, "Native PNG decode returned incomplete pixels.");
                // Horizontal/vertical refer to the scrolling axes: compare the
                // left/right and top/bottom edge bytes respectively.
                for (int y = 0; y < height; y++)
                    check.horizontalEdgesEqual &= SamePixel(pixels[y * width], pixels[y * width + width - 1]);
                for (int x = 0; x < width; x++)
                    check.verticalEdgesEqual &= SamePixel(pixels[x], pixels[(height - 1) * width + x]);
                Require(check.horizontalEdgesEqual && check.verticalEdgesEqual,
                    "Opposite PNG edge pixels differ at " + Names[index] + ".");
                report.nativeEdgeChecks += 2;

                byte[] reference = null;
                var hashes = new HashSet<string>();
                check.everyCellIdentical = true;
                for (int row = 0; row < check.rows; row++)
                for (int column = 0; column < check.columns; column++)
                {
                    var cell = new byte[check.cellByteCount];
                    for (int y = 0; y < unitHeight; y++)
                    for (int x = 0; x < unitWidth; x++)
                    {
                        // Unity GetPixels32 starts at bottom-left. Serialize
                        // top-left RGBA rows to match an ordinary PNG crop.
                        Color32 pixel = pixels[(height - 1 - (row * unitHeight + y)) * width + column * unitWidth + x];
                        int offset = (y * unitWidth + x) * 4;
                        cell[offset] = pixel.r; cell[offset + 1] = pixel.g; cell[offset + 2] = pixel.b; cell[offset + 3] = pixel.a;
                    }
                    string hash = HashBytes(cell); hashes.Add(hash);
                    if (reference == null) { reference = cell; check.unitRgbaSha256 = hash; }
                    else if (!reference.SequenceEqual(cell)) check.everyCellIdentical = false;
                }
                check.uniqueCells = hashes.Count;
                Require(check.everyCellIdentical && check.uniqueCells == 1,
                    "PNG contains differing pixel cells at " + Names[index] + "; uniqueCells=" + check.uniqueCells);
                report.identicalCellChecks++;
                Save();
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }
        report.checks.Add("Six native-decoded PNGs have strictly equal opposite edges and one unique " + unitWidth
            + "x" + unitHeight + " RGBA8 repeat unit; repeat-unit equivalents are distinct from diamond-motif counts. No importer settings or source assets were changed.");
    }
    static bool SamePixel(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    static float ModelViewportArea(DedicatedCameraConnector connector, Camera camera)
    {
        var renderers = connector.FocusingC.WholeT.GetComponentsInChildren<Renderer>()
            .Where(renderer => renderer.enabled && renderer.GetComponent<ParticleSystem>() == null).ToArray();
        Require(renderers.Length > 0, "Loaded HOME model has no visible renderer.");
        var points = renderers.SelectMany(renderer => BoundsCorners(renderer.bounds)).Select(camera.WorldToViewportPoint).ToArray();
        Require(points.All(point => point.z > 0), "HOME model is behind its actual preview camera.");
        float width = points.Max(point => point.x) - points.Min(point => point.x);
        float height = points.Max(point => point.y) - points.Min(point => point.y);
        return width * height;
    }
    static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
    {
        for (int i = 0; i < 8; i++) yield return bounds.center + Vector3.Scale(bounds.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }

    static async UniTask<DedicatedCameraConnector> HomeReady()
    {
        await Wait(() => ProcessesRunner.Main.currentProcess?.Step == MainSceneStep.FrontPage
            && ProcessesRunner.Main.currentProcess.CanEnterOtherProcess() && Layer<FrontLayer>() != null
            && Layer<LowerMainBar>() != null && Layer<ProgressLayer>() == null, 45, "HOME process/model readiness");
        var connector = Field<DedicatedCameraConnector>(Layer<FrontLayer>(), "camConnector");
        await Wait(() => connector.TaskRunningCount == 0 && connector.FocusingC != null, 30, "production HOME preview model");
        return connector;
    }
    static async UniTask CollectionReady()
    {
        await Wait(() => ProcessesRunner.Main.currentProcess?.Step == MainSceneStep.UnitList
            && ProcessesRunner.Main.currentProcess.CanEnterOtherProcess() && Layer<UnitsLayer>() != null
            && Layer<ProgressLayer>() == null, 30, "production collection readiness");
        var connector = Field<DedicatedCameraConnector>(Layer<UnitOptionLayer>(), "_connector");
        await Wait(() => connector.TaskRunningCount == 0 && connector.FocusingC != null, 30, "collection preview model");
        await Stable();
    }

    static async UniTask FreezePreview(DedicatedCameraConnector connector)
    {
        await UniTask.Delay(1300, DelayType.Realtime);
        connector.EnableRotateDirection(false, false); connector.RotateTarget(-30, 0);
        foreach (var animator in connector.FocusingC.WholeT.GetComponentsInChildren<Animator>())
        {
            animator.applyRootMotion = false;
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                var state = animator.GetCurrentAnimatorStateInfo(layer);
                if (state.fullPathHash != 0) animator.Play(state.fullPathHash, layer, 0);
            }
            animator.Update(0); animator.speed = 0;
        }
        foreach (var body in connector.FocusingC.WholeT.GetComponentsInChildren<Rigidbody>())
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
        }
        // Root motion has already advanced while the real model was loading.
        // Reset its presentation root after sampling the identical idle frame.
        connector.FocusingC.WholeT.position = Field<Vector3>(connector, "modelPos");
        foreach (var particles in connector.FocusingC.WholeT.GetComponentsInChildren<ParticleSystem>())
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        await Stable();
        // CameraTreat snaps its actual authored presentation after pose/bounds
        // settle. This does not alter pitch, framing, projection or crop policy.
        var camera = Field<Camera>(connector, "camera");
        typeof(DedicatedCameraConnector).GetMethod("CameraPositionCal", Fields).Invoke(connector, null);
        typeof(DedicatedCameraConnector).GetMethod("CameraTreat", Fields).Invoke(connector, new object[] { camera, true });
        connector.enabled = false;
        await Stable();
    }

    static async UniTask<ScrollSample> CheckScrollAndWrap(Vector2Int viewport, int index, RawImage image, List<GameObject> backgrounds)
    {
        var scroll = image.GetComponent<OffsetScrolling>(); Require(scroll != null, "Authored background scrolling component is missing.");
        var sample = new ScrollSample { configuredDirection = new Vector2(Field<float>(scroll, "_x"), Field<float>(scroll, "_y")),
            configuredSpeed = Field<float>(scroll, "scrollSpeed") };
        sample.configuredUvPerSecond = sample.configuredDirection * sample.configuredSpeed;
        Require(sample.configuredUvPerSecond.sqrMagnitude > 0, "Authored background scrolling speed is zero.");
        // Start and end after the frame's real MonoBehaviour Updates, so the
        // deltaTime samples cover exactly the same frames as OffsetScrolling.
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        var inactive = backgrounds.Where((_, number) => number != index).Select(BackgroundImage).ToArray();
        var beforeInactive = inactive.Select(other => other.uvRect).ToArray();
        var start = image.uvRect;
        sample.startUvPosition = start.position;
        double startClock = Time.timeAsDouble, startRealtime = Time.realtimeSinceStartupAsDouble;
        var allScrollers = backgrounds.Select(BackgroundImage).SelectMany(other => other.GetComponents<OffsetScrolling>()).ToArray();
        // Enable all production scrollers during this check: inactive roots,
        // rather than fixture-disabled components, must prevent their updates.
        foreach (var scroller in allScrollers) scroller.enabled = true;
        try
        {
            do
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                sample.scaledElapsed += Time.deltaTime;
                sample.expectedUvDelta += sample.configuredDirection * Time.deltaTime * sample.configuredSpeed;
                sample.frames++;
            } while (Time.realtimeSinceStartupAsDouble - startRealtime < .2);
            sample.endUvPosition = image.uvRect.position;
            sample.actualUvDelta = sample.endUvPosition - sample.startUvPosition;
            sample.realtimeElapsed = Time.realtimeSinceStartupAsDouble - startRealtime;
            sample.scaledClockElapsed = Time.timeAsDouble - startClock;
        }
        finally { foreach (var scroller in allScrollers) scroller.enabled = false; }
        Require(sample.frames > 0 && sample.scaledElapsed > 0 && image.uvRect.size == start.size,
            "Production scroll sampling had no scaled frame interval or changed the UV size.");
        sample.uvDeltaError = Vector2.Distance(sample.actualUvDelta, sample.expectedUvDelta);
        Require(sample.uvDeltaError < .00001f,
            "Production scroll speed differs from configured direction/speed at " + Names[index]
            + "; frames=" + sample.frames + "; expected=" + sample.expectedUvDelta + "; actual=" + sample.actualUvDelta);
        sample.measuredUvPerSecond = sample.actualUvDelta / sample.scaledElapsed;
        var screenRect = ScreenRect(image.rectTransform);
        sample.patternScreenPixelsPerSecond = -new Vector2(sample.measuredUvPerSecond.x * screenRect.width / start.width,
            sample.measuredUvPerSecond.y * screenRect.height / start.height);
        Require(inactive.Select(other => other.uvRect).SequenceEqual(beforeInactive), "Inactive background UV advanced.");
        report.scrollSpeedChecks++;
        // Phase records exercise both texture boundaries in the actual renderer.
        image.uvRect = new Rect(new Vector2(.99f, .99f), start.size); await Stable();
        sample.wrapBeforeUv = image.uvRect;
        sample.wrapBeforeScreenshot = await Screenshot(viewport, Names[index] + "-wrap-before");
        image.uvRect = new Rect(new Vector2(1.01f, 1.01f), start.size); await Stable();
        sample.wrapAfterUv = image.uvRect;
        sample.wrapAfterScreenshot = await Screenshot(viewport, Names[index] + "-wrap-after");
        CenterUV(image);
        report.scrollingChecks++;
        return sample;
    }

    static async UniTask CaptureDynamicSequence(Vector2Int viewport, RawImage image, List<GameObject> backgrounds)
    {
        report.phase = "dynamic-red/" + viewport.x + "x" + viewport.y; Save();
        var scroll = image.GetComponent<OffsetScrolling>();
        var sequence = new DynamicSequence { viewport = viewport.x + "x" + viewport.y, color = "red",
            configuredDirection = new Vector2(Field<float>(scroll, "_x"), Field<float>(scroll, "_y")),
            configuredSpeed = Field<float>(scroll, "scrollSpeed") };
        report.dynamicSequences.Add(sequence);
        var allScrollers = backgrounds.Select(BackgroundImage).SelectMany(other => other.GetComponents<OffsetScrolling>()).ToArray();
        var inactive = backgrounds.Select(BackgroundImage).Where(other => other != image).ToArray();
        var beforeInactive = inactive.Select(other => other.uvRect).ToArray();
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        sequence.startUvPosition = image.uvRect.position;
        double startedRealtime = Time.realtimeSinceStartupAsDouble, startedClock = Time.timeAsDouble;
        double nextCapture = .2;
        foreach (var scroller in allScrollers) scroller.enabled = true;
        try
        {
            RecordDynamicFrame(viewport, image, sequence, startedRealtime, startedClock);
            do
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                sequence.scaledElapsed += Time.deltaTime;
                sequence.expectedUvDelta += sequence.configuredDirection * Time.deltaTime * sequence.configuredSpeed;
                sequence.updateFrames++;
                sequence.realtimeElapsed = Time.realtimeSinceStartupAsDouble - startedRealtime;
                if (sequence.realtimeElapsed >= nextCapture || sequence.realtimeElapsed >= 7)
                {
                    RecordDynamicFrame(viewport, image, sequence, startedRealtime, startedClock);
                    nextCapture = Math.Floor(sequence.realtimeElapsed / .2) * .2 + .2;
                }
            } while (sequence.realtimeElapsed < 7);
            sequence.endUvPosition = image.uvRect.position;
            sequence.actualUvDelta = sequence.endUvPosition - sequence.startUvPosition;
            sequence.uvDeltaError = Vector2.Distance(sequence.actualUvDelta, sequence.expectedUvDelta);
        }
        finally { foreach (var scroller in allScrollers) scroller.enabled = false; }
        Require(sequence.uvDeltaError < .0001f && sequence.updateFrames > 0,
            "Long dynamic capture changed the authored scrolling speed.");
        Require(inactive.Select(other => other.uvRect).SequenceEqual(beforeInactive), "An inactive background advanced during the dynamic capture.");
        foreach (var frame in sequence.samples)
        {
            await Wait(() => File.Exists(frame.screenshot) && new FileInfo(frame.screenshot).Length > 100, 5, "dynamic screenshot readback");
            byte[] png = File.ReadAllBytes(frame.screenshot);
            int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            Require(width == viewport.x && height == viewport.y, "Dynamic screenshot has the wrong native viewport dimensions.");
            Require(Vector2.Distance(frame.uvRect.position, frame.expectedUvPosition) < .0001f,
                "Dynamic frame UV differs from the actual accumulated scaled-frame motion.");
            report.dynamicFrameChecks++;
        }
        CenterUV(image); Save();
    }

    static void RecordDynamicFrame(Vector2Int viewport, RawImage image, DynamicSequence sequence, double startedRealtime, double startedClock)
    {
        int index = sequence.samples.Count;
        string directory = Path.Combine(Output, viewport.x + "x" + viewport.y, "dynamic-red"); Directory.CreateDirectory(directory);
        string path = Path.GetFullPath(Path.Combine(directory, "red-" + index.ToString("D3") + ".png"));
        Require(!File.Exists(path), "Refuse to overwrite a dynamic capture frame.");
        var frame = new DynamicFrame { index = index, unityFrame = Time.frameCount, utcTime = DateTime.UtcNow.ToString("O"),
            deltaTime = Time.deltaTime, scaledElapsed = sequence.scaledElapsed,
            realtimeElapsed = Time.realtimeSinceStartupAsDouble - startedRealtime, scaledClockElapsed = Time.timeAsDouble - startedClock,
            uvRect = image.uvRect, expectedUvPosition = sequence.startUvPosition + sequence.expectedUvDelta, screenshot = path };
        sequence.samples.Add(frame); report.screenshots.Add(path);
        ScreenCapture.CaptureScreenshot(path); Save();
    }

    static void CenterUV(RawImage image)
    {
        Require(authoredUvCenters.TryGetValue(image, out var center), "Authored background UV phase was not recorded.");
        image.uvRect = new Rect(center - image.uvRect.size * .5f, image.uvRect.size);
    }
    static Vector2 ExpectedUVSize(RawImage image)
    {
        if (report != null && report.staticGradient) return ExpectedStaticUv(image).size;
        float targetAspect = image.rectTransform.rect.width / image.rectTransform.rect.height;
        float textureAspect = (float)image.texture.width / image.texture.height;
        var reference = EffectiveReferenceUVSize(image);
        float sampledAspect = textureAspect * reference.x / reference.y;
        return targetAspect > sampledAspect ? new Vector2(reference.x, reference.y * sampledAspect / targetAspect)
            : new Vector2(reference.x * targetAspect / sampledAspect, reference.y);
    }
    static Vector2 RawReferenceUVSize(RawImage image)
    {
        var component = image.GetComponent("ScrollingBackgroundAspectFill");
        var field = component == null ? null : component.GetType().GetField("referenceUvSize", Fields);
        return field == null ? Vector2.one : (Vector2)field.GetValue(component);
    }
    static Vector2 EffectiveReferenceUVSize(RawImage image)
    {
        var value = RawReferenceUVSize(image);
        return PositiveFinite(value.x) && PositiveFinite(value.y) ? value : Vector2.one;
    }
    static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    static void CheckAspect(RawImage image)
    {
        Require(image.GetComponent("ScrollingBackgroundAspectFill") != null, "Authored background has no aspect-fill component.");
        Vector2 expected = ExpectedUVSize(image);
        Require(Vector2.Distance(image.uvRect.size, expected) < .00005f,
            "Background texture aspect does not match the actual rectangle: " + image.texture.name + "; actual=" + image.uvRect.size + "; expected=" + expected);
        Require(Mathf.Abs((image.texture.width * image.uvRect.width) / (image.texture.height * (report.staticGradient ? Mathf.Abs(image.uvRect.height) : image.uvRect.height))
            - image.rectTransform.rect.width / image.rectTransform.rect.height) < .00005f,
            "Background sampling still stretches its texture.");
        report.aspectChecks++;
    }
    static void RefreshAspect(RawImage image)
    {
        var component = image.GetComponent("ScrollingBackgroundAspectFill");
        Require(component != null, "Authored background aspect component is unavailable.");
        component.GetType().GetMethod("RefreshAspectFill", BindingFlags.Instance | BindingFlags.Public).Invoke(component, null);
    }
    static void SameCenter(RawImage image, Vector2 expected, string purpose)
    {
        Require(Vector2.Distance(image.uvRect.center, expected) < .00005f, "UV scroll center changed during " + purpose);
        report.preservedPhaseChecks++;
    }
    static async UniTask ValidatePhasePreservation(List<GameObject> backgrounds)
    {
        var centers = new Vector2[6];
        for (int index = 0; index < 6; index++)
        {
            BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable(); CheckAspect(BackgroundImage(backgrounds[index]));
            centers[index] = new Vector2(.637f + index * .011f, .719f + index * .013f);
            var image = BackgroundImage(backgrounds[index]); image.uvRect = new Rect(centers[index] - image.uvRect.size * .5f, image.uvRect.size);
        }
        for (int index = 5; index >= 0; index--)
        {
            BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable();
            CheckBackground(backgrounds, index); SameCenter(BackgroundImage(backgrounds[index]), centers[index], "element switch");
        }
        BackGroundPS.target.Off(); await Stable(); Require(backgrounds.All(root => !root.activeSelf), "Off left an aspect-fill background active.");
        BackGroundPS.target.ChangeBGByElement(Element.Null); await Stable();
        SameCenter(BackgroundImage(backgrounds[5]), centers[5], "Off/Null recovery");
        int initial = Field<int>(BackGroundPS.target, "playingNo");
        for (int step = 1; step <= 6; step++)
        {
            BackGroundPS.target.Next(); await Stable(); int index = (initial + step) % 6;
            CheckBackground(backgrounds, index); SameCenter(BackgroundImage(backgrounds[index]), centers[index], "Next cycle");
        }
        BackGroundPS.target.ChangeBGByElement((Element)999); await Stable();
        SameCenter(BackgroundImage(backgrounds[5]), centers[5], "unknown-element fallback");
    }
    static async UniTask ValidateResizeAndRecovery(List<GameObject> backgrounds)
    {
        var centers = backgrounds.Select(BackgroundImage).Select(image => image.uvRect.center).ToArray();
        // Reverse the preceding ascending sizes, include a second iPad shape,
        // and return to the first size. Every root updates when it reactivates.
        foreach (var size in new[] { new Vector2Int(834,1194), new Vector2Int(768,1024), new Vector2Int(540,960), new Vector2Int(390,844), new Vector2Int(375,667) })
        {
            SetResolution(size); await Stable();
            await Wait(() => Screen.width == size.x && Screen.height == size.y, 5, "reverse actual viewport " + size);
            for (int index = 0; index < 6; index++)
            {
                BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable();
                var image = BackgroundImage(backgrounds[index]); CheckBackground(backgrounds, index); CheckAspect(image);
                SameCenter(image, centers[index], "viewport resize " + size);
                var rect = ScreenRect(image.rectTransform);
                Require(rect.xMin <= 1 && rect.yMin <= 1 && rect.xMax >= Screen.width - 1 && rect.yMax >= Screen.height - 1,
                    "Aspect-fill background failed to cover a resized viewport.");
                report.resizeChecks++;
            }
        }
        for (int index = 0; index < 6; index++)
        {
            BackGroundPS.target.ChangeBGByElement(Elements[index]); await Stable();
            var image = BackgroundImage(backgrounds[index]); var uv = image.uvRect;
            var rect = image.rectTransform; var size = rect.sizeDelta; var position = rect.anchoredPosition;
            try
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);
                RefreshAspect(image);
                Require(image.uvRect == uv, "A zero rectangle corrupted the retained UV phase.");
            }
            finally { rect.sizeDelta = size; rect.anchoredPosition = position; }
            await Stable(); RefreshAspect(image); CheckAspect(image); SameCenter(image, uv.center, "zero rectangle recovery");
            report.zeroRectRecoveryChecks++;
            var texture = image.texture; uv = image.uvRect;
            try { image.texture = null; RefreshAspect(image); Require(image.uvRect == uv, "A late texture cleared UV phase."); }
            finally { image.texture = texture; }
            RefreshAspect(image); await Stable(); CheckAspect(image); SameCenter(image, uv.center, "late texture recovery");
            report.lateTextureRecoveryChecks++;
            var component = image.GetComponent("ScrollingBackgroundAspectFill");
            var referenceField = component.GetType().GetField("referenceUvSize", Fields);
            Require(referenceField != null, "Reference UV field is missing from the production aspect-fill component.");
            var originalReference = (Vector2)referenceField.GetValue(component);
            var referenceCenter = image.uvRect.center;
            try
            {
                referenceField.SetValue(component, Vector2.one); RefreshAspect(image); CheckAspect(image);
                SameCenter(image, referenceCenter, "legacy reference one"); report.legacyReferenceChecks++;
                foreach (var invalid in new[] { Vector2.zero, new Vector2(-1, 1), new Vector2(float.NaN, 1), new Vector2(1, float.PositiveInfinity) })
                {
                    referenceField.SetValue(component, invalid); RefreshAspect(image); CheckAspect(image);
                    Require(EffectiveReferenceUVSize(image) == Vector2.one, "Invalid reference UV did not use the legacy fallback window.");
                    SameCenter(image, referenceCenter, "invalid reference fallback"); report.invalidReferenceChecks++;
                }
            }
            finally { referenceField.SetValue(component, originalReference); RefreshAspect(image); }
            CheckAspect(image); SameCenter(image, referenceCenter, "original reference recovery");
            report.invalidReferenceRecoveryChecks++;
        }
        report.checks.Add("Reference-window aspect-fill math, retained scroll center through element/Off/Next switches, reverse phone/tablet resize, zero rectangle, late texture, legacy reference one and invalid-reference fallback/recovery verified against the actual authored RawImages.");
    }

    static async UniTask NativeClick(Button button)
    {
        if (button is BOButton) await Wait(() => !BOButton.AnyProcess, 3, "native button debounce");
        Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Native control is unavailable.");
        Require(EventSystem.current != null, "Authored MainMenuScene EventSystem is missing.");
        var rect = (RectTransform)button.transform;
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        Require(point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height, "Native control is outside the viewport.");
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Native control is blocked by " + (hits.Count == 0 ? "no raycast target" : hits[0].gameObject.name) + ": " + button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        report.pointerClicks++; await Stable();
    }

    static RawImage BackgroundImage(GameObject background) => background.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "BG");
    static Rect ScreenRect(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    static void SetResolution(Vector2Int size)
    {
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        type.GetMethod("SetCustomResolution", Fields).Invoke(EditorWindow.GetWindow(type), new object[] { new Vector2(size.x, size.y), "Menu Background Review" });
    }
    static async UniTask Stable()
    {
        await UniTask.Delay(250, DelayType.Realtime); await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        if (finishing) throw new OperationCanceledException(); Canvas.ForceUpdateCanvases();
    }
    static async UniTask Wait(Func<bool> predicate, double seconds, string purpose)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate())
        {
            if (finishing) throw new OperationCanceledException();
            if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException(purpose);
            await UniTask.Delay(100, DelayType.Realtime);
        }
    }
    static async UniTask<string> Screenshot(Vector2Int viewport, string name)
    {
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        string directory = Path.Combine(Output, viewport.x + "x" + viewport.y); Directory.CreateDirectory(directory);
        string path = Path.GetFullPath(Path.Combine(directory, name + ".png"));
        if (File.Exists(path)) File.Delete(path); ScreenCapture.CaptureScreenshot(path);
        await Wait(() => File.Exists(path) && new FileInfo(path).Length > 100, 5, "native screenshot " + name);
        byte[] png = File.ReadAllBytes(path);
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Require(width == viewport.x && height == viewport.y && width == Screen.width && height == Screen.height,
            "Screenshot dimensions do not match the actual requested viewport.");
        report.screenshots.Add(path); Save(); return path;
    }
    static T Layer<T>() where T : UILayer => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .FirstOrDefault(layer => layer != null && !layer.IsClosing);
    static int LiveCount<T>() where T : UILayer => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .Count(layer => layer != null && !layer.IsClosing);
    static T Field<T>(object target, string name)
    {
        Require(target != null, "Missing reflection target for " + name);
        var field = target.GetType().GetField(name, Fields);
        Require(field != null, "Missing authored field " + name); return (T)field.GetValue(target);
    }
    static object CallLoader(string method, object[] arguments, Type[] types = null)
    {
        var selected = types == null ? Loader.GetMethods(Fields).Single(item => item.Name == method && item.GetParameters().Length == arguments.Length)
            : Loader.GetMethod(method, types);
        return selected.Invoke(null, arguments);
    }
    static string Hash(string path)
    {
        Require(!string.IsNullOrEmpty(path) && File.Exists(path), "Referenced source asset is missing: " + path);
        return HashBytes(File.ReadAllBytes(path));
    }
    static string HashBytes(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Save() { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true)); }

    static void Finish(string error)
    {
        if (finishing) return; finishing = true;
        report ??= new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O"), phase = "startup" };
        if (!string.IsNullOrEmpty(error)) report.errors.Add(error);
        report.passed &= report.errors.Count == 0 && report.complete;
        Save(); SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".RestoreScenes", true);
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Poll; SceneManager.sceneLoaded -= IsolateServices;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        foreach (var pref in new[] { "showUnit", "PLAYFAB_CUSTOM_ID" })
            if (SessionState.GetBool(Key + ".Had." + pref, false)) PlayerPrefs.SetString(pref, SessionState.GetString(Key + ".Pref." + pref, ""));
            else PlayerPrefs.DeleteKey(pref);
        RestoreFilterAutoload();
        Debug.Log("[MenuBackgroundReview] " + (report.passed ? "PASS" : "FAIL") + " " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        SessionState.SetBool(Key + ".Exit", Application.isBatchMode); SessionState.SetInt(Key + ".ExitCode", report.passed ? 0 : 1);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else EditorApplication.delayCall += RestoreScenes;
    }
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (SessionState.GetBool(Key, false)) Finish("Play mode stopped before the review completed.");
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
