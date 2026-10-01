using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using mainMenu;
using ModelView;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Live, read-only menu review through the production login, processes and pointer
/// handlers. Does not replace the logged-in account with a local fixture.
/// </summary>
[InitializeOnLoad]
public static class PocketStrikerUILiveReview
{
    const string Key = "PocketStriker.UILiveReview";
    const string Startup = "Assets/Scene/ABLoadScene/Scene1.unity";
    const double MaximumSeconds = 480;
    const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static bool finishing;
    static Report report;
    static CancellationTokenSource cancellation;
    static Case current;
    static string Output => SessionState.GetString(Key + ".Output", "Logs/UIArt/Live/latest");

    [Serializable] public sealed class Case
    {
        public string name;
        public string status = "unreached";
        public string observedScene;
        public string observedProcess;
        public string finalProcess;
        public string catalogState;
        public List<CaptureState> captures = new List<CaptureState>();
        public string note;
        public int pointerClicks;
        public int duplicateLayerChecks;
        public double seconds;
        public List<string> screenshots = new List<string>();
        public int programmaticScrollEndpoints;
    }
    [Serializable] public sealed class CaptureState
    {
        public string path;
        public string scene;
        public string process;
        public int width;
        public int height;
        public int activeModelPreviews;
        public int loadedModelPreviews;
        public int portraitCount;
        public int loadedPortraits;
    }
    [Serializable] public sealed class Report
    {
        public bool passed;
        public bool complete;
        public bool realAccountLogin;
        public bool fixtureAccountUsed;
        public string unityVersion;
        public string utcTime;
        public string loginMode;
        public string scope = "Actual Scene1 startup, production login, PreScene/ProcessesRunner navigation, native EventSystem raycasts, loaded UI/models, return/close and repeated entry. Screenshots use ScreenCapture at the actual Game view size.";
        public string limitation = "Editor Play mode with local Addressables Fast Mode and real PlayFab account data. No IAP, account deletion, device linking, password reset, external links, mail claims, stone merges or gacha purchases are dispatched. Empty/locked/unreached pages are recorded explicitly. Synthetic warning/confirmation text exercises the real shared modal.";
        public string startupSideEffects = "Normal login may create the existing device account if missing, run the production check-in, load/read local mail caches, reconcile pending tutorial progress and sanitize saved team data. These are production startup behaviors, not fixture mutations. No account or credentials are written into this report.";
        public int width;
        public int height;
        public int sceneRecoveries;
        public List<Case> cases = new List<Case>();
        public List<string> errors = new List<string>();
    }
    [Serializable] sealed class SceneBackup { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] sealed class SceneRecord { public string path; public bool loaded; public bool active; }

    static readonly string[] Inventory =
    {
        "title", "password-login", "home", "adventure-preparation", "adventure-stage-list",
        "team-edit", "arena", "ranking", "arena-awards", "random-boss", "collection",
        "skill-editor", "skill-tips", "skill-combo", "stones", "gacha", "gacha-alternate",
        "gacha-probabilities", "shop", "settings-account", "settings-volume", "settings-device",
        "settings-support", "settings-language", "settings-nickname", "nickname-dialog", "mail",
        "mail-detail", "training", "warning-modal", "confirmation-modal", "battle-hud", "battle-pause"
    };

    static PocketStrikerUILiveReview()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key, false)) Attach();
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Key + ".RestoreScenes", false))
            EditorApplication.delayCall += RestoreScenes;
    }

    [MenuItem("PocketStriker/Validation/UI Live Review")]
    public static void StartBatch() => Begin(DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
    public static void StartBeforeBatch() => Begin("before");
    public static void StartAfterBatch() => Begin("after");
    public static void StartRefinedBatch() => Begin("delivery-final");
    public static void StartRefinedSmallBatch() => Begin("final-small", 375, 667);
    public static void StartRefinedTallBatch() => Begin("refined-tall", 390, 844);

    static void Begin(string label, int width = 540, int height = 960)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start UI live review from a stopped editor.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scenes before UI live review.");
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
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetString(Key + ".Output", "Logs/UIArt/Live/" + label);
        SessionState.SetBool(Key + ".Running", false);
        PreserveInt("auto");
        PreserveInt("gangbangCountOption");
        PreserveString("showUnit");
        SessionState.SetBool(Key, true);
        finishing = false;
        report = null;
        settings.ActivePlayModeDataBuilderIndex = fast;
        var gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var resolutionMethod = gameView.GetMethod("SetCustomResolution", Fields);
        Require(resolutionMethod != null, "Native Game view resolution setter is unavailable.");
        resolutionMethod.Invoke(EditorWindow.GetWindow(gameView),
            new object[] { new Vector2(width, height), "PocketStriker UI Live Review" });
        Attach();
        EditorSceneManager.OpenScene(Startup, OpenSceneMode.Single);
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
        if (errors.Length < 16000)
            SessionState.SetString(Key + ".Errors", errors + Sanitize(type + ": " + message) + "\n");
    }
    static string Sanitize(string value)
    {
        value = Regex.Replace(value ?? "", @"(?i)(sessionticket|authorization|password|customid|email|playfabid)[\s\""'=:\-]+[^\s,\}\""']+", "$1=<withheld>");
        return Regex.Replace(value, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", "<email withheld>");
    }
    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        var start = DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime();
        if ((DateTime.UtcNow - start).TotalSeconds > MaximumSeconds)
        { Finish("Global UI live review deadline exceeded: " + Diagnostic()); return; }
        if (!EditorApplication.isPlaying || !Starter.ConfigInitialised || SessionState.GetBool(Key + ".Running", false)) return;
        if (Layer<TitleScreenLayer>() == null) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        foreach (string name in Inventory) report.cases.Add(new Case { name = name });
        Directory.CreateDirectory(Output);
        cancellation = new CancellationTokenSource();
        var originalAccount = PlayerAccountInfo.Me;
        var originalDestination = MainMenuNote.GoingTo;
        var originalHistory = ReturnLayer.ReturnMissionList.ToArray();
        try
        {
            await Check("title", async () => { await Stable(); await Screenshot("title"); });
            await Check("password-login", async () =>
            {
                await Click(Field<Button>(Layer<TitleScreenLayer>(), "accountLoginBtn"));
                await Wait(() => Field<RectTransform>(Layer<TitleScreenLayer>(), "loginByPwTab").gameObject.activeInHierarchy, 4, "password login panel");
                await Stable(); await Screenshot("password-login");
                await Click(Field<Button>(Layer<TitleScreenLayer>(), "cancelBtn"));
                await Wait(() => Field<RectTransform>(Layer<TitleScreenLayer>(), "mainTab").gameObject.activeInHierarchy, 4, "login cancel");
            });
            report.loginMode = "Production title device login; credential values are neither read by the review nor reported";
            var title = Layer<TitleScreenLayer>();
            Require(title != null, "Title disappeared before login.");
            await Click(Field<Button>(title, "touchScreenBtn"));
            await Wait(() => PreScene.target != null && PlayerAccountInfo.Me != null && ProcessesRunner.Main.currentProcess != null, 90, "production login/main scene");
            report.realAccountLogin = true;
            // Preserve only ordinary UI preferences. Never extract login identifiers.
            PreserveInt(PlayFabSetting._arenaPointCode);
            await DismissStartupModals();
            Require(PlayerAccountInfo.Me.tutorialProgress == "Finished", "The actual account has unfinished onboarding; menu review is blocked rather than substituting a fixture account.");
            await Check("home", async () => { await Home(); RequirePreview(Layer<FrontLayer>()); await Screenshot("home"); });
            await Adventure();
            await Arena();
            await Check("random-boss", async () =>
            {
                await Home(); await Click(Field<Button>(Layer<FrontLayer>(), "EventFightBtn"));
                await Page<EventBattleTop>(MainSceneStep.RandomBoss, 40); await Screenshot("random-boss"); await Back(MainSceneStep.FrontPage);
            });
            await Collection();
            await Check("stones", async () =>
            {
                await Home(); await Click(Tab("stoneTab")); await Page<StoneListLayer>(MainSceneStep.SkillStoneList, 35);
                await Screenshot("stones"); await ScrollScreens(Layer<StoneListLayer>(), "stones");
                await RepeatTab("stoneTab", MainSceneStep.SkillStoneList, typeof(StoneListLayer));
            });
            await Gacha();
            await Check("shop", async () =>
            {
                await Home(); var button = Field<Button>(Layer<UpperInfoBar>(), "diamondPlus");
                if (button.IsInteractable()) await Click(button);
                else
                {
                    current.note = "The real currency-plus entry is disabled while IAP is uninitialized. Inspected the production ShopTop process through PreScene; no purchase was dispatched.";
                    Require(PreScene.target.trySwitchToStep(MainSceneStep.ShopTop), "Shop process entry rejected.");
                }
                await Page<ShopTopLayer>(MainSceneStep.ShopTop, 20);
                current.catalogState = "Observed catalog items=" + IAPManager.StoneProductCatalog.Count
                    + "; active product cells=" + Layer<ShopTopLayer>().GetComponentsInChildren<ProductCell>().Length
                    + "; IAP initialized=" + (IAPManager.Target != null && IAPManager.Target.IsInitialized.Value)
                    + ". Production purchase-history callback completion is not exposed; shop shell verified, full catalog completion unverified.";
                await Screenshot("shop"); await ScrollScreens(Layer<ShopTopLayer>(), "shop");
                await Back(MainSceneStep.FrontPage);
            });
            await Settings();
            await Mail();
            await Check("training", async () =>
            {
                await Home(); await Click(Field<Button>(Layer<FrontLayer>(), "TrainBtn"));
                await Page<SelfFightLayer>(MainSceneStep.SelfFightFront, 35);
                var practice = Layer<SelfFightLayer>();
                var start = Field<Button>(Field<FightBeginBtn>(practice, "fightStartBtn"), "btn");
                Require(!start.IsInteractable(), "An empty practice lineup can start a fight.");
                typeof(SelfFightLayer).GetMethod("FightStart", Fields).Invoke(practice, null);
                await Stable();
                Require(Layer<SelfFightLayer>() == practice && SceneManager.GetActiveScene().name == "MainMenuScene", "Empty practice callback escaped its validation guard.");
                await Screenshot("training");
                var candidate = Layer<UnitsLayer>().GetComponentsInChildren<HeroIcon>().FirstOrDefault(icon =>
                    UnitInfo.GetUnitInfo(dataAccess.Units.Get(icon.InstanceID))?.set.CheckEdit() == SkillSet.SkillEditError.Perfect
                    && dataAccess.Stones.GetEquippingStones(icon.InstanceID).Count == 9);
                Require(candidate != null, "No legal owned character available for practice validation.");
                await Click(Field<HeroCell>(practice, "team11_R").iconButton); await Click(candidate.iconButton);
                Require(!start.IsInteractable(), "One-sided practice lineup can start a fight.");
                await Click(Field<HeroCell>(practice, "team21_R").iconButton); await Click(candidate.iconButton);
                Require(start.IsInteractable(), "Valid opposing practice lineups remain disabled.");
                await Screenshot("training-ready");
                practice.Clear();
                Require(!start.IsInteractable(), "Clearing practice lineups left start enabled.");
                current.note = "Actual owned character assigned to both teams through native slot/portrait clicks; empty, one-sided, valid and cleared lineups checked. Direct empty-start callback guard also checked; no practice match dispatched.";
                await Back(MainSceneStep.FrontPage);
            });
            await SharedModals();
            await Battle();
            await Home();
        }
        catch (Exception exception) { report.errors.Add(Sanitize(exception.Message) + " | " + Diagnostic()); }
        finally
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            PlayerAccountInfo.Me = originalAccount;
            MainMenuNote.GoingTo = originalDestination;
            ReturnLayer.ReturnMissionList.Clear(); ReturnLayer.ReturnMissionList.AddRange(originalHistory);
            Finish(null);
        }
    }

    static async UniTask Adventure()
    {
        await Check("adventure-preparation", async () =>
        {
            await Home(); await Click(Field<LowerBarIcon>(Layer<FrontLayer>(), "ArcadeBtn").BOButton);
            await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 40); RequirePreview(Layer<FightPrepareLayer>()); await Screenshot("adventure-preparation");
        });
        await Check("team-edit", async () =>
        {
            Require(Layer<FightPrepareLayer>() != null, "Adventure preparation was not reached.");
            await Click(Field<Button>(Layer<FightPrepareLayer>(), "editTeamButton"));
            await Wait(() => Step == MainSceneStep.TeamEditFront && Ready && (Layer<TeamEditLayer>() != null && ModelsReady(Layer<TeamEditLayer>()) || Layer<TeamSingleSelectLayer>() != null && ModelsReady(Layer<TeamSingleSelectLayer>())), 30, "team edit");
            await Stable(); RequirePreview((UILayer)Layer<TeamEditLayer>() ?? Layer<TeamSingleSelectLayer>()); await Screenshot("team-edit"); await Back(MainSceneStep.QuestInfo);
        });
        await Check("adventure-stage-list", async () =>
        {
            Require(Layer<FightPrepareLayer>() != null, "Adventure preparation was not reached.");
            await Click(Field<Button>(Layer<FightPrepareLayer>(), "toArcadeFrontBtn"));
            await Page<ArcadeTop>(MainSceneStep.ArcadeFront, 35); await Screenshot("adventure-stage-list");
            var stage = Layer<ArcadeTop>().GetComponentsInChildren<StageButton>().FirstOrDefault(item => item.Button.IsInteractable());
            Require(stage != null, "No reachable production stage card.");
            var portrait = stage.GetComponentsInChildren<HeroIcon>().FirstOrDefault(icon => icon.iconButton.IsInteractable());
            if (portrait != null)
            {
                await Click(portrait.iconButton); await Stable();
                Require(Step == MainSceneStep.ArcadeFront, "Enemy preview unexpectedly entered a stage.");
                await Screenshot("adventure-enemy-preview");
            }
            await Click(stage.Button, normalized: new Vector2(0.1f, 0.5f)); await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 40);
            await Screenshot("adventure-stage-reentry");
        });
    }
    static async UniTask Arena()
    {
        await Check("arena", async () =>
        {
            await Home(); Require(PlayerAccountInfo.Me.arcadeProcess >= 5, "Actual account has not unlocked arena (stage 5).");
            await Click(Field<LowerBarIcon>(Layer<FrontLayer>(), "ArenaBtn").BOButton);
            await Wait(() => Layer<ArenaLayer>() != null || Layer<ArenaNewSeason>() != null || Layer<NickNameLayer>() != null, 30, "arena entry");
            var season = Layer<ArenaNewSeason>();
            if (season != null) { await Stable(); await Screenshot("arena-new-season"); await Click(Field<Button>(season, "closeBtn")); }
            Require(Layer<NickNameLayer>() == null, "Actual account has no nickname: arena requires a new saved nickname; skipped automatic profile mutation.");
            await Page<ArenaLayer>(MainSceneStep.Arena, 35); await Screenshot("arena");
        });
        await Check("ranking", async () =>
        {
            Require(Layer<ArenaLayer>() != null, "Arena was not reached.");
            await Click(Field<Button>(Layer<ArenaLayer>(), "rankingPageBtn"));
            await Page<RankingLayer>(MainSceneStep.Ranking, 25);
            await Wait(() => Field<VerticalLayoutGroup>(Layer<RankingLayer>(), "enemiesT").transform.childCount > 0, 20, "server-populated ranking rows");
            await Page<RankingLayer>(MainSceneStep.Ranking, 25); await Screenshot("ranking"); await ScrollScreens(Layer<RankingLayer>(), "ranking"); await Back(MainSceneStep.Arena);
        });
        await Check("arena-awards", async () =>
        {
            if (Step == MainSceneStep.Ranking) await Back(MainSceneStep.Arena);
            Require(Layer<ArenaLayer>() != null, "Arena was not reached.");
            await Click(Field<Button>(Layer<ArenaLayer>(), "rewardBtn"));
            await Page<ArenaAwardLayer>(MainSceneStep.ArenaAward, 20);
            Require(Field<RectTransform>(Layer<ArenaAwardLayer>(), "itemsParent").GetComponentsInChildren<ArenaRewardItem>().Length > 0, "Actual server-configured arena reward list is empty.");
            await Screenshot("arena-awards");
            await ScrollScreens(Layer<ArenaAwardLayer>(), "arena-awards"); await Back(MainSceneStep.Arena); await Back(MainSceneStep.FrontPage);
        });
    }
    static async UniTask Collection()
    {
        await Check("collection", async () =>
        {
            await Home(); await Click(Tab("fighterTab")); await Page<UnitOptionLayer>(MainSceneStep.UnitList, 30);
            Require(Layer<UnitsLayer>() != null && dataAccess.Units.Dic.Count > 0, "Actual collection is empty.");
            RequirePreview(Layer<UnitOptionLayer>()); await Screenshot("collection"); await RepeatTab("fighterTab", MainSceneStep.UnitList, typeof(UnitOptionLayer));
        });
        await Check("skill-editor", async () =>
        {
            Require(Layer<UnitOptionLayer>() != null, "Collection was not reached.");
            await Click(Field<Button>(Layer<UnitOptionLayer>(), "skillEditButton"));
            await Page<SkillEditLayer>(MainSceneStep.UnitSkillEdit, 40);
            Require(Layer<SkillEditLayer>().Initialized, "Skill editor did not initialize."); RequirePreview(Layer<SkillEditLayer>()); await Screenshot("skill-editor");
        });
        await Check("skill-tips", async () =>
        {
            var layer = Layer<SkillEditLayer>(); Require(layer != null, "Skill editor was not reached.");
            var button = PersistentButton(layer, "OpenTip");
            if (button == null)
            {
                Require(layer.transform.Find("middle/V1/combo") != null && !layer.transform.Find("middle/V1/combo").gameObject.activeInHierarchy,
                    "Tips button unexpectedly missing from an active feature.");
                current.status = "unavailable";
                current.note = "The authored combo parent has been disabled since commit 821d0c6b65 (2024-06-10), including current baseline; tips are inside that historically inactive feature group. No hidden feature was activated.";
                return;
            }
            await Click(button);
            await Wait(() => Layer<SkillEditTipLayer>() != null, 5, "skill tips"); await Stable(); await Screenshot("skill-tips");
            var close = PersistentButton(Layer<SkillEditTipLayer>(), "CloseTip");
            Require(close != null, "No production tips close button."); await Click(close);
            await Wait(() => Layer<SkillEditTipLayer>() == null, 5, "tips close");
        });
        await Check("skill-combo", async () =>
        {
            var layer = Layer<SkillEditLayer>(); Require(layer != null, "Skill editor was not reached.");
            if (!layer.nineSlot.comboShowBtn.gameObject.activeInHierarchy)
            {
                current.status = "unavailable";
                current.note = "Combo preview is gated by the authored hidden parent and skill/tutorial eligibility; inaccessible through the current production page.";
                await Back(MainSceneStep.UnitList);
                return;
            }
            Require(layer.nineSlot.comboShowBtn.IsInteractable(), "Actual equipped skills do not enable combo preview.");
            await Click(layer.nineSlot.comboShowBtn);
            await Wait(() => layer.nineSlot.comboCloseBtn.gameObject.activeInHierarchy, 35, "combo preview completion");
            await Stable(); await Screenshot("skill-combo"); await Click(layer.nineSlot.comboCloseBtn);
            await Wait(() => !layer.nineSlot.comboCloseBtn.gameObject.activeInHierarchy, 5, "combo close");
            await Back(MainSceneStep.UnitList);
        });
    }
    static async UniTask Gacha()
    {
        await Check("gacha", async () =>
        {
            await Home(); await Click(Tab("gotchaTab")); await Page<GotchaLayer>(MainSceneStep.GotchaFront, 25); await Screenshot("gacha");
            await RepeatTab("gotchaTab", MainSceneStep.GotchaFront, typeof(GotchaLayer));
        });
        await Check("gacha-alternate", async () =>
        {
            Require(Layer<GotchaLayer>() != null, "Gacha was not reached.");
            await Click(Field<Button>(Layer<GotchaLayer>(), "right")); await Stable(); await Screenshot("gacha-alternate");
            await Click(Field<Button>(Layer<GotchaLayer>(), "left")); await Stable(); CheckDuplicates();
        });
        await Check("gacha-probabilities", async () =>
        {
            var layer = Layer<GotchaLayer>(); Require(layer != null, "Gacha was not reached.");
            var tables = Field<List<DropTablePage>>(layer, "dropTables");
            var table = tables.FirstOrDefault(item => Field<Button>(item, "openDropTableInfo").gameObject.activeInHierarchy);
            Require(table != null, "Actual gacha page has no visible probability entry.");
            await Click(Field<Button>(table, "openDropTableInfo")); await Page<DropTableInfoLayer>(MainSceneStep.DropTableInfo, 25);
            await Wait(() => Field<VerticalLayoutGroup>(Layer<DropTableInfoLayer>(), "resultT").transform.childCount > 0, 20, "server-populated probability rows");
            await Stable(); await Screenshot("gacha-probabilities"); await ScrollScreens(Layer<DropTableInfoLayer>(), "gacha-probabilities"); await Back(MainSceneStep.GotchaFront);
        });
    }
    static async UniTask Settings()
    {
        bool opened = false;
        foreach (string tab in new[] { "account", "volume", "device", "support", "language", "nickname" })
        {
            await Check("settings-" + tab, async () =>
            {
                if (!opened)
                {
                    await Home(); await Click(Field<Button>(Layer<UpperInfoBar>(), "settingBtn"));
                    await Page<SettingLayer>(MainSceneStep.Setting, 20);
                    await Wait(() => Field<RectTransform>(Layer<SettingLayer>(), "accountPanel").gameObject.activeInHierarchy, 20, "settings account response");
                    opened = true;
                }
                var layer = Layer<SettingLayer>(); Require(layer != null, "Settings disappeared.");
                string field = tab == "nickname" ? "nickName" : tab;
                await Click(Field<Button>(layer, field + "Btn"), true); await Click(Field<Button>(layer, field + "Btn"));
                await Wait(() => Field<RectTransform>(layer, field + "Panel").gameObject.activeInHierarchy, 5, "settings " + tab);
                await Stable(); CheckDuplicates(); await Screenshot("settings-" + tab);
            });
        }
        await Check("nickname-dialog", async () =>
        {
            var layer = Layer<SettingLayer>(); Require(layer != null, "Settings was not reached.");
            await Click(Field<Button>(layer, "resetNickNameBtn"));
            await Wait(() => Layer<NickNameLayer>() != null, 5, "nickname dialog"); await Stable(); await Screenshot("nickname-dialog");
            await Click(Field<Button>(Layer<NickNameLayer>(), "Cancel"));
            await Wait(() => Layer<NickNameLayer>() == null && layer.gameObject.activeInHierarchy, 5, "nickname cancel/resume");
            await Back(MainSceneStep.FrontPage);
        });
    }
    static async UniTask Mail()
    {
        await Check("mail", async () =>
        {
            await Home(); await Click(Field<Button>(Layer<UpperInfoBar>(), "mailBtn"));
            await Page<MailBox>(MainSceneStep.MailBox, 20); await Screenshot("mail"); await ScrollScreens(Layer<MailBox>(), "mail");
        });
        var row = Layer<MailBox>()?.GetComponentsInChildren<MailListView>().FirstOrDefault();
        if (row == null && Layer<MailBox>() != null && report.cases.First(test => test.name == "mail").status == "passed")
        {
            var item = report.cases.First(test => test.name == "mail-detail");
            item.status = "unavailable"; item.note = "Actual mailbox has no mail row; detail view was not fabricated.";
        }
        else if (row != null) await Check("mail-detail", async () =>
        {
            await Click(Field<Button>(row, "detailBtn")); await Page<MailDetailView>(MainSceneStep.MailDetail, 20);
            await Screenshot("mail-detail"); await Back(MainSceneStep.MailBox);
        });
        if (Step == MainSceneStep.MailBox) await Back(MainSceneStep.FrontPage);
    }
    static async UniTask Battle()
    {
        await Check("battle-hud", async () =>
        {
            await Home();
            await Click(Field<LowerBarIcon>(Layer<FrontLayer>(), "ArcadeBtn").BOButton);
            await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 40);
            var start = Field<FightBeginBtn>(Layer<FightPrepareLayer>(), "beginFight");
            await Click(Field<Button>(start, "btn"));
            await Wait(() => Layer<FightingStepLayer>() != null && Layer<FightingStepLayer>().PauseButton.gameObject.activeInHierarchy
                && Layer<FightingStepLayer>().PauseButton.IsInteractable() && Layer<ProgressLayer>() == null, 90, "loaded live battle HUD");
            await Stable(); await Screenshot("battle-hud");
            current.note = "Started the developer account's currently selected adventure through the real preparation button. No synthetic match state was injected.";
        });
        await Check("battle-pause", async () =>
        {
            Require(Layer<FightingStepLayer>() != null, "Battle HUD was not reached.");
            await Click(Layer<FightingStepLayer>().PauseButton);
            await Wait(() => Layer<FightScenePauseSupport>() != null && Time.timeScale == 0, 5, "battle pause");
            await Stable(); await Screenshot("battle-pause");
            var resume = PersistentButton(Layer<FightScenePauseSupport>(), "Resume");
            await Click(resume);
            await Wait(() => Layer<FightScenePauseSupport>() == null && Time.timeScale > 0, 5, "battle resume");
            await Click(Layer<FightingStepLayer>().PauseButton);
            await Wait(() => Layer<FightScenePauseSupport>() != null && Time.timeScale == 0, 5, "battle pause reentry");
            await Click(PersistentButton(Layer<FightScenePauseSupport>(), "Return"));
            await Wait(() => SceneManager.GetActiveScene().name == "MainMenuScene" && PreScene.target != null && Ready, 90, "battle return to menu");
            Require(Time.timeScale > 0, "Battle return left the menu paused.");
            await Stable(); await Screenshot("battle-return");
            current.note = "Native pointer pause, resume, pause again, and return-to-menu callbacks. Result/rewards were not forced.";
        });
    }

    static async UniTask SharedModals()
    {
        await Check("warning-modal", async () =>
        {
            await Home(); PopupLayer.ArrangeWarnWindow(Translate.Get("CanLinkLater"));
            await Wait(() => Layer<PopupLayer>() != null, 4, "warning modal"); await Stable(); await Screenshot("warning-modal");
            await Click(Field<Button>(Layer<PopupLayer>(), "YesButton")); await Wait(() => Layer<PopupLayer>() == null, 4, "warning close");
            current.note = "Synthetic message through the production popup, native pointer close, no account operation.";
        });
        await Check("confirmation-modal", async () =>
        {
            bool accepted = false;
            PopupLayer.ArrangeConfirmWindow(() => accepted = true, Translate.Get("IfSetNickName"));
            await Wait(() => Layer<PopupLayer>() != null, 4, "confirmation modal"); await Stable(); await Screenshot("confirmation-modal");
            await Click(Field<Button>(Layer<PopupLayer>(), "NoButton")); await Wait(() => Layer<PopupLayer>() == null, 4, "confirmation cancel");
            Require(!accepted, "Cancel fired the confirmation action.");
            current.note = "Synthetic message through the production popup; verified cancel did not execute its action.";
        });
    }

    static async UniTask Check(string name, Func<UniTask> action)
    {
        current = report.cases.First(item => item.name == name);
        current.status = "running";
        var start = DateTime.UtcNow;
        try { await action(); if (current.status == "running") current.status = "passed"; }
        catch (OperationCanceledException) { current.status = "failed"; current.note = "Review canceled or deadline reached."; throw; }
        catch (Exception exception)
        {
            current.status = "failed";
            current.note = (current.note == null ? "" : current.note + " | ") + Sanitize(exception.Message);
            try { await Screenshot(name + "-failure"); } catch { }
        }
        finally
        {
            current.seconds = Math.Round((DateTime.UtcNow - start).TotalSeconds, 2);
            if (current.observedScene == null) current.observedScene = SceneManager.GetActiveScene().name;
            if (current.observedProcess == null) current.observedProcess = ProcessesRunner.Main.currentProcess?.GetType().Name;
            current.finalProcess = ProcessesRunner.Main.currentProcess?.GetType().Name;
            SaveReport();
        }
    }
    static MainSceneStep Step => ProcessesRunner.Main.currentProcess?.Step ?? MainSceneStep.None;
    static bool Ready => ProcessesRunner.Main.currentProcess?.CanEnterOtherProcess() == true && Layer<ProgressLayer>() == null;
    static T Layer<T>() where T : UILayer => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
        .FirstOrDefault(item => item != null && item.gameObject.activeInHierarchy && !item.IsClosing);
    static async UniTask Page<T>(MainSceneStep step, double timeout) where T : UILayer
    {
        await Wait(() => Step == step && Ready && Layer<T>() != null && ModelsReady(Layer<T>()), timeout, step + " / " + typeof(T).Name);
        await Stable(); CheckDuplicates();
        report.width = Screen.width; report.height = Screen.height;
        Require(Screen.width < Screen.height, "The actual Game view is not portrait.");
    }
    static void RequirePreview(UILayer layer)
    {
        Require(layer != null && layer.GetComponentsInChildren<DedicatedCameraConnector>()
            .Any(camera => camera.FocusingC != null), "Expected live character preview was not loaded; task completion alone is insufficient.");
    }
    static bool ModelsReady(UILayer layer) => layer != null && layer.GetComponentsInChildren<DedicatedCameraConnector>()
        .All(camera => camera.TaskRunningCount == 0);
    static async UniTask Home()
    {
        await DismissStartupModals();
        if (Step == MainSceneStep.FrontPage && Ready && Layer<FrontLayer>() != null)
        { await Page<FrontLayer>(MainSceneStep.FrontPage, 25); await DismissStartupModals(); await Page<FrontLayer>(MainSceneStep.FrontPage, 10); return; }
        try
        {
            await Wait(() => Ready && Layer<LowerMainBar>() != null, 10, "return to home readiness");
            await Click(Tab("playTab")); await Page<FrontLayer>(MainSceneStep.FrontPage, 40);
            await DismissStartupModals(); await Page<FrontLayer>(MainSceneStep.FrontPage, 10);
        }
        catch (Exception) when (cancellation != null && !cancellation.IsCancellationRequested)
        {
            // A failed page must not prevent independent menu checks. This is a
            // documented recovery, never a success for the route that failed.
            report.sceneRecoveries++;
            ReturnLayer.Clear(); MainMenuNote.GoingTo = MainSceneStep.FrontPage;
            SceneManager.LoadScene(1);
            await Wait(() => PreScene.target != null && Step == MainSceneStep.FrontPage, 25, "main scene recovery");
            await DismissStartupModals(); await Page<FrontLayer>(MainSceneStep.FrontPage, 40);
        }
    }
    static async UniTask Back(MainSceneStep expected)
    {
        Require(ReturnLayer.ReturnMissionList.Count > 0 && Layer<ReturnLayer>() != null, "No production return entry.");
        int count = ReturnLayer.ReturnMissionList.Count;
        await Click(Field<Button>(Layer<ReturnLayer>(), "returnButton"));
        await Wait(() => Step == expected && Ready, 40, "native return to " + expected);
        Require(ReturnLayer.ReturnMissionList.Count < count, "Return stack did not shrink."); await Stable(); CheckDuplicates();
    }
    static async UniTask DismissStartupModals()
    {
        var ask = Layer<AskIfLinkDeviceLayer>();
        if (ask != null)
        {
            await Stable(); await Screenshot("device-link-declined");
            await Click(Field<Button>(ask, "No"));
            await Wait(() => Layer<AskIfLinkDeviceLayer>() == null, 5, "device-link decline");
        }
        var popup = Layer<PopupLayer>();
        if (popup != null)
        {
            await Stable(); await Screenshot("startup-warning");
            var no = Field<Button>(popup, "NoButton");
            await Click(no.gameObject.activeInHierarchy ? no : Field<Button>(popup, "YesButton"));
            await Wait(() => Layer<PopupLayer>() == null, 5, "startup popup dismissal");
        }
    }
    static Button Tab(string name) => Field<LowerBarIcon>(Layer<LowerMainBar>(), name).BOButton;
    static async UniTask RepeatTab(string name, MainSceneStep expected, Type layerType)
    {
        await Click(Tab(name));
        await Wait(() => Step == expected && Ready && UnityEngine.Object.FindObjectsByType(layerType, FindObjectsInactive.Exclude)
            .OfType<UILayer>().Any(item => !item.IsClosing && ModelsReady(item)), 30, "repeat entry " + expected);
        await Stable();
        await Wait(() => UnityEngine.Object.FindObjectsByType(layerType, FindObjectsInactive.Exclude).OfType<UILayer>()
            .Any(item => !item.IsClosing && ModelsReady(item)), 15, "repeat model readiness");
        CheckDuplicates(); await Screenshot(current.name + "-repeat");
    }
    static Button PersistentButton(UILayer layer, string method) => layer.GetComponentsInChildren<Button>()
        .FirstOrDefault(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
            .Any(index => button.onClick.GetPersistentMethodName(index) == method));
    static async UniTask Click(Button button, bool rapidRepeat = false, Vector2? normalized = null)
    {
        if (button is BOButton) await Wait(() => !BOButton.AnyProcess, 2, "native button debounce");
        Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Button is missing, hidden or disabled.");
        Require(EventSystem.current != null, "No production EventSystem.");
        var rect = (RectTransform)button.transform;
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(normalized.HasValue ? new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, normalized.Value.x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, normalized.Value.y)) : rect.rect.center));
        Require(point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height, "Button center is outside the viewport: " + button.name);
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Native pointer is blocked at " + button.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "no hit"));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        if (current != null) current.pointerClicks++;
        if (rapidRepeat && button != null && button.gameObject.activeInHierarchy)
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        await UniTask.Delay(350, DelayType.Realtime, cancellationToken: cancellation?.Token ?? default);
    }
    static void CheckDuplicates()
    {
        var duplicates = UnityEngine.Object.FindObjectsByType<UILayer>(FindObjectsInactive.Exclude)
            .Where(layer => !layer.IsClosing).GroupBy(layer => layer.GetType().Name).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Require(duplicates.Length == 0, "Duplicate active production layers: " + string.Join(", ", duplicates));
        if (current != null) current.duplicateLayerChecks++;
    }
    static async UniTask ScrollScreens(UILayer layer, string name)
    {
        foreach (var scroll in layer.GetComponentsInChildren<ScrollRect>())
        {
            if (!scroll.vertical || scroll.content == null || scroll.viewport == null || scroll.content.rect.height <= scroll.viewport.rect.height + 1) continue;
            if (current != null) current.programmaticScrollEndpoints++;
            float original = scroll.verticalNormalizedPosition;
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 0;
            await Stable(); await Screenshot(name + "-scroll-bottom");
            scroll.verticalNormalizedPosition = original; await Stable(); break;
        }
    }
    static async UniTask Stable()
    {
        await UniTask.Delay(350, DelayType.Realtime, cancellationToken: cancellation?.Token ?? default);
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellation?.Token ?? default);
        Canvas.ForceUpdateCanvases();
    }
    static async UniTask Wait(Func<bool> predicate, double seconds, string purpose)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!predicate())
        {
            cancellation?.Token.ThrowIfCancellationRequested();
            if (finishing) throw new OperationCanceledException();
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Timed out waiting for " + purpose + ": " + Diagnostic());
            await UniTask.Delay(100, DelayType.Realtime, cancellationToken: cancellation?.Token ?? default);
        }
    }
    static async UniTask Screenshot(string name)
    {
        if (finishing) return;
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellation?.Token ?? default);
        var portraits = UnityEngine.Object.FindObjectsByType<HeroIcon>(FindObjectsInactive.Exclude)
            .Where(icon => icon.unitConfig != null && !string.IsNullOrEmpty(icon.unitConfig.RECORD_ID)).ToArray();
        if (portraits.Length > 0)
        {
            await Wait(() => portraits.All(icon => icon == null || Field<Image>(icon, "icon").sprite != null), 10, "portrait images");
            foreach (var portrait in portraits.Where(icon => icon != null))
            {
                var frame = Field<Image>(portrait, "frame");
                Require(frame == null || frame.sprite == null || !AssetDatabase.GetAssetPath(frame.sprite).EndsWith("PreparationButtonFill.png"),
                    "Opaque button fill covers portrait: " + portrait.name);
            }
            await Stable();
        }
        foreach (var row in UnityEngine.Object.FindObjectsByType<ResultTableNode>(FindObjectsInactive.Exclude))
        {
            var root = (RectTransform)row.transform;
            var label = Field<Text>(row, "name").rectTransform;
            var probability = Field<Text>(row, "rate").rectTransform;
            Require(!LocalBounds(root, label).Overlaps(LocalBounds(root, probability)), "Probability overlaps the skill name.");
            var holder = Field<RectTransform>(row, "iconT");
            Require(root.rect.Contains(LocalBounds(root, holder).min) && root.rect.Contains(LocalBounds(root, holder).max), "Probability gem holder leaves its row.");
        }
        foreach (var cell in UnityEngine.Object.FindObjectsByType<ProductCell>(FindObjectsInactive.Exclude))
        {
            var label = Field<Text>(cell, "price");
            Require(!string.IsNullOrEmpty(label.text) && label.text != "Not Available" && label.text != "Not", "Unlocalized shop availability label.");
        }
        string path = Path.GetFullPath(Path.Combine(Output, name + ".png"));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        await Wait(() => File.Exists(path) && new FileInfo(path).Length > 100, 5, "native screenshot " + name);
        byte[] png = File.ReadAllBytes(path);
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Require(width == Screen.width && height == Screen.height, "Screenshot dimensions differ from the live backbuffer.");
        if (current != null)
        {
            if (!current.screenshots.Contains(path)) current.screenshots.Add(path);
            var cameras = UnityEngine.Object.FindObjectsByType<DedicatedCameraConnector>(FindObjectsInactive.Exclude);
            current.captures.Add(new CaptureState { path = path, scene = SceneManager.GetActiveScene().name,
                process = ProcessesRunner.Main.currentProcess?.GetType().Name, width = width, height = height,
                activeModelPreviews = cameras.Length, loadedModelPreviews = cameras.Count(camera => camera.FocusingC != null),
                portraitCount = portraits.Length, loadedPortraits = portraits.Count(icon => icon != null && Field<Image>(icon, "icon").sprite != null) });
            if (current.observedScene == null) current.observedScene = SceneManager.GetActiveScene().name;
            if (current.observedProcess == null) current.observedProcess = ProcessesRunner.Main.currentProcess?.GetType().Name;
        }
    }
    static Rect LocalBounds(RectTransform parent, RectTransform child)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        var points = corners.Select(parent.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    static T Field<T>(object target, string name)
    {
        Require(target != null, "Field target missing: " + name);
        var field = target.GetType().GetField(name, Fields);
        Require(field != null, "Production field missing: " + target.GetType().Name + "." + name);
        return (T)field.GetValue(target);
    }
    static string Diagnostic() => SceneManager.GetActiveScene().name + " / " + ProcessesRunner.Main.currentProcess?.GetType().Name
        + " / ready=" + Ready;
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void SaveReport()
    {
        if (report == null) return;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
    }
    static void Finish(string error)
    {
        if (finishing) return;
        finishing = true;
        cancellation?.Cancel();
        report ??= new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        if (report.cases.Count == 0) foreach (var name in Inventory) report.cases.Add(new Case { name = name });
        if (!string.IsNullOrEmpty(error)) report.errors.Add(Sanitize(error));
        var logged = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(logged)) report.errors.Add(logged);
        foreach (var item in report.cases.Where(item => item.status == "running")) item.status = "failed";
        report.complete = report.cases.All(item => item.status == "passed" || item.status == "unavailable");
        report.passed = report.realAccountLogin && report.complete && report.errors.Count == 0;
        SaveReport();
        Application.logMessageReceived -= CaptureError; EditorApplication.update -= Poll;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        RestoreInt("auto"); RestoreInt("gangbangCountOption"); RestoreString("showUnit"); RestoreInt(PlayFabSetting._arenaPointCode);
        SessionState.SetBool(Key, false);
        SessionState.SetBool(Key + ".RestoreScenes", true);
        Debug.Log("[UILiveReview] " + (report.passed ? "PASS" : "INCOMPLETE/FAIL") + ": " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
    static void PreserveInt(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        SessionState.SetBool(Key + ".Prefs." + name + ".Saved", true);
        SessionState.SetBool(Key + ".Prefs." + name + ".Had", PlayerPrefs.HasKey(name));
        SessionState.SetInt(Key + ".Prefs." + name + ".Value", PlayerPrefs.GetInt(name, 0));
    }
    static void PreserveString(string name)
    {
        SessionState.SetBool(Key + ".Prefs." + name + ".Saved", true);
        SessionState.SetBool(Key + ".Prefs." + name + ".Had", PlayerPrefs.HasKey(name));
        SessionState.SetString(Key + ".Prefs." + name + ".Value", PlayerPrefs.GetString(name, ""));
    }
    static void RestoreInt(string name)
    {
        if (string.IsNullOrEmpty(name) || !SessionState.GetBool(Key + ".Prefs." + name + ".Saved", false)) return;
        if (SessionState.GetBool(Key + ".Prefs." + name + ".Had", false)) PlayerPrefs.SetInt(name, SessionState.GetInt(Key + ".Prefs." + name + ".Value", 0));
        else PlayerPrefs.DeleteKey(name);
        SessionState.SetBool(Key + ".Prefs." + name + ".Saved", false);
    }
    static void RestoreString(string name)
    {
        if (!SessionState.GetBool(Key + ".Prefs." + name + ".Saved", false)) return;
        if (SessionState.GetBool(Key + ".Prefs." + name + ".Had", false)) PlayerPrefs.SetString(name, SessionState.GetString(Key + ".Prefs." + name + ".Value", ""));
        else PlayerPrefs.DeleteKey(name);
        SessionState.SetBool(Key + ".Prefs." + name + ".Saved", false);
    }
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (SessionState.GetBool(Key, false)) Finish("Play mode was stopped before the review completed.");
        RestoreScenes();
    }
    static void RestoreScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(Key + ".RestoreScenes", false)) return;
        SessionState.SetBool(Key + ".RestoreScenes", false);
        var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".Scenes", "{}"));
        var scenes = backup?.scenes.Where(scene => !string.IsNullOrEmpty(scene.path) && File.Exists(scene.path))
            .Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
        if (scenes != null && scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(scenes);
    }
}
