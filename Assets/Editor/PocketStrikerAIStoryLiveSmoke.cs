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

/// <summary>Production device login, real AI generation and native story/result navigation.</summary>
[InitializeOnLoad]
public static class PocketStrikerAIStoryLiveSmoke
{
    const string Key = "PocketStriker.AIStoryLiveSmoke";
    const string Startup = "Assets/Scene/ABLoadScene/Scene1.unity";
    const double MaximumSeconds = 360;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static bool finishing;
    static Report report;
    static CancellationTokenSource cancellation;
    static string Output => SessionState.GetString(Key + ".Output", "Logs/AIStory/Live/after");

    [Serializable] public sealed class Report
    {
        public bool passed;
        public bool complete;
        public bool baseline;
        public bool realAccountLogin;
        public bool generated;
        public bool reusedReadyCache;
        public bool storyDisplayed;
        public bool generatedSpriteStable;
        public bool captionFits;
        public bool storyFinished;
        public bool naturalBattleFinished;
        public bool returnedToHome;
        public bool fixtureAccountUsed;
        public bool presentationResultInjected;
        public string unityVersion;
        public string utcTime;
        public string phase;
        public string outcome;
        public string stage;
        public double generationSeconds;
        public int scenes;
        public int visualScenes;
        public int textResponseLogs;
        public int imageResponseLogs;
        public int providerErrorLogs;
        public int otherErrorLogs;
        public int pointerClicks;
        public string limitation = "Editor Play mode, local Addressables Fast Mode, production device login and actual PlayFab/Azure story requests. A cloned completed Quest Group uses fixture team counts and HP multipliers to shorten a natural battle. Generation is explicitly awaited by this test before the battle; production results never wait for it. No account replacement, manual deaths, result injection or remote reward is used. Physical-device behavior and cold-cache performance are not proven.";
        public List<string> imageDimensions = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<string> failures = new List<string>();
    }
    [Serializable] sealed class SceneBackup { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded; public bool active; }

    static PocketStrikerAIStoryLiveSmoke()
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
        SessionState.SetString(Key + ".Output", "Logs/AIStory/Live/" + label);
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
            unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O"), phase = "login" };
        Directory.CreateDirectory(Output);
        cancellation = new CancellationTokenSource();
        FightInfo originalFight = FightLoad.Fight;
        bool originalLogging = FightGlobalSetting.HitBoxLogger;
        GameObject serviceHost = null;
        GangbangInfo fixture = null;
        try
        {
            await Stable();
            await NativeClick(Field<Button>(Layer<TitleScreenLayer>(), "touchScreenBtn"));
            await Wait(() => PreScene.target != null && PlayerAccountInfo.Me != null
                && ProcessesRunner.Main.currentProcess != null, 90, "production login");
            report.realAccountLogin = true;
            await Wait(() => Layer<FrontLayer>() != null && ProcessesRunner.Main.currentProcess?.CanEnterOtherProcess() == true
                && Layer<ProgressLayer>() == null, 45, "main menu account data readiness");
            await Stable();
            await DismissStartupModals();
            Require(PlayerAccountInfo.Me.tutorialProgress == "Finished", "The development account has unfinished onboarding.");
            Require(PlayerAccountInfo.Me.arcadeProcess >= 4, "Quest 4 must already be completed to avoid remote rewards.");
            report.stage = "4";
            await Screenshot("home");

            report.phase = "fixture-preparation";
            var stages = new ArcadeModeManager();
            await stages.Initialize().Timeout(TimeSpan.FromSeconds(30), DelayType.Realtime);
            var authored = await stages.LoadStage(4).Timeout(TimeSpan.FromSeconds(30), DelayType.Realtime);
            Require(authored != null && authored.UnitsData.Count > 0, "Registered Quest 4 did not load.");
            fixture = CreateGroup(authored.UnitsData[0].DeepCopy(), PlayerAccountInfo.Me.PlayFabId);
            UnityEngine.Object.Destroy(authored);
            FightLoad.Fight = fixture;
            FightGlobalSetting.HitBoxLogger = false;

            report.phase = "real-generation";
            Save();
            serviceHost = new GameObject("AIStoryLiveSmokeService");
            var service = serviceHost.AddComponent<AIServiceManager>();
            double generationStart = Time.realtimeSinceStartupAsDouble;
            var story = await service.LoadAIStory().AttachExternalCancellation(cancellation.Token)
                .Timeout(TimeSpan.FromSeconds(60), DelayType.Realtime);
            report.generationSeconds = Time.realtimeSinceStartupAsDouble - generationStart;
            report.scenes = story?.StoryScenes?.Count ?? 0;
            report.visualScenes = story?.StoryScenes?.Count(scene => scene?.Pic != null) ?? 0;
            report.generated = story != null && story.HasVisualScene();
            if (!report.generated)
            {
                report.outcome = "Real generation returned no visual story; presentation was not exercised.";
                report.complete = true;
                return;
            }
            foreach (var scene in story.StoryScenes.Where(scene => scene?.Pic != null))
                report.imageDimensions.Add(scene.Pic.texture.width + "x" + scene.Pic.texture.height);
            Save();
            UnityEngine.Object.Destroy(serviceHost);
            serviceHost = null;

            report.phase = "natural-battle";
            FightLoad.Go(fixture);
            await Wait(() => SceneManager.GetActiveScene().name == "FightScene"
                && global::FightScene.FightScene.target != null
                && FSceneProcessesRunner.Main.currentProcess is FightingProcess, 90, "natural battle start");
            await Wait(() => global::FightScene.FightScene.target.AIStoryInfo != null, 15, "ready story cache reuse");
            report.reusedReadyCache = ReferenceEquals(global::FightScene.FightScene.target.AIStoryInfo, story);
            Require(report.reusedReadyCache, "The battle did not consume the real generated story cache.");
            await Screenshot("battle");
            await Wait(() => Layer<ArenaFightOver>() != null && Field<bool>(Layer<ArenaFightOver>(), "aiStoryPlaying"),
                90, "natural victory and story presentation");
            report.naturalBattleFinished = FightLogger.value.GameOver.Value
                && FightLogger.value.IsLocalPlayerWinner(RTFightManager.playerTeam, PlayerAccountInfo.Me.PlayFabId);
            Require(report.naturalBattleFinished, "Story presentation did not follow a natural local victory.");
            var result = Layer<ArenaFightOver>();
            var image = Field<Image>(result, "storyBgImage");
            report.storyDisplayed = image.gameObject.activeInHierarchy && image.sprite != null;
            Require(report.storyDisplayed, "Generated story image is not visible.");
            report.phase = "story-clicks";
            await Stable();
            report.generatedSpriteStable = image.sprite == story.StoryScenes[story.FindNextVisualSceneIndex(-1)].Pic
                && image.overrideSprite == image.sprite && image.GetComponent<Animator>() == null && !image.canvasRenderer.cull;
            Require(report.generatedSpriteStable, "An animated prefab replaced or hid the generated illustration.");
            await Screenshot("story-image");
            int clickLimit = story.StoryScenes.Sum(scene => (scene?.Lines?.Count ?? 0) + 1) + 4;
            for (int index = 0; index < clickLimit && Field<bool>(result, "aiStoryPlaying"); index++)
            {
                await NativeClick(Field<Button>(result, "storyMaskBtn"));
                if (index == 0 && Field<bool>(result, "aiStoryPlaying"))
                {
                    var caption = Field<Text>(result, "shortStory");
                    Canvas.ForceUpdateCanvases();
                    report.captionFits = caption.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(caption.text)
                        && caption.cachedTextGenerator.characterCountVisible >= caption.text.TrimEnd().Length;
                    Require(report.captionFits, "Story caption is hidden or clipped.");
                    await Screenshot("story-text");
                }
            }
            report.storyFinished = !Field<bool>(result, "aiStoryPlaying");
            Require(report.storyFinished, "Story clicks did not finish all generated pages.");
            await Wait(() => FSceneProcessesRunner.Main.currentProcess is FightOverProcess, 15, "story-to-result transition");
            await Stable();
            await Screenshot("result");
            report.phase = "return-home";
            await NativeClick(Field<Button>(result, "returnBtn"));
            await Wait(() => SceneManager.GetActiveScene().name == "MainMenuScene" && PreScene.target != null
                && Layer<FrontLayer>() != null && ProcessesRunner.Main.currentProcess?.CanEnterOtherProcess() == true
                && Layer<ProgressLayer>() == null, 45, "result return to home");
            report.returnedToHome = true;
            await Screenshot("returned-home");
            report.complete = true;
            report.passed = report.realAccountLogin && report.generated && report.reusedReadyCache && report.storyDisplayed
                && report.generatedSpriteStable && report.captionFits && report.storyFinished && report.naturalBattleFinished && report.returnedToHome && report.otherErrorLogs == 0;
            report.outcome = report.passed ? "Real generated story passed natural battle presentation and return navigation."
                : "Flow completed with recorded runtime errors.";
        }
        catch (Exception exception)
        {
            // Exception type and test phase are sufficient for a non-sensitive report.
            report.failures.Add(report.phase + ": " + exception.GetType().Name
                + (exception.StackTrace != null && exception.StackTrace.Contains("PocketStrikerAIStoryLiveSmoke.Require")
                    ? ": " + exception.Message : ""));
            report.outcome = "The live test did not complete its declared coverage.";
        }
        finally
        {
            if (serviceHost != null) UnityEngine.Object.Destroy(serviceHost);
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
            FightLoad.Fight = originalFight;
            FightGlobalSetting.HitBoxLogger = originalLogging;
            Finish(null);
        }
    }

    static GangbangInfo CreateGroup(UnitInfo leader, string localPlayerId)
    {
        var fight = ScriptableObject.CreateInstance<GangbangInfo>();
        fight.ID = "4";
        fight.EventType = FightEventType.Quest;
        fight.team1Mode = fight.team2Mode = TeamMode.MultiRaid;
        fight.Team1ID = localPlayerId;
        fight.Team2ID = "local-ai-story-smoke-enemy";
        fight.battleGroundID = 0;
        fight.team1CGMode = fight.team2CGMode = CriticalGaugeMode.Normal;
        fight.team1AIMode = fight.team2AIMode = AIMode.Aggressive;
        fight.dumbAIDecisionDelay = 2;
        fight.team1HpRate = 1;
        fight.team2HpRate = 0.3f;
        fight.FightMembers = new FightMembers();
        var hero = leader.DeepCopy(); hero.id = "0";
        var enemy = leader.DeepCopy(); enemy.id = "0";
        fight.FightMembers.HeroSets.Set(0, 0, hero);
        fight.FightMembers.EnemySets.Set(0, 0, enemy);
        fight.Team1GroupSet = new List<GangbangInfo.SoldierGroupSet> { new GangbangInfo.SoldierGroupSet("0", 22) };
        fight.Team2GroupSet = new List<GangbangInfo.SoldierGroupSet> { new GangbangInfo.SoldierGroupSet("0", 1) };
        fight.RecordTeamLimit(22);
        fight.ConvertTeamToGangbang();
        return fight;
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
        Debug.Log("[AIStoryLiveSmoke] " + (report.passed ? "PASS" : "INCOMPLETE/FAIL") + ": "
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
