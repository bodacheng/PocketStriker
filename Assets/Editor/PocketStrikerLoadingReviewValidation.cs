using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Offline Play-mode review of real loading prefabs and isolated authored scene transitions.</summary>
[InitializeOnLoad]
public static class PocketStrikerLoadingReviewValidation
{
    const string Key = "PocketStriker.ClientLoadingReview";
    const string StartupScene = "Assets/Scene/ABLoadScene/Scene1.unity";
    const string MenuScene = "Assets/Scene/MainScene/MainMenuScene.unity";
    const string NeutralTextureGuid = "532a8e9fc730c44af96fd53fbe0a41d8";
    const string NeutralMaterialGuid = "b779056d5a7d4bf1b0c3d8380507f433";
    const double MaximumSeconds = 360;
    const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Vector2Int[] Viewports = { new Vector2Int(375, 667), new Vector2Int(390, 844), new Vector2Int(540, 960), new Vector2Int(768, 1024) };
    // The production loader is internal to Assembly-CSharp. Reflection crosses
    // that assembly boundary without changing its visibility or implementation.
    static class UILayerLoader
    {
        static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
        public static T Get<T>() where T : UILayer => (T)Loader.GetMethod("Get").MakeGenericMethod(typeof(T)).Invoke(null, null);
        public static T Load<T>(bool insertToTop, string key, bool fullScreen) where T : UILayer =>
            (T)Loader.GetMethod("Load").MakeGenericMethod(typeof(T)).Invoke(null, new object[] { insertToTop, key, fullScreen });
        public static void Remove<T>() where T : UILayer => Loader.GetMethod("Remove", Type.EmptyTypes).MakeGenericMethod(typeof(T)).Invoke(null, null);
        public static void Clear() => Loader.GetMethod("Clear").Invoke(null, new object[] { null });
        public static void SetHanger(Transform safe, Transform full) => Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new object[] { safe, full });
        public static void SetEffectBg(RectTransform rect) => Loader.GetMethod("SetEffectBg").Invoke(null, new object[] { rect });
    }
    static bool finishing;
    static Report report;
    static string Output => Environment.GetEnvironmentVariable("POCKETSTRIKER_LOADING_REVIEW_OUTPUT") ?? "Logs/DownloadLoadingReview/Loading/Runtime";

    [Serializable] public sealed class Report
    {
        public bool passed, complete, sourceAssetsUnchanged, externalServicesIsolated;
        public string utcTime, finishedUtc, unityVersion, phase, colorSpace;
        public string scope = "Offline Editor Play mode: actual startup and MainMenuScene Single loads, actual Resources UnitInstructionLayer/ProgressLayer, UILayerLoader, PosCal, natural background/text fades and progress tweens. Native EventSystem raycast/down/up/click dispatches the production ReturnLayer.Stack callback; the fixture then performs the requested isolated startup Single scene load. Natural observations never write background UV, disable its scroller, disable its aspect component, freeze animation or change Time.timeScale.";
        public string limitation = "StartUpPresentation.Start and PreScene.Start are disabled before Start; IAP and ads objects are removed before Start. Local CommonSetting and translation CSV replace remote configuration. No battle preparation process, gameplay, login, authenticated return route, real download, account, purchase or advertising runs. The return callback signals a fixture-owned scene transition. Screens are Editor viewport captures, not physical safe-area, mobile GPU or performance measurements.";
        public int viewportCases, startupDownloadCases, instructionCases, closeReopenCases, resizeCases, actualSingleSceneLoads, nativeReturnClicks, grayscaleChecks, textLayoutChecks;
        public int progressAnimationChecks, stationaryFrames, backgroundScrollerChecks, isolatedIAPObjects, isolatedAdsObjects;
        public int longStaticWaitChecks, percentageRangeFitChecks, percentageCompletionChecks;
        public float maximumObservedUvDelta;
        public List<Case> cases = new List<Case>();
        public List<Observation> observations = new List<Observation>();
        public List<SceneTransition> sceneTransitions = new List<SceneTransition>();
        public List<TextMeasurement> textMeasurements = new List<TextMeasurement>();
        public List<PercentageFit> percentageFits = new List<PercentageFit>();
        public List<string> screenshots = new List<string>(), checks = new List<string>(), errors = new List<string>();
    }
    [Serializable] public sealed class Case
    {
        public string viewport, sourcePngSha256, materialPath, shader, filterMode, wrapU, wrapV;
        public string downloadScreenshot, instructionScreenshot, reopenedScreenshot, menuReloadScreenshot, returnedScreenshot;
        public string title, body, progressDescription;
        public bool staticGradient, verticalFlip, backgroundCoversViewport, nativeReturnRequested;
        public Rect expectedUv, observedUv;
        public int textureWidth, textureHeight;
        public List<Color32> edgePixels = new List<Color32>();
        public float maximumGrayscaleChannelDifference;
    }
    [Serializable] public sealed class Observation
    {
        public string viewport, purpose, startedUtc, finishedUtc;
        public bool instruction, progressAdvanced, percentageMatchesSlider, longStaticWait;
        public float startSlider, endSlider, minimumSlider, maximumSlider, maximumUvDelta, elapsedRealtime, requestedRealtime;
        public int startPercentage, endPercentage, percentageChanges;
        public Rect beforeUv, afterUv;
        public List<Frame> frames = new List<Frame>();
    }
    [Serializable] public sealed class Frame
    {
        public int unityFrame, percentage;
        public float elapsedRealtime, slider;
        public Rect uvRect;
        public bool scrollerEnabled;
    }
    [Serializable] public sealed class SceneTransition
    {
        public string beforePath, afterPath, startedUtc, finishedUtc;
        public ulong beforeHandle, afterHandle;
        public bool actualSingleLoad, startupDisabled, preSceneDisabled, iapRemoved, adsRemoved;
    }
    [Serializable] public sealed class TextMeasurement
    {
        public string viewport, objectName, caption, font;
        public int fontSize, renderedVisibleCharacters, completeVisibleCharacters, renderedVertices, completeVertices, cachedVisibleCharacters, cachedVertices;
        public float pixelsPerUnit, preferredHeight, paddedLayoutHeight;
        public Rect rect, generatedGlyphBounds;
        public bool renderedCharactersComplete, renderedGeometryComplete;
    }
    [Serializable] public sealed class PercentageFit
    {
        public string viewport, font, widestCaption, tallestCaption, actualCompleteCaption;
        public int fontSize, numericCaptions;
        public float maximumPreferredWidth, maximumPreferredHeight;
        public Rect actualRow;
        public bool actualCompleteGlyphs, footerOverlapFree;
    }
    [Serializable] sealed class SceneBackup { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }

    static PocketStrikerLoadingReviewValidation()
    {
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        if (SessionState.GetBool(Key, false)) { SuspendFilterAutoload(); Attach(); }
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Key + ".Restore", false))
            EditorApplication.delayCall += RestoreScenes;
    }

    [MenuItem("PocketStriker/Validation/Client Loading Background Review")]
    public static void StartBatch()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start from a stopped editor.");
        if (!Application.isBatchMode)
            for (int index = 0; index < SceneManager.sceneCount; index++)
                Require(!SceneManager.GetSceneAt(index).isDirty, "Save open scenes before review.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Require(settings != null, "Addressables settings are missing.");
        int fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        Require(fast >= 0, "Local Addressables Fast Mode builder is missing.");
        var backup = new SceneBackup();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            backup.scenes.Add(new SceneRecord { path = scene.path, loaded = scene.isLoaded, active = scene.isActive });
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(backup));
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex);
        SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetInt(Key + ".IAP", 0); SessionState.SetInt(Key + ".Ads", 0);
        SessionState.SetBool(Key + ".Running", false); SessionState.SetBool(Key + ".Restore", false);
        var view = GameView();
        var selectedSize = view.GetType().GetProperty("selectedSizeIndex", Fields);
        SessionState.SetInt(Key + ".GameSize", selectedSize != null ? (int)selectedSize.GetValue(view) : -1);
        const string layout = "UserSettings/Layouts/default-6000.dwlt";
        SessionState.SetString(Key + ".Layout", File.Exists(layout) ? Convert.ToBase64String(File.ReadAllBytes(layout)) : "");
        settings.ActivePlayModeDataBuilderIndex = fast;
        finishing = false; report = null; SessionState.SetBool(Key, true);
        SuspendFilterAutoload(); SetResolution(Viewports[0]); Attach();
        EditorSceneManager.OpenScene(StartupScene, OpenSceneMode.Single);
        var startup = UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>();
        Require(startup != null, "Authored startup presentation is missing.");
        startup.enabled = false;
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= CaptureError; Application.logMessageReceived += CaptureError;
        SceneManager.sceneLoaded -= IsolateServices; SceneManager.sceneLoaded += IsolateServices;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }

    static void IsolateServices(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        foreach (var startup in UnityEngine.Object.FindObjectsByType<StartUpPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None)) startup.enabled = false;
        foreach (var pre in UnityEngine.Object.FindObjectsByType<PreScene>(FindObjectsInactive.Include, FindObjectsSortMode.None)) pre.enabled = false;
        foreach (var iap in UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            iap.gameObject.SetActive(false); UnityEngine.Object.Destroy(iap.gameObject);
            SessionState.SetInt(Key + ".IAP", SessionState.GetInt(Key + ".IAP", 0) + 1);
        }
        IAPManager.Target = null;
        foreach (var ads in UnityEngine.Object.FindObjectsByType<AdsInitializer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ads.gameObject.SetActive(false); UnityEngine.Object.Destroy(ads.gameObject);
            SessionState.SetInt(Key + ".Ads", SessionState.GetInt(Key + ".Ads", 0) + 1);
        }
        // Hardware input is isolated; native EventSystem raycasts/events below
        // still exercise the real GraphicRaycaster and BOButton callbacks.
        foreach (var module in UnityEngine.Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None)) module.enabled = false;
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string existing = SessionState.GetString(Key + ".Errors", "");
        if (existing.Length < 18000) SessionState.SetString(Key + ".Errors", existing + message + "\n" + stack + "\n");
    }

    static void Poll()
    {
        if (finishing || !SessionState.GetBool(Key, false)) return;
        string errors = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(errors)) { Finish(errors); return; }
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Started", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > MaximumSeconds)
        { Finish("Loading review exceeded its 360-second deadline: " + report?.phase); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        SessionState.SetBool(Key + ".Running", true); Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
            colorSpace = QualitySettings.activeColorSpace.ToString(), phase = "local-configuration" };
        Directory.CreateDirectory(Output);
        var oldSetting = AppSetting.Value;
        var oldRandom = UnityEngine.Random.state;
        var commonFields = typeof(CommonSetting).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => !field.IsInitOnly).ToArray();
        var oldCommon = commonFields.Select(field => field.GetValue(null)).ToArray();
        var oldDurations = CommonSetting.CharacterAnimDuration.ToArray();
        var oldRows = Translate.GetRowList().ToArray();
        var oldLanguageProvider = typeof(Translate).GetField("languageProvider", Fields).GetValue(null);
        var oldTranslationLoaded = typeof(Translate).GetField("isLoaded", Fields).GetValue(null);
        var tipMap = (IDictionary<string, string>)typeof(Translate).GetField("GameTipsRecordIds", Fields).GetValue(null);
        var oldTips = tipMap.ToArray();
        var oldPosCanvas = PosCal.Canvas; var oldSafe = PosCal.SafeAreaRect;
        var hashes = ProtectedPaths().ToDictionary(path => path, Hash);
        try
        {
            await UniTask.NextFrame();
            Require(!PlayFab.PlayFabClientAPI.IsClientLoggedIn(), "A real account is logged in; refuse the offline review.");
            AppSetting.Value = new AppSetting { Language = SystemLanguage.Chinese };
            var common = AssetDatabase.LoadAssetAtPath<CommonSetting>("Assets/Setting/CommonSetting.asset");
            Require(common != null, "Bundled CommonSetting is missing."); common.Initialise();
            await Translate.LoadLanguageCodes(_ => UniTask.FromResult(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ExternalAssets/Config/LanguageCode.csv")));
            Require(!string.IsNullOrEmpty(Translate.Get("LoadingBattle")), "Bundled loading translation is missing.");
            UnityEngine.Random.InitState(137);
            BindScene();
            foreach (var viewport in Viewports)
            {
                report.phase = viewport.x + "x" + viewport.y; Save();
                SetResolution(viewport); await SettleViewport(viewport); BindScene();
                var item = new Case { viewport = viewport.x + "x" + viewport.y,
                    sourcePngSha256 = Hash(AssetDatabase.GUIDToAssetPath(NeutralTextureGuid)) };
                report.cases.Add(item); report.viewportCases++;
                await CheckStartupDownload(viewport, item);
                var instruction = OpenInstruction();
                await Stable(); CheckInstruction(instruction, viewport, item);
                await CheckPercentageRange(viewport);
                await Observe(viewport, "initial-battle-loading", instruction, true);
                item.instructionScreenshot = await Screenshot(viewport, "instruction-static");
                CheckGrayscale(item.instructionScreenshot, item);
                report.instructionCases++;

                // Resize the same live hierarchy; no UV or scroller fixture writes.
                var resized = viewport == Viewports[3] ? Viewports[0] : Viewports[3];
                SetResolution(resized); await SettleViewport(resized);
                CheckInstruction(instruction, resized, null);
                await Observe(resized, "live-resize", instruction, false); report.resizeCases++;
                SetResolution(viewport); await SettleViewport(viewport);
                CheckInstruction(instruction, viewport, null);

                var previous = instruction;
                await CloseLayers();
                Require(previous == null, "Instruction was not destroyed after the production loader removed it.");
                instruction = OpenInstruction(); await Stable();
                Require(instruction != previous, "Reopen reused a destroyed loading layer.");
                CheckInstruction(instruction, viewport, null);
                await Observe(viewport, "close-destroy-reopen", instruction, true);
                item.reopenedScreenshot = await Screenshot(viewport, "instruction-reopened");
                report.closeReopenCases++;

                await CloseLayers();
                await LoadScene(MenuScene);
                instruction = OpenInstruction(); await Stable(); CheckInstruction(instruction, viewport, null);
                await Observe(viewport, "actual-mainmenu-single-load", instruction, true);
                item.menuReloadScreenshot = await Screenshot(viewport, "instruction-scene-reloaded");
                await CloseLayers();
                bool returnRequested = false;
                ReturnLayer.Stack(MainSceneStep.FrontPage, step => { returnRequested = step == MainSceneStep.FrontPage; return true; });
                await Stable();
                var returnLayer = UILayerLoader.Get<ReturnLayer>();
                Require(returnLayer != null && ReturnLayer.ReturnMissionList.Count == 1, "Production return mission was not created.");
                await NativeClick(Field<BOButton>(returnLayer, "returnButton"));
                Require(returnRequested && ReturnLayer.ReturnMissionList.Count == 0 && UILayerLoader.Get<ReturnLayer>() == null,
                    "Native Return did not invoke and consume the production callback.");
                item.nativeReturnRequested = true; report.nativeReturnClicks++;
                await LoadScene(StartupScene);
                instruction = OpenInstruction(); await Stable(); CheckInstruction(instruction, viewport, null);
                await Observe(viewport, "return-callback-and-actual-startup-single-load", instruction, true);
                item.returnedScreenshot = await Screenshot(viewport, "instruction-returned-reopened");
                await CloseLayers(); Save();
            }
            report.sourceAssetsUnchanged = hashes.All(pair => Hash(pair.Key) == pair.Value);
            Require(report.sourceAssetsUnchanged, "A production scene, prefab, background image or material changed during the review.");
            report.externalServicesIsolated = ServicesIsolated();
            Require(report.externalServicesIsolated, "External startup services escaped isolation.");
            report.isolatedIAPObjects = SessionState.GetInt(Key + ".IAP", 0);
            report.isolatedAdsObjects = SessionState.GetInt(Key + ".Ads", 0);
            report.checks.Add("Four native portrait viewports; gray/black full-height vertically flipped background; actual Point/V-Clamp sampler; natural waiting and progress advancement; live resize; destroyed layer recreation; eight isolated Single scene loads; four native Return callbacks; startup downloads remain opaque black.");
            report.complete = true;
            report.passed = report.viewportCases == 4 && report.startupDownloadCases == 4 && report.instructionCases == 4
                && report.closeReopenCases == 4 && report.resizeCases == 4 && report.actualSingleSceneLoads == 8
                && report.nativeReturnClicks == 4 && report.grayscaleChecks == 4 && report.progressAnimationChecks == 20
                && report.longStaticWaitChecks == 4 && report.percentageRangeFitChecks == 404 && report.percentageCompletionChecks == 4
                && report.maximumObservedUvDelta <= .000001f && report.errors.Count == 0;
        }
        catch (Exception exception)
        {
            report.errors.Add(exception.GetBaseException().ToString());
            // Preserve the actual failed screen alongside measurements; this
            // does not alter the UI or soften a failed glyph assertion.
            if (!finishing && Application.isPlaying && Screen.width > 0 && Screen.height > 0)
                try { await Screenshot(new Vector2Int(Screen.width, Screen.height), "loading-failure-diagnostic"); }
                catch (Exception captureException) { report.errors.Add("Failure screenshot: " + captureException.GetBaseException().Message); }
        }
        finally
        {
            ProgressLayer.Close(); UILayerLoader.Remove<UnitInstructionLayer>(); ReturnLayer.Clear(); UILayerLoader.Clear();
            ReleaseSceneEffects();
            AppSetting.Value = oldSetting; UnityEngine.Random.state = oldRandom;
            for (int index = 0; index < commonFields.Length; index++) commonFields[index].SetValue(null, oldCommon[index]);
            CommonSetting.CharacterAnimDuration.Clear(); foreach (var pair in oldDurations) CommonSetting.CharacterAnimDuration.Add(pair.Key, pair.Value);
            Translate.GetRowList().Clear(); Translate.GetRowList().AddRange(oldRows);
            typeof(Translate).GetField("languageProvider", Fields).SetValue(null, oldLanguageProvider);
            typeof(Translate).GetField("isLoaded", Fields).SetValue(null, oldTranslationLoaded);
            tipMap.Clear(); foreach (var pair in oldTips) tipMap.Add(pair.Key, pair.Value);
            PosCal.Canvas = oldPosCanvas; PosCal.SafeAreaRect = oldSafe;
            Finish(null);
        }
    }

    static void BindScene()
    {
        var startup = UnityEngine.Object.FindAnyObjectByType<StartUpPresentation>();
        if (startup != null)
        {
            Require(!startup.enabled, "Startup network preparation is enabled.");
            PosCal.Canvas = Field<Canvas>(startup, "canvas");
            PosCal.SafeAreaRect = Field<RectTransform>(startup, "safeAreaRect");
        }
        else
        {
            Require(PreScene.target != null && !PreScene.target.enabled, "Isolated production menu is missing.");
            PosCal.Canvas = Field<Canvas>(PreScene.target, "Canvas");
            PosCal.SafeAreaRect = Field<RectTransform>(PreScene.target, "safeAreaRect");
        }
        Require(PosCal.Canvas != null, "Authored canvas is missing.");
        PosCal.TestIni();
        UILayerLoader.SetHanger(PosCal.SafeAreaRect, PosCal.Canvas.transform);
        UILayerLoader.SetEffectBg(null);
        Canvas.ForceUpdateCanvases();
        Require(EventSystem.current != null && ServicesIsolated(), "Scene UI or service isolation is unavailable.");
    }

    static UnitInstructionLayer OpenInstruction()
    {
        var instruction = UILayerLoader.Load<UnitInstructionLayer>(true, null, true);
        Require(instruction != null, "Actual instruction prefab could not be loaded.");
        instruction.LoadUnitImage();
        var progress = UILayerLoader.Load<ProgressLayer>(true, null, true);
        Require(progress != null, "Actual progress prefab could not be loaded.");
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattle"), .2f, false);
        return instruction;
    }

    static async UniTask CheckStartupDownload(Vector2Int viewport, Case item)
    {
        Require(SceneManager.GetActiveScene().path == StartupScene, "Download presentation is not in the real startup scene.");
        var startupBackground = PosCal.Canvas.GetComponentsInChildren<Image>(true).Single(image => image.name == "StartupBackground");
        Require(startupBackground.color == Color.black && startupBackground.GetComponents<OffsetScrolling>().Length == 0,
            "Authored startup background is not static black.");
        ProgressLayer.Downloading("正在下载资源");
        ProgressLayer.LoadingPercent("正在下载资源", .2f, false); await Stable();
        var progress = UILayerLoader.Get<ProgressLayer>();
        Require(progress != null && Field<Image>(progress, "bigCurtain").color == Color.black
            && Field<Image>(progress, "bigCurtain").raycastTarget, "Downloads lack their opaque black input blocker.");
        Require(progress.GetComponentsInChildren<OffsetScrolling>(true).Length == 0, "Startup progress has a background scroller.");
        await Observe(viewport, "startup-download-opaque-black", null, true);
        item.downloadScreenshot = await Screenshot(viewport, "startup-download-black");
        var pixels = LoadPixels(item.downloadScreenshot);
        try
        {
            foreach (var point in new[] { new Vector2Int(2, 2), new Vector2Int(2, pixels.height - 3), new Vector2Int(pixels.width - 3, pixels.height / 2) })
            {
                var color = (Color32)pixels.GetPixel(point.x, point.y);
                Require(color.r <= 2 && color.g <= 2 && color.b <= 2, "Startup download capture is not black at an unobstructed edge.");
            }
        }
        finally { UnityEngine.Object.Destroy(pixels); }
        report.startupDownloadCases++; await CloseLayers();
    }

    static void CheckInstruction(UnitInstructionLayer instruction, Vector2Int viewport, Case item)
    {
        var image = Field<RawImage>(instruction, "bgImage");
        var aspect = image.GetComponent<ScrollingBackgroundAspectFill>();
        Require(aspect != null && aspect.isActiveAndEnabled && aspect.StaticGradient && aspect.FlipStaticVertically,
            "Loading does not use the production static flipped aspect component.");
        Require(image.texture != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.texture)) == NeutralTextureGuid,
            "Loading changed or lost its existing background texture.");
        Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.material)) == NeutralMaterialGuid
            && image.material.shader.name == "PocketStriker/UI/StaticPixelGradient", "Loading does not use the existing neutral shader/material.");
        Require(image.texture.filterMode == FilterMode.Point && image.texture.wrapModeV == TextureWrapMode.Clamp,
            "Loading background loses its point pixels or wraps vertical endpoints.");
        Require(image.GetComponents<OffsetScrolling>().All(scroller => !scroller.enabled), "Production loading scroller is enabled.");
        float width = (float)viewport.x / viewport.y / ((float)image.texture.width / image.texture.height);
        var expected = new Rect((1 - width) * .5f, 1, width, -1);
        Require(RectError(image.uvRect, expected) <= .00001f, "Loading UV does not preserve the independent full-height vertical flip.");
        var bounds = ScreenRect(image.rectTransform);
        Require(Mathf.Abs(bounds.xMin) <= 1 && Mathf.Abs(bounds.yMin) <= 1 && Mathf.Abs(bounds.xMax - viewport.x) <= 1
            && Mathf.Abs(bounds.yMax - viewport.y) <= 1, "Loading backdrop does not cover the complete viewport.");
        var progress = UILayerLoader.Get<ProgressLayer>();
        var texts = new[] { Field<Text>(instruction, "gameTipTitle"), Field<Text>(instruction, "gameTip"), Field<Text>(progress, "info"), Field<Text>(progress, "percentage") };
        var safe = ScreenRect(PosCal.SafeAreaRect);
        foreach (var text in texts)
        {
            Require(!string.IsNullOrWhiteSpace(text.text) && text.gameObject.activeInHierarchy, "Loading copy is absent.");
            var textBounds = ScreenRect(text.rectTransform);
            Require(Contains(safe, textBounds), "Loading text leaves the safe content bounds: " + text.name);
            CheckTextGeometry(text, viewport);
        }
        var bar = ScreenRect((RectTransform)Field<Slider>(progress, "progressBar").transform);
        Require(Contains(safe, bar), "Progress bar leaves the safe content bounds.");
        for (int index = 0; index < texts.Length; index++)
        {
            Require(!ScreenRect(texts[index].rectTransform).Overlaps(bar), "Loading text overlaps the progress bar.");
            for (int previous = 0; previous < index; previous++)
                Require(!ScreenRect(texts[index].rectTransform).Overlaps(ScreenRect(texts[previous].rectTransform)), "Loading text regions overlap.");
        }
        report.textLayoutChecks++; report.backgroundScrollerChecks++;
        if (item == null) return;
        item.materialPath = AssetDatabase.GetAssetPath(image.material); item.shader = image.material.shader.name;
        item.filterMode = image.texture.filterMode.ToString(); item.wrapU = image.texture.wrapModeU.ToString(); item.wrapV = image.texture.wrapModeV.ToString();
        item.textureWidth = image.texture.width; item.textureHeight = image.texture.height;
        item.staticGradient = aspect.StaticGradient; item.verticalFlip = aspect.FlipStaticVertically;
        item.expectedUv = expected; item.observedUv = image.uvRect; item.backgroundCoversViewport = true;
        item.title = texts[0].text; item.body = texts[1].text; item.progressDescription = texts[2].text;
    }

    static void CheckTextGeometry(Text text, Vector2Int viewport)
    {
        var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
        using (var rendered = new TextGenerator())
        using (var complete = new TextGenerator())
        using (var layout = new TextGenerator())
        {
            bool renderedPopulated = rendered.Populate(text.text, settings);
            var completeSettings = settings;
            completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
            bool completePopulated = complete.Populate(text.text, completeSettings);
            var layoutSettings = text.GetGenerationSettings(new Vector2(text.rectTransform.rect.width, 0));
            layoutSettings.horizontalOverflow = HorizontalWrapMode.Wrap;
            layoutSettings.verticalOverflow = VerticalWrapMode.Overflow;
            // TextHeight is a layout allocator: it rounds this value up and
            // adds four units of padding. Padding is not generated glyph ink
            // and must not be mistaken for clipped text in a fixed-height row.
            float preferred = layout.GetPreferredHeight(text.text, layoutSettings) / text.pixelsPerUnit;
            var vertices = rendered.verts;
            var glyph = vertices.Select(vertex => (Vector2)(vertex.position / text.pixelsPerUnit)).ToArray();
            var measurement = new TextMeasurement
            {
                viewport = viewport.x + "x" + viewport.y, objectName = text.name, caption = text.text,
                font = text.font != null ? text.font.name : "null", fontSize = text.fontSize,
                pixelsPerUnit = text.pixelsPerUnit, rect = text.rectTransform.rect, preferredHeight = preferred,
                paddedLayoutHeight = LoadingScreenLayout.TextHeight(text, text.rectTransform.rect.width),
                renderedVisibleCharacters = rendered.characterCountVisible, completeVisibleCharacters = complete.characterCountVisible,
                renderedVertices = vertices.Count, completeVertices = complete.verts.Count,
                cachedVisibleCharacters = text.cachedTextGenerator.characterCountVisible, cachedVertices = text.cachedTextGenerator.verts.Count,
                renderedCharactersComplete = renderedPopulated && completePopulated && rendered.characterCountVisible == complete.characterCountVisible,
                renderedGeometryComplete = renderedPopulated && completePopulated && vertices.Count == complete.verts.Count
            };
            if (glyph.Length > 0) measurement.generatedGlyphBounds = Rect.MinMaxRect(glyph.Min(point => point.x), glyph.Min(point => point.y), glyph.Max(point => point.x), glyph.Max(point => point.y));
            report.textMeasurements.Add(measurement); Save();
            Require(renderedPopulated && completePopulated && vertices.Count >= 4 && rendered.characterCountVisible > 0,
                "Loading copy generates no visible glyphs: " + text.name);
            Require(measurement.renderedCharactersComplete && measurement.renderedGeometryComplete,
                "Loading copy truncates generated glyphs: " + text.name + "; visible=" + rendered.characterCountVisible
                + "/" + complete.characterCountVisible + "; vertices=" + vertices.Count + "/" + complete.verts.Count);
            Require(measurement.cachedVisibleCharacters == measurement.renderedVisibleCharacters && measurement.cachedVertices == measurement.renderedVertices,
                "Actual uGUI text mesh differs from the complete measured text: " + text.name);
            Require(preferred <= text.rectTransform.rect.height + .6f,
                "Loading copy preferred height exceeds its row: " + text.name + "; preferred=" + preferred
                + "; paddedAllocator=" + measurement.paddedLayoutHeight + "; rectHeight=" + text.rectTransform.rect.height);
        }
    }

    static async UniTask CheckPercentageRange(Vector2Int viewport)
    {
        var progress = UILayerLoader.Get<ProgressLayer>();
        var percentage = Field<Text>(progress, "percentage");
        var fit = new PercentageFit { viewport = viewport.x + "x" + viewport.y, font = percentage.font.name,
            fontSize = percentage.fontSize, actualRow = percentage.rectTransform.rect };
        var settings = percentage.GetGenerationSettings(percentage.rectTransform.rect.size);
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        using (var generator = new TextGenerator())
            for (int value = 0; value <= 100; value++)
            {
                string caption = value + "%";
                float width = generator.GetPreferredWidth(caption, settings) / percentage.pixelsPerUnit;
                float height = generator.GetPreferredHeight(caption, settings) / percentage.pixelsPerUnit;
                if (width > fit.maximumPreferredWidth) { fit.maximumPreferredWidth = width; fit.widestCaption = caption; }
                if (height > fit.maximumPreferredHeight) { fit.maximumPreferredHeight = height; fit.tallestCaption = caption; }
                Require(width <= fit.actualRow.width + .6f && height <= fit.actualRow.height + .6f,
                    "Numeric loading caption does not fit its measured percentage row: " + caption);
                fit.numericCaptions++; report.percentageRangeFitChecks++;
            }
        // Exercise the real visible four-character completion caption, then
        // return through the production progress API for the long observation.
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattle"), 1, false); await Stable();
        Require(percentage.text == "100%", "The actual completion percentage is absent.");
        CheckTextGeometry(percentage, viewport);
        fit.actualCompleteCaption = percentage.text; fit.actualCompleteGlyphs = true;
        var bounds = ScreenRect(percentage.rectTransform);
        var info = ScreenRect(Field<Text>(progress, "info").rectTransform);
        var bar = ScreenRect((RectTransform)Field<Slider>(progress, "progressBar").transform);
        fit.footerOverlapFree = Contains(ScreenRect(PosCal.SafeAreaRect), bounds) && !bounds.Overlaps(info) && !bounds.Overlaps(bar);
        Require(fit.footerOverlapFree, "Measured completion percentage overlaps the footer or leaves the safe area.");
        report.percentageCompletionChecks++; report.percentageFits.Add(fit);
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattle"), .2f, false); await Stable(); Save();
    }

    static async UniTask Observe(Vector2Int viewport, string purpose, UnitInstructionLayer instruction, bool animateProgress)
    {
        var progress = UILayerLoader.Get<ProgressLayer>();
        Require(progress != null, "Missing real progress layer during observation.");
        var slider = Field<Slider>(progress, "progressBar"); var percentage = Field<Text>(progress, "percentage");
        var image = instruction != null ? Field<RawImage>(instruction, "bgImage") : null;
        var observation = new Observation { viewport = viewport.x + "x" + viewport.y, purpose = purpose,
            instruction = instruction != null, startedUtc = DateTime.UtcNow.ToString("O"), startSlider = slider.value,
            minimumSlider = slider.value, maximumSlider = slider.value, startPercentage = Percentage(percentage),
            percentageMatchesSlider = true, beforeUv = image != null ? image.uvRect : default,
            longStaticWait = purpose == "initial-battle-loading", requestedRealtime = purpose == "initial-battle-loading" ? 9 : 1.2f };
        report.observations.Add(observation);
        if (animateProgress) ProgressLayer.LoadingPercent(instruction == null ? "正在下载资源" : Translate.Get("LoadingBattle"), .8f, true);
        double start = Time.realtimeSinceStartupAsDouble;
        int previousPercent = observation.startPercentage;
        do
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate); Require(!finishing, "Review was interrupted.");
            int percent = Percentage(percentage);
            var frame = new Frame { unityFrame = Time.frameCount, elapsedRealtime = (float)(Time.realtimeSinceStartupAsDouble - start),
                slider = slider.value, percentage = percent, uvRect = image != null ? image.uvRect : default };
            if (image != null)
            {
                frame.scrollerEnabled = image.GetComponents<OffsetScrolling>().Any(scroller => scroller.enabled);
                Require(!frame.scrollerEnabled, "Loading scroller restarted during a natural observation.");
                observation.maximumUvDelta = Mathf.Max(observation.maximumUvDelta, RectError(frame.uvRect, observation.beforeUv));
                Require(observation.maximumUvDelta <= .000001f, "Loading background moved during natural frames.");
                report.stationaryFrames++;
            }
            observation.minimumSlider = Mathf.Min(observation.minimumSlider, slider.value);
            observation.maximumSlider = Mathf.Max(observation.maximumSlider, slider.value);
            observation.percentageMatchesSlider &= Mathf.Abs(percent - Mathf.FloorToInt(slider.value * 100)) <= 1;
            if (percent != previousPercent) observation.percentageChanges++;
            previousPercent = percent; observation.frames.Add(frame);
        } while (Time.realtimeSinceStartupAsDouble - start < observation.requestedRealtime);
        observation.elapsedRealtime = (float)(Time.realtimeSinceStartupAsDouble - start);
        observation.endSlider = slider.value; observation.endPercentage = Percentage(percentage);
        observation.afterUv = image != null ? image.uvRect : default; observation.finishedUtc = DateTime.UtcNow.ToString("O");
        report.maximumObservedUvDelta = Mathf.Max(report.maximumObservedUvDelta, observation.maximumUvDelta);
        if (observation.longStaticWait)
        {
            Require(observation.elapsedRealtime >= 9 && image != null, "Initial loading did not complete its nine-second natural static observation.");
            report.longStaticWaitChecks++;
        }
        Require(observation.percentageMatchesSlider, "Progress percentage no longer follows the actual Slider.");
        if (animateProgress)
        {
            observation.progressAdvanced = observation.maximumSlider - observation.minimumSlider > .1f && observation.percentageChanges >= 2
                && Mathf.Abs(observation.endSlider - .8f) < .01f && observation.endPercentage == 80;
            Require(observation.progressAdvanced, "Loading progress did not visibly advance to 80% through the production tween.");
            report.progressAnimationChecks++;
        }
        Save();
    }

    static void CheckGrayscale(string path, Case item)
    {
        var pixels = LoadPixels(path);
        try
        {
            foreach (int x in new[] { 2, pixels.width - 3 })
            foreach (float y in new[] { .01f, .20f, .50f, .80f, .99f })
            {
                var color = (Color32)pixels.GetPixel(x, Mathf.Clamp(Mathf.RoundToInt((pixels.height - 1) * y), 0, pixels.height - 1));
                float difference = Mathf.Max(color.r, Mathf.Max(color.g, color.b)) - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
                item.maximumGrayscaleChannelDifference = Mathf.Max(item.maximumGrayscaleChannelDifference, difference);
                item.edgePixels.Add(color); Require(difference <= 2, "Rendered loading background is colored rather than neutral gray/black.");
            }
            var top = (Color32)pixels.GetPixel(2, pixels.height - 3); var bottom = (Color32)pixels.GetPixel(2, 2);
            Require(top.r <= 6 && bottom.r > top.r + 20, "Loading capture lost the near-black top or neutral lower endpoint.");
        }
        finally { UnityEngine.Object.Destroy(pixels); }
        report.grayscaleChecks++;
    }

    static async UniTask CloseLayers()
    {
        ProgressLayer.Close(); UILayerLoader.Remove<UnitInstructionLayer>(); ReturnLayer.Clear();
        await UniTask.NextFrame(); await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        Require(UILayerLoader.Get<UnitInstructionLayer>() == null && UILayerLoader.Get<ProgressLayer>() == null, "Closed loading UI remained alive.");
    }

    static async UniTask LoadScene(string path)
    {
        var before = SceneManager.GetActiveScene();
        var transition = new SceneTransition { beforePath = before.path, beforeHandle = before.handle.GetRawData(), afterPath = path, startedUtc = DateTime.UtcNow.ToString("O") };
        UILayerLoader.Clear(); ReturnLayer.Clear(); ReleaseSceneEffects();
        await SceneManager.LoadSceneAsync(path, LoadSceneMode.Single).ToUniTask();
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        var after = SceneManager.GetActiveScene();
        transition.afterHandle = after.handle.GetRawData(); transition.actualSingleLoad = after.path == path && after.handle != before.handle && !before.isLoaded;
        transition.startupDisabled = UnityEngine.Object.FindObjectsByType<StartUpPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(startup => !startup.enabled);
        transition.preSceneDisabled = UnityEngine.Object.FindObjectsByType<PreScene>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(pre => !pre.enabled);
        transition.iapRemoved = UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0;
        transition.adsRemoved = UnityEngine.Object.FindObjectsByType<AdsInitializer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0;
        Require(transition.actualSingleLoad && transition.startupDisabled && transition.preSceneDisabled && transition.iapRemoved && transition.adsRemoved,
            "Actual Single scene replacement or pre-Start isolation failed.");
        transition.finishedUtc = DateTime.UtcNow.ToString("O"); report.sceneTransitions.Add(transition); report.actualSingleSceneLoads++;
        BindScene(); await Stable(); Save();
    }

    static async UniTask NativeClick(Button button)
    {
        await Wait(() => !BOButton.AnyProcess, 3, "native button debounce");
        Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Return button is unavailable.");
        var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        Require(point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height, "Return button is outside the viewport.");
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Return button is blocked by " + (hits.Count > 0 ? hits[0].gameObject.name : "no raycast target"));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        await Stable();
    }

    static bool ServicesIsolated() => UnityEngine.Object.FindObjectsByType<StartUpPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(startup => !startup.enabled)
        && UnityEngine.Object.FindObjectsByType<PreScene>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(pre => !pre.enabled)
        && UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0
        && UnityEngine.Object.FindObjectsByType<AdsInitializer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0;

    static void ReleaseSceneEffects()
    {
        var pre = PreScene.target;
        if (pre == null) return;
        var image = Field<RawImage>(pre, "effectBg"); var texture = image.texture as RenderTexture;
        if (texture == null) return;
        pre.noPostProcessCamera.targetTexture = null; image.texture = null;
        texture.Release(); UnityEngine.Object.Destroy(texture);
    }

    static IEnumerable<string> ProtectedPaths()
    {
        yield return StartupScene; yield return MenuScene;
        yield return "Assets/Resources/DummyLayerSystem/UnitInstructionLayer.prefab";
        yield return "Assets/Resources/DummyLayerSystem/ProgressLayer.prefab";
        yield return "Assets/MainSceneSystem/ScrollingBackgroundAspectFill.cs";
        yield return AssetDatabase.GUIDToAssetPath(NeutralMaterialGuid);
        foreach (string guid in new[] { "eeefd2f03ff5f4541bbbef04d1738e92", "e8e966676cb5e46fdb42579bb766cc9d", "b3fe5a1162ddb4abb81010d278b820c7", "ac31607d78a8a4677b2dd1a525c3e5da", "404e72dee7db1419299acd1370911f1a", NeutralTextureGuid })
            yield return AssetDatabase.GUIDToAssetPath(guid);
    }
    static int Percentage(Text text) { Require(int.TryParse(text.text.TrimEnd('%'), out int value), "Invalid visible progress percentage: " + text.text); return value; }
    static float RectError(Rect a, Rect b) => Mathf.Max(Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)), Mathf.Max(Mathf.Abs(a.width - b.width), Mathf.Abs(a.height - b.height)));
    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
    static Rect ScreenRect(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    static EditorWindow GameView() => EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView", true));
    static void SetResolution(Vector2Int viewport) => GameView().GetType().GetMethod("SetCustomResolution", Fields).Invoke(GameView(), new object[] { new Vector2(viewport.x, viewport.y), "PocketStriker Loading Review" });
    static async UniTask SettleViewport(Vector2Int viewport)
    { await Stable(); await Wait(() => Screen.width == viewport.x && Screen.height == viewport.y, 6, "actual viewport " + viewport); Canvas.ForceUpdateCanvases(); await Stable(); }
    static async UniTask Stable()
    { await UniTask.Delay(260, DelayType.Realtime); await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate); Require(!finishing, "Review was interrupted."); Canvas.ForceUpdateCanvases(); }
    static async UniTask Wait(Func<bool> predicate, double seconds, string purpose)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate())
        { Require(!finishing, "Review was interrupted."); if (Time.realtimeSinceStartupAsDouble >= end) throw new TimeoutException(purpose); await UniTask.Delay(70, DelayType.Realtime); }
    }
    static async UniTask<string> Screenshot(Vector2Int viewport, string name)
    {
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        string directory = Path.Combine(Output, viewport.x + "x" + viewport.y); Directory.CreateDirectory(directory);
        string path = Path.GetFullPath(Path.Combine(directory, name + ".png"));
        if (File.Exists(path)) File.Delete(path); ScreenCapture.CaptureScreenshot(path);
        await Wait(() => File.Exists(path) && new FileInfo(path).Length > 100, 6, "native PNG " + name);
        var bytes = File.ReadAllBytes(path);
        int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        Require(width == viewport.x && height == viewport.y && new FileInfo(path).Length < 10 * 1024 * 1024,
            "Actual PNG dimensions or delivery size are incorrect.");
        report.screenshots.Add(path); Save(); return path;
    }
    static Texture2D LoadPixels(string path)
    { var result = new Texture2D(2, 2, TextureFormat.RGBA32, false); Require(result.LoadImage(File.ReadAllBytes(path), false), "Could not read actual screenshot."); return result; }
    static string Hash(string path)
    {
        Require(!string.IsNullOrEmpty(path) && File.Exists(path), "Source file is unavailable: " + path);
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Save() { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true)); }

    static void Finish(string error)
    {
        if (finishing) return; finishing = true;
        report ??= new Report { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, phase = "startup" };
        if (!string.IsNullOrEmpty(error)) report.errors.Add(error);
        report.finishedUtc = DateTime.UtcNow.ToString("O"); report.passed &= report.complete && report.errors.Count == 0;
        Save(); SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Restore", true);
        Application.logMessageReceived -= CaptureError; SceneManager.sceneLoaded -= IsolateServices; EditorApplication.update -= Poll;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        RestoreFilterAutoload();
        Debug.Log("[ClientLoadingReview] " + (report.passed ? "PASS" : "FAIL") + " " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        SessionState.SetBool(Key + ".Exit", Application.isBatchMode); SessionState.SetInt(Key + ".ExitCode", report.passed ? 0 : 1);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else EditorApplication.delayCall += RestoreScenes;
    }
    static void PlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (SessionState.GetBool(Key, false)) Finish("Play mode stopped before the review completed.");
        if (SessionState.GetBool(Key + ".Restore", false)) EditorApplication.delayCall += RestoreScenes;
    }
    static void RestoreScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Key + ".Restore", false);
        var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".Scenes", "{}"));
        var setup = backup?.scenes.Where(scene => !string.IsNullOrEmpty(scene.path)).Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
        if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        int oldSize = SessionState.GetInt(Key + ".GameSize", -1);
        if (oldSize >= 0) GameView().GetType().GetProperty("selectedSizeIndex", Fields)?.SetValue(GameView(), oldSize);
        string layout = SessionState.GetString(Key + ".Layout", "");
        if (!string.IsNullOrEmpty(layout)) File.WriteAllBytes("UserSettings/Layouts/default-6000.dwlt", Convert.FromBase64String(layout));
        if (SessionState.GetBool(Key + ".Exit", false))
        { SessionState.SetBool(Key + ".Exit", false); EditorApplication.Exit(SessionState.GetInt(Key + ".ExitCode", 1)); }
    }
    static void SuspendFilterAutoload()
    {
        if (!SessionState.GetBool(Key + ".FilterSaved", false))
        { SessionState.SetBool(Key + ".FilterOriginal", Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD); SessionState.SetBool(Key + ".FilterSaved", true); }
        Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD = false;
    }
    static void RestoreFilterAutoload()
    {
        if (!SessionState.GetBool(Key + ".FilterSaved", false)) return;
        Crosstales.BWF.EditorUtil.EditorConfig.PREFAB_AUTOLOAD = SessionState.GetBool(Key + ".FilterOriginal", false);
        SessionState.EraseBool(Key + ".FilterSaved"); SessionState.EraseBool(Key + ".FilterOriginal");
    }
}
