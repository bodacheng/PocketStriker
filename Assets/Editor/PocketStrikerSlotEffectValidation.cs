using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks real particle frames against battle-cell geometry without entering Play mode.</summary>
public static class PocketStrikerSlotEffectValidation
{
    const string OutputDirectory = "Logs/UILayout/SlotEffects";
    static readonly string[] Effects = { "normal", "ex1", "ex2", "ex3" };
    static bool running;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public bool currentScenesUnchanged;
        public string unityVersion;
        public string utcTime;
        public string scope = "Actual SlotEffects prefabs and InBattleEvolution cell hierarchy/layout, in isolated preview scenes. Baked particle geometry and the outward half-brightness border of the actual texture tile are projected to screen pixels. Repeated refresh, cell movement, non-square resize and parent scaling are checked. Previews show all particle children; green outlines mark the actual UI cells.";
        public string limitation = "No account, battle simulation, skill stones or game UI behaviours are loaded; this does not exercise Addressables loading or live animations.";
        public List<CaseResult> cases = new List<CaseResult>();
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public sealed class CaseResult
    {
        public string device;
        public string effect;
        public string state;
        public Rect targetPixels;
        public Rect particleQuadPixels;
        public Rect textureBorderPixels;
        public Vector2 textureBorderFraction;
        public float centerErrorPixels;
        public float maximumEdgeErrorPixels;
        public bool passed;
    }

    sealed class Device
    {
        public string name;
        public int width, height, bottom, top;
        public Device(string name, int width, int height, int bottom = 0, int top = 0)
        { this.name = name; this.width = width; this.height = height; this.bottom = bottom; this.top = top; }
    }

    [MenuItem("PocketStriker/Validation/Check Slot Effects")]
    public static void Validate()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SlotEffects] Stop Play mode before running validation.");
            return;
        }
        running = true;
        try
        {
            var report = ValidateEffects();
            var message = $"[SlotEffects] {(report.passed ? "PASS" : "FAIL")}: {report.cases.Count} cases, {report.screenshots.Count} previews. {Path.GetFullPath(OutputDirectory)}/report.json";
            if (report.passed) Debug.Log(message);
            else Debug.LogError(message + "\n" + string.Join("\n", report.errors.Take(12)));
        }
        finally { running = false; }
    }

    public static Report ValidateEffects()
    {
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var dirty = scenes.Select(scene => scene.isDirty).ToArray();
        var active = SceneManager.GetActiveScene();
        var previousCanvas = PosCal.Canvas;
        var previousSafe = PosCal.SafeAreaRect;
        Directory.CreateDirectory(OutputDirectory);
        try
        {
            foreach (var device in new[] {
                new Device("540x960", 540, 960), new Device("1080x1920", 1080, 1920),
                new Device("1170x2532-safe", 1170, 2532, 102, 141), new Device("1536x2048-ipad", 1536, 2048)
            })
            {
                try { CheckDevice(device, report); }
                catch (Exception exception) { report.errors.Add(device.name + ": " + exception.GetBaseException().Message); }
            }
        }
        finally
        {
            PosCal.Canvas = previousCanvas;
            PosCal.SafeAreaRect = previousSafe;
            report.currentScenesUnchanged = SceneManager.GetActiveScene() == active && SceneManager.sceneCount == scenes.Length
                && scenes.Select((scene, index) => scene.IsValid() && scene.isLoaded && scene.isDirty == dirty[index]).All(value => value);
            if (!report.currentScenesUnchanged) report.errors.Add("The open scene state changed during validation.");
            report.passed = report.errors.Count == 0 && report.cases.Count == 64 && report.cases.All(item => item.passed)
                && report.screenshots.Count == 4;
            File.WriteAllText(Path.Combine(OutputDirectory, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckDevice(Device device, Report report)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Slot Effect Validation");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var texture = new RenderTexture(device.width, device.height, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        try
        {
            float safeHeight = device.height - device.bottom - device.top;
            float pixelScale = Mathf.Min(device.width / 1080f, safeHeight / 1920f);
            var canvas = NewRect("Canvas", rig.transform).gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(device.width, device.height) / pixelScale;
            canvasRect.localScale = Vector3.one * 0.001f;
            var uiCamera = NewCamera("UI Camera", rig.transform, scene, texture);
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = canvasRect.rect.height * 0.001f / 2;
            uiCamera.transform.position = new Vector3(0, 0, -10);
            uiCamera.cullingMask = 0;
            canvas.worldCamera = uiCamera;
            var effectCamera = NewCamera("FX Camera", rig.transform, scene, texture);
            effectCamera.orthographic = true;
            effectCamera.orthographicSize = 30;
            effectCamera.transform.position = new Vector3(0, 0, -10);
            var safe = NewRect("Safe Area", canvasRect);
            safe.anchorMin = new Vector2(0, device.bottom / (float)device.height);
            safe.anchorMax = new Vector2(1, (device.height - device.top) / (float)device.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.Canvas = canvas;
            PosCal.SafeAreaRect = safe;
            var cells = CreateBattleCells(safe);
            Require(cells.Length == 9, "Battle prefab did not provide nine cells.");
            var target = cells[4];
            foreach (var effectName in Effects)
            {
                var effect = CreateEffect(effectName, target, effectCamera);
                var layout = effect.GetComponent<SlotEffectLayout>();
                var grid = target.parent.GetComponent<GridLayoutGroup>();
                var originalSize = target.sizeDelta;
                var originalPosition = target.anchoredPosition;
                var originalPivot = target.pivot;
                var originalParentScale = target.parent.localScale;
                if (grid != null) grid.enabled = false;
                try
                {
                    CheckCase(device, effectName, "initial", effect, target, uiCamera, effectCamera, report);
                    for (int iteration = 0; iteration < 12; iteration++) layout.RefreshLayout();
                    CheckCase(device, effectName, "repeat-refresh", effect, target, uiCamera, effectCamera, report);
                    target.pivot = new Vector2(0.2f, 0.8f);
                    target.anchoredPosition += new Vector2(63, -47);
                    layout.RefreshLayout();
                    CheckCase(device, effectName, "moved-noncenter-pivot", effect, target, uiCamera, effectCamera, report);
                    target.sizeDelta = new Vector2(originalSize.x * 0.72f, originalSize.y * 1.18f);
                    target.parent.localScale = new Vector3(0.83f, 1.07f, 1);
                    for (int iteration = 0; iteration < 12; iteration++) layout.RefreshLayout();
                    CheckCase(device, effectName, "resized-parent-scaled", effect, target, uiCamera, effectCamera, report);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(effect.gameObject);
                    target.parent.localScale = originalParentScale;
                    target.pivot = originalPivot;
                    target.sizeDelta = originalSize;
                    target.anchoredPosition = originalPosition;
                    if (grid != null) grid.enabled = true;
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)target.parent);
                }
            }

            for (int index = 0; index < cells.Length; index++)
                CreateEffect(Effects[index % Effects.Length], cells[index], effectCamera);
            var path = Path.Combine(OutputDirectory, device.name + "-battle-grid.png");
            RenderPreview(effectCamera, texture, cells.Select(cell => ScreenBounds(cell, uiCamera)).ToArray(), path);
            report.screenshots.Add(path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static ParticleSystem CreateEffect(string name, RectTransform target, Camera camera)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/SlotEffects/" + name + ".prefab");
        Require(source != null, "Missing effect prefab: " + name);
        var instance = UnityEngine.Object.Instantiate(source, target, false);
        foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;
        var particles = instance.GetComponent<ParticleSystem>();
        var layout = instance.GetComponent<SlotEffectLayout>() ?? instance.AddComponent<SlotEffectLayout>();
        layout.Initialize(target, camera);
        foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            system.useAutoRandomSeed = false;
            system.randomSeed = 42;
        }
        particles.Simulate(0.5f, true, true, true);
        return particles;
    }

    static void CheckCase(Device device, string name, string state, ParticleSystem effect, RectTransform target,
        Camera uiCamera, Camera effectCamera, Report report)
    {
        var result = new CaseResult { device = device.name, effect = name, state = state, targetPixels = ScreenBounds(target, uiCamera) };
        report.cases.Add(result);
        var mesh = new Mesh();
        Texture2D readable = null;
        try
        {
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            // A paused edit-mode system retains its previously simulated scale until it updates.
            effect.Simulate(1f / 60f, true, false, false);
            renderer.BakeMesh(mesh, effectCamera, ParticleSystemBakeMeshOptions.BakeRotationAndScale);
            Require(mesh.vertexCount >= 4, "No particle quad was baked.");
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            // Bake rotation/scale explicitly; translation remains relative to the renderer.
            var world = vertices.Select(point => point + effect.transform.position).ToArray();
            result.particleQuadPixels = Bounds(world.Select(effectCamera.WorldToScreenPoint));
            var uvBounds = Bounds(uvs.Select(uv => (Vector3)uv));
            var sourceTexture = renderer.sharedMaterial.mainTexture;
            var assetPath = AssetDatabase.GetAssetPath(sourceTexture);
            readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Require(readable.LoadImage(File.ReadAllBytes(assetPath)), "Cannot decode the authored particle texture.");
            var borderUV = TextureBorder(readable, uvBounds);
            result.textureBorderFraction = new Vector2(borderUV.width / uvBounds.width, borderUV.height / uvBounds.height);
            var triangle = mesh.triangles;
            var a = triangle[0]; var b = triangle[1]; var c = triangle[2];
            Vector3 UVPoint(Vector2 uv)
            {
                var ab = uvs[b] - uvs[a]; var ac = uvs[c] - uvs[a]; var delta = uv - uvs[a];
                float determinant = ab.x * ac.y - ab.y * ac.x;
                Require(Mathf.Abs(determinant) > 0.000001f, "Degenerate particle UVs.");
                float u = (delta.x * ac.y - delta.y * ac.x) / determinant;
                float v = (ab.x * delta.y - ab.y * delta.x) / determinant;
                return effectCamera.WorldToScreenPoint(world[a] + u * (world[b] - world[a]) + v * (world[c] - world[a]));
            }
            result.textureBorderPixels = Bounds(new[] { UVPoint(borderUV.min), UVPoint(borderUV.max),
                UVPoint(new Vector2(borderUV.xMin, borderUV.yMax)), UVPoint(new Vector2(borderUV.xMax, borderUV.yMin)) });
            var expected = result.targetPixels; var actual = result.textureBorderPixels;
            result.centerErrorPixels = Vector2.Distance(expected.center, effectCamera.WorldToScreenPoint(effect.transform.position));
            result.maximumEdgeErrorPixels = Mathf.Max(Mathf.Abs(actual.xMin - expected.xMin), Mathf.Abs(actual.xMax - expected.xMax),
                Mathf.Abs(actual.yMin - expected.yMin), Mathf.Abs(actual.yMax - expected.yMax));
            float tolerance = Mathf.Max(3, Mathf.Max(expected.width, expected.height) * 0.055f);
            result.passed = result.centerErrorPixels <= 1 && result.maximumEdgeErrorPixels <= tolerance
                && effect.main.scalingMode == ParticleSystemScalingMode.Local && effect.main.simulationSpace == ParticleSystemSimulationSpace.Local;
            Require(result.passed, $"Border error {result.maximumEdgeErrorPixels:F2}px (limit {tolerance:F2}px), center error {result.centerErrorPixels:F2}px.");
        }
        catch (Exception exception) { report.errors.Add(device.name + "/" + name + "/" + state + ": " + exception.GetBaseException().Message); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    // Measure the outside of each luminous line, independently of the runtime calibration constants.
    static Rect TextureBorder(Texture2D texture, Rect tile)
    {
        int left = Mathf.RoundToInt(tile.xMin * texture.width), bottom = Mathf.RoundToInt(tile.yMin * texture.height);
        int width = Mathf.RoundToInt(tile.width * texture.width), height = Mathf.RoundToInt(tile.height * texture.height);
        Require(width > 8 && height > 8, "Unexpected flipbook tile size.");
        var pixels = texture.GetPixels(left, bottom, width, height);
        var columns = new float[width]; var rows = new float[height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var color = pixels[y * width + x];
            float brightness = Mathf.Max(color.r, color.g, color.b) * color.a;
            columns[x] += brightness; rows[y] += brightness;
        }
        int Peak(float[] values, int from, int to)
        {
            int index = from;
            for (int i = from + 1; i < to; i++) if (values[i] > values[index]) index = i;
            Require(values[index] > 0.01f, "Particle texture tile has no visible frame.");
            return index;
        }
        int x0 = Peak(columns, 0, width / 2), x1 = Peak(columns, width / 2, width);
        int y0 = Peak(rows, 0, height / 2), y1 = Peak(rows, height / 2, height);
        float OuterEdge(float[] values, int peak, int direction)
        {
            float threshold = values[peak] * 0.5f;
            int index = peak;
            while (index + direction >= 0 && index + direction < values.Length && values[index + direction] >= threshold)
                index += direction;
            int next = index + direction;
            if (next < 0 || next >= values.Length) return index;
            return index + direction * (values[index] - threshold) / (values[index] - values[next]);
        }
        return Rect.MinMaxRect((left + OuterEdge(columns, x0, -1) + 0.5f) / texture.width,
            (bottom + OuterEdge(rows, y0, -1) + 0.5f) / texture.height,
            (left + OuterEdge(columns, x1, 1) + 0.5f) / texture.width,
            (bottom + OuterEdge(rows, y1, 1) + 0.5f) / texture.height);
    }

    static RectTransform[] CreateBattleCells(RectTransform parent)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/InBattleEvolution.prefab");
        Require(source != null, "Missing InBattleEvolution prefab.");
        var copies = new Dictionary<Transform, Transform>();
        var root = (RectTransform)CopyHierarchy(source.transform, parent, copies);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        var sourceLayer = source.GetComponent<UILayer>();
        var layer = root.gameObject.AddComponent<UILayer>();
        var sourceFields = new SerializedObject(sourceLayer); var targetFields = new SerializedObject(layer);
        foreach (var name in new[] { "top", "middle", "bottom" })
            targetFields.FindProperty(name).objectReferenceValue = copies[(Transform)sourceFields.FindProperty(name).objectReferenceValue];
        targetFields.ApplyModifiedPropertiesWithoutUndo();
        foreach (var helper in source.GetComponentsInChildren<MidAreaSizeHelper>(true))
        {
            var clone = copies[helper.transform].gameObject.AddComponent<MidAreaSizeHelper>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(helper), clone);
            var fields = new SerializedObject(clone);
            var helperTarget = (Transform)new SerializedObject(helper).FindProperty("rectTransform").objectReferenceValue;
            fields.FindProperty("rectTransform").objectReferenceValue = helperTarget != null ? copies[helperTarget] : clone.transform;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
        Canvas.ForceUpdateCanvases();
        layer.ResizeAreas();
        var nine = source.GetComponentInChildren<NineForShow>(true);
        var grid = copies[nine.transform].GetComponent<GridLayoutGroup>();
        Require(grid != null, "Battle grid layout is missing.");
        float cellSize = ((RectTransform)grid.transform).rect.height / 3;
        grid.cellSize = new Vector2(cellSize, cellSize);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid.transform);
        Canvas.ForceUpdateCanvases();
        return new[] { nine.A1T, nine.A2T, nine.A3T, nine.B1T, nine.B2T, nine.B3T, nine.C1T, nine.C2T, nine.C3T }
            .Select(button => (RectTransform)copies[button.transform]).ToArray();
    }

    static Transform CopyHierarchy(Transform source, Transform parent, IDictionary<Transform, Transform> copies)
    {
        var target = source is RectTransform ? NewRect(source.name, parent) : new GameObject(source.name).transform;
        target.SetParent(parent, false);
        target.localPosition = source.localPosition; target.localRotation = source.localRotation; target.localScale = source.localScale;
        if (source is RectTransform rect && target is RectTransform copy)
        {
            copy.anchorMin = rect.anchorMin; copy.anchorMax = rect.anchorMax; copy.pivot = rect.pivot;
            copy.sizeDelta = rect.sizeDelta; copy.anchoredPosition3D = rect.anchoredPosition3D;
        }
        copies.Add(source, target);
        foreach (var component in source.GetComponents<Component>())
            if (component is GridLayoutGroup || component is HorizontalLayoutGroup || component is VerticalLayoutGroup
                || component is ContentSizeFitter || component is AspectRatioFitter || component is LayoutElement)
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(component), target.gameObject.AddComponent(component.GetType()));
        target.gameObject.SetActive(source.gameObject.activeSelf);
        foreach (Transform child in source) CopyHierarchy(child, target, copies);
        return target;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static Camera NewCamera(string name, Transform parent, Scene scene, RenderTexture texture)
    {
        var camera = new GameObject(name, typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(parent, false); camera.scene = scene; camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f, 0.035f, 0.05f, 1);
        camera.nearClipPlane = 0.01f; camera.farClipPlane = 100; camera.targetTexture = texture;
        return camera;
    }

    static Rect ScreenBounds(RectTransform rect, Camera camera)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Bounds(corners.Select(point => (Vector3)RectTransformUtility.WorldToScreenPoint(camera, point)));
    }

    static Rect Bounds(IEnumerable<Vector3> points)
    {
        var values = points.ToArray();
        return Rect.MinMaxRect(values.Min(point => point.x), values.Min(point => point.y), values.Max(point => point.x), values.Max(point => point.y));
    }

    static void RenderPreview(Camera camera, RenderTexture target, Rect[] cells, string path)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            // Thin green cell outlines are diagnostic overlays, not replacement effect artwork.
            foreach (var cell in cells)
            {
                int x0 = Mathf.Clamp(Mathf.RoundToInt(cell.xMin), 0, target.width - 1), x1 = Mathf.Clamp(Mathf.RoundToInt(cell.xMax), 0, target.width - 1);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(cell.yMin), 0, target.height - 1), y1 = Mathf.Clamp(Mathf.RoundToInt(cell.yMax), 0, target.height - 1);
                for (int x = x0; x <= x1; x++) { image.SetPixel(x, y0, Color.green); image.SetPixel(x, y1, Color.green); }
                for (int y = y0; y <= y1; y++) { image.SetPixel(x0, y, Color.green); image.SetPixel(x1, y, Color.green); }
            }
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
