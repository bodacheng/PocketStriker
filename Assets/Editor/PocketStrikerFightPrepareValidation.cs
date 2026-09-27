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

/// <summary>Exercises the populated preparation header through its runtime entry points.</summary>
public static class PocketStrikerFightPrepareValidation
{
    const string OutputDirectory = "Logs/UILayout/FightPrepare";
    static readonly string[] Routes = { "Team", "Rotation", "Evolution", "Boss", "Gangbang", "ArenaReuse" };
    static readonly SystemLanguage[] Languages = { SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.Chinese };
    const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [Serializable]
    public class Report
    {
        public bool passed;
        public int casesChecked;
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public string scope = "Real preparation prefabs, runtime SetArcadeFeature/SetEventFeature/SetGangbangFeature/SetArenaFeature and SetFightMode; real reward rows with six-digit amounts and claimed marks. Models and live account data are not loaded.";
    }

    [MenuItem("PocketStriker/Validation/Check Fight Preparation")]
    public static void Validate()
    {
        var report = ValidatePreparation();
        var message = $"[FightPrepare] {(report.passed ? "PASS" : "FAIL")}: {report.casesChecked} cases, {report.screenshots.Count} renders. {Path.GetFullPath(OutputDirectory)}/report.json";
        if (report.passed) Debug.Log(message);
        else Debug.LogError(message + "\n" + string.Join("\n", report.errors.Take(12)));
    }

    public static Report ValidatePreparation()
    {
        var report = new Report();
        Directory.CreateDirectory(OutputDirectory);
        var oldAccount = PlayerAccountInfo.Me;
        var oldLanguage = AppSetting.Value.Language;
        var oldCanvas = PosCal.Canvas;
        var oldSafeArea = PosCal.SafeAreaRect;
        var languageRows = Translate.GetRowList();
        var oldRows = languageRows.ToArray();
        var languageProvider = typeof(Translate).GetField("languageProvider", PrivateStatic);
        var oldProvider = languageProvider.GetValue(null);
        var stageAwards = typeof(PlayFabReadClient).GetField("_stageAward", PrivateStatic);
        var gangAwards = typeof(PlayFabReadClient).GetField("_gangbangAward", PrivateStatic);
        var oldStageAwards = stageAwards.GetValue(null);
        var oldGangAwards = gangAwards.GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Fight Preparation Validation");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var cameraObject = new GameObject("Validation Camera", typeof(Camera));
        cameraObject.transform.SetParent(rig.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 960;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.04f, 0.07f, 1);
        var canvasObject = new GameObject("Validation Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(rig.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        var canvasRect = (RectTransform)canvas.transform;
        PosCal.Canvas = canvas;
        PosCal.SafeAreaRect = canvasRect;
        try
        {
            languageRows.Clear();
            foreach (var row in CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv")).Skip(1))
                if (row.Length >= 4) languageRows.Add(new Translate.Row { RECORD_ID = row[0], EN = row[1], JP = row[2], CH = row[3] });
            languageProvider.SetValue(null, (Func<SystemLanguage>)(() => AppSetting.Value.Language));
            var awards = new Dictionary<string, Award> { ["9999"] = new Award { d = 999999, g = 123456 } };
            stageAwards.SetValue(null, awards);
            gangAwards.SetValue(null, awards);
            foreach (var width in new[] { 1080, 1440 })
            foreach (var language in Languages)
            foreach (var claimed in new[] { false, true })
            foreach (var route in Routes)
            {
                GameObject instance = null;
                GangbangInfo gang = null;
                string description = $"{route}, {language}, width={width}, claimed={claimed}";
                try
                {
                    canvasRect.sizeDelta = new Vector2(width, 1920);
                    AppSetting.Value.Language = language;
                    PlayerAccountInfo.Me = new PlayerAccountInfo
                    {
                        tutorialProgress = "Finished", arcadeProcess = claimed ? 9999 : 9998, gangbangProcess = claimed ? 9999 : 9998
                    };
                    var path = route == "Gangbang" ? "Assets/Resources/DummyLayerSystem/FightPrepareLayer/FightPrepareLayer_gb.prefab"
                        : "Assets/Resources/DummyLayerSystem/FightPrepareLayer.prefab";
                    instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), canvas.transform, false);
                    foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var cameraInPrefab in instance.GetComponentsInChildren<Camera>(true)) cameraInPrefab.enabled = false;
                    var layer = instance.GetComponent<FightPrepareLayer>();
                    var root = (RectTransform)instance.transform;
                    root.anchorMin = Vector2.zero;
                    root.anchorMax = Vector2.one;
                    root.offsetMin = root.offsetMax = Vector2.zero;
                    root.localScale = Vector3.one;
                    layer.ResizeAreas();
                    foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) converter.Change();
                    var mode = route == "Team" || route == "Gangbang" ? 1 : route == "Evolution" ? 3 : route == "Rotation" ? 2 : 0;
                    if (route == "Gangbang")
                    {
                        gang = ScriptableObject.CreateInstance<GangbangInfo>();
                        layer.SetGangbangFeature(gang, () => { }, "9999", (a, b, c, d) => c, (a, b) => 1);
                        // Match the later ApplySelectedCountOption visual state
                        // without clicking a control that writes PlayerPrefs.
                        var selectedOption = Mathf.Clamp(PlayerPrefs.GetInt("gangbangCountOption", 1), 1, 3);
                        for (var option = 1; option <= 3; option++)
                        {
                            var frame = Field<GameObject>(layer, "countSelectedFrame" + option);
                            frame.SetActive(option == selectedOption);
                            var frameImage = frame.GetComponent<Image>();
                            Require(frameImage.sprite != null && frameImage.type == Image.Type.Sliced && !frameImage.fillCenter,
                                "Gangbang selected frame renders as a solid block behind its labels.");
                        }
                    }
                    else
                    {
                        // Boss and Arena start from a populated adventure instance
                        // to cover reuse without stale reward/header objects.
                        layer.SetArcadeFeature(() => { }, "9999", mode == 0 ? 1 : mode);
                        if (route == "Boss") layer.SetEventFeature("hard_fixture");
                        if (route == "ArenaReuse") layer.SetArenaFeature();
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/DummyLayerSystem/FightPrepareLayer/"
                            + (route == "Evolution" ? "EvolutionMode.anim" : "SwitchModeMode.anim"));
                        clip.SampleAnimation(instance, 0);
                    }
                    layer.SetFightMode(mode);
                    foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var layout in instance.GetComponentsInChildren<LayoutGroup>(true).Reverse())
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                    Canvas.ForceUpdateCanvases();
                    CheckHeader(layer, route);
                    if (width == 1080 && claimed)
                    {
                        var imagePath = Path.Combine(OutputDirectory, route.ToLowerInvariant() + "-" + language.ToString().ToLowerInvariant() + ".png");
                        Render(camera, imagePath);
                        report.screenshots.Add(imagePath);
                    }
                }
                catch (Exception exception) { report.errors.Add(description + ": " + exception.GetBaseException().Message); }
                finally
                {
                    if (instance != null)
                    {
                        foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) LanguageConverterManger.List.Remove(converter);
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                    if (gang != null) UnityEngine.Object.DestroyImmediate(gang);
                }
                report.casesChecked++;
            }
        }
        finally
        {
            PlayerAccountInfo.Me = oldAccount;
            AppSetting.Value.Language = oldLanguage;
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafeArea;
            languageRows.Clear();
            languageRows.AddRange(oldRows);
            languageProvider.SetValue(null, oldProvider);
            stageAwards.SetValue(null, oldStageAwards);
            gangAwards.SetValue(null, oldGangAwards);
            UnityEngine.Object.DestroyImmediate(rig);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        report.passed = report.errors.Count == 0 && report.casesChecked == 72 && report.screenshots.Count == 18;
        File.WriteAllText(Path.Combine(OutputDirectory, "report.json"), JsonUtility.ToJson(report, true));
        return report;
    }

    static void CheckHeader(FightPrepareLayer layer, string route)
    {
        var title = Field<Text>(layer, "arcadeStageNoText");
        var reward = Field<RewardUI>(layer, "rewardUI");
        var back = Field<BOButton>(layer, "toArcadeFrontBtn");
        bool hidden = route == "ArenaReuse";
        Require(title.gameObject.activeSelf != hidden, "Incorrect title visibility after reuse.");
        Require(reward.gameObject.activeSelf == (route != "Boss" && !hidden), "Incorrect reward visibility after reuse.");
        if (hidden) return;
        var parent = (RectTransform)title.transform.parent;
        Require(reward.transform.parent == parent && back.transform.parent == parent, "Reward or return button is still attached to title text.");
        var titleBounds = Bounds(parent, title.rectTransform);
        Require(Contains(parent.rect, titleBounds), "Title escapes its UI region.");
        Require(title.fontSize >= 36 && !title.resizeTextForBestFit, "Title was shrunk.");
        Require(title.preferredWidth <= title.rectTransform.rect.width + 0.5f
            && title.preferredHeight <= title.rectTransform.rect.height + 0.5f, "Populated stage/battle-type title is clipped.");
        if (back.gameObject.activeInHierarchy)
            Require(!titleBounds.Overlaps(Bounds(parent, (RectTransform)back.transform)), "Return button overlaps title.");
        if (reward.gameObject.activeInHierarchy)
        {
            var rewardBounds = Bounds(parent, (RectTransform)reward.transform);
            Require(Contains(parent.rect, rewardBounds) && !rewardBounds.Overlaps(titleBounds), "Reward column overlaps title or escapes region.");
            var graphics = reward.GetComponentsInChildren<Graphic>().Where(graphic => graphic.gameObject.activeInHierarchy).ToArray();
            for (var index = 0; index < graphics.Length; index++)
            {
                var bounds = Bounds(parent, graphics[index].rectTransform);
                Require(Contains(rewardBounds, bounds) && !bounds.Overlaps(titleBounds), "Reward graphic escapes its column: " + graphics[index].name);
                if (graphics[index] is Text count)
                    Require(count.preferredWidth <= count.rectTransform.rect.width + 0.5f && count.preferredHeight <= count.rectTransform.rect.height + 0.5f,
                        "Six-digit reward amount is clipped.");
                for (var previous = 0; previous < index; previous++)
                    Require(!bounds.Overlaps(Bounds(parent, graphics[previous].rectTransform)), "Reward amount, currency icon, or claimed mark overlap.");
            }
        }
        var modes = new[] { "modeFlgR", "modeFlgM", "modeFlgE", "modeFlgG" }
            .Select(name => Field<GameObject>(layer, name)).Where(flag => flag != null).ToList();
        var modeSwitch = Field<FightModeSwitch>(layer, "fightModeSwitch");
        modes.Add(modeSwitch.gameObject);
        if (route == "Gangbang")
            Require(!modeSwitch.gameObject.activeSelf && layer.GetSetFightMode() == TeamMode.MultiRaid,
                "Fixed Gangbang mode must stay configured while its redundant selector remains hidden.");
        foreach (var mode in modes.Where(flag => flag.activeInHierarchy))
        {
            var bounds = Bounds(parent, (RectTransform)mode.transform);
            Require(!bounds.Overlaps(titleBounds), "Runtime battle-mode badge overlaps stage title.");
            if (reward.gameObject.activeInHierarchy)
                Require(!bounds.Overlaps(Bounds(parent, (RectTransform)reward.transform)), "Runtime battle-mode badge overlaps rewards.");
            if (route == "Gangbang")
                for (var option = 1; option <= 3; option++)
                {
                    var countButton = Field<BOButton>(layer, "countSet" + option);
                    if (countButton.gameObject.activeInHierarchy)
                        Require(!bounds.Overlaps(Bounds(parent, (RectTransform)countButton.transform)), "Battle-mode badge overlaps a Gangbang count option.");
                }
        }
    }

    static T Field<T>(FightPrepareLayer layer, string name) where T : UnityEngine.Object =>
        (T)typeof(FightPrepareLayer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer);

    static Rect Bounds(RectTransform root, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }

    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 0.5f && inner.yMin >= outer.yMin - 0.5f
        && inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;

    static void Render(Camera camera, string path)
    {
        var target = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(540, 960, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            target.Create();
            camera.targetTexture = target;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
