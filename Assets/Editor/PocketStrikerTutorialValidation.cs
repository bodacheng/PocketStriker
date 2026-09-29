using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using FightScene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Actual prefab UI raycasts and tutorial callbacks, without account or battle startup.</summary>
public static partial class PocketStrikerTutorialValidation
{
    const string PrefabPath = "Assets/Resources/DummyLayerSystem/FightingStepLayer.prefab";
    const string ReportPath = "Logs/Tutorial/report.json";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool running;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public int pagesChecked;
        public int raycastsChecked;
        public int storyCasesChecked;
        public int blockedClicksChecked;
        public int blockedStoryChecks;
        public bool pausedDelayPassed;
        public bool reopenDelayPassed;
        public bool forceAutoPassed;
        public bool sourcePrefabUnchanged;
        public string scope = "Copied FightingStepLayer prefab transforms and built-in UI components; actual runtime overlay promotion, ClickNextTutorial page/click/delay logic, force-auto callback and AI-story eligibility. EventSystem.RaycastAll runs against rendered UI graphics.";
        public string limitation = "Battle simulation, joystick input, localization and skill execution are omitted. The validation layer overrides battle-camera teardown only. Battle EventTrigger callbacks are replaced by counters; authored ForceClickAutoBtn tutorial events run normally. AI generation/network calls are not made. UniTask forces realtime delays in Edit mode, so paused-delay checks are not a substitute for Play-mode timing validation.";
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Tutorial")]
    public static async void Validate() { await Run(false); }

    // Do not pass -quit: the asynchronous validation exits Unity when it has completed.
    public static async void ValidateBatch() { await Run(true); }

    static async UniTask Run(bool exitWhenDone)
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Tutorial validation requires a stopped editor.");
        running = true;
        var report = new Report { unityVersion = Application.unityVersion };
        var sourceText = File.ReadAllText(PrefabPath);
        var oldTimeScale = Time.timeScale;
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var oldFight = FightLoad.Fight;
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Tutorial Validation");
        rig.SetActive(false);
        SceneManager.MoveGameObjectToScene(rig, scene);
        RenderTexture texture = null;
        EventSystem eventSystem = null;
        void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message);
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
            camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000;
            texture = new RenderTexture(540, 960, 24);
            texture.Create();
            camera.targetTexture = texture;
            camera.aspect = 540f / 960;

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1080, 1920);
            PosCal.Canvas = canvas;
            PosCal.SafeAreaRect = canvasRect;
            var eventObject = new GameObject("EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(rig.transform, false);
            eventSystem = eventObject.GetComponent<EventSystem>();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(source != null, "FightingStepLayer prefab is missing.");
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var root = (RectTransform)CopyHierarchy(source.transform, canvas.transform, map);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.localPosition = Vector3.zero;
            root.localScale = Vector3.one;
            foreach (var original in source.GetComponentsInChildren<Component>(true))
            {
                if (original == null || !CopyComponent(original)) continue;
                var target = ((Transform)map[original.transform]).gameObject.AddComponent(original.GetType());
                EditorUtility.CopySerialized(original, target);
                map.Add(original, target);
            }
            foreach (var pair in map.ToArray())
                if (pair.Key is Component && !(pair.Key is Transform)) RemapReferences((Component)pair.Value, map);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.transition = Selectable.Transition.None;
            }
            foreach (var trigger in root.GetComponentsInChildren<EventTrigger>(true)) trigger.triggers.Clear();
            foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

            var sourceLayer = source.GetComponent<FightingStepLayer>();
            // Runtime teardown expects a live RTFightManager. The subclass changes only teardown.
            var layer = root.gameObject.AddComponent<PocketStrikerTutorialValidationLayer>();
            foreach (var name in new[] { "pauseButton", "team1UI", "team2UI", "clickNextTutorial", "clickTriggerDreamCombo", "forceClickAutoBtnBlackMask" })
            {
                var field = typeof(FightingStepLayer).GetField(name, Private);
                field.SetValue(layer, map[(UnityEngine.Object)field.GetValue(sourceLayer)]);
            }
            foreach (var name in new[] { "top", "middle", "bottom" })
            {
                var field = typeof(UILayer).GetField(name, Private);
                field.SetValue(layer, map[(UnityEngine.Object)field.GetValue(sourceLayer)]);
            }
            var tutorial = Field<ClickNextTutorial>(layer, "clickNextTutorial");
            var pages = Field<GameObject[]>(tutorial, "TutorialLayers");
            Require(pages.Length == 6, "Expected the six authored tutorial pages.");
            foreach (var special in root.GetComponentsInChildren<TutorialLayerSpecialEvent>(true))
            {
                var original = map.First(pair => pair.Value == special).Key as TutorialLayerSpecialEvent;
                special.onClick = new UnityEvent();
                for (var i = 0; i < original.onClick.GetPersistentEventCount(); i++)
                {
                    var method = original.onClick.GetPersistentMethodName(i);
                    Require(method == "ForceClickAutoBtn", "Unhandled authored tutorial event: " + method);
                    special.onClick.AddListener(layer.ForceClickAutoBtn);
                }
            }
            tutorial.Button.onClick.RemoveAllListeners();
            Invoke(tutorial, "Awake");
            SetField(tutorial, "clickDelay", 0.03f);
            Invoke(layer, "ResetOverlayStates");
            Invoke(layer, "KeepTopButtonsClickable");
            layer.ResizeAreas();

            var auto = layer.Team1UI.AutoSwitch;
            auto.gameObject.SetActive(true);
            var automatic = false;

            var originalSkill = source.GetComponentsInChildren<EventTrigger>(true).First(trigger =>
                trigger.triggers.Any(entry => Enumerable.Range(0, entry.callback.GetPersistentEventCount())
                    .Any(index => entry.callback.GetPersistentMethodName(index) == "Fire1Down")));
            var skill = (EventTrigger)map[originalSkill];
            skill.gameObject.SetActive(true);
            var skillClicks = 0;
            var skillDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            skillDown.callback.AddListener(_ => skillClicks++);
            skill.triggers.Add(skillDown);
            rig.SetActive(true);
            Invoke(eventSystem, "OnEnable");
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.Rebind();
            auto.Initialize(() => automatic, value => automatic = value);
            Rebuild(root, camera, texture);
            Vector2 blank = camera.WorldToScreenPoint(root.TransformPoint(new Vector3(0, 120, 0)));
            var skillPoint = Center(skill.transform, camera);
            var autoPoint = Center(auto.transform, camera);
            CheckTarget(eventSystem, skillPoint, skill.gameObject, "Closed tutorial: skill button", report);
            CheckTarget(eventSystem, autoPoint, auto.gameObject, "Closed tutorial: auto button", report);

            Time.timeScale = 0;
            layer.OpenTutorial();
            Require(tutorial.GetComponent<Canvas>().sortingOrder > auto.GetComponent<Canvas>().sortingOrder, "Tutorial sorting order did not stay above auto: " + tutorial.GetComponent<Canvas>().sortingOrder + "/" + auto.GetComponent<Canvas>().sortingOrder);
            Require(!tutorial.Button.interactable, "Tutorial did not lock its opening click.");
            await WaitFor(() => tutorial.Button.interactable, "Tutorial stayed locked while timeScale=0.");
            report.pausedDelayPassed = true;
            for (var page = 0; page < pages.Length; page++)
            {
                Require(Field<int>(tutorial, "pageIndex") == page, "Incorrect current tutorial page.");
                Require(pages.Count(item => item.activeSelf) == 1 && pages[page].activeSelf, "Exactly the expected page must be active.");
                Rebuild(root, camera, texture);
                foreach (var point in new[] { blank, skillPoint, autoPoint })
                    CheckTarget(eventSystem, point, tutorial.Button.gameObject, "Page " + (page + 1), report);
                Require(!automatic, "Force-auto action ran on page entry instead of on the next click.");
                var pointToClick = new[] { blank, skillPoint, autoPoint }[page % 3];
                Dispatch(eventSystem, pointToClick);
                Require(skillClicks == 0, "Tutorial click leaked into a battle skill.");
                var hasSpecial = pages[page].GetComponent<TutorialLayerSpecialEvent>() != null;
                if (hasSpecial)
                {
                    Require(automatic, "Special page's next click did not enable auto.");
                    Require(!Field<GameObject>(layer, "forceClickAutoBtnBlackMask").activeSelf, "Force-auto mask did not close.");
                    automatic = false;
                    report.forceAutoPassed = true;
                }
                report.pagesChecked++;
                if (page + 1 < pages.Length)
                {
                    Dispatch(eventSystem, pointToClick);
                    Require(Field<int>(tutorial, "pageIndex") == page + 1 && skillClicks == 0,
                        "A rapid repeat click skipped a tutorial page or triggered a skill.");
                    report.blockedClicksChecked++;
                    await WaitFor(() => tutorial.Button.interactable, "Next tutorial page stayed locked.");
                }
            }
            Require(!tutorial.gameObject.activeSelf, "The last click did not close the tutorial.");
            Rebuild(root, camera, texture);
            CheckTarget(eventSystem, skillPoint, skill.gameObject, "Finished tutorial: skill button", report);
            CheckTarget(eventSystem, autoPoint, auto.gameObject, "Finished tutorial: auto button", report);
            Dispatch(eventSystem, skillPoint);
            Require(skillClicks == 1, "Skill input did not resume after tutorial close.");

            SetField(tutorial, "clickDelay", 0.1f);
            tutorial.Open();
            await UniTask.Delay(TimeSpan.FromSeconds(0.04), ignoreTimeScale: true);
            tutorial.gameObject.SetActive(false);
            SetField(tutorial, "clickDelay", 0.3f);
            tutorial.Open();
            await UniTask.Delay(TimeSpan.FromSeconds(0.13), ignoreTimeScale: true);
            Require(!tutorial.Button.interactable, "A stale page delay unlocked a reopened tutorial.");
            await WaitFor(() => tutorial.Button.interactable, "Reopened tutorial never unlocked.");
            report.reopenDelayPassed = true;
            tutorial.gameObject.SetActive(false);
            CheckStoryRules(rig.transform, report);
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            Time.timeScale = oldTimeScale;
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafe;
            FightLoad.Fight = oldFight;
            if (eventSystem != null) Invoke(eventSystem, "OnDisable");
            foreach (var raycaster in rig.GetComponentsInChildren<GraphicRaycaster>(true))
                typeof(BaseRaycaster).GetMethod("OnDisable", Private).Invoke(raycaster, null);
            UnityEngine.Object.DestroyImmediate(rig);
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSceneManager.ClosePreviewScene(scene);
            Application.logMessageReceived -= CaptureError;
            report.sourcePrefabUnchanged = sourceText == File.ReadAllText(PrefabPath);
            report.passed = report.errors.Count == 0 && report.sourcePrefabUnchanged && report.pagesChecked == 6
                && report.forceAutoPassed && report.pausedDelayPassed && report.reopenDelayPassed && report.storyCasesChecked == 9 && report.blockedClicksChecked == 5 && report.blockedStoryChecks == 5;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            running = false;
        }
        var summary = $"[Tutorial] {(report.passed ? "PASS" : "FAIL")}: {report.pagesChecked} pages, {report.raycastsChecked} raycasts, {report.storyCasesChecked} story cases. {Path.GetFullPath(ReportPath)}";
        if (report.passed) Debug.Log(summary);
        else Debug.LogError(summary + "\n" + string.Join("\n", report.errors));
        if (exitWhenDone) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    static void CheckStoryRules(Transform parent, Report report)
    {
        var obj = new GameObject("Inactive story policy probe");
        obj.SetActive(false);
        obj.transform.SetParent(parent, false);
        var controller = obj.AddComponent<FightScene.FightScene>();
        var info = ScriptableObject.CreateInstance<FightInfo>();
        var cached = ScriptableObject.CreateInstance<StoryInfo>();
        try
        {
            FightLoad.Fight = null;
            Require(!(bool)Invoke(controller, "ShouldLoadAIStory"), "Null fight allows AI story.");
            report.storyCasesChecked++;
            foreach (var fixture in new[] {
                (FightEventType.Quest, "1", false, false), (FightEventType.Quest, "2", false, false),
                (FightEventType.Quest, "3", true, false), (FightEventType.Event, "event", true, false),
                (FightEventType.Quest, "3", false, true), (FightEventType.Event, "event", false, true),
                (FightEventType.Gangbang, "1", false, true), (FightEventType.SkillTest, "1", false, false)
            })
            {
                info.EventType = fixture.Item1;
                info.ID = fixture.Item2;
                info.RunTutorial = fixture.Item3;
                FightLoad.Fight = info;
                Require((bool)Invoke(controller, "ShouldLoadAIStory") == fixture.Item4,
                    "Incorrect AI-story eligibility: " + fixture);
                report.storyCasesChecked++;
                if (!fixture.Item4)
                {
                    SetField(controller, "aiStoryInfo", cached);
                    Require(controller.AIStoryInfo == null && controller.EnsureAIStory().GetAwaiter().GetResult() == null,
                        "A blocked stage exposed a cached AI story: " + fixture);
                    SetField(controller, "aiStoryInfo", null);
                    // A pending sentinel catches accidental reuse without allowing a regression to make a network request.
                    var pending = new UniTaskCompletionSource<StoryInfo>();
                    SetField(controller, "aiStoryLoadSource", pending);
                    var result = controller.EnsureAIStory();
                    Require(result.Status == UniTaskStatus.Succeeded && result.GetAwaiter().GetResult() == null,
                        "A blocked stage waited for AI generation: " + fixture);
                    Require(controller.AIServiceManager == null && ReferenceEquals(Field<UniTaskCompletionSource<StoryInfo>>(controller, "aiStoryLoadSource"), pending),
                        "A blocked stage initialized the AI service or replaced the pending source.");
                    SetField(controller, "aiStoryLoadSource", null);
                    report.blockedStoryChecks++;
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(info); UnityEngine.Object.DestroyImmediate(cached); }
    }

    static Transform CopyHierarchy(Transform source, Transform parent, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        var obj = source is RectTransform ? new GameObject(source.name, typeof(RectTransform)) : new GameObject(source.name);
        obj.SetActive(false);
        var copy = obj.transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        if (source is RectTransform rect && copy is RectTransform target)
        {
            target.anchorMin = rect.anchorMin; target.anchorMax = rect.anchorMax;
            target.pivot = rect.pivot; target.sizeDelta = rect.sizeDelta; target.anchoredPosition3D = rect.anchoredPosition3D;
        }
        map.Add(source, copy);
        map.Add(source.gameObject, obj);
        foreach (Transform child in source) CopyHierarchy(child, copy, map);
        obj.SetActive(source.gameObject.activeSelf);
        return copy;
    }

    static bool CopyComponent(Component component) => component is Graphic || component is Selectable
        || component is Canvas || component is CanvasGroup || component is GraphicRaycaster || component is CanvasRenderer
        || component is LayoutGroup || component is LayoutElement || component is ContentSizeFitter
        || component is AspectRatioFitter || component is Mask || component is RectMask2D
        || component is EventTrigger || component is ClickNextTutorial || component is TutorialLayerSpecialEvent
        || component is UIPosClamper
        || component is TeamUIManager || component is AutoSwitch
        || (component is Animator && component.GetComponentInParent<AutoSwitch>(true) != null);

    static void RemapReferences(Component copy, IDictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        var serialized = new SerializedObject(copy);
        var property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference
                && property.objectReferenceValue != null && map.TryGetValue(property.objectReferenceValue, out var value))
                property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Rebuild(RectTransform root, Camera camera, RenderTexture target)
    {
        foreach (var layout in root.GetComponentsInChildren<LayoutGroup>(true).Reverse())
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
        foreach (var nestedCanvas in root.GetComponentsInChildren<Canvas>(true)) nestedCanvas.worldCamera = camera;
        foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.Rebuild(CanvasUpdate.PreRender);
        foreach (var raycaster in root.GetComponentsInParent<Canvas>()[0].GetComponentsInChildren<GraphicRaycaster>(true))
            typeof(BaseRaycaster).GetMethod("OnEnable", Private).Invoke(raycaster, null);
        Canvas.ForceUpdateCanvases();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
        Canvas.ForceUpdateCanvases();
    }

    static Vector2 Center(Transform transform, Camera camera)
    {
        var rect = (RectTransform)transform;
        return camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
    }

    static GameObject Hit(EventSystem system, Vector2 point)
    {
        var hits = new List<RaycastResult>();
        system.RaycastAll(new PointerEventData(system) { position = point }, hits);
        Require(hits.Count > 0, "No UI raycast result at " + point);
        return hits[0].gameObject;
    }

    static void CheckTarget(EventSystem system, Vector2 point, GameObject expected, string label, Report report)
    {
        var hit = Hit(system, point);
        Require(hit == expected || hit.transform.IsChildOf(expected.transform),
            label + ": expected " + expected.name + ", hit " + hit.name + " at " + point
            + "; expected graphic=" + GraphicDetails(expected));
        report.raycastsChecked++;
        report.checks.Add(label + " at " + point + " -> " + hit.name);
    }

    static string GraphicDetails(GameObject obj)
    {
        var graphic = obj.GetComponent<Graphic>();
        return graphic == null ? "none" : $"depth={graphic.depth}, cull={graphic.canvasRenderer.cull}, active={graphic.isActiveAndEnabled}, raycast={graphic.raycastTarget}, canvas={graphic.canvas?.name}, order={graphic.canvas?.sortingOrder}, camera={graphic.canvas?.worldCamera?.name}, rect={graphic.rectTransform.rect}";
    }

    static void Dispatch(EventSystem system, Vector2 point)
    {
        var hit = Hit(system, point);
        var pointer = new PointerEventData(system) { position = point, button = PointerEventData.InputButton.Left };
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
    }

    static async UniTask WaitFor(Func<bool> predicate, string failure)
    {
        var deadline = EditorApplication.timeSinceStartup + 3;
        while (!predicate() && EditorApplication.timeSinceStartup < deadline) await UniTask.Yield();
        Require(predicate(), failure);
    }

    static FieldInfo FindField(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Private);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    static T Field<T>(object target, string name) => (T)FindField(target, name).GetValue(target);
    static void SetField(object target, string name, object value) => FindField(target, name).SetValue(target, value);
    static object Invoke(object target, string name) =>
        (target.GetType().GetMethod(name, Private) ?? target.GetType().BaseType.GetMethod(name, Private)).Invoke(target, null);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

// Only the battle-camera teardown is suppressed. All tested inherited methods are production code.
public sealed class PocketStrikerTutorialValidationLayer : FightingStepLayer
{
    void OnDisable() { }
    public override void OnDestroy() { }
}
