using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Temporary capture-only harness in the isolated /tmp checkout.
// Navigation, encounters, teams, combat and pixels remain production behavior.
public static partial class PocketStrikerUILiveReview
{
    [Serializable] sealed class StoreBattleObservation
    {
        public string filename, capturedUtc, eventType, stageId, mode, battleGroundId;
        public int loadedHeroes, loadedEnemies, activeHeroes, activeEnemies;
        public bool realAccountLogin, gameOver, actualFightingProcess, groupBattle, team1Auto;
    }

    static async UniTask StoreBattleRun()
    {
        report = new Report
        {
            unityVersion = Application.unityVersion,
            utcTime = DateTime.UtcNow.ToString("O"),
            scope = "Three actual completed Adventure battle modes through visible native stage-list navigation. Rendered Editor frames, final local source, not build39 physical-device captures.",
            limitation = "Rotation shows one active fighter per side with reserves. Team and Group are actual authored encounters. No fixture, hidden entry, mode override, purchase, reward action, account edit, team edit, skill edit, AUTO toggle or group-count adjustment.",
            startupSideEffects = "Normal production device login/check-in/team sanitation can run. Adventure can start normal asynchronous AI story prefetch; native return cancels the client request. No synthetic server data or credential values are accessed by this harness."
        };
        foreach (var name in Inventory) report.cases.Add(new Case { name = name });
        cancellation = new System.Threading.CancellationTokenSource();
        string source = Path.GetFullPath("StoreCaptureSourceManifest.json");
        Require(File.Exists(source), "Source provenance manifest missing.");
        storeManifest = new StoreCaptureManifest
        {
            sourceManifestPath = source, sourceManifestSha256 = StoreSha(File.ReadAllBytes(source)),
            language = StoreLanguage,
            requestedWidth = SessionState.GetInt(StoreKey + ".Width", 0),
            requestedHeight = SessionState.GetInt(StoreKey + ".Height", 0),
            unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O")
        };
        File.Copy(source, Path.Combine(Output, "source-manifest.json"), true);
        try
        {
            var title = Layer<TitleScreenLayer>();
            Require(title != null, "Production title missing.");
            report.loginMode = "Production title device login, no credentials read or reported";
            await StoreWaitForTitleLayout(title);
            await Click(Field<Button>(title, "touchScreenBtn"));
            await Wait(() => PreScene.target != null && PlayerAccountInfo.Me != null && ProcessesRunner.Main.currentProcess != null,
                90, "production login/main scene");
            report.realAccountLogin = true;
            foreach (var locator in UnityEngine.AddressableAssets.Addressables.ResourceLocators)
                storeManifest.resourceLocators.Add(new StoreResourceLocator { id = Sanitize(locator.LocatorId), type = locator.GetType().FullName });
            StoreSaveManifest();
            await DismissStartupModals();
            Require(PlayerAccountInfo.Me.tutorialProgress == "Finished", "Actual onboarding unfinished; no fixture or tutorial mutation is allowed.");
            await StoreSetLanguage();
            await Check("battle-1v1", () => StoreCaptureNaturalBattle("battle-1v1", FightMode.Rotate));
            Require(current.status == "passed", "Rotation capture failed; stop before another encounter.");
            await Check("battle-team", () => StoreCaptureNaturalBattle("battle-team", FightMode.Multi));
            Require(current.status == "passed", "Team capture failed; stop before another encounter.");
            await Check("battle-group", () => StoreCaptureNaturalBattle("battle-group", FightMode.Group));
            Require(current.status == "passed", "Group capture failed.");
        }
        catch (Exception exception) { report.errors.Add(Sanitize(exception.Message) + " | " + Diagnostic()); }
        finally { cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null; Finish(null); }
    }

    static async UniTask<int> StoreSelectCompletedMode(FightMode wanted)
    {
        await Home();
        await Click(Field<LowerBarIcon>(Layer<FrontLayer>(), "ArcadeBtn").BOButton);
        await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 45);
        await Click(Field<Button>(Layer<FightPrepareLayer>(), "toArcadeFrontBtn"));
        await Page<ArcadeTop>(MainSceneStep.ArcadeFront, 45);
        for (int pageIndex = 0; pageIndex < 45; pageIndex++)
        {
            var page = Layer<ArcadeTop>();
            var stageButtons = page.GetComponentsInChildren<StageButton>()
                .Where(stage => stage.StageNo <= PlayerAccountInfo.Me.arcadeProcess && !AdventureModeRules.IsTutorialStage(stage.StageNo.ToString()))
                .OrderByDescending(stage => stage.StageNo).ToArray();
            foreach (var stageButton in stageButtons)
            {
                FightInfo loaded = await ArcadeModeManager.Instance.LoadStage(stageButton.StageNo);
                bool match = loaded != null && loaded.EventType == FightEventType.Quest && loaded.FightMode == wanted;
                if (loaded != null && !ReferenceEquals(loaded, FightLoad.Fight)) UnityEngine.Object.Destroy(loaded);
                if (!match) continue;
                StageButton visible; Vector2 pointer;
                bool canClick = StoreTryVisibleCompletedStage(page, out visible, out pointer, stageButton.StageNo);
                if (!canClick)
                {
                    // Real ScrollRect wheel events only: no card, mask or raycast edits.
                    var scroll = page.GetComponentsInChildren<ScrollRect>().FirstOrDefault(item => item.vertical && item.viewport != null && item.isActiveAndEnabled);
                    if (scroll != null)
                    {
                        for (int wheel = 0; wheel < 8 && !canClick; wheel++)
                        {
                            Canvas.ForceUpdateCanvases();
                            var canvas = scroll.GetComponentInParent<Canvas>().rootCanvas;
                            var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                                scroll.viewport.TransformPoint(scroll.viewport.rect.center));
                            var data = new PointerEventData(EventSystem.current) { position = point, scrollDelta = new Vector2(0, wheel < 4 ? -2 : 2) };
                            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                            Require(hits.Count > 0, "Stage scrolling has no native hit.");
                            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.scrollHandler);
                            await Stable();
                            canClick = StoreTryVisibleCompletedStage(page, out visible, out pointer, stageButton.StageNo);
                        }
                    }
                }
                if (!canClick) continue;
                int stageId = visible.StageNo;
                await Click(visible.Button, false, pointer);
                await Page<FightPrepareLayer>(MainSceneStep.QuestInfo, 45);
                Require(FightLoad.Fight != null && FightLoad.Fight.EventType == FightEventType.Quest &&
                    FightLoad.Fight.ID == stageId.ToString() && FightLoad.Fight.FightMode == wanted,
                    "Native preparation does not match the completed authored encounter.");
                return stageId;
            }
            var last = Field<Button>(page, "lastChapter");
            Require(last != null && last.gameObject.activeInHierarchy && last.IsInteractable(), "No completed " + wanted + " encounter is accessible by visible stage navigation.");
            int priorMinimum = page.GetComponentsInChildren<StageButton>().Min(stage => stage.StageNo);
            await Click(last);
            await Wait(() => Ready && Layer<ArcadeTop>() != null && Layer<ArcadeTop>().GetComponentsInChildren<StageButton>()
                .Any(stage => stage.StageNo < priorMinimum), 45, "native previous chapter");
            await Page<ArcadeTop>(MainSceneStep.ArcadeFront, 45);
        }
        throw new TimeoutException("No natural completed stage found within the stage-list bound.");
    }

    static void StoreRequireLiveBattle(FightMode wanted)
    {
        Require(FightLoad.Fight != null && FightLoad.Fight.EventType == FightEventType.Quest && FightLoad.Fight.FightMode == wanted,
            "Runtime encounter mode differs from authored preparation.");
        Require(FSceneProcessesRunner.Main.currentProcess is FightingProcess && !FightLogger.value.GameOver.Value &&
            Layer<CommonFightResult>() == null && Layer<PopupLayer>() == null && Layer<ProgressLayer>() == null,
            "No active undecided production battle frame; no result or reward action is dispatched.");
        Require(Layer<FightingStepLayer>() != null && Layer<FightingStepLayer>().PauseButton.IsInteractable(), "Battle pause is unavailable.");
    }

    static StoreBattleObservation StoreObserveBattle(string filename, FightMode wanted)
    {
        StoreRequireLiveBattle(wanted);
        var manager = RTFightManager.Target;
        Require(manager != null, "Runtime teams missing.");
        var observed = new StoreBattleObservation
        {
            filename = filename, capturedUtc = DateTime.UtcNow.ToString("O"), eventType = FightLoad.Fight.EventType.ToString(),
            stageId = FightLoad.Fight.ID, mode = FightLoad.Fight.FightMode.ToString(), battleGroundId = FightLoad.Fight.battleGroundID.ToString(),
            loadedHeroes = manager.team1.teamMembers.GetValues().Count, loadedEnemies = manager.team2.teamMembers.GetValues().Count,
            activeHeroes = manager.team1.GetFightingUnitTs().Count, activeEnemies = manager.team2.GetFightingUnitTs().Count,
            realAccountLogin = report.realAccountLogin, gameOver = FightLogger.value.GameOver.Value,
            actualFightingProcess = FSceneProcessesRunner.Main.currentProcess is FightingProcess,
            groupBattle = FightLoad.Fight.IsGroupBattle, team1Auto = FightLoad.Fight.Team1Auto
        };
        if (wanted == FightMode.Rotate) Require(observed.activeHeroes == 1 && observed.activeEnemies == 1, "Rotation frame is not one active fighter per side.");
        if (wanted == FightMode.Multi) Require(observed.activeHeroes >= 2 && observed.activeEnemies >= 2 && !observed.groupBattle, "Team frame lacks simultaneous fighters on both sides.");
        if (wanted == FightMode.Group) Require(observed.groupBattle && observed.activeHeroes + observed.activeEnemies > 6 &&
            Math.Max(observed.activeHeroes, observed.activeEnemies) > 3, "Group frame lacks the actual expanded battlefield population.");
        return observed;
    }

    static async UniTask StoreCaptureNaturalBattle(string name, FightMode wanted)
    {
        int stageId = await StoreSelectCompletedMode(wanted);
        await Click(Field<Button>(Field<FightBeginBtn>(Layer<FightPrepareLayer>(), "beginFight"), "btn"));
        if (Layer<PopupLayer>() != null)
        {
            var popup = Layer<PopupLayer>();
            var text = Field<Text>(popup, "ValidationIntro").text;
            string expected = Translate.Get(wanted == FightMode.Group ? "HasExtraSeatForGangbangButFight" : "HasExtraSeatButFight");
            Require(text == expected && Field<Button>(popup, "NoButton").gameObject.activeInHierarchy,
                "Unexpected battle popup; do not automatically accept it.");
            await Click(Field<Button>(popup, "YesButton"));
        }
        await Wait(() => FSceneProcessesRunner.Main.currentProcess is FightingProcess && Layer<FightingStepLayer>() != null &&
            Layer<FightingStepLayer>().PauseButton.gameObject.activeInHierarchy && Layer<FightingStepLayer>().PauseButton.IsInteractable() && Layer<ProgressLayer>() == null,
            90, "actual running " + wanted + " encounter");
        var textures = new List<Texture2D>();
        var observations = new List<StoreBattleObservation>();
        try
        {
            for (int candidate = 1; candidate <= 3; candidate++)
            {
                await UniTask.Delay(300, DelayType.Realtime, cancellationToken: cancellation.Token);
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellation.Token);
                string filename = name + "-" + candidate + ".png";
                var observed = StoreObserveBattle(filename, wanted);
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                Require(texture != null && texture.width == Screen.width && texture.height == Screen.height &&
                    texture.width == SessionState.GetInt(StoreKey + ".Width", 0) && texture.height == SessionState.GetInt(StoreKey + ".Height", 0),
                    "Rendered frame dimensions differ from requested ASC size.");
                textures.Add(texture); observations.Add(observed);
            }
            StoreRequireLiveBattle(wanted);
            await Click(Layer<FightingStepLayer>().PauseButton);
            await Wait(() => Layer<FightScenePauseSupport>() != null && Time.timeScale == 0, 5, "native battle pause before PNG encoding");
            Require(!FightLogger.value.GameOver.Value && Layer<CommonFightResult>() == null, "Battle ended before native pause; no files are selected as store evidence.");
            for (int index = 0; index < textures.Count; index++)
            {
                string path = Path.Combine(Output, observations[index].filename);
                byte[] png = textures[index].EncodeToPNG();
                File.WriteAllBytes(path, png);
                storeManifest.battleFrames.Add(observations[index]);
                StoreRecordScreenshot(path, textures[index].width, textures[index].height);
                current.screenshots.Add(path);
                current.captures.Add(new CaptureState { path = path, scene = SceneManager.GetActiveScene().name,
                    process = "FightingProcess (pixels captured before native pause)", width = textures[index].width, height = textures[index].height });
            }
            await Click(PersistentButton(Layer<FightScenePauseSupport>(), "Return"));
            await Wait(() => SceneManager.GetActiveScene().name == "MainMenuScene" && PreScene.target != null && Ready,
                90, "native return after battle screenshots");
            current.note = "Actual completed Quest stage " + stageId + "; runtime " + wanted +
                "; three live frames grabbed before native pause; PNG encoded while paused, then native Return. No mode/team/AUTO/count edits, hidden entry or result/reward action. Rotation has one active fighter per side and may have reserves.";
        }
        finally { foreach (var texture in textures) if (texture != null) UnityEngine.Object.Destroy(texture); }
    }

    static async UniTask StorePrepareTrainingDuel()
    {
        await Home();
        await Click(Field<Button>(Layer<FrontLayer>(), "TrainBtn"));
        await Page<SelfFightLayer>(MainSceneStep.SelfFightFront, 45);
        var self = Layer<SelfFightLayer>();
        var icons = Layer<UnitsLayer>().GetComponentsInChildren<HeroIcon>()
            .Where(icon => icon.InstanceID != null && dataAccess.Units.Get(icon.InstanceID) != null &&
                dataAccess.Stones.GetEquippingStones(icon.InstanceID).Count == 9 && icon.iconButton != null && icon.iconButton.IsInteractable())
            .OrderBy(icon => icon.unitConfig?.RECORD_ID).ToArray();
        Require(icons.Length >= 1, "No actually owned fully equipped fighter can enter a natural practice duel.");
        await Click(icons[0].iconButton);
        await Click(Field<HeroCell>(self, "team11_R").iconButton);
        await Click(icons[0].iconButton);
        await Click(Field<HeroCell>(self, "team21_R").iconButton);
        var mode = Field<FightModeSwitch>(self, "_fightModeSwitch");
        if (mode.TeamMode != TeamMode.Rotation)
        {
            await Click(Field<Button>(mode, "btn"));
            Require(mode.TeamMode == TeamMode.Rotation, "Native practice mode switch did not select rotation.");
        }
        var stage = Field<FightInfo>(self, "_stage");
        Require(stage.EventType == FightEventType.Self && stage.FightMembers.HeroSets.GetValues().Count == 1 &&
            stage.FightMembers.EnemySets.GetValues().Count == 1 && stage.FightMembers.CheckStonesLegal(FightEventType.Self),
            "Native temporary practice selections do not form a legal strict 1v1.");
        await Click(Field<Button>(Field<FightBeginBtn>(self, "fightStartBtn"), "btn"));
    }
}
