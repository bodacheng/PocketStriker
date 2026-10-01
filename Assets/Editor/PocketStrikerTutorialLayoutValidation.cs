using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightScene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class PocketStrikerTutorialValidation
{
    const string LayoutOutput = "Logs/Tutorial/Layout";
    const string SideIconPath = "Assets/Resources/DummyLayerSystem/FightingStepLayer/SideUnitIcon.prefab";
    static readonly string[] LayoutLanguages = { "en", "ja", "zh" };

    [Serializable]
    public sealed class LayoutReport
    {
        public bool passed;
        public string unityVersion;
        public int pagesChecked;
        public int labelsChecked;
        public int pointersChecked;
        public int stableLayoutsChecked;
        public int initialPagesChecked;
        public int hudTargetsChecked;
        public int animationResetsChecked;
        public int panelsClearOfControls;
        public bool sourcePrefabsUnchanged;
        public string scope = "Actual FightingStepLayer, SideUnitIcon and stoneModel UI copies; production OpenTutorial/ForceClickDreamComboBtn callers, BattleHUDPresentation, BattleTutorialLayout and UILayer.ResizeAreas. Four phone/tablet viewports and three languages, six tutorial pages plus dormant Dream overlay. Verifies generated glyph bounds, final wrapped lines, safe area, pointer target identity/endpoints, explanation panels clear of live control geometry, initial page and HUD recovery after native Animator.Rebind. Screenshots use actual portrait/skill sprites and fonts.";
        public string limitation = "UI-only preview fixture: combat models, accounts, network, live animations and battle execution are omitted. A native player icon supplies HP/energy targets before the combat dictionary is populated. Three local authored skill sprites and empty initial Dream gauge represent available control geometry. Canvas scaling reproduces PortraitSafeAreaLayout Expand; device safe insets are explicit fixture inputs. Native animation bindings are rebound, then production tutorial entry restores presentation.";
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<LayoutPage> pages = new List<LayoutPage>();
    }

    [Serializable]
    public sealed class LayoutPage
    {
        public string viewport;
        public string language;
        public string page;
        public Rect safeBounds;
        public List<LayoutLabel> labels = new List<LayoutLabel>();
        public List<LayoutPointer> pointers = new List<LayoutPointer>();
    }

    [Serializable]
    public sealed class LayoutLabel
    {
        public string path;
        public string text;
        public Rect bounds;
        public Rect glyphBounds;
        public float preferredHeight;
        public float availableHeight;
        public int fontSize;
    }

    [Serializable]
    public sealed class LayoutPointer
    {
        public string label;
        public string target;
        public Rect targetBounds;
        public Vector2 tip;
    }

    readonly struct LayoutViewport
    {
        public readonly int Width, Height;
        public readonly Rect Safe;
        public LayoutViewport(int width, int height, Rect safe)
        { Width = width; Height = height; Safe = safe; }
        public string Name => Width + "x" + Height;
    }

    [MenuItem("PocketStriker/Validation/Tutorial Layout")]
    public static void ValidateLayout() => RunLayout(false);

    public static void ValidateLayoutBatch() => RunLayout(true);

    static void RunLayout(bool exitWhenDone)
    {
        var report = ValidateTutorialLayout();
        var summary = $"[TutorialLayout] {(report.passed ? "PASS" : "FAIL")}: {report.pagesChecked} pages, {report.labelsChecked} labels, {report.pointersChecked} pointers, {report.screenshots.Count} renders. {Path.GetFullPath(LayoutOutput)}/report.json";
        if (report.passed) Debug.Log(summary);
        else Debug.LogError(summary + "\n" + string.Join("\n", report.errors));
        if (exitWhenDone) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static LayoutReport ValidateTutorialLayout()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Tutorial layout validation requires a stopped editor.");
        var report = new LayoutReport { unityVersion = Application.unityVersion };
        Directory.CreateDirectory(LayoutOutput);
        var sourceFiles = new[] { PrefabPath, SideIconPath, HUDStonePath }.ToDictionary(path => path, File.ReadAllText);
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var oldLanguage = AppSetting.Value.Language;
        var dragCanvasField = typeof(HeroIcon).GetField("canvas", BindingFlags.Static | BindingFlags.NonPublic);
        var oldDragCanvas = dragCanvasField.GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Tutorial Layout Validation");
        rig.SetActive(false);
        SceneManager.MoveGameObjectToScene(rig, scene);
        RenderTexture texture = null;
        void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message);
        }
        Application.logMessageReceived += CaptureError;
        try
        {
            var cameraObject = new GameObject("UI Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.085f, 0.11f, 0.14f, 1);

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1080, 1920);
            PosCal.Canvas = canvas;
            dragCanvasField.SetValue(null, canvas);
            var safe = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvas.transform, false);
            safe.anchorMin = Vector2.zero;
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.SafeAreaRect = safe;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(source != null, "Missing battle UI prefab.");
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var root = CopyLayoutUI(source, safe, map);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.localPosition = Vector3.zero;
            root.localScale = Vector3.one;
            var sourceLayer = source.GetComponent<FightingStepLayer>();
            var layer = root.gameObject.AddComponent<PocketStrikerTutorialValidationLayer>();
            foreach (var name in new[] { "pauseButton", "inputsManager", "team1UI", "team2UI", "clickNextTutorial", "clickTriggerDreamCombo", "forceClickAutoBtnBlackMask", "top", "middle", "bottom" })
                SetField(layer, name, map[(UnityEngine.Object)FindField(sourceLayer, name).GetValue(sourceLayer)]);
            CopyBackdrops(sourceLayer, layer, map);
            Invoke(layer, "ResetOverlayStates");
            Invoke(layer, "KeepTopButtonsClickable");
            var tutorial = Field<ClickNextTutorial>(layer, "clickNextTutorial");
            var pages = Field<GameObject[]>(tutorial, "TutorialLayers");
            Require(pages.Length == 6, "Expected six tutorial pages.");
            var dream = (RectTransform)root.Find("DreamComboTutorial");
            Require(dream != null, "Missing Dream Combo tutorial overlay.");
            var iconSource = AssetDatabase.LoadAssetAtPath<GameObject>(SideIconPath);
            var iconMap = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var icon = CopyLayoutUI(iconSource, root.Find("middle/TeamICons"), iconMap);
            icon.gameObject.SetActive(true);
            icon.localScale = Vector3.one;
            var sourceSide = iconSource.GetComponent<SideUnitIcon>();
            var health = (RectTransform)iconMap[sourceSide.HealthBarRect];
            var energy = (RectTransform)iconMap[sourceSide.EnergyBarRect];
            ((GameObject)iconMap[sourceSide.DreamComboFlg]).SetActive(false);
            ((Text)iconMap[sourceSide.TeamIndicator]).gameObject.SetActive(false);
            var hp = health.GetComponent<Slider>();
            hp.value = 0.8f;
            foreach (var graphic in energy.GetComponentsInChildren<Graphic>(true)) graphic.gameObject.SetActive(true);
            PopulateLayoutPortrait(iconSource, iconMap);
            icon.GetComponent<SideUnitIcon>().ApplyBattleHUDStyle(false, false);
            HUDPopulateSkills(layer.InputsManager);
            layer.InputsManager.DreamComboGauge.SetPercent(0);
            layer.Team1UI.AutoSwitch.gameObject.SetActive(true);
            layer.Team2UI.AutoSwitch.gameObject.SetActive(false);
            Field<BOButton>(layer, "pauseButton").gameObject.SetActive(false);
            foreach (var team in new[] { layer.Team1UI, layer.Team2UI })
            {
                team.LiveUnitCount.gameObject.SetActive(false);
                Field<Text>(team, "rotationModeHitCombo").gameObject.SetActive(false);
                team.SelectedFrame.gameObject.SetActive(false);
            }
            Invoke(layer, "PrepareTutorialLayout");
            var layout = root.GetComponent<BattleTutorialLayout>();
            var rows = CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv"));
            var viewports = new[]
            {
                new LayoutViewport(540, 960, new Rect(0, 0, 540, 960)),
                new LayoutViewport(375, 667, new Rect(0, 0, 375, 667)),
                new LayoutViewport(390, 844, new Rect(0, 34, 390, 763)),
                new LayoutViewport(768, 1024, new Rect(0, 0, 768, 1024))
            };
            rig.SetActive(true);
            var automatic = false;
            layer.Team1UI.AutoSwitch.Initialize(() => automatic, value => automatic = value);
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var viewport in viewports)
            {
                if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
                texture = new RenderTexture(viewport.Width, viewport.Height, 24, RenderTextureFormat.ARGB32);
                texture.Create();
                camera.targetTexture = texture;
                camera.aspect = (float)viewport.Width / viewport.Height;
                // PortraitSafeAreaLayout raises the reference resolution by the
                // safe-area ratio, then CanvasScaler Expand chooses the smaller scale.
                float scale = Mathf.Min(viewport.Safe.width / 1080, viewport.Safe.height / 1920);
                canvasRect.sizeDelta = new Vector2(viewport.Width / scale, viewport.Height / scale);
                camera.orthographicSize = canvasRect.rect.height / 2;
                safe.anchorMin = new Vector2(viewport.Safe.xMin / viewport.Width, viewport.Safe.yMin / viewport.Height);
                safe.anchorMax = new Vector2(viewport.Safe.xMax / viewport.Width, viewport.Safe.yMax / viewport.Height);
                safe.offsetMin = safe.offsetMax = Vector2.zero;
                layer.ResizeAreas();
                for (int language = 0; language < LayoutLanguages.Length; language++)
                {
                    AppSetting.Value.Language = new[] { SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.ChineseSimplified }[language];
                    LocalizeLayoutUI(source, map, rows, language);
                    layer.RefreshPresentation();
                    foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.Rebind();
                    Invoke(layer, "ResetOverlayStates");
                    layer.OpenTutorial();
                    Require(pages[0].activeSelf && pages.Count(page => page.activeSelf) == 1,
                        "Real OpenTutorial caller does not begin at the first page.");
                    report.initialPagesChecked++;
                    Rebuild(root, camera, texture);
                    CheckTutorialHUDTargets(root, layer, LayoutBounds(root, safe), report);
                    report.animationResetsChecked++;
                    foreach (var page in pages.Concat(new[] { dream.gameObject }))
                    {
                        foreach (var item in pages) item.SetActive(false);
                        bool isDream = page == dream.gameObject;
                        tutorial.gameObject.SetActive(!isDream);
                        dream.gameObject.SetActive(isDream);
                        page.SetActive(true);
                        if (isDream) layer.ForceClickDreamComboBtn();
                        // Match OpenTutorial/ForceClickDreamComboBtn after activation:
                        // nested HUD canvases must never draw over the explanation.
                        typeof(FightingStepLayer).GetMethod("PromoteToOverlayCanvas", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, new object[] { isDream ? dream : tutorial.transform, 1002 });
                        var label = viewport.Name + "/" + LayoutLanguages[language] + "/" + page.name;
                        var pageReport = new LayoutPage
                        {
                            viewport = viewport.Name, language = LayoutLanguages[language], page = page.name,
                            safeBounds = LayoutBounds(root, safe)
                        };
                        report.pages.Add(pageReport);
                        try
                        {
                            Rebuild(root, camera, texture);
                            layout.RefreshLayout();
                            Rebuild(root, camera, texture);
                            // Legacy Text can repack its shared font atlas while
                            // another label renders. Rebuild once with all glyphs present.
                            layout.RefreshLayout();
                            Rebuild(root, camera, texture);
                            var rects = page.GetComponentsInChildren<RectTransform>().ToArray();
                            var before = rects.Select(rect => LayoutBounds(root, rect)).ToArray();
                            for (int repeat = 0; repeat < 3; repeat++)
                            {
                                layer.ResizeAreas();
                                layout.RefreshLayout();
                                Rebuild(root, camera, texture);
                            }
                            for (int index = 0; index < rects.Length; index++)
                            {
                                var after = LayoutBounds(root, rects[index]);
                                Require(Vector2.Distance(before[index].position, after.position) < 0.5f
                                    && Vector2.Distance(before[index].size, after.size) < 0.5f,
                                    "Repeated layout moves " + LayoutPath(root, rects[index]));
                            }
                            report.stableLayoutsChecked++;
                            CheckLayoutPage(root, (RectTransform)page.transform, layout, health, energy, pageReport, report);
                        }
                        catch (Exception exception) { report.errors.Add(label + ": " + exception.GetBaseException().Message); }
                        var path = Path.Combine(LayoutOutput, viewport.Name + "-" + LayoutLanguages[language] + "-" + page.name + ".png");
                        SaveLayoutRender(texture, path);
                        report.screenshots.Add(path);
                        report.pagesChecked++;
                    }
                }
            }
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafe;
            AppSetting.Value.Language = oldLanguage;
            dragCanvasField.SetValue(null, oldDragCanvas);
            foreach (var raycaster in rig.GetComponentsInChildren<GraphicRaycaster>(true))
                typeof(BaseRaycaster).GetMethod("OnDisable", Private).Invoke(raycaster, null);
            UnityEngine.Object.DestroyImmediate(rig);
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSceneManager.ClosePreviewScene(scene);
            Application.logMessageReceived -= CaptureError;
            report.sourcePrefabsUnchanged = sourceFiles.All(pair => File.ReadAllText(pair.Key) == pair.Value);
            report.passed = report.errors.Count == 0 && report.sourcePrefabsUnchanged
                && report.pagesChecked == 84 && report.screenshots.Count == 84 && report.stableLayoutsChecked == 84
                && report.labelsChecked >= 108 && report.pointersChecked == 84 && report.initialPagesChecked == 12
                && report.animationResetsChecked == 12 && report.hudTargetsChecked == 84 && report.panelsClearOfControls == 84;
            File.WriteAllText(Path.Combine(LayoutOutput, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static RectTransform CopyLayoutUI(GameObject source, Transform parent, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        Require(source != null, "Missing layout fixture prefab.");
        // Include the actual input manager, joysticks, gauge and portrait controller,
        // so tutorial targets follow the same production HUD geometry as live combat.
        return HUDCopyUI(source, parent, map);
    }

    static void PopulateLayoutPortrait(GameObject source, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        var portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/OrganizedResources/InUse/ExternalAssets/Unit_Icon/1.png");
        var hero = source.GetComponentInChildren<HeroIcon>(true);
        Require(hero != null && portrait != null, "Missing actual player portrait fixture.");
        var sourceImage = Field<Image>(hero, "icon");
        var image = (Image)map[sourceImage];
        image.sprite = portrait;
        image.gameObject.SetActive(true);
        image.color = Color.white;
        var curtain = (Image)map[Field<Image>(hero, "cooldownCurtain")];
        curtain.fillAmount = 0;
    }

    static void LocalizeLayoutUI(GameObject source, Dictionary<UnityEngine.Object, UnityEngine.Object> map, string[][] rows, int language)
    {
        foreach (var converter in source.GetComponentsInChildren<LanguageConverter>(true))
        {
            var sourceText = converter.target != null ? converter.target : converter.GetComponent<Text>();
            if (sourceText == null || string.IsNullOrEmpty(converter.languageCode)) continue;
            var row = rows.FirstOrDefault(item => item.Length >= 4 && item[0] == converter.languageCode);
            Require(row != null, "Missing localization: " + converter.languageCode);
            ((Text)map[sourceText]).text = row[language + 1];
        }
    }

    static void CheckLayoutPage(RectTransform root, RectTransform page, BattleTutorialLayout layout, RectTransform health,
        RectTransform energy, LayoutPage pageReport, LayoutReport report)
    {
        var visible = page.GetComponentsInChildren<Text>().Where(text => text.enabled && !string.IsNullOrWhiteSpace(text.text)).ToArray();
        Require(visible.Length > 0, "Tutorial page contains no visible text.");
        foreach (var text in visible)
        {
            var bounds = LayoutBounds(root, text.rectTransform);
            var glyphs = LayoutGlyphBounds(root, text);
            var data = new LayoutLabel
            {
                path = LayoutPath(root, text.transform), text = text.text, bounds = bounds, glyphBounds = glyphs,
                preferredHeight = text.preferredHeight, availableHeight = text.rectTransform.rect.height, fontSize = text.fontSize
            };
            pageReport.labels.Add(data);
            Require(LayoutContains(pageReport.safeBounds, bounds), data.path + " text rect is outside the safe area: " + bounds);
            Require(LayoutContains(pageReport.safeBounds, glyphs), data.path + " glyphs are outside the safe area: " + glyphs);
            Require(text.preferredHeight <= text.rectTransform.rect.height + 1,
                data.path + " clips wrapped lines: preferred=" + text.preferredHeight + ", available=" + text.rectTransform.rect.height);
            Require(LayoutContains(bounds, glyphs, 2), data.path + " glyphs escape the label rect.");
            Require(text.font != null, data.path + " has no font.");
            foreach (char character in text.text.Where(character => !char.IsWhiteSpace(character)).Distinct())
                Require(text.font.HasCharacter(character), data.path + " font misses U+" + ((int)character).ToString("X4"));
            report.labelsChecked++;
        }
        var panels = new List<Rect>();
        foreach (var callout in layout.Callouts.Where(item => item.Label != null && item.Label.gameObject.activeInHierarchy))
        {
            var overlay = callout.Label.GetComponentInParent<ClickNextTutorial>()?.GetComponent<Canvas>()
                ?? root.Find("DreamComboTutorial").GetComponent<Canvas>();
            Require(overlay != null && overlay.overrideSorting && overlay.sortingOrder > 1001,
                "Tutorial explanation is below an active HUD canvas.");
            var expected = ExpectedLayoutTarget(root, callout.Label, health);
            Require(callout.Target == expected, callout.Label.name + " points at the wrong UI target.");
            var expectedBounds = LayoutBounds(root, expected);
            if (expected == health)
            {
                var energyBounds = LayoutBounds(root, energy);
                expectedBounds = Rect.MinMaxRect(Mathf.Min(expectedBounds.xMin, energyBounds.xMin), Mathf.Min(expectedBounds.yMin, energyBounds.yMin),
                    Mathf.Max(expectedBounds.xMax, energyBounds.xMax), Mathf.Max(expectedBounds.yMax, energyBounds.yMax));
            }
            Require(Vector2.Distance(expectedBounds.position, callout.TargetBounds.position) < 1
                && Vector2.Distance(expectedBounds.size, callout.TargetBounds.size) < 1, "Callout bounds do not match actual target geometry.");
            var nearest = new Vector2(Mathf.Clamp(callout.Tip.x, expectedBounds.xMin, expectedBounds.xMax),
                Mathf.Clamp(callout.Tip.y, expectedBounds.yMin, expectedBounds.yMax));
            Require(Vector2.Distance(callout.Tip, nearest) <= 2, "Pointer tip misses its target: " + callout.Tip + " -> " + expectedBounds);
            Require(callout.Arrow != null && callout.Arrow.gameObject.activeInHierarchy, "Callout arrow is hidden.");
            var arrowTip = root.InverseTransformPoint(callout.Arrow.TransformPoint(
                new Vector3(callout.Arrow.rect.center.x, callout.Arrow.rect.yMax)));
            Require(Vector2.Distance(arrowTip, callout.Tip) <= 1, "Rendered arrow tip differs from the calculated target point.");
            Require(LayoutContains(pageReport.safeBounds, LayoutBounds(root, callout.Arrow)), "Arrow escapes safe area.");
            Require(callout.Panel != null && callout.Panel.gameObject.activeInHierarchy, "Callout panel is hidden.");
            var panel = LayoutBounds(root, callout.Panel);
            Require(LayoutContains(pageReport.safeBounds, panel), "Callout panel escapes safe area.");
            Require(panels.All(previous => !previous.Overlaps(panel)), "Tutorial explanation panels overlap.");
            var layer = root.GetComponent<FightingStepLayer>();
            foreach (var control in HUDControlRects(layer).Where(control => control.gameObject.activeInHierarchy))
                Require(!panel.Overlaps(LayoutBounds(root, control)), "Tutorial explanation covers an actual HUD control: " + control.name);
            report.panelsClearOfControls++;
            panels.Add(panel);
            pageReport.pointers.Add(new LayoutPointer
            {
                label = LayoutPath(root, callout.Label.transform), target = LayoutPath(root, expected),
                targetBounds = expectedBounds, tip = callout.Tip
            });
            report.pointersChecked++;
        }
    }

    static void CheckTutorialHUDTargets(RectTransform root, FightingStepLayer layer, Rect safe, LayoutReport report)
    {
        var controls = HUDControlRects(layer).Skip(2).ToArray();
        foreach (var control in controls)
        {
            Require(LayoutContains(safe, LayoutBounds(root, control)), "Tutorial HUD target is outside its safe area: " + control.name);
            Require(LayoutBounds(root, control).yMax < LayoutBounds(root, layer.MiddleArea).yMin + 2,
                "Tutorial HUD target enters the battle camera area: " + control.name);
            report.hudTargetsChecked++;
        }
        foreach (var animator in layer.Team1UI.AutoSwitch.GetComponentsInChildren<Animator>(true))
            Require(!animator.enabled, "A legacy AUTO animation controls the tutorial HUD geometry.");
        Require(Vector2.Distance(LayoutBounds(root, (RectTransform)layer.InputsManager.AttackButton.transform).size,
            Vector2.one * 156 * Mathf.Clamp(safe.width / 1200, .72f, 1.1f)) < 2, "Tutorial is showing legacy oversized skill controls.");
    }

    static RectTransform ExpectedLayoutTarget(RectTransform root, Text label, RectTransform health)
    {
        var path = LayoutPath(root, label.transform);
        if (path.Contains("/UserHPEXBarIntro/")) return health;
        string target = path.Contains("/Direction/") ? "Dark Joystick/Joystick Base"
            : path.Contains("/Attack1Intro/") ? "bottom/Attack"
            : path.Contains("/Attack2Intro/") ? "bottom/Fire1"
            : path.Contains("/Attack3Intro/") ? "bottom/Fire2"
            : path.Contains("/AutoIntro/") ? "top/team1Auto"
            : path.Contains("/DremComboIntro/") ? "middle/DreamCombo" : null;
        Require(target != null, "Unknown callout: " + path);
        var result = root.Find(target) as RectTransform;
        Require(result != null, "Missing expected target: " + target);
        return result;
    }

    static Rect LayoutGlyphBounds(RectTransform root, Text text)
    {
        using (var generator = new TextGenerator())
        {
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            generator.Populate(text.text, settings);
            var vertices = generator.verts;
            // preferredHeight alone can miss font ascender/descender rounding:
            // the last wrapped line may be silently truncated at exactly that height.
            using (var complete = new TextGenerator())
            {
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                complete.Populate(text.text, settings);
                Require(vertices.Count == complete.verts.Count,
                    LayoutPath(root, text.transform) + " truncates glyphs: rendered=" + (vertices.Count / 4)
                    + ", complete=" + (complete.verts.Count / 4) + ", height=" + text.rectTransform.rect.height
                    + ", preferred=" + text.preferredHeight);
            }
            // Unity 6 uGUI Text.OnPopulateMesh consumes every quad. There is no
            // trailing placeholder quad to remove; a one-character label has four vertices.
            Require(vertices.Count >= 4, "No generated text geometry: " + text.name + "; caption=" + text.text
                + "; font=" + (text.font != null ? text.font.name : "null") + "; fontSize=" + text.fontSize
                + "; rect=" + text.rectTransform.rect + "; scale=" + text.transform.lossyScale
                + "; active=" + text.gameObject.activeInHierarchy + "; verts=" + vertices.Count);
            var points = vertices
                .Select(vertex => root.InverseTransformPoint(text.transform.TransformPoint(vertex.position / text.pixelsPerUnit))).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }
    }

    static Rect LayoutBounds(RectTransform root, RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }

    static bool LayoutContains(Rect outer, Rect inner, float tolerance = 1) => inner.xMin >= outer.xMin - tolerance
        && inner.yMin >= outer.yMin - tolerance && inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;

    static string LayoutPath(Transform root, Transform child) => child == root ? root.name
        : LayoutPath(root, child.parent) + "/" + child.name;

    static void SaveLayoutRender(RenderTexture texture, string path)
    {
        var previous = RenderTexture.active;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
    }
}
