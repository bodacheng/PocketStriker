using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Audits authored UI geometry in isolated preview scenes. Game behaviours are never instantiated:
/// only transforms, standard UI layout/graphics and the base UILayer sizing code are copied.
/// </summary>
public static class PocketStrikerUILayoutValidation
{
    const string ReportPath = "Logs/UILayout/report.json";
    const float Tolerance = 2f;
    // Actual Load(..., loadToFullScreen: true) call sites in StartUpPresentation,
    // FightingProcess, PreparingProcess, FrontPage and the shared modal helpers.
    static readonly HashSet<string> FullScreenLayers = new HashSet<string> {
        "TitleScreenLayer", "TitleBgLayer", "ProgressLayer", "UnitInstructionLayer", "PopupLayer", "AskIfLinkDeviceLayer", "HighLightLayer"
    };
    static readonly string[] AuthoredState = { "authored" };
    static readonly string[] SettingStates = {
        "authored", "volume", "account-email-unset", "account-email-set", "device-linked", "device-unlinked", "support", "language", "nickname"
    };
    static readonly string[] SkillEditStates = { "authored", "combo-explanation" };
    static bool running;

    [Serializable]
    public sealed class Report
    {
        public string unityVersion;
        public string utcTime;
        public bool passed;
        public bool regionGeometryPassed;
        public bool currentScenesUnchanged;
        public bool safeAreaInitializationPassed;
        public int registeredLayers;
        public int expectedCases;
        public int casesChecked;
        public int errors;
        public int warnings;
        public string scope = "Authored active prefab states plus explicit Settings panel/account/device and skill combo-explanation visibility variants, built-in layout, and UILayer.ResizeAreas; game Awake/Start, account and network code are not executed.";
        public string[] limitations = {
            "Runtime-created rows, localization changes, animation states, camera-rendered content and screen-specific Start/Update positioning need Play-mode visual review.",
            "Text checks use authored RectTransforms, not per-glyph ink bounds. Deliberately overlapping controls within the same region are not inferred to be errors.",
            "Inactive objects are excluded except explicitly selected Settings/skill-combo visibility states. Zero-alpha CanvasGroups are checked at their authored animation rest pose with alpha lifted in the preview; this does not validate animated positions.",
            "Full-screen dismissal controls are explicitly recorded as intentional overlays.",
            "Preview copies omit game behaviours. Built-in layout components and graphics are copied; a copied base UILayer invokes the actual shared sizing implementation."
        };
        public List<CaseResult> cases = new List<CaseResult>();
        public List<CompositionResult> compositions = new List<CompositionResult>();
        public List<InitializationResult> safeAreaInitializations = new List<InitializationResult>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class InitializationResult
    {
        public string fallback;
        public Vector2 screenPixels;
        public Rect safeAreaPixels;
        public bool separateSafeAreaChild;
        public bool sameSafeAreaAfterRepeat;
        public bool oneLayoutComponent;
        public bool rootAnchorsUnchanged;
        public bool referenceResolutionStable;
        public bool referenceResolutionMatchesSafeArea;
        public Vector2 referenceResolutionAfterFirst;
        public Vector2 referenceResolutionAfterRepeat;
        public bool passed;
    }

    [Serializable]
    public sealed class CaseResult
    {
        public string layer;
        public string prefab;
        public string device;
        public string parenting;
        public string state;
        public Vector2 screenPixels;
        public Rect safeAreaPixels;
        public Vector2 canvasSize;
        public Rect safeBounds;
        public string regionCoverage;
        public string previewPng;
        public bool previewContainsUI;
        public string previewScope;
        public bool repeatResizeStable;
        public int textAndControlsChecked;
        public int alphaRestPoseContentChecks;
        public int inactiveOrDisabledContent;
        public bool settingsContentCentered;
        public float settingsContentCenterOffset;
        public int clippedContentChecks;
        public int intentionalFullscreenControls;
        public int fullScreenBackdropsChecked;
        public int comboTranslationsChecked;
        public string[] omittedBehaviours;
        public List<RegionResult> regions = new List<RegionResult>();
        public List<Finding> findings = new List<Finding>();
        public List<ContentResult> content = new List<ContentResult>();
    }

    [Serializable]
    public sealed class ContentResult
    {
        public string path;
        public string component;
        public Rect bounds;
        public bool animationRestPose;
        public bool hasAuthoredGraphic;
    }

    [Serializable]
    public sealed class CompositionResult
    {
        public string page;
        public string state;
        public string device;
        public string[] layers;
        public string visibilityRule;
        public int layerPairsChecked;
        public List<Finding> findings = new List<Finding>();
    }

    [Serializable]
    public sealed class RegionResult
    {
        public string name;
        public string path;
        public Rect bounds;
        public bool active;
    }

    [Serializable]
    public sealed class Finding
    {
        public string severity;
        public string kind;
        public string path;
        public string region;
        public Rect bounds;
        public Rect allowedBounds;
        public string message;
    }

    sealed class Device
    {
        public string name;
        public Vector2 size;
        public Rect safe;
        public Device(string name, int width, int height, int bottom = 0, int top = 0)
        {
            this.name = name;
            size = new Vector2(width, height);
            safe = new Rect(0, bottom, width, height - bottom - top);
        }
    }

    sealed class SceneState
    {
        public Scene scene;
        public bool dirty;
        public string path;
    }

    [MenuItem("PocketStriker/Validation/Battle Loading Screen")]
    public static void ValidateBattleLoading()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before checking the loading screen.");
        var previousCanvas = PosCal.Canvas;
        var previousSafeArea = PosCal.SafeAreaRect;
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ExternalAssets/BattleGround" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<BattleGround>() == null)
                    report.failures.Add("BattleGround component missing at prefab root: " + path);
            }
            foreach (var layerName in new[] { "UnitInstructionLayer", "ProgressLayer" })
            foreach (var device in new[] {
                new Device("Phone 540x960", 540, 960),
                new Device("Small phone 375x667", 375, 667),
                new Device("Notched phone 390x844", 390, 844, 34, 47),
                new Device("Screenshot phone 1206x2622", 1206, 2622, 102, 186),
                new Device("iPad 768x1024", 768, 1024)
            })
            {
                var source = Resources.Load<GameObject>("DummyLayerSystem/" + layerName);
                var result = new CaseResult { layer = layerName, device = device.name,
                    parenting = "full-screen canvas (runtime route)", state = "authored", screenPixels = device.size,
                    safeAreaPixels = device.safe };
                CheckCase(source, device, result, true);
                report.cases.Add(result);
            }
            report.errors = report.failures.Count + report.cases.Sum(item => item.findings.Count(finding => finding.severity == "error"));
            report.warnings = report.cases.Sum(item => item.findings.Count(finding => finding.severity == "warning"));
            report.casesChecked = report.expectedCases = report.cases.Count;
            report.passed = report.errors == 0 && report.warnings == 0;
            Directory.CreateDirectory("Logs/UILayout");
            File.WriteAllText("Logs/UILayout/battle-loading.json", JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("Battle loading layout failed; see Logs/UILayout/battle-loading.json.");
            Debug.Log("[BattleLoading] PASS: ten layout cases, full-screen backgrounds, safe content, Chinese glyphs, repeated resize and battlefield prefab components.");
        }
        finally { PosCal.Canvas = previousCanvas; PosCal.SafeAreaRect = previousSafeArea; }
    }

    [MenuItem("PocketStriker/Validation/UI Layout")]
    public static void Validate()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UILayout] Stop Play mode before running UI Layout validation.");
            return;
        }
        running = true;
        var previousCanvas = PosCal.Canvas;
        var previousSafeArea = PosCal.SafeAreaRect;
        var previousActiveScene = SceneManager.GetActiveScene();
        var sceneStates = Enumerable.Range(0, SceneManager.sceneCount).Select(index => SceneManager.GetSceneAt(index))
            .Select(scene => new SceneState { scene = scene, dirty = scene.isDirty, path = scene.path }).ToArray();
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        try
        {
            CheckSafeAreaInitialization(report, true);
            CheckSafeAreaInitialization(report, false);
            report.safeAreaInitializationPassed = report.safeAreaInitializations.Count == 2
                && report.safeAreaInitializations.All(result => result.passed);
            var loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader");
            var pathsField = loader?.GetField("Paths", BindingFlags.Static | BindingFlags.NonPublic);
            var paths = pathsField?.GetValue(null) as IEnumerable<KeyValuePair<string, string>>;
            if (paths == null) throw new InvalidOperationException("UILayerLoader.Paths could not be read; coverage is unknown.");
            var entries = paths.OrderBy(pair => pair.Key).ToArray();
            report.registeredLayers = entries.Length;
            report.expectedCases = entries.Sum(entry => (FullScreenLayers.Contains(entry.Key) ? 8 : 4) * StatesForLayer(entry.Key).Length);
            var devices = new[] {
                new Device("Phone 540x960", 540, 960),
                new Device("Small phone 375x667", 375, 667),
                new Device("Notched phone 390x844", 390, 844, 34, 47),
                new Device("iPad 768x1024", 768, 1024)
            };
            foreach (var entry in entries)
            {
                var prefab = Resources.Load<GameObject>(entry.Value);
                if (prefab == null)
                {
                    report.failures.Add("Missing registered prefab: " + entry.Key + " (" + entry.Value + ")");
                    continue;
                }
                foreach (var device in devices)
                foreach (var fullScreen in FullScreenLayers.Contains(entry.Key) ? new[] { false, true } : new[] { false })
                foreach (var state in StatesForLayer(entry.Key))
                {
                    var result = new CaseResult {
                        layer = entry.Key, prefab = AssetDatabase.GetAssetPath(prefab), device = device.name,
                        screenPixels = device.size, safeAreaPixels = device.safe,
                        parenting = fullScreen ? "full-screen canvas (runtime route)" : "safe-area child", state = state
                    };
                    report.cases.Add(result);
                    try { CheckCase(prefab, device, result, fullScreen); }
                    catch (Exception exception) { Add(result, "error", "validation-exception", entry.Key, "", default, default, exception.ToString()); }
                    report.casesChecked++;
                }
            }
            CheckCompositions(report);
        }
        catch (Exception exception) { report.failures.Add(exception.ToString()); }
        finally
        {
            PosCal.Canvas = previousCanvas;
            PosCal.SafeAreaRect = previousSafeArea;
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            report.currentScenesUnchanged = SceneManager.sceneCount == sceneStates.Length && sceneStates.All(before => {
                var scene = before.scene;
                return scene.IsValid() && scene.path == before.path && scene.isDirty == before.dirty;
            });
            if (!report.currentScenesUnchanged) report.failures.Add("Open scene set or scene dirty state changed during validation.");
            report.errors = report.failures.Count + report.cases.Sum(item => item.findings.Count(finding => finding.severity == "error"));
            report.warnings = report.cases.Sum(item => item.findings.Count(finding => finding.severity == "warning"))
                + report.compositions.Sum(item => item.findings.Count);
            report.regionGeometryPassed = report.errors == 0 && report.casesChecked == report.expectedCases;
            report.passed = report.regionGeometryPassed && report.warnings == 0;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            running = false;
            var summary = $"[UILayout] {(report.passed ? "PASS" : "REVIEW REQUIRED")}: {report.casesChecked}/{report.expectedCases} cases, "
                + $"{report.errors} errors, {report.warnings} content risks. Shared region geometry passed={report.regionGeometryPassed}. "
                + "Report: " + Path.GetFullPath(ReportPath);
            if (report.errors > 0) Debug.LogError(summary);
            else if (report.warnings > 0) Debug.LogWarning(summary);
            else Debug.Log(summary);
        }
    }

    [MenuItem("PocketStriker/Validation/Full Screen Backdrops")]
    public static void ValidateBackdrops()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before checking backdrops.");
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        try
        {
            var loader = typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader");
            var paths = (IDictionary<string, string>)loader.GetField("Paths", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (var path in paths.Values.Distinct())
            {
                var source = Resources.Load<GameObject>(path);
                var layer = source.GetComponent<UILayer>();
                if (layer.FullScreenBackdrops.Count == 0) continue;
                report.registeredLayers++;
                foreach (var device in new[] {
                    new Device("Phone 540x960", 540, 960),
                    new Device("Notched phone 390x844", 390, 844, 34, 47),
                    new Device("Screenshot phone 1206x2622", 1206, 2622, 102, 186),
                    new Device("iPad 768x1024", 768, 1024)
                })
                {
                    var result = new CaseResult { layer = source.name, prefab = path, device = device.name,
                        parenting = "safe-area child (backdrop regression)", state = "authored", screenPixels = device.size,
                        safeAreaPixels = device.safe };
                    CheckCase(source, device, result, false);
                    report.cases.Add(result);
                }
            }
            report.errors = report.failures.Count + report.cases.Sum(item => item.findings.Count(finding => finding.severity == "error"));
            report.warnings = report.cases.Sum(item => item.findings.Count(finding => finding.severity == "warning"));
            report.casesChecked = report.expectedCases = report.cases.Count;
            report.passed = report.registeredLayers > 0 && report.errors == 0 && report.warnings == 0;
            Directory.CreateDirectory("Logs/UILayout");
            File.WriteAllText("Logs/UILayout/backdrops-report.json", JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException($"Backdrop validation failed: {report.errors} errors, {report.warnings} warnings.");
            Debug.Log($"[Backdrops] PASS: {report.registeredLayers} layers, {report.casesChecked} device cases, {report.cases.Sum(item => item.fullScreenBackdropsChecked)} full-canvas rectangles.");
        }
        finally { PosCal.Canvas = oldCanvas; PosCal.SafeAreaRect = oldSafe; }
    }

    static void CheckSafeAreaInitialization(Report report, bool canvasAsFallback)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var previousCanvas = PosCal.Canvas;
        var previousSafeArea = PosCal.SafeAreaRect;
        var result = new InitializationResult {
            fallback = canvasAsFallback ? "legacy root canvas" : "missing safe area",
            screenPixels = new Vector2(Screen.width, Screen.height), safeAreaPixels = Screen.safeArea
        };
        report.safeAreaInitializations.Add(result);
        try
        {
            var canvasObject = new GameObject("Safe Area Initialization Validation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            var root = canvasObject.GetComponent<RectTransform>();
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            var reference = new Vector2(1080, 1920);
            scaler.referenceResolution = reference;
            var anchorMin = root.anchorMin;
            var anchorMax = root.anchorMax;
            PosCal.Canvas = canvas;
            PosCal.SafeAreaRect = canvasAsFallback ? root : null;
            PosCal.TestIni();
            var firstSafeArea = PosCal.SafeAreaRect;
            result.referenceResolutionAfterFirst = scaler.referenceResolution;
            result.separateSafeAreaChild = firstSafeArea != null && firstSafeArea != root && firstSafeArea.parent == root;
            PosCal.TestIni();
            result.referenceResolutionAfterRepeat = scaler.referenceResolution;
            result.sameSafeAreaAfterRepeat = firstSafeArea != null && PosCal.SafeAreaRect == firstSafeArea && root.childCount == 1;
            result.oneLayoutComponent = canvasObject.GetComponents<PortraitSafeAreaLayout>().Length == 1;
            result.rootAnchorsUnchanged = root.anchorMin == anchorMin && root.anchorMax == anchorMax;
            result.referenceResolutionStable = (result.referenceResolutionAfterFirst - result.referenceResolutionAfterRepeat).sqrMagnitude < 0.01f;
            var safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            var expected = Screen.width > 0 && Screen.height > 0 ? new Vector2(
                reference.x * Screen.width / safe.width, reference.y * Screen.height / safe.height) : reference;
            result.referenceResolutionMatchesSafeArea = (scaler.referenceResolution - expected).sqrMagnitude < 0.01f;
            result.passed = result.separateSafeAreaChild && result.sameSafeAreaAfterRepeat && result.oneLayoutComponent
                && result.rootAnchorsUnchanged && result.referenceResolutionStable && result.referenceResolutionMatchesSafeArea;
            if (!result.passed) report.failures.Add("Safe-area initialization failed for " + result.fallback + "; see safeAreaInitializations details.");
        }
        catch (Exception exception) { report.failures.Add("Safe-area initialization (" + result.fallback + "): " + exception); }
        finally
        {
            PosCal.Canvas = previousCanvas;
            PosCal.SafeAreaRect = previousSafeArea;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void CheckCase(GameObject source, Device device, CaseResult result, bool fullScreen)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var canvasObject = new GameObject("UI Layout Validation Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            // World-space prevents the open Game view's real resolution from replacing simulated dimensions.
            canvas.renderMode = RenderMode.WorldSpace;
            var effectiveReference = new Vector2(1080 * device.size.x / device.safe.width, 1920 * device.size.y / device.safe.height);
            var scale = Mathf.Min(device.size.x / effectiveReference.x, device.size.y / effectiveReference.y);
            var canvasRect = (RectTransform)canvasObject.transform;
            result.canvasSize = device.size / scale;
            canvasRect.sizeDelta = result.canvasSize;
            var safe = NewRect("Safe Area", canvasRect);
            safe.anchorMin = new Vector2(device.safe.xMin / device.size.x, device.safe.yMin / device.size.y);
            safe.anchorMax = new Vector2(device.safe.xMax / device.size.x, device.safe.yMax / device.size.y);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.Canvas = canvas;
            PosCal.SafeAreaRect = safe;

            var copies = new Dictionary<Transform, Transform>();
            var root = CopyHierarchy(source.transform, fullScreen ? canvasRect : safe, copies) as RectTransform;
            if (root == null) throw new InvalidOperationException("Registered UI root is not a RectTransform.");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.localPosition = Vector3.zero;
            root.localScale = Vector3.one;
            foreach (var pair in copies) CopyLayoutAndGraphics(pair.Key, pair.Value);
            var sourceLayer = source.GetComponent<UILayer>();
            if (sourceLayer == null) throw new InvalidOperationException("Registered prefab has no UILayer.");
            if (sourceLayer is SettingLayer && result.state != "authored")
            {
                // Geometry-only stand-ins let the runtime centering helper measure
                // the actual button/input bounds without copying their listeners.
                foreach (var selectable in source.GetComponentsInChildren<Selectable>(true))
                {
                    var copy = copies[selectable.transform].gameObject.AddComponent<Selectable>();
                    copy.transition = Selectable.Transition.None;
                    copy.enabled = selectable.enabled;
                }
                ApplySettingState(sourceLayer, copies, result.state);
            }
            if (sourceLayer is SkillEditLayer skillEdit && result.state == "combo-explanation")
                ApplyComboExplanationState(skillEdit, copies);
            var layer = root.gameObject.AddComponent<UILayer>();
            var sourceFields = new SerializedObject(sourceLayer);
            var targetFields = new SerializedObject(layer);
            foreach (var field in new[] { "top", "middle", "bottom" })
            {
                var sourceArea = sourceFields.FindProperty(field)?.objectReferenceValue as Transform;
                targetFields.FindProperty(field).objectReferenceValue = sourceArea != null && copies.TryGetValue(sourceArea, out var copiedArea)
                    ? copiedArea : null;
            }
            var backdrops = targetFields.FindProperty("fullScreenBackdrops");
            backdrops.arraySize = sourceLayer.FullScreenBackdrops.Count;
            for (int index = 0; index < backdrops.arraySize; index++)
                backdrops.GetArrayElementAtIndex(index).objectReferenceValue = copies[sourceLayer.FullScreenBackdrops[index]];
            targetFields.ApplyModifiedPropertiesWithoutUndo();
            foreach (var helper in source.GetComponentsInChildren<MidAreaSizeHelper>(true))
            {
                var copy = copies[helper.transform].gameObject.AddComponent<MidAreaSizeHelper>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(helper), copy);
                var fields = new SerializedObject(copy);
                var target = new SerializedObject(helper).FindProperty("rectTransform").objectReferenceValue as Transform;
                fields.FindProperty("rectTransform").objectReferenceValue = target != null && copies.TryGetValue(target, out var copiedTarget) ? copiedTarget : copy.transform;
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            result.omittedBehaviours = source.GetComponentsInChildren<MonoBehaviour>(true).Where(component => component != null
                    && !(component is UILayer) && !(component is MidAreaSizeHelper) && !IsCopiedUI(component))
                .Select(component => component.GetType().FullName).Distinct().OrderBy(name => name).ToArray();
            Rebuild(root);
            layer.ResizeAreas();
            Rebuild(root);
            var fullBounds = Bounds(canvasRect, root);
            foreach (var backdrop in layer.FullScreenBackdrops)
            {
                var bounds = Bounds(backdrop, root);
                result.fullScreenBackdropsChecked++;
                if (!Contains(bounds, fullBounds) || !Contains(fullBounds, bounds))
                    Add(result, "error", "backdrop-not-fullscreen", PathOf(backdrop, root), "overlay", bounds, fullBounds,
                        "Backdrop must cover the entire canvas including the unsafe edges.");
            }
            if (sourceLayer is SettingLayer && result.state != "authored")
            {
                PlaceSettingSelectionFrame(sourceLayer, copies, result.state);
                var panel = (RectTransform)SettingCopy(sourceFields, copies, SettingPanelPrefix(result.state) + "Panel");
                SettingLayer.CenterVisibleContent(panel, layer.MiddleArea);
                var centeredPosition = panel.anchoredPosition;
                SettingLayer.CenterVisibleContent(panel, layer.MiddleArea);
                if ((centeredPosition - panel.anchoredPosition).sqrMagnitude > Tolerance * Tolerance)
                    Add(result, "error", "settings-centering-not-stable", PathOf(panel, root), "middle", default, default,
                        "Centering the active settings panel twice changed its position.");
            }
            var areaNames = new[] { "top", "middle", "bottom" };
            var areas = areaNames.Select(name => new SerializedObject(layer).FindProperty(name).objectReferenceValue as RectTransform).ToArray();
            result.safeBounds = Bounds(safe, root);
            if (fullScreen && (sourceLayer is UnitInstructionLayer || sourceLayer is ProgressLayer))
            {
                var background = sourceFields.FindProperty(sourceLayer is UnitInstructionLayer ? "bgImage" : "bigCurtain").objectReferenceValue as Graphic;
                var backgroundRect = background != null ? copies[background.transform] as RectTransform : null;
                if (backgroundRect == null || !Contains(Bounds(backgroundRect, root), Bounds(canvasRect, root)))
                    Add(result, "error", "loading-background-not-fullscreen", source.name, "", default,
                        Bounds(canvasRect, root), "Loading background must cover the entire canvas, including device safe-area insets.");
            }
            var firstBounds = areas.Select(area => area != null ? Bounds(area, root) : default).ToArray();
            layer.ResizeAreas();
            Rebuild(root);
            result.repeatResizeStable = areas.Select((area, index) => area == null || Approximately(firstBounds[index], Bounds(area, root))).All(stable => stable);
            if (!result.repeatResizeStable) Add(result, "error", "non-idempotent-resize", source.name, "", default, result.safeBounds,
                "A second ResizeAreas call changed the geometry.");
            result.regionCoverage = areas.All(area => area != null) ? "three-region" : areas.Any(area => area != null) ? "partial-regions" : "overlay-without-regions";
            for (var index = 0; index < areas.Length; index++)
            {
                var area = areas[index];
                if (area == null) continue;
                var bounds = Bounds(area, root);
                result.regions.Add(new RegionResult { name = areaNames[index], path = PathOf(area, root), bounds = bounds, active = area.gameObject.activeInHierarchy });
                if (!Contains(result.safeBounds, bounds)) Add(result, "error", "region-outside-safe-area", PathOf(area, root), areaNames[index], bounds,
                    result.safeBounds, "Region extends beyond the safe UI root.");
                for (var other = 0; other < index; other++)
                    if (areas[other] != null && PositiveIntersection(bounds, Bounds(areas[other], root)))
                        Add(result, "error", "region-overlap", PathOf(area, root), areaNames[index], bounds, Bounds(areas[other], root),
                            areaNames[index] + " overlaps " + areaNames[other] + ".");
            }
            CheckVisibleContent(source, root, copies, areas, areaNames, result);
            if (sourceLayer is SettingLayer && result.state != "authored")
            {
                var panel = SettingCopy(sourceFields, copies, SettingPanelPrefix(result.state) + "Panel");
                var prefix = PathOf(panel, root) + "/";
                var content = result.content.Where(item => item.path.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                if (content.Length == 0)
                    Add(result, "error", "settings-content-not-covered", prefix, "middle", default, default,
                        "The selected settings panel has no measured visible content.");
                else
                {
                    var min = content.Min(item => item.bounds.yMin);
                    var max = content.Max(item => item.bounds.yMax);
                    result.settingsContentCenterOffset = (min + max) * 0.5f - Bounds(layer.MiddleArea, root).center.y;
                    result.settingsContentCentered = Mathf.Abs(result.settingsContentCenterOffset) <= Tolerance;
                    if (!result.settingsContentCentered)
                        Add(result, "error", "settings-content-not-centered", prefix, "middle", default, Bounds(layer.MiddleArea, root),
                            "Visible settings content center differs from the window center by " + result.settingsContentCenterOffset + " units.");
                }
            }
            if (sourceLayer is SkillEditLayer comboLayer && result.state == "combo-explanation")
                CheckComboExplanationState(comboLayer, root, copies, result);
            if (sourceLayer is UnitInstructionLayer)
            {
                // Use the screenshot's localized tip so the preview also exposes missing glyphs.
                var title = (Text)sourceFields.FindProperty("gameTipTitle").objectReferenceValue;
                var tip = (Text)sourceFields.FindProperty("gameTip").objectReferenceValue;
                const string titleText = "战争模式相机操作";
                const string tipText = "・战争模式下，单指划动屏幕中央可旋转相机";
                copies[title.transform].GetComponent<Text>().text = titleText;
                copies[tip.transform].GetComponent<Text>().text = tipText;
                foreach (var label in new[] { title, tip })
                foreach (var character in (titleText + tipText).Distinct())
                    if (label.font == null || !label.font.HasCharacter(character))
                        Add(result, "error", "loading-font-missing-glyph", label.name, "", default, default,
                            "Loading font is missing U+" + ((int)character).ToString("X4"));
                Rebuild(root);
            }
            if (sourceLayer is ProgressLayer)
            {
                var label = (Text)sourceFields.FindProperty("info").objectReferenceValue;
                const string progressText = "生成中";
                copies[label.transform].GetComponent<Text>().text = progressText;
                foreach (var character in progressText)
                    if (label.font == null || !label.font.HasCharacter(character))
                        Add(result, "error", "loading-font-missing-glyph", label.name, "", default, default,
                            "Progress font is missing U+" + ((int)character).ToString("X4"));
                Rebuild(root);
            }
            if (device.name == "Phone 540x960" || sourceLayer is UnitInstructionLayer || sourceLayer is ProgressLayer)
                RenderPreview(canvas, scene, result);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static string[] StatesForLayer(string layer) => layer == "SettingLayer" ? SettingStates
        : layer == "SkillEditLayer" ? SkillEditStates : AuthoredState;

    static void ApplyComboExplanationState(SkillEditLayer source, IDictionary<Transform, Transform> copies)
    {
        var fields = new SerializedObject(source);
        var hide = fields.FindProperty("duringComboHide");
        for (var index = 0; index < hide.arraySize; index++)
        {
            var target = hide.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
            if (target != null) copies[target].gameObject.SetActive(false);
        }
        SettingCopy(fields, copies, "stoneBoxRect").gameObject.SetActive(false);
        SettingCopy(fields, copies, "mask").gameObject.SetActive(false);
        var nineFields = new SerializedObject(source.nineSlot);
        // Stable state after ShowCombo(true), for a player who has completed the tutorial.
        // Mirror IntroAboutCombo(true) and the final action visibility without running skills/effects.
        SettingCopy(nineFields, copies, "validationWarnSide").gameObject.SetActive(true);
        foreach (var field in new[] { "randomBtn", "removeAllBtn", "ConfirmSkillChangeButton", "ResetButton" })
            SettingCopy(nineFields, copies, field).gameObject.SetActive(false);
        foreach (var field in new[] { "comboShowBtn", "dreamComboShowBtn", "comboCloseBtn" })
            SettingCopy(nineFields, copies, field).gameObject.SetActive(true);
    }

    static void CheckComboExplanationState(SkillEditLayer source, RectTransform root, IDictionary<Transform, Transform> copies, CaseResult result)
    {
        var fields = new SerializedObject(source.nineSlot);
        foreach (var field in new[] { "validationWarnSide", "comboCloseBtn" })
        {
            var target = SettingCopy(fields, copies, field);
            var path = PathOf(target, root);
            if (!result.content.Any(item => item.path == path))
                Add(result, "error", "combo-state-not-covered", path, "middle", default, default,
                    "The explicit combo-explanation state must check its explanation and close control.");
        }
        var text = SettingCopy(fields, copies, "validationWarnSide").GetComponent<Text>();
        var rows = CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv"));
        var languages = new[] { "English", "Japanese", "Chinese" };
        foreach (var key in new[] { "IntroOfCombo", "IntroOfDreamCombo" })
        {
            var row = rows.FirstOrDefault(item => item[0] == key);
            if (row == null) throw new InvalidOperationException("Missing combo explanation translation: " + key);
            for (var language = 0; language < languages.Length; language++)
            {
                text.text = row[language + 1];
                result.comboTranslationsChecked++;
                if (text.preferredHeight > text.rectTransform.rect.height + Tolerance)
                    Add(result, "warning", "combo-explanation-text-overflow", PathOf(text.transform, root), "middle",
                        Bounds(text.rectTransform, root), Bounds(text.rectTransform, root),
                        key + " / " + languages[language] + " needs " + text.preferredHeight + " units of height; available " + text.rectTransform.rect.height + ".");
            }
        }
        // Capture the real Japanese dream-combo copy; other translations were measured above.
        text.text = rows.First(item => item[0] == "IntroOfDreamCombo")[2];
    }

    static string SettingPanelPrefix(string state) => state.StartsWith("account-", StringComparison.Ordinal) ? "account"
        : state.StartsWith("device-", StringComparison.Ordinal) ? "device" : state == "nickname" ? "nickName" : state;

    static Transform SettingCopy(SerializedObject fields, IDictionary<Transform, Transform> copies, string field)
    {
        var value = fields.FindProperty(field)?.objectReferenceValue;
        var source = value is Component component ? component.transform : (value as GameObject)?.transform;
        if (source == null || !copies.TryGetValue(source, out var copy))
            throw new InvalidOperationException("UI state fixture is missing serialized field: " + field);
        return copy;
    }

    static void ApplySettingState(UILayer source, IDictionary<Transform, Transform> copies, string state)
    {
        var fields = new SerializedObject(source);
        var selectedPanel = SettingPanelPrefix(state) + "Panel";
        // Mirror CloseAllPanels + the chosen button, never invoke Initialise or listeners.
        foreach (var panel in new[] { "volumePanel", "accountPanel", "devicePanel", "supportPanel", "languagePanel", "nickNamePanel" })
            SettingCopy(fields, copies, panel).gameObject.SetActive(panel == selectedPanel);
        if (state.StartsWith("account-", StringComparison.Ordinal))
        {
            // AccountPhase_EmailToBeSet / AccountPhase_EmailSet, without loading account data.
            var emailSet = state == "account-email-set";
            SettingCopy(fields, copies, "emailSettingT").gameObject.SetActive(!emailSet);
            SettingCopy(fields, copies, "emailT").gameObject.SetActive(emailSet);
            SettingCopy(fields, copies, "CurrentEmail").gameObject.SetActive(emailSet);
            SettingCopy(fields, copies, "EmailInput").gameObject.SetActive(!emailSet);
            SettingCopy(fields, copies, "EmailConfirmBtn").gameObject.SetActive(!emailSet);
            SettingCopy(fields, copies, "SendPwResetBtn").gameObject.SetActive(emailSet);
        }
        if (state.StartsWith("device-", StringComparison.Ordinal))
        {
            // RefreshLinkDeviceBtn's mutually exclusive linked/unlinked actions.
            var linked = state == "device-linked";
            SettingCopy(fields, copies, "linkDeviceBtn").gameObject.SetActive(!linked);
            SettingCopy(fields, copies, "unLinkDeviceBtn").gameObject.SetActive(linked);
        }
    }

    static void PlaceSettingSelectionFrame(UILayer source, IDictionary<Transform, Transform> copies, string state)
    {
        var fields = new SerializedObject(source);
        var frame = SettingCopy(fields, copies, "selectedFrame");
        frame.position = SettingCopy(fields, copies, SettingPanelPrefix(state) + "Btn").position;
        frame.gameObject.SetActive(true);
    }

    static void CheckCompositions(Report report)
    {
        // Main-menu pages retain LowerMainBar and a history ReturnLayer. Modal dialogs are intentionally
        // excluded: obscuring the underlying page is their purpose. These are authored-state risk checks.
        var pages = new[] {
            new[] { "Front", "FrontLayer", "UpperInfoBar", "LowerMainBar" },
            new[] { "Arcade", "ArcadeTop", "LowerMainBar", "ReturnLayer" },
            new[] { "Shop", "ShopTopLayer", "UpperInfoBar", "LowerMainBar", "ReturnLayer" },
            new[] { "Gotcha", "GotchaLayer", "UpperInfoBar", "LowerMainBar", "ReturnLayer" },
            new[] { "Units", "UnitOptionLayer", "UnitsLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "TeamEdit", "TeamEditLayer", "UnitsLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "TeamSingleSelect", "TeamSingleSelectLayer", "UnitsLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "SelfFight", "SelfFightLayer", "UnitsLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "Settings", "SettingLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "Mail", "MailBox", "LowerMainBar", "ReturnLayer" },
            new[] { "Ranking", "RankingLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "ArenaAwards", "ArenaAwardLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "Stones", "StoneListLayer", "LowerMainBar", "ReturnLayer" },
            new[] { "DropTable", "DropTableInfoLayer", "LowerMainBar", "ReturnLayer" }
        };
        foreach (var device in report.cases.Select(item => item.device).Distinct())
        foreach (var page in pages)
        foreach (var state in page[0] == "Settings" ? SettingStates : AuthoredState)
        {
            var composition = new CompositionResult {
                page = page[0], state = state, device = device, layers = page.Skip(1).ToArray(),
                visibilityRule = page[0] == "Shop" ? "UpperInfoBar toolbar hidden; currency/VIP retained."
                    : page[0] == "Gotcha" ? "UpperInfoBar settings/mail hidden; shop/currency/VIP retained."
                    : "Authored active objects; zero-alpha objects included at animation rest pose."
            };
            report.compositions.Add(composition);
            var layers = composition.layers.Select(name => report.cases.FirstOrDefault(item => item.layer == name && item.device == device
                    && item.parenting == "safe-area child" && item.state == (name == "SettingLayer" ? state : "authored")))
                .Where(item => item != null).ToArray();
            for (var index = 0; index < layers.Length; index++)
            for (var other = 0; other < index; other++)
            {
                composition.layerPairsChecked++;
                foreach (var first in layers[index].content.Where(item => VisibleInComposition(page[0], item.path)))
                foreach (var second in layers[other].content.Where(item => VisibleInComposition(page[0], item.path)))
                {
                    if (!PositiveIntersection(first.bounds, second.bounds)) continue;
                    composition.findings.Add(new Finding {
                        severity = "warning", kind = "cross-layer-content-overlap", path = first.path, region = second.path,
                        bounds = first.bounds, allowedBounds = second.bounds,
                        message = "Authored content in concurrently loaded layers intersects. Verify runtime visibility and intended overlay behavior."
                            + (first.animationRestPose || second.animationRestPose ? " Includes an authored zero-alpha animation rest pose." : "")
                    });
                }
            }
        }
    }

    static bool VisibleInComposition(string page, string path)
    {
        if (!path.StartsWith("UpperInfoBar/top/v/", StringComparison.Ordinal)) return true;
        if (page == "Shop") return false;
        if (page == "Gotcha") return path.StartsWith("UpperInfoBar/top/v/shop", StringComparison.Ordinal);
        return true;
    }

    static Transform CopyHierarchy(Transform source, Transform parent, IDictionary<Transform, Transform> copies)
    {
        var copy = source is RectTransform ? new GameObject(source.name, typeof(RectTransform)).transform : new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        if (source is RectTransform rect && copy is RectTransform target)
        {
            target.anchorMin = rect.anchorMin;
            target.anchorMax = rect.anchorMax;
            target.pivot = rect.pivot;
            target.sizeDelta = rect.sizeDelta;
            target.anchoredPosition3D = rect.anchoredPosition3D;
        }
        copy.gameObject.SetActive(source.gameObject.activeSelf);
        copies.Add(source, copy);
        foreach (Transform child in source) CopyHierarchy(child, copy, copies);
        return copy;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static bool IsCopiedUI(Component component)
    {
        var type = component.GetType();
        return type == typeof(Image) || type == typeof(RawImage) || type == typeof(Text) || type == typeof(TextMeshProUGUI)
            || type == typeof(HorizontalLayoutGroup) || type == typeof(VerticalLayoutGroup) || type == typeof(GridLayoutGroup)
            || type == typeof(ContentSizeFitter) || type == typeof(AspectRatioFitter) || type == typeof(LayoutElement)
            || type == typeof(Mask) || type == typeof(RectMask2D) || type == typeof(CanvasGroup);
    }

    static void CopyLayoutAndGraphics(Transform source, Transform target)
    {
        // Only known local layout/graphic types: no Animator, game scripts, UI events or network dependencies.
        foreach (var component in source.GetComponents<Component>().Where(component => component != null && IsCopiedUI(component)))
        {
            var copy = target.gameObject.AddComponent(component.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(component), copy);
            if (copy is Behaviour behaviour && component is Behaviour original) behaviour.enabled = original.enabled;
            if (copy is CanvasGroup group) group.alpha = 1;
        }
    }

    static void Rebuild(RectTransform root)
    {
        // Rebuilding only the root skips layout groups beneath intermediate transforms which have
        // no ILayoutController. Solve nested groups from the leaves, then repeat after parents settle.
        var rects = root.GetComponentsInChildren<RectTransform>(true);
        for (var pass = 0; pass < 2; pass++)
        {
            root.ForceUpdateRectTransforms();
            for (var index = rects.Length - 1; index >= 0; index--)
                if (rects[index].GetComponents<Component>().Any(component => component is ILayoutController))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rects[index]);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }
        root.ForceUpdateRectTransforms();
    }

    static void CheckVisibleContent(GameObject source, RectTransform root, IDictionary<Transform, Transform> copies,
        RectTransform[] areas, string[] names, CaseResult result)
    {
        var candidates = source.GetComponentsInChildren<Component>(true).Where(component => component is Selectable
            || component is Text text && !string.IsNullOrWhiteSpace(text.text)
            || component is TMP_Text tmp && !string.IsNullOrWhiteSpace(tmp.text));
        var checkedRects = new HashSet<Transform>();
        foreach (var candidate in candidates)
        {
            if (!(copies[candidate.transform] is RectTransform rect) || !checkedRects.Add(candidate.transform)) continue;
            if (!IsVisible(candidate, source.transform, copies, out var alphaRestPose))
            {
                result.inactiveOrDisabledContent++;
                continue;
            }
            var bounds = Bounds(rect, root);
            var clipped = false;
            for (var ancestor = candidate.transform.parent; ancestor != null && ancestor != source.transform.parent; ancestor = ancestor.parent)
            {
                var rectMask = ancestor.GetComponent<RectMask2D>();
                var mask = ancestor.GetComponent<Mask>();
                if ((rectMask == null || !rectMask.enabled) && (mask == null || !mask.enabled)) continue;
                if (!(copies[ancestor] is RectTransform clipRect)) continue;
                var clipBounds = Bounds(clipRect, root);
                if (rectMask != null && rectMask.enabled)
                {
                    // RectMask2D padding is expressed in local units. UI parents in this project are axis-aligned.
                    var padding = rectMask.padding;
                    clipBounds = Rect.MinMaxRect(clipBounds.xMin + padding.x, clipBounds.yMin + padding.y,
                        clipBounds.xMax - padding.z, clipBounds.yMax - padding.w);
                }
                bounds = Intersection(bounds, clipBounds);
                clipped = true;
            }
            if (bounds.width <= Tolerance || bounds.height <= Tolerance) continue;
            result.textAndControlsChecked++;
            if (alphaRestPose) result.alphaRestPoseContentChecks++;
            if (clipped) result.clippedContentChecks++;
            var regionIndex = Array.FindIndex(areas, area => area != null && (rect == area || rect.IsChildOf(area)));
            if (regionIndex < 0 && candidate is Selectable && Covers(bounds, result.safeBounds))
            {
                result.intentionalFullscreenControls++;
                continue;
            }
            result.content.Add(new ContentResult { path = PathOf(rect, root), component = candidate.GetType().Name,
                bounds = bounds, animationRestPose = alphaRestPose,
                hasAuthoredGraphic = candidate.GetComponentsInChildren<Graphic>(true).Any(graphic =>
                    IsVisible(graphic, source.transform, copies, out _) && (!(graphic is Text legacyText) || !string.IsNullOrWhiteSpace(legacyText.text))
                    && (!(graphic is TMP_Text tmpText) || !string.IsNullOrWhiteSpace(tmpText.text))) });
            var allowed = regionIndex >= 0 ? Bounds(areas[regionIndex], root) : result.safeBounds;
            if (!Contains(allowed, bounds)) Add(result, "warning", regionIndex >= 0 ? "visible-content-crosses-region" : "visible-content-outside-safe-area",
                PathOf(rect, root), regionIndex >= 0 ? names[regionIndex] : "overlay", bounds, allowed,
                candidate.GetType().Name + (clipped ? " remains outside its region after ancestor mask clipping." : " extends beyond its assigned region.")
                + (alphaRestPose ? " Checked at the authored zero-alpha animation rest pose; verify the visible animation state." : ""));
        }
    }

    static bool IsVisible(Component component, Transform root, IDictionary<Transform, Transform> copies, out bool alphaRestPose)
    {
        alphaRestPose = false;
        // Use the isolated copy's chosen visibility state, while source components supply type,
        // enabled/color values and original CanvasGroup alpha for animation-rest-pose reporting.
        for (var node = component.transform; node != null; node = node.parent)
        {
            if (!copies.TryGetValue(node, out var copy) || !copy.gameObject.activeSelf) return false;
            if (node == root) break;
        }
        if (component is Behaviour behaviour && !behaviour.enabled) return false;
        if (component is Graphic graphic && graphic.color.a <= 0.001f) return false;
        var alpha = 1f;
        for (var node = component.transform; node != null; node = node.parent)
        {
            var stopGroups = false;
            foreach (var group in node.GetComponents<CanvasGroup>())
            {
                if (!group.enabled) continue;
                alpha *= group.alpha;
                stopGroups |= group.ignoreParentGroups;
            }
            if (stopGroups || node == root) break;
        }
        alphaRestPose = alpha <= 0.001f;
        return true;
    }

    static void RenderPreview(Canvas canvas, Scene scene, CaseResult result)
    {
        var cameraObject = new GameObject("UI Layout Preview Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = result.canvasSize.y * 0.5f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.09f, 1);
        canvas.worldCamera = camera;
        int width = (int)result.screenPixels.x, height = (int)result.screenPixels.y;
        var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            texture.Create();
            camera.targetTexture = texture;
            // Geometry has already been measured in a fixed-size world-space canvas. Switch only
            // for capture: the project's URP renderer draws these UI materials in its camera UI pass.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 1;
            canvas.scaleFactor = width / result.canvasSize.x;
            canvas.pixelPerfect = true;
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            Canvas.ForceUpdateCanvases();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
            RenderTexture.active = texture;
            image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            var pixels = image.GetPixels32();
            var background = pixels[0];
            result.previewContainsUI = pixels.Any(pixel => pixel.r != background.r || pixel.g != background.g || pixel.b != background.b);
            var expectsVisibleContent = result.content.Any(item => item.hasAuthoredGraphic && PositiveIntersection(item.bounds, result.safeBounds));
            result.previewScope = result.previewContainsUI ? "Authored UI graphics, with zero-alpha CanvasGroups lifted to show animation rest poses."
                : expectsVisibleContent ? "Capture did not display the expected authored graphics; visual inspection incomplete."
                : "No visible authored graphics in the current state. Transparent interaction rectangles were checked; runtime-generated visuals remain outside this preview.";
            if (!result.previewContainsUI && expectsVisibleContent)
                Add(result, "warning", "empty-preview", result.layer, "", default, default,
                    "The captured preview is uniform despite in-bounds authored controls/text; do not treat this image as a completed visual check.");
            var directory = Path.Combine(Path.GetDirectoryName(ReportPath), "previews");
            Directory.CreateDirectory(directory);
            result.previewPng = Path.Combine(directory, result.layer + (result.parenting.StartsWith("full-screen", StringComparison.Ordinal) ? "-fullscreen" : "")
                + (result.state == "authored" ? "" : "-" + result.state)
                + (width == 540 && height == 960 ? "" : $"-{width}x{height}") + ".png");
            File.WriteAllBytes(result.previewPng, image.EncodeToPNG());
        }
        catch (Exception exception)
        {
            Add(result, "warning", "preview-unavailable", result.layer, "", default, default, exception.Message);
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static Rect Bounds(RectTransform rect, RectTransform root)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            var point = (Vector2)root.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - Tolerance && inner.xMax <= outer.xMax + Tolerance
        && inner.yMin >= outer.yMin - Tolerance && inner.yMax <= outer.yMax + Tolerance;
    static bool Covers(Rect inner, Rect outer) => inner.width >= outer.width * 0.95f && inner.height >= outer.height * 0.95f;
    static bool Approximately(Rect a, Rect b) => (a.position - b.position).sqrMagnitude < 0.01f && (a.size - b.size).sqrMagnitude < 0.01f;
    static bool PositiveIntersection(Rect a, Rect b) { var rect = Intersection(a, b); return rect.width > Tolerance && rect.height > Tolerance; }
    static Rect Intersection(Rect a, Rect b) => new Rect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin),
        Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)), Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin)));
    static string PathOf(Transform child, Transform root)
    {
        var parts = new List<string>();
        for (var node = child; node != null; node = node.parent) { parts.Add(node.name); if (node == root) break; }
        parts.Reverse();
        return string.Join("/", parts);
    }
    static void Add(CaseResult result, string severity, string kind, string path, string region, Rect bounds, Rect allowed, string message)
    {
        result.findings.Add(new Finding { severity = severity, kind = kind, path = path, region = region, bounds = bounds, allowedBounds = allowed, message = message });
    }
}
