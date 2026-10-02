using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using DummyLayerSystem;
using mainMenu;
using Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>First-account skill editing and tutorial transitions with no account/network calls.</summary>
public static class PocketStrikerPreBattleTutorialValidation
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
    const string AccountId = "local-tutorial-prebattle";
    const string OtherId = "local-tutorial-prebattle-other";
    const string PrefabPath = "Assets/Resources/DummyLayerSystem/SkillEditLayer.prefab";
    const string ReportPath = "Logs/Tutorial/prebattle.json";
    static readonly string[] Guides = { "spStoneGuide1", "spStoneGuide2", "spStoneGuide3", "spStoneGuide4", "spStoneGuide5", "spStoneGuide6" };

    [Serializable] public sealed class Report
    {
        public bool passed;
        public int dragTargets, movingTargets, instructionStates, invalidWarnings, fullInvalidChecks, autoRepairChecks, transitions, persistenceChecks;
        public bool markerFailureAdvances, endedCallbackIgnored, pointerStopsWhenHidden, sourcePrefabUnchanged;
        public string scope = "Copied authored SkillEditLayer transforms/UI and actual SkillStonesBox category filtering, TheNineSlot validation/placement, SkillEditLayer guidance and Auto Fill candidate/clear caller, SkillEditTry equipment-success callback, GoTo/OpenSkillEdit readiness and TutorialRunner chain. Isolated account keys verify account separation and stale-save clearing. Twelve owned local stones reproduce normal/EX1/EX2/EX3 steps, full invalid-set repair and confirm guidance.";
        public string limitation = "Stopped-editor UI/component fixture; equipment success is supplied after the callback's normal cloud-save boundary. Auto Fill uses the actual planning, clear and fill methods; only slot effects and secondary HP/effect presentation are overridden. The production validator and inventory refresh run against the filled slot components. No network, currencies, model loading, full scene startup or natural combat. Existing Skill UI, battle tutorial and combat-flow validations cover layout, raycasts and battle separately.";
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Pre-battle Tutorial")]
    public static void Validate() { var report = Run(); Debug.Log("[PreBattleTutorial] " + (report.passed ? "PASS" : "FAIL")); }
    public static void ValidateBatch() { var report = Run(); Debug.Log("[PreBattleTutorial] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(ReportPath)); EditorApplication.Exit(report.passed ? 0 : 1); }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before pre-battle validation.");
        var report = new Report();
        var sourceText = File.ReadAllText(PrefabPath);
        var account = PlayerAccountInfo.Me; var settings = AppSetting.Value;
        var pre = PreScene.target; var canvas = PosCal.Canvas; var safe = PosCal.SafeAreaRect;
        var selected = SkillStonesBox.Selected; var current = ProcessesRunner.Main.currentProcess;
        var randomState = UnityEngine.Random.state;
        var configs = SkillConfigTable.SkillConfigRefDic; var translations = Translate.GetRowList().ToArray();
        var stoneData = (IDictionary<string, dataAccess.StoneOfPlayerInfo>)typeof(dataAccess.Stones).GetField("Dic", StaticFields).GetValue(null);
        var stoneModels = (IDictionary<string, SKStoneItem>)typeof(dataAccess.Stones).GetField("RenderModelDic", StaticFields).GetValue(null);
        var savedData = stoneData.ToArray(); var savedModels = stoneModels.ToArray();
        const string unitId = "local-tutorial-prebattle-unit";
        var oldUnit = dataAccess.Units.Get(unitId); var oldUnitConfig = Units.GetUnitConfig(unitId);
        var prefKeys = new[] { "PENDING_TUTORIAL_PROGRESS:" + AccountId, "PENDING_TUTORIAL_PROGRESS:" + OtherId, "DontShowFrontFight" };
        var prefValues = prefKeys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
        var preview = EditorSceneManager.NewPreviewScene();
        Application.LogCallback errors = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack); };
        Application.logMessageReceived += errors;
        try
        {
            PlayerAccountInfo.Me = new PlayerAccountInfo { PlayFabId = AccountId, tutorialProgress = "Started" };
            AppSetting.Value = new AppSetting { Language = SystemLanguage.English };
            foreach (var key in prefKeys.Take(2)) PlayerPrefs.DeleteKey(key);
            stoneData.Clear(); stoneModels.Clear();
            SkillConfigTable.SkillConfigRefDic = new Dictionary<string, SkillConfig>();
            Translate.GetRowList().Clear();
            foreach (var row in CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv")).Skip(1))
                if (row.Length >= 4) Translate.GetRowList().Add(new Translate.Row { RECORD_ID = row[0], EN = row[1], JP = row[2], CH = row[3] });
            var rig = new GameObject("Pre-battle Tutorial Fixture"); rig.SetActive(false); SceneManager.MoveGameObjectToScene(rig, preview);
            var canvasRoot = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); canvasRoot.transform.SetParent(rig.transform, false);
            PosCal.Canvas = canvasRoot.GetComponent<Canvas>(); PosCal.Canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasRoot.transform; canvasRect.sizeDelta = new Vector2(1080, 1920); PosCal.SafeAreaRect = canvasRect;
            var preRoot = new GameObject("Inactive PreScene"); preRoot.SetActive(false); preRoot.transform.SetParent(rig.transform, false);
            PreScene.target = preRoot.AddComponent<PreScene>();
            PreScene.target.stonesTempContainer = (RectTransform)new GameObject("Stone Temp", typeof(RectTransform)).transform;
            PreScene.target.stonesTempContainer.SetParent(rig.transform, false);
            var hero = new UnitInfo { id = unitId, r_id = unitId };
            dataAccess.Units.Dic[unitId] = hero; Units.Dic[unitId] = new UnitConfig { RECORD_ID = unitId, TYPE = "human" };
            Set(PreScene.target, "_focusing", hero);
            ProcessesRunner.Main.currentProcess = new ReadinessPage { Step = MainSceneStep.UnitSkillEdit };
            var map = new Dictionary<Object, Object>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = CopyHierarchy(source.transform, canvasRoot.transform, map);
            foreach (var original in source.GetComponentsInChildren<Component>(true))
            {
                if (!CopyComponent(original)) continue;
                var copy = ((Transform)map[original.transform]).gameObject.AddComponent(original.GetType());
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(original), copy); map[original] = copy;
            }
            foreach (var pair in map.ToArray()) if (pair.Value is Component component && !(component is Transform)) Remap(component, map);
            var layer = root.gameObject.AddComponent<PocketStrikerPreBattleTutorialValidationLayer>();
            var originalLayer = source.GetComponent<SkillEditLayer>();
            foreach (var name in Guides.Concat(new[] { "clickAutoEditIndicator", "clickAutoEditIndicator2", "mask" }))
                Set(layer, name, map[Get<GameObject>(originalLayer, name)]);
            layer.nineSlot = (TheNineSlot)map[originalLayer.nineSlot]; layer.stonesBox = (SkillStonesBox)map[originalLayer.stonesBox];
            foreach (var name in new[] { "top", "middle", "bottom" }) Set(layer, name, map[Get<Transform>(originalLayer, name)]);
            layer.Initialized = true; var nine = layer.nineSlot; var box = layer.stonesBox;
            nine.StartUp(_ => { }); box.FocusingType = "human"; box.IniExTabs();
            SkillStonesBox.Selected = Get<GameObject>(box, "selectedFrame");
            for (var i = 0; i < 12; i++)
            {
                var sp = i < 3 ? i + 1 : 0;
                var id = "prebattle-stone-" + i;
                var config = new SkillConfig { RECORD_ID = id, REAL_NAME = id, TYPE = "human", SP_LEVEL = sp, AIAttrs = new AIAttrs { AI_MIN_DIS = 0, AI_MAX_DIS = 20 } };
                SkillConfigTable.SkillConfigRefDic[id] = config;
                stoneData[id] = new dataAccess.StoneOfPlayerInfo { InstanceId = id, SkillId = id, Born = "false", Level = 1 };
                var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/BasicSprites/stoneModel.prefab"), PreScene.target.stonesTempContainer, false).GetComponent<SKStoneItem>();
                item.instanceId = id; item._SkillConfig = config; stoneModels[id] = item;
            }
            box.GenerateCells(9); rig.SetActive(true); layer.ResizeAreas();
            var equipped = new List<SKStoneItem>();
            foreach (var step in Enumerable.Range(0, 4))
            {
                layer.ExtraTipForSpStoneEquip();
                Require(Guides.Select(name => Get<GameObject>(layer, name).activeSelf).SequenceEqual(Enumerable.Range(0, 6).Select(index => index == step)), "Incorrect drag guide for step " + step);
                Require(box.SelectedSpLevel == step && box.ScrollRect.vertical, "Tutorial category is incorrect or inventory scrolling was disabled.");
                var pointer = Get<GameObject>(layer, Guides[step]).GetComponentInChildren<SkillEditTutorial11>(true);
                var from = Get<RectTransform>(pointer, "startPoint"); var to = Get<RectTransform>(pointer, "endPoint");
                var sourceCell = from != null ? from.GetComponent<StoneCell>() : null; var targetCell = to != null ? to.GetComponent<StoneCell>() : null;
                Require(sourceCell != null && sourceCell.GetItem()?._SkillConfig.SP_LEVEL == step, "Drag pointer does not start on an actual visible stone.");
                Require(targetCell != null && targetCell.gameObject.activeSelf && targetCell.GetItem() == null, "Drag pointer targets an occupied/hidden slot.");
                var slot = nine.GetSlotByCell(targetCell);
                Require(step == 0 ? (slot.num - 1) % 3 == 0 : (slot.num - 1) % 3 != 0, "Pointer does not preserve a normal first-column opener.");
                Invoke(pointer, "OnEnable");
                var tween = Get<Tween>(pointer, "moveTween"); var duration = Get<float>(pointer, "moveDuration");
                var fromPosition = from.position; var toPosition = to.position;
                from.position += Vector3.right * 37; to.position += Vector3.down * 19;
                tween.Goto(duration * .5f);
                Require(Vector3.Distance(Get<RectTransform>(pointer, "targetUIElement").position, Vector3.Lerp(from.position, to.position, .5f)) < .1f,
                    "Drag demonstration stops following its actual targets after scrolling/resizing."); report.movingTargets++;
                from.position = fromPosition; to.position = toPosition;
                foreach (var graphic in Get<GameObject>(layer, Guides[step]).GetComponentsInChildren<Graphic>(true)) Require(!graphic.raycastTarget, "A tutorial decoration intercepts drag/drop input.");
                report.dragTargets++; report.instructionStates++;
                var item = sourceCell.GetItem(); targetCell.AddItem(item); equipped.Add(item); sourceCell.UpdateMyItem();
                // Production OnDisable kills any active pointer tween when this guide is replaced.
                pointer.gameObject.SetActive(false); Invoke(pointer, "OnDisable");
                Require(Get<Tween>(pointer, "moveTween") == null, "Hidden pointer kept animating."); report.pointerStopsWhenHidden = true;
            }
            layer.ExtraTipForSpStoneEquip();
            Require(Get<GameObject>(layer, "spStoneGuide5").activeSelf && Get<GameObject>(layer, "clickAutoEditIndicator").activeSelf && !nine.confirmBtnIndicator.activeSelf, "Partial tutorial set does not point to Auto Fill."); report.instructionStates++;
            foreach (var slot in nine.GetEmptySlots().ToArray())
            {
                var item = stoneModels.Values.First(model => model._SkillConfig.SP_LEVEL == 0 && !equipped.Contains(model)); slot._cell.AddItem(item); equipped.Add(item);
            }
            layer.ExtraTipForSpStoneEquip();
            Require(nine.ValidateWarn() == SkillSet.SkillEditError.Perfect && Get<GameObject>(layer, "spStoneGuide6").activeSelf && nine.confirmBtnIndicator.activeSelf
                && !Get<GameObject>(layer, "clickAutoEditIndicator").activeSelf, "Legal set did not replace Auto Fill with Confirm."); report.instructionStates++;
            foreach (var error in new[] { SkillSet.SkillEditError.UnBalanced, SkillSet.SkillEditError.NoNormalStart, SkillSet.SkillEditError.NoAtLeastTwoEx, SkillSet.SkillEditError.RepeatedSkill })
            {
                nine.ValidationWarn(error); var warning = Get<Text>(nine, "validationWarn");
                Require(warning.gameObject.activeSelf && !string.IsNullOrEmpty(warning.text), "New-account illegal set has no explanatory warning: " + error); report.invalidWarnings++;
            }
            nine.ValidationWarn(SkillSet.SkillEditError.Perfect); Require(!Get<Text>(nine, "validationWarn").gameObject.activeSelf, "A repaired set keeps an obsolete warning.");
            // A full set with EX-only openers has no empty slot to drag into.
            // It must expose a repair action instead of locking onto step zero.
            foreach (var slot in nine.AllSlot) slot._cell.RemoveToTemp();
            var normalItems = equipped.Where(item => item._SkillConfig.SP_LEVEL == 0).ToArray(); var normalIndex = 0;
            foreach (var slot in nine.AllSlot)
                slot._cell.AddItem((slot.num - 1) % 3 == 0 ? stoneModels["prebattle-stone-" + ((slot.num - 1) / 3)] : normalItems[normalIndex++]);
            Require(nine.ValidateWarn() == SkillSet.SkillEditError.NoNormalStart, "Full-invalid fixture does not reproduce missing normal openers.");
            layer.ExtraTipForSpStoneEquip();
            Require(Get<GameObject>(layer, "spStoneGuide5").activeSelf && Get<GameObject>(layer, "clickAutoEditIndicator").activeSelf
                && !Get<GameObject>(layer, "spStoneGuide1").activeSelf && !nine.confirmBtnIndicator.activeSelf
                && Get<Text>(nine, "validationWarn").gameObject.activeSelf && nine.AllSlot.All(slot => slot._cell.gameObject.activeSelf),
                "Full-invalid new account has no actionable repair or leaves its slots hidden."); report.fullInvalidChecks++;
            // Run the real Auto Fill caller through its owned-stone repair plan.
            // The fixture suppresses slot effects/secondary HP presentation,
            // and records the live cells before calling actual production fill.
            typeof(SkillEditLayer).GetMethod("FinishRemains", Fields).Invoke(layer, null);
            Require(layer.AutoFillSet != null && layer.OccupiedBeforeAutoFill == 0 && layer.AutoFillSet.CheckEdit() == SkillSet.SkillEditError.Perfect,
                "Auto Fill did not clear occupied invalid cells before applying its legal replacement candidate."); report.autoRepairChecks++;
            var repairedIds = layer.AutoFillSet.SkillIDList();
            Require(nine.GetCurrentNineSlotAllSkillIds().SequenceEqual(repairedIds) && nine.ValidateWarn() == SkillSet.SkillEditError.Perfect,
                "Production Auto Fill did not apply its legal candidate to all nine slot components."); report.autoRepairChecks++;
            CheckSuccessCallback(layer, report);
            CheckPersistence(report);
            CheckTransitions(report);
            report.sourcePrefabUnchanged = File.ReadAllText(PrefabPath) == sourceText; Require(report.sourcePrefabUnchanged, "Validation changed the authored prefab.");
            report.checks.Add("Normal/EX1/EX2/EX3 pointers start on visible inventory stones, target sensible empty slots, and do not consume drag input.");
            report.checks.Add("Auto Fill/Confirm switch, illegal-set explanations, marker-failure continuation and pointer/callback cleanup verified.");
            report.checks.Add("Tutorial navigation waits for page initialization; account progress keys and stale saves remain isolated.");
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            stoneData.Clear(); foreach (var entry in savedData) stoneData.Add(entry.Key, entry.Value);
            stoneModels.Clear(); foreach (var entry in savedModels) stoneModels.Add(entry.Key, entry.Value);
            SkillConfigTable.SkillConfigRefDic = configs; Translate.GetRowList().Clear(); Translate.GetRowList().AddRange(translations);
            if (oldUnit == null) dataAccess.Units.Dic.Remove(unitId); else dataAccess.Units.Dic[unitId] = oldUnit;
            if (oldUnitConfig == null) Units.Dic.Remove(unitId); else Units.Dic[unitId] = oldUnitConfig;
            PlayerAccountInfo.Me = account; AppSetting.Value = settings; PreScene.target = pre; PosCal.Canvas = canvas; PosCal.SafeAreaRect = safe;
            SkillStonesBox.Selected = selected; ProcessesRunner.Main.currentProcess = current;
            UnityEngine.Random.state = randomState;
            for (var index = 0; index < prefKeys.Length; index++) { if (prefValues[index] == null) PlayerPrefs.DeleteKey(prefKeys[index]); else PlayerPrefs.SetString(prefKeys[index], prefValues[index]); } PlayerPrefs.Save();
            Application.logMessageReceived -= errors;
        }
        report.passed = report.errors.Count == 0 && report.dragTargets == 4 && report.movingTargets == 4 && report.instructionStates == 6 && report.invalidWarnings == 4 && report.fullInvalidChecks == 1 && report.autoRepairChecks == 2
            && report.markerFailureAdvances && report.endedCallbackIgnored && report.pointerStopsWhenHidden && report.sourcePrefabUnchanged;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); return report;
    }

    static void CheckSuccessCallback(SkillEditLayer layer, Report report)
    {
        PlayerAccountInfo.Me.tutorialProgress = "GotchaFinished";
        var process = new SkillEditTry("openInstruction2");
        var saves = 0; Set(process, "_skillEditLayer", layer); Set(process, "_saveProgress", (Action<string>)(_ => saves++));
        process.ProcessEnter(); process.LocalUpdate();
        var callback = Get<Action>(layer.nineSlot, "extraSkillEditSuccess"); Require(callback != null, "The initialized skill page has no tutorial success callback.");
        callback(); callback();
        Require(process.CanEnterOtherProcess() && PlayerAccountInfo.Me.tutorialProgress == "SkillEditFinished2" && PlayFabReadClient.GetPendingTutorialProgress() == "SkillEditFinished2" && saves == 1,
            "Failed/unacknowledged marker save blocks the equipped account or duplicates completion."); report.markerFailureAdvances = true;
        Require(Guides.All(name => !Get<GameObject>(layer, name).activeSelf) && !layer.nineSlot.confirmBtnIndicator.activeSelf
            && !Get<GameObject>(layer, "clickAutoEditIndicator").activeSelf && !Get<GameObject>(layer, "clickAutoEditIndicator2").activeSelf, "Success leaves obsolete tutorial indicators.");
        Require(layer.nineSlot.AllSlot.All(slot => slot._cell.gameObject.activeSelf), "Success leaves combo slots hidden.");
        foreach (var name in new[] { "closeCheckBox", "nearCheckBox", "farCheckBox" }) Require(Get<Toggle>(layer.stonesBox, name).gameObject.activeSelf, "Tutorial cleanup did not restore inventory filters.");
        process.ProcessEnd(); callback(); Require(saves == 1 && Get<Action>(layer.nineSlot, "extraSkillEditSuccess") == null && Get<Action>(layer.nineSlot, "_extraOnNineSlotChanged") == null,
            "Ended tutorial callback can still alter progress."); report.endedCallbackIgnored = true;
    }

    static void CheckPersistence(Report report)
    {
        PlayFabReadClient.RememberPendingTutorialProgress("SkillEditFinished2"); report.persistenceChecks++;
        PlayerAccountInfo.Me = new PlayerAccountInfo { PlayFabId = OtherId, tutorialProgress = "Started" };
        Require(PlayFabReadClient.GetPendingTutorialProgress() == null, "Another account's pending marker skips a new account's tutorial."); report.persistenceChecks++;
        PlayFabReadClient.RememberPendingTutorialProgress("SkillEditFinished");
        PlayFabReadClient.ClearPendingTutorialProgress("SkillEditFinished2", AccountId);
        Require(PlayFabReadClient.GetPendingTutorialProgress() == "SkillEditFinished", "Old account's completed save clears the new account's marker."); report.persistenceChecks++;
        PlayerAccountInfo.Me = new PlayerAccountInfo { PlayFabId = AccountId, tutorialProgress = "Started" };
        PlayFabReadClient.RememberPendingTutorialProgress("StageOneFinished"); PlayFabReadClient.ClearPendingTutorialProgress("SkillEditFinished", AccountId);
        Require(PlayFabReadClient.GetPendingTutorialProgress() == "StageOneFinished", "Old marker acknowledgement erased newer local progress."); report.persistenceChecks++;
    }

    static void CheckTransitions(Report report)
    {
        var page = new ReadinessPage { Step = MainSceneStep.UnitSkillEdit }; ProcessesRunner.Main.currentProcess = page;
        var go = new GoTo(MainSceneStep.UnitSkillEdit); var open = new OpenSkillEdit("1");
        Require(!go.CanEnterOtherProcess() && !open.CanEnterOtherProcess(), "Navigation advances while the skill page is still loading."); report.transitions++;
        page.Loaded(true); Require(go.CanEnterOtherProcess() && open.CanEnterOtherProcess(), "Initialized skill page cannot advance."); report.transitions++;
        PlayerAccountInfo.Me.tutorialProgress = "Finished";
        var runner = new TutorialRunner(); var first = new ChainProcess(); var second = new ChainProcess();
        Get<List<TutorialProcess>>(runner, "_tutorialProcesses").AddRange(new TutorialProcess[] { first, second });
        runner.MoveToNext(); Require(first.enters == 1 && second.enters == 0, "Chain skipped its opening instruction.");
        runner.Process(); Require(first.exits == 1 && second.enters == 1, "Chain did not close the preceding instruction before advancing.");
        runner.Process(); Require(second.exits == 1 && Get<List<TutorialProcess>>(runner, "_tutorialProcesses").Count == 0, "Completed tutorial chain kept an active instruction."); report.transitions += 3;
    }

    sealed class ReadinessPage : MSceneProcess { public void Loaded(bool value) => SetLoaded(value); }
    sealed class ChainProcess : TutorialProcess { public int enters, exits; bool ready; public override void ProcessEnter() => enters++; public override void ProcessEnd() => exits++; public override void LocalUpdate() => ready = true; public override bool CanEnterOtherProcess() => ready; }
    static bool CopyComponent(Component component) => component is Graphic || component is CanvasRenderer || component is LayoutGroup || component is CanvasGroup || component is LayoutElement || component is ContentSizeFitter
        || component is Mask || component is RectMask2D || component is BOButton || component is Toggle || component is ScrollRect || component is StoneCell || component is TheNineSlot || component is SkillStonesBox
        || component is ConfirmBtnColorSwapper || component is SkillStoneBoxTabEffectsManager || component is ShowAllMyStoneLevel || component is SkillEditTutorial11 || component is UIPosClamper;
    static Transform CopyHierarchy(Transform source, Transform parent, IDictionary<Object, Object> map)
    {
        var obj = source is RectTransform ? new GameObject(source.name, typeof(RectTransform)) : new GameObject(source.name); obj.SetActive(false);
        var target = obj.transform; target.SetParent(parent, false); target.localPosition = source.localPosition; target.localRotation = source.localRotation; target.localScale = source.localScale;
        if (source is RectTransform from && target is RectTransform to) { to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot; to.sizeDelta = from.sizeDelta; to.anchoredPosition3D = from.anchoredPosition3D; }
        map[source] = target; map[source.gameObject] = obj; foreach (Transform child in source) CopyHierarchy(child, target, map); obj.SetActive(source.gameObject.activeSelf); return target;
    }
    static void Remap(Component target, IDictionary<Object, Object> map)
    {
        var serialized = new SerializedObject(target); var property = serialized.GetIterator();
        while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && map.TryGetValue(property.objectReferenceValue, out var copy)) property.objectReferenceValue = copy;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static FieldInfo Field(object target, string name) { var type = target.GetType(); while (type != null) { var field = type.GetField(name, Fields); if (field != null) return field; type = type.BaseType; } throw new MissingFieldException(name); }
    static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static void Invoke(object target, string name) => target.GetType().GetMethod(name, Fields).Invoke(target, null);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

// Validation uses the production guidance; only uninitialized model/effect teardown is omitted.
public sealed class PocketStrikerPreBattleTutorialValidationLayer : SkillEditLayer
{
    public SkillSet AutoFillSet { get; private set; }
    public int OccupiedBeforeAutoFill { get; private set; }
    protected override void Finish(UnitInfo info, SkillSet targetSkillSet)
    {
        AutoFillSet = targetSkillSet;
        OccupiedBeforeAutoFill = nineSlot.AllSlot.Count(slot => slot._cell.GetItem() != null);
        base.Finish(info, targetSkillSet);
    }
    protected override void PlayAutoFillSlotEffect(int targetSlot, SkillConfig skillConfig) { }
    protected override void RefreshAutoFillPresentation() { nineSlot.ValidateWarn(); stonesBox.RestFilter(); }
    public override void OnDestroy() { }
}
