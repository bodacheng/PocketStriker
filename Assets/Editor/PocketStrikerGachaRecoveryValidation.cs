using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using mainMenu;
using Cysharp.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using Skill;
using UniRx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real gacha buttons, production purchase-failure handling, popup close and return; no PlayFab requests.</summary>
[InitializeOnLoad]
public static class PocketStrikerGachaRecoveryValidation
{
    const string Output = "Logs/UILayout/GachaRecovery";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string utcTime;
        public string unityVersion;
        public bool currentScenesUnchanged;
        public string scope = "Actual Resources prefabs, GotchaLayer.Setup/GotchaBtn, GotchaFront/GotchaResult.NineTimes with an instance-injected offline PurchaseItem request, real EventSystems pointer clicks, production failure PopupLayer and ReturnLayer.POP. Front and result pages, both currencies at two portrait sizes, duplicate input, close, retry and return are exercised.";
        public string limitation = "Isolated local Editor Play-mode component interaction with runtime destruction and captured errors; not a live account, successful purchase/rewards, network failures or a device test. UpperInfoBar.Setup, reward animation/stone population and live gacha background rendering are omitted; local balances and a background fixture isolate failure recovery.";
        public int casesChecked;
        public int pointerClicks;
        public List<CaseResult> cases = new List<CaseResult>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class CaseResult
    {
        public string device;
        public string currency;
        public string page;
        public bool passed;
        public bool pageRetainedWhilePending;
        public bool controlsPaused;
        public bool duplicatePurchaseBlocked;
        public bool originalComponentsRecovered;
        public bool selectionPreserved;
        public bool warningClosed;
        public bool retryWorked;
        public bool returnWorked;
        public bool synchronousRequestFailureRecovered;
        public int requests;
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
    }

    const string RunKey = "PocketStriker.GachaRecovery.Playmode";
    static bool finishing;
    [Serializable] sealed class SceneRecords { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }

    static PocketStrikerGachaRecoveryValidation()
    {
        if (SessionState.GetBool(RunKey, false)) Attach();
    }

    // Run without -quit: production removal callbacks require Play mode.
    [MenuItem("PocketStriker/Validation/Gacha Failure Recovery")]
    public static void ValidateBatch()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before gacha validation.");
        var records = new SceneRecords();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
        {
            if (!Application.isBatchMode && scene.isLoaded)
                Require(!string.IsNullOrEmpty(scene.path) && !SceneManager.GetSceneByPath(scene.path).isDirty, "Save open scenes before gacha validation.");
            records.scenes.Add(new SceneRecord { path = scene.path, loaded = scene.isLoaded, active = scene.isActive });
        }
        SessionState.SetString(RunKey + ".Scenes", JsonUtility.ToJson(records));
        SessionState.SetString(RunKey + ".Started", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(RunKey + ".Errors", "");
        SessionState.SetBool(RunKey + ".Running", false);
        SessionState.SetBool(RunKey, true); finishing = false;
        Attach();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= CaptureError; Application.logMessageReceived += CaptureError;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }
    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetString(RunKey + ".Errors", SessionState.GetString(RunKey + ".Errors", "") + message + "\n");
    }
    static void Poll()
    {
        if (finishing || !SessionState.GetBool(RunKey, false)) return;
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(RunKey + ".Started", DateTime.UtcNow.ToString("O")))).TotalSeconds > 120)
        { Finish(new Report { errors = new List<string> { "Gacha recovery Play-mode deadline exceeded." } }); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(RunKey + ".Running", false)) return;
        SessionState.SetBool(RunKey + ".Running", true); Run().Forget();
    }
    static async UniTask Run()
    {
        Report report;
        try { report = await ValidateRecovery(); }
        catch (Exception exception) { report = new Report { errors = new List<string> { exception.ToString() } }; }
        await UniTask.Delay(400, DelayType.Realtime);
        Finish(report);
    }
    static void Finish(Report report)
    {
        if (finishing) return; finishing = true;
        var logged = SessionState.GetString(RunKey + ".Errors", "");
        if (!string.IsNullOrWhiteSpace(logged)) report.errors.Add(logged);
        report.passed = report.errors.Count == 0 && report.casesChecked == 8;
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        SessionState.SetBool(RunKey, false);
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Poll;
        var summary = $"[GachaRecovery] {(report.passed ? "PASS" : "FAIL")}: {report.casesChecked}/8 Play-mode component cases, {report.pointerClicks} pointer clicks.";
        if (report.passed) Debug.Log(summary); else Debug.LogError(summary + "\n" + string.Join("\n", report.errors));
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) { EditorApplication.Exit(report.passed ? 0 : 1); return; }
        EditorApplication.delayCall += RestoreScenes;
    }
    static void RestoreScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += RestoreScenes; return; }
        var records = JsonUtility.FromJson<SceneRecords>(SessionState.GetString(RunKey + ".Scenes", "{}"));
        var setup = records.scenes.Where(scene => !string.IsNullOrEmpty(scene.path))
            .Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    public static async UniTask<Report> ValidateRecovery()
    {
        var report = new Report { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var dirty = scenes.Select(scene => scene.isDirty).ToArray();
        var active = SceneManager.GetActiveScene();
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var oldStars = StarsFall.target;
        var oldEventSystem = EventSystem.current;
        var oldDm = Currencies.DiamondCount;
        var oldGd = Currencies.CoinCount;
        var oldReturns = ReturnLayer.ReturnMissionList.ToArray();
        var oldSkillConfigs = SkillConfigTable.SkillConfigRefDic;
        var oldUpdateForms = StoneLevelUpProccessor.UpdateAllStoneForms.ToArray();
        var oldNeedGold = StoneLevelUpProccessor.needGoldWhole;
        var hanger = Loader.GetField("_hanger", StaticPrivate);
        var fullHanger = Loader.GetField("_fullScreenHanger", StaticPrivate);
        var effectBg = Loader.GetField("effectBg", StaticPrivate);
        var oldHanger = hanger.GetValue(null);
        var oldFullHanger = fullHanger.GetValue(null);
        var oldEffectBg = effectBg.GetValue(null);
        var queues = (IList)Loader.GetField("Queues", StaticPrivate).GetValue(null);
        var oldQueues = queues.Cast<object>().ToArray();
        var debounce = typeof(BOButton).GetField("<AnyProcess>k__BackingField", StaticPrivate);
        var oldDebounce = debounce.GetValue(null);
        Directory.CreateDirectory(Output);
        try
        {
            Require(EditorApplication.isPlaying, "Gacha recovery requires its isolated Play-mode fixture.");
            // The loader can recover scene objects by type. Never let a fixture
            // capture or remove a user's already loaded UI layer.
            Require(UnityEngine.Object.FindObjectsByType<UILayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Close scenes with instantiated UI layers before this isolated validation.");
            Currencies.DiamondCount = new ReactiveProperty<int>(10000);
            Currencies.CoinCount = new ReactiveProperty<int>(10000);
            queues.Clear();
            SkillConfigTable.SkillConfigRefDic = new Dictionary<string, SkillConfig>();
            ReturnLayer.ReturnMissionList.Clear();
            effectBg.SetValue(null, null);
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(390, 844) })
            foreach (var currency in new[] { "GD", "DM" })
            foreach (var repeat in new[] { false, true })
            {
                var result = new CaseResult { device = size.x + "x" + size.y, currency = currency, page = repeat ? "result" : "front" };
                report.cases.Add(result);
                try { await CheckCase(size, currency, repeat, result, report, debounce); }
                catch (Exception exception) { result.errors.Add(exception.ToString()); }
                result.passed = result.errors.Count == 0;
                report.errors.AddRange(result.errors.Select(error => result.device + "/" + currency + "/" + result.page + ": " + error));
                report.casesChecked++;
            }
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            queues.Clear();
            foreach (var item in oldQueues) queues.Add(item);
            hanger.SetValue(null, oldHanger); fullHanger.SetValue(null, oldFullHanger); effectBg.SetValue(null, oldEffectBg);
            ReturnLayer.ReturnMissionList.Clear(); ReturnLayer.ReturnMissionList.AddRange(oldReturns);
            SkillConfigTable.SkillConfigRefDic = oldSkillConfigs;
            StoneLevelUpProccessor.UpdateAllStoneForms.Clear(); StoneLevelUpProccessor.UpdateAllStoneForms.AddRange(oldUpdateForms);
            StoneLevelUpProccessor.needGoldWhole = oldNeedGold;
            PosCal.Canvas = oldCanvas; PosCal.SafeAreaRect = oldSafe; StarsFall.target = oldStars;
            if (oldEventSystem != null && oldEventSystem.isActiveAndEnabled) EventSystem.current = oldEventSystem;
            debounce.SetValue(null, oldDebounce);
            if (Currencies.DiamondCount != oldDm) Currencies.DiamondCount.Dispose();
            if (Currencies.CoinCount != oldGd) Currencies.CoinCount.Dispose();
            Currencies.DiamondCount = oldDm; Currencies.CoinCount = oldGd;
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            report.currentScenesUnchanged = SceneManager.sceneCount == scenes.Length
                && scenes.Select((scene, index) => scene.IsValid() && scene.isDirty == dirty[index]).All(value => value);
            if (!report.currentScenesUnchanged) report.errors.Add("Open scenes or their dirty state changed.");
            report.passed = report.errors.Count == 0 && report.casesChecked == 8;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static async UniTask CheckCase(Vector2Int size, string currency, bool repeat, CaseResult result, Report report, FieldInfo debounce)
    {
        var scene = SceneManager.CreateScene("Gacha Fixture " + Guid.NewGuid());
        RenderTexture texture = null;
        try
        {
            var rig = new GameObject("Offline Gacha Recovery");
            SceneManager.MoveGameObjectToScene(rig, scene);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.scene = scene; camera.orthographic = true;
            camera.nearClipPlane = 0.01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.04f, 0.055f, 0.085f, 1);
            texture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            texture.Create(); camera.targetTexture = texture;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvas.scaleFactor = Mathf.Min(size.x / 1080f, size.y / 1920f);
            var safe = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvas.transform, false); safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.Canvas = canvas; PosCal.SafeAreaRect = safe;
            Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new object[] { safe, safe });
            var events = new GameObject("EventSystem", typeof(EventSystem));
            events.transform.SetParent(rig.transform, false); EventSystem.current = events.GetComponent<EventSystem>();
            Canvas.ForceUpdateCanvases();
            // Only MoveNext's two local background objects are needed. No scene,
            // camera animation, service manager or real account is started.
            var starsObject = new GameObject("Local Gacha Background");
            starsObject.SetActive(false);
            starsObject.transform.SetParent(rig.transform, false);
            var stars = starsObject.AddComponent<StarsFall>(); StarsFall.target = stars;
            Set(stars, "_camera", camera);
            foreach (var name in new[] { "dmGotchaBackground", "gdGotchaBackground" })
            {
                var background = new GameObject(name); background.transform.SetParent(starsObject.transform, false); Set(stars, name, background);
            }
            MSceneProcess process = repeat ? (MSceneProcess)new GotchaResult() : new GotchaFront();
            var nine = (Action<string, string, int>)Delegate.CreateDelegate(typeof(Action<string, string, int>), process,
                process.GetType().GetMethod("NineTimes", Private));
            UILayer layer;
            GotchaBtn nativeBtn;
            var selected = -1;
            if (repeat)
            {
                var resultLayer = Load<GotchaResultLayer>();
                layer = resultLayer;
                Set(process, "layer", resultLayer);
                resultLayer.Setup(currency + "Gotcha", nine);
                var gd = Field<GotchaBtn>(resultLayer, "GDGotchaBtn");
                var dm = Field<GotchaBtn>(resultLayer, "DMGotchaBtn");
                gd.gameObject.SetActive(currency == "GD"); dm.gameObject.SetActive(currency == "DM");
                nativeBtn = currency == "GD" ? gd : dm;
                // Reproduce the settled result page's retry controls, without
                // running Addressables effects or populating account rewards.
                Field<Button>(resultLayer, "Skip").gameObject.SetActive(false);
                Field<Button>(resultLayer, "SpeedOnce").gameObject.SetActive(false);
                resultLayer.NineForShow.gameObject.SetActive(true);
                Set(resultLayer, "showFinished", true);
            }
            else
            {
                var frontLayer = Load<GotchaLayer>();
                layer = frontLayer;
                Set(process, "_layer", frontLayer);
                var pages = Field<List<DropTablePage>>(frontLayer, "dropTables");
                selected = pages.FindIndex(page => Field<GotchaBtn>(page, "gotcha") != null
                    && Field<string>(Field<GotchaBtn>(page, "gotcha"), "currencyCode") == currency);
                Require(selected >= 0, "Native gacha page for currency " + currency + " is missing.");
                Set(process, "_startIndex", selected);
                var move = (Action<int, List<DropTablePage>>)Delegate.CreateDelegate(typeof(Action<int, List<DropTablePage>>), process,
                    typeof(GotchaFront).GetMethod("MoveNext", Private));
                frontLayer.Setup(nine, id => { }, move, false);
                nativeBtn = Field<GotchaBtn>(pages[selected], "gotcha");
            }
            var upper = Load<UpperInfoBar>();
            foreach (var text in upper.GetComponentsInChildren<Text>(true).Where(text => text.name.Contains("Coin"))) text.text = "10000";
            var execute = Field<BOButton>(nativeBtn, "executeBtn");
            var expectedItem = Field<string>(nativeBtn, "itemId");
            var returns = 0;
            ReturnLayer.Stack(MainSceneStep.FrontPage, step => { returns++; process.ProcessEnd(); return true; });
            var back = Get<ReturnLayer>();
            Action<PlayFabError> fail = null;
            var requests = 0;
            Action<PurchaseItemRequest, Action<PurchaseItemResult>, Action<PlayFabError>> offlinePurchase = (request, success, error) =>
            {
                requests++; fail = error;
                Require(request.CatalogVersion == "stone" && request.StoreId == "StoneGotcha"
                    && request.VirtualCurrency == currency && request.ItemId == expectedItem
                    && request.Price == Field<int>(nativeBtn, "currencyCount"), "Production request parameters changed.");
            };
            Set(process, "_purchaseItem", offlinePurchase);
            Canvas.ForceUpdateCanvases();
            Render(camera, texture, result, "ready");
            Click(execute, report, debounce);
            Require(requests == 1 && fail != null, "The native summon button did not request a purchase.");
            result.pageRetainedWhilePending = CurrentPage(repeat) == layer && layer.gameObject.activeInHierarchy
                && Get<UpperInfoBar>() == upper && upper.gameObject.activeInHierarchy && back.gameObject.activeInHierarchy;
            Require(result.pageRetainedWhilePending, "Purchase removed the visible page, balances or return control before its result.");
            result.controlsPaused = !execute.IsInteractable() && !Field<BOButton>(back, "returnButton").IsInteractable();
            Require(result.controlsPaused, "The summon or return button is active while a purchase is pending.");
            for (var i = 0; i < 5; i++) Click(execute, report, debounce);
            execute.onClick.Invoke(); // Exercise NineTimes' own duplicate guard as well as Selectable's group guard.
            Click(Field<BOButton>(back, "returnButton"), report, debounce);
            result.duplicatePurchaseBlocked = requests == 1 && returns == 0;
            Require(result.duplicatePurchaseBlocked, "Rapid repeated input issued another purchase or left the page.");
            fail(new PlayFabError { Error = PlayFabErrorCode.ServiceUnavailable, ErrorMessage = "Offline purchase failure. Please try again." });
            result.originalComponentsRecovered = CurrentPage(repeat) == layer && !layer.IsClosing
                && Get<UpperInfoBar>() == upper && !upper.IsClosing && back.gameObject.activeInHierarchy
                && execute.IsInteractable() && Field<BOButton>(back, "returnButton").IsInteractable()
                && !Field<bool>(process, repeat ? "processingGotcha" : "_processingGotcha");
            Require(result.originalComponentsRecovered, "The production failure callback did not restore the original page and controls.");
            result.selectionPreserved = nativeBtn.gameObject.activeInHierarchy && (repeat
                ? Field<string>(layer, "gotchaId") == expectedItem : Field<int>(process, "_startIndex") == selected);
            Require(result.selectionPreserved, "Failure changed the selected summon category.");
            var warning = Get<PopupLayer>();
            Require(warning != null && Field<Text>(warning, "ValidationIntro").text.Contains("Offline purchase failure"),
                "The production warning did not show the purchase failure message.");
            Render(camera, texture, result, "failure-warning");
            Click(Field<BOButton>(warning, "YesButton"), report, debounce);
            result.warningClosed = Get<PopupLayer>() == null;
            Require(result.warningClosed, "The native warning close button did not close the popup.");
            Render(camera, texture, result, "recovered");
            Click(execute, report, debounce);
            Require(requests == 2, "The summon button cannot retry after closing the failure warning.");
            fail(new PlayFabError { Error = PlayFabErrorCode.ServiceUnavailable, ErrorMessage = "Offline retry failure." });
            Click(Field<BOButton>(Get<PopupLayer>(), "YesButton"), report, debounce);
            result.retryWorked = requests == 2 && execute.IsInteractable() && CurrentPage(repeat) == layer;
            Require(result.retryWorked, "The second failure left the native summon button unusable.");
            // The client can also reject a request synchronously (for example,
            // without a client session). The same controls must recover then.
            Set(process, "_purchaseItem", (Action<PurchaseItemRequest, Action<PurchaseItemResult>, Action<PlayFabError>>)((request, success, error) =>
            { throw new InvalidOperationException("Offline client request rejection."); }));
            Click(execute, report, debounce);
            result.synchronousRequestFailureRecovered = execute.IsInteractable() && back.gameObject.activeInHierarchy
                && !Field<bool>(process, repeat ? "processingGotcha" : "_processingGotcha") && Get<PopupLayer>() != null;
            Require(result.synchronousRequestFailureRecovered, "A synchronous client error left purchase controls locked.");
            Click(Field<BOButton>(Get<PopupLayer>(), "YesButton"), report, debounce);
            Click(Field<BOButton>(back, "returnButton"), report, debounce);
            result.returnWorked = returns == 1 && ReturnLayer.ReturnMissionList.Count == 0
                && Get<ReturnLayer>() == null && CurrentPage(repeat) == null;
            Require(result.returnWorked, "The recovered native return button did not execute ReturnLayer.POP and close the page.");
            result.requests = requests;
        }
        finally
        {
            ReturnLayer.ReturnMissionList.Clear();
            ((IList)Loader.GetField("Queues", StaticPrivate).GetValue(null)).Clear();
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            await SceneManager.UnloadSceneAsync(scene);
            await UniTask.NextFrame();
        }
    }

    static UILayer CurrentPage(bool repeat) => repeat ? (UILayer)Get<GotchaResultLayer>() : Get<GotchaLayer>();

    static T Load<T>() where T : UILayer => (T)Loader.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == "Load" && method.IsGenericMethodDefinition).MakeGenericMethod(typeof(T))
        .Invoke(null, new object[] { false, null, false });
    static T Get<T>() where T : UILayer => (T)Loader.GetMethod("Get", BindingFlags.Public | BindingFlags.Static)
        .MakeGenericMethod(typeof(T)).Invoke(null, null);
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Click(Button button, Report report, FieldInfo debounce)
    {
        Require(button != null && button.gameObject.activeInHierarchy, "Native pointer target is missing or inactive.");
        // Isolate the global 300ms BOButton debounce so duplicate checks exercise
        // the purchase's own lock. All native button/popup listeners still run.
        debounce.SetValue(null, false);
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        report.pointerClicks++;
    }

    static void Render(Camera camera, RenderTexture target, CaseResult result, string state)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var text in PosCal.Canvas.GetComponentsInChildren<Text>()) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            var path = Path.Combine(Output, $"{result.device}-{result.currency}-{result.page}-{state}.png");
            File.WriteAllBytes(path, image.EncodeToPNG()); result.screenshots.Add(path);
        }
        finally { RenderTexture.active = previous; if (image != null) UnityEngine.Object.DestroyImmediate(image); }
    }
}
