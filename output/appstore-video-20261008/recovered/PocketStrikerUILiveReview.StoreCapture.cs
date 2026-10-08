using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Installed only in an isolated /tmp project by install_runner.py.
// Reuses production startup, account login, native pointer navigation and models.
public static partial class PocketStrikerUILiveReview
{
    const string StoreKey = "PocketStriker.StoreCapture";
    static readonly string[] StoreIntKeys = { "auto", "preferAdventureMode", "AutoRotateCamera", "gangbangCountOption", "gangbangPos0", "gangbangPos1", "gangbangPos2", "ResolutionWidth", "ResolutionHeight", "Fullscreen" };
    static readonly string[] StoreStringKeys = { "showUnit" };
    static string StoreLanguage => SessionState.GetString(StoreKey + ".Language", "ja");
    static string StoreOutput => SessionState.GetString(StoreKey + ".Output", "");

    [Serializable] sealed class StoreCaptureManifest
    {
        public string scope = "Editor-current real startup/login/navigation; current source includes changes beyond TestFlight39. Not a physical-device build39 capture.";
        public string sourceManifestPath, sourceManifestSha256, language, unityVersion, utcTime;
        public int requestedWidth, requestedHeight;
        public bool fixtureAccountUsed, simulatedBannerUsed, hiddenComboActivated;
        public bool preferencesRestored, appSettingsFileRestored, scenesRestored;
        public string addressablesPlayMode = "BuildScriptFastMode: local AssetDatabase; reference aa/iOS catalog is not the used runtime catalog";
        public List<StoreResourceLocator> resourceLocators = new List<StoreResourceLocator>();
        public List<StoreTitleLayoutState> titleLayout = new List<StoreTitleLayoutState>();
        public List<StoreImageSource> images = new List<StoreImageSource>();
    }
    [Serializable] sealed class StoreResourceLocator { public string id, type; }
    [Serializable] sealed class StoreTitleLayoutState
    {
        public string stage, utcTime, renderMode;
        public int screenWidth, screenHeight;
        public Rect buttonPixels;
        public Vector2 buttonCenterPixels;
        public float canvasScaleFactor;
        public bool titlePresent, buttonPresent, canvasPresent, buttonActive, buttonInteractable, cameraAvailable, cameraEnabled;
        public Rect cameraPixelRect;
    }
    [Serializable] sealed class StoreImageSource
    {
        public string filename, sha256, capturedUtc, language, scene, process;
        public int width, height;
        public bool actualAccountLogin, physicalDeviceCapture, fixtureAccountUsed, simulatedBannerUsed;
    }
    static StoreCaptureManifest storeManifest;

    // Exactly one language/size per editor process. Environment is intentionally
    // restricted; never reads login IDs, tokens, passwords, account files or prefs.
    public static void StartStoreCapture()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        Require(project.StartsWith("/tmp/", StringComparison.Ordinal) || project.StartsWith("/private/tmp/", StringComparison.Ordinal), "Store capture requires an isolated /tmp project.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before store capture.");
        Require(!BannerSimulation && Environment.GetEnvironmentVariable("POCKETSTRIKER_ASYNC_ICON_LIVE") != "1", "Fixture/simulation flags must be disabled.");
        string language = Environment.GetEnvironmentVariable("POCKETSTRIKER_STORE_LANGUAGE") ?? "ja";
        Require(new[] { "ja", "en", "zh" }.Contains(language), "Language must be ja/en/zh.");
        string device = Environment.GetEnvironmentVariable("POCKETSTRIKER_STORE_DEVICE") ?? "iphone";
        Require(device == "iphone" || device == "ipad", "Device must be iphone/ipad.");
        int width = device == "iphone" ? 1290 : 2048, height = device == "iphone" ? 2796 : 2732;
        string output = Path.GetFullPath(Environment.GetEnvironmentVariable("POCKETSTRIKER_STORE_OUTPUT") ?? Path.Combine(project, "Logs", "StoreCapture", language + "-" + device));
        Require(output.StartsWith("/tmp/", StringComparison.Ordinal) || output.StartsWith("/private/tmp/", StringComparison.Ordinal), "Output must be under /tmp.");
        Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Output directory must be fresh; previous evidence is preserved.");
        Directory.CreateDirectory(output);
        SessionState.SetString(StoreKey + ".Language", language);
        SessionState.SetString(StoreKey + ".Output", output);
        SessionState.SetInt(StoreKey + ".Width", width); SessionState.SetInt(StoreKey + ".Height", height);
        StoreSnapshotState();
        var viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var view = EditorWindow.GetWindow(viewType);
        var selectedSize = viewType.GetProperty("selectedSizeIndex", Fields);
        SessionState.SetInt(StoreKey + ".GameSize", selectedSize != null ? (int)selectedSize.GetValue(view) : -1);
        EditorApplication.playModeStateChanged -= StoreOnPlayModeChanged;
        EditorApplication.playModeStateChanged += StoreOnPlayModeChanged;
        try { Begin("store-" + language + "-" + device, width, height); }
        catch { StoreRestoreState(); throw; }
    }

    static void StoreSnapshotState()
    {
        foreach (var key in StoreIntKeys.Concat(new[] { PlayFabSetting._arenaPointCode }).Where(x => !string.IsNullOrEmpty(x)).Distinct())
        {
            SessionState.SetBool(StoreKey + ".Int." + key + ".Had", PlayerPrefs.HasKey(key));
            SessionState.SetInt(StoreKey + ".Int." + key + ".Value", PlayerPrefs.GetInt(key, 0));
        }
        foreach (var key in StoreStringKeys)
        {
            SessionState.SetBool(StoreKey + ".String." + key + ".Had", PlayerPrefs.HasKey(key));
            SessionState.SetString(StoreKey + ".String." + key + ".Value", PlayerPrefs.GetString(key, ""));
        }
        string settingsPath = Path.Combine(Application.persistentDataPath, "AppSetting.json");
        SessionState.SetString(StoreKey + ".SettingsPath", settingsPath);
        bool had = File.Exists(settingsPath);
        SessionState.SetBool(StoreKey + ".SettingsHad", had);
        SessionState.SetString(StoreKey + ".SettingsBytes", had ? Convert.ToBase64String(File.ReadAllBytes(settingsPath)) : "");
        SessionState.SetInt(StoreKey + ".MemoryLanguage", (int)AppSetting.Value.Language);
        SessionState.SetBool(StoreKey + ".Saved", true);
        SessionState.SetBool(StoreKey + ".Restored", false);
    }

    static void StoreRestoreState()
    {
        if (!SessionState.GetBool(StoreKey + ".Saved", false)) return;
        foreach (var key in StoreIntKeys.Concat(new[] { PlayFabSetting._arenaPointCode }).Where(x => !string.IsNullOrEmpty(x)).Distinct())
        {
            if (SessionState.GetBool(StoreKey + ".Int." + key + ".Had", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(StoreKey + ".Int." + key + ".Value", 0));
            else PlayerPrefs.DeleteKey(key);
        }
        foreach (var key in StoreStringKeys)
        {
            if (SessionState.GetBool(StoreKey + ".String." + key + ".Had", false)) PlayerPrefs.SetString(key, SessionState.GetString(StoreKey + ".String." + key + ".Value", ""));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        string path = SessionState.GetString(StoreKey + ".SettingsPath", "");
        bool settingsRestored;
        if (SessionState.GetBool(StoreKey + ".SettingsHad", false))
        {
            byte[] original = Convert.FromBase64String(SessionState.GetString(StoreKey + ".SettingsBytes", ""));
            File.WriteAllBytes(path, original);
            settingsRestored = File.ReadAllBytes(path).SequenceEqual(original);
        }
        else { if (File.Exists(path)) File.Delete(path); settingsRestored = !File.Exists(path); }
        AppSetting.Value.Language = (SystemLanguage)SessionState.GetInt(StoreKey + ".MemoryLanguage", (int)SystemLanguage.English);
        bool prefsRestored = StoreIntKeys.Concat(new[] { PlayFabSetting._arenaPointCode }).Where(x => !string.IsNullOrEmpty(x)).Distinct().All(key =>
            PlayerPrefs.HasKey(key) == SessionState.GetBool(StoreKey + ".Int." + key + ".Had", false) &&
            (!PlayerPrefs.HasKey(key) || PlayerPrefs.GetInt(key) == SessionState.GetInt(StoreKey + ".Int." + key + ".Value", 0))) &&
            StoreStringKeys.All(key => PlayerPrefs.HasKey(key) == SessionState.GetBool(StoreKey + ".String." + key + ".Had", false) &&
            (!PlayerPrefs.HasKey(key) || PlayerPrefs.GetString(key) == SessionState.GetString(StoreKey + ".String." + key + ".Value", "")));
        if (storeManifest != null) { storeManifest.preferencesRestored = prefsRestored; storeManifest.appSettingsFileRestored = settingsRestored; }
        SessionState.SetBool(StoreKey + ".Restored", prefsRestored && settingsRestored);
        SessionState.SetBool(StoreKey + ".Saved", false);
        SessionState.EraseString(StoreKey + ".SettingsBytes");
        StoreSaveManifest();
    }

    static async UniTask StoreSetLanguage()
    {
        await Home(); await Click(Field<Button>(Layer<UpperInfoBar>(), "settingBtn"));
        await Page<SettingLayer>(MainSceneStep.Setting, 25);
        var setting = Layer<SettingLayer>();
        await Click(Field<Button>(setting, "languageBtn"));
        await Wait(() => Field<RectTransform>(setting, "languagePanel").gameObject.activeInHierarchy, 10, "language panel");
        string button = StoreLanguage == "ja" ? "jpBtn" : StoreLanguage == "en" ? "enBtn" : "chBtn";
        SystemLanguage expected = StoreLanguage == "ja" ? SystemLanguage.Japanese : StoreLanguage == "en" ? SystemLanguage.English : SystemLanguage.Chinese;
        await Click(Field<Button>(setting, button));
        await Wait(() => AppSetting.Value.Language == expected && !Field<bool>(setting, "_languageChanging"), 45, "production language resource reload");
        await Back(MainSceneStep.FrontPage);
    }

    static StoreTitleLayoutState StoreObserveTitleLayout(TitleScreenLayer title, string stage)
    {
        var state = new StoreTitleLayoutState { stage = stage, utcTime = DateTime.UtcNow.ToString("O"), screenWidth = Screen.width, screenHeight = Screen.height };
        if (title == null) return state;
        state.titlePresent = true;
        var button = Field<Button>(title, "touchScreenBtn");
        if (button == null) return state;
        state.buttonPresent = true; state.buttonActive = button.gameObject.activeInHierarchy; state.buttonInteractable = button.IsInteractable();
        var rect = button.transform as RectTransform;
        var parentCanvas = button.GetComponentInParent<Canvas>();
        if (rect == null || parentCanvas == null) return state;
        var canvas = parentCanvas.rootCanvas;
        if (canvas == null) return state;
        state.canvasPresent = true; state.renderMode = canvas.renderMode.ToString(); state.canvasScaleFactor = canvas.scaleFactor;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        state.cameraAvailable = canvas.renderMode == RenderMode.ScreenSpaceOverlay || camera != null;
        state.cameraEnabled = canvas.renderMode == RenderMode.ScreenSpaceOverlay || (camera != null && camera.isActiveAndEnabled);
        state.cameraPixelRect = camera != null ? camera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
        if (!state.cameraAvailable) return state;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var pixels = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
        state.buttonPixels = Rect.MinMaxRect(pixels.Min(p => p.x), pixels.Min(p => p.y), pixels.Max(p => p.x), pixels.Max(p => p.y));
        state.buttonCenterPixels = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        return state;
    }
    static bool StoreTitleInsideViewport(StoreTitleLayoutState state)
    {
        var rect = state.buttonPixels; var center = state.buttonCenterPixels;
        return state.cameraAvailable && rect.width > 1 && rect.height > 1 &&
            !float.IsNaN(center.x) && !float.IsNaN(center.y) && !float.IsInfinity(center.x) && !float.IsInfinity(center.y) &&
            rect.xMin >= -2 && rect.yMin >= -2 && rect.xMax <= Screen.width + 2 && rect.yMax <= Screen.height + 2 &&
            center.x >= 0 && center.y >= 0 && center.x <= Screen.width && center.y <= Screen.height;
    }
    static async UniTask StoreWaitForTitleLayout(TitleScreenLayer title)
    {
        // SetCustomResolution requests the GameView size before Play; native Screen
        // and first Canvas/camera layout can settle on later render/player frames.
        // Observe those separately without moving controls or changing UI resources.
        storeManifest.titleLayout.Add(StoreObserveTitleLayout(title, "before-wait")); StoreSaveManifest();
        await Stable(); Canvas.ForceUpdateCanvases();
        int width = SessionState.GetInt(StoreKey + ".Width", 0), height = SessionState.GetInt(StoreKey + ".Height", 0);
        try { await Wait(() => Screen.width == width && Screen.height == height, 20, "native GameView backbuffer resolution"); }
        catch (TimeoutException)
        {
            storeManifest.titleLayout.Add(StoreObserveTitleLayout(title, "resolution-timeout")); StoreSaveManifest();
            throw new TimeoutException("GameView resolution did not settle: actual=" + Screen.width + "x" + Screen.height + ", requested=" + width + "x" + height + ". No login click dispatched.");
        }
        storeManifest.titleLayout.Add(StoreObserveTitleLayout(title, "resolution-ready")); StoreSaveManifest();
        try
        {
            await Wait(() =>
            {
                Canvas.ForceUpdateCanvases();
                var observed = StoreObserveTitleLayout(title, "probe");
                return observed.buttonActive && observed.buttonInteractable && StoreTitleInsideViewport(observed);
            }, 20, "natural title Canvas/camera/button layout");
        }
        catch (TimeoutException)
        {
            var observed = StoreObserveTitleLayout(title, "layout-timeout");
            storeManifest.titleLayout.Add(observed); StoreSaveManifest();
            throw new TimeoutException("Title did not reach native click conditions: actual=" + Screen.width + "x" + Screen.height + ", button=" + observed.buttonPixels + ", center=" + observed.buttonCenterPixels + ", active=" + observed.buttonActive + ", interactable=" + observed.buttonInteractable + ", cameraAvailable=" + observed.cameraAvailable + ", cameraEnabled=" + observed.cameraEnabled + ", cameraRect=" + observed.cameraPixelRect + ". No login click dispatched.");
        }
        await Stable(); Canvas.ForceUpdateCanvases();
        var final = StoreObserveTitleLayout(title, "click-ready");
        storeManifest.titleLayout.Add(final); StoreSaveManifest();
        Require(Screen.width == width && Screen.height == height && StoreTitleInsideViewport(final), "Title layout changed before native login click.");
        report.width = Screen.width; report.height = Screen.height;
    }

    // Read-only preflight. The actual action remains the original native Click.
    // Stage portraits are separate buttons; a card's center can hit their frame.
    static bool StoreTryVisibleCompletedStage(ArcadeTop page, out StageButton chosen, out Vector2 normalized)
    {
        chosen = null; normalized = Vector2.zero;
        Require(page != null && EventSystem.current != null, "No natural stage page/EventSystem.");
        Canvas.ForceUpdateCanvases();
        var points = new[] {
            new Vector2(.08f, .55f), new Vector2(.12f, .65f), new Vector2(.08f, .8f),
            new Vector2(.15f, .4f), new Vector2(.05f, .2f), new Vector2(.2f, .8f),
            new Vector2(.9f, .15f), new Vector2(.95f, .5f), new Vector2(.9f, .85f),
            new Vector2(.5f, .15f), new Vector2(.5f, .85f)
        };
        foreach (var stage in page.GetComponentsInChildren<StageButton>()
            .Where(stage => stage.StageNo <= PlayerAccountInfo.Me.arcadeProcess && !AdventureModeRules.IsTutorialStage(stage.StageNo.ToString()))
            .OrderByDescending(stage => stage.StageNo))
        {
            var button = stage.Button;
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable()) continue;
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && camera == null) continue;
            foreach (var candidate in points)
            {
                var local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, candidate.x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, candidate.y));
                var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local));
                if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y) ||
                    point.x < 1 || point.y < 1 || point.x > Screen.width - 1 || point.y > Screen.height - 1) continue;
                // Reject points clipped by the existing page's native scrolling/masks.
                if (rect.GetComponentsInParent<ScrollRect>().Any(scroll => scroll.isActiveAndEnabled && scroll.viewport != null &&
                    !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, camera))) continue;
                if (rect.GetComponentsInParent<RectMask2D>().Any(mask => mask.isActiveAndEnabled &&
                    !RectTransformUtility.RectangleContainsScreenPoint(mask.rectTransform, point, camera))) continue;
                if (rect.GetComponentsInParent<Mask>().Any(mask => mask.isActiveAndEnabled &&
                    !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)mask.transform, point, camera))) continue;
                var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button.gameObject) continue;
                chosen = stage; normalized = candidate;
                current.note = "Read-only native raycast preflight chose completed non-tutorial Stage" + stage.StageNo +
                    " at normalized=" + candidate + "; top click handler is the existing StageButton. No portrait/UI/raycast state was changed.";
                return true;
            }
        }
        return false;
    }

    static async UniTask StoreRun()
    {
        report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O"),
            scope = "Five whitelisted Editor screenshots via actual startup, production device login, native navigation and actual loaded models. Current local source, not TestFlight39 device media.",
            limitation = "Editor rendering and local Addressables Fast Mode; no physical-device safe-area/performance evidence. No fixtures, simulated banners, hidden combo activation, purchase, account edit, claim, team or skill edit are dispatched.",
            startupSideEffects = "Production startup can perform normal device-account login/creation, check-in, tutorial reconciliation and team sanitization; the runner does not replace accounts or write synthetic server data." };
        foreach (string name in Inventory) report.cases.Add(new Case { name = name });
        Directory.CreateDirectory(Output);
        cancellation = new System.Threading.CancellationTokenSource();
        string source = Path.GetFullPath("StoreCaptureSourceManifest.json");
        Require(File.Exists(source), "Source provenance manifest is missing.");
        storeManifest = new StoreCaptureManifest { sourceManifestPath = source, sourceManifestSha256 = StoreSha(File.ReadAllBytes(source)),
            language = StoreLanguage, requestedWidth = SessionState.GetInt(StoreKey + ".Width", 0), requestedHeight = SessionState.GetInt(StoreKey + ".Height", 0),
            unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        File.Copy(source, Path.Combine(Output, "source-manifest.json"), true);
        try
        {
            var title = Layer<TitleScreenLayer>(); Require(title != null, "Production title missing.");
            report.loginMode = "Production title device login; no credential values are read or reported";
            await StoreWaitForTitleLayout(title);
            await Click(Field<Button>(title, "touchScreenBtn"));
            await Wait(() => PreScene.target != null && PlayerAccountInfo.Me != null && ProcessesRunner.Main.currentProcess != null, 90, "production login/main scene");
            report.realAccountLogin = true;
            foreach (var locator in Addressables.ResourceLocators)
                storeManifest.resourceLocators.Add(new StoreResourceLocator { id = Sanitize(locator.LocatorId), type = locator.GetType().FullName });
            StoreSaveManifest();
            await DismissStartupModals();
            Require(PlayerAccountInfo.Me.tutorialProgress == "Finished", "Actual account onboarding is unfinished; capture stops without modifying tutorial/account or injecting a fixture.");
            await StoreSetLanguage();
            await Check("home", async () => { await Home(); RequirePreview(Layer<FrontLayer>()); await Screenshot("home"); });
            await Check("skill-editor", async () =>
            {
                await Home(); await Click(Tab("fighterTab")); await Page<UnitOptionLayer>(MainSceneStep.UnitList, 35);
                Require(dataAccess.Units.Dic.Count > 0, "Actual owned collection is empty.");
                await Click(Field<Button>(Layer<UnitOptionLayer>(), "skillEditButton"));
                await Page<SkillEditLayer>(MainSceneStep.UnitSkillEdit, 45);
                Require(Layer<SkillEditLayer>().Initialized, "Skill editor is not initialized.");
                RequirePreview(Layer<SkillEditLayer>());
                var combo = Layer<SkillEditLayer>().transform.Find("middle/V1/combo");
                Require(combo == null || !combo.gameObject.activeInHierarchy, "Unexpected hidden combo activation; stop for source review.");
                await Screenshot("skill-editor");
            });
            await Check("adventure-preparation", async () =>
            {
                await Home(); await Click(Field<LowerBarIcon>(Layer<FrontLayer>(), "ArcadeBtn").BOButton);
                await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 45); RequirePreview(Layer<FightPrepareLayer>());
                await Screenshot("adventure-preparation");
            });
            await Check("random-boss", async () =>
            {
                await Home(); await Click(Field<Button>(Layer<FrontLayer>(), "EventFightBtn"));
                await Page<EventBattleTop>(MainSceneStep.RandomBoss, 45); await Screenshot("random-boss");
            });
            await Check("battle-hud", async () =>
            {
                await Home(); await Click(Field<LowerBarIcon>(Layer<FrontLayer>(), "ArcadeBtn").BOButton);
                await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 45);
                await Click(Field<Button>(Layer<FightPrepareLayer>(), "toArcadeFrontBtn"));
                await Page<ArcadeTop>(MainSceneStep.ArcadeFront, 45);
                StageButton completed; Vector2 stagePointer;
                Require(StoreTryVisibleCompletedStage(Layer<ArcadeTop>(), out completed, out stagePointer),
                    "No visible completed non-tutorial stage with its own native top click handler; no progress/UI/raycast state was changed.");
                await Click(completed.Button, false, stagePointer);
                await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 45);
                Require(FightLoad.Fight != null && FightLoad.Fight.EventType == FightEventType.Quest, "Only a real completed adventure may start; no arena/event ticket spending.");
                await Click(Field<Button>(Field<FightBeginBtn>(Layer<FightPrepareLayer>(), "beginFight"), "btn"));
                await Wait(() => Layer<FightingStepLayer>() != null && Layer<FightingStepLayer>().PauseButton.gameObject.activeInHierarchy &&
                    Layer<FightingStepLayer>().PauseButton.IsInteractable() && Layer<ProgressLayer>() == null, 90, "live adventure HUD");
                Require(!FightLogger.value.GameOver.Value && Layer<CommonFightResult>() == null, "Natural battle already ended; no result/reward action is dispatched.");
                await Screenshot("battle-hud");
                Require(!FightLogger.value.GameOver.Value && Layer<CommonFightResult>() == null && Layer<FightingStepLayer>() != null && Layer<FightingStepLayer>().PauseButton.IsInteractable(), "Natural battle ended before safe pause; no result/reward action is dispatched.");
                await Click(Layer<FightingStepLayer>().PauseButton);
                await Wait(() => Layer<FightScenePauseSupport>() != null && Time.timeScale == 0, 5, "native battle pause");
                await Click(PersistentButton(Layer<FightScenePauseSupport>(), "Return"));
                await Wait(() => SceneManager.GetActiveScene().name == "MainMenuScene" && PreScene.target != null && Ready, 90, "native battle return");
                current.note = "Completed non-tutorial adventure selected through native stage card; no synthetic match, force-win/reward or AUTO preference change. Native pause then return immediately after capture. Normal AI prefetch can start as part of production fight loading.";
            });
        }
        catch (Exception exception) { report.errors.Add(Sanitize(exception.Message) + " | " + Diagnostic()); }
        finally { cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null; Finish(null); }
    }

    static string StoreSha(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static void StoreRecordScreenshot(string path, int width, int height)
    {
        Require(width == SessionState.GetInt(StoreKey + ".Width", 0) && height == SessionState.GetInt(StoreKey + ".Height", 0), "Rendered dimensions differ from the requested ASC dimensions.");
        if (storeManifest == null) return;
        storeManifest.images.Add(new StoreImageSource { filename = Path.GetFileName(path), sha256 = StoreSha(File.ReadAllBytes(path)),
            capturedUtc = DateTime.UtcNow.ToString("O"), language = StoreLanguage, scene = SceneManager.GetActiveScene().name,
            process = ProcessesRunner.Main.currentProcess?.GetType().Name, width = width, height = height, actualAccountLogin = report.realAccountLogin });
        StoreSaveManifest();
    }
    static void StoreSaveManifest()
    { if (storeManifest != null && !string.IsNullOrEmpty(StoreOutput)) File.WriteAllText(Path.Combine(StoreOutput, "capture-manifest.json"), JsonUtility.ToJson(storeManifest, true)); }
    static void StoreReattach()
    {
        if (!SessionState.GetBool(StoreKey + ".Saved", false)) return;
        EditorApplication.playModeStateChanged -= StoreOnPlayModeChanged;
        EditorApplication.playModeStateChanged += StoreOnPlayModeChanged;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.delayCall += StoreCompleteAfterStop;
    }
    static void StoreOnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.delayCall += StoreCompleteAfterStop;
    }
    static void StoreCompleteAfterStop()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(StoreKey + ".Saved", false)) return;
        try
        {
            // Domain reload is enabled. Read the completed sanitized output before
            // restoring state; no in-memory static report is assumed to survive.
            string captures = Path.Combine(StoreOutput, "capture-manifest.json"), summary = Path.Combine(StoreOutput, "report.json");
            if (storeManifest == null && File.Exists(captures)) storeManifest = JsonUtility.FromJson<StoreCaptureManifest>(File.ReadAllText(captures));
            if (report == null && File.Exists(summary)) report = JsonUtility.FromJson<Report>(File.ReadAllText(summary));
            StoreRestoreState();
            RestoreScenes();
            int size = SessionState.GetInt(StoreKey + ".GameSize", -1);
            if (size >= 0)
            {
                var viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
                viewType.GetProperty("selectedSizeIndex", Fields)?.SetValue(EditorWindow.GetWindow(viewType), size);
            }
            if (storeManifest != null) { storeManifest.scenesRestored = !SessionState.GetBool(Key + ".RestoreScenes", false); StoreSaveManifest(); }
            EditorApplication.playModeStateChanged -= StoreOnPlayModeChanged;
            if (Environment.GetEnvironmentVariable("POCKETSTRIKER_STORE_QUIT") == "1")
                EditorApplication.Exit(report != null && report.passed && SessionState.GetBool(StoreKey + ".Restored", false) ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogError("Store capture restoration failed: " + Sanitize(exception.Message)); }
    }
}
