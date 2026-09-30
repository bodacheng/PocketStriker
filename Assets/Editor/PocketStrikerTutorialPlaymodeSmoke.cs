using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using FightScene;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Exercises the real first-quest tutorial caller and animated battle HUD.</summary>
[InitializeOnLoad]
public static class PocketStrikerTutorialPlaymodeSmoke
{
    const string Key = "PocketStriker.TutorialPlaymodeSmoke";
    const string Output = "Logs/Tutorial/Playmode";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool finishing;
    static Report report;

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string scope = "Actual published first quest, FightLoad.Go, Preparing/CountDown/Fighting, production OpenTutorial, live animated HUD and models. Six native tutorial pages, first-page click lock while paused, later pages with active animations, real raycasts, callout targets, force-auto callback and Dream Combo overlay.";
        public string limitation = "Local fixture account and Addressables Fast Mode; shop services removed before Start. Both teams become invulnerable after battle starts to isolate tutorial UI. Battle victory/rewards are covered by the separate combat-flow smoke. Editor Play mode, not a device test.";
        public int pages;
        public int pointerChecks;
        public int raycastChecks;
        public bool forceAuto;
        public bool skillInputRestored;
        public bool dreamOverlay;
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
    }

    static PocketStrikerTutorialPlaymodeSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }

    [MenuItem("PocketStriker/Validation/Tutorial Playmode Smoke")]
    public static void StartBatch()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start from a stopped editor.");
        if (!Application.isBatchMode)
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scenes before tutorial smoke.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        Require(fast >= 0, "Addressables Fast Mode is unavailable.");
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex);
        SessionState.SetBool(Key + ".HadAuto", PlayerPrefs.HasKey("auto"));
        SessionState.SetInt(Key + ".Auto", PlayerPrefs.GetInt("auto", 0));
        SessionState.SetString(Key + ".Start", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key, true);
        settings.ActivePlayModeDataBuilderIndex = fast;
        var gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        gameView.GetMethod("SetCustomResolution", Private).Invoke(EditorWindow.GetWindow(gameView),
            new object[] { new Vector2(540, 960), "PocketStriker Tutorial Smoke" });
        finishing = false;
        Attach();
        EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(scene => scene.enabled).path);
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= CaptureError; Application.logMessageReceived += CaptureError;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        SceneManager.sceneLoaded -= IsolateServices; SceneManager.sceneLoaded += IsolateServices;
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        var errors = SessionState.GetString(Key + ".Errors", "");
        if (errors.Length < 24000) SessionState.SetString(Key + ".Errors", errors + message + "\n" + stack + "\n");
    }

    static void IsolateServices(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        foreach (var shop in UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include))
        { shop.gameObject.SetActive(false); UnityEngine.Object.Destroy(shop.gameObject); }
        IAPManager.Target = null;
    }

    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        var errors = SessionState.GetString(Key + ".Errors", "");
        if (errors.Length > 0) { Finish(errors); return; }
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > 360)
        { Finish("Tutorial smoke timeout: " + SceneManager.GetActiveScene().name + "/" + FSceneProcessesRunner.Main.currentProcess?.GetType().Name); return; }
        if (!EditorApplication.isPlaying || !Starter.ConfigInitialised || SessionState.GetBool(Key + ".Running", false)) return;
        bool title = UnityEngine.Object.FindObjectsByType<TitleScreenLayer>(FindObjectsSortMode.None)
            .Any(layer => layer.gameObject.activeInHierarchy && !layer.IsClosing);
        if (!title || (SceneManager.GetActiveScene().buildIndex != 0 && FSceneProcessesRunner.Main.currentProcess is not FightingProcess)) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report();
        var oldAccount = PlayerAccountInfo.Me;
        var oldLanguage = AppSetting.Value.Language;
        var oldLogging = FightGlobalSetting.HitBoxLogger;
        try
        {
            FightGlobalSetting.HitBoxLogger = false;
            IsolateServices(default, default);
            await UniTask.NextFrame();
            PlayerAccountInfo.Me = new PlayerAccountInfo { PlayFabId = "local-tutorial-smoke", tutorialProgress = "Started", noAdsState = true };
            AppSetting.Value.Language = SystemLanguage.Japanese;
            PlayerPrefs.SetInt("auto", 0);
            Directory.CreateDirectory(Output);
            var manager = new ArcadeModeManager();
            await manager.Initialize().Timeout(TimeSpan.FromSeconds(30));
            var quest = await manager.LoadStage(1).Timeout(TimeSpan.FromSeconds(30));
            var leaderSource = await manager.LoadStage(4).Timeout(TimeSpan.FromSeconds(30));
            Require(quest != null && quest.UnitsData.Count > 0 && !quest.IsGroupBattle, "Published first quest is missing.");
            quest.EventType = FightEventType.Quest;
            quest.Team1ID = "local-tutorial-smoke"; quest.Team2ID = "local-tutorial-smoke-enemy";
            quest.FightMembers = new FightMembers();
            var hero = leaderSource.UnitsData[0].DeepCopy(); hero.id = "0";
            quest.FightMembers.HeroSets.Set(0, 0, hero);
            for (int i = 0; i < quest.UnitsData.Count; i++)
            { var enemy = quest.UnitsData[i].DeepCopy(); enemy.id = i.ToString(); quest.FightMembers.EnemySets.Set(0, i, enemy); }
            Require(quest.ShouldRunFirstQuestTutorial, "First quest no longer selects its native tutorial.");
            FightLoad.Go(quest);
            await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == "FightScene"
                && FSceneProcessesRunner.Main.currentProcess is FightingProcess && FightLoad.Fight?.ID == "1"
                && UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>()?.Initialized == true).Timeout(TimeSpan.FromSeconds(180));
            Require(FightLoad.Fight.RunTutorial, "Production fight loader omitted the first-quest tutorial.");
            var layer = UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>();
            var tutorial = Field<ClickNextTutorial>(layer, "clickNextTutorial");
            var pages = Field<GameObject[]>(tutorial, "TutorialLayers");
            var layout = layer.GetComponent<BattleTutorialLayout>();
            Require(tutorial.gameObject.activeInHierarchy && pages.Length == 6 && layout != null,
                "Production Fighting caller did not open the six-page tutorial.");
            RTFightManager.Target.team1.TurnAllUnitsInvincible(true);
            RTFightManager.Target.team2.TurnAllUnitsInvincible(true);
            Time.timeScale = 0;
            for (int i = 0; i < pages.Length; i++)
            {
                await UniTask.WaitUntil(() => tutorial.Button.interactable).Timeout(TimeSpan.FromSeconds(4));
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                Require(Field<int>(tutorial, "pageIndex") == i && pages.Count(page => page.activeSelf) == 1 && pages[i].activeSelf,
                    "Wrong live tutorial page " + i);
                Require(tutorial.GetComponent<Canvas>().sortingOrder > layer.Team1UI.AutoSwitch.GetComponent<Canvas>().sortingOrder,
                    "Tutorial sorted behind the actual Auto control.");
                layout.RefreshLayout(); Canvas.ForceUpdateCanvases();
                CheckCallouts(layer, layout);
                foreach (var target in new[] { layer.InputsManager.AttackButton.transform, layer.Team1UI.AutoSwitch.transform, tutorial.transform })
                    Require(TopHit(ScreenCenter(target)).GetComponentInParent<Button>() == tutorial.Button,
                        "Tutorial click passed through to " + target.name);
                await Screenshot("page-" + (i + 1));
                var point = ScreenCenter(layer.InputsManager.AttackButton.transform);
                bool special = pages[i].GetComponent<TutorialLayerSpecialEvent>() != null;
                Dispatch(point);
                Require(!layer.InputsManager.attack, "A tutorial click leaked into the live attack input.");
                if (i == 0) Time.timeScale = 1;
                if (i < pages.Length - 1)
                {
                    Dispatch(point);
                    Require(Field<int>(tutorial, "pageIndex") == i + 1, "Rapid repeated click skipped a page.");
                }
                if (special)
                {
                    Require(RTFightManager.Target.team1.Auto && !Field<GameObject>(layer, "forceClickAutoBtnBlackMask").activeSelf,
                        "Final tutorial click did not enable auto and clear its blocker.");
                    report.forceAuto = true;
                }
                report.pages++;
            }
            Require(!tutorial.gameObject.activeSelf, "The six-page tutorial did not close.");
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            var restoredHit = TopHit(ScreenCenter(layer.InputsManager.AttackButton.transform));
            Require(ExecuteEvents.GetEventHandler<IPointerDownHandler>(restoredHit) == layer.InputsManager.AttackButton.gameObject,
                "Battle skill remained blocked after the tutorial: hit=" + restoredHit.name
                + ", handler=" + ExecuteEvents.GetEventHandler<IPointerDownHandler>(restoredHit)?.name
                + ", expected=" + layer.InputsManager.AttackButton.name);
            var press = new PointerEventData(EventSystem.current) { position = ScreenCenter(layer.InputsManager.AttackButton.transform) };
            ExecuteEvents.ExecuteHierarchy(restoredHit, press, ExecuteEvents.pointerDownHandler);
            Require(layer.InputsManager.attack, "Native attack input did not resume after tutorial close.");
            ExecuteEvents.ExecuteHierarchy(restoredHit, press, ExecuteEvents.pointerUpHandler);
            Require(!layer.InputsManager.attack, "Native attack input did not release after tutorial close.");
            report.skillInputRestored = true;
            // The real Dream explanation is used when charge is full. Reproduce
            // that visual condition before opening its otherwise dormant overlay.
            layer.InputsManager.CurrentFocus.Value.FightDataRef.DreamComboGauge.Value = FightGlobalSetting.DreamComboGaugeMax;
            layer.ForceClickDreamComboBtn();
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            layout.RefreshLayout(); CheckCallouts(layer, layout);
            var dreamOverlay = Field<GameObject>(layer, "clickTriggerDreamCombo");
            Require(dreamOverlay.activeInHierarchy && dreamOverlay.GetComponent<Canvas>().sortingOrder > layer.Team1UI.AutoSwitch.GetComponent<Canvas>().sortingOrder,
                "Dream Combo tutorial did not use the live overlay hierarchy.");
            await Screenshot("dream-combo");
            layer.TutorialModeForceOnClickDreamCombo();
            Require(!dreamOverlay.activeSelf, "Dream Combo tutorial did not close through its production action.");
            report.dreamOverlay = true;
            Require(report.pages == 6 && report.forceAuto && report.pointerChecks > 0, "Tutorial flow checks were incomplete.");
            report.passed = true;
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            Time.timeScale = 1; PlayerAccountInfo.Me = oldAccount;
            AppSetting.Value.Language = oldLanguage; FightGlobalSetting.HitBoxLogger = oldLogging;
            Finish(null);
        }
    }

    static void CheckCallouts(FightingStepLayer layer, BattleTutorialLayout layout)
    {
        foreach (var item in layout.Callouts.Where(item => item.Label.gameObject.activeInHierarchy))
        {
            Require(item.Target != null && item.Target.gameObject.activeInHierarchy, "Missing live pointer target: " + item.Label.name);
            if (item.Label.transform.parent.parent.name == "UserHPEXBarIntro")
                Require(item.Target.GetComponentInParent<SideUnitIcon>() != null, "HP pointer uses a detached tutorial fixture.");
            var nearest = new Vector2(Mathf.Clamp(item.Tip.x, item.TargetBounds.xMin, item.TargetBounds.xMax),
                Mathf.Clamp(item.Tip.y, item.TargetBounds.yMin, item.TargetBounds.yMax));
            Require(Vector2.Distance(item.Tip, nearest) <= 2, "Tutorial pointer misses the actual target.");
            report.pointerChecks++;
        }
    }

    static Vector2 ScreenCenter(Transform target)
    {
        var rt = (RectTransform)target;
        var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rt.TransformPoint(rt.rect.center));
    }

    static GameObject TopHit(Vector2 point)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Require(hits.Count > 0, "No live UI raycast at " + point);
        report.raycastChecks++;
        return hits[0].gameObject;
    }

    static void Dispatch(Vector2 point)
    {
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hit = TopHit(point);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
    }

    static async UniTask Screenshot(string name)
    {
        var path = Path.GetFullPath(Path.Combine(Output, name + ".png"));
        ScreenCapture.CaptureScreenshot(path);
        await UniTask.Delay(250, DelayType.Realtime);
        Require(File.Exists(path), "Screenshot missing: " + name);
        report.screenshots.Add(path);
    }

    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    static void Finish(string error)
    {
        if (finishing) return;
        finishing = true; report ??= new Report();
        if (!string.IsNullOrEmpty(error)) report.errors.Add(error);
        var logged = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(logged) && logged != error) report.errors.Add(logged);
        report.passed &= report.errors.Count == 0;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Poll; SceneManager.sceneLoaded -= IsolateServices;
        AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        if (SessionState.GetBool(Key + ".HadAuto", false)) PlayerPrefs.SetInt("auto", SessionState.GetInt(Key + ".Auto", 0));
        else PlayerPrefs.DeleteKey("auto");
        Debug.Log("[TutorialPlaymodeSmoke] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1); else EditorApplication.isPlaying = false;
    }
}
