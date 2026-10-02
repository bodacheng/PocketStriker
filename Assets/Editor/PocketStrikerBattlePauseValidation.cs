using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightScene;
using MCombat.Shared.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Offline native pause-toggle interactions and localized production-prefab screenshots.</summary>
public static class PocketStrikerBattlePauseValidation
{
    const string PrefabPath = "Assets/Resources/DummyLayerSystem/FightScenePauseSupport.prefab";
    const string Output = "Logs/UILayout/BattlePause";
    const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
    static bool running;

    [Serializable] public sealed class Report
    {
        public bool passed, sourceFilesUnchanged, currentScenesUnchanged, globalsRestored;
        public int viewportLanguageCases, raycastsChecked, pointerCallbacks, activationCycles;
        public string unityVersion, utcTime;
        public string scope = "Real FightScenePauseSupport prefab and Setup, native Toggle pointer-click gate, production GraphicRaycaster hit targets, silent restores and actual presentation colors/anchors. Two portrait sizes, three languages and on/off state screenshots. Editor invokes presentation lifecycle callbacks for repeated activation; runtime listener delegates are inspected to detect duplicated Setup/presentation subscriptions. The isolated CameraManager dictionary is empty so the real camera callback has no preference-persistence side effect.";
        public string limitation = "Isolated stopped-Editor preview, not Play-mode battle initialization, physical-device touch or automatic orbit behavior. Rendered PNGs require human visual review in addition to geometry/color and nonuniform-image assertions.";
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public List<Evidence> screenshots = new List<Evidence>();
    }
    [Serializable] public sealed class Evidence
    {
        public string language, state, path, label;
        public int width, height;
        public Rect row, labelBounds, track, thumb;
        public Color trackColor, outlineColor;
    }

    [MenuItem("PocketStriker/Validation/Battle Pause Toggle")]
    public static void Validate()
    {
        var report = Run();
        if (report.passed) Debug.Log("[BattlePause] PASS: " + report.screenshots.Count + " screenshots. " + Output + "/report.json");
        else Debug.LogError("[BattlePause] FAIL: " + string.Join("\n", report.errors));
    }

    public static void ValidateBatch()
    {
        var report = Run();
        if (!report.passed) Debug.LogError("[BattlePause] FAIL: " + string.Join("\n", report.errors));
        else Debug.Log("[BattlePause] PASS: " + report.screenshots.Count + " screenshots. " + Output + "/report.json");
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Battle pause validation requires a stopped editor.");
        running = true;
        Directory.CreateDirectory(Output);
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var paths = new[] { PrefabPath, "Assets/RealTimeGameProcessManager/FightSettingSupport/BattleCameraTogglePresentation.cs",
            "Assets/RealTimeGameProcessManager/FightSettingSupport/FightScenePauseSupport.cs", "Assets/ExternalAssets/Config/LanguageCode.csv" };
        var sources = paths.ToDictionary(path => path, File.ReadAllBytes);
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var dirty = scenes.Select(scene => scene.isDirty).ToArray();
        var roots = scenes.Select(scene => scene.isLoaded ? scene.GetRootGameObjects().Select(item => item.GetEntityId().ToString()).OrderBy(id => id).ToArray() : Array.Empty<string>()).ToArray();
        var activeScene = SceneManager.GetActiveScene();
        var previewCount = EditorSceneManager.previewSceneCount;
        var oldSettings = AppSetting.Value;
        var oldBgm = AppSetting.BGMSource;
        var oldUiAudio = AppSetting.UiAudioSource;
        var oldFight = FightLoad.Fight;
        var oldManager = RTFightManager.Target;
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var oldEvents = EventSystem.current;
        var oldConverters = LanguageConverterManger.List;
        var loaderQueues = (List<UILayer>)typeof(UILayer).Assembly.GetType("DummyLayerSystem.UILayerLoader", true)
            .GetField("Queues", StaticFields).GetValue(null);
        var oldQueues = loaderQueues.ToArray();
        var rows = Translate.GetRowList();
        var oldRows = rows.ToArray();
        var providerField = typeof(Translate).GetField("languageProvider", StaticFields);
        var oldProvider = providerField.GetValue(null);
        var cameraGlobals = new[] { CameraManagerCore._camera, CameraManagerCore._subCamera, CameraManagerCore._centerCamera };
        bool hadAutoPreference = PlayerPrefs.HasKey("AutoRotateCamera");
        int autoPreference = PlayerPrefs.GetInt("AutoRotateCamera", 1);
        Scene preview = default;
        FightInfo fight = null;
        bool interactionsAttempted = false;
        EventSystem fixtureEvents = null;
        void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message);
        }
        Application.logMessageReceived += CaptureError;
        try
        {
            AppSetting.Value = new AppSetting { Language = SystemLanguage.English };
            // Audio slider restores may call their real handlers, so detach audio sources temporarily.
            typeof(AppSetting).GetField("bgmSource", StaticFields).SetValue(null, null);
            typeof(AppSetting).GetField("uiAudioSource", StaticFields).SetValue(null, null);
            LanguageConverterManger.List = new List<LanguageConverter>();
            rows.Clear();
            foreach (var row in CsvParser2.Parse(File.ReadAllText(paths[3])).Skip(1))
                if (row.Length >= 4) rows.Add(new Translate.Row { RECORD_ID = row[0], EN = row[1], JP = row[2], CH = row[3] });
            providerField.SetValue(null, (Func<SystemLanguage>)(() => AppSetting.Value.Language));
            fight = ScriptableObject.CreateInstance<FightInfo>();
            fight.team1Mode = TeamMode.Rotation;
            FightLoad.Fight = fight;

            preview = EditorSceneManager.NewPreviewScene();
            var rig = new GameObject("Battle Pause Validation");
            SceneManager.MoveGameObjectToScene(rig, preview);
            // Keep combat components inactive; no battle/service lifecycle is started.
            var combat = new GameObject("Inactive Combat Callback Fixture");
            combat.SetActive(false);
            combat.transform.SetParent(rig.transform, false);
            var manager = combat.AddComponent<RTFightManager>();
            var cameraManager = combat.AddComponent<CameraManager>();
            typeof(CameraManagerCore).GetField("CModeDic", InstanceFields).SetValue(cameraManager, new Dictionary<C_Mode, CameraModeCore>());
            manager._CameraManager = cameraManager;
            RTFightManager.Target = manager;

            var cameraObject = new GameObject("Pause Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.enabled = false;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.09f, 1);
            var canvasObject = new GameObject("Pause Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.worldCamera = camera;
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            var safeObject = new GameObject("Safe Area", typeof(RectTransform));
            safeObject.transform.SetParent(canvas.transform, false);
            var safe = (RectTransform)safeObject.transform;
            safe.anchorMin = Vector2.zero;
            safe.anchorMax = Vector2.one;
            PosCal.Canvas = canvas;
            PosCal.SafeAreaRect = safe;
            var eventObject = new GameObject("Pause Pointer Fixture", typeof(EventSystem));
            eventObject.transform.SetParent(rig.transform, false);
            var events = fixtureEvents = eventObject.GetComponent<EventSystem>();
            if (!Registered(events)) typeof(EventSystem).GetMethod("OnEnable", InstanceFields).Invoke(events, null);
            EventSystem.current = events;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(source != null, "Pause prefab is missing.");

            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(1206, 2622) })
            foreach (var language in new[] { SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.Chinese })
            {
                GameObject instance = null;
                RenderTexture target = null;
                try
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    float scale = Mathf.Min(size.x / 1080f, size.y / 1920f);
                    var canvasSize = new Vector2(size.x, size.y) / scale;
                    canvasRect.sizeDelta = canvasSize;
                    // 390x844 uses a representative notch/home-indicator inset; larger capture matches the supplied screenshot shape.
                    safe.offsetMin = size.x == 390 ? new Vector2(0, 34f / scale) : Vector2.zero;
                    safe.offsetMax = size.x == 390 ? new Vector2(0, -47f / scale) : Vector2.zero;
                    camera.orthographicSize = canvasSize.y * 0.5f;
                    camera.aspect = (float)size.x / size.y;
                    AppSetting.Value.Language = language;
                    instance = UnityEngine.Object.Instantiate(source, safe, false);
                    var root = (RectTransform)instance.transform;
                    root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                    root.offsetMin = root.offsetMax = Vector2.zero;
                    foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var group in instance.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
                    var layer = instance.GetComponent<FightScenePauseSupport>();
                    layer.ResizeAreas();
                    foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) converter.Change();
                    var toggle = Field<Toggle>(layer, "autoRoateCamera");
                    var presentation = toggle.GetComponent<BattleCameraTogglePresentation>();
                    Require(presentation != null, "Native Toggle has no production presentation component.");
                    if (ListenerCount(toggle.onValueChanged, presentation, "OnValueChanged") == 0) Lifecycle(presentation, "OnEnable");
                    var track = Field<Image>(presentation, "track");
                    var outline = Field<Image>(presentation, "outline");
                    var thumb = Field<RectTransform>(presentation, "thumb");
                    var label = toggle.transform.Find("Label").GetComponent<Text>();
                    CheckArt(toggle, track, outline, thumb, label);

                    target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                    target.Create();
                    camera.targetTexture = target;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.planeDistance = 1;
                    canvas.scaleFactor = scale;
                    canvas.pixelPerfect = true;
                    Settle(root);
                    CheckLayout(root, safe, toggle, label, track, thumb);
                    Require(label.text == Translate.Get("AutoCamera"), "Switch label is not localized for " + language);
                    foreach (char character in label.text.Where(value => !char.IsWhiteSpace(value)))
                        Require(label.font != null && label.font.HasCharacter(character), "Switch label is missing a glyph: " + character);

                    if (!interactionsAttempted)
                    {
                        interactionsAttempted = true;
                        CheckCallbacks(layer, toggle, presentation, events, canvas.GetComponent<GraphicRaycaster>(), camera, label, track, report);
                        report.checks.Add("Native label/row/track clicks, disabled/inactive gates and eight Setup/activation cycles retain one owned listener and one external callback per click.");
                    }
                    foreach (bool on in new[] { false, true })
                    {
                        toggle.interactable = true;
                        toggle.SetIsOnWithoutNotify(on);
                        presentation.RefreshVisuals();
                        Settle(root);
                        CheckVisuals(toggle, track, outline, thumb, on, true);
                        CheckLayout(root, safe, toggle, label, track, thumb);
                        var evidence = new Evidence { language = language.ToString(), state = on ? "on" : "off", width = size.x, height = size.y,
                            path = Path.Combine(Output, $"pause-{size.x}x{size.y}-{language}-{(on ? "on" : "off")}.png"), label = label.text,
                            row = Bounds(root, (RectTransform)toggle.transform), labelBounds = Bounds(root, label.rectTransform),
                            track = Bounds(root, track.rectTransform), thumb = Bounds(root, thumb), trackColor = track.color, outlineColor = outline.color };
                        Render(canvas, camera, target, evidence.path);
                        report.screenshots.Add(evidence);
                    }
                    report.viewportLanguageCases++;
                }
                catch (Exception exception) { report.errors.Add(size + " / " + language + ": " + exception); }
                finally
                {
                    camera.targetTexture = null;
                    canvas.renderMode = RenderMode.WorldSpace;
                    if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                    LanguageConverterManger.List.RemoveAll(converter => converter == null);
                }
            }
            report.checks.Add("Two portrait sizes and three languages retain in-bounds, separate label/track/thumb rectangles and real sprite references; 12 on/off PNGs captured.");
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            if (fixtureEvents != null && Registered(fixtureEvents))
                typeof(EventSystem).GetMethod("OnDisable", InstanceFields).Invoke(fixtureEvents, null);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (fight != null) UnityEngine.Object.DestroyImmediate(fight);
            AppSetting.Value = oldSettings;
            typeof(AppSetting).GetField("bgmSource", StaticFields).SetValue(null, oldBgm);
            typeof(AppSetting).GetField("uiAudioSource", StaticFields).SetValue(null, oldUiAudio);
            FightLoad.Fight = oldFight; RTFightManager.Target = oldManager;
            PosCal.Canvas = oldCanvas; PosCal.SafeAreaRect = oldSafe;
            if (oldEvents != null) EventSystem.current = oldEvents;
            LanguageConverterManger.List = oldConverters;
            // UILayer.OnDestroy prunes null loader entries; preserve even those unrelated editor entries.
            loaderQueues.Clear(); loaderQueues.AddRange(oldQueues);
            rows.Clear(); rows.AddRange(oldRows); providerField.SetValue(null, oldProvider);
            CameraManagerCore._camera = cameraGlobals[0]; CameraManagerCore._subCamera = cameraGlobals[1]; CameraManagerCore._centerCamera = cameraGlobals[2];
            if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
            report.sourceFilesUnchanged = sources.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value));
            report.currentScenesUnchanged = EditorSceneManager.previewSceneCount == previewCount && SceneManager.sceneCount == scenes.Length
                && Enumerable.Range(0, scenes.Length).All(index => SceneManager.GetSceneAt(index) == scenes[index] && scenes[index].isDirty == dirty[index]
                    && (!scenes[index].isLoaded || scenes[index].GetRootGameObjects().Select(item => item.GetEntityId().ToString()).OrderBy(id => id).SequenceEqual(roots[index])))
                && SceneManager.GetActiveScene() == activeScene;
            report.globalsRestored = ReferenceEquals(AppSetting.Value, oldSettings) && AppSetting.BGMSource == oldBgm && AppSetting.UiAudioSource == oldUiAudio
                && FightLoad.Fight == oldFight && RTFightManager.Target == oldManager && PosCal.Canvas == oldCanvas && PosCal.SafeAreaRect == oldSafe
                && EventSystem.current == oldEvents && ReferenceEquals(LanguageConverterManger.List, oldConverters) && rows.SequenceEqual(oldRows)
                && loaderQueues.SequenceEqual(oldQueues)
                && ReferenceEquals(providerField.GetValue(null), oldProvider) && CameraManagerCore._camera == cameraGlobals[0]
                && CameraManagerCore._subCamera == cameraGlobals[1] && CameraManagerCore._centerCamera == cameraGlobals[2]
                && PlayerPrefs.HasKey("AutoRotateCamera") == hadAutoPreference && PlayerPrefs.GetInt("AutoRotateCamera", 1) == autoPreference;
            if (!report.sourceFilesUnchanged || !report.currentScenesUnchanged || !report.globalsRestored)
                report.errors.Add("Validation changed source files, open/preview scenes or shared state.");
            report.passed = report.errors.Count == 0 && report.viewportLanguageCases == 6 && report.screenshots.Count == 12 && report.activationCycles == 8;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= CaptureError;
            running = false;
        }
        return report;
    }

    static void CheckCallbacks(FightScenePauseSupport layer, Toggle toggle, BattleCameraTogglePresentation presentation, EventSystem events,
        GraphicRaycaster raycaster, Camera camera, Text label, Image track, Report report)
    {
        int callbacks = 0, setupCalls = 0;
        toggle.onValueChanged.AddListener(_ => callbacks++);
        for (int cycle = 0; cycle < 8; cycle++)
        {
            layer.Setup(() => setupCalls++, () => { }, () => { });
            Require(callbacks == 0, "Setup notified a restored preference.");
            Require(ListenerCount(toggle.onValueChanged, layer, "OnAutoRotateCameraChanged") == 1, "Repeated Setup duplicated the real camera callback.");
            if (ListenerCount(toggle.onValueChanged, presentation, "OnValueChanged") != 0) Lifecycle(presentation, "OnDisable");
            toggle.gameObject.SetActive(false);
            Require(ListenerCount(toggle.onValueChanged, presentation, "OnValueChanged") == 0, "Presentation subscription survived deactivation.");
            toggle.gameObject.SetActive(true);
            if (ListenerCount(toggle.onValueChanged, presentation, "OnValueChanged") == 0) Lifecycle(presentation, "OnEnable");
            Require(ListenerCount(toggle.onValueChanged, presentation, "OnValueChanged") == 1, "Repeated activation duplicated presentation callbacks.");
            report.activationCycles++;
        }
        Require(setupCalls == 8, "Setup did not run its immediate action exactly once.");
        var row = (RectTransform)toggle.transform;
        foreach (var point in new[] { label.rectTransform.TransformPoint(label.rectTransform.rect.center),
            row.TransformPoint(new Vector2(row.rect.xMin + 20, row.rect.center.y)), track.rectTransform.TransformPoint(track.rectTransform.rect.center) })
        {
            bool before = toggle.isOn;
            Click(toggle, events, raycaster, camera, point, report);
            Require(toggle.isOn != before && callbacks == report.pointerCallbacks + 1, "Native pointer click did not dispatch exactly once.");
            report.pointerCallbacks++;
        }
        int count = callbacks;
        toggle.SetIsOnWithoutNotify(false); presentation.RefreshVisuals();
        toggle.SetIsOnWithoutNotify(true); presentation.RefreshVisuals();
        Require(callbacks == count, "Silent state restoration emitted a value callback.");
        CheckVisuals(toggle, Field<Image>(presentation, "track"), Field<Image>(presentation, "outline"), Field<RectTransform>(presentation, "thumb"), true, true);
        toggle.SetIsOnWithoutNotify(false);
        Lifecycle(presentation, "LateUpdate");
        CheckVisuals(toggle, Field<Image>(presentation, "track"), Field<Image>(presentation, "outline"), Field<RectTransform>(presentation, "thumb"), false, true);
        toggle.SetIsOnWithoutNotify(true);
        Lifecycle(presentation, "LateUpdate");
        Require(callbacks == count, "Late presentation refresh emitted a value callback.");
        toggle.interactable = false;
        Lifecycle(presentation, "LateUpdate");
        CheckVisuals(toggle, Field<Image>(presentation, "track"), Field<Image>(presentation, "outline"), Field<RectTransform>(presentation, "thumb"), true, false);
        Click(toggle, events, raycaster, camera, label.rectTransform.TransformPoint(label.rectTransform.rect.center), report);
        Require(toggle.isOn && callbacks == count, "Disabled native Toggle accepted a click.");
        var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
        toggle.gameObject.SetActive(false);
        ExecuteEvents.Execute(toggle.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Require(callbacks == count, "Inactive native Toggle accepted a click.");
        toggle.gameObject.SetActive(true); toggle.interactable = true;
        if (ListenerCount(toggle.onValueChanged, presentation, "OnValueChanged") == 0) Lifecycle(presentation, "OnEnable");
        presentation.RefreshVisuals();
    }

    static void Click(Toggle toggle, EventSystem events, GraphicRaycaster raycaster, Camera camera, Vector3 worldPoint, Report report)
    {
        // A stopped-Editor canvas has no frame loop assigning native graphic depths.
        // Render after activation/dirty geometry before testing the real raycaster.
        Draw(raycaster.GetComponent<Canvas>(), camera, camera.targetTexture);
        var data = new PointerEventData(events) { position = camera.WorldToScreenPoint(worldPoint), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        raycaster.Raycast(data, hits);
        var hit = hits.Where(item => item.gameObject != null).OrderByDescending(item => item.depth).FirstOrDefault().gameObject;
        var graphic = toggle.GetComponent<Image>();
        Require(hit != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == toggle.gameObject,
            "Label/row/track click is blocked by " + (hit != null ? hit.name : "nothing") + " at " + data.position
            + "; row depth=" + graphic.depth + ", cull=" + graphic.canvasRenderer.cull + ", active=" + graphic.isActiveAndEnabled
            + ", raycast=" + graphic.raycastTarget + ", canvas=" + graphic.canvas?.name + ", camera=" + raycaster.eventCamera?.name
            + ", pixelRect=" + camera.pixelRect + ", inside=" + RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, data.position, camera)
            + ", filter=" + graphic.Raycast(data.position, camera));
        report.raycastsChecked++;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
    }

    static void CheckArt(Toggle toggle, Image track, Image outline, RectTransform thumb, Text label)
    {
        Require(toggle.GetType() == typeof(Toggle) && toggle.targetGraphic == thumb.GetComponent<Image>() && toggle.graphic == null,
            "Switch no longer uses a native Toggle with an always-visible thumb.");
        Require(toggle.GetComponent<Image>()?.raycastTarget == true && !label.raycastTarget && !track.raycastTarget && !outline.raycastTarget
            && !thumb.GetComponent<Image>().raycastTarget, "Switch does not have one complete, unobstructed row click target.");
        Require(!toggle.GetComponent<Image>().canvasRenderer.cullTransparentMesh, "Transparent full-row switch target can be culled from raycasts.");
        Require(track.sprite != null && outline.sprite != null && thumb.GetComponent<Image>().sprite != null, "Switch sprite reference is missing.");
        Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(track.sprite)) == "c541b21ee2004b9aacbd84ee924843af"
            && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(outline.sprite)) == "efb71ca1ee684dd8874d7c61ac1f97ca"
            && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(thumb.GetComponent<Image>().sprite)) == "c26a820b2129e2f4eb122efd27df5339",
            "Switch is not using the authored rounded fill/outline and circular thumb textures.");
        Require(track.type == Image.Type.Sliced && outline.type == Image.Type.Sliced && thumb.GetComponent<Image>().preserveAspect,
            "Switch shape will stretch its rounded track or circle.");
    }

    static void CheckVisuals(Toggle toggle, Image track, Image outline, RectTransform thumb, bool on, bool interactable)
    {
        float alpha = interactable ? 1f : 0.45f;
        Require(toggle.isOn == on && toggle.IsInteractable() == interactable, "Toggle state does not match the requested visual state.");
        Require(Close(track.color, on ? new Color(0.04f, 0.55f, 0.65f, alpha) : new Color(0.09f, 0.16f, 0.21f, alpha))
            && Close(outline.color, on ? new Color(0.30f, 0.90f, 0.96f, alpha) : new Color(0.39f, 0.54f, 0.60f, alpha)), "Switch state has the wrong track/outline color.");
        var anchor = new Vector2(on ? 1 : 0, 0.5f);
        Require(Vector2.Distance(thumb.anchorMin, anchor) < 0.01f && Vector2.Distance(thumb.anchorMax, anchor) < 0.01f
            && Vector2.Distance(thumb.anchoredPosition, new Vector2(on ? -44 : 44, 0)) < 0.01f, "Switch thumb is on the wrong end after a silent restore.");
        Require(thumb.gameObject.activeInHierarchy && thumb.GetComponent<Image>().color == Color.white, "Switch thumb is hidden or not white.");
    }

    static void CheckLayout(RectTransform root, RectTransform safe, Toggle toggle, Text label, Image track, RectTransform thumb)
    {
        var safeBounds = Bounds(root, safe);
        var row = Bounds(root, (RectTransform)toggle.transform);
        var labelBounds = Bounds(root, label.rectTransform);
        var trackBounds = Bounds(root, track.rectTransform);
        Require(Contains(safeBounds, row) && Contains(row, labelBounds) && Contains(row, trackBounds) && Contains(trackBounds, Bounds(root, thumb)),
            "Pause switch row/label/track/thumb exceeds its safe-area container.");
        Require(!labelBounds.Overlaps(trackBounds) && label.preferredHeight <= label.rectTransform.rect.height + 1,
            "Localized switch label overlaps the track or clips vertically.");
        foreach (var selectable in root.GetComponentsInChildren<Selectable>(true).Where(value => value != toggle && value.gameObject.activeInHierarchy))
            Require(!row.Overlaps(Bounds(root, (RectTransform)selectable.transform)), "Switch row overlaps another pause control: " + selectable.name);
    }

    static void Render(Canvas canvas, Camera camera, RenderTexture target, string path)
    {
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            Draw(canvas, camera, target);
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            var pixels = image.GetPixels32(); var background = pixels[0];
            Require(pixels.Any(pixel => pixel.r != background.r || pixel.g != background.g || pixel.b != background.b), "Pause preview is uniform; UI was not rendered.");
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; if (image != null) UnityEngine.Object.DestroyImmediate(image); }
    }

    static void Draw(Canvas canvas, Camera camera, RenderTexture target)
    {
        foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) { graphic.SetAllDirty(); graphic.Rebuild(CanvasUpdate.PreRender); }
        Canvas.ForceUpdateCanvases();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
        Canvas.ForceUpdateCanvases();
    }

    static void Settle(RectTransform root) { Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(root); Canvas.ForceUpdateCanvases(); }
    static void Lifecycle(BattleCameraTogglePresentation presentation, string name) => typeof(BattleCameraTogglePresentation).GetMethod(name, InstanceFields).Invoke(presentation, null);
    static bool Registered(EventSystem events) => ((IList)typeof(EventSystem).GetField("m_EventSystems", StaticFields).GetValue(null)).Contains(events);
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, InstanceFields).GetValue(owner);
    static int ListenerCount(UnityEventBase value, object target, string method)
    {
        var calls = typeof(UnityEventBase).GetField("m_Calls", InstanceFields).GetValue(value);
        var runtime = (IEnumerable)calls.GetType().GetField("m_RuntimeCalls", InstanceFields).GetValue(calls);
        int count = 0;
        foreach (var call in runtime)
        for (var type = call.GetType(); type != null; type = type.BaseType)
        foreach (var field in type.GetFields(InstanceFields | BindingFlags.DeclaredOnly))
            if (typeof(Delegate).IsAssignableFrom(field.FieldType) && field.GetValue(call) is Delegate callback)
                count += callback.GetInvocationList().Count(item => ReferenceEquals(item.Target, target) && item.Method.Name == method);
        return count;
    }
    static Rect Bounds(RectTransform root, RectTransform child)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMin >= outer.yMin - 1 && inner.yMax <= outer.yMax + 1;
    static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < 0.01f;
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
