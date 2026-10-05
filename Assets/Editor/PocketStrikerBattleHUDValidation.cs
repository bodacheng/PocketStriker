using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightScene;
using RengeGames.HealthBars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Offline production HUD geometry and input checks, using the authored UI and local fighter assets.</summary>
public static class PocketStrikerBattleHUDValidation
{
    [MenuItem("PocketStriker/Validation/Battle HUD")]
    public static void Validate()
    {
        var report = PocketStrikerTutorialValidation.RunBattleHUDFixture();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_BATTLE_HUD_PASSED: " + report.viewportsChecked + " viewports, "
            + report.raycastsChecked + " raycasts, " + report.inputStatesChecked + " input states, "
            + report.cameraTouchPointsChecked + " camera touch points.");
    }

    public static void ValidateBatch()
    {
        var report = PocketStrikerTutorialValidation.RunBattleHUDFixture();
        Debug.Log("[BattleHUD] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath("Logs/UILayout/BattleHUD/report.json"));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    // Both fixtures are synchronous and offline; a single editor import/compile covers them.
    public static void ValidateAllBatch()
    {
        var group = PocketStrikerGroupBattleValidation.Run();
        var hud = PocketStrikerTutorialValidation.RunBattleHUDFixture();
        bool passed = group.passed && hud.passed;
        Debug.Log("[BattleHUD+Group] " + (passed ? "PASS" : "FAIL") + ": HUD " + hud.viewportsChecked
            + " viewports, Group " + group.countCasesChecked + " count cases, " + group.retryCasesChecked + " retry cases.");
        if (!passed) Debug.LogError(string.Join("\n", group.errors.Concat(hud.errors)));
        EditorApplication.Exit(passed ? 0 : 1);
    }

    public static void ValidatePresentationBatch()
    {
        try
        {
            var camera = PocketStrikerBattleCameraValidation.Run();
            var hud = PocketStrikerTutorialValidation.RunBattleHUDFixture();
            var prepare = PocketStrikerFightPrepareValidation.ValidatePreparation();
            var composition = PocketStrikerCameraLoadingValidation.ValidateCameraAndLoading();
            bool passed = camera.passed && hud.passed && prepare.passed && composition.passed;
            Debug.Log("[BattlePresentation] " + (passed ? "PASS" : "FAIL") + ": camera " + camera.framingCases
                + " cases, HUD " + hud.viewportsChecked + " viewports, preparation " + prepare.casesChecked + " cases.");
            if (!passed) Debug.LogError(string.Join("\n", camera.errors.Concat(hud.errors).Concat(prepare.errors).Concat(composition.errors)));
            EditorApplication.Exit(passed ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void ValidateTutorialAndGroupBatch()
    {
        var group = PocketStrikerGroupBattleValidation.Run();
        var tutorial = PocketStrikerTutorialValidation.ValidateTutorialLayout();
        bool passed = group.passed && tutorial.passed;
        Debug.Log("[Tutorial+Group] " + (passed ? "PASS" : "FAIL") + ": tutorial " + tutorial.pagesChecked
            + " layouts, Group " + group.countCasesChecked + " count cases.");
        if (!passed) Debug.LogError(string.Join("\n", group.errors.Concat(tutorial.errors)));
        EditorApplication.Exit(passed ? 0 : 1);
    }
}

// Reuses the proven preview-scene UI copier, raycaster registration and URP render helpers.
public static partial class PocketStrikerTutorialValidation
{
    const string HUDOutput = "Logs/UILayout/BattleHUD";
    const string HUDStonePath = "Assets/Resources/BasicSprites/stoneModel.prefab";
    const string HUDModelPath = "Assets/ExternalAssets/Unit/human/haruka.prefab";

    [Serializable]
    public sealed class HUDReport
    {
        public bool passed;
        public string unityVersion;
        public int viewportsChecked;
        public int geometryChecks;
        public int raycastsChecked;
        public int inputStatesChecked;
        public int cameraTouchPointsChecked;
        public int cameraPointerOwnershipChecks;
        public int stableLayoutsChecked;
        public int labelsChecked;
        public int localizedLabelCases;
        public int autoPresentationChecks;
        public int groupCountCases;
        public bool sourcePrefabsUnchanged;
        public string scope = "Actual FightingStepLayer, SideUnitIcon and stoneModel prefab UI copies; production BattleHUDPresentation and SideUnitIcon.ApplyBattleHUDStyle. Phone, compact phone, notched phone, tablet and notched tablet safe areas. EventSystem raycasts verify pause, AUTO, six skill controls, expanded action edges, player portraits, movement pad and full-screen camera touch points. Camera input checks cover a rejected second finger, simultaneous movement and action input, and captured gestures crossing controls. Authored action down/up callbacks verify countdown locking and recovery. This static fixture has no bound fighter focus and explicitly restores its manually displayed controls after verifying production no-focus hiding; live focus/energy behavior is covered separately. Screenshots contain local posed haruka models, actual portrait and skill sprites.";
        public string limitation = "Stopped-editor fixture: battle simulation, accounts, Addressables downloads, pause scene navigation and live camera-follow bars are omitted. The pause's native Button press is observed with a local callback. Skill EventTriggers are reconstructed from their authored persistent method names because RuntimeOnly persistent callbacks do not run in Edit mode. Joystick pointer state is tested with transitions/gravity disabled; axis magnitude assumes a screen-space canvas and is left to Play-mode smoke. Static ground and boundary are fixture context, not a simulation of the runtime arena.";
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<string> autoStateScreenshots = new List<string>();
        public List<string> groupCountScreenshots = new List<string>();
        public List<HUDCase> cases = new List<HUDCase>();
    }

    [Serializable]
    public sealed class HUDCase
    {
        public string viewport;
        public Rect safeBounds;
        public List<HUDRect> controls = new List<HUDRect>();
    }

    [Serializable]
    public sealed class HUDRect { public string name; public Rect bounds; }

    public static HUDReport RunBattleHUDFixture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Battle HUD validation requires a stopped editor.");
        Directory.CreateDirectory(HUDOutput);
        var report = new HUDReport { unityVersion = Application.unityVersion };
        var sources = new[] { PrefabPath, SideIconPath, HUDStonePath, HUDModelPath }
            .ToDictionary(path => path, File.ReadAllText);
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var oldFight = FightLoad.Fight;
        var oldEventSystem = EventSystem.current;
        var oldLanguage = AppSetting.Value.Language;
        var dragCanvasField = typeof(HeroIcon).GetField("canvas", BindingFlags.Static | BindingFlags.NonPublic);
        var oldDragCanvas = dragCanvasField.GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Battle HUD Validation");
        rig.SetActive(false);
        SceneManager.MoveGameObjectToScene(rig, scene);
        RenderTexture target = null, worldTarget = null;
        EventSystem events = null;
        FightInfo fight = null;
        Material contextMaterial = null;
        void CaptureError(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 200)
                report.errors.Add(message);
        }
        Application.logMessageReceived += CaptureError;
        try
        {
            fight = ScriptableObject.CreateInstance<FightInfo>();
            AppSetting.Value.Language = SystemLanguage.ChineseSimplified;
            fight.EventType = FightEventType.Quest;
            fight.FightMode = FightMode.Multi;
            FightLoad.Fight = fight;

            var cameraObject = new GameObject("HUD Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000;
            camera.cullingMask = 1 << 5;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.14f, 1);

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.layer = 5;
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1200, 2133.333f);
            PosCal.Canvas = canvas;
            // HeroIcon.Awake normally creates a global drag canvas. Keep the fixture local.
            dragCanvasField.SetValue(null, canvas);
            var safe = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvasRect, false);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.SafeAreaRect = safe;
            var eventObject = new GameObject("EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(rig.transform, false);
            events = eventObject.GetComponent<EventSystem>();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(source != null, "Missing FightingStepLayer prefab.");
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var root = HUDCopyUI(source, canvasRect, map);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.localPosition = Vector3.zero; root.localScale = Vector3.one;
            var sourceLayer = source.GetComponent<FightingStepLayer>();
            var layer = root.gameObject.AddComponent<PocketStrikerTutorialValidationLayer>();
            foreach (var name in new[] { "pauseButton", "inputsManager", "team1UI", "team2UI", "clickNextTutorial", "clickTriggerDreamCombo", "forceClickAutoBtnBlackMask", "top", "middle", "bottom" })
                SetField(layer, name, map[(UnityEngine.Object)FindField(sourceLayer, name).GetValue(sourceLayer)]);
            CopyBackdrops(sourceLayer, layer, map);
            Invoke(layer, "ResetOverlayStates");
            Invoke(layer, "KeepTopButtonsClickable");
            layer.Team1UI.TeamMode = layer.Team2UI.TeamMode = TeamMode.Rotation;
            layer.Team1UI.AutoSwitch.gameObject.SetActive(true);
            layer.Team2UI.AutoSwitch.gameObject.SetActive(false);
            foreach (var team in new[] { layer.Team1UI, layer.Team2UI })
            {
                team.LiveUnitCount.gameObject.SetActive(false);
                Field<Text>(team, "rotationModeHitCombo").gameObject.SetActive(false);
                team.SelectedFrame.gameObject.SetActive(false);
            }
            var rail = Field<RectTransform>(layer.Team1UI, "sideIconsContainer");
            rail.gameObject.SetActive(true);
            var portraits = new List<SideUnitIcon>();
            for (int index = 0; index < 3; index++)
            {
                var icon = HUDCreateSideIcon(rail, new[] { 1, 9, 3, 7 }[index], false);
                icon.name = "Local Player " + (index + 1);
                portraits.Add(icon);
            }
            var floating = new List<SideUnitIcon>();
            for (int index = 0; index < 4; index++)
            {
                var icon = HUDCreateSideIcon(root, 9, true);
                icon.name = "Local Enemy Bar " + (index + 1);
                floating.Add(icon);
            }
            HUDPopulateSkills(layer.InputsManager);
            HUDBindAuthoredInputs(source, map, layer.InputsManager);
            layer.PauseButton.gameObject.SetActive(true);
            var pauseCount = 0;
            layer.PauseButton.onClick.AddListener(() => pauseCount++);
            var automatic = false;
            var auto = layer.Team1UI.AutoSwitch;
            var background = new GameObject("Local Battle Context", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            background.gameObject.layer = 5;
            background.transform.SetParent(root, false);
            background.transform.SetAsFirstSibling();
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.raycastTarget = false;
            var worldCamera = HUDCreateWorld(rig.transform, scene, out contextMaterial);
            rig.SetActive(true);
            Invoke(events, "OnEnable");
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) { animator.Rebind(); animator.enabled = false; }
            auto.Initialize(() => automatic, value => automatic = value);
            var viewports = new[]
            {
                new LayoutViewport(540, 960, new Rect(0, 0, 540, 960)),
                new LayoutViewport(375, 667, new Rect(0, 0, 375, 667)),
                new LayoutViewport(390, 844, new Rect(0, 34, 390, 763)),
                new LayoutViewport(768, 1024, new Rect(0, 0, 768, 1024)),
                new LayoutViewport(834, 1194, new Rect(0, 20, 834, 1154))
            };
            foreach (var viewport in viewports)
            {
                try
                {
                    HUDRelease(ref target); HUDRelease(ref worldTarget);
                    target = new RenderTexture(viewport.Width, viewport.Height, 24, RenderTextureFormat.ARGB32); target.Create();
                    worldTarget = new RenderTexture(viewport.Width, viewport.Height, 24, RenderTextureFormat.ARGB32); worldTarget.Create();
                    camera.targetTexture = target; camera.aspect = (float)viewport.Width / viewport.Height;
                    float scale = Mathf.Min(viewport.Safe.width / 1200, viewport.Safe.height / 2133.333f);
                    canvasRect.sizeDelta = new Vector2(viewport.Width / scale, viewport.Height / scale);
                    camera.orthographicSize = canvasRect.rect.height / 2;
                    safe.anchorMin = new Vector2(viewport.Safe.xMin / viewport.Width, viewport.Safe.yMin / viewport.Height);
                    safe.anchorMax = new Vector2(viewport.Safe.xMax / viewport.Width, viewport.Safe.yMax / viewport.Height);
                    safe.offsetMin = safe.offsetMax = Vector2.zero;
                    worldCamera.aspect = camera.aspect; worldCamera.targetTexture = worldTarget;
                    worldCamera.orthographicSize = Mathf.Max(6.8f, 5.4f / worldCamera.aspect);
                    HUDRenderCamera(worldCamera, worldTarget);
                    background.texture = worldTarget;
                    layer.ResizeAreas(); layer.RefreshPresentation();
                    HUDPlaceFloating(root, worldCamera, floating, viewport);
                    Rebuild(root, camera, target);
                    layer.RefreshPresentation(); Rebuild(root, camera, target);
                    var caseReport = new HUDCase { viewport = viewport.Name, safeBounds = LayoutBounds(root, safe) };
                    report.cases.Add(caseReport);
                    HUDCheckGeometry(root, layer, portraits, floating, caseReport, report);
                    var watched = HUDControlRects(layer).Concat(portraits.Select(icon => (RectTransform)icon.transform)).ToArray();
                    var before = watched.Select(rect => LayoutBounds(root, rect)).ToArray();
                    for (int repeat = 0; repeat < 3; repeat++) { layer.ResizeAreas(); layer.RefreshPresentation(); Rebuild(root, camera, target); }
                    for (int index = 0; index < watched.Length; index++)
                    {
                        var after = LayoutBounds(root, watched[index]);
                        Require(Vector2.Distance(before[index].position, after.position) < 0.5f && Vector2.Distance(before[index].size, after.size) < 0.5f,
                            "HUD layout drifts on repeated refresh: " + watched[index].name);
                    }
                    report.stableLayoutsChecked++;
                    HUDCheckInputs(layer, events, camera, report, () => automatic, () => pauseCount);
                    auto.ChangeAutoState(true);
                    layer.RefreshPresentation(); Rebuild(root, camera, target);
                    var autoOnPath = Path.Combine(HUDOutput, viewport.Name + "-auto-on.png");
                    SaveLayoutRender(target, autoOnPath); report.autoStateScreenshots.Add(autoOnPath);
                    foreach (var language in new[] { SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.ChineseSimplified })
                    {
                        AppSetting.Value.Language = language;
                        layer.RefreshPresentation(); Rebuild(root, camera, target);
                        Rebuild(root, camera, target);
                        HUDCheckLabels(root, layer, caseReport.safeBounds, report);
                        report.localizedLabelCases++;
                    }
                    auto.ChangeAutoState(false); layer.RefreshPresentation();
                    Rebuild(root, camera, target);
                    var autoButton = Field<BOButton>(auto, "btn");
                    autoButton.interactable = false;
                    layer.RefreshPresentation(); Rebuild(root, camera, target);
                    var autoDisabledPath = Path.Combine(HUDOutput, viewport.Name + "-auto-disabled.png");
                    SaveLayoutRender(target, autoDisabledPath); report.autoStateScreenshots.Add(autoDisabledPath);
                    autoButton.interactable = true;
                    layer.RefreshPresentation(); Rebuild(root, camera, target);
                    var path = Path.Combine(HUDOutput, viewport.Name + ".png");
                    SaveLayoutRender(target, path); report.screenshots.Add(path);
                    HUDCheckGroupCounts(root, layer, camera, target, caseReport.safeBounds, viewport.Name, report);
                    report.viewportsChecked++;
                }
                catch (Exception exception) { report.errors.Add(viewport.Name + ": " + exception.GetBaseException().ToString()); }
            }
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            PosCal.Canvas = oldCanvas; PosCal.SafeAreaRect = oldSafe; FightLoad.Fight = oldFight;
            AppSetting.Value.Language = oldLanguage;
            dragCanvasField.SetValue(null, oldDragCanvas);
            if (events != null) Invoke(events, "OnDisable");
            foreach (var raycaster in rig.GetComponentsInChildren<GraphicRaycaster>(true))
                typeof(BaseRaycaster).GetMethod("OnDisable", Private).Invoke(raycaster, null);
            UnityEngine.Object.DestroyImmediate(rig);
            if (fight != null) UnityEngine.Object.DestroyImmediate(fight);
            if (contextMaterial != null) UnityEngine.Object.DestroyImmediate(contextMaterial);
            HUDRelease(ref target); HUDRelease(ref worldTarget);
            EditorSceneManager.ClosePreviewScene(scene);
            if (oldEventSystem != null) EventSystem.current = oldEventSystem;
            Application.logMessageReceived -= CaptureError;
            report.sourcePrefabsUnchanged = sources.All(pair => File.ReadAllText(pair.Key) == pair.Value);
            report.passed = report.errors.Count == 0 && report.sourcePrefabsUnchanged && report.viewportsChecked == 5
                && report.stableLayoutsChecked == 5 && report.screenshots.Count == 5 && report.localizedLabelCases == 15
                && report.cameraTouchPointsChecked == 45 && report.cameraPointerOwnershipChecks == 180
                && report.autoPresentationChecks >= 35 && report.autoStateScreenshots.Count == 10
                && report.groupCountCases == 30 && report.groupCountScreenshots.Count == 5;
            File.WriteAllText(Path.Combine(HUDOutput, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static RectTransform HUDCopyUI(GameObject source, Transform parent, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        var root = (RectTransform)CopyHierarchy(source.transform, parent, map);
        foreach (var original in source.GetComponentsInChildren<Component>(true))
        {
            if (original == null || !(CopyComponent(original) || original is MidAreaSizeHelper || original is MobileInputsManager
                || original is UltimateJoystick || original is HeroIcon || original is SideUnitIcon || original is Shadow
                || original is RadialSegmentedHealthBar)) continue;
            var destination = ((Transform)map[original.transform]).gameObject;
            // ExecuteAlways radial bars may generate their Image when added.
            var copy = destination.GetComponent(original.GetType());
            if (copy == null) copy = destination.AddComponent(original.GetType());
            EditorUtility.CopySerialized(original, copy); map.Add(original, copy);
        }
        foreach (var pair in map.ToArray())
            if (pair.Key is Component && !(pair.Key is Transform)) RemapReferences((Component)pair.Value, map);
        // ExecuteAlways Awake can initialize a radial bar before CopySerialized
        // replaces its property fields. Rebind its internal dictionary/callbacks
        // to the copied properties before the production presentation writes them.
        foreach (var gauge in root.GetComponentsInChildren<RadialSegmentedHealthBar>(true)) Invoke(gauge, "InitProperties");
        foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 5;
        foreach (var clamper in root.GetComponentsInChildren<UIPosClamper>(true)) clamper.enabled = false;
        foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var button in root.GetComponentsInChildren<Button>(true)) { button.onClick = new Button.ButtonClickedEvent(); button.transition = Selectable.Transition.None; }
        foreach (var trigger in root.GetComponentsInChildren<EventTrigger>(true)) trigger.triggers.Clear();
        foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true))
            if (group.GetComponentInParent<UltimateJoystick>(true) == null) group.alpha = 1;
        foreach (var joystick in root.GetComponentsInChildren<UltimateJoystick>(true))
        {
            joystick.inputTransition = false; joystick.gravity = 0;
            joystick.tapCountOption = UltimateJoystick.TapCountOption.NoCount;
        }
        return root;
    }

    static SideUnitIcon HUDCreateSideIcon(Transform parent, int id, bool floating)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SideIconPath);
        var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        var root = HUDCopyUI(source, parent, map);
        var icon = root.GetComponent<SideUnitIcon>();
        root.gameObject.SetActive(true); root.localScale = Vector3.one;
        icon.DreamComboFlg.SetActive(false); icon.TeamIndicator.gameObject.SetActive(false);
        icon.HealthBarRect.GetComponent<Slider>().value = 0.72f;
        Field<Slider>(icon, "resistBar").value = 0.54f;
        icon.RefreshExBar(65);
        var hero = icon.Icon;
        var image = Field<Image>(hero, "icon");
        image.sprite = HUDSprite("Unit_Icon", id); image.color = Color.white;
        Field<Image>(hero, "iconBg").color = new Color(0.09f, 0.14f, 0.18f, 1);
        Field<Image>(hero, "frame").color = new Color(0.25f, 0.57f, 0.68f, 1);
        hero.CooldownCurtainUpdate(0);
        if (hero.WarnFlag != null) hero.WarnFlag.SetActive(false);
        icon.ApplyBattleHUDStyle(floating, false);
        icon.SetBattleHUDTeamColor(!floating);
        hero.gameObject.SetActive(!floating);
        return icon;
    }

    static Sprite HUDSprite(string directory, int id)
    {
        var parent = "Assets/OrganizedResources/InUse/ExternalAssets/" + directory;
        var path = Directory.GetFiles(parent).FirstOrDefault(file => Path.GetFileNameWithoutExtension(file) == id.ToString()
            && Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase));
        Require(path != null, "Missing local sprite " + directory + "/" + id);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Require(sprite != null, "Local image is not imported as Sprite: " + path);
        return sprite;
    }

    static void HUDPopulateSkills(MobileInputsManager input)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(HUDStonePath);
        var buttons = new[] { input.AttackButton, input.Fire1Button, input.Fire2Button };
        for (int index = 0; index < buttons.Length; index++)
        {
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var gem = HUDCopyUI(source, buttons[index].transform, map);
            gem.name = "Local Skill " + index;
            gem.anchorMin = Vector2.zero; gem.anchorMax = Vector2.one;
            gem.offsetMin = Vector2.one * 14; gem.offsetMax = Vector2.one * -14;
            gem.localScale = Vector3.one; gem.gameObject.SetActive(true);
            var image = gem.GetComponent<Image>(); image.sprite = HUDSprite("SkillIcon", new[] { 44, 103, 95 }[index]); image.color = Color.white;
            foreach (var graphic in gem.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var child in gem.GetComponentsInChildren<Transform>(true).Where(child => child != gem)) child.gameObject.SetActive(false);
        }
        input.DreamComboGauge.SetPercent(0.65f);
        foreach (var button in buttons.Concat(new[] { input.DashButton, input.DefendButton, input.DreamComboBtn })) button.gameObject.SetActive(true);
        input.MovementJoystick.gameObject.SetActive(true);
    }

    static void HUDBindAuthoredInputs(GameObject source, Dictionary<UnityEngine.Object, UnityEngine.Object> map, MobileInputsManager inputs)
    {
        var allowed = new HashSet<string> { "AttackDown", "AttackUp", "Fire1Down", "Fire1Up", "Fire2Down", "Fire2Up", "DefendDown", "DefendUp", "RushDown", "RushUp", "DreamComboDown", "DreamComboUp" };
        int calls = 0;
        foreach (var original in source.GetComponentsInChildren<EventTrigger>(true))
        {
            var copy = (EventTrigger)map[original];
            foreach (var entry in original.triggers)
            {
                var recreated = new EventTrigger.Entry { eventID = entry.eventID };
                for (int index = 0; index < entry.callback.GetPersistentEventCount(); index++)
                {
                    var method = entry.callback.GetPersistentMethodName(index);
                    if (!allowed.Contains(method)) continue;
                    Require(entry.callback.GetPersistentTarget(index) is MobileInputsManager, "Skill input targets the wrong production component.");
                    var callback = (Action)Delegate.CreateDelegate(typeof(Action), inputs, method);
                    recreated.callback.AddListener(_ => callback()); calls++;
                }
                if (recreated.callback != null) copy.triggers.Add(recreated);
            }
        }
        Require(calls == 12, "Expected twelve authored skill down/up callbacks; found " + calls);
    }

    static RectTransform[] HUDControlRects(FightingStepLayer layer) => new[]
    {
        (RectTransform)layer.PauseButton.transform, (RectTransform)layer.Team1UI.AutoSwitch.transform,
        (RectTransform)layer.InputsManager.AttackButton.transform, (RectTransform)layer.InputsManager.Fire1Button.transform,
        (RectTransform)layer.InputsManager.Fire2Button.transform, (RectTransform)layer.InputsManager.DefendButton.transform,
        (RectTransform)layer.InputsManager.DashButton.transform, (RectTransform)layer.InputsManager.DreamComboBtn.transform,
        layer.InputsManager.MovementJoystick.joystickBase
    };

    static void HUDCheckGeometry(RectTransform root, FightingStepLayer layer, IList<SideUnitIcon> portraits,
        IList<SideUnitIcon> floating, HUDCase item, HUDReport report)
    {
        var controls = HUDControlRects(layer);
        foreach (var rect in controls)
        {
            var bounds = LayoutBounds(root, rect);
            Require(LayoutContains(item.safeBounds, bounds, 2), "Control escapes safe area: " + rect.name + " " + bounds);
            Require(bounds.width > 0 && bounds.height > 0, "Control has no size: " + rect.name);
            item.controls.Add(new HUDRect { name = rect.name, bounds = bounds }); report.geometryChecks++;
        }
        var safeWidth = item.safeBounds.width;
        var unit = Mathf.Clamp(safeWidth / 1200f, 0.72f, 1.1f);
        var expectedSizes = new[] { new Vector2(76, 72), new Vector2(164, 72), new Vector2(156, 156), new Vector2(156, 156),
            new Vector2(156, 156), new Vector2(100, 100), new Vector2(100, 100), new Vector2(112, 112), new Vector2(216, 216) };
        for (int index = 0; index < controls.Length; index++)
            Require(Vector2.Distance(LayoutBounds(root, controls[index]).size, expectedSizes[index] * unit) < 2,
                "Control has unexpected production dimensions: " + controls[index].name);
        var expectedCenters = new[] {
            new Vector2(item.safeBounds.xMin + 58 * unit, item.safeBounds.yMax - 58 * unit),
            new Vector2(item.safeBounds.xMax - 106 * unit, item.safeBounds.yMax - 58 * unit),
            new Vector2(item.safeBounds.xMax - 448 * unit, item.safeBounds.yMin + 162 * unit),
            new Vector2(item.safeBounds.xMax - 274 * unit, item.safeBounds.yMin + 162 * unit),
            new Vector2(item.safeBounds.xMax - 100 * unit, item.safeBounds.yMin + 162 * unit),
            new Vector2(item.safeBounds.xMax - 274 * unit, item.safeBounds.yMin + 322 * unit),
            new Vector2(item.safeBounds.xMax - 448 * unit, item.safeBounds.yMin + 322 * unit),
            new Vector2(item.safeBounds.xMax - 100 * unit, item.safeBounds.yMin + 330 * unit),
            new Vector2(item.safeBounds.xMin + 156 * unit, item.safeBounds.yMin + 208 * unit) };
        for (int index = 0; index < controls.Length; index++)
            Require(Vector2.Distance(LayoutBounds(root, controls[index]).center, expectedCenters[index]) < 2,
                "Lifted control has an unexpected safe-area position: " + controls[index].name);
        var middle = LayoutBounds(root, layer.MiddleArea);
        Require(Mathf.Abs(middle.yMin - (item.safeBounds.yMin + 392 * unit)) < 2,
            "Battle camera's middle area does not reserve the lifted controls.");
        foreach (var control in controls.Skip(2))
            Require(LayoutBounds(root, control).yMax <= middle.yMin + 1, "A lifted control enters the camera's playable middle area.");
        report.geometryChecks += controls.Length + 1;
        Require(LayoutBounds(root, controls[1]).width <= safeWidth * 0.17f, "AUTO is oversized relative to the safe width.");
        Require(LayoutBounds(root, controls[0]).width <= safeWidth * 0.09f, "Pause is oversized relative to the safe width.");
        for (int index = 0; index < controls.Length; index++)
            for (int other = index + 1; other < controls.Length; other++)
                Require(!LayoutBounds(root, controls[index]).Overlaps(LayoutBounds(root, controls[other])), "Controls overlap: " + controls[index].name + "/" + controls[other].name);
        var last = new List<Rect>();
        foreach (var icon in portraits)
        {
            var bounds = LayoutBounds(root, (RectTransform)icon.transform);
            Require(LayoutContains(item.safeBounds, bounds, 2), "Player rail escapes safe area.");
            Require(last.All(previous => !previous.Overlaps(bounds)), "Player rail entries overlap.");
            Require(controls.All(control => !LayoutBounds(root, control).Overlaps(bounds)), "Player rail covers a control.");
            last.Add(bounds);
            Require(LayoutContains(LayoutBounds(root, layer.Team1UI.SideIconsContainer), bounds, 2), "Player portrait escapes its rail.");
            Require(icon.HealthBarRect.rect.width <= 112 && icon.HealthBarRect.rect.height <= 14, "Player HP bar is oversized.");
            Require(Field<Slider>(icon, "resistBar").GetComponent<RectTransform>().rect.height <= 7, "Player resistance bar is oversized.");
            report.geometryChecks += 4;
        }
        foreach (var icon in floating)
        {
            var bounds = LayoutBounds(root, icon.HealthBarRect);
            Require(LayoutContains(item.safeBounds, bounds, 2), "World HP bar escapes the safe area.");
            Require(bounds.width <= safeWidth * 0.12f && bounds.height <= safeWidth * 0.02f, "World HP bar is oversized.");
            Require(!icon.Icon.gameObject.activeSelf, "World HP bar displays the full portrait.");
            report.geometryChecks += 3;
        }
        var gauge = (RectTransform)layer.InputsManager.DreamComboGauge.transform;
        Require(gauge.IsChildOf(layer.InputsManager.DreamComboBtn.transform), "Dream Combo gauge is detached from its control.");
        Require(LayoutContains(LayoutBounds(root, (RectTransform)layer.InputsManager.DreamComboBtn.transform), LayoutBounds(root, gauge), 2), "Dream Combo ring escapes its button.");
        foreach (var button in new[] { layer.InputsManager.DashButton, layer.InputsManager.DreamComboBtn })
        {
            Require(!button.GetComponentsInChildren<Text>().Any(text => text.enabled && !string.IsNullOrWhiteSpace(text.text)),
                "Effect-only action still displays a caption: " + button.name);
            Require(!button.GetComponentsInChildren<Image>().Any(image => image.enabled && image.color.a > 0.01f
                && image.transform != gauge && !image.transform.IsChildOf(gauge)),
                "Effect-only action still displays a UI frame/background: " + button.name);
            Require(button.targetGraphic != null && button.targetGraphic.color.a == 0 && button.targetGraphic.raycastTarget,
                "Effect-only action lost its transparent touch target: " + button.name);
            report.geometryChecks += 3;
        }
        var movement = layer.InputsManager.MovementJoystick;
        var baseBounds = LayoutBounds(root, movement.joystickBase);
        var knobBounds = LayoutBounds(root, movement.joystick);
        Require(LayoutContains(baseBounds, knobBounds, 1), "Movement knob escapes the joystick base.");
        Require(knobBounds.width <= baseBounds.width * 0.4f && knobBounds.height <= baseBounds.height * 0.4f,
            "Movement knob is oversized relative to its base: " + knobBounds.size + "/" + baseBounds.size);
        Require(Vector2.Distance(knobBounds.size, new Vector2(68, 68) * unit) < 2, "Movement knob does not use its compact presentation dimensions.");
        report.geometryChecks += 3;
        foreach (var gem in root.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Local Skill ")))
            Require(LayoutContains(LayoutBounds(root, (RectTransform)gem.parent), LayoutBounds(root, gem), 1), "Skill art escapes its button.");
        var joysticks = root.GetComponentsInChildren<UltimateJoystick>().ToArray();
        Require(joysticks.Length == 2, "Expected movement and camera joysticks.");
        Require(movement.customActivationRange && movement.activationWidth <= 40.1f && movement.activationHeight <= 35.1f,
            "Movement joystick lost its reserved activation region.");
        var cameraPad = joysticks.Single(joystick => joystick.joystickName == "RotateCamera");
        Require(cameraPad.customActivationRange && cameraPad.activationWidth == 100 && cameraPad.activationHeight == 100,
            "Camera input does not cover the full battlefield.");
        Require(LayoutContains(LayoutBounds(root, (RectTransform)cameraPad.transform), item.safeBounds, 1),
            "Camera input leaves part of the visible safe area unreachable.");
        Require(cameraPad.GetComponent<Graphic>().raycastTarget && !cameraPad.GetComponent<Graphic>().canvasRenderer.cullTransparentMesh,
            "Transparent camera input can be removed by rendering.");
    }

    static void HUDCheckLabels(RectTransform root, FightingStepLayer layer, Rect safeBounds, HUDReport report)
    {
        foreach (var text in HUDControlRects(layer).SelectMany(control => control.GetComponentsInChildren<Text>()).Where(text => text.enabled && !string.IsNullOrWhiteSpace(text.text)).Distinct())
        {
            var bounds = LayoutBounds(root, text.rectTransform);
            Require(LayoutContains(safeBounds, bounds, 2), "HUD caption escapes safe area: " + text.text);
            Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "HUD caption clips wrapped lines: " + text.text);
            var glyphs = LayoutGlyphBounds(root, text);
            Require(LayoutContains(bounds, glyphs, 2), "HUD glyphs escape their caption: " + text.text);
            foreach (char character in text.text.Where(character => !char.IsWhiteSpace(character)).Distinct())
                Require(text.font != null && text.font.HasCharacter(character), "HUD font misses U+" + ((int)character).ToString("X4"));
            report.labelsChecked++;
        }
    }

    static void HUDCheckGroupCounts(RectTransform root, FightingStepLayer layer, Camera camera,
        RenderTexture target, Rect safeBounds, string viewport, HUDReport report)
    {
        var counts = new[] { layer.Team1UI.LiveUnitCount, layer.Team2UI.LiveUnitCount };
        var enemyAuto = layer.Team2UI.AutoSwitch.gameObject;
        bool enemyAutoVisible = enemyAuto.activeSelf;
        try
        {
            enemyAuto.SetActive(true);
            foreach (var count in counts) count.gameObject.SetActive(true);
            layer.RefreshPresentation();
            foreach (int remaining in new[] { 0, 9, 48 })
            {
                counts[0].text = "Player:" + remaining + "/48";
                counts[1].text = "Enemy:" + remaining + "/48";
                Rebuild(root, camera, target);
                foreach (var count in counts)
                {
                    var bounds = LayoutBounds(root, count.rectTransform);
                    Require(LayoutContains(safeBounds, bounds, 1), "Group count escapes safe area: " + count.name);
                    Require(LayoutContains(bounds, LayoutGlyphBounds(root, count), 1), "Group count clips: " + count.text);
                    Require(count.fontSize >= 30 && count.fontStyle == FontStyle.Bold && count.color.a == 1,
                        "Group count lost readable emphasis.");
                    var outline = count.GetComponent<Outline>();
                    Require(outline != null && outline.enabled && outline.effectColor.a == 1 && !count.raycastTarget,
                        "Group count lost contrast or blocks touch input.");
                    foreach (var control in new[] { layer.PauseButton.transform, layer.Team1UI.AutoSwitch.transform, layer.Team2UI.AutoSwitch.transform })
                        Require(!bounds.Overlaps(LayoutBounds(root, (RectTransform)control)), "Group count covers a top control.");
                    report.groupCountCases++;
                }
                Require(!LayoutBounds(root, counts[0].rectTransform).Overlaps(LayoutBounds(root, counts[1].rectTransform)),
                    "Group team counts overlap.");
            }
            var path = Path.Combine(HUDOutput, viewport + "-group-counts.png");
            SaveLayoutRender(target, path);
            report.groupCountScreenshots.Add(path);
        }
        finally
        {
            foreach (var count in counts) count.gameObject.SetActive(false);
            enemyAuto.SetActive(enemyAutoVisible);
            layer.RefreshPresentation();
        }
    }

    static void HUDCheckInputs(FightingStepLayer layer, EventSystem events, Camera camera, HUDReport report,
        Func<bool> automatic, Func<int> pauseCount)
    {
        var input = layer.InputsManager;
        var buttons = new[] { input.AttackButton, input.Fire1Button, input.Fire2Button, input.DefendButton, input.DashButton, input.DreamComboBtn };
        var state = new Func<bool>[] { () => input.attack, () => input.fire1, () => input.fire2, () => input.defendButtonHover, () => input.acc, () => input.dreamCombo };
        void ActionPointer(int index, GameObject hit, Vector2 point, bool enabled, string label)
        {
            var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            Require(state[index]() == enabled && state.Where((_, other) => other != index).All(value => !value()),
                label + " down set the wrong input state: " + buttons[index].name);
            Require(layer.GetComponentsInChildren<UltimateJoystick>().All(joystick => !joystick.GetJoystickState()),
                label + " down leaked into a joystick: " + buttons[index].name);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            Require(state.All(value => !value()), label + " up did not clear input: " + buttons[index].name);
            report.inputStatesChecked += 2;
        }
        void Target(Transform control, string label)
        {
            var hit = Hit(events, Center(control, camera));
            Require(hit == control.gameObject || hit.transform.IsChildOf(control), label + " is occluded by " + hit.name);
            report.raycastsChecked++;
        }
        Target(layer.PauseButton.transform, "Pause"); Target(layer.Team1UI.AutoSwitch.transform, "AUTO");
        foreach (var portrait in layer.Team1UI.SideIconsContainer.GetComponentsInChildren<SideUnitIcon>())
            Target(portrait.transform, "Player portrait");
        foreach (var button in new[] { layer.PauseButton, Field<BOButton>(layer.Team1UI.AutoSwitch, "btn"),
            layer.InputsManager.DashButton, layer.InputsManager.DefendButton, layer.InputsManager.DreamComboBtn })
        {
            var graphic = button.targetGraphic;
            Require(graphic != null && graphic.raycastPadding.x <= -20 && graphic.raycastPadding.y <= -20
                && graphic.raycastPadding.z <= -20 && graphic.raycastPadding.w <= -20, "Compact labeled control has no expanded touch padding.");
            var rect = graphic.rectTransform;
            var outside = camera.WorldToScreenPoint(rect.TransformPoint(new Vector3(rect.rect.xMin - 12, rect.rect.center.y)));
            var hit = Hit(events, outside);
            Require(hit == button.gameObject || hit.transform.IsChildOf(button.transform), "Expanded touch target is occluded: " + button.name + " by " + hit.name
                + " at " + outside + "; target depth=" + graphic.depth + ", culled=" + graphic.canvasRenderer.cull
                + ", canvas=" + graphic.canvas?.name + ", sorting=" + graphic.canvas?.sortingOrder
                + ", directRaycast=" + graphic.Raycast(outside, camera));
            report.raycastsChecked++;
            int actionIndex = Array.IndexOf(buttons, button);
            if (actionIndex >= 0) ActionPointer(actionIndex, hit, outside, true, "Expanded action target");
        }
        int paused = pauseCount();
        typeof(Button).GetMethod("Press", Private).Invoke(layer.PauseButton, null);
        Require(pauseCount() == paused + 1, "Pause Button press did not reach its callback."); report.inputStatesChecked++;
        var autoButton = Field<BOButton>(layer.Team1UI.AutoSwitch, "btn");
        var presentation = layer.GetComponent<BattleHUDPresentation>();
        Image AutoImage(string name) => autoButton.GetComponentsInChildren<Image>(true).Single(image => image.name == name);
        void CheckAuto(bool expected, bool interactable)
        {
            Invoke(presentation, "LateUpdate");
            var text = autoButton.GetComponentsInChildren<Text>(true).Single(label => label.enabled && !string.IsNullOrWhiteSpace(label.text));
            Require(text.text == (expected ? "AUTO ON" : "AUTO OFF"), "AUTO caption does not state its actual mode.");
            var thumb = AutoImage("AutoStateThumb");
            var track = AutoImage("AutoStateTrack");
            Require(thumb.rectTransform.anchorMin.x == (expected ? 1 : 0)
                && thumb.rectTransform.anchorMax.x == (expected ? 1 : 0), "AUTO switch marker does not match its state.");
            Require(!thumb.raycastTarget && !track.raycastTarget && !text.raycastTarget,
                "AUTO status decorations intercept the button's pointer input.");
            Require(interactable ? thumb.color.a > .99f : thumb.color.a <= .6f,
                "AUTO marker does not reflect the disabled state.");
            Require(autoButton.IsInteractable() == interactable, "AUTO presentation changed button availability.");
            report.autoPresentationChecks++;
        }
        CheckAuto(automatic(), true);
        var offFill = autoButton.colors.normalColor;
        bool before = automatic(); typeof(Button).GetMethod("Press", Private).Invoke(autoButton, null);
        Require(automatic() != before, "AUTO did not toggle on.");
        CheckAuto(automatic(), true);
        Require(autoButton.colors.normalColor.grayscale > offFill.grayscale + .2f,
            "AUTO ON is not visibly brighter than OFF.");
        typeof(Button).GetMethod("Press", Private).Invoke(autoButton, null);
        Require(automatic() == before, "AUTO did not toggle off.");
        CheckAuto(automatic(), true);
        autoButton.interactable = false;
        CheckAuto(automatic(), false);
        typeof(Button).GetMethod("Press", Private).Invoke(autoButton, null);
        Require(automatic() == before, "Disabled AUTO button still toggles AI.");
        layer.Team1UI.AutoSwitch.ChangeAutoState(true);
        CheckAuto(true, false);
        autoButton.interactable = true;
        CheckAuto(true, true);
        layer.Team1UI.AutoSwitch.ChangeAutoState(before);
        layer.Team1UI.AutoSwitch.gameObject.SetActive(false);
        layer.RefreshPresentation();
        Require(!layer.Team1UI.AutoSwitch.gameObject.activeSelf, "AUTO styling reopens a hidden mode/tutorial control.");
        layer.Team1UI.AutoSwitch.gameObject.SetActive(true);
        CheckAuto(before, true);
        report.inputStatesChecked += 3;
        for (int index = 0; index < buttons.Length; index++)
        {
            Target(buttons[index].transform, buttons[index].name);
            var point = Center(buttons[index].transform, camera);
            var hit = Hit(events, point);
            ActionPointer(index, hit, point, true, "Skill center");
        }
        foreach (var joystick in layer.GetComponentsInChildren<UltimateJoystick>())
        {
            var point = Center(joystick.joystickBase, camera);
            var hit = Hit(events, point);
            Require(hit == joystick.gameObject || hit.transform.IsChildOf(joystick.transform), "Joystick is occluded by " + hit.name);
            report.raycastsChecked++;
            var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            Require(joystick.GetJoystickState(), "Joystick pointer down did not activate.");
            Require(state.All(value => !value()), "Joystick input leaked into a skill control.");
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            Require(!joystick.GetJoystickState(), "Joystick pointer up did not release."); report.inputStatesChecked += 2;
        }
        HUDCheckCameraTouch(layer, events, camera, report);
        input.PreparingMode(true);
        Require(new[] { input.AttackButton, input.Fire1Button, input.Fire2Button, input.DashButton, input.DreamComboBtn }.All(button => !button.interactable)
            && !input.MovementJoystick.enabled, "Preparing mode did not lock the attack controls and movement.");
        for (int index = 0; index < buttons.Length; index++)
        {
            var point = Center(buttons[index].transform, camera);
            var hit = Hit(events, point);
            Require(hit == buttons[index].gameObject || hit.transform.IsChildOf(buttons[index].transform),
                "Disabled countdown action is occluded: " + buttons[index].name + " by " + hit.name);
            report.raycastsChecked++;
            // EventTrigger receives down/up independently of Button.interactable.
            ActionPointer(index, hit, point, false, "Disabled countdown action");
        }
        input.PreparingMode(false);
        Require(buttons.All(button => button.interactable) && input.MovementJoystick.enabled, "Battle input did not recover after preparing mode.");
        Require(!input.DashButton.gameObject.activeSelf && !input.DreamComboBtn.gameObject.activeSelf,
            "No-focus battle input did not hide Dash and Dream Combo.");
        // Local posed models have no production input focus. Restore the fixture's
        // explicit display so the next viewport can test every action hit target.
        foreach (var button in buttons) button.gameObject.SetActive(true);
        for (int index = 0; index < buttons.Length; index++)
            ActionPointer(index, buttons[index].gameObject, Center(buttons[index].transform, camera), true, "Recovered battle action");
        report.inputStatesChecked += 2;
    }

    static void HUDCheckCameraTouch(FightingStepLayer layer, EventSystem events, Camera camera, HUDReport report)
    {
        var root = (RectTransform)layer.transform;
        var safe = LayoutBounds(root, PosCal.SafeAreaRect);
        var input = layer.InputsManager;
        var cameraPad = layer.GetComponentsInChildren<UltimateJoystick>().Single(joystick => joystick.joystickName == "RotateCamera");
        var movement = input.MovementJoystick;
        var state = new Func<bool>[] { () => input.attack, () => input.fire1, () => input.fire2, () => input.defendButtonHover, () => input.acc, () => input.dreamCombo };
        PointerEventData Pointer(int id, Vector2 point) => new PointerEventData(events)
            { pointerId = id, position = point, button = PointerEventData.InputButton.Left };
        Vector2 Point(float x, float y) => camera.WorldToScreenPoint(root.TransformPoint(
            new Vector2(Mathf.Lerp(safe.xMin, safe.xMax, x), Mathf.Lerp(safe.yMin, safe.yMax, y))));
        foreach (float x in new[] { .2f, .5f, .82f })
        foreach (float y in new[] { .47f, .65f, .82f })
        {
            var point = Point(x, y);
            var hit = Hit(events, point);
            Require(hit == cameraPad.gameObject, "Empty battlefield touch is blocked by " + hit.name + " at " + point);
            report.raycastsChecked++;
            var owner = Pointer(71, point);
            ExecuteEvents.ExecuteHierarchy(hit, owner, ExecuteEvents.pointerDownHandler);
            Require(cameraPad.GetJoystickState() && !movement.GetJoystickState() && state.All(value => !value()),
                "Camera touch leaked into a movement/action control.");
            var other = Pointer(72, point + new Vector2(96, 32));
            var knobBefore = cameraPad.joystick.localPosition;
            var axisBefore = new Vector2(cameraPad.HorizontalAxis, cameraPad.VerticalAxis);
            ExecuteEvents.ExecuteHierarchy(hit, other, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, other, ExecuteEvents.dragHandler);
            Require(cameraPad.joystick.localPosition == knobBefore
                && new Vector2(cameraPad.HorizontalAxis, cameraPad.VerticalAxis) == axisBefore,
                "A second finger took over the active camera gesture.");
            ExecuteEvents.ExecuteHierarchy(hit, other, ExecuteEvents.pointerUpHandler);
            Require(cameraPad.GetJoystickState(), "A rejected second finger ended the camera gesture.");
            // Input remains captured by its initial hit even when the owner
            // crosses a button; no pointer-down is dispatched to that button.
            owner.position = Center(input.AttackButton.transform, camera);
            ExecuteEvents.ExecuteHierarchy(hit, owner, ExecuteEvents.dragHandler);
            Require(cameraPad.GetJoystickState() && state.All(value => !value()), "A camera drag pressed an action button.");
            ExecuteEvents.ExecuteHierarchy(hit, owner, ExecuteEvents.pointerUpHandler);
            Require(!cameraPad.GetJoystickState() && cameraPad.HorizontalAxis == 0 && cameraPad.VerticalAxis == 0,
                "The owning camera pointer did not release/reset its input.");
            report.cameraTouchPointsChecked++;
            report.cameraPointerOwnershipChecks += 4;
            report.inputStatesChecked += 5;
        }

        var cameraPointer = Pointer(81, Point(.82f, .65f));
        var movementPointer = Pointer(82, Center(movement.joystickBase, camera));
        ExecuteEvents.ExecuteHierarchy(cameraPad.gameObject, cameraPointer, ExecuteEvents.pointerDownHandler);
        var movementHit = Hit(events, movementPointer.position);
        Require(movementHit == movement.gameObject || movementHit.transform.IsChildOf(movement.transform),
            "Camera pad swallowed the movement activation area.");
        ExecuteEvents.ExecuteHierarchy(movementHit, movementPointer, ExecuteEvents.pointerDownHandler);
        Require(cameraPad.GetJoystickState() && movement.GetJoystickState(), "Movement and camera cannot be used together.");
        ExecuteEvents.ExecuteHierarchy(movementHit, movementPointer, ExecuteEvents.pointerUpHandler);
        Require(cameraPad.GetJoystickState() && !movement.GetJoystickState(), "Releasing movement ended the camera gesture.");

        var buttons = new[] { input.AttackButton, input.Fire1Button, input.Fire2Button, input.DefendButton, input.DashButton, input.DreamComboBtn };
        for (int index = 0; index < buttons.Length; index++)
        {
            var point = Center(buttons[index].transform, camera);
            var hit = Hit(events, point);
            var pointer = Pointer(90 + index, point);
            Require(hit == buttons[index].gameObject || hit.transform.IsChildOf(buttons[index].transform),
                "Active camera input occluded an action button: " + buttons[index].name);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            Require(state[index]() && state.Where((_, other) => other != index).All(value => !value()) && cameraPad.GetJoystickState(),
                "Simultaneous camera/action pointer changed the wrong input.");
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            Require(state.All(value => !value()) && cameraPad.GetJoystickState(), "Releasing an action ended camera input.");
            report.raycastsChecked++;
            report.inputStatesChecked += 2;
        }
        ExecuteEvents.ExecuteHierarchy(cameraPad.gameObject, cameraPointer, ExecuteEvents.pointerUpHandler);
        Require(!cameraPad.GetJoystickState(), "Simultaneous input left camera active.");
        report.raycastsChecked++;
        report.inputStatesChecked += 5;
    }

    static Camera HUDCreateWorld(Transform parent, Scene scene, out Material contextMaterial)
    {
        var world = new GameObject("Local Battle Models"); world.transform.SetParent(parent, false);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(HUDModelPath);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/haruka/idle.anim");
        Require(source != null && idle != null, "Local battle model fixture is missing.");
        for (int index = 0; index < 8; index++)
        {
            var model = UnityEngine.Object.Instantiate(source, world.transform, false);
            foreach (var behaviour in model.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) idle.SampleAnimation(animator.gameObject, 0);
            model.transform.localPosition = HUDModelPosition(index);
            model.transform.localRotation = Quaternion.Euler(0, index < 4 ? 45 : 225, 0);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) { renderer.gameObject.layer = 0; renderer.lightProbeUsage = LightProbeUsage.Off; }
        }
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "Static Arena Context"; ground.transform.SetParent(world.transform, false);
        ground.transform.localScale = new Vector3(17, 0.08f, 17); ground.transform.localPosition = new Vector3(0, -0.12f, 0);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        contextMaterial = material;
        material.color = new Color(0.13f, 0.17f, 0.19f, 1);
        ground.GetComponent<Renderer>().sharedMaterial = material;
        var cameraObject = new GameObject("Local World Camera", typeof(Camera)); cameraObject.transform.SetParent(world.transform, false);
        var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
        camera.orthographic = true; camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
        camera.cullingMask = 1; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.1f, 0.12f, 1);
        camera.transform.position = new Vector3(8, 12, -12); camera.transform.LookAt(new Vector3(0, 0.4f, 0));
        var lightObject = new GameObject("Local Key Light", typeof(Light)); lightObject.transform.SetParent(world.transform, false);
        var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.cullingMask = 1;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        return camera;
    }

    static Vector3 HUDModelPosition(int index) => new Vector3((index % 4 - 1.5f) * 1.8f, 0, index < 4 ? -1.7f : 1.7f);

    static void HUDPlaceFloating(RectTransform root, Camera world, IList<SideUnitIcon> bars, LayoutViewport viewport)
    {
        for (int index = 0; index < bars.Count; index++)
        {
            var point = world.WorldToViewportPoint(HUDModelPosition(index + 4) + Vector3.up * 2.3f);
            var rect = (RectTransform)bars[index].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(point.x, point.y);
            rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
        }
    }

    static void HUDRenderCamera(Camera camera, RenderTexture target)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
    }

    static void HUDRelease(ref RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null;
    }
}
