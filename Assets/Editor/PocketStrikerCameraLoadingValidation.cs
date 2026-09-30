using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModelView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Exercises real connector camera output and real loading-layer layout without game/network startup.</summary>
public static class PocketStrikerCameraLoadingValidation
{
    const string DirectoryPath = "Logs/UILayout/CameraLoading";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public int connectorRenders;
        public int loadingCases;
        public bool transparentBackground;
        public bool independentPreviews;
        public bool darkOverlayRegressionDetected;
        public bool textureCleanup;
        public bool textureCleanupUsesManualEditorLifecycle;
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public string scope = "Actual DedicatedCameraConnector prefab, transparent ARGB32 cameras and RawImage composition above dark overlay UI. Two copies of the bundled haruka 3D mesh use diagnostic unlit colors to test camera isolation and brightness; no sprite model stand-in. Disable/re-enable texture disposal is checked directly. In this stopped-editor preview scene, the non-ExecuteAlways connector's OnDestroy lifecycle is invoked explicitly before DestroyImmediate; this tests cleanup implementation, not automatic Play-mode callback dispatch. Actual UnitInstructionLayer and ProgressLayer runtime layout covers six device/safe-area shapes, all six authored tips and three languages. Gameplay components are removed from fixtures before activation; no Addressables, accounts, loaders or network startup is invoked.";
    }

    [MenuItem("PocketStriker/Validation/Camera Composition and Loading")]
    public static void Validate()
    {
        var report = ValidateCameraAndLoading();
        if (!report.passed) throw new InvalidOperationException("Camera/loading validation failed: " + string.Join("\n", report.errors));
        Debug.Log($"[CameraLoading] PASS: {report.connectorRenders} real camera renders, {report.loadingCases} localized loading cases. {DirectoryPath}/report.json");
    }

    public static Report ValidateCameraAndLoading()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before camera/loading validation.");
        Directory.CreateDirectory(DirectoryPath);
        var report = new Report();
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Camera Loading Validation");
        SceneManager.MoveGameObjectToScene(rig, scene);
        rig.SetActive(false);
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var oldPreScene = mainMenu.PreScene.target;
        mainMenu.PreScene.target = null;
        var materials = new List<Material>();
        try
        {
            var canvasObject = new GameObject("Composition Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 5;
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var root = (RectTransform)canvas.transform;
            root.sizeDelta = new Vector2(1080, 1920);
            PosCal.Canvas = canvas;
            var safeObject = new GameObject("Safe Area", typeof(RectTransform));
            safeObject.transform.SetParent(root, false);
            var safe = (RectTransform)safeObject.transform;
            safe.anchorMin = Vector2.zero;
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.SafeAreaRect = safe;
            var cameraObject = new GameObject("Composition Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 960;
            camera.aspect = 1080f / 1920;
            camera.transform.position = new Vector3(0, 0, -2000);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.12f, 0.17f, 1);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 4000;
            camera.cullingMask = (1 << 5) | 1;
            canvas.worldCamera = camera;
            var background = AddPanel(root, "PreparationBackdrop", new Color(0.025f, 0.045f, 0.065f, 0.58f));
            var preview = AddPanel(root, "PreparationPreview", new Color(0.045f, 0.075f, 0.105f, 0.72f));
            var left = CreateConnector(root, scene, new Rect(-460, -370, 440, 740), new Color(0.20f, 0.92f, 0.72f), materials);
            var right = CreateConnector(root, scene, new Rect(20, -370, 440, 740), new Color(0.98f, 0.28f, 0.35f), materials);
            rig.SetActive(true);
            Canvas.ForceUpdateCanvases();
            CheckConnector(left, true, report);
            CheckConnector(right, false, report);
            var leftImage = left.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Model UI Presentation");
            var rightImage = right.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Model UI Presentation");
            Require(leftImage.transform.GetSiblingIndex() == 0 && !leftImage.raycastTarget, "Model surface covers rotation/touch controls.");
            Require(leftImage.texture != rightImage.texture, "Two model connectors share their output texture.");
            var leftTexture = (RenderTexture)leftImage.texture;
            var rightTexture = (RenderTexture)rightImage.texture;
            var samplePoint = OpaqueSample(leftTexture);
            var composed = RenderToPixels(camera, 540, 960);
            float before = SampleComposition(composed, root, (RectTransform)left.transform, samplePoint).maxColorComponent;
            WritePng(composed, "real-connector-composition.png", report);
            UnityEngine.Object.DestroyImmediate(composed);
            Require(before > 0.65f, "Actual connector output is darkened by preparation overlay panels.");
            // Deliberately reproduce the old camera-below-panel composition. This
            // negative control proves the pixel assertion detects the screenshot bug.
            background.transform.SetAsLastSibling();
            preview.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            var broken = RenderToPixels(camera, 540, 960);
            float after = SampleComposition(broken, root, (RectTransform)left.transform, samplePoint).maxColorComponent;
            UnityEngine.Object.DestroyImmediate(broken);
            Require(after < before * 0.70f, "Pixel check did not detect the reproduced dark-overlay regression.");
            report.darkOverlayRegressionDetected = true;
            background.transform.SetAsFirstSibling();
            preview.transform.SetSiblingIndex(1);
            left.DisableUIPresentation();
            Require(leftTexture == null && leftImage.texture == null, "DisableUIPresentation did not dispose its render texture.");
            left.EnableUIPresentation();
            CheckConnector(left, true, report);
            var replacement = (RenderTexture)leftImage.texture;
            // Non-ExecuteAlways behaviours are not initialized as playing scripts
            // in a stopped-editor preview scene. Explicitly exercise their actual
            // cleanup callback here; Play mode tests own automatic dispatch.
            Invoke(left, "OnDestroy");
            Invoke(right, "OnDestroy");
            Require(replacement == null && rightTexture == null && leftImage.texture == null && rightImage.texture == null &&
                Field<Camera>(left, "camera").targetTexture == null && Field<Camera>(right, "camera").targetTexture == null,
                "Connector OnDestroy cleanup did not dispose and detach its render texture.");
            report.textureCleanupUsesManualEditorLifecycle = true;
            UnityEngine.Object.DestroyImmediate(left.gameObject);
            UnityEngine.Object.DestroyImmediate(right.gameObject);
            Require(left == null && right == null, "Preview connectors were not destroyed after cleanup.");
            report.textureCleanup = true;
            UnityEngine.Object.DestroyImmediate(background);
            UnityEngine.Object.DestroyImmediate(preview);
            CheckLoading(root, safe, camera, report);
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafe;
            mainMenu.PreScene.target = oldPreScene;
            foreach (var connector in rig.GetComponentsInChildren<DedicatedCameraConnector>(true)) Invoke(connector, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(rig);
            foreach (var material in materials) if (material != null) UnityEngine.Object.DestroyImmediate(material);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        report.passed = report.errors.Count == 0 && report.loadingCases == 108 && report.connectorRenders == 3;
        File.WriteAllText(Path.Combine(DirectoryPath, "report.json"), JsonUtility.ToJson(report, true));
        return report;
    }

    static DedicatedCameraConnector CreateConnector(RectTransform root, Scene scene, Rect bounds, Color color, List<Material> materials)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/P3/DedicatedCameraConnector.prefab");
        Require(prefab != null, "Missing connector prefab.");
        var instance = UnityEngine.Object.Instantiate(prefab, root, false);
        var connector = instance.GetComponent<DedicatedCameraConnector>();
        LoadingScreenLayout.Place((RectTransform)instance.transform, root, bounds);
        connector.EnableUIPresentation();
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/Unit/human/haruka.prefab");
        Require(modelPrefab != null, "Missing local haruka 3D mesh.");
        var model = UnityEngine.Object.Instantiate(modelPrefab, instance.transform, false);
        // Remove dependants before their required components; Unity rejects the
        // inverse order and would leave gameplay scripts in this mesh fixture.
        foreach (var centre in model.GetComponentsInChildren<Data_Center>(true)) UnityEngine.Object.DestroyImmediate(centre);
        foreach (var link in model.GetComponentsInChildren<OutsideDataLink>(true)) UnityEngine.Object.DestroyImmediate(link);
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
        foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/haruka/idle.anim");
        foreach (var animator in model.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
            if (idle != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) idle.SampleAnimation(animator.gameObject, 0);
        }
        foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 3;
        model.transform.position = Field<Vector3>(connector, "modelPos");
        model.transform.rotation = Quaternion.Euler(0, 160, 0);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);
        materials.Add(material);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            if (!(renderer is ParticleSystemRenderer)) renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        Invoke(connector, "Initialize", true, model.transform, instance.transform);
        Invoke(connector, "CameraPositionCal");
        var camera = Field<Camera>(connector, "camera");
        camera.scene = scene;
        camera.enabled = false;
        Field<Text>(connector, "unitName").gameObject.SetActive(false);
        return connector;
    }

    static void CheckConnector(DedicatedCameraConnector connector, bool teal, Report report)
    {
        Invoke(connector, "CameraPositionCal");
        var camera = Field<Camera>(connector, "camera");
        var target = camera.targetTexture;
        Require(target != null && target.format == RenderTextureFormat.ARGB32, "Connector is not rendering to transparent ARGB32 output.");
        RenderCamera(camera, target);
        var pixels = ReadPixels(target);
        var colors = pixels.GetPixels();
        var opaque = colors.Where(color => color.a > 0.90f).ToArray();
        Require(opaque.Length > 200, "Real connector camera renders no visible 3D model.");
        Require(colors[0].a < 0.02f && colors[colors.Length - 1].a < 0.02f, "Connector camera background is opaque.");
        int wrongColor = opaque.Count(color => teal ? color.r > color.g + 0.12f : color.g > color.r + 0.12f);
        Require(wrongColor == 0, "Connector includes pixels from the other preview's model.");
        WritePng(pixels, "connector-" + (teal ? "teal" : "coral") + "-" + report.connectorRenders + ".png", report);
        UnityEngine.Object.DestroyImmediate(pixels);
        report.transparentBackground = true;
        report.independentPreviews = true;
        report.connectorRenders++;
    }

    static void CheckLoading(RectTransform root, RectTransform safe, Camera camera, Report report)
    {
        var rows = CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv"))
            .Where(row => row.Length >= 4).GroupBy(row => row[0]).ToDictionary(group => group.Key, group => group.First());
        var instructionObject = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DummyLayerSystem/UnitInstructionLayer"), root, false);
        var progressObject = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DummyLayerSystem/ProgressLayer"), root, false);
        var instruction = instructionObject.GetComponent<UnitInstructionLayer>();
        var progress = progressObject.GetComponent<ProgressLayer>();
        foreach (var animator in instructionObject.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var animator in progressObject.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        Field<RawImage>(instruction, "bgImage").color = new Color(0.18f, 0.24f, 0.31f, 1);
        Field<Image>(progress, "bigCurtain").color = Color.clear;
        var slider = Field<Slider>(progress, "progressBar");
        slider.gameObject.SetActive(true);
        slider.value = 0.62f;
        Field<Text>(progress, "percentage").text = "62%";
        var shapes = new[] {
            new Vector4(1080, 1920, 0, 0), new Vector4(1080, 2340, 144, 90),
            new Vector4(1080, 1920, 112, 96), new Vector4(1440, 1920, 0, 0),
            new Vector4(1536, 2048, 48, 40), new Vector4(1080, 2560, 190, 110)
        };
        for (int shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
        {
            var shape = shapes[shapeIndex];
            root.sizeDelta = new Vector2(shape.x, shape.y);
            safe.offsetMin = new Vector2(0, shape.w);
            safe.offsetMax = new Vector2(0, -shape.z);
            foreach (var layer in new[] { instructionObject, progressObject })
            {
                var rect = (RectTransform)layer.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            Canvas.ForceUpdateCanvases();
            for (int language = 1; language <= 3; language++)
            for (int tip = 1; tip <= 6; tip++)
            {
                string context = $"device {shapeIndex}, language {language}, tip {tip}";
                try
                {
                    instruction.SetTip(rows["TipTitle" + tip][language], rows["GameTip" + tip][language]);
                    Field<Text>(progress, "info").text = language == 1 ? "Generating battlefield" : language == 2 ? "戦場生成中" : "战场生成中";
                    progress.RefreshLoadingLayout();
                    Canvas.ForceUpdateCanvases();
                    var title = Field<Text>(instruction, "gameTipTitle");
                    var body = Field<Text>(instruction, "gameTip");
                    var info = Field<Text>(progress, "info");
                    var percent = Field<Text>(progress, "percentage");
                    var safeBounds = LoadingScreenLayout.SafeBounds((RectTransform)instruction.transform);
                    var tipRects = new[] { title, body, info, percent }.Select(text => Bounds((RectTransform)instruction.transform, text.rectTransform)).ToArray();
                    var barBounds = Bounds((RectTransform)instruction.transform, (RectTransform)slider.transform);
                    foreach (var rect in tipRects.Concat(new[] { barBounds })) Require(Contains(safeBounds, rect), context + ": loading content escapes safe area.");
                    for (int index = 0; index < tipRects.Length; index++)
                    {
                        Require(!tipRects[index].Overlaps(barBounds), context + ": text overlaps progress bar.");
                        for (int previous = 0; previous < index; previous++) Require(!tipRects[index].Overlaps(tipRects[previous]), context + ": loading texts overlap.");
                    }
                    Require(LoadingScreenLayout.TextHeight(title, title.rectTransform.rect.width) <= title.rectTransform.rect.height + 0.5f, context + ": title is clipped.");
                    Require(LoadingScreenLayout.TextHeight(body, body.rectTransform.rect.width) <= body.rectTransform.rect.height + 0.5f, context + ": full tip copy is clipped.");
                    var before = tipRects.ToArray();
                    instruction.RefreshLoadingLayout();
                    progress.RefreshLoadingLayout();
                    Canvas.ForceUpdateCanvases();
                    var after = new[] { title, body, info, percent }.Select(text => Bounds((RectTransform)instruction.transform, text.rectTransform)).ToArray();
                    for (int index = 0; index < after.Length; index++) Require((after[index].position - before[index].position).sqrMagnitude < 0.01f, context + ": layout accumulates offsets.");
                    report.loadingCases++;
                    if (tip == 1 && language == 2)
                    {
                        camera.aspect = shape.x / shape.y;
                        camera.orthographicSize = shape.y * 0.5f;
                        var image = RenderToPixels(camera, Mathf.RoundToInt(shape.x * 0.5f), Mathf.RoundToInt(shape.y * 0.5f));
                        WritePng(image, "loading-japanese-combo-" + shapeIndex + ".png", report);
                        UnityEngine.Object.DestroyImmediate(image);
                    }
                }
                catch (Exception exception) { report.errors.Add(context + ": " + exception.Message); }
            }
        }
        UnityEngine.Object.DestroyImmediate(instructionObject);
        UnityEngine.Object.DestroyImmediate(progressObject);
    }

    static GameObject AddPanel(RectTransform parent, string name, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.layer = 5;
        panel.transform.SetParent(parent, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    static Vector2 OpaqueSample(RenderTexture texture)
    {
        var pixels = ReadPixels(texture);
        var colors = pixels.GetPixels();
        int index = -1;
        float nearest = float.MaxValue;
        for (int y = 3; y < texture.height - 3; y++)
        for (int x = 3; x < texture.width - 3; x++)
        {
            int candidate = y * texture.width + x;
            if (colors[candidate].a <= 0.99f || colors[candidate].maxColorComponent <= 0.75f) continue;
            if (colors[candidate - 3].a < 0.95f || colors[candidate + 3].a < 0.95f ||
                colors[candidate - 3 * texture.width].a < 0.95f || colors[candidate + 3 * texture.width].a < 0.95f) continue;
            float distance = (x - texture.width * 0.5f) * (x - texture.width * 0.5f) + (y - texture.height * 0.5f) * (y - texture.height * 0.5f);
            if (distance >= nearest) continue;
            nearest = distance;
            index = candidate;
        }
        UnityEngine.Object.DestroyImmediate(pixels);
        Require(index >= 0, "No bright opaque pixel to sample in real model output.");
        return new Vector2((index % texture.width + 0.5f) / texture.width, (index / texture.width + 0.5f) / texture.height);
    }

    static Color SampleComposition(Texture2D image, RectTransform root, RectTransform connector, Vector2 sample)
    {
        var bounds = Bounds(root, connector);
        var point = bounds.min + Vector2.Scale(bounds.size, sample);
        int x = Mathf.Clamp(Mathf.FloorToInt((point.x - root.rect.xMin) / root.rect.width * image.width), 0, image.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((point.y - root.rect.yMin) / root.rect.height * image.height), 0, image.height - 1);
        return image.GetPixel(x, y);
    }

    static Texture2D RenderToPixels(Camera camera, int width, int height)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        target.Create();
        var original = camera.targetTexture;
        try { camera.targetTexture = target; RenderCamera(camera, target); return ReadPixels(target); }
        finally { camera.targetTexture = original; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }

    static Texture2D ReadPixels(RenderTexture target)
    {
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            return image;
        }
        finally { RenderTexture.active = previous; }
    }

    static void RenderCamera(Camera camera, RenderTexture target)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
    }

    static void WritePng(Texture2D image, string name, Report report)
    {
        var path = Path.Combine(DirectoryPath, name);
        File.WriteAllBytes(path, image.EncodeToPNG());
        report.screenshots.Add(path);
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    static Rect Bounds(RectTransform root, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
