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

/// <summary>Exercises the real arena reward page with local award rows; no account or network calls.</summary>
public static class PocketStrikerArenaAwardValidation
{
    const string OutputDirectory = "Logs/UILayout";

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string utcTime;
        public string unityVersion;
        public int casesChecked;
        public bool currentScenesUnchanged;
        public string scope = "Actual ArenaAwardLayer.SetUp and ArenaRewardItem.Set, with six and sixteen local rewards at four device sizes; repeated population, row geometry, scroll endpoints, fixed title bounds and title screenshot pixels are checked.";
        public List<CaseResult> cases = new List<CaseResult>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public sealed class CaseResult
    {
        public string device;
        public int rewards;
        public bool passed;
        public float viewportHeight;
        public float contentHeight;
        public bool repeatedSetupStable;
        public bool firstRewardReachable;
        public bool lastRewardReachable;
        public bool titlePositionStable;
        public Rect titleBoundsAtTop;
        public Rect titleBoundsAtBottom;
        public int topTitlePixels;
        public int bottomTitlePixels = -1;
        public bool titleScreenshotsVerified;
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
    }

    sealed class Device
    {
        public string name;
        public int width, height;
        public Rect safe;
        public Device(string name, int width, int height, int bottom = 0, int top = 0)
        {
            this.name = name; this.width = width; this.height = height;
            safe = new Rect(0, bottom, width, height - bottom - top);
        }
    }

    [MenuItem("PocketStriker/Validation/Arena Awards")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ArenaAwards] Stop Play mode before validating.");
            return;
        }
        var report = ValidateAwards();
        var summary = $"[ArenaAwards] {(report.passed ? "PASS" : "FAIL")}: {report.casesChecked}/8 populated-page cases; "
            + $"{report.errors.Count} errors. {Path.GetFullPath(OutputDirectory)}/arena-awards.json";
        if (report.passed) Debug.Log(summary);
        else Debug.LogError(summary + "\n" + string.Join("\n", report.errors.Take(10)));
    }

    public static Report ValidateAwards()
    {
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var dirty = scenes.Select(scene => scene.isDirty).ToArray();
        var activeScene = SceneManager.GetActiveScene();
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        Directory.CreateDirectory(OutputDirectory);
        var devices = new[] {
            new Device("540x960", 540, 960), new Device("375x667", 375, 667),
            new Device("390x844-notch", 390, 844, 34, 47), new Device("768x1024", 768, 1024)
        };
        try
        {
            foreach (var device in devices)
            foreach (var count in new[] { 6, 16 })
            {
                var result = new CaseResult { device = device.name, rewards = count };
                report.cases.Add(result);
                try { CheckCase(device, count, result); }
                catch (Exception exception) { result.errors.Add(exception.ToString()); }
                result.passed = result.errors.Count == 0;
                report.errors.AddRange(result.errors.Select(error => device.name + "/" + count + ": " + error));
                report.casesChecked++;
            }
        }
        finally
        {
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafe;
            if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
            report.currentScenesUnchanged = SceneManager.sceneCount == scenes.Length
                && scenes.Select((scene, index) => scene.IsValid() && scene.isDirty == dirty[index]).All(value => value);
            if (!report.currentScenesUnchanged) report.errors.Add("Open scenes or their dirty state changed.");
            report.passed = report.errors.Count == 0 && report.casesChecked == 8;
            File.WriteAllText(Path.Combine(OutputDirectory, "arena-awards.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckCase(Device device, int count, CaseResult result)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture texture = null;
        try
        {
            var rig = new GameObject("Arena Award Validation");
            SceneManager.MoveGameObjectToScene(rig, scene);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.065f, 0.09f, 1);
            texture = new RenderTexture(device.width, device.height, 24, RenderTextureFormat.ARGB32);
            texture.Create();
            camera.targetTexture = texture;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.scaleFactor = Mathf.Min(device.safe.width / 1080, device.safe.height / 1920);
            var safe = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvas.transform, false);
            safe.anchorMin = new Vector2(device.safe.xMin / device.width, device.safe.yMin / device.height);
            safe.anchorMax = new Vector2(device.safe.xMax / device.width, device.safe.yMax / device.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            PosCal.Canvas = canvas;
            PosCal.SafeAreaRect = safe;
            Canvas.ForceUpdateCanvases();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/ArenaAwardLayer.prefab");
            var layer = UnityEngine.Object.Instantiate(prefab, safe, false).GetComponent<ArenaAwardLayer>();
            var root = (RectTransform)layer.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var fields = new SerializedObject(layer);
            var content = (RectTransform)fields.FindProperty("itemsParent").objectReferenceValue;
            var scroll = (ScrollRect)fields.FindProperty("rewardScroll").objectReferenceValue;
            Require(scroll != null && scroll.content == content && scroll.viewport.GetComponent<RectMask2D>() != null,
                "Reward rows need a scroll viewport with RectMask2D.", result);
            var data = new Dictionary<string, Award>();
            for (var index = count - 1; index >= 0; index--)
                data.Add((index * 100).ToString(), new Award { d = 50 + index * 75 });
            layer.SetUp(data);
            Rebuild(root);
            var firstHeight = content.rect.height;
            layer.SetUp(data);
            Rebuild(root);
            foreach (var animator in layer.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var rows = content.GetComponentsInChildren<ArenaRewardItem>().ToArray();
            result.repeatedSetupStable = rows.Length == count && Mathf.Abs(content.rect.height - firstHeight) < 0.1f;
            Require(result.repeatedSetupStable, "Repeated SetUp duplicated rows or changed the content height.", result);
            result.viewportHeight = scroll.viewport.rect.height;
            result.contentHeight = content.rect.height;
            Require(content.rect.height >= count * 140 + (count - 1) * 16 - 1, "Content height does not include all fixed-height rows and gaps.", result);
            var viewportBounds = Bounds(scroll.viewport, root);
            var title = root.Find("intro") as RectTransform;
            var titleBounds = Bounds(title, root);
            Require(titleBounds.yMin >= viewportBounds.yMax + 20, "Title overlaps the reward viewport.", result);
            Require(viewportBounds.yMin >= root.rect.yMin + 240, "Reward viewport overlaps the shared bottom navigation.", result);
            for (var index = 0; index < rows.Length; index++)
            {
                var rect = (RectTransform)rows[index].transform;
                var bounds = Bounds(rect, content);
                Require((rect.localScale - Vector3.one).sqrMagnitude < 0.0001f, "Reward row inherited canvas world scale.", result);
                Require(bounds.height >= 139, "Reward row was compressed below its readable height.", result);
                if (index > 0)
                    Require(Bounds((RectTransform)rows[index - 1].transform, content).yMin >= bounds.yMax + 12,
                        "Adjacent reward rows overlap or have no separation.", result);
                var background = rect.Find("bg") as RectTransform;
                Require(Contains(bounds, Bounds(background, content)), "Reward background extends outside its allocated row.", result);
                foreach (var text in rows[index].GetComponentsInChildren<Text>())
                    Require(Contains(bounds, Bounds(text.rectTransform, content)), "Reward text extends outside its row: " + text.name, result);
                var point = new SerializedObject(rows[index]).FindProperty("arenaPoint").objectReferenceValue as Text;
                Require(point != null && point.text == (index * 100).ToString(), "Rewards are not ordered by rank threshold.", result);
            }
            scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            result.titleBoundsAtTop = Bounds(title, root);
            result.firstRewardReachable = Contains(Bounds(scroll.viewport, root), Bounds((RectTransform)rows[0].transform, root));
            Require(result.firstRewardReachable, "First reward is clipped at the top scroll endpoint.", result);
            result.topTitlePixels = Render(camera, texture, root, title, result, "top");
            result.titleScreenshotsVerified = result.topTitlePixels >= 20;
            Require(result.titleScreenshotsVerified, "Title has no visible pixels in the top screenshot.", result);
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            result.titleBoundsAtBottom = Bounds(title, root);
            result.titlePositionStable = (result.titleBoundsAtTop.position - result.titleBoundsAtBottom.position).sqrMagnitude < 0.01f
                && (result.titleBoundsAtTop.size - result.titleBoundsAtBottom.size).sqrMagnitude < 0.01f;
            Require(result.titlePositionStable, "Fixed title moves or resizes when the rewards scroll.", result);
            result.lastRewardReachable = Contains(Bounds(scroll.viewport, root), Bounds((RectTransform)rows[rows.Length - 1].transform, root));
            Require(result.lastRewardReachable, "Last reward cannot be reached without crossing the bottom navigation.", result);
            if (count == 16)
            {
                Require(content.rect.height > scroll.viewport.rect.height, "Overflow fixture did not exercise scrolling.", result);
                result.bottomTitlePixels = Render(camera, texture, root, title, result, "bottom");
                result.titleScreenshotsVerified &= result.bottomTitlePixels >= 20
                    && Mathf.Abs(result.bottomTitlePixels - result.topTitlePixels) <= result.topTitlePixels * 0.1f;
                Require(result.titleScreenshotsVerified, "Title pixels disappear or change substantially in the bottom screenshot.", result);
            }
        }
        finally
        {
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Rebuild(RectTransform root)
    {
        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true).Reverse())
            if (rect.GetComponent<LayoutGroup>() != null || rect.GetComponent<ContentSizeFitter>() != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();
    }

    static Rect Bounds(RectTransform rect, RectTransform relativeTo)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            var point = (Vector2)relativeTo.InverseTransformPoint(corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.xMax <= outer.xMax + 1
        && inner.yMin >= outer.yMin - 1 && inner.yMax <= outer.yMax + 1;
    static void Require(bool condition, string message, CaseResult result) { if (!condition) result.errors.Add(message); }

    static int Render(Camera camera, RenderTexture target, RectTransform root, RectTransform title, CaseResult result, string endpoint)
    {
        // A later legacy Text can rebuild a shared font atlas after the fixed title
        // was rendered. Flush all text twice before synchronous offscreen captures.
        var texts = root.GetComponentsInChildren<Text>();
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var text in texts) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            var path = Path.Combine(OutputDirectory, $"arena-awards-{result.device}-{result.rewards}-{endpoint}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            result.screenshots.Add(path);
            var corners = new Vector3[4];
            title.GetWorldCorners(corners);
            var min = camera.WorldToScreenPoint(corners[0]);
            var max = camera.WorldToScreenPoint(corners[2]);
            var pixels = image.GetPixels32();
            var visiblePixels = 0;
            for (var y = Mathf.Max(0, Mathf.CeilToInt(min.y)); y < Mathf.Min(target.height, Mathf.FloorToInt(max.y)); y++)
            for (var x = Mathf.Max(0, Mathf.CeilToInt(min.x)); x < Mathf.Min(target.width, Mathf.FloorToInt(max.x)); x++)
            {
                var pixel = pixels[y * target.width + x];
                // This isolated title region contains only white/grey glyphs over
                // the dark camera clear color; reward graphics start below it.
                if (pixel.r > 100 && pixel.g > 100 && pixel.b > 100) visiblePixels++;
            }
            return visiblePixels;
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
