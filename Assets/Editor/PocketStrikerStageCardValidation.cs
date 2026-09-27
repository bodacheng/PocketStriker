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
    const string IconPrefabPath = "Assets/Resources/DummyLayerSystem/unit/UnitIconPrefab.prefab";
    static readonly CriticalGaugeMode[] GaugeModes = { CriticalGaugeMode.Normal, CriticalGaugeMode.DoubleGain, CriticalGaugeMode.Unlimited };
    static readonly Color[] PortraitColors = { new Color(1, 0.15f, 0.25f), new Color(1, 0.82f, 0.1f), new Color(0.15f, 0.85f, 0.4f), new Color(0.7f, 0.3f, 1) };
    static readonly MethodInfo RefreshLayout = typeof(StageButton).GetMethod("LayoutAdventureCard", BindingFlags.NonPublic | BindingFlags.Instance);

    [Serializable]
    public class Report
    {
        public bool passed;
        public int casesChecked;
        public string unityVersion;
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public string screenshotScope = "Actual stage-card and UnitIconPrefab instances with local character portraits and localized labels. Coverage includes both prefabs, widths 600/750, heights 200/240, 1/4 portraits, stage numbers 999/9999, locked/unlocked, three languages, all battle modes and independently all energy modes. Checks whole-card portrait centering, compact left-aligned rewards, and energy beside the battle mode. No account or battle scene is loaded.";
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
        var texture = new RenderTexture(790, 700, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        camera.targetTexture = texture;
        var canvasObject = new GameObject("Validation Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(rig.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        canvas.scaleFactor = 1;
        canvas.pixelPerfect = true;
        var rows = CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv"));
        var sourceFiles = Prefabs.Select(PrefabPath).Concat(new[] { IconPrefabPath })
            .ToDictionary(path => path, File.ReadAllText);
        try
        {
            foreach (var prefab in Prefabs)
            foreach (var width in new[] { 600, 750 })
            foreach (var height in new[] { 200, 240 })
            for (var language = 0; language < Languages.Length; language++)
            for (var mode = 0; mode < ModeKeys.Length; mode++)
            for (var profile = 0; profile < 2; profile++)
            {
                StageButton card = null;
                var stage = (mode + profile) % 2 == 0 ? 999 : 9999;
                var unlocked = (language + profile) % 2 == 0;
                var portraitCount = profile == 0 ? 1 : 4;
                var description = $"{prefab}, {width}x{height}, stage={stage}, unlocked={unlocked}, portraits={portraitCount}, {Languages[language]}, {ModeKeys[mode]}";
                try
                {
                    card = CreateCard(canvas.transform, prefab, mode, language, rows, width, height, stage, unlocked, portraitCount, 0);
                    if (profile == 1)
                    {
                        card.RewardUI.ShowRewards(500000, 999999);
                        UpdateCard(card, language, rows);
                    }
                    var baseline = StableBounds(card);
                    foreach (var gauge in GaugeModes)
                    {
                        try
                        {
                            card.CriticalGaugeMode = gauge;
                            UpdateCard(card, language, rows);
                            CheckCard(card, mode, language, rows, gauge, unlocked, portraitCount, profile == 1);
                            CheckStableBounds(baseline, StableBounds(card), "Energy mode moves other card content.");
                            card.ChangeColorOfIcons(!unlocked);
                            CheckLockState(card, !unlocked);
                            card.ChangeColorOfIcons(unlocked);
                            CheckLockState(card, unlocked);
                            CheckStableBounds(baseline, StableBounds(card), "Lock state moves card content.");
                        }
                        catch (Exception exception) { report.errors.Add(description + ", energy=" + gauge + ": " + exception.GetBaseException().Message); }
                        report.casesChecked++;
                    }
                }
                catch (Exception exception) { report.errors.Add(description + ": " + exception.GetBaseException().Message); }
                finally { DestroyCard(card); }
            }

            for (var language = 0; language < Languages.Length; language++)
                RenderSheet(canvas.transform, camera, texture, rows, language, false, report);

            texture.Release();
            texture.height = 820;
            texture.Create();
            RenderSheet(canvas.transform, camera, texture, rows, 1, true, report);
            foreach (var source in sourceFiles)
                if (File.ReadAllText(source.Key) != source.Value)
                    report.errors.Add("Validation modified a source prefab: " + source.Key);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        report.passed = report.errors.Count == 0 && report.casesChecked == 432 && report.screenshots.Count == 4;
        File.WriteAllText(Path.Combine(OutputDirectory, "stage-cards.json"), JsonUtility.ToJson(report, true));
        return report;
    }

    static string PrefabPath(string name) => "Assets/Resources/DummyLayerSystem/arcade/" + name + ".prefab";

    static StageButton CreateCard(Transform parent, string prefabName, int mode, int language,
        string[][] rows, int width, int height, int stage, bool unlocked, int portraitCount, float top)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefabName));
        Require(prefab != null, "Missing stage prefab: " + prefabName);
        var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
        try
        {
            var card = instance.GetComponent<StageButton>();
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(width, height);
            card.StageNo = stage;
            card.SetFightMode(mode + 1);
            card.RewardUI.ShowRewards(50, 99);
            card.RewardUI.AwardRender(!unlocked);
            card.CriticalGaugeMode = CriticalGaugeMode.Normal;
            card.ChangeColorOfIcons(unlocked);
            var icons = Field<RectTransform>(card, "iconsT");
            foreach (Transform child in icons) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var iconAsset = AssetDatabase.LoadAssetAtPath<GameObject>(IconPrefabPath);
            var iconPrefab = iconAsset != null ? iconAsset.GetComponent<HeroIcon>() : null;
            Require(iconPrefab != null, "Missing UnitIconPrefab.");
            for (var index = 0; index < portraitCount; index++)
            {
                var icon = UnityEngine.Object.Instantiate(iconPrefab, icons, false);
                icon.name = "Enemy " + (index + 1);
                foreach (var animator in icon.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var portraitPath = "Assets/OrganizedResources/InUse/ExternalAssets/Unit_Icon/" + (index == 3 ? 999 : index + 1) + ".png";
                var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
                Require(portrait != null, "Missing local portrait: " + portraitPath);
                var portraitImage = Field<Image>(icon, "icon");
                portraitImage.sprite = portrait;
                portraitImage.color = Color.white;
                portraitImage.gameObject.SetActive(true);
                Field<Image>(icon, "frame").color = PortraitColors[index];
                Field<Image>(icon, "iconBg").color = new Color(PortraitColors[index].r * 0.22f, PortraitColors[index].g * 0.22f, PortraitColors[index].b * 0.22f, 1);
                var curtain = Field<Image>(icon, "cooldownCurtain");
                if (curtain != null) curtain.fillAmount = 0;
                icon.WarnFlag.SetActive(false);
            }
            UpdateCard(card, language, rows);
            return card;
        }
        catch
        {
            DestroyCard(instance.GetComponent<StageButton>());
            throw;
        }
    }

    static void UpdateCard(StageButton card, int language, string[][] rows)
    {
        RefreshLayout.Invoke(card, null);
        foreach (var converter in card.GetComponentsInChildren<LanguageConverter>(true))
        {
            var row = rows.FirstOrDefault(item => item.Length >= 4 && item[0] == converter.languageCode);
            var target = converter.target != null ? converter.target : converter.GetComponent<Text>();
            Require(row != null, "Missing localization: " + converter.languageCode);
            if (target != null) target.text = row[language + 1];
        }
        var rect = (RectTransform)card.transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();
        // Match HeroIcon's runtime aspect fit after StageButton has fitted the icon roots.
        foreach (var hero in Field<RectTransform>(card, "iconsT").GetComponentsInChildren<HeroIcon>())
        {
            var portrait = Field<Image>(hero, "icon");
            var size = ((RectTransform)hero.transform).rect.size;
            var aspect = portrait.sprite.rect.width / portrait.sprite.rect.height;
            portrait.rectTransform.sizeDelta = aspect < 1 ? new Vector2(size.y * aspect, size.y) : new Vector2(size.x, size.x / aspect);
        }
    }

    static void RenderSheet(Transform parent, Camera camera, RenderTexture texture, string[][] rows,
        int language, bool edgeCases, Report report)
    {
        var cards = new List<StageButton>();
        try
        {
            for (var mode = 0; mode < ModeKeys.Length; mode++)
            {
                var card = CreateCard(parent, Prefabs[mode == 2 ? 1 : 0], mode, language, rows,
                    edgeCases ? 600 : 750, edgeCases ? 240 : 200,
                    mode == 2 ? 9999 : 999, mode != 2, edgeCases && mode == 1 ? 1 : 4,
                    20 + mode * (edgeCases ? 260 : 220));
                cards.Add(card);
                card.RewardUI.AwardRender(false);
                card.CriticalGaugeMode = GaugeModes[mode];
                UpdateCard(card, language, rows);
            }
            Canvas.ForceUpdateCanvases();
            var path = Path.Combine(OutputDirectory, "stage-cards-" + (edgeCases ? "narrow-tall" : Languages[language].ToLowerInvariant()) + ".png");
            Render(camera, texture, path);
            report.screenshots.Add(path);
        }
        catch (Exception exception) { report.errors.Add("Render " + Languages[language] + ": " + exception.GetBaseException().Message); }
        finally { foreach (var card in cards) DestroyCard(card); }
    }

    static void CheckCard(StageButton card, int mode, int language, string[][] rows,
        CriticalGaugeMode gaugeMode, bool unlocked, int portraitCount, bool largeRewards)
    {
        var root = (RectTransform)card.transform;
        var flag = Field<CanvasGroup>(card, "_modeFlag");
        Require(card.transform.Find("bg").GetSiblingIndex() == 0, "Card fill draws over the stage number or portraits.");
        var modeLabel = flag.GetComponentInChildren<Text>();
        var id = Field<Text>(card, "id");
        var icons = Field<RectTransform>(card, "iconsT");
        var layout = icons.GetComponent<HorizontalLayoutGroup>();
        Require(layout != null && !layout.childControlWidth && !layout.childControlHeight, "Portrait layout overrides fitted icon sizes.");
        Require(layout.childAlignment == TextAnchor.MiddleCenter, "Portrait group is not centered.");
        Require(flag.transform.parent == root && Mathf.Approximately(flag.alpha, 1), "Mode label inherits or changes transparency.");
        Require(!flag.blocksRaycasts, "Mode label intercepts card input.");
        Require(flag.GetComponent<Image>() == null || !flag.GetComponent<Image>().enabled, "Mode label still has a filled background.");
        Require(!flag.GetComponents<Outline>().Any(outline => outline.enabled), "Mode label still has a rectangular outline.");
        Require(!modeLabel.resizeTextForBestFit && modeLabel.fontSize >= 28, "Mode label shrank below its readable size.");
        Require(modeLabel.text == Translation(rows, ModeKeys[mode], language), "Incorrect localized battle mode.");
        CheckText(modeLabel, "Battle mode");
        Require(!id.resizeTextForBestFit && id.fontSize >= 32, "Stage number becomes too small.");
        Require(id.text == card.StageNo.ToString() && id.cachedTextGenerator.verts.Count > 0,
            "Stage number does not generate visible glyphs.");
        CheckText(id, "Stage number");
        CheckLockState(card, unlocked);

        var modeBounds = Bounds(root, (RectTransform)flag.transform);
        var idBounds = Bounds(root, id.rectTransform);
        var iconBounds = Bounds(root, icons);
        var rewardRoot = Bounds(root, (RectTransform)card.RewardUI.transform);
        var rewards = card.RewardUI.GetComponentsInChildren<Graphic>()
            .Where(graphic => graphic.enabled && graphic.gameObject.activeInHierarchy).ToArray();
        var portraits = icons.Cast<RectTransform>().Where(icon => icon.gameObject.activeSelf).Select(icon => Bounds(root, icon)).ToArray();
        Require(portraits.Length == portraitCount, "Unexpected portrait count.");
        var inset = Rect.MinMaxRect(root.rect.xMin + 27.5f, root.rect.yMin + 21.5f, root.rect.xMax - 27.5f, root.rect.yMax - 27.5f);
        foreach (var item in new[] { modeBounds, idBounds, rewardRoot }.Concat(portraits))
            Require(Contains(inset, item), "Card content loses its outer padding.");
        Require(!modeBounds.Overlaps(idBounds), "Mode label overlaps the stage number.");
        Require(Mathf.Abs(rewardRoot.height - 34) <= 0.5f && Mathf.Abs(rewardRoot.yMin - root.rect.yMin - 22) <= 0.5f,
            "Rewards are not aligned to the card bottom.");
        var compactRewardLimit = largeRewards ? 300 : unlocked ? 180 : 230;
        Require(Mathf.Abs(rewardRoot.xMin - root.rect.xMin - 28) <= 0.5f && rewardRoot.width <= compactRewardLimit + 0.5f,
            "Rewards are not tightly grouped at the left edge.");
        foreach (var graphic in rewards)
        {
            var bounds = Bounds(root, graphic.rectTransform);
            Require(Contains(root.rect, bounds), "Reward escapes card bounds: " + graphic.name);
            Require(!bounds.Overlaps(modeBounds) && !bounds.Overlaps(idBounds), "Reward overlaps stage metadata: " + graphic.name);
            Require(!graphic.raycastTarget, "Reward intercepts card input: " + graphic.name);
            foreach (var portrait in portraits) Require(!bounds.Overlaps(portrait), "Reward overlaps an enemy portrait: " + graphic.name);
            if (graphic is Text text) CheckText(text, "Reward");
        }
        var rewardLabels = rewards.OfType<Text>().Select(text => Bounds(root, text.rectTransform)).ToArray();
        Require(rewardLabels.Length == 2 && Mathf.Abs(rewardLabels[0].center.y - rewardLabels[1].center.y) < 0.5f,
            "Reward values are not arranged horizontally.");
        Require(!rewardLabels[0].Overlaps(rewardLabels[1]), "Reward values overlap.");
        for (var index = 0; index < portraits.Length; index++)
        {
            var portrait = portraits[index];
            Require(Contains(iconBounds, portrait), "Enemy portrait escapes its allotted region.");
            Require(!portrait.Overlaps(modeBounds) && !portrait.Overlaps(idBounds), "Enemy portrait overlaps stage metadata.");
            Require(Mathf.Abs(portrait.center.y - root.rect.center.y) < 0.5f,
                "Enemy portrait is not vertically centered in the whole card.");
            Require(Mathf.Abs(portrait.width - portrait.height) < 0.5f && portrait.width <= 100.5f, "Enemy portrait is stretched or oversized.");
            if (index == 0) continue;
            var gap = portrait.xMin - portraits[index - 1].xMax;
            Require(Mathf.Abs(gap - 14) < 0.5f, "Enemy portraits do not have equal 14-unit spacing.");
            Require(Mathf.Abs(portrait.width - portraits[0].width) < 0.5f, "Enemy portrait sizes differ.");
        }
        Require(Mathf.Abs((portraits[0].xMin + portraits[portraits.Length - 1].xMax) / 2 - iconBounds.center.x) < 0.5f,
            "Enemy portrait row is not horizontally centered.");
        Require(Mathf.Abs(iconBounds.center.y - root.rect.center.y) < 0.5f,
            "Enemy portrait region is not centered in the whole card.");
        Require(iconBounds.xMin >= root.rect.xMin + 312.5f && iconBounds.xMax <= root.rect.xMax - 27.5f,
            "Enemy portrait region overlaps the metadata column or outer padding.");

        var gauges = new[] { Field<GameObject>(card, "enemyDoubleExModeFlg"), Field<GameObject>(card, "enemyInfiniteExModeFlg") };
        Require(gauges[0].activeSelf == (gaugeMode == CriticalGaugeMode.DoubleGain)
            && gauges[1].activeSelf == (gaugeMode == CriticalGaugeMode.Unlimited), "Wrong energy flag is visible.");
        for (var index = 0; index < gauges.Length; index++)
        {
            var gauge = gauges[index];
            Require(gauge.GetComponent<Image>() == null || !gauge.GetComponent<Image>().enabled, "Energy label still has a hexagon background.");
            Require(!gauge.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.enabled && graphic.raycastTarget), "Energy label intercepts card input.");
            if (!gauge.activeInHierarchy) continue;
            var bounds = Bounds(root, (RectTransform)gauge.transform);
            Require(Contains(inset, bounds), "Energy label loses its outer padding.");
            Require(Mathf.Abs(bounds.center.y - modeBounds.center.y) < 0.5f && Mathf.Abs(bounds.height - modeBounds.height) < 0.5f,
                "Energy label is not aligned with the battle mode.");
            Require(Mathf.Abs(bounds.xMin - modeBounds.xMax - 8) < 0.5f && bounds.width <= 96.5f,
                "Energy label is not compactly placed beside the battle mode.");
            Require(!bounds.Overlaps(modeBounds) && !bounds.Overlaps(idBounds), "Energy label overlaps stage metadata.");
            foreach (var portrait in portraits) Require(!bounds.Overlaps(portrait), "Energy label overlaps an enemy portrait.");
            foreach (var graphic in rewards) Require(!bounds.Overlaps(Bounds(root, graphic.rectTransform)), "Energy label overlaps a reward.");
            var labels = gauge.GetComponentsInChildren<Text>();
            Require(labels.Length == 1, "Energy label is not a single compact line.");
            var expected = Translation(rows, index == 0 ? "StageEnergyDouble" : "StageEnergyUnlimited", language);
            Require(labels[0].text == expected && !labels[0].text.Contains("\n"), "Incorrect localized energy label.");
            CheckText(labels[0], "Energy label");
        }
        Require(!id.raycastTarget && !flag.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.enabled && graphic.raycastTarget),
            "Stage metadata intercepts card input.");
    }

    static void CheckLockState(StageButton card, bool unlocked)
    {
        Require(card.Button.interactable == unlocked, "Locked state no longer controls the stage button.");
        Require(Mathf.Abs(card.GetComponent<Image>().color.a - (unlocked ? 1 : 0.3f)) < 0.01f, "Locked card opacity changed unexpectedly.");
        Require(Mathf.Approximately(Field<CanvasGroup>(card, "_modeFlag").alpha, 1), "Locked battle-mode label is faded.");
    }

    static void CheckText(Text text, string description)
    {
        var width = text.preferredWidth;
        var height = text.preferredHeight;
        if (text.resizeTextForBestFit)
        {
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            using (var generator = new TextGenerator())
            {
                generator.Populate(text.text, settings);
                var fontSize = generator.fontSizeUsedForBestFit;
                Require(fontSize >= 16, description + " becomes too small.");
                settings.resizeTextForBestFit = false;
                settings.fontSize = fontSize;
                width = generator.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit;
                height = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
            }
        }
        Require(width <= text.rectTransform.rect.width + 0.5f, description + " overflows horizontally.");
        Require(height <= text.rectTransform.rect.height + 0.5f, description + " is clipped vertically.");
    }

    static string Translation(string[][] rows, string key, int language)
    {
        var row = rows.FirstOrDefault(item => item.Length >= 4 && item[0] == key);
        Require(row != null, "Missing localization: " + key);
        return row[language + 1];
    }

    static Rect[] StableBounds(StageButton card)
    {
        var root = (RectTransform)card.transform;
        return new[] { Field<Text>(card, "id").rectTransform, (RectTransform)Field<CanvasGroup>(card, "_modeFlag").transform,
                (RectTransform)card.RewardUI.transform, Field<RectTransform>(card, "iconsT") }
            .Concat(Field<RectTransform>(card, "iconsT").Cast<RectTransform>())
            .Select(rect => Bounds(root, rect)).ToArray();
    }

    static void CheckStableBounds(Rect[] before, Rect[] after, string message)
    {
        Require(before.Length == after.Length, message);
        for (var index = 0; index < before.Length; index++)
            Require(Vector2.Distance(before[index].position, after[index].position) < 0.5f
                && Vector2.Distance(before[index].size, after[index].size) < 0.5f, message);
    }

    static T Field<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

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
