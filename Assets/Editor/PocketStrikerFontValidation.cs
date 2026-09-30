using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks packaged font coverage instead of relying on the editor's OS font fallback.</summary>
public static class PocketStrikerFontValidation
{
    const string AvalonPath = "Assets/Fonts/Avalon-Bold.ttf";
    const string CjkPath = "Assets/Fonts/NotoSansCJKsc-Regular.otf";
    const string OutputPath = "Logs/Fonts/report.json";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string buildTarget;
        public int prefabsChecked;
        public int labelsChecked;
        public int localizedLabelsChecked;
        public int uniqueCharactersChecked;
        public int characterGeometryChecks;
        public int preparationHeaderCases;
        public List<string> errors = new List<string>();
        public string scope = "Bundled font data and explicit imported fallback references; all Resources prefabs including nested UI, current English/Japanese/Chinese localization and startup messages; actual generated glyph geometry; real preparation header layout at 1080x1920, 1080x2348 and 1440x1920. Network and gameplay initialization are omitted. Device rendering still needs an iOS smoke test.";
    }

    [MenuItem("PocketStriker/Validation/Check Bundled UI Fonts")]
    public static void Validate()
    {
        var report = ValidateFonts();
        var message = $"[Fonts] {(report.passed ? "PASS" : "FAIL")}: {report.labelsChecked} labels, {report.uniqueCharactersChecked} unique characters, {report.characterGeometryChecks} glyph meshes, {report.preparationHeaderCases} preparation headers. {Path.GetFullPath(OutputPath)}";
        if (report.passed) Debug.Log(message);
        else Debug.LogError(message + "\n" + string.Join("\n", report.errors.Take(20)));
    }

    public static void ValidateBatch()
    {
        Validate();
        var report = JsonUtility.FromJson<Report>(File.ReadAllText(OutputPath));
        if (!report.passed) throw new InvalidOperationException("Bundled UI font validation failed. See " + OutputPath);
    }

    public static Report ValidateFonts()
    {
        var report = new Report { unityVersion = Application.unityVersion, buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString() };
        var allRows = CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv"))
            .Skip(1).Where(row => row.Length >= 4).ToArray();
        // Translate.Find_RECORD_ID selects the first matching row. Preserve that
        // behavior for existing duplicate keys while auditing every row's glyphs.
        var rows = allRows.Where(row => !string.IsNullOrEmpty(row[0])).GroupBy(row => row[0]).ToDictionary(group => group.Key, group => group.First());
        var characters = new HashSet<char>(allRows.SelectMany(row => string.Concat(row.Skip(1).Take(3)))
            .Where(character => !char.IsWhiteSpace(character) && !char.IsControl(character)));
        // These messages appear before the remotely loaded translation table is available.
        characters.UnionWith("正在检测程序版本检查资源中正在下载资源正在初始化游戏数据游戏配置初始化失败，请重试。下载界面初始化失败启动资源准备失败游戏初始化失败游戏設定の初期化に失敗しました。再試行してください。ダウンロード画面リソース準備ゲームデータ初期化中Stage 1 · 轮换团战进化成就");
        characters.RemoveWhere(character => char.IsWhiteSpace(character) || char.IsControl(character));
        report.uniqueCharactersChecked = characters.Count;
        try
        {
            var cjk = AssetDatabase.LoadAssetAtPath<Font>(CjkPath);
            var avalon = AssetDatabase.LoadAssetAtPath<Font>(AvalonPath);
            Require(cjk != null && avalon != null, "Bundled UI fonts are missing.");
            CheckPackaging(cjk);
            CheckPackaging(avalon);
            Require(AssetDatabase.AssetPathToGUID(AvalonPath) == "96e17474f840a01459f0cc936c5d4d9b", "Avalon GUID changed, breaking existing UI references.");
            Require(((TrueTypeFontImporter)AssetImporter.GetAtPath(CjkPath)).fontNames.Contains("Noto Sans CJK SC"), "Noto's actual family name is missing from its importer.");
            foreach (var character in characters)
                Require(cjk.HasCharacter(character), "Bundled Noto font misses U+" + ((int)character).ToString("X4"));
            foreach (var font in new[] { cjk, avalon }) CheckCharacterGeometry(font, characters, report);

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.prefabsChecked++;
                foreach (var label in prefab.GetComponentsInChildren<Text>(true))
                {
                    report.labelsChecked++;
                    var context = path + ": " + HierarchyPath(label.transform);
                    try
                    {
                        Require(label.font != null, context + " has a missing font.");
                        CheckPackaging(label.font);
                        CheckTextGlyphs(label, label.text, context);
                    }
                    catch (Exception exception) { report.errors.Add(context + ": " + exception.Message); }
                }
                foreach (var converter in prefab.GetComponentsInChildren<LanguageConverter>(true))
                {
                    if (string.IsNullOrEmpty(converter.languageCode)) continue;
                    var label = converter.target != null ? converter.target : converter.GetComponent<Text>();
                    if (label == null) continue;
                    report.localizedLabelsChecked++;
                    var context = path + ": " + converter.languageCode;
                    try
                    {
                        Require(rows.TryGetValue(converter.languageCode, out var row), context + " has no localization row.");
                        foreach (var translation in row.Skip(1).Take(3)) CheckTextGlyphs(label, translation, context);
                    }
                    catch (Exception exception) { report.errors.Add(context + ": " + exception.Message); }
                }
            }
            CheckPreparationHeaders(report);
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        report.passed = report.errors.Count == 0;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, JsonUtility.ToJson(report, true));
        return report;
    }

    static void CheckPackaging(Font font)
    {
        var path = AssetDatabase.GetAssetPath(font);
        Require(!string.IsNullOrEmpty(path) && !path.Contains("/Editor/"), "Runtime UI references an editor-only or OS font: " + path);
        var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        Require(importer != null && importer.includeFontData, "Runtime font data is excluded: " + path);
        if (path == CjkPath) return;
        Require(path == AvalonPath, "Runtime UI uses an unvalidated font: " + path);
        Require(importer.fontReferences.Contains(AssetDatabase.LoadAssetAtPath<Font>(CjkPath)), "Avalon lacks the explicitly bundled Noto fallback.");
        Require(importer.fontNames.Contains("Avalon") && importer.fontNames.Contains("Noto Sans CJK SC"), "Avalon fallback family names do not match the font files.");
    }

    static void CheckCharacterGeometry(Font font, IEnumerable<char> characters, Report report)
    {
        using (var generator = new TextGenerator())
        {
            var settings = new TextGenerationSettings
            {
                font = font, fontSize = 36, fontStyle = FontStyle.Normal, color = Color.white,
                scaleFactor = 1, lineSpacing = 1, textAnchor = TextAnchor.MiddleCenter,
                generationExtents = new Vector2(256, 128), pivot = new Vector2(0.5f, 0.5f),
                horizontalOverflow = HorizontalWrapMode.Overflow, verticalOverflow = VerticalWrapMode.Overflow,
                updateBounds = true
            };
            foreach (var character in characters.OrderBy(character => character))
            {
                var value = character.ToString();
                font.RequestCharactersInTexture(value, settings.fontSize, settings.fontStyle);
                Require(generator.Populate(value, settings) && generator.characterCountVisible >= 1 && generator.verts.Count >= 4,
                    font.name + " generates no geometry for U+" + ((int)character).ToString("X4"));
                Require(font.GetCharacterInfo(character, out var glyph, settings.fontSize, settings.fontStyle)
                    && glyph.maxX > glyph.minX && glyph.maxY > glyph.minY,
                    font.name + " has an empty glyph for U+" + ((int)character).ToString("X4"));
                report.characterGeometryChecks++;
            }
        }
    }

    static void CheckTextGlyphs(Text label, string value, string context)
    {
        Require(label.font != null, context + " has no font.");
        foreach (var character in (value ?? string.Empty).Distinct().Where(character => !char.IsWhiteSpace(character)))
        {
            label.font.RequestCharactersInTexture(character.ToString(), Math.Max(1, label.fontSize), label.fontStyle);
            Require(label.font.GetCharacterInfo(character, out _, Math.Max(1, label.fontSize), label.fontStyle),
                context + " misses U+" + ((int)character).ToString("X4"));
        }
    }

    static void CheckPreparationHeaders(Report report)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Font Preparation Header", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(rig, scene);
        var canvas = rig.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var root = (RectTransform)rig.transform;
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        PosCal.Canvas = canvas;
        PosCal.SafeAreaRect = root;
        try
        {
            foreach (var size in new[] { new Vector2(1080, 1920), new Vector2(1080, 2348), new Vector2(1440, 1920) })
            {
                root.sizeDelta = size;
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/FightPrepareLayer.prefab"), rig.transform, false);
                try
                {
                    foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                    var rect = (RectTransform)instance.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    rect.localScale = Vector3.one;
                    var layer = instance.GetComponent<FightPrepareLayer>();
                    layer.ResizeAreas();
                    typeof(FightPrepareLayer).GetMethod("LayoutStageHeader", Private).Invoke(layer, null);
                    var label = (Text)typeof(FightPrepareLayer).GetField("arcadeStageNoText", Private).GetValue(layer);
                    foreach (var value in new[] { "Stage 1 · 轮换", "Stage 1 · 团战", "Stage 1 · 进化", "Stage 9999 · Rotation", "Stage 9999 · Team Battle", "Stage 9999 · 交代戦", "Stage 9999 · チーム戦" })
                    {
                        label.text = value;
                        Canvas.ForceUpdateCanvases();
                        CheckTextGlyphs(label, value, "Preparation header " + size);
                        var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                        using (var generator = new TextGenerator())
                        {
                            Require(generator.Populate(value, settings), "Preparation header generation failed: " + value);
                            Require(generator.characterCountVisible >= value.Count(character => !char.IsWhiteSpace(character)), "Preparation header drops characters: " + value);
                            Require(label.preferredWidth <= label.rectTransform.rect.width + 0.5f && label.preferredHeight <= label.rectTransform.rect.height + 0.5f,
                                "Preparation header clips at " + size + ": " + value);
                        }
                        report.preparationHeaderCases++;
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }
        finally
        {
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafe;
            UnityEngine.Object.DestroyImmediate(rig);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static string HierarchyPath(Transform target) => target.parent == null ? target.name : HierarchyPath(target.parent) + "/" + target.name;
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
