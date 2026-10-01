using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using FightScene;
using Skill;
using UniRx;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runs killable production Group battles through countdown, elimination and results.</summary>
[InitializeOnLoad]
public static class PocketStrikerCombatFlowSmoke
{
    const string Key = "PocketStriker.CombatFlowSmoke";
    static string Output => SessionState.GetBool(Key + ".EvolutionHeal", false) ? "Logs/Evolution/Playmode"
        : SessionState.GetBool(Key + ".StoryFailures", false) ? "Logs/AIStory/Playmode" : "Logs/CombatFlow/Playmode";
    const string Account = "local-combat-flow-smoke";
    static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    static bool finishing;
    static Report report;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string utcTime;
        public string scope = "Actual FightScene with Addressables Fast Mode and FightLoad.Go; real animated models, AI, animation hitboxes, damage, death subscriptions, elimination tracker and FightOver. Group 24v24 and a 22v1 sparse final-enemy battle; loading/countdown must preserve HP and Empty behavior. These actual battles use no manual death, damage, GameOver or invincibility. Separate local collider fixtures exercise saturation and owner rebind.";
        public string limitation = "Local Self event uses GangbangInfo Group mechanics while isolating account/IAP/advertising/story services. Fixture team HP multipliers shorten real combat without prescribing a winner. Winner and local-winner contracts used by rewards are checked; no remote reward is granted. Editor Play mode, not physical-device performance.";
        public int isolatedIAPObjects;
        public int saturatedFixtureColliders;
        public int grownFixtureCapacity;
        public bool legacyBufferTruncates;
        public bool capacityDoesNotShrink;
        public bool detectionClearDropsNearest;
        public bool ownerRebindFiltersDeath;
        public bool nullOwnerLaterResolves;
        public int optionalStoryRequestCases;
        public int evolutionDefeatHeals;
        public int evolutionSkillRecalculations;
        public int evolutionHealthBarChecks;
        public bool evolutionFinalDefeatHeal;
        public bool normalRotationHealthUnchanged;
        public bool deadPlayerNotRevived;
        public List<Case> cases = new List<Case>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public sealed class Case
    {
        public string name;
        public int team1;
        public int team2;
        public int preparingFrames;
        public int countdownFrames;
        public int preBattleHpSamples;
        public int fightingFrames;
        public int damageEvents;
        public int attackStateSamples;
        public int deathCount;
        public int liveTargetCacheChecks;
        public int oneEnemyFrames;
        public int oneEnemyAcquiredFrames;
        public int largestQueryCount;
        public int queryCapacity;
        public float battleSeconds;
        public float finalEnemySeconds;
        public string winner;
        public bool resultPreservesWinner;
        public bool resultLocalWinnerContract;
        public string storyFault;
        public bool resultWhileStoryPending;
    }

    static PocketStrikerCombatFlowSmoke()
    {
        if (SessionState.GetBool(Key, false)) Attach();
    }

    [MenuItem("PocketStriker/Validation/Combat Flow Playmode Smoke")]
    public static void StartBatch()
    {
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin();
    }

    [MenuItem("PocketStriker/Validation/AI Story Failure Playmode Smoke")]
    public static void StartStoryFailureBatch()
    {
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", true);
        Begin();
    }

    [MenuItem("PocketStriker/Validation/Evolution Heal Playmode Smoke")]
    public static void StartEvolutionHealBatch()
    {
        SessionState.SetBool(Key + ".EvolutionHeal", true);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin();
    }

    static void Begin()
    {
        finishing = false;
        report = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start combat-flow smoke from a stopped editor.");
        if (!Application.isBatchMode)
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before combat-flow smoke.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Require(settings != null, "Addressables settings are missing.");
        int fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        Require(fast >= 0, "Addressables Fast Mode builder is missing.");
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex);
        SessionState.SetBool(Key + ".HadAuto", PlayerPrefs.HasKey("auto"));
        SessionState.SetInt(Key + ".Auto", PlayerPrefs.GetInt("auto", 0));
        SessionState.SetString(Key + ".Start", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetInt(Key + ".IAPObjects", 0);
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key, true);
        settings.ActivePlayModeDataBuilderIndex = fast;
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var view = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetMethod("SetCustomResolution", PrivateInstance)?.Invoke(view,
            new object[] { new Vector2(540, 960), "PocketStriker Combat Flow Smoke" });
        Attach();
        EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(scene => scene.enabled).path, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= CaptureError;
        Application.logMessageReceived += CaptureError;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        SceneManager.sceneLoaded -= IsolateLoadedSceneServices;
        SceneManager.sceneLoaded += IsolateLoadedSceneServices;
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        var errors = SessionState.GetString(Key + ".Errors", "");
        if (errors.Length < 24000) SessionState.SetString(Key + ".Errors", errors + message + "\n" + stack + "\n");
    }

    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        var errors = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(errors)) { Finish(errors); return; }
        var started = DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime();
        if ((DateTime.UtcNow - started).TotalSeconds > 600)
        {
            Finish("Combat-flow smoke timeout. " + Diagnostic());
            return;
        }
        if (!EditorApplication.isPlaying || !Starter.ConfigInitialised || SessionState.GetBool(Key + ".Running", false)) return;
        bool title = UnityEngine.Object.FindObjectsByType<TitleScreenLayer>(FindObjectsSortMode.None)
            .Any(layer => layer != null && layer.gameObject.activeInHierarchy && !layer.IsClosing);
        if (!title || (SceneManager.GetActiveScene().buildIndex != 0 && FSceneProcessesRunner.Main.currentProcess is not FightingProcess)) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var oldAccount = PlayerAccountInfo.Me;
        var oldLanguage = AppSetting.Value.Language;
        bool oldLogging = FightGlobalSetting.HitBoxLogger;
        try
        {
            RemoveShopStartupObjects();
            await UniTask.NextFrame();
            Require(UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include).Length == 0,
                "Shop account subscriptions survived fixture isolation.");
            PlayerAccountInfo.Me = new PlayerAccountInfo
                { PlayFabId = Account, tutorialProgress = "Finished", noAdsState = true, arcadeProcess = 9999 };
            AppSetting.Value.Language = SystemLanguage.Chinese;
            PlayerPrefs.SetInt("auto", 1);
            // Validation must not rewrite the user's persistent skill-analysis data.
            FightGlobalSetting.HitBoxLogger = false;
            Directory.CreateDirectory(Output);
            if (!SessionState.GetBool(Key + ".EvolutionHeal", false)) ValidateSaturatedQuery();
            var manager = new ArcadeModeManager();
            await manager.Initialize().Timeout(TimeSpan.FromSeconds(30));
            var authored = await manager.LoadStage(4).Timeout(TimeSpan.FromSeconds(30));
            Require(authored is GangbangInfo && authored.UnitsData.Count > 0, "Registered Group stage 4 did not load.");
            var leader = authored.UnitsData[0].DeepCopy();
            UnityEngine.Object.Destroy(authored);
            if (SessionState.GetBool(Key + ".EvolutionHeal", false))
            {
                report.scope = "Actual FightLoad.Go, production rotation units/death subscriptions, four Evolution opponent defeats including the terminal defeat, three real skill-choice UI callbacks/stat recalculations and live HP sliders. A non-Evolution rotation defeat and already-dead player are negative controls. HP includes the configured team multiplier.";
                report.limitation = "Local Self battles isolate account/reward services. Invulnerability prevents incidental AI damage; fixture HP reductions and death notifications make boundary conditions deterministic. This checks production defeat handling and evolution transitions, not natural damage or device performance.";
                await ValidateEvolutionHeal(leader);
                report.passed = true;
                return;
            }
            if (SessionState.GetBool(Key + ".StoryFailures", false))
            {
                report.scope = "Native Quest Group battles with real AI/damage/elimination, FightResultAnim and ArenaFightOver. Injected optional story exceptions, never-ending requests and malformed responses; results must complete without awaiting story generation. Also checks success, empty, exception, real-time timeout and cancellation through the production request wrapper.";
                report.limitation = "Local fixture account already completed Quest 4 so no remote reward is requested. Editor-only injected story loaders make faults deterministic without contacting a provider. HP multipliers shorten combat, and a native click dismisses any authored local fallback story. Editor Play mode.";
                await ValidateOptionalStoryRequests();
                foreach (var fault in new[] { "exception", "pending", "empty" })
                {
                    SessionState.SetString(Key + ".StoryFault", fault);
                    var quest = CreateGroup(leader, 22, 1);
                    quest.EventType = FightEventType.Quest;
                    quest.ID = "4";
                    await RunBattle(quest, "story-" + fault, report.cases.Count > 0);
                }
                Require(report.cases.Count == 3 && report.cases.All(item => item.resultPreservesWinner)
                    && report.cases.Any(item => item.storyFault == "pending" && item.resultWhileStoryPending),
                    "An optional story fault blocked natural battle results.");
                report.passed = true;
                return;
            }
            await RunBattle(CreateGroup(leader, 24, 24), "group-24v24", false);
            await RunBattle(CreateGroup(leader, 22, 1), "group-22v1-final-enemy", true);
            Require(report.cases.Count == 2 && report.cases.All(item => item.resultPreservesWinner
                && item.damageEvents > 0 && item.deathCount > 0), "Both real battles must end by damage and elimination.");
            report.passed = true;
        }
        catch (Exception exception) { report.errors.Add(exception + "\n" + Diagnostic()); }
        finally
        {
            PlayerAccountInfo.Me = oldAccount;
            AppSetting.Value.Language = oldLanguage;
            FightGlobalSetting.HitBoxLogger = oldLogging;
            Finish(null);
        }
    }

    static async UniTask ValidateOptionalStoryRequests()
    {
        var texture = new Texture2D(1, 1);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        var valid = ScriptableObject.CreateInstance<StoryInfo>();
        var empty = ScriptableObject.CreateInstance<StoryInfo>();
        valid.StoryScenes = new List<StoryInfo.StoryScene> { new StoryInfo.StoryScene { Pic = sprite, Lines = new List<string> { "Ready" } } };
        try
        {
            Require(await BattleStoryRequest.Load(() => UniTask.FromResult(valid), CancellationToken.None) == valid, "Ready story was dropped."); report.optionalStoryRequestCases++;
            Require(await BattleStoryRequest.Load(() => UniTask.FromResult(empty), CancellationToken.None) == null, "Malformed story was accepted."); report.optionalStoryRequestCases++;
            Require(await BattleStoryRequest.Load(() => UniTask.FromException<StoryInfo>(new InvalidOperationException("fixture service failure")), CancellationToken.None) == null, "Story exception escaped."); report.optionalStoryRequestCases++;
            Require(await BattleStoryRequest.Load(() => new UniTaskCompletionSource<StoryInfo>().Task, CancellationToken.None, 0.05) == null, "Hanging story did not time out."); report.optionalStoryRequestCases++;
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Require(await BattleStoryRequest.Load(() => new UniTaskCompletionSource<StoryInfo>().Task, cancelled.Token) == null, "Story cancellation escaped."); report.optionalStoryRequestCases++;
        }
        finally { UnityEngine.Object.DestroyImmediate(valid); UnityEngine.Object.DestroyImmediate(empty); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
    }

    static void ValidateSaturatedQuery()
    {
        var fixture = new GameObject("CombatFlowSaturatedSensorFixture");
        fixture.transform.position = new Vector3(0, 512, 0);
        var detector = fixture.AddComponent<SensorUnity>();
        typeof(SensorUnity).GetField("_layers", PrivateInstance).SetValue(detector, (LayerMask)((1 << 2) | (1 << 3)));
        var team = new TeamConfig("fixture", Team.player1, new List<Team> { Team.player2 });
        team.enemyLayerMask = 1 << 3;
        team.enemyShieldLayerMask = 0;
        team.enemyWeaponLayerMask = 0;
        var sensor = new Sensor { Center = fixture.transform, SensorRadius = 10 };
        sensor.SetDetectLayer(team, null);
        detector.SensorDetectionResultClearProcesses.Add(sensor.SensorDetectionResultClearProcess);
        detector.SensorDetectionResultSortProcesses.Add(sensor.SensorDetectionResultSortProcess);
        try
        {
            Collider enemy = null;
            const int colliders = 97;
            for (int index = 0; index < colliders; index++)
            {
                var part = new GameObject(index == colliders - 1 ? "FinalEnemy" : "FriendlyBodyPart");
                part.layer = index == colliders - 1 ? 3 : 2;
                part.transform.SetParent(fixture.transform, false);
                part.transform.localPosition = new Vector3((index % 10) * 0.2f, 0, (index / 10) * 0.2f);
                var collider = part.AddComponent<BoxCollider>();
                collider.size = Vector3.one * 0.05f;
                if (index == colliders - 1) enemy = collider;
            }
            Physics.SyncTransforms();
            var legacy = new Collider[2];
            report.legacyBufferTruncates = Physics.OverlapSphereNonAlloc(fixture.transform.position, 10, legacy,
                (1 << 2) | (1 << 3)) == legacy.Length;
            Require(report.legacyBufferTruncates, "Saturation negative control did not fill the old remaining*2 buffer.");
            detector.Setup(10, fixture.transform.position, 2);
            detector.ForceImmediateDetection();
            report.saturatedFixtureColliders = ReadHitCount(detector);
            report.grownFixtureCapacity = ReadCapacity(detector);
            Require(report.saturatedFixtureColliders == colliders && report.grownFixtureCapacity > colliders,
                "A saturated production NonAlloc query did not recover every collider.");
            Require(sensor.GetClosestEnemyColliderInSensorRange() == enemy,
                "The final enemy was hidden behind friendly body-part query results.");
            var limb = enemy.gameObject.AddComponent<BO_Limb>();
            detector.ForceImmediateDetection();
            var liveObject = new GameObject("LiveFixtureOwner");
            liveObject.transform.SetParent(fixture.transform, false);
            liveObject.SetActive(false);
            var liveOwner = liveObject.AddComponent<Data_Center>();
            liveOwner.WholeT = liveObject.transform;
            var deadObject = new GameObject("DeadFixtureOwner");
            deadObject.transform.SetParent(fixture.transform, false);
            deadObject.SetActive(false);
            var deadOwner = deadObject.AddComponent<Data_Center>();
            deadOwner.WholeT = deadObject.transform;
            deadOwner.FightDataRef.IsDead.Value = true;
            limb.Center = liveOwner;
            Require(sensor.GetClosestEnemyColliderInSensorRange() == enemy, "A later live-owner binding disappeared.");
            limb.Center = deadOwner;
            report.nullOwnerLaterResolves = sensor.GetClosestEnemyColliderInSensorRange() == null;
            Require(report.nullOwnerLaterResolves, "A cached null owner hid a subsequent dead-owner binding.");
            limb.Center = liveOwner;
            detector.ForceImmediateDetection();
            Require(sensor.GetClosestEnemyColliderInSensorRange() == enemy, "Rebinding the collider to a live owner did not restore it.");
            limb.Center = deadOwner;
            report.ownerRebindFiltersDeath = sensor.GetClosestEnemyColliderInSensorRange() == null;
            Require(report.ownerRebindFiltersDeath, "A cached alive owner survived rebind to a dead owner.");
            detector.Setup(10, fixture.transform.position, 2);
            report.capacityDoesNotShrink = ReadCapacity(detector) == report.grownFixtureCapacity;
            Require(report.capacityDoesNotShrink, "Casualty reconfiguration shrank the high-water query capacity.");
            sensor.SensorDetectionResultClearProcess();
            report.detectionClearDropsNearest = sensor.GetClosestEnemyColliderInSensorRange() == null;
            Require(report.detectionClearDropsNearest, "Clearing detection retained the old nearest enemy.");
        }
        finally
        {
            detector.Stop();
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    static GangbangInfo CreateGroup(UnitInfo leader, int heroes, int enemies)
    {
        var fight = ScriptableObject.CreateInstance<GangbangInfo>();
        fight.ID = "combat-flow-" + heroes + "v" + enemies;
        fight.EventType = FightEventType.Self;
        fight.team1Mode = fight.team2Mode = TeamMode.MultiRaid;
        fight.Team1ID = Account;
        fight.Team2ID = Account + "-enemy";
        fight.battleGroundID = 0;
        fight.team1CGMode = fight.team2CGMode = CriticalGaugeMode.Normal;
        fight.team1AIMode = fight.team2AIMode = AIMode.Aggressive;
        fight.dumbAIDecisionDelay = 2;
        fight.team1HpRate = heroes == enemies ? 0.3f : 1f;
        fight.team2HpRate = 0.3f;
        fight.FightMembers = new FightMembers();
        var hero = leader.DeepCopy(); hero.id = "0";
        var enemy = leader.DeepCopy(); enemy.id = "0";
        fight.FightMembers.HeroSets.Set(0, 0, hero);
        fight.FightMembers.EnemySets.Set(0, 0, enemy);
        fight.Team1GroupSet = new List<GangbangInfo.SoldierGroupSet> { new GangbangInfo.SoldierGroupSet("0", heroes) };
        fight.Team2GroupSet = new List<GangbangInfo.SoldierGroupSet> { new GangbangInfo.SoldierGroupSet("0", enemies) };
        fight.RecordTeamLimit(Mathf.Max(heroes, enemies));
        fight.ConvertTeamToGangbang();
        return fight;
    }

    static async UniTask RunBattle(GangbangInfo fight, string name, bool inScene)
    {
        var item = new Case { name = name, team1 = fight.FightMembers.HeroSets.Count,
            team2 = fight.FightMembers.EnemySets.Count };
        report.cases.Add(item);
        var hp = new Dictionary<Data_Center, float>();
        var beforeManager = RTFightManager.Target;
        var start = Time.realtimeSinceStartup;
        float fightingStart = -1;
        float finalEnemyStart = -1;
        float lastLog = start;
        bool sawGameOver = false;
        bool fallbackClicked = false;
        Team winner = Team.none;
        using var deathResult = FightLogger.value.GameOver.Subscribe(over =>
        {
            if (!over || FSceneProcessesRunner.Main.currentProcess is not FightingProcess
                || FightLoad.Fight?.ID != fight.ID) return;
            sawGameOver = true;
            winner = FightLogger.value.GetWinnerTeam();
        });
        FightLoad.Go(fight, inScene);
        while (true)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            Require(Time.realtimeSinceStartup - start < 240, name + " did not reach FightOver. " + Diagnostic());
            var manager = RTFightManager.Target;
            if (manager == null || (!inScene && manager == beforeManager) || SceneManager.GetActiveScene().name != "FightScene") continue;
            var process = FSceneProcessesRunner.Main.currentProcess;
            var units = manager.team1.teamMembers.mDict.Values.Concat(manager.team2.teamMembers.mDict.Values).ToList();
            var byObject = units.Where(unit => unit != null && unit.WholeT != null)
                .ToDictionary(unit => unit.WholeT.gameObject);
            if (process is PreparingProcess || process is CountDownProcess)
            {
                if (process is PreparingProcess) item.preparingFrames++; else item.countdownFrames++;
                foreach (var unit in units)
                {
                    Require(!unit._MyBehaviorRunner.IfRunning(), name + " ran a behavior before countdown ended.");
                    var state = unit._MyBehaviorRunner.GetNowState();
                    Require(state == null || string.IsNullOrEmpty(state.StateKey) || state.StateKey == "Empty",
                        name + " left Empty before countdown ended: " + state?.StateKey);
                    float current = unit.FightDataRef.CurrentHp.Value;
                    if (hp.TryGetValue(unit, out float previous) && previous > 0)
                        Require(current >= previous - 0.0001f, name + " lost HP during " + process.GetType().Name);
                    hp[unit] = current;
                    if (current > 0) item.preBattleHpSamples++;
                    Require(!unit.FightDataRef.IsDead.Value, name + " killed a fighter before FightingProcess.");
                }
            }
            else if (process is FightingProcess)
            {
                if (fightingStart < 0)
                {
                    fightingStart = Time.realtimeSinceStartup;
                    Require(manager.team1.teamMembers.mDict.Count == item.team1 && manager.team2.teamMembers.mDict.Count == item.team2,
                        name + " did not load the configured team sizes.");
                    Require(item.preparingFrames > 0 && item.countdownFrames > 0 && item.preBattleHpSamples > 0,
                        name + " did not observe the real loading/countdown with initialized HP.");
                    Require(manager.team1.Auto && manager.team2.Auto, name + " did not use both teams' real AI.");
                    Require(units.All(unit => !unit.FightDataRef.Invincible), name + " contains an invincible fighter.");
                }
                item.fightingFrames++;
                foreach (var unit in units)
                {
                    float current = unit.FightDataRef.CurrentHp.Value;
                    if (hp.TryGetValue(unit, out float previous) && current < previous - 0.0001f) item.damageEvents++;
                    hp[unit] = current;
                    var state = unit._MyBehaviorRunner.GetNowState();
                    if (state != null && (state.StateType is BehaviorType.GR or BehaviorType.GM or BehaviorType.GMB
                        or BehaviorType.GI or BehaviorType.CT)) item.attackStateSamples++;
                    if (unit.FightDataRef.IsDead.Value) continue;
                    // GetEnemies(false) must invalidate its cached corpse target after a registry change.
                    var enemies = unit.Sensor.GetEnemiesByDistance(false);
                    foreach (var target in enemies)
                    {
                        Require(target != null && target.activeInHierarchy, "Live-target cache retained an inactive enemy.");
                        Require(byObject.TryGetValue(target, out var center) && !center.FightDataRef.IsDead.Value,
                            "Live-target cache retained a dead enemy.");
                    }
                    item.liveTargetCacheChecks++;
                }
                int remainingEnemies = manager.team2.teamMembers.mDict.Values.Count(unit => !unit.FightDataRef.IsDead.Value);
                if (remainingEnemies == 1)
                {
                    if (finalEnemyStart < 0) finalEnemyStart = Time.realtimeSinceStartup;
                    item.oneEnemyFrames++;
                    if (manager.team1.teamMembers.mDict.Values.Any(unit => !unit.FightDataRef.IsDead.Value
                        && unit.Sensor.GetClosestEnemyColliderInSensorRange() != null)) item.oneEnemyAcquiredFrames++;
                }
                var detector = BoundaryControlByGod.target.SensorUnity;
                int hits = ReadHitCount(detector);
                item.largestQueryCount = Math.Max(item.largestQueryCount, hits);
                item.queryCapacity = ReadCapacity(detector);
                Require(hits < item.queryCapacity, "Live shared query remained saturated after detection.");
            }
            else if (process is FightResultAnim && SessionState.GetBool(Key + ".StoryFailures", false))
            {
                var storyLayer = UnityEngine.Object.FindFirstObjectByType<ArenaFightOver>();
                if (storyLayer != null && !fallbackClicked)
                {
                    var text = (UnityEngine.UI.Text)typeof(ArenaFightOver).GetField("shortStory", PrivateInstance).GetValue(storyLayer);
                    if (text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                    {
                        var button = (BOButton)typeof(ArenaFightOver).GetField("storyMaskBtn", PrivateInstance).GetValue(storyLayer);
                        button.onClick.Invoke(); fallbackClicked = true;
                    }
                }
            }
            else if (process is FightOverProcess)
            {
                // The terminal hit can occur in FixedUpdate, followed by the
                // process transition in Update before this end-of-frame sample.
                foreach (var unit in units)
                {
                    Require(!unit._MyBehaviorRunner.IfRunning(), "A previous fighter's AI kept running on the result screen.");
                    if (hp.TryGetValue(unit, out float previous) && unit.FightDataRef.CurrentHp.Value < previous - 0.0001f)
                        item.damageEvents++;
                    if (!unit.FightDataRef.IsDead.Value)
                        Require(unit.Sensor.GetEnemiesByDistance(false).Count == 0,
                            "The winning team's cached target list retained eliminated opponents.");
                }
                item.battleSeconds = Time.realtimeSinceStartup - fightingStart;
                item.finalEnemySeconds = finalEnemyStart < 0 ? 0 : Time.realtimeSinceStartup - finalEnemyStart;
                item.deathCount = units.Count(unit => unit.FightDataRef.IsDead.Value);
                item.winner = winner.ToString();
                item.resultPreservesWinner = sawGameOver && winner != Team.none && FightLogger.value.GameOver.Value
                    && FightLogger.value.GetWinnerTeam() == winner;
                string winningId = winner == manager.team1.teamConfig.myTeam ? FightLoad.Fight.Team1ID : FightLoad.Fight.Team2ID;
                item.resultLocalWinnerContract = FightLogger.value.GetWinnerId() == winningId
                    && FightLogger.value.IsLocalPlayerWinner(RTFightManager.playerTeam, Account)
                    == (winner == RTFightManager.playerTeam || winningId == Account);
                Require(item.resultPreservesWinner && item.resultLocalWinnerContract,
                    name + " lost winner/reward eligibility when entering results.");
                Require(item.damageEvents > 0 && item.attackStateSamples > 0 && item.deathCount >= Math.Min(item.team1, item.team2),
                    name + " ended without real attacks, damage and team elimination.");
                bool storyCase = SessionState.GetBool(Key + ".StoryFailures", false);
                item.storyFault = storyCase ? SessionState.GetString(Key + ".StoryFault", "") : "";
                if (storyCase)
                {
                    var storyTask = global::FightScene.FightScene.target.EnsureAIStory();
                    item.resultWhileStoryPending = storyTask.Status == UniTaskStatus.Pending;
                    Require(global::FightScene.FightScene.target.AIStoryInfo == null, "A failed story reached result presentation.");
                    Require(item.storyFault != "pending" || item.resultWhileStoryPending, "Hanging story fixture was not still pending at results.");
                }
                await UniTask.WaitUntil(() => storyCase ? UnityEngine.Object.FindFirstObjectByType<ArenaFightOver>() != null
                    : UnityEngine.Object.FindFirstObjectByType<CommonFightResult>() != null)
                    .Timeout(TimeSpan.FromSeconds(15));
                if (item.team2 == 1)
                    Require(winner == manager.team1.teamConfig.myTeam && item.oneEnemyFrames > 0 && item.oneEnemyAcquiredFrames > 0,
                        "The sparse last-enemy battle did not acquire and eliminate its only enemy.");
                Debug.Log("[CombatFlowSmoke] " + name + " PASS: damage=" + item.damageEvents + ", deaths=" + item.deathCount
                    + ", winner=" + item.winner + ", seconds=" + item.battleSeconds + ", finalEnemySeconds=" + item.finalEnemySeconds);
                return;
            }
            if (Time.realtimeSinceStartup - lastLog > 15)
            {
                lastLog = Time.realtimeSinceStartup;
                Debug.Log("[CombatFlowSmoke] " + name + ": " + Diagnostic());
                File.WriteAllText(Path.Combine(Output, "progress.json"), JsonUtility.ToJson(report, true));
            }
        }
    }

    static async UniTask ValidateEvolutionHeal(UnitInfo leader)
    {
        FightInfo CreateFixture(string id, bool evolution, int enemies)
        {
            var fight = ScriptableObject.CreateInstance<FightInfo>();
            fight.ID = id;
            fight.EventType = FightEventType.Self;
            fight.FightMode = evolution ? FightMode.Evolve : FightMode.Rotate;
            fight.Team1ID = Account; fight.Team2ID = Account + "-enemy";
            fight.battleGroundID = 0;
            fight.team1HpRate = 1.7f;
            fight.FightMembers = new FightMembers();
            var hero = leader.DeepCopy(); hero.id = "0";
            fight.FightMembers.HeroSets.Set(0, 0, hero);
            for (int i = 0; i < enemies; i++)
            {
                var enemy = leader.DeepCopy(); enemy.id = i.ToString();
                fight.FightMembers.EnemySets.Set(0, i, enemy);
            }
            return fight;
        }

        async UniTask<Data_Center> StartFixture(FightInfo fight, bool reuse)
        {
            FightLoad.Go(fight, reuse);
            await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == "FightScene"
                && FightLoad.Fight?.ID == fight.ID && FSceneProcessesRunner.Main.currentProcess is FightingProcess
                && UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>()?.Initialized == true)
                .Timeout(TimeSpan.FromSeconds(180));
            var manager = RTFightManager.Target;
            manager.team1.TurnAllUnitsInvincible(true); manager.team2.TurnAllUnitsInvincible(true);
            manager.team1.Auto = false; manager.team2.Auto = false;
            var hero = manager.team1.RMode_Unit.Value;
            float expected = SkillSet.INI_Hp(hero.UnitInfo.set.SkillIDList(), hero.UnitInfo.level) * FightLoad.Fight.team1HpRate;
            Require(expected > 0 && Mathf.Approximately(hero.FightDataRef.MaxHp, expected)
                && Mathf.Approximately(hero.FightDataRef.CurrentHp.Value, expected), "Initial MaxHp lost the configured team HP multiplier.");
            return hero;
        }

        async UniTask CheckHealthBar(Data_Center hero, float fraction)
        {
            hero.FightDataRef.CurrentHp.Value = hero.FightDataRef.MaxHp * fraction;
            await UniTask.Delay(300);
            var layer = UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>();
            var slider = layer.Team1UI.UnitIconDic[hero].HealthBarRect.GetComponent<UnityEngine.UI.Slider>();
            Require(Mathf.Abs(slider.value - fraction) < 0.001f, "Live HP bar did not use the evolved maximum HP.");
            report.evolutionHealthBarChecks++;
        }

        var evolutionFight = CreateFixture("evolution-heal", true, 4);
        var hero = await StartFixture(evolutionFight, false);
        for (int index = 0; index < 4; index++)
        {
            var enemy = RTFightManager.Target.team2.RMode_Unit.Value;
            Require(enemy != null && !enemy.FightDataRef.IsDead.Value, "Evolution did not present a living next opponent.");
            await CheckHealthBar(hero, 0.35f);
            enemy.FightDataRef.IsDead.Value = true;
            Require(Mathf.Approximately(hero.FightDataRef.CurrentHp.Value, hero.FightDataRef.MaxHp),
                "Opponent defeat did not immediately restore full HP before the skill choice: " + index);
            report.evolutionDefeatHeals++;
            if (index == 3)
            {
                Require(FightLogger.value.GameOver.Value && FightLogger.value.GetWinnerTeam() == Team.player1
                    && ActiveEvolutionLayer() == null, "Final defeat did not skip evolution and complete the battle.");
                report.evolutionFinalDefeatHeal = true;
                break;
            }

            var evolution = ActiveEvolutionLayer();
            Require(evolution != null, "Opponent defeat omitted the real evolution layer.");
            await UniTask.WaitUntil(() => ((UnityEngine.UI.Text)typeof(InBattleEvolution)
                .GetField("upperText", PrivateInstance).GetValue(evolution)).text == Translate.Get("ChooseYourEvolution"))
                .Timeout(TimeSpan.FromSeconds(20));
            var options = (EvolutionSkill[])typeof(InBattleEvolution).GetField("skillOptions", PrivateInstance).GetValue(evolution);
            options[0].Btn.onClick.Invoke();
            await UniTask.WaitUntil(() => ActiveEvolutionLayer() == null
                && RTFightManager.Target.team2.RMode_Unit.Value != enemy)
                .Timeout(TimeSpan.FromSeconds(30));
            float expected = SkillSet.INI_Hp(hero.UnitInfo.set.SkillIDList(), hero.UnitInfo.level) * FightLoad.Fight.team1HpRate;
            Require(Mathf.Approximately(hero.FightDataRef.MaxHp, expected)
                && Mathf.Approximately(hero.FightDataRef.CurrentHp.Value, expected), "Skill choice did not refresh maximum/full HP.");
            report.evolutionSkillRecalculations++;
        }
        await UniTask.WaitUntil(() => FSceneProcessesRunner.Main.currentProcess is FightOverProcess)
            .Timeout(TimeSpan.FromSeconds(15));

        var rotationFight = CreateFixture("rotation-no-heal", false, 1);
        hero = await StartFixture(rotationFight, true);
        float damagedHp = hero.FightDataRef.MaxHp * 0.35f;
        hero.FightDataRef.CurrentHp.Value = damagedHp;
        RTFightManager.Target.team2.RMode_Unit.Value.FightDataRef.IsDead.Value = true;
        Require(Mathf.Approximately(hero.FightDataRef.CurrentHp.Value, damagedHp), "Normal rotation unexpectedly healed on an enemy defeat.");
        report.normalRotationHealthUnchanged = true;
        await UniTask.WaitUntil(() => FSceneProcessesRunner.Main.currentProcess is FightOverProcess)
            .Timeout(TimeSpan.FromSeconds(15));

        var simultaneousFight = CreateFixture("evolution-dead-player", true, 1);
        hero = await StartFixture(simultaneousFight, true);
        hero.FightDataRef.CurrentHp.Value = 0;
        hero.FightDataRef.IsDead.Value = true;
        RTFightManager.Target.team2.RMode_Unit.Value.FightDataRef.IsDead.Value = true;
        Require(hero.FightDataRef.CurrentHp.Value == 0 && hero.FightDataRef.IsDead.Value, "A defeated player was revived by the opponent's death.");
        report.deadPlayerNotRevived = true;
        Require(report.evolutionDefeatHeals == 4 && report.evolutionSkillRecalculations == 3
            && report.evolutionHealthBarChecks == 4 && report.evolutionFinalDefeatHeal
            && report.normalRotationHealthUnchanged && report.deadPlayerNotRevived, "Evolution heal checks were incomplete.");
        UnityEngine.Object.Destroy(evolutionFight); UnityEngine.Object.Destroy(rotationFight); UnityEngine.Object.Destroy(simultaneousFight);
    }

    static InBattleEvolution ActiveEvolutionLayer() => UnityEngine.Object
        .FindObjectsByType<InBattleEvolution>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .FirstOrDefault(layer => !layer.IsClosing && layer.gameObject.activeInHierarchy);

    static int ReadHitCount(SensorUnity sensor) => (int)typeof(SensorUnity).GetField("_hitCount", PrivateInstance).GetValue(sensor);
    static int ReadCapacity(SensorUnity sensor) => ((Collider[])typeof(SensorUnity).GetField("_hits", PrivateInstance).GetValue(sensor))?.Length ?? 0;

    static string Diagnostic()
    {
        var manager = RTFightManager.Target;
        string TeamState(UnitsManger team) => team?.teamMembers == null ? "unloaded" : string.Join("; ", team.teamMembers.mDict.Values
            .Where(unit => unit != null && !unit.FightDataRef.IsDead.Value).Take(4)
            .Select(unit => unit.UnitInfo?.id + ":hp=" + unit.FightDataRef.CurrentHp.Value + ",state="
                + unit._MyBehaviorRunner?.GetNowState()?.StateKey + ",pos=" + unit.geometryCenter.position));
        int Alive(UnitsManger team) => team?.teamMembers?.mDict.Values.Count(unit => unit != null && !unit.FightDataRef.IsDead.Value) ?? -1;
        return "scene=" + SceneManager.GetActiveScene().name + ",process=" + FSceneProcessesRunner.Main.currentProcess?.GetType().Name
            + ",alive=" + Alive(manager?.team1) + "/" + Alive(manager?.team2) + ",team1=" + TeamState(manager?.team1)
            + ",team2=" + TeamState(manager?.team2);
    }

    static void IsolateLoadedSceneServices(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        RemoveShopStartupObjects();
        if (SessionState.GetBool(Key + ".StoryFailures", false))
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<global::FightScene.FightScene>();
            if (controller != null)
                typeof(global::FightScene.FightScene).GetField("StoryLoaderForValidation", PrivateInstance).SetValue(controller,
                    (Func<UniTask<StoryInfo>>)(() => SessionState.GetString(Key + ".StoryFault", "") switch
                    {
                        "exception" => UniTask.FromException<StoryInfo>(new InvalidOperationException("fixture story generation failed")),
                        "pending" => new UniTaskCompletionSource<StoryInfo>().Task,
                        _ => UniTask.FromResult(ScriptableObject.CreateInstance<StoryInfo>())
                    }));
        }
    }

    static void RemoveShopStartupObjects()
    {
        // Destroy the GameObject to cancel UniRx AddTo account-ready callbacks before Start.
        var shops = UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include);
        foreach (var shop in shops) { shop.gameObject.SetActive(false); UnityEngine.Object.Destroy(shop.gameObject); }
        IAPManager.Target = null;
        int removed = SessionState.GetInt(Key + ".IAPObjects", 0) + shops.Length;
        SessionState.SetInt(Key + ".IAPObjects", removed);
        if (report != null) report.isolatedIAPObjects = removed;
    }

    static void Finish(string error)
    {
        if (finishing) return;
        finishing = true;
        report ??= new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        if (!string.IsNullOrEmpty(error)) report.errors.Add(error);
        var logged = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(logged) && logged != error) report.errors.Add(logged);
        report.passed = report.passed && report.errors.Count == 0;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CaptureError;
        EditorApplication.update -= Poll;
        SceneManager.sceneLoaded -= IsolateLoadedSceneServices;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        if (SessionState.GetBool(Key + ".HadAuto", false)) PlayerPrefs.SetInt("auto", SessionState.GetInt(Key + ".Auto", 0));
        else PlayerPrefs.DeleteKey("auto");
        Debug.Log("[CombatFlowSmoke] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
