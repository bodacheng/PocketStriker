using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using mainMenu;
using FightScene;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

public static partial class PocketStrikerUILiveReview
{
    static async UniTask StoreVideoRun()
    {
        report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O"),
            scope = "Four genuine Unity Recorder GameView recordings with native sound effects: strict 1v1, completed team/group quests and unsaved skill editor. In-game BGM is temporarily set to zero only for recording; continuous native lobby music will be mixed afterward. Current Editor source, not physical build39 footage.",
            limitation = "No account skill confirmation, reward/result click, progress changes, fixture, banner simulation, hidden combo or synthetic battle. Editor Fast Mode uses local assets.",
            startupSideEffects = "Ordinary existing production device login and startup behavior. Temporary preferences are restored." };
        foreach (string name in Inventory) report.cases.Add(new Case { name = name });
        cancellation = new System.Threading.CancellationTokenSource();
        string source = Path.GetFullPath("StoreCaptureSourceManifest.json");
        Require(File.Exists(source), "Source manifest missing.");
        storeManifest = new StoreCaptureManifest { sourceManifestPath = source, sourceManifestSha256 = StoreSha(File.ReadAllBytes(source)),
            language = StoreLanguage, requestedWidth = 886, requestedHeight = 1920, unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        File.Copy(source, Path.Combine(Output, "source-manifest.json"), true);
        try
        {
            var title = Layer<TitleScreenLayer>(); Require(title != null, "Production title missing.");
            report.loginMode = "Existing production device login";
            await StoreWaitForTitleLayout(title);
            await Click(Field<Button>(title, "touchScreenBtn"));
            await Wait(() => PreScene.target != null && PlayerAccountInfo.Me != null && ProcessesRunner.Main.currentProcess != null, 90, "production login");
            report.realAccountLogin = true;
            foreach (var locator in Addressables.ResourceLocators)
                storeManifest.resourceLocators.Add(new StoreResourceLocator { id = Sanitize(locator.LocatorId), type = locator.GetType().FullName });
            StoreSaveManifest();
            await DismissStartupModals();
            Require(PlayerAccountInfo.Me.tutorialProgress == "Finished", "Normal account tutorial is unfinished; no fixture or account mutation permitted.");
            await StoreSetLanguage();
            // Original memory/file settings and Editor audio state were preserved before Play.
            // These are capture-only runtime values; SaveSettings is never called.
            EditorUtility.audioMasterMute = false;
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            AppSetting.Value.BgmVolume = 0f;
            AppSetting.Value.EffectsVolume = Mathf.Max(.5f, AppSetting.Value.EffectsVolume);
            await Check("battle-duel", () => StoreRecaptureBattle("battle-duel", FightMode.Rotate, 5));
            Require(current.status == "passed", "Duel capture failed; no next encounter.");
            await Check("battle-team", () => StoreRecaptureBattle("battle-team", FightMode.Multi, 5));
            Require(current.status == "passed", "Team capture failed; no next encounter.");
            await Check("battle-group", () => StoreRecaptureBattle("battle-group", FightMode.Group, 6));
            Require(current.status == "passed", "Group capture failed; no next encounter.");
            await Check("skill-editor", StoreRecaptureSkillEditor);

        }
        catch (Exception exception) { report.errors.Add(Sanitize(exception.Message) + " | " + Diagnostic()); }
        finally
        {
            StopStoreVideoRecorder(); StoreWriteVideoManifest();
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            Finish(null);
        }
    }
    static async UniTask StoreRecaptureBattle(string name, FightMode wanted, double seconds)
    {
        int stageId = -1;
        if (wanted == FightMode.Rotate) await StorePrepareTrainingDuel();
        else
        {
            stageId = await StoreSelectCompletedMode(wanted);
            await Click(Field<Button>(Field<FightBeginBtn>(Layer<FightPrepareLayer>(), "beginFight"), "btn"));
        }
        if (Layer<PopupLayer>() != null)
        {
            var popup = Layer<PopupLayer>();
            string expected = Translate.Get(wanted == FightMode.Group ? "HasExtraSeatForGangbangButFight" : "HasExtraSeatButFight");
            Require(Field<Text>(popup, "ValidationIntro").text == expected && Field<Button>(popup, "NoButton").gameObject.activeInHierarchy,
                "Unexpected battle popup; no automatic acceptance.");
            await Click(Field<Button>(popup, "YesButton"));
        }
        await Wait(() => FSceneProcessesRunner.Main.currentProcess is FightingProcess && Layer<FightingStepLayer>() != null &&
            Layer<FightingStepLayer>().PauseButton.gameObject.activeInHierarchy && Layer<FightingStepLayer>().PauseButton.IsInteractable() && Layer<ProgressLayer>() == null,
            90, "actual running " + wanted + " encounter");
        StoreRequireLiveBattle(wanted);
        StoreObserveBattle(name + "-native-audio", wanted);
        await StoreVideoRecordClip(name, seconds, true);
        await UniTask.NextFrame();
        Require(!FightLogger.value.GameOver.Value && Layer<CommonFightResult>() == null, "Natural battle ended; recording stopped without result/reward action.");
        await Click(Layer<FightingStepLayer>().PauseButton);
        await Wait(() => Layer<FightScenePauseSupport>() != null && Time.timeScale == 0, 5, "native pause");
        await Click(PersistentButton(Layer<FightScenePauseSupport>(), "Return"));
        await Wait(() => SceneManager.GetActiveScene().name == "MainMenuScene" && PreScene.target != null && Ready, 90, "native return");
        current.note = "Real " + wanted + (stageId < 0 ? " strict 1v1 practice" : " completed Quest stage " + stageId) +
            "; native SFX recorded, runtime BGM zero, no rewards/progress/account-save action.";
    }

    static async UniTask StoreRecaptureSkillEditor()
    {
        await Home(); await Click(Tab("fighterTab")); await Page<UnitOptionLayer>(MainSceneStep.UnitList, 35);
        Require(dataAccess.Units.Dic.Count > 0, "Actual owned collection empty.");
        await Click(Field<Button>(Layer<UnitOptionLayer>(), "skillEditButton")); await Page<SkillEditLayer>(MainSceneStep.UnitSkillEdit, 45);
        var layer = Layer<SkillEditLayer>(); Require(layer.Initialized, "Skill editor not initialized."); RequirePreview(layer);
        var combo = layer.transform.Find("middle/V1/combo"); Require(combo == null || !combo.gameObject.activeInHierarchy, "Unexpected hidden combo activation.");
        var cells = layer.nineSlot.AllSlot.Where(x => x != null && x._cell != null && x._cell.GetItem() != null &&
            x._cell.btn.gameObject.activeInHierarchy && x._cell.btn.IsInteractable()).Select(x => x._cell).ToArray();
        Require(cells.Length == 9, "Skill preview requires nine normally equipped stones.");
        var savedIds = layer.nineSlot.AllSlot.Select(x => x._cell.GetItem()?.instanceId).ToArray();
        var recording = StoreVideoRecordClip("skill-editor", 13, false).Preserve();
        bool reset = false;
        try
        {
            await UniTask.Delay(750, DelayType.Realtime);
            await Click(cells[0].btn); StoreVideoNoteAction("native-nine-slot-selected");
            await UniTask.Delay(750, DelayType.Realtime);
            var replacement = layer.stonesBox.GetComponentsInChildren<StoneCell>().FirstOrDefault(x => x != null && x.GetItem() != null &&
                x.GetItem()._SkillConfig != null && !savedIds.Contains(x.GetItem().instanceId) && StoreVideoButtonVisible(x.btn));
            Require(replacement != null, "No visible naturally owned inventory replacement stone.");
            var replacementId = replacement.GetItem().instanceId;
            await Click(replacement.btn, true);
            Require(layer.nineSlot.AllSlot[0]._cell.GetItem()?.instanceId == replacementId, "Native inventory double click did not install its stone.");
            StoreVideoNoteAction("native-stone-replacement-unsaved");
            await UniTask.Delay(1000, DelayType.Realtime);
            await Click(layer.nineSlot.ResetButton);
            await Wait(() => layer.nineSlot.AllSlot.Select(x => x._cell.GetItem()?.instanceId).SequenceEqual(savedIds), 5, "native reset reloads all nine saved stones");
            reset = true; StoreVideoNoteAction("native-reset-unsaved-edit");
            for (int i = 0; i < 3; i++)
            {
                await Click(layer.nineSlot.AllSlot[i]._cell.btn); StoreVideoNoteAction("native-equipped-skill-selection-" + (i + 1));
                await UniTask.Delay(2300, DelayType.Realtime);
            }
            await recording;
        }
        finally
        {
            StopStoreVideoRecorder();
            if (!reset && layer != null && layer.nineSlot != null && StoreVideoButtonVisible(layer.nineSlot.ResetButton))
                await Click(layer.nineSlot.ResetButton);
        }
        Require(reset && layer.nineSlot.AllSlot.Select(x => x._cell.GetItem()?.instanceId).SequenceEqual(savedIds), "Native skill reset was not verified; no save/confirm action.");
        current.note = "Actual native unsaved replacement, reset of all nine saved stones, and skill animation/SFX. No confirmation, account save, hidden combo or inventory mutation.";
    }

    static bool StoreVideoButtonVisible(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable() || EventSystem.current == null) return false;
        var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        if (point.x < 1 || point.y < 1 || point.x >= Screen.width - 1 || point.y >= Screen.height - 1) return false;
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        return hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject;
    }

}
