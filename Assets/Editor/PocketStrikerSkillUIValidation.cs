using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using mainMenu;
using Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real skill UI geometry and tab filtering, in a stopped-editor local fixture.</summary>
public static class PocketStrikerSkillUIValidation
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
    const string Output = "Logs/UILayout/SkillUI";
    static readonly Type Loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true);
    static readonly string[] TabNames = { "NormalTab", "EX1Tab", "EX2Tab", "EX3Tab" };
    static readonly string[] Effects = { "normal", "EX1", "EX2", "EX3" };
    static readonly string[] Elements = { "defaultmagic", "redmagic", "bluemagic", "greenmagic", "lightmagic", "darkmagic" };

    [Serializable] public sealed class Report
    {
        public bool passed;
        public int gemBoundsChecked, raycastsChecked, filteredTabsChecked, iconInsetsChecked, particleShapesChecked;
        public int composedCentersChecked, selectedHaloCentersChecked, effectPresentationRectsChecked, loaderReparentsChecked;
        public List<string> errors = new List<string>();
        public string scope = "Actual SkillEditLayer/StoneListLayer layout and six element category models at 1080x1920 and 1206x2622. Real UILayerLoader.Load reparents a RenderTexture-backed effectBg RawImage; production PortraitSafeAreaLayout.Apply changes device-safe and banner insets. Gem centers and rotating bounds are projected through the RawImage presentation, then native UI raycasts hit the four category buttons. The layer-0 selected halo is projected directly and checked against the displayed layer-5 gems after resize. Production IniExTabs/onClick/RestFilter verifies four skill categories and repeated initialization; real stoneModel/StoneCell verifies inventory/nine-slot insets.";
        public string limitation = "Stopped-editor world-space UI fixture. Device ratios are controlled by real RenderTextures; safe-area and banner inputs are normalized to the host Screen for production Apply. Geometry follows the actual RawImage UV presentation, without frame-pixel analysis, native phone SDKs, network, account changes, camera-stack animation or Play-mode particle timing.";
    }

    public static void ValidateBatch()
    {
        var report = Run();
        Debug.Log("Skill UI validation passed: " + report.passed);
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before validating skill UI.");
        var report = new Report();
        var oldCanvas = PosCal.Canvas; var oldSafe = PosCal.SafeAreaRect; var oldPre = PreScene.target;
        var oldSelected = SkillStonesBox.Selected; var oldEvents = EventSystem.current;
        var oldBanner = BannerAds.target;
        var oldHanger = Loader.GetField("_hanger", StaticFields).GetValue(null);
        var oldFullHanger = Loader.GetField("_fullScreenHanger", StaticFields).GetValue(null);
        var oldEffectBg = Loader.GetField("effectBg", StaticFields).GetValue(null);
        var loaderQueues = (List<UILayer>)Loader.GetField("Queues", StaticFields).GetValue(null);
        var oldQueues = loaderQueues.ToArray();
        var stoneData = (IDictionary<string, dataAccess.StoneOfPlayerInfo>)typeof(dataAccess.Stones).GetField("Dic", StaticFields).GetValue(null);
        var stoneModels = (IDictionary<string, SKStoneItem>)typeof(dataAccess.Stones).GetField("RenderModelDic", StaticFields).GetValue(null);
        var savedData = stoneData.ToArray(); var savedModels = stoneModels.ToArray();
        var savedConfigs = SkillConfigTable.SkillConfigRefDic;
        var savedUnitRows = Units.rowList.ToArray();
        var unitLoaded = typeof(Units).GetField("isLoaded", StaticFields); var savedUnitLoaded = unitLoaded.GetValue(null);
        Application.LogCallback captureErrors = (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message + "\n" + stack);
        };
        Application.logMessageReceived += captureErrors;
        try
        {
            stoneData.Clear(); stoneModels.Clear();
            loaderQueues.Clear();
            // The supported category list comes from unit rows. A standalone batch
            // Editor starts without the menu's asynchronous table initialization.
            Units.rowList.Clear();
            Units.rowList.Add(new Units.Row { RECORD_ID = "1", REAL_NAME = "adam", MONSTER_TYPE = "human", ELEMENT = "3",
                RARITY_LEVEL = "1", BASIC_MOVEMENT_PACK = "adam", MOVE_TYPE = "Move1", DODGE_SKILL = "Rush", DEFENDABLE_FLAG = "1" });
            unitLoaded.SetValue(null, true);
            SkillConfigTable.SkillConfigRefDic = new Dictionary<string, SkillConfig>();
            for (int ex = 0; ex < 4; ex++)
            {
                var id = "skill-ui-fixture-" + ex;
                SkillConfigTable.SkillConfigRefDic[id] = new SkillConfig { RECORD_ID = id, TYPE = "human", SP_LEVEL = ex,
                    AIAttrs = new AIAttrs { AI_MIN_DIS = 0, AI_MAX_DIS = 20 } };
                stoneData[id] = new dataAccess.StoneOfPlayerInfo { InstanceId = id, SkillId = id, Born = "false", Level = 1 };
            }
            foreach (var device in new[] { new Vector2Int(1080, 1920), new Vector2Int(1206, 2622) })
            foreach (var page in new[] { "SkillEditLayer", "StoneListLayer" })
            {
                try { CheckPage(page, device, stoneModels, report); }
                catch (Exception exception) { report.errors.Add(page + "/" + device + ": " + exception.GetBaseException().Message); }
                stoneModels.Clear();
            }
        }
        finally
        {
            stoneData.Clear(); foreach (var entry in savedData) stoneData.Add(entry.Key, entry.Value);
            stoneModels.Clear(); foreach (var entry in savedModels) stoneModels.Add(entry.Key, entry.Value);
            SkillConfigTable.SkillConfigRefDic = savedConfigs;
            Units.rowList.Clear(); Units.rowList.AddRange(savedUnitRows); unitLoaded.SetValue(null, savedUnitLoaded);
            PosCal.Canvas = oldCanvas; PosCal.SafeAreaRect = oldSafe; PreScene.target = oldPre; SkillStonesBox.Selected = oldSelected;
            BannerAds.target = oldBanner;
            Loader.GetField("_hanger", StaticFields).SetValue(null, oldHanger);
            Loader.GetField("_fullScreenHanger", StaticFields).SetValue(null, oldFullHanger);
            Loader.GetMethod("SetEffectBg").Invoke(null, new[] { oldEffectBg });
            loaderQueues.Clear(); loaderQueues.AddRange(oldQueues);
            // Unity rejects assigning a null EventSystem; fixture destruction already removes it.
            if (oldEvents != null) EventSystem.current = oldEvents;
            Application.logMessageReceived -= captureErrors;
            report.passed = report.errors.Count == 0 && report.filteredTabsChecked == 16 && report.gemBoundsChecked == 1152
                && report.raycastsChecked == 48 && report.iconInsetsChecked == 24 && report.particleShapesChecked > 0
                && report.composedCentersChecked == 288 && report.selectedHaloCentersChecked == 48
                && report.effectPresentationRectsChecked == 72 && report.loaderReparentsChecked == 12;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckPage(string page, Vector2Int device, IDictionary<string, SKStoneItem> stoneModels, Report report)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Skill UI Fixture"); SceneManager.MoveGameObjectToScene(rig, scene);
        var texture = new RenderTexture(device.x, device.y, 16); texture.Create();
        var effectTexture = new RenderTexture(device.x, device.y, 16); effectTexture.Create();
        EventSystem eventSystem = null;
        try
        {
            var canvasRect = NewRect("Canvas", rig.transform);
            canvasRect.sizeDelta = new Vector2(1080, device.y * 1080f / device.x); canvasRect.localScale = Vector3.one * .01f;
            var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.gameObject.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            var uiCamera = Camera(rig.transform, texture, canvasRect.rect.height * .005f);
            var effectCamera = Camera(rig.transform, effectTexture, 30); canvas.worldCamera = uiCamera;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var safe = NewRect("Safe Area", canvasRect); safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.Canvas = canvas; PosCal.SafeAreaRect = safe;
            var safeLayout = canvas.gameObject.AddComponent<PortraitSafeAreaLayout>();
            var bannerObject = new GameObject("Simulated banner occupancy"); bannerObject.SetActive(false); bannerObject.transform.SetParent(rig.transform, false);
            var banner = bannerObject.AddComponent<BannerAds>(); bannerObject.SetActive(true); BannerAds.target = banner;
            ApplyDeviceInsets(safeLayout, canvas, safe, banner, device, 0);
            var effectBgRect = NewRect("effectBg", canvasRect); effectBgRect.anchorMin = Vector2.zero; effectBgRect.anchorMax = Vector2.one;
            effectBgRect.offsetMin = effectBgRect.offsetMax = Vector2.zero;
            var effectBg = effectBgRect.gameObject.AddComponent<RawImage>(); effectBg.texture = effectTexture; effectBg.raycastTarget = false;
            Loader.GetMethod("SetHanger", new[] { typeof(Transform), typeof(Transform) }).Invoke(null, new object[] { safe, canvasRect });
            Loader.GetMethod("SetEffectBg").Invoke(null, new object[] { effectBgRect });
            // ReturnLayer has no startup/services lifecycle. Use a real new-load path,
            // then its existing-load path to move the effect presentation between hangers.
            Require(Object.FindObjectsByType<ReturnLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Close existing ReturnLayer instances before isolated skill UI validation.");
            var loaderLoad = Loader.GetMethod("Load").MakeGenericMethod(typeof(ReturnLayer));
            var loadedReturn = (ReturnLayer)loaderLoad.Invoke(null, new object[] { false, null, false });
            Require(loadedReturn != null && effectBgRect.parent == safe, "The production loader did not reparent the effect presentation to the safe hanger.");
            loadedReturn.gameObject.SetActive(false); report.loaderReparentsChecked++;
            var preRoot = new GameObject("Inactive local PreScene"); preRoot.SetActive(false); preRoot.transform.SetParent(rig.transform, false);
            PreScene.target = preRoot.AddComponent<PreScene>(); PreScene.target.stonesTempContainer = NewRect("Stone Temp", rig.transform);
            eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>(); eventSystem.transform.SetParent(rig.transform, false);
            // Stopped-editor components do not run their normal registration lifecycle.
            if (!RegisteredEventSystems().Contains(eventSystem))
                typeof(EventSystem).GetMethod("OnEnable", Fields).Invoke(eventSystem, null);
            EventSystem.current = eventSystem;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/" + page + ".prefab");
            var copies = new Dictionary<Transform, Transform>(); var root = (RectTransform)Copy(source.transform, safe, copies);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var sourceLayer = source.GetComponent<UILayer>(); var layer = root.gameObject.AddComponent<UILayer>();
            foreach (var field in new[] { "top", "middle", "bottom" }) Set(layer, field, copies[Get<Transform>(sourceLayer, field)]);
            foreach (var helper in source.GetComponentsInChildren<MidAreaSizeHelper>(true))
            {
                var target = copies[helper.transform].gameObject.AddComponent<MidAreaSizeHelper>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(helper), target);
                var originalRect = Get<RectTransform>(helper, "rectTransform");
                Set(target, "rectTransform", originalRect != null ? copies[originalRect] : target.transform);
            }
            layer.ResizeAreas(); Canvas.ForceUpdateCanvases();
            var sourceBox = source.GetComponentInChildren<SkillStonesBox>(true);
            var box = copies[sourceBox.transform].gameObject.AddComponent<SkillStonesBox>(); box.FocusingType = "human";
            var dropdown = copies[Get<Dropdown>(sourceBox, "types").transform].gameObject.AddComponent<Dropdown>(); Set(box, "types", dropdown);
            foreach (var field in TabNames.Concat(new[] { "orderBtn" }))
            {
                var original = Get<BOButton>(sourceBox, field); var target = copies[original.transform].gameObject.AddComponent<BOButton>();
                target.transition = Selectable.Transition.None; target.targetGraphic = target.GetComponent<Graphic>(); Set(box, field, target);
            }
            foreach (var field in new[] { "closeCheckBox", "nearCheckBox", "farCheckBox" })
            {
                var original = Get<Toggle>(sourceBox, field); var target = copies[original.transform].gameObject.AddComponent<Toggle>();
                target.SetIsOnWithoutNotify(original.isOn); Set(box, field, target);
            }
            Set(box, "orderButtonText", copies[Get<Text>(sourceBox, "orderButtonText").transform].GetComponent<Text>());
            Set(box, "cellPrefab", Get<StoneCell>(sourceBox, "cellPrefab"));
            var grid = copies[sourceBox.Grid.transform].GetComponent<GridLayoutGroup>(); Set(box, "grid", grid);
            var scroll = copies[sourceBox.ScrollRect.transform].gameObject.AddComponent<ScrollRect>();
            scroll.content = (RectTransform)grid.transform; scroll.viewport = (RectTransform)copies[sourceBox.ScrollRect.viewport]; Set(box, "scrollRect", scroll);
            var selected = copies[Get<GameObject>(sourceBox, "selectedFrame").transform].gameObject;
            Set(box, "selectedFrame", selected); SkillStonesBox.Selected = selected;
            var sourceManager = sourceBox._tabEffects;
            var manager = copies[sourceManager.transform].gameObject.AddComponent<SkillStoneBoxTabEffectsManager>(); box._tabEffects = manager;
            var group = new ElementStoneTagsGroup(); Set(manager, "_focusingEffectsGroup", group);
            var groupEffects = Get<IDictionary<int, ParticleSystem>>(group, "_btnEffectsSetsForStoneBox");
            var halo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/buttonEffects/selectedTab.prefab"), manager.transform, false).GetComponent<ParticleSystem>();
            Set(group, "_selectedTab", halo);
            var buttons = TabNames.Select(name => Get<BOButton>(box, name)).ToArray();
            Require(buttons.All(button => button.GetComponent<Image>().color.a == 0), "A legacy category button background is visible.");
            for (int ex = 0; ex < 4; ex++)
            {
                var icon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/BasicSprites/stoneModel.prefab"), PreScene.target.stonesTempContainer, false).GetComponent<SKStoneItem>();
                var id = "skill-ui-fixture-" + ex; icon.instanceId = id; icon._SkillConfig = SkillConfigTable.SkillConfigRefDic[id]; stoneModels[id] = icon;
                if (ex == 3) icon.image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/OrganizedResources/InUse/ExternalAssets/SkillIcon/198.png");
                var effect = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/buttonEffects/bluemagic/" + Effects[ex] + ".prefab"), manager.transform, false).GetComponent<ParticleSystem>();
                groupEffects[ex] = effect; group.RefreshBoxEffects(ex, (RectTransform)buttons[ex].transform, effectCamera);
            }
            box.GenerateCells(); box.FilterFeatureRefresh(true); box.FilterFeatureRefresh(true);
            Require(dropdown.options.Count == 1 && !dropdown.gameObject.activeSelf, "The single supported skill type shows redundant dropdown UI.");
            box.IniExTabs(); box.IniExTabs(); int pressed = 0; box.ExTabPressed += () => pressed++;
            for (int ex = 0; ex < 4; ex++)
            {
                buttons[ex].onClick.Invoke();
                var visible = box.GetComponentsInChildren<StoneCell>().Select(cell => cell.GetItem()).Where(item => item != null).ToArray();
                Require(visible.Length == 1 && visible[0].instanceId == "skill-ui-fixture-" + ex,
                    "Category onClick did not show only its matching skill stones: " + ex);
                Require(pressed == ex + 1, "Repeated initialization duplicated category click handlers.");
                report.filteredTabsChecked++;
            }
            var cell = box.GetComponentsInChildren<StoneCell>().First(cell => cell.GetItem() != null);
            CheckInsets(cell, report);
            var nine = Object.Instantiate(Get<StoneCell>(sourceBox, "cellPrefab"), root, false); nine.cellPhase = StoneCell.CellPhase.NineSlotCell;
            var equipped = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/BasicSprites/stoneModel.prefab"), PreScene.target.stonesTempContainer, false).GetComponent<SKStoneItem>();
            equipped._SkillConfig = SkillConfigTable.SkillConfigRefDic["skill-ui-fixture-3"]; equipped.image.sprite = stoneModels["skill-ui-fixture-3"].image.sprite;
            Require(equipped.image.sprite != null, "The authored EX3 skill sprite is missing."); nine.AddItem(equipped); CheckInsets(nine, report);

            foreach (var element in Elements)
            {
                var effects = new List<SkillStoneTabEffectLayout>();
                try
                {
                    for (int ex = 0; ex < 4; ex++)
                    {
                        var effect = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/buttonEffects/" + element + "/" + Effects[ex] + ".prefab"), manager.transform, false);
                        Require(effect.GetComponentsInChildren<Collider>(true).Length == 0 && effect.GetComponentsInChildren<Graphic>(true).Length == 0,
                            "Category artwork unexpectedly intercepts input.");
                        var layout = effect.AddComponent<SkillStoneTabEffectLayout>(); layout.Initialize((RectTransform)buttons[ex].transform, effectCamera); effect.SetActive(true); effects.Add(layout);
                    }
                    for (int state = 0; state < 3; state++)
                    {
                        ApplyDeviceInsets(safeLayout, canvas, safe, banner, device, state);
                        layer.ResizeAreas(); Canvas.ForceUpdateCanvases();
                        CheckEffectPresentationRect(effectBgRect, canvasRect, uiCamera, device, state, report);
                        if (element == Elements[0]) Draw(canvas, uiCamera, texture);
                        for (int ex = 0; ex < 4; ex++)
                        {
                            var layout = effects[ex]; layout.RefreshLayout(); var rect = (RectTransform)buttons[ex].transform;
                            var allowed = ScreenRect(rect, uiCamera);
                            var center = PresentedPoint(layout.transform.position, effectCamera, effectBg, uiCamera);
                            Require(Vector2.Distance(center, allowed.center) < .1f, "Displayed category gem no longer follows its button after RenderTexture presentation: "
                                + page + "/" + device + "/" + element + "/" + Effects[ex] + "/inset=" + state + ", gem=" + center + ", button=" + allowed.center);
                            report.composedCentersChecked++;
                            if (element == "defaultmagic") CheckParticleShapes(layout, allowed, effectCamera, effectBg, uiCamera, report);
                            if (element == Elements[0])
                            {
                                manager.SetSelectedTabPos(ex);
                                var haloLayout = halo.GetComponent<SkillStoneTabEffectLayout>();
                                Require(haloLayout != null && haloLayout.Target == rect, "Selecting the category did not bind its halo to the button.");
                                haloLayout.RefreshLayout();
                                var haloCenter = (Vector2)effectCamera.WorldToScreenPoint(halo.transform.position);
                                Require(halo.gameObject.layer == 0 && effects[ex].gameObject.layer == 5, "Fixture does not reproduce direct halo versus RenderTexture gem layers.");
                                Require(Vector2.Distance(center, haloCenter) < .1f && Vector2.Distance(haloCenter, allowed.center) < .1f,
                                    "Displayed category gem and directly rendered selection halo have different centers after inset change: " + state + "/" + Effects[ex]);
                                report.selectedHaloCentersChecked++;
                                var results = new List<RaycastResult>(); var data = new PointerEventData(eventSystem) { position = center };
                                canvas.GetComponent<GraphicRaycaster>().Raycast(data, results);
                                var hit = results.Where(result => result.gameObject != null).OrderByDescending(result => result.depth).FirstOrDefault();
                                var image = buttons[ex].GetComponent<Image>();
                                Require(hit.gameObject != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == buttons[ex].gameObject,
                                    "Category " + Effects[ex] + " touch at " + center + " reaches " + (hit.gameObject != null ? hit.gameObject.name : "nothing")
                                    + " (hit depth=" + hit.depth + "); button=" + buttons[ex].name + ", depth=" + image.depth + ", cull=" + image.canvasRenderer.cull
                                    + ", cullTransparent=" + image.canvasRenderer.cullTransparentMesh + ", active=" + image.isActiveAndEnabled
                                    + ", inside=" + RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, center, uiCamera)
                                    + ", filter=" + image.Raycast(center, uiCamera) + ", pixelRect=" + uiCamera.pixelRect);
                                report.raycastsChecked++;
                            }
                            foreach (int angle in new[] { 0, 45, 90, 135 })
                            {
                                foreach (var mesh in layout.GetComponentsInChildren<MeshFilter>()) mesh.transform.localRotation = Quaternion.Euler(23, angle, 17);
                                layout.RefreshLayout();
                                foreach (var renderer in layout.GetComponentsInChildren<MeshRenderer>())
                                foreach (var point in BoundsCorners(renderer.bounds))
                                {
                                    var pixel = PresentedPoint(point, effectCamera, effectBg, uiCamera);
                                    Require(allowed.Contains(pixel), "Rotating category geometry extends outside its button: " + element + "/" + Effects[ex]);
                                }
                                report.gemBoundsChecked++;
                            }
                        }
                    }
                }
                finally { foreach (var effect in effects) Object.DestroyImmediate(effect.gameObject); }
            }
            foreach (var fullScreen in new[] { true, false })
            {
                Require(loaderLoad.Invoke(null, new object[] { false, null, fullScreen }) == loadedReturn, "Existing loader path did not reuse its layer.");
                Require(effectBgRect.parent == (fullScreen ? canvasRect : safe), "Existing loader path left the effect presentation in the previous hanger.");
                Canvas.ForceUpdateCanvases(); CheckEffectPresentationRect(effectBgRect, canvasRect, uiCamera, device, 2, null);
                report.loaderReparentsChecked++;
            }
        }
        finally
        {
            if (eventSystem != null && RegisteredEventSystems().Contains(eventSystem))
                typeof(EventSystem).GetMethod("OnDisable", Fields).Invoke(eventSystem, null);
            Loader.GetMethod("SetEffectBg").Invoke(null, new object[] { null });
            Object.DestroyImmediate(rig); texture.Release(); Object.DestroyImmediate(texture); effectTexture.Release(); Object.DestroyImmediate(effectTexture); EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void CheckInsets(StoneCell cell, Report report)
    {
        var grid = cell.transform.parent.GetComponent<GridLayoutGroup>(); if (grid != null) grid.enabled = false;
        foreach (var size in new[] { 82f, 120f, 180f })
        {
            var rect = (RectTransform)cell.transform; rect.sizeDelta = new Vector2(size, size); rect.ForceUpdateRectTransforms();
            var icon = (RectTransform)cell.GetItem().transform;
            foreach (var corner in Corners(icon))
            {
                var point = (Vector2)rect.InverseTransformPoint(corner);
                Require(point.x > rect.rect.xMin && point.x < rect.rect.xMax && point.y > rect.rect.yMin && point.y < rect.rect.yMax,
                    "Skill sprite extends into an adjacent cell.");
            }
            Require(cell.GetItem().image.preserveAspect, "Skill sprite aspect ratio is not preserved."); report.iconInsetsChecked++;
        }
        if (grid != null) grid.enabled = true;
    }
    static void ApplyDeviceInsets(PortraitSafeAreaLayout layout, Canvas canvas, RectTransform safe, BannerAds banner, Vector2Int device, int state)
    {
        var deviceSafe = state == 0 ? new Rect(0, 0, device.x, device.y)
            : new Rect(0, device.y * .04f, device.x, device.y * .90f);
        float bannerPixels = state == 2 ? device.x * 90f / 390f : 0;
        Require(Screen.width > 0 && Screen.height > 0, "The Editor Screen has no valid dimensions for safe-area validation.");
        Set(layout, "safeAreaForValidation", (Rect?)new Rect(deviceSafe.x * Screen.width / device.x, deviceSafe.y * Screen.height / device.y,
            deviceSafe.width * Screen.width / device.x, deviceSafe.height * Screen.height / device.y));
        Set(banner, "_occupiedHeightPixels", bannerPixels * Screen.height / device.y);
        layout.Initialize(canvas, safe);
        var expected = PortraitSafeAreaLayout.ContentSafeArea(deviceSafe, device, bannerPixels);
        Require(Vector2.Distance(safe.anchorMin, new Vector2(expected.xMin / device.x, expected.yMin / device.y)) < .0001f
            && Vector2.Distance(safe.anchorMax, new Vector2(expected.xMax / device.x, expected.yMax / device.y)) < .0001f,
            "Production safe-area Apply did not reserve the device and banner insets exactly once.");
    }
    static void CheckEffectPresentationRect(RectTransform effectBg, RectTransform canvas, Camera camera, Vector2Int device, int state, Report report)
    {
        var expected = ScreenRect(canvas, camera); var actual = ScreenRect(effectBg, camera);
        Require(Vector2.Distance(actual.min, expected.min) < .1f && Vector2.Distance(actual.max, expected.max) < .1f,
            "RenderTexture effectBg no longer covers the full canvas after loader reparent and safe/banner Apply: " + device + "/inset=" + state
            + ", displayed=" + actual + ", canvas=" + expected + ", parent=" + effectBg.parent.name);
        if (report != null) report.effectPresentationRectsChecked++;
    }
    // The effect camera pixel coordinates are texture coordinates, not final UI
    // screen coordinates. This is the extra transform omitted by the old checks.
    static Vector2 PresentedPoint(Vector3 world, Camera effectCamera, RawImage presentation, Camera uiCamera)
    {
        var viewport = effectCamera.WorldToViewportPoint(world); var uv = presentation.uvRect;
        var normalized = new Vector2((viewport.x - uv.x) / uv.width, (viewport.y - uv.y) / uv.height);
        var rect = presentation.rectTransform; var local = rect.rect.min + Vector2.Scale(rect.rect.size, normalized);
        return RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(local));
    }
    static void CheckParticleShapes(SkillStoneTabEffectLayout layout, Rect allowed, Camera camera, RawImage presentation, Camera uiCamera, Report report)
    {
        foreach (var system in layout.GetComponentsInChildren<ParticleSystem>(true))
        {
            var shape = system.shape;
            float radius = system.main.startSize.constantMax * .5f;
            if (shape.enabled) radius += shape.position.magnitude + shape.radius * Mathf.Max(Mathf.Abs(shape.scale.x), Mathf.Abs(shape.scale.y), Mathf.Abs(shape.scale.z));
            var scale = system.transform.lossyScale; radius *= Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var position = system.transform.position;
            foreach (var delta in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down })
                Require(allowed.Contains(PresentedPoint(position + delta * radius, camera, presentation, uiCamera)), "Fallback particle emission extends outside its category button.");
            report.particleShapesChecked++;
        }
    }
    static Transform Copy(Transform source, Transform parent, IDictionary<Transform, Transform> copies)
    {
        var target = source is RectTransform ? NewRect(source.name, parent) : new GameObject(source.name).transform;
        target.SetParent(parent, false); target.localPosition = source.localPosition; target.localRotation = source.localRotation; target.localScale = source.localScale;
        if (source is RectTransform from && target is RectTransform to)
        { to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot; to.sizeDelta = from.sizeDelta; to.anchoredPosition3D = from.anchoredPosition3D; }
        foreach (var component in source.GetComponents<Component>())
            if (component is Image || component is Text || component is RawImage || component is GridLayoutGroup || component is HorizontalLayoutGroup
                || component is VerticalLayoutGroup || component is LayoutElement || component is ContentSizeFitter || component is RectMask2D || component is Mask)
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(component), target.gameObject.AddComponent(component.GetType()));
        // Added UI graphics otherwise use the new CanvasRenderer's default transparent culling,
        // rather than the authored transparent hit targets' setting.
        var sourceRenderer = source.GetComponent<CanvasRenderer>(); var targetRenderer = target.GetComponent<CanvasRenderer>();
        if (sourceRenderer != null && targetRenderer != null) targetRenderer.cullTransparentMesh = sourceRenderer.cullTransparentMesh;
        target.gameObject.SetActive(source.gameObject.activeSelf); copies.Add(source, target);
        foreach (Transform child in source) Copy(child, target, copies); return target;
    }
    static RectTransform NewRect(string name, Transform parent) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
    static Camera Camera(Transform parent, RenderTexture texture, float size)
    { var camera = new GameObject("Fixture Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(parent, false); camera.scene = parent.gameObject.scene; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = size; camera.targetTexture = texture; camera.enabled = false; return camera; }
    static void Draw(Canvas canvas, Camera camera, RenderTexture target)
    {
        foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) { graphic.SetAllDirty(); graphic.Rebuild(CanvasUpdate.PreRender); }
        Canvas.ForceUpdateCanvases();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request); else camera.Render();
        Canvas.ForceUpdateCanvases();
    }
    static Vector3[] Corners(RectTransform rect) { var points = new Vector3[4]; rect.GetWorldCorners(points); return points; }
    static Rect ScreenRect(RectTransform rect, Camera camera)
    { var points = Corners(rect).Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray(); return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y)); }
    static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
    { for (int i = 0; i < 8; i++) yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)); }
    static FieldInfo Field(object target, string name) { var type = target.GetType(); FieldInfo info = null; while (type != null && info == null) { info = type.GetField(name, Fields); type = type.BaseType; } return info ?? throw new MissingFieldException(name); }
    static IList<EventSystem> RegisteredEventSystems() => (IList<EventSystem>)typeof(EventSystem).GetField("m_EventSystems", StaticFields).GetValue(null);
    static T Get<T>(object target, string field) => (T)Field(target, field).GetValue(target);
    static void Set(object target, string field, object value) => Field(target, field).SetValue(target, value);
    static void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
}
