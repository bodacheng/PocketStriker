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

/// <summary>Renders real stage prefabs without loading accounts or entering combat.</summary>
public static class PocketStrikerStageCardValidation
{
    const string OutputDirectory = "Logs/UILayout";
    static readonly string[] Prefabs = { "StageButton", "EvolutionStageButton" };
    static readonly string[] Languages = { "English", "Japanese", "Chinese" };
    static readonly string[] ModeKeys = { "TeamModeM", "TeamModeR", "TeamModeE" };
    static readonly MethodInfo RefreshLayout = typeof(StageButton).GetMethod("LayoutAdventureCard", BindingFlags.NonPublic | BindingFlags.Instance);

    [Serializable]
    public class Report
    {
        public bool passed;
        public int casesChecked;
        public string unityVersion;
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public string screenshotScope = "Actual stage-card prefabs and localized text; numbered portrait placeholders exercise four enemy slots. No account or battle scene is loaded.";
    }

    [MenuItem("PocketStriker/Validation/Check Stage Cards")]
    public static void Validate()
    {
        var report = ValidateCards();
        var message = $"[StageCards] {(report.passed ? "PASS" : "FAIL")}: {report.casesChecked} cases, {report.screenshots.Count} renders. {Path.GetFullPath(OutputDirectory)}/stage-cards.json";
        if (report.passed) Debug.Log(message);
        else Debug.LogError(message + "\n" + string.Join("\n", report.errors.Take(12)));
    }

    public static Report ValidateCards()
    {
        var report = new Report { unityVersion = Application.unityVersion };
        Directory.CreateDirectory(OutputDirectory);
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Stage Card Validation");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var cameraObject = new GameObject("Validation Camera", typeof(Camera));
        cameraObject.transform.SetParent(rig.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.035f, 0.055f, 1);
        camera.orthographic = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100;
        var texture = new RenderTexture(395, 350, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        camera.targetTexture = texture;
        var canvasObject = new GameObject("Validation Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(rig.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        canvas.scaleFactor = 0.5f;
        canvas.pixelPerfect = true;
        var rows = CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv"));
        try
        {
            foreach (var prefab in Prefabs)
            foreach (var width in new[] { 600, 750 })
            foreach (var stage in new[] { 999, 9999 })
            foreach (var unlocked in new[] { true, false })
            for (var language = 0; language < Languages.Length; language++)
            for (var mode = 0; mode < ModeKeys.Length; mode++)
            {
                StageButton card = null;
                var description = $"{prefab}, width={width}, stage={stage}, unlocked={unlocked}, {Languages[language]}, {ModeKeys[mode]}";
                try
                {
                    card = CreateCard(canvas.transform, prefab, mode, language, rows, width, stage, unlocked, 0);
                    CheckCard(card);
                }
                catch (Exception exception) { report.errors.Add(description + ": " + exception.GetBaseException().Message); }
                finally { DestroyCard(card); }
                report.casesChecked++;
            }

            for (var language = 0; language < Languages.Length; language++)
            {
                var cards = new List<StageButton>();
                try
                {
                    for (var mode = 0; mode < ModeKeys.Length; mode++)
                        cards.Add(CreateCard(canvas.transform, Prefabs[mode == 2 ? 1 : 0], mode, language, rows,
                            750, mode == 2 ? 9999 : 999, mode != 2, 20 + mode * 220));
                    Canvas.ForceUpdateCanvases();
                    var path = Path.Combine(OutputDirectory, "stage-cards-" + Languages[language].ToLowerInvariant() + ".png");
                    Render(camera, texture, path);
                    report.screenshots.Add(path);
                }
                catch (Exception exception) { report.errors.Add("Render " + Languages[language] + ": " + exception.GetBaseException().Message); }
                finally { foreach (var card in cards) DestroyCard(card); }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        report.passed = report.errors.Count == 0 && report.casesChecked == 144 && report.screenshots.Count == 3;
        File.WriteAllText(Path.Combine(OutputDirectory, "stage-cards.json"), JsonUtility.ToJson(report, true));
        return report;
    }

    static StageButton CreateCard(Transform parent, string prefabName, int mode, int language,
        string[][] rows, int width, int stage, bool unlocked, float top)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/arcade/" + prefabName + ".prefab");
        var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
        var card = instance.GetComponent<StageButton>();
        var animator = instance.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        var rect = (RectTransform)instance.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -top);
        rect.sizeDelta = new Vector2(width, 200);
        card.StageNo = stage;
        card.SetFightMode(mode + 1);
        card.RewardUI.ShowRewards(50, 99);
        card.RewardUI.AwardRender(true);
        card.CriticalGaugeMode = mode == 0 ? CriticalGaugeMode.DoubleGain : mode == 1 ? CriticalGaugeMode.Unlimited : CriticalGaugeMode.Normal;
        card.ChangeColorOfIcons(unlocked);
        foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true))
        {
            var row = rows.FirstOrDefault(item => item.Length >= 4 && item[0] == converter.languageCode);
            var target = converter.target != null ? converter.target : converter.GetComponent<Text>();
            if (row != null && target != null) target.text = row[language + 1];
        }
        var icons = Field<RectTransform>(card, "iconsT");
        for (var index = 0; index < 4; index++)
        {
            var icon = new GameObject("Enemy " + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            icon.transform.SetParent(icons, false);
            ((RectTransform)icon.transform).sizeDelta = new Vector2(100, 100);
            icon.GetComponent<Image>().color = new Color(0.13f + index * 0.035f, 0.27f, 0.33f, 1);
            var label = new GameObject("Portrait Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(icon.transform, false);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.text = (index + 1).ToString();
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        }
        RefreshLayout.Invoke(card, null);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();
        return card;
    }

    static void CheckCard(StageButton card)
    {
        var root = (RectTransform)card.transform;
        var flag = Field<CanvasGroup>(card, "_modeFlag");
        var modeLabel = flag.GetComponentInChildren<Text>();
        var id = Field<Text>(card, "id");
        var icons = Field<RectTransform>(card, "iconsT");
        var layout = icons.GetComponent<HorizontalLayoutGroup>();
        Require(!layout.childControlWidth && !layout.childControlHeight, "Portrait layout overrides fitted icon sizes.");
        Require(flag.transform.parent == root && flag.alpha == 1, "Mode label has inherited or disabled transparency.");
        Require(flag.GetComponent<Image>().color.a == 1 && modeLabel.color == Color.white, "Mode label contrast was reduced.");
        Require(!modeLabel.resizeTextForBestFit && modeLabel.fontSize >= 32, "Mode label shrank below its readable size.");
        Require(modeLabel.preferredWidth <= modeLabel.rectTransform.rect.width + 0.5f, "Localized mode text overflows horizontally.");
        Require(modeLabel.preferredHeight <= modeLabel.rectTransform.rect.height + 0.5f, "Localized mode text is clipped vertically.");
        Require(!id.resizeTextForBestFit && id.fontSize >= 36, "Stage number becomes too small.");
        Require(id.preferredWidth <= id.rectTransform.rect.width + 0.5f
            && id.preferredHeight <= id.rectTransform.rect.height + 0.5f, "Stage number is clipped.");

        var modeBounds = Bounds(root, (RectTransform)flag.transform);
        var idBounds = Bounds(root, id.rectTransform);
        var rewards = card.RewardUI.GetComponentsInChildren<Graphic>().Where(graphic => graphic.gameObject.activeInHierarchy).ToArray();
        var portraits = icons.Cast<RectTransform>().Select(icon => Bounds(root, icon)).ToArray();
        foreach (var item in new[] { modeBounds, idBounds }.Concat(portraits).Concat(rewards.Select(graphic => Bounds(root, graphic.rectTransform))))
            Require(Contains(root.rect, item), "Card content escapes the card bounds.");
        Require(!modeBounds.Overlaps(idBounds), "Mode label overlaps the stage number.");
        foreach (var graphic in rewards)
        {
            var bounds = Bounds(root, graphic.rectTransform);
            Require(!bounds.Overlaps(modeBounds) && !bounds.Overlaps(idBounds), "Reward overlaps the mode label or stage number: " + graphic.name);
            foreach (var portrait in portraits) Require(!bounds.Overlaps(portrait), "Reward overlaps an enemy portrait: " + graphic.name);
        }
        for (var index = 0; index < portraits.Length; index++)
        {
            Require(!portraits[index].Overlaps(modeBounds) && !portraits[index].Overlaps(idBounds), "Enemy portrait overlaps stage metadata.");
            if (index > 0) Require(!portraits[index].Overlaps(portraits[index - 1]), "Enemy portraits overlap.");
        }
        foreach (var gauge in new[] { Field<GameObject>(card, "enemyDoubleExModeFlg"), Field<GameObject>(card, "enemyInfiniteExModeFlg") })
        {
            if (!gauge.activeInHierarchy) continue;
            var bounds = Bounds(root, (RectTransform)gauge.transform);
            Require(Contains(root.rect, bounds), "Critical gauge flag escapes the card.");
            Require(!bounds.Overlaps(modeBounds) && !bounds.Overlaps(idBounds), "Critical gauge flag overlaps stage metadata.");
            foreach (var portrait in portraits) Require(!bounds.Overlaps(portrait), "Critical gauge flag overlaps an enemy portrait.");
            foreach (var graphic in rewards) Require(!bounds.Overlaps(Bounds(root, graphic.rectTransform)), "Critical gauge flag overlaps a reward.");
            foreach (var label in gauge.GetComponentsInChildren<Text>())
                Require(label.preferredHeight <= label.rectTransform.rect.height + 0.5f, "Critical gauge text is clipped.");
        }
    }

    static T Field<T>(StageButton card, string name) where T : UnityEngine.Object =>
        (T)typeof(StageButton).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(card);

    static Rect Bounds(RectTransform root, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }

    static bool Contains(Rect outside, Rect inside) => inside.xMin >= outside.xMin - 0.5f && inside.yMin >= outside.yMin - 0.5f
        && inside.xMax <= outside.xMax + 0.5f && inside.yMax <= outside.yMax + 0.5f;

    static void Render(Camera camera, RenderTexture target, string path)
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
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void DestroyCard(StageButton card)
    {
        if (card == null) return;
        foreach (var converter in card.GetComponentsInChildren<LanguageConverter>(true))
            LanguageConverterManger.List.Remove(converter);
        UnityEngine.Object.DestroyImmediate(card.gameObject);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
