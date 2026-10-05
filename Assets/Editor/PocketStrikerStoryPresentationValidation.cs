using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Exercises the production story layout and tap sequence without account/provider calls.</summary>
public static class PocketStrikerStoryPresentationValidation
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string Output = "Logs/AIStory/Presentation";
    [Serializable] public sealed class Report
    {
        public bool passed;
        public string scope = "Production ArenaFightOver story construction, portrait geometry, localized hints, worst-case caption fit and actual button listeners in isolated preview scenes. The inactive probe explicitly invokes the production viewport callback. Screenshots use existing portrait artwork as an offline fixture; no AI provider is called.";
        public List<string> checks = new List<string>();
        public List<string> previews = new List<string>();
        public List<string> failures = new List<string>();
    }

    public static void ValidateBatch()
    {
        var report = Run();
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        Directory.CreateDirectory(Output);
        var report = new Report();
        var originalLanguage = AIStoryRuntimeContext.LanguageProvider;
        var originalController = global::FightScene.FightScene.target;
        global::FightScene.FightScene.target = null;
        try
        {
            foreach (var language in new[] { SystemLanguage.Chinese, SystemLanguage.Japanese, SystemLanguage.English })
                foreach (int height in new[] { 1920, 2340, 1440 })
                {
                    AIStoryRuntimeContext.LanguageProvider = () => language;
                    string name = language + "-1080x" + height;
                    try { CheckCase(language, height, report); report.checks.Add(name); }
                    catch (Exception error) { report.failures.Add(name + ": " + error); }
                }
        }
        finally
        {
            AIStoryRuntimeContext.LanguageProvider = originalLanguage;
            global::FightScene.FightScene.target = originalController;
        }
        report.passed = report.failures.Count == 0 && report.checks.Count == 9;
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        Debug.Log("[StoryPresentation] " + (report.passed ? "PASS" : "FAIL") + ": " + report.checks.Count + " cases");
        return report;
    }

    static void CheckCase(SystemLanguage language, int height, Report report)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        Sprite sprite = null;
        StoryInfo story = null;
        try
        {
            var canvasObject = new GameObject("Story validation canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var surface = (RectTransform)canvasObject.transform;
            surface.sizeDelta = new Vector2(1080, height);
            var layerObject = NewRect("Story validation layer", surface).gameObject;
            var layer = layerObject.AddComponent<ArenaFightOver>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/ArenaFightOver.prefab").GetComponent<ArenaFightOver>();
            var oldImage = NewRect("Authored image stand-in", layerObject.transform).gameObject.AddComponent<Image>();
            var oldCaption = NewRect("Authored caption stand-in", layerObject.transform).gameObject.AddComponent<Text>();
            oldCaption.font = Get<Text>(source, "shortStory").font;
            Set(layer, "storyBgImage", oldImage);
            Set(layer, "shortStory", oldCaption);
            Invoke(layer, "PrepareAIStoryPresentation");
            var presentation = Get<GameObject>(layer, "aiStoryPresentation");
            var picture = Get<Image>(layer, "storyBgImage");
            var caption = Get<Text>(layer, "shortStory");
            var panel = Get<GameObject>(layer, "aiStoryCaptionPanel");
            var button = Get<BOButton>(layer, "storyMaskBtn");
            var hint = presentation.transform.Find("Continue Hint").GetComponent<Text>();
            Require(presentation.transform.Find("Story Heading") == null, "A story heading remains.");
            Require(picture.rectTransform.rect.size == surface.rect.size && picture.preserveAspect, "Artwork must use the whole surface without distortion.");
            Require(!panel.activeSelf && picture.rectTransform.anchorMin == Vector2.zero && picture.rectTransform.anchorMax == Vector2.one,
                "The initial illustration reserves caption space.");
            string expectedHint = language == SystemLanguage.Chinese ? "轻触继续" : language == SystemLanguage.Japanese ? "タップして続ける" : "Tap to continue";
            Require(hint.text == expectedHint, "Continue hint lost localization.");
            var artwork = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AIStory/Art/PocketStrikerLogin.png");
            sprite = Sprite.Create(artwork, new Rect(0, 0, artwork.width, artwork.height), new Vector2(.5f, .5f));
            float fittedScale = Mathf.Min(1080f / artwork.width, (float)height / artwork.height);
            Require(artwork.width * artwork.height * fittedScale * fittedScale / (1080f * height) >= .74f,
                "The portrait picture occupies too little of the game surface.");
            string firstLine = language == SystemLanguage.Chinese ? "旅人穿过云间古桥，在山谷中发现了一座被遗忘的城堡。"
                : language == SystemLanguage.Japanese ? "旅人は雲の橋を渡り、谷の奥に忘れられた城を見つけた。"
                : "The traveler crossed a bridge among the clouds and found a forgotten castle in the valley.";
            story = ScriptableObject.CreateInstance<StoryInfo>();
            story.StoryScenes = new List<StoryInfo.StoryScene> {
                new StoryInfo.StoryScene { Pic = sprite, Title = "战斗余韵", Lines = new List<string> { firstLine, "第二行" } },
                new StoryInfo.StoryScene { Pic = sprite, Lines = new List<string> { "The end." } }
            };
            Set(layer, "activeAIStory", story);
            Set(layer, "aiStoryPlaying", true);
            Set(layer, "aiStorySceneIndex", 0);
            int completed = 0;
            Set(layer, "storyFinishedCallback", (Action)(() => completed++));
            button.SetListener(() => Invoke(layer, "AdvanceAIStory"));
            Invoke(layer, "DisplayAIStoryScene");
            Require(picture.sprite == sprite && !caption.gameObject.activeSelf && !panel.activeSelf, "Scene did not start with the illustration alone.");
            button.onClick.Invoke();
            Require(caption.text == firstLine && caption.gameObject.activeSelf && panel.activeSelf, "First tap did not reveal a readable caption.");
            Require(picture.rectTransform.rect.size == surface.rect.size, "Showing captions shrank the illustration.");
            Require(caption.rectTransform.rect.height < surface.rect.height * .185f, "A short caption obscures the full maximum caption area.");
            caption.text = new string('云', 200);
            Invoke(layer, "ResizeAIStoryCaption");
            var settings = caption.GetGenerationSettings(caption.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = caption.resizeTextMinSize;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float worstHeight = new TextGenerator().GetPreferredHeight(new string('云', 200), settings) / caption.pixelsPerUnit;
            Require(worstHeight <= caption.rectTransform.rect.height, "A maximum-length caption is clipped even at minimum font size.");
            Require(caption.rectTransform.anchorMin.y > hint.rectTransform.anchorMax.y, "Caption overlaps the continue hint.");
            caption.text = firstLine;
            Invoke(layer, "ResizeAIStoryCaption");
            float beforeResizeHeight = caption.rectTransform.rect.height;
            // Inactive probes do not receive Unity dimension messages, so invoke
            // the actual callback after changing the viewport geometry.
            var layerRect = (RectTransform)layer.transform;
            layerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 120);
            Invoke(layer, "OnRectTransformDimensionsChange");
            Require(picture.rectTransform.rect.height == height - 120 && Mathf.Abs(caption.rectTransform.rect.height - beforeResizeHeight) < 1,
                "Caption/artwork failed to adapt: viewport=" + layerRect.rect.size + ", illustration=" + picture.rectTransform.rect.size
                + ", caption height before=" + beforeResizeHeight + ", after=" + caption.rectTransform.rect.height + ".");
            layerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Invoke(layer, "OnRectTransformDimensionsChange");
            button.onClick.Invoke();
            Require(caption.text == "第二行", "Second tap skipped a line.");
            button.onClick.Invoke();
            Require(Get<int>(layer, "aiStorySceneIndex") == 1 && !caption.gameObject.activeSelf && !panel.activeSelf, "Next scene retained the old caption overlay.");
            button.onClick.Invoke(); button.onClick.Invoke(); button.onClick.Invoke();
            Require(completed == 1 && !Get<bool>(layer, "aiStoryPlaying") && !presentation.activeSelf,
                "Completing the story failed to resume the result exactly once.");

            if (language == SystemLanguage.Chinese && height != 1440)
            {
                // Remove the inactive game behaviour before activating graphics
                // for capture; its Awake/account/result flow is outside this probe.
                caption.text = firstLine;
                Invoke(layer, "ResizeAIStoryCaption");
                UnityEngine.Object.DestroyImmediate(layer);
                presentation.SetActive(true);
                picture.gameObject.SetActive(true);
                caption.gameObject.SetActive(true);
                panel.SetActive(true);
                canvasObject.SetActive(true);
                string path = Path.Combine(Output, "portrait-1080x" + height + ".png");
                Capture(scene, canvas, height, path);
                report.previews.Add(path);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (story != null) UnityEngine.Object.DestroyImmediate(story);
            if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
        }
    }

    static void Capture(Scene scene, Canvas canvas, int height, string path)
    {
        var cameraObject = new GameObject("Story preview camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene; camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = height * .5f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(1080, height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            target.Create(); camera.targetTexture = target;
            canvas.worldCamera = camera; canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 1; canvas.scaleFactor = 1;
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) { graphic.SetAllDirty(); graphic.Rebuild(CanvasUpdate.PreRender); }
            Canvas.ForceUpdateCanvases();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1080, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1080, height), 0, 0); image.Apply();
            Require(image.GetPixels32().Any(pixel => pixel.r != 0 || pixel.g != 0 || pixel.b != 0), "Story preview is empty.");
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    static T Get<T>(ArenaFightOver layer, string name) => (T)typeof(ArenaFightOver).GetField(name, Members).GetValue(layer);
    static void Set(ArenaFightOver layer, string name, object value) => typeof(ArenaFightOver).GetField(name, Members).SetValue(layer, value);
    static void Invoke(ArenaFightOver layer, string name) => typeof(ArenaFightOver).GetMethod(name, Members).Invoke(layer, null);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
