using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Renders populated preparation prefabs with local art through their runtime entry points.</summary>
public static class PocketStrikerFightPrepareValidation
{
    const string OutputDirectory = "Logs/UILayout/FightPrepare";
    const string IconPrefabPath = "Assets/Resources/DummyLayerSystem/unit/UnitIconPrefab.prefab";
    const string StonePrefabPath = "Assets/Resources/BasicSprites/stoneModel.prefab";
    const string PortraitDirectory = "Assets/OrganizedResources/InUse/ExternalAssets/Unit_Icon/";
    const string SkillDirectory = "Assets/OrganizedResources/InUse/ExternalAssets/SkillIcon/";
    const string BackgroundGuid = "eeefd2f03ff5f4541bbbef04d1738e92";
    static readonly string[] Routes = { "Team", "Rotation", "Evolution", "Boss", "Gangbang", "ArenaReuse" };
    static readonly SystemLanguage[] Languages = { SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.Chinese };
    const int PreparationButtonCasesPerLanguage = 11;
    const int ExpectedResultButtons = 2;
    static readonly int[] LockedResultModes = { 0, 1, 2, 3, 4, -1 };
    const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    static readonly Viewport[] Viewports =
    {
        new Viewport("phone", 1080, 1920, 0, 0),
        new Viewport("notched-phone", 1080, 2340, 80, 64),
        new Viewport("tablet", 1440, 1920, 0, 0),
        new Viewport("notched-tablet", 1536, 2048, 48, 40)
    };

    sealed class Viewport
    {
        public readonly string name;
        public readonly int width, height, topInset, bottomInset;
        public Viewport(string name, int width, int height, int topInset, int bottomInset)
        { this.name = name; this.width = width; this.height = height; this.topInset = topInset; this.bottomInset = bottomInset; }
    }

    [Serializable]
    public class Report
    {
        public bool passed;
        public int casesChecked;
        public int localizedSkillsChecked;
        public int skillDetailCasesChecked;
        public int buttonStatesChecked;
        public int expectedButtonStates = Languages.Length
            * (PreparationButtonCasesPerLanguage + ExpectedResultButtons * LockedResultModes.Length);
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public string scope = "Real preparation prefabs, HeroIcon and stoneModel prefabs with local portrait/gem art, actual menu background, and static localized LowerMainBar/ReturnLayer prefabs inside the safe area. Runtime SetArcadeFeature/SetEventFeature/SetGangbangFeature/SetArenaFeature and SetFightMode cover three languages, large rewards, claim states, four phone/tablet and safe-area shapes, repeated layout stability, cached mode-animation position correction, and hint visibility with Arena mottos/hidden Edit. Shared sliced button textures, rectangular hit targets, disabled/enabled/guided Fight, Edit callback, locked encounter modes, practice mode switching and saved preferences are checked through Unity's Button.Press gate. Both actual result prefab buttons hide all alternate modes, and direct hidden UnityEvent invocation cannot trigger those actions. All 96 authored skills have exact localized names/intros and populated metadata in three languages. Real slot callbacks open the detail card; every skill's title/metadata and scrollable introduction are checked on four device shapes. Preview, close, scrim, route reuse and character selection clear stale details; renderer work is blocked and cancelled locally during selection validation. Stage 55 Evolution fixture uses four enemies and one hero, plus a local character render sampled from its idle clip; the screenshot's remotely supplied skeletal enemy is not available locally. No network, Addressables, live account, or combat initialization is invoked.";
    }

    [MenuItem("PocketStriker/Validation/Check Fight Preparation")]
    public static void Validate()
    {
        var report = ValidatePreparation();
        var message = $"[FightPrepare] {(report.passed ? "PASS" : "FAIL")}: {report.casesChecked} cases, {report.screenshots.Count} renders. {Path.GetFullPath(OutputDirectory)}/report.json";
        if (report.passed) Debug.Log(message);
        else Debug.LogError(message + "\n" + string.Join("\n", report.errors.Take(12)));
    }

    public static void ValidateBatch()
    {
        try
        {
            var report = ValidatePreparation();
            Debug.Log($"[FightPrepare] {(report.passed ? "PASS" : "FAIL")}: {report.casesChecked} cases, {report.screenshots.Count} renders. {Path.GetFullPath(OutputDirectory)}/report.json");
            if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ValidateDesignBatch()
    {
        try
        {
            var preparation = ValidatePreparation();
            var fonts = PocketStrikerFontValidation.ValidateFonts();
            PocketStrikerUILayoutValidation.Validate();
            var layout = JsonUtility.FromJson<PocketStrikerUILayoutValidation.Report>(File.ReadAllText("Logs/UILayout/report.json"));
            var cards = PocketStrikerStageCardValidation.ValidateCards();
            bool passed = preparation.passed && fonts.passed && layout.passed && cards.passed;
            var summary = $"[Preparation Design] {(passed ? "PASS" : "FAIL")}\n"
                + $"Preparation: {preparation.passed}, {preparation.casesChecked} cases, {preparation.screenshots.Count} renders, {preparation.localizedSkillsChecked} localized skills, {preparation.skillDetailCasesChecked} detail viewports, {preparation.buttonStatesChecked} button states\n"
                + $"Fonts: {fonts.passed}, {fonts.labelsChecked} labels, {fonts.characterGeometryChecks} glyph checks\n"
                + $"Global UI: {layout.passed}, {layout.casesChecked}/{layout.expectedCases} cases, {layout.errors} errors, {layout.warnings} warnings\n"
                + $"Stage cards: {cards.passed}, {cards.casesChecked} cases, {cards.screenshots.Count} renders\n"
                + "Reports: Logs/UILayout/FightPrepare/report.json, Logs/Fonts/report.json, Logs/UILayout/report.json, Logs/UILayout/stage-cards.json";
            File.WriteAllText(Path.Combine(OutputDirectory, "design-summary.txt"), summary);
            if (passed) Debug.Log(summary);
            else Debug.LogError(summary + "\n" + string.Join("\n", preparation.errors.Concat(fonts.errors).Concat(layout.failures).Concat(cards.errors)));
            EditorApplication.Exit(passed ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static Report ValidatePreparation()
    {
        var report = new Report();
        Directory.CreateDirectory(OutputDirectory);
        var oldAccount = PlayerAccountInfo.Me;
        var oldLanguage = AppSetting.Value.Language;
        var oldLimits = new[] { CommonSetting.GangbangModeMaxUnitPerTeam1, CommonSetting.GangbangModeMaxUnitPerTeam2, CommonSetting.GangbangModeMaxUnitPerTeam3 };
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
        var skills = new LocalSkillFixture();
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
        var safeObject = new GameObject("Validation Safe Area", typeof(RectTransform));
        safeObject.transform.SetParent(canvas.transform, false);
        var safeRect = (RectTransform)safeObject.transform;
        safeRect.anchorMin = Vector2.zero;
        safeRect.anchorMax = Vector2.one;
        PosCal.Canvas = canvas;
        PosCal.SafeAreaRect = safeRect;
        Sprite modelSprite = null;
        Texture2D modelTexture = null;
        var sourcePaths = new[] { "Assets/Resources/DummyLayerSystem/FightPrepareLayer.prefab",
            "Assets/Resources/DummyLayerSystem/FightPrepareLayer/FightPrepareLayer_gb.prefab", IconPrefabPath, StonePrefabPath,
            "Assets/Resources/DummyLayerSystem/LowerMainBar.prefab", "Assets/Resources/DummyLayerSystem/ReturnLayer.prefab" };
        var sourceFiles = sourcePaths.ToDictionary(path => path, File.ReadAllText);
        try
        {
            languageRows.Clear();
            foreach (var row in CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv")).Skip(1))
                if (row.Length >= 4) languageRows.Add(new Translate.Row { RECORD_ID = row[0], EN = row[1], JP = row[2], CH = row[3] });
            languageProvider.SetValue(null, (Func<SystemLanguage>)(() => AppSetting.Value.Language));
            skills.Load();
            var common = new SerializedObject(AssetDatabase.LoadAssetAtPath<CommonSetting>("Assets/Setting/CommonSetting.asset"));
            CommonSetting.GangbangModeMaxUnitPerTeam1 = common.FindProperty("gangbangModeMaxUnitPerTeam1").intValue;
            CommonSetting.GangbangModeMaxUnitPerTeam2 = common.FindProperty("gangbangModeMaxUnitPerTeam2").intValue;
            CommonSetting.GangbangModeMaxUnitPerTeam3 = common.FindProperty("gangbangModeMaxUnitPerTeam3").intValue;
            CheckLocalizedSkills(skills, report);
            var awards = new Dictionary<string, Award> { ["9999"] = new Award { d = 999999, g = 123456 }, ["55"] = new Award { d = 10, g = 10 } };
            stageAwards.SetValue(null, awards);
            gangAwards.SetValue(null, awards);
            AddBackground(canvas.transform);
            modelSprite = CreateLocalModelPreview(out modelTexture);
            foreach (var viewport in Viewports)
            foreach (var language in Languages)
            foreach (var claimed in new[] { false, true })
            foreach (var route in Routes)
            {
                GameObject instance = null;
                GangbangInfo gang = null;
                string description = $"{route}, {language}, {viewport.name}={viewport.width}x{viewport.height}, claimed={claimed}";
                try
                {
                    ConfigureViewport(viewport, canvasRect, safeRect, camera);
                    AppSetting.Value.Language = language;
                    skills.SetLanguage(language);
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
                    var mode = route == "Team" || route == "Gangbang" ? 1 : route == "Evolution" ? 3 : 2;
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
                    if (route == "Gangbang")
                    {
                        PopulateGroupPreparation(layer, modelSprite);
                        layer.RefreshPreparationLayout();
                        FitPortraits(layer);
                    }
                    else
                    {
                        PopulatePreparation(layer, route == "Evolution" ? 1 : 3, modelSprite);
                        Field<GameObject>(layer, "enemyDoubleExModeFlg").SetActive(route == "Rotation");
                        Field<GameObject>(layer, "enemyInfiniteExModeFlg").SetActive(route == "Boss");
                        layer.RefreshPreparationLayout();
                        FitPortraits(layer);
                    }
                    foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var layout in instance.GetComponentsInChildren<LayoutGroup>(true).Reverse())
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                    Canvas.ForceUpdateCanvases();
                    if (viewport.name == "phone" && claimed)
                    {
                        var imagePath = Path.Combine(OutputDirectory, route.ToLowerInvariant() + "-" + language.ToString().ToLowerInvariant() + ".png");
                        Render(camera, imagePath, viewport, safeRect);
                        report.screenshots.Add(imagePath);
                    }
                    CheckHeader(layer, route);
                    if (route != "Gangbang") CheckPreparationLayout(layer, canvasRect, safeRect);
                    else CheckGangbangPreparationButtons(layer, safeRect);
                    if (route == "Gangbang" && viewport.name == "phone" && claimed)
                        CheckGroupRosterInput(layer, canvas, camera);
                    if (route == "Team" && viewport.name == "phone" && claimed) CheckPreparationButtonStates(layer, report);
                }
                catch (Exception exception) { report.errors.Add(description + ": " + exception.GetBaseException()); }
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
            foreach (var viewport in Viewports)
            {
                GameObject instance = null;
                try
                {
                    ConfigureViewport(viewport, canvasRect, safeRect, camera);
                    AppSetting.Value.Language = SystemLanguage.Japanese;
                    PlayerAccountInfo.Me = new PlayerAccountInfo { tutorialProgress = "Finished", arcadeProcess = 54 };
                    instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePaths[0]), canvas.transform, false);
                    foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var localCamera in instance.GetComponentsInChildren<Camera>(true)) localCamera.enabled = false;
                    var layer = instance.GetComponent<FightPrepareLayer>();
                    var root = (RectTransform)instance.transform;
                    root.anchorMin = Vector2.zero;
                    root.anchorMax = Vector2.one;
                    root.offsetMin = root.offsetMax = Vector2.zero;
                    root.localScale = Vector3.one;
                    layer.ResizeAreas();
                    foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) converter.Change();
                    layer.SetArcadeFeature(() => { }, "55", AdventureModeRules.EvolutionMode);
                    AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/DummyLayerSystem/FightPrepareLayer/EvolutionMode.anim").SampleAnimation(instance, 0);
                    layer.SetFightMode(AdventureModeRules.EvolutionMode);
                    PopulatePreparation(layer, 1, modelSprite);
                    layer.RefreshPreparationLayout();
                    FitPortraits(layer);
                    foreach (var layout in instance.GetComponentsInChildren<LayoutGroup>(true).Reverse())
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                    Canvas.ForceUpdateCanvases();
                    var path = Path.Combine(OutputDirectory, "evolution-stage55-" + viewport.name + ".png");
                    Render(camera, path, viewport, safeRect);
                    report.screenshots.Add(path);
                    CheckHeader(layer, "Evolution");
                    CheckPreparationLayout(layer, canvasRect, safeRect);
                }
                catch (Exception exception) { report.errors.Add("Stage 55 showcase, " + viewport.name + ": " + exception.GetBaseException()); }
                finally { DestroyPreparation(instance); }
                report.casesChecked++;
            }
            CheckSkillDetailViewports(skills, report, canvasRect, safeRect, camera, modelSprite);
            foreach (var source in sourceFiles)
                Require(File.ReadAllText(source.Key) == source.Value, "Validation modified source prefab: " + source.Key);
        }
        finally
        {
            PlayerAccountInfo.Me = oldAccount;
            AppSetting.Value.Language = oldLanguage;
            CommonSetting.GangbangModeMaxUnitPerTeam1 = oldLimits[0];
            CommonSetting.GangbangModeMaxUnitPerTeam2 = oldLimits[1];
            CommonSetting.GangbangModeMaxUnitPerTeam3 = oldLimits[2];
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafeArea;
            languageRows.Clear();
            languageRows.AddRange(oldRows);
            languageProvider.SetValue(null, oldProvider);
            stageAwards.SetValue(null, oldStageAwards);
            gangAwards.SetValue(null, oldGangAwards);
            skills.Dispose();
            UnityEngine.Object.DestroyImmediate(rig);
            if (modelSprite != null) UnityEngine.Object.DestroyImmediate(modelSprite);
            if (modelTexture != null) UnityEngine.Object.DestroyImmediate(modelTexture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        report.passed = report.errors.Count == 0 && report.casesChecked == 160 && report.screenshots.Count == 26
            && report.localizedSkillsChecked == 288 && report.skillDetailCasesChecked == 12
            && report.buttonStatesChecked == report.expectedButtonStates;
        File.WriteAllText(Path.Combine(OutputDirectory, "report.json"), JsonUtility.ToJson(report, true));
        return report;
    }

    static void ConfigureViewport(Viewport viewport, RectTransform canvas, RectTransform safe, Camera camera)
    {
        canvas.sizeDelta = new Vector2(viewport.width, viewport.height);
        safe.offsetMin = new Vector2(0, viewport.bottomInset);
        safe.offsetMax = new Vector2(0, -viewport.topInset);
        camera.orthographicSize = viewport.height / 2f;
        camera.aspect = (float)viewport.width / viewport.height;
        Canvas.ForceUpdateCanvases();
    }

    sealed class LocalSkillFixture : IDisposable
    {
        readonly IDictionary<string, SkillConfig> originalConfigs = SkillConfigTable.SkillConfigRefDic;
        readonly FieldInfo rowsField = typeof(SkillNameTable).GetField("rowList", PrivateStatic);
        readonly FieldInfo namesField = typeof(SkillNameTable).GetField("SkillNameDic", PrivateStatic);
        readonly FieldInfo introsField = typeof(SkillNameTable).GetField("SkillIntroDic", PrivateStatic);
        readonly FieldInfo loadedField = typeof(SkillNameTable).GetField("isLoaded", PrivateStatic);
        readonly MethodInfo prepareNames = typeof(SkillNameTable).GetMethod("PrepareSkillNames", PrivateStatic);
        readonly SkillNameTable.Row[] originalRows;
        readonly Dictionary<string, string> originalNames, originalIntros;
        readonly bool originalLoaded;
        public readonly Dictionary<string, string[]> copy = new Dictionary<string, string[]>();
        public List<SkillConfig> configs;

        public LocalSkillFixture()
        {
            originalRows = ((List<SkillNameTable.Row>)rowsField.GetValue(null)).ToArray();
            originalNames = new Dictionary<string, string>((IDictionary<string, string>)namesField.GetValue(null));
            originalIntros = new Dictionary<string, string>((IDictionary<string, string>)introsField.GetValue(null));
            originalLoaded = (bool)loadedField.GetValue(null);
        }

        public void Load()
        {
            var ai = CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/skill_ai_attrs.csv"))
                .Skip(1).Where(row => row.Length >= 4).ToDictionary(row => row[0]);
            configs = new List<SkillConfig>();
            foreach (var row in CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/mst_skill.csv")).Skip(1))
            {
                if (row.Length < 8) continue;
                Require(ai.TryGetValue(row[0], out var distances), "Skill lacks AI range data: " + row[0]);
                Require(Enum.TryParse(SkillConfigTable.NormalizeAttackTypeOrOriginal(row[6]), out BehaviorType state),
                    "Skill has an unknown behavior type: " + row[0]);
                configs.Add(new SkillConfig
                {
                    RECORD_ID = row[0], REAL_NAME = row[1], TYPE = row[2], SP_LEVEL = int.Parse(row[3], CultureInfo.InvariantCulture),
                    ATTACK_WEIGHT = float.Parse(row[4], CultureInfo.InvariantCulture), HP_WEIGHT = float.Parse(row[5], CultureInfo.InvariantCulture),
                    STATE_TYPE = state, EVENT_CODE = row[7],
                    AIAttrs = new AIAttrs
                    {
                        AI_MIN_DIS = float.Parse(distances[1], CultureInfo.InvariantCulture),
                        AI_MAX_DIS = float.Parse(distances[2], CultureInfo.InvariantCulture), height = int.Parse(distances[3], CultureInfo.InvariantCulture)
                    }
                });
            }
            SkillConfigTable.SkillConfigRefDic = configs.ToDictionary(config => config.RECORD_ID);
            foreach (var row in CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/skill_name.csv")).Skip(1))
                if (row.Length == 7) copy.Add(row[0], row);
            var text = new TextAsset(File.ReadAllText("Assets/ExternalAssets/Config/skill_name.csv"));
            try { typeof(SkillNameTable).GetMethod("Load", PrivateStatic).Invoke(null, new object[] { text }); }
            finally { UnityEngine.Object.DestroyImmediate(text); }
        }

        public void SetLanguage(SystemLanguage language) => prepareNames.Invoke(null, new object[] { language });

        public void Dispose()
        {
            SkillConfigTable.SkillConfigRefDic = originalConfigs;
            var rows = (List<SkillNameTable.Row>)rowsField.GetValue(null);
            rows.Clear();
            rows.AddRange(originalRows);
            RestoreCopy((IDictionary<string, string>)namesField.GetValue(null), originalNames);
            RestoreCopy((IDictionary<string, string>)introsField.GetValue(null), originalIntros);
            loadedField.SetValue(null, originalLoaded);
        }

        static void RestoreCopy(IDictionary<string, string> target, IDictionary<string, string> source)
        {
            target.Clear();
            foreach (var pair in source) target.Add(pair.Key, pair.Value);
        }
    }

    static void CheckLocalizedSkills(LocalSkillFixture skills, Report report)
    {
        Require(skills.configs.Count == 96 && skills.copy.Count == 96, "Expected all 96 authored skill definitions and descriptions.");
        Require(skills.configs.Select(config => config.RECORD_ID).Distinct().Count() == 96
            && skills.configs.All(config => skills.copy.ContainsKey(config.RECORD_ID)), "Skill IDs and description IDs do not match.");
        for (int languageIndex = 0; languageIndex < Languages.Length; languageIndex++)
        {
            AppSetting.Value.Language = Languages[languageIndex];
            skills.SetLanguage(Languages[languageIndex]);
            foreach (var config in skills.configs)
            {
                try
                {
                    var row = skills.copy[config.RECORD_ID];
                    Require(!string.IsNullOrWhiteSpace(row[languageIndex + 1]) && !string.IsNullOrWhiteSpace(row[languageIndex + 4]),
                        "Authored name or introduction is empty.");
                    Require(SkillDescription.GetName(config) == row[languageIndex + 1], "Skill name does not use its localized authored copy.");
                    Require(SkillDescription.GetIntro(config) == row[languageIndex + 4].Trim(), "Skill introduction does not use its localized authored copy.");
                    Require(!string.IsNullOrWhiteSpace(SkillDescription.GetCategory(config))
                        && !string.IsNullOrWhiteSpace(SkillDescription.GetSpecialTier(config))
                        && !string.IsNullOrWhiteSpace(SkillDescription.GetRange(config)), "Skill metadata is empty.");
                    Require(SkillDescription.GetMetadata(config).Contains(SkillDescription.GetCategory(config))
                        && SkillDescription.GetMetadata(config).Contains(SkillDescription.GetRange(config)), "Skill metadata drops its category or AI range.");
                }
                catch (Exception exception) { report.errors.Add("Skill " + config.RECORD_ID + ", " + Languages[languageIndex] + ": " + exception.GetBaseException().Message); }
                report.localizedSkillsChecked++;
            }
            Require(!string.IsNullOrWhiteSpace(SkillDescription.TapHint) && !string.IsNullOrWhiteSpace(SkillDescription.PreviewLabel)
                && !string.IsNullOrWhiteSpace(SkillDescription.CloseLabel), "Localized skill controls are empty.");
        }
    }

    static void CheckSkillDetailViewports(LocalSkillFixture skills, Report report, RectTransform canvas, RectTransform safe, Camera camera, Sprite model)
    {
        foreach (var viewport in Viewports)
        foreach (var language in Languages)
        {
            GameObject instance = null;
            try
            {
                ConfigureViewport(viewport, canvas, safe, camera);
                AppSetting.Value.Language = language;
                skills.SetLanguage(language);
                PlayerAccountInfo.Me = new PlayerAccountInfo { tutorialProgress = "Finished", arcadeProcess = 54 };
                instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/FightPrepareLayer.prefab"), canvas, false);
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var localCamera in instance.GetComponentsInChildren<Camera>(true)) localCamera.enabled = false;
                var layer = instance.GetComponent<FightPrepareLayer>();
                var root = (RectTransform)instance.transform;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                root.localScale = Vector3.one;
                layer.ResizeAreas();
                foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) converter.Change();
                layer.SetArcadeFeature(() => { }, "55", AdventureModeRules.EvolutionMode);
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/DummyLayerSystem/FightPrepareLayer/EvolutionMode.anim").SampleAnimation(instance, 0);
                layer.SetFightMode(AdventureModeRules.EvolutionMode);
                PopulatePreparation(layer, 1, model);
                layer.RefreshPreparationLayout();
                FitPortraits(layer);
                RebuildPreparation(instance);

                var nine = Field<NineForShow>(layer, "nineForShow");
                foreach (var slot in nine.AllButton())
                {
                    var item = slot.GetComponentInChildren<SKStoneItem>();
                    Require(item != null && item._SkillConfig != null, "Skill slot fixture lacks a real definition.");
                    slot.onClick.Invoke();
                    CheckSkillDetails(layer, safe, item._SkillConfig);
                    Field<Button>(layer, "_preparationSkillCloseButton").onClick.Invoke();
                    CheckSkillDetailsHidden(layer, "Close leaves stale skill selection or preview callback.");
                }

                // Every authored skill must fit the title and metadata. Long
                // introductions retain a scrollable content rect instead of clipping.
                foreach (var config in skills.configs)
                {
                    layer.ShowPreparationSkillDetails(config, null);
                    CheckSkillDetails(layer, safe, config);
                    Require(!Field<Button>(layer, "_preparationSkillPreviewButton").interactable,
                        "Preview remains enabled without a preview callback.");
                }
                var showcase = skills.configs.First(config => config.RECORD_ID == "37");
                int previewInvocations = 0;
                layer.ShowPreparationSkillDetails(showcase, () => previewInvocations++);
                Field<Button>(layer, "_preparationSkillPreviewButton").onClick.Invoke();
                Require(previewInvocations == 1, "Skill preview does not invoke its supplied demonstration callback exactly once.");
                CheckSkillDetailsHidden(layer, "Preview leaves stale skill selection or callback.");
                layer.ShowPreparationSkillDetails(showcase, null);
                Field<RectTransform>(layer, "_preparationSkillOverlay").GetComponent<Button>().onClick.Invoke();
                CheckSkillDetailsHidden(layer, "Scrim dismissal leaves stale skill selection or callback.");

                layer.ShowPreparationSkillDetails(showcase, () => previewInvocations++);
                CheckSkillDetails(layer, safe, showcase);
                if (language == SystemLanguage.Chinese)
                {
                    var path = Path.Combine(OutputDirectory, "skill-details-chinese-" + viewport.name + ".png");
                    Render(camera, path, viewport, safe);
                    report.screenshots.Add(path);
                }
                layer.SetEventFeature("hard_fixture");
                CheckSkillDetailsHidden(layer, "Changing preparation route leaves stale skill details.");
                CheckCharacterSelectionClearsDetails(layer, showcase);
            }
            catch (Exception exception) { report.errors.Add("Skill details, " + language + ", " + viewport.name + ": " + exception.GetBaseException()); }
            finally { DestroyPreparation(instance); }
            report.skillDetailCasesChecked++;
            report.casesChecked++;
        }
    }

    static void CheckSkillDetails(FightPrepareLayer layer, RectTransform safe, SkillConfig config)
    {
        layer.LayoutPreparationSkillDetails();
        Canvas.ForceUpdateCanvases();
        var overlay = Field<RectTransform>(layer, "_preparationSkillOverlay");
        var card = Field<RectTransform>(layer, "_preparationSkillCard");
        var viewport = Field<RectTransform>(layer, "_preparationSkillViewport");
        var title = Field<Text>(layer, "_preparationSkillTitle");
        var metadata = Field<Text>(layer, "_preparationSkillMetadata");
        var intro = Field<Text>(layer, "_preparationSkillIntro");
        Require(overlay.gameObject.activeInHierarchy && card.gameObject.activeInHierarchy, "Clicking a populated gem does not show skill details.");
        Require(ReferenceEquals(typeof(FightPrepareLayer).GetField("_preparationSelectedSkill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer), config),
            "Detail card shows a different skill than the selected slot.");
        Require(title.text == SkillDescription.GetName(config) && intro.text == SkillDescription.GetIntro(config)
            && metadata.text == SkillDescription.GetMetadata(config), "Detail card drops or changes localized skill copy.");
        Require(Contains(safe.rect, Bounds(safe, overlay)) && Contains(safe.rect, Bounds(safe, card)), "Skill details escape the device safe area.");
        Require(Contains(layer.MiddleArea.rect, Bounds(layer.MiddleArea, card)), "Skill detail card escapes its preparation area.");
        var regions = new[] { title.rectTransform, metadata.rectTransform, viewport,
            (RectTransform)Field<Button>(layer, "_preparationSkillCloseButton").transform,
            (RectTransform)Field<Button>(layer, "_preparationSkillPreviewButton").transform };
        var bounds = regions.Select(rect => Bounds(card, rect)).ToArray();
        for (int index = 0; index < regions.Length; index++)
        {
            Require(Contains(card.rect, bounds[index]), "Detail control escapes its card: " + regions[index].name);
            for (int previous = 0; previous < index; previous++)
                Require(!bounds[index].Overlaps(bounds[previous]), "Detail controls overlap: " + regions[index].name);
        }
        foreach (var text in new[] { title, metadata, Field<Text>(layer, "_preparationSkillCaption"),
            Field<Text>(layer, "_preparationSkillPreviewLabel"), Field<Text>(layer, "_preparationSkillCloseLabel") })
            Require(text.preferredHeight <= text.rectTransform.rect.height + 0.5f, "Localized detail label is clipped: " + text.name);
        Require(intro.rectTransform.rect.height + 0.5f >= intro.preferredHeight && intro.rectTransform.rect.width > 0,
            "Skill introduction is clipped instead of fitting its scroll content.");
        var scroll = Field<ScrollRect>(layer, "_preparationSkillScroll");
        Require(scroll.vertical && !scroll.horizontal && scroll.viewport == viewport && scroll.content == intro.rectTransform,
            "Skill details do not retain their vertical scrolling relationship.");
        Require(viewport.GetComponent<RectMask2D>() != null, "Skill introduction has no viewport clipping mask.");
    }

    static void CheckSkillDetailsHidden(FightPrepareLayer layer, string message)
    {
        Require(!Field<RectTransform>(layer, "_preparationSkillOverlay").gameObject.activeSelf
            && typeof(FightPrepareLayer).GetField("_preparationSelectedSkill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer) == null
            && typeof(FightPrepareLayer).GetField("_preparationSkillPreviewAction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer) == null, message);
    }

    static void CheckCharacterSelectionClearsDetails(FightPrepareLayer layer, SkillConfig config)
    {
        layer.ShowPreparationSkillDetails(config, null);
        var queue = (SingleThreadProcessor)typeof(FightPrepareLayer).GetField("_previewQueue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer);
        var semaphore = (SemaphoreSlim)typeof(SingleThreadProcessor).GetField("semaphore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(queue);
        Require(semaphore.Wait(0), "Local character-switch fixture could not hold the preview queue.");
        using (var cancellation = new CancellationTokenSource())
        {
            try
            {
                // Hold renderer work locally. The production selection path must
                // clear the card before its first await; cancel before releasing
                // so no model, Addressables, account or network work can run.
                var unit = new UnitInfo { id = "local-selection-fixture", r_id = "1" };
                var version = (int)typeof(FightPrepareLayer).GetField("_displayVersion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer);
                var task = (UniTask)typeof(FightPrepareLayer).GetMethod("FocusTeamUnit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layer,
                    new object[] { unit.id, new Dictionary<string, UnitInfo> { [unit.id] = unit },
                        Field<ModelView.DedicatedCameraConnector>(layer, "connector"), Field<NineForShow>(layer, "nineForShow"), version, cancellation.Token });
                CheckSkillDetailsHidden(layer, "Changing selected character leaves stale skill details.");
                cancellation.Cancel();
                task.Forget();
            }
            finally { cancellation.Cancel(); semaphore.Release(); }
        }
    }

    static void RebuildPreparation(GameObject instance)
    {
        foreach (var layout in instance.GetComponentsInChildren<LayoutGroup>(true).Reverse())
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
        Canvas.ForceUpdateCanvases();
    }

    static void CheckPreparationButtonStates(FightPrepareLayer layer, Report report)
    {
        var fight = Field<FightBeginBtn>(layer, "beginFight");
        var fightButton = Field<BOButton>(fight, "btn");
        var skin = fightButton.GetComponent<PreparationButtonSkin>();
        Require(skin != null, "FIGHT has no preparation skin.");
        var outline = fightButton.transform.Find("PreparationOutline").GetComponent<Image>();
        int fightInvocations = 0, editInvocations = 0;
        layer.SetFightBeginFeature(() => fightInvocations++);
        layer.SetTeamEditFeature(() => editInvocations++);
        bool preferenceExisted = PlayerPrefs.HasKey("preferAdventureMode");
        int preference = PlayerPrefs.GetInt("preferAdventureMode", 2);
        int originalGangbangPreference = PlayerPrefs.GetInt("gangbangCountOption", 1);
        bool originalGangbangPreferenceExisted = PlayerPrefs.HasKey("gangbangCountOption");
        try
        {
            layer.SetFightBeginEnableRender(false);
            var disabledOutline = outline.color;
            Press(fightButton);
            Require(!fightButton.interactable && fightInvocations == 0, "Disabled FIGHT still launches its action.");
            report.buttonStatesChecked++;
            layer.SetFightBeginEnableRender(true);
            Press(fightButton);
            Require(fightButton.interactable && fightInvocations == 1 && outline.color != disabledOutline,
                "Enabled FIGHT loses its action or disabled-state distinction.");
            report.buttonStatesChecked++;
            layer.SetFightBeginEnableRender(true, true);
            typeof(PreparationButtonSkin).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skin, null);
            Require((bool)typeof(PreparationButtonSkin).GetField("_guide", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skin)
                && outline.color.a >= 0.66f && outline.color.a <= 1 && !Field<Animator>(fight, "animator").enabled,
                "Guided FIGHT loses its outline pulse or restores the old ring animator.");
            report.buttonStatesChecked++;
            layer.SetFightBeginEnableRender(true, false);
            Require(!(bool)typeof(PreparationButtonSkin).GetField("_guide", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skin),
                "Clearing guidance leaves the preparation button pulsing.");
            report.buttonStatesChecked++;
            Press(Field<BOButton>(layer, "editTeamButton"));
            Require(editInvocations == 1, "Skinned Edit team loses its assigned action.");
            report.buttonStatesChecked++;

            var mode = Field<FightModeSwitch>(layer, "fightModeSwitch");
            var modeButton = Field<BOButton>(mode, "btn");
            PlayerPrefs.SetInt("preferAdventureMode", 1);
            layer.SetFightMode(0);
            Require(mode.TeamMode == TeamMode.Rotation && !modeButton.interactable,
                "Unconfigured non-training preparation remains selectable or inherits the practice preference.");
            Press(modeButton);
            Require(mode.TeamMode == TeamMode.Rotation && PlayerPrefs.GetInt("preferAdventureMode") == 1,
                "Non-training preparation changes the mode or saved practice preference.");
            report.buttonStatesChecked++;
            PlayerPrefs.SetInt("preferAdventureMode", 2);
            mode.Setup(0, 2, BattleModeRules.AllowsModeSwitch(FightEventType.Self));
            Require(mode.TeamMode == TeamMode.Rotation && modeButton.interactable,
                "Practice mode does not retain its saved Rotation preference.");
            Press(modeButton);
            Require(mode.TeamMode == TeamMode.MultiRaid && PlayerPrefs.GetInt("preferAdventureMode") == 1,
                "Skinned mode switch does not select/save Team mode.");
            report.buttonStatesChecked++;
            Press(modeButton);
            Require(mode.TeamMode == TeamMode.Rotation && PlayerPrefs.GetInt("preferAdventureMode") == 2,
                "Skinned mode switch does not select/save Rotation mode.");
            report.buttonStatesChecked++;
            layer.SetFightMode(1);
            Press(modeButton);
            Require(!modeButton.interactable && mode.TeamMode == TeamMode.MultiRaid,
                "Fixed Team mode remains interactive after skinning.");
            report.buttonStatesChecked++;
            layer.SetFightMode(2);
            Press(modeButton);
            Require(!modeButton.interactable && mode.TeamMode == TeamMode.Rotation,
                "Fixed Rotation mode remains interactive after skinning.");
            report.buttonStatesChecked++;
            layer.SetFightMode(3);
            Require(!modeButton.gameObject.activeSelf && mode.TeamMode == TeamMode.Rotation,
                "Evolution mode restores the hidden mode switch.");
            Require(PlayerPrefs.GetInt("gangbangCountOption", 1) == originalGangbangPreference
                && PlayerPrefs.HasKey("gangbangCountOption") == originalGangbangPreferenceExisted,
                "Preparation button checks change the Gangbang preference.");
            report.buttonStatesChecked++;

            var resultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/ArenaFightOver.prefab");
            Require(resultPrefab != null, "The result-screen prefab is missing.");
            var resultButtons = resultPrefab.GetComponentsInChildren<NextOrAgainBtn>(true);
            Require(resultButtons.Length == ExpectedResultButtons, "The result prefab must include retry and next buttons.");
            foreach (var resultButton in resultButtons)
            {
                var instance = UnityEngine.Object.Instantiate(resultButton.gameObject);
                try
                {
                    var result = instance.GetComponent<NextOrAgainBtn>();
                    foreach (var fixedMode in LockedResultModes)
                    {
                        result.SetUp(fixedMode, "Stage 6");
                        Require(!Field<RectTransform>(result, "modeRoot").gameObject.activeSelf
                            && !Field<BOButton>(result, "againFor1v1Btn").gameObject.activeSelf
                            && !Field<BOButton>(result, "againForMultiBtn").gameObject.activeSelf,
                            "A non-training retry/next result exposes mode choices.");
                        var mainCalls = 0;
                        var alternateCalls = 0;
                        result.SetUpAction(() => mainCalls++, () => alternateCalls++, () => alternateCalls++);
                        Field<BOButton>(result, "againBtn").onClick.Invoke();
                        Field<BOButton>(result, "againFor1v1Btn").onClick.Invoke();
                        Field<BOButton>(result, "againForMultiBtn").onClick.Invoke();
                        Require(mainCalls == 1 && alternateCalls == 0,
                            "A hidden result action still switches the encounter mode.");
                        report.buttonStatesChecked++;
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }
        finally
        {
            if (preferenceExisted) PlayerPrefs.SetInt("preferAdventureMode", preference);
            else PlayerPrefs.DeleteKey("preferAdventureMode");
            layer.SetFightMode(1);
            layer.SetFightBeginEnableRender(true, false);
        }
    }

    static void Press(Button button)
    {
        // Use Unity's actual active/interactable event gate without BOButton's
        // global anti-double-tap timer, sound hooks or edit-mode coroutines.
        typeof(Button).GetMethod("Press", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, null);
    }

    static void CheckPreparationButtonGeometry(FightPrepareLayer layer)
    {
        var fight = Field<FightBeginBtn>(layer, "beginFight");
        var mode = Field<FightModeSwitch>(layer, "fightModeSwitch");
        var buttons = new[] { Field<BOButton>(fight, "btn"), Field<BOButton>(layer, "editTeamButton"), Field<BOButton>(mode, "btn"), Field<BOButton>(layer, "toArcadeFrontBtn") };
        var expectedSizes = new[] { new Vector2(416, 104), new Vector2(200, 68), new Vector2(220, 88), new Vector2(212, 76) };
        var fill = Resources.Load<Sprite>("UI/Preparation/PreparationButtonFill");
        var border = Resources.Load<Sprite>("UI/Preparation/PreparationButtonOutline");
        Require(fill != null && border != null && fill.border.sqrMagnitude > 0 && border.border.sqrMagnitude > 0,
            "Preparation button textures are missing their nine-slice borders.");
        for (int index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            Require(button.GetComponent<PreparationButtonSkin>() != null, "Preparation button has no skin: " + button.name);
            Require(Vector2.Distance(((RectTransform)button.transform).rect.size, expectedSizes[index]) < 0.5f,
                "Preparation button has the wrong hit-target size: " + button.name);
            var background = button.transform.Find("PreparationFill").GetComponent<Image>();
            var outline = button.transform.Find("PreparationOutline").GetComponent<Image>();
            Require(background.sprite == fill && outline.sprite == border && background.type == Image.Type.Sliced && outline.type == Image.Type.Sliced,
                "Preparation buttons do not share the sliced fill/outline textures.");
            Require(background.raycastTarget && !outline.raycastTarget && button.targetGraphic == background,
                "Preparation button hit target or state graphic is incorrect.");
            foreach (var label in button.GetComponentsInChildren<Text>(true).Where(text => text.name != "teamEditTip"))
                if (label.enabled && label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                    Require(label.preferredHeight <= label.rectTransform.rect.height + 0.5f, "Preparation button label is clipped: " + label.name);
        }
        Require(!Field<Animator>(fight, "animator").enabled && !Field<Animator>(mode, "animator").enabled,
            "Legacy button animators still overwrite the new preparation skin.");
        foreach (var image in fight.GetComponentsInChildren<Image>(true))
            if (image.name != "PreparationFill" && image.name != "PreparationOutline")
                Require(!image.enabled, "Old FIGHT ring remains visible: " + image.name);
    }

    static void CheckGangbangPreparationButtons(FightPrepareLayer layer, RectTransform safe)
    {
        CheckPreparationButtonGeometry(layer);
        var root = (RectTransform)layer.transform;
        var fight = (RectTransform)Field<FightBeginBtn>(layer, "beginFight").transform;
        var edit = (RectTransform)Field<BOButton>(layer, "editTeamButton").transform;
        var editBounds = Bounds((RectTransform)layer.transform, edit);
        Require(Mathf.Abs(editBounds.width - 200) < 0.5f && Mathf.Abs(editBounds.height - 68) < 0.5f,
            "Gangbang's scaled roster shrinks its edit action.");
        foreach (var control in new[] { fight, edit })
            if (control.gameObject.activeInHierarchy)
                Require(Contains(safe.rect, Bounds(safe, control)), "Gangbang preparation button escapes the device safe area: " + control.name);
        var header = Field<RectTransform>(layer, "_groupHeader");
        var left = Field<RectTransform>(layer, "_groupLeft");
        var right = Field<RectTransform>(layer, "_groupRight");
        var preview = Field<RectTransform>(layer, "_groupPreview");
        var actions = Field<RectTransform>(layer, "_groupActions");
        var panels = new[] { header, left, right, preview, actions };
        foreach (var name in new[] { "team1Name", "team2Name" })
        {
            var caption = Field<Text>(layer, name);
            Require(caption.gameObject.activeInHierarchy && caption.enabled && !string.IsNullOrWhiteSpace(caption.text), "Group team caption is hidden.");
            Require(caption.preferredHeight <= caption.rectTransform.rect.height + 1, "Group team caption clips.");
        }
        foreach (var panel in panels) Require(Contains(safe.rect, Bounds(safe, panel)), "Group portrait panel escapes safe area: " + panel.name);
        for (int i = 0; i < panels.Length; i++)
            for (int j = 0; j < i; j++) Require(!Bounds(root, panels[i]).Overlaps(Bounds(root, panels[j])), "Group portrait panels overlap.");
        Require(Bounds(root, left).xMax < Bounds(root, preview).xMin && Bounds(root, right).xMin > Bounds(root, preview).xMax,
            "Group teams are not vertical rails on opposite sides of the models.");
        Require(Bounds(root, left).yMax > root.rect.yMax - root.rect.height * 0.2f, "Group roster leaves the top of the portrait screen empty.");
        foreach (var row in new[] { Field<RectTransform>(layer, "myTeamShowT"), Field<RectTransform>(layer, "enemyTeamShowT") })
        {
            Require(row.Cast<Transform>().Count(child => child.gameObject.activeSelf) == (row == Field<RectTransform>(layer, "myTeamShowT") ? 3 : 4),
                "Group roster includes unexpected or missing portraits.");
            float previous = float.PositiveInfinity;
            foreach (var icon in row.GetComponentsInChildren<GangbangHeroIcon>())
            {
                var bounds = Bounds(root, (RectTransform)icon.transform);
                Require(bounds.center.y < previous, "Group portraits are not ordered vertically.");
                previous = bounds.center.y;
                var count = Field<Text>(icon, "count");
                Require(count.preferredHeight <= count.rectTransform.rect.height + 1, "Group unit count clips.");
                foreach (var button in new[] { Field<BOButton>(icon, "minusBtn"), Field<BOButton>(icon, "plusBtn") })
                    if (button.gameObject.activeSelf)
                    {
                        Require(((RectTransform)button.transform).rect.height >= 48, "Group count touch control is too small.");
                        Require(!bounds.Overlaps(Bounds(root, (RectTransform)button.transform)), "Group count control covers its portrait.");
                        Require(button.targetGraphic != null && button.targetGraphic.raycastTarget, "Group count control lost its hit target.");
                    }
            }
            var scroll = row.GetComponentInParent<ScrollRect>();
            Require(scroll != null && scroll.content == row && scroll.vertical && !scroll.horizontal, "Group long roster cannot scroll vertically.");
        }
        var first = Bounds(root, (RectTransform)Field<BOButton>(layer, "countSet1").transform);
        foreach (var name in new[] { "countSet2", "countSet3" })
            Require(Mathf.Abs(Bounds(root, (RectTransform)Field<BOButton>(layer, name).transform).center.y - first.center.y) < 1,
                "Group population options are not a horizontal row.");
        var watched = panels.Concat(new[] { fight, edit }).ToArray();
        var before = watched.Select(item => Bounds(root, item)).ToArray();
        layer.RefreshPreparationLayout();
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < watched.Length; i++) Require(Vector2.Distance(before[i].center, Bounds(root, watched[i]).center) < 0.1f,
            "Group repeated layout moves a stable control.");
    }

    static void CheckGroupRosterInput(FightPrepareLayer layer, Canvas canvas, Camera camera)
    {
        var row = Field<RectTransform>(layer, "myTeamShowT");
        var icon = row.GetComponentInChildren<GangbangHeroIcon>();
        var count = Field<Text>(icon, "count");
        int before = int.Parse(count.text);
        Press(Field<BOButton>(icon, "plusBtn"));
        Require(int.Parse(count.text) == before + 1, "Group plus button does not update its authored counter callback.");
        Press(Field<BOButton>(icon, "minusBtn"));
        Require(int.Parse(count.text) == before, "Group minus button does not update its authored counter callback.");
        var scroll = row.GetComponentInParent<ScrollRect>();
        var oldViewportSize = scroll.viewport.sizeDelta;
        var oldContentSize = row.sizeDelta;
        var oldPosition = row.anchoredPosition;
        var oldCurrent = EventSystem.current;
        var eventObject = new GameObject("Group Scroll Gesture", typeof(EventSystem));
        var events = eventObject.GetComponent<EventSystem>();
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        bool added = raycaster == null;
        if (added) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        try
        {
            scroll.viewport.sizeDelta = new Vector2(scroll.viewport.sizeDelta.x, 220);
            row.sizeDelta = new Vector2(row.sizeDelta.x, 1100);
            row.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(events)
            {
                button = PointerEventData.InputButton.Left,
                position = camera.WorldToScreenPoint(icon.transform.position),
                pointerPressRaycast = new RaycastResult { module = raycaster },
                pointerCurrentRaycast = new RaycastResult { module = raycaster }
            };
            var target = ExecuteEvents.GetEventHandler<IDragHandler>(icon.iconButton.gameObject);
            Require(target == icon.gameObject, "Group gesture does not traverse the real portrait handler.");
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
            pointer.position += Vector2.up * 80;
            pointer.delta = Vector2.up * 80;
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.endDragHandler);
            Require(row.anchoredPosition.y > 10 && HeroIcon.dragging == null,
                "Dragging a group portrait does not scroll the roster or clones a drag icon.");
        }
        finally
        {
            scroll.StopMovement();
            scroll.viewport.sizeDelta = oldViewportSize;
            row.sizeDelta = oldContentSize;
            row.anchoredPosition = oldPosition;
            if (added) UnityEngine.Object.DestroyImmediate(raycaster);
            UnityEngine.Object.DestroyImmediate(eventObject);
            if (oldCurrent != null) EventSystem.current = oldCurrent;
        }
    }

    static void AddBackground(Transform canvas)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(BackgroundGuid));
        Require(texture != null, "Missing shared menu background.");
        var background = new GameObject("Actual Menu Background", typeof(RectTransform), typeof(RawImage));
        background.transform.SetParent(canvas, false);
        background.transform.SetAsFirstSibling();
        var image = background.GetComponent<RawImage>();
        image.texture = texture;
        image.color = new Color(0.8862745f, 0.8862745f, 0.8862745f, 1);
        image.raycastTarget = false;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
    }

    static GameObject AddNavigationPreview(RectTransform safe)
    {
        var fixture = new GameObject("Shared Navigation Fixture", typeof(RectTransform));
        fixture.SetActive(false);
        fixture.transform.SetParent(safe, false);
        var rect = (RectTransform)fixture.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        foreach (var name in new[] { "LowerMainBar", "ReturnLayer" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/" + name + ".prefab");
            Require(prefab != null, "Missing shared navigation prefab: " + name);
            var instance = UnityEngine.Object.Instantiate(prefab, fixture.transform, false);
            var root = (RectTransform)instance.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
            foreach (var behaviour in instance.GetComponentsInChildren<Behaviour>(true))
                if (!(behaviour is Graphic) && !(behaviour is Selectable) && !(behaviour is BaseMeshEffect)
                    && !(behaviour is Mask) && !(behaviour is RectMask2D) && !(behaviour is LayoutGroup))
                    behaviour.enabled = false;
            foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) converter.Change();
            instance.GetComponent<UILayer>().ResizeAreas();
            if (name == "LowerMainBar")
            {
                foreach (var icon in instance.GetComponentsInChildren<LowerBarIcon>(true))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/DummyLayerSystem/LayerDefine/LowerMainBar/"
                        + (icon.name == "play" ? "selected.anim" : "unselect.anim"));
                    Require(clip != null, "Missing shared navigation selection clip.");
                    clip.SampleAnimation(icon.gameObject, clip.length);
                    icon.Indicator.SetActive(false);
                }
                Field<GameObject>(instance.GetComponent<LowerMainBar>(), "hasStoneToBeUpdateBadge").SetActive(false);
            }
            else
            {
                Field<GameObject>(instance.GetComponent<ReturnLayer>(), "indicator").SetActive(false);
                Field<GameObject>(instance.GetComponent<ReturnLayer>(), "curtain").SetActive(false);
            }
        }
        safe.SetAsLastSibling();
        fixture.SetActive(true);
        Canvas.ForceUpdateCanvases();
        // All shared controls retain their authored bounds. The fixture's safe
        // parent supplies the same bottom inset as the live navigation layers.
        foreach (var button in fixture.GetComponentsInChildren<BOButton>())
            Require(Contains(safe.rect, Bounds(safe, (RectTransform)button.transform)), "Shared navigation control escapes safe area: " + button.name);
        return fixture;
    }

    static void PopulatePreparation(FightPrepareLayer layer, int heroCount, Sprite model)
    {
        var iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IconPrefabPath).GetComponent<HeroIcon>();
        Require(iconPrefab != null, "Missing actual HeroIcon prefab.");
        AddPortraitRow(Field<RectTransform>(layer, "enemyTeamShowT"), iconPrefab, new[] { 3, 3, 3, 13 }, true);
        AddPortraitRow(Field<RectTransform>(layer, "myTeamShowT"), iconPrefab, Enumerable.Range(1, heroCount).ToArray(), false);
        var stones = AssetDatabase.LoadAssetAtPath<GameObject>(StonePrefabPath);
        Require(stones != null, "Missing actual stoneModel prefab.");
        var nine = Field<NineForShow>(layer, "nineForShow");
        var skillIds = new[] { 37, 9, 12, 9, 10, 9, 10, 36, 9 };
        var buttons = nine.AllButton();
        Require(buttons.Count == 9 && buttons.All(button => button != null), "Preparation does not expose all nine gem slots.");
        for (var index = 0; index < buttons.Count; index++)
        {
            var stone = UnityEngine.Object.Instantiate(stones, buttons[index].transform, false);
            stone.name = "Local Gem Fixture " + skillIds[index];
            var item = stone.GetComponent<SKStoneItem>();
            item.enabled = false;
            item._SkillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillIds[index].ToString(CultureInfo.InvariantCulture));
            item.image.sprite = LocalSprite(SkillDirectory, skillIds[index]);
            item.image.raycastTarget = false;
            item.info.gameObject.SetActive(false);
            var rect = (RectTransform)stone.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            foreach (var animator in stone.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        }
        Field<Text>(layer, "team1Name").text = Translate.Get("Player");
        Field<Text>(layer, "team2Name").text = Translate.Get("Enemy");
        Field<Text>(layer, "team1OneWord").text = string.Empty;
        Field<Text>(layer, "team2OneWord").text = string.Empty;
        Field<GameObject>(layer, "teamEditIndicator").SetActive(false);
        layer.SetFightBeginEnableRender(true);
        var connector = Field<ModelView.DedicatedCameraConnector>(layer, "connector");
        var preview = new GameObject("Static Local Character Fixture", typeof(RectTransform), typeof(Image));
        preview.transform.SetParent(connector.transform, false);
        preview.transform.SetAsFirstSibling();
        var modelImage = preview.GetComponent<Image>();
        modelImage.sprite = model;
        modelImage.preserveAspect = true;
        modelImage.raycastTarget = false;
        modelImage.rectTransform.anchorMin = Vector2.zero;
        modelImage.rectTransform.anchorMax = Vector2.one;
        modelImage.rectTransform.offsetMin = new Vector2(12, 16);
        modelImage.rectTransform.offsetMax = new Vector2(-12, -16);
    }

    static void PopulateGroupPreparation(FightPrepareLayer layer, Sprite model)
    {
        PopulatePreparation(layer, 3, model);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/unit/GangbangHeroIcon.prefab").GetComponent<GangbangHeroIcon>();
        AddPortraitRow(Field<RectTransform>(layer, "myTeamShowT"), prefab, new[] { 1, 2, 1 }, false);
        AddPortraitRow(Field<RectTransform>(layer, "enemyTeamShowT"), prefab, new[] { 8, 8, 8, 13 }, true);
        foreach (var name in new[] { "myTeamShowT", "enemyTeamShowT" })
        {
            bool editable = name == "myTeamShowT";
            int index = 0;
            foreach (var icon in Field<RectTransform>(layer, name).GetComponentsInChildren<GangbangHeroIcon>())
            {
                int value = editable ? new[] { 8, 5, 11 }[index++] : new[] { 8, 8, 8, 0 }[index++];
                Func<int, int> set = next => value = Mathf.Clamp(next, 0, 24);
                Func<int> get = () => value;
                typeof(GangbangHeroIcon).GetMethod("SetUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(icon, new object[] { set, get, editable });
            }
        }
        Field<Text>(layer, "team1WholeCount").text = "24/24";
        Field<Text>(layer, "team2WholeCount").text = "24/24";
        var firstNine = Field<NineForShow>(layer, "nineForShow");
        var secondNine = Field<NineForShow>(layer, "nineForShowE");
        for (int i = 0; i < 9; i++)
        {
            var stone = firstNine.AllButton()[i].GetComponentInChildren<SKStoneItem>(true);
            UnityEngine.Object.Instantiate(stone.gameObject, secondNine.AllButton()[i].transform, false);
        }
        var preview = Field<ModelView.DedicatedCameraConnector>(layer, "connector").transform.Find("Static Local Character Fixture");
        UnityEngine.Object.Instantiate(preview.gameObject, Field<ModelView.DedicatedCameraConnector>(layer, "connectorE").transform, false);
    }

    static void AddPortraitRow(RectTransform parent, HeroIcon prefab, int[] ids, bool enemy)
    {
        while (parent.childCount > 0) UnityEngine.Object.DestroyImmediate(parent.GetChild(0).gameObject);
        foreach (var id in ids)
        {
            var icon = UnityEngine.Object.Instantiate(prefab, parent, false);
            icon.name = (enemy ? "Enemy" : "Hero") + " Fixture " + id;
            foreach (var animator in icon.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var image = Field<Image>(icon, "icon");
            image.sprite = LocalSprite(PortraitDirectory, id);
            image.color = Color.white;
            image.gameObject.SetActive(true);
            var color = enemy ? new Color(0.95f, 0.22f, 0.33f) : new Color(1f, 0.83f, 0.15f);
            Field<Image>(icon, "frame").color = color;
            Field<Image>(icon, "iconBg").color = new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.22f, 1);
            var curtain = Field<Image>(icon, "cooldownCurtain");
            if (curtain != null) curtain.fillAmount = 0;
            icon.WarnFlag.SetActive(false);
            icon.ApplyPreparationStyle();
        }
    }

    static Sprite LocalSprite(string directory, int id)
    {
        var path = Directory.GetFiles(directory, id + ".*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(candidate => candidate.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        Require(path != null, "Missing local sprite: " + directory + id);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Require(sprite != null, "Local sprite is not imported as a Sprite: " + path);
        return sprite;
    }

    static void FitPortraits(FightPrepareLayer layer)
    {
        foreach (var icon in layer.GetComponentsInChildren<HeroIcon>())
        {
            var image = Field<Image>(icon, "icon");
            if (image.sprite == null) continue;
            var size = ((RectTransform)icon.transform).rect.size;
            var aspect = image.sprite.rect.width / image.sprite.rect.height;
            image.rectTransform.sizeDelta = aspect < 1 ? new Vector2(size.y * aspect, size.y) : new Vector2(size.x, size.x / aspect);
        }
    }

    static void CheckPreparationLayout(FightPrepareLayer layer, RectTransform canvas, RectTransform safe)
    {
        var root = (RectTransform)layer.transform;
        var safeBounds = Bounds(root, safe);
        var top = Bounds(root, layer.TopArea);
        var middle = Bounds(root, layer.MiddleArea);
        Require(Contains(safeBounds, top) && Contains(safeBounds, middle), "Preparation sections escape the safe area.");
        Require(!top.Overlaps(middle), "Preview/header section overlaps team section.");
        var enemyRow = Field<RectTransform>(layer, "enemyTeamShowT");
        var heroRow = Field<RectTransform>(layer, "myTeamShowT");
        var enemyPanel = (RectTransform)enemyRow.parent;
        var heroPanel = (RectTransform)heroRow.parent;
        var enemyBounds = Bounds(root, enemyPanel);
        var heroBounds = Bounds(root, heroPanel);
        Require(Contains(middle, enemyBounds) && Contains(middle, heroBounds), "A team panel escapes the preparation region.");
        Require(!enemyBounds.Overlaps(heroBounds), "Enemy and player team panels overlap.");
        CheckPortraitRow(enemyRow, enemyPanel);
        CheckPortraitRow(heroRow, heroPanel);
        var fight = (RectTransform)Field<FightBeginBtn>(layer, "beginFight").transform;
        var edit = (RectTransform)Field<BOButton>(layer, "editTeamButton").transform;
        var fightBounds = Bounds(root, fight);
        Require(Contains(safeBounds, fightBounds) && Contains(middle, fightBounds), "FIGHT escapes its action region or safe area.");
        Require(!fightBounds.Overlaps(enemyBounds) && !fightBounds.Overlaps(heroBounds), "FIGHT overlaps a team panel.");
        if (edit.gameObject.activeInHierarchy)
        {
            var editBounds = Bounds(root, edit);
            Require(Contains(heroBounds, editBounds), "Edit team button escapes the player panel.");
            Require(!editBounds.Overlaps(Bounds(root, heroRow)), "Edit team button overlaps the portrait row.");
            Require(!editBounds.Overlaps(fightBounds), "Edit team button overlaps FIGHT.");
        }
        var preview = (RectTransform)layer.TopArea.Find("PreparationPreview");
        Require(preview != null, "Preparation preview panel is missing.");
        var previewBounds = Bounds(root, preview);
        Require(Contains(top, previewBounds), "Character/gem preview escapes its section.");
        var connector = (RectTransform)Field<ModelView.DedicatedCameraConnector>(layer, "connector").transform;
        var nine = Field<NineForShow>(layer, "nineForShow");
        var grid = (RectTransform)nine.transform;
        var modelBounds = Bounds(root, connector);
        var gridBounds = Bounds(root, grid);
        Require(Contains(previewBounds, modelBounds) && Contains(previewBounds, gridBounds), "Character model or gems escape their preview panel.");
        Require(!modelBounds.Overlaps(gridBounds), "Character model region overlaps gem grid.");
        var slots = nine.AllButton().Select(button => Bounds(root, (RectTransform)button.transform)).ToArray();
        for (var index = 0; index < slots.Length; index++)
        {
            Require(Contains(gridBounds, slots[index]), "A gem slot escapes the grid.");
            Require(Mathf.Abs(slots[index].width - slots[index].height) < 0.5f, "A gem slot is stretched.");
            for (var previous = 0; previous < index; previous++) Require(!slots[index].Overlaps(slots[previous]), "Gem slots overlap.");
        }
        var monitored = new[] { layer.TopArea, layer.MiddleArea, enemyPanel, heroPanel, enemyRow, heroRow, fight, edit, preview, connector, grid };
        var before = monitored.Select(rect => Bounds(root, rect)).ToArray();
        layer.RefreshPreparationLayout();
        Canvas.ForceUpdateCanvases();
        for (var index = 0; index < monitored.Length; index++)
        {
            var after = Bounds(root, monitored[index]);
            Require(Vector2.Distance(before[index].position, after.position) < 0.5f
                && Vector2.Distance(before[index].size, after.size) < 0.5f, "Repeated layout drifts: " + monitored[index].name);
        }
        CheckCachedAnimationCorrection(layer, root, fight, edit);
        CheckHintVisibility(layer, edit.gameObject);
        CheckPreparationButtonGeometry(layer);
    }

    static void CheckCachedAnimationCorrection(FightPrepareLayer layer, RectTransform root, RectTransform fight, RectTransform edit)
    {
        var selectors = (RectTransform)layer.MiddleArea.Find("Teams/mid/V");
        Require(selectors != null, "Preparation mode selector region is missing.");
        var controls = new[] { fight, edit, selectors };
        var original = controls.Select(control => Bounds(root, control)).ToArray();
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/DummyLayerSystem/FightPrepareLayer/"
            + (selectors.gameObject.activeSelf ? "SwitchModeMode.anim" : "EvolutionMode.anim"));
        Require(clip != null, "Missing preparation mode animation.");
        var title = Field<Text>(layer, "arcadeStageNoText");
        var originalColor = title.color;
        var sentinel = new Color(0.123f, 0.234f, 0.345f, 0.678f);
        try
        {
            // A full style/layout pass resets this color. The steady-state
            // animation correction should only restore the three positions.
            title.color = sentinel;
            clip.SampleAnimation(layer.gameObject, 0);
            InvokeLateUpdate(layer);
            Canvas.ForceUpdateCanvases();
            Require(title.color == sentinel, "Mode animation correction performs a full styling pass.");
            for (var index = 0; index < controls.Length; index++)
            {
                var restored = Bounds(root, controls[index]);
                Require(Vector2.Distance(original[index].position, restored.position) < 0.5f
                    && Vector2.Distance(original[index].size, restored.size) < 0.5f,
                    "Cached mode animation correction fails to restore " + controls[index].name);
            }
        }
        finally { title.color = originalColor; }
    }

    static void CheckHintVisibility(FightPrepareLayer layer, GameObject edit)
    {
        var hint = Field<Text>(layer, "teamEditIndicatorText");
        var indicator = Field<GameObject>(layer, "teamEditIndicator");
        var motto = Field<Text>(layer, "team1OneWord");
        var originalHint = hint.text;
        var originalMotto = motto.text;
        bool originalHintActive = hint.gameObject.activeSelf;
        bool originalIndicatorActive = indicator.activeSelf;
        bool originalEditActive = edit.activeSelf;
        try
        {
            hint.text = Translate.Get("HasExtraSeat");
            hint.gameObject.SetActive(true);
            indicator.SetActive(true);
            motto.text = "Arena fixture motto";
            InvokeLateUpdate(layer);
            Require(!hint.enabled, "Preparation hint overlaps a nonempty Arena motto.");
            motto.text = string.Empty;
            InvokeLateUpdate(layer);
            Require(hint.enabled == edit.activeInHierarchy, "Clearing Arena motto does not restore the appropriate hint visibility.");
            edit.SetActive(false);
            InvokeLateUpdate(layer);
            Require(!hint.enabled, "Preparation hint remains enabled when Edit is hidden.");
        }
        finally
        {
            hint.text = originalHint;
            motto.text = originalMotto;
            hint.gameObject.SetActive(originalHintActive);
            indicator.SetActive(originalIndicatorActive);
            edit.SetActive(originalEditActive);
            InvokeLateUpdate(layer);
        }
    }

    static void InvokeLateUpdate(FightPrepareLayer layer) => typeof(FightPrepareLayer)
        .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layer, null);

    static void CheckPortraitRow(RectTransform row, RectTransform panel)
    {
        var rowBounds = Bounds(panel, row);
        Require(Contains(panel.rect, rowBounds), "Portrait row escapes its panel: " + panel.name);
        var portraits = row.GetComponentsInChildren<HeroIcon>().Select(icon => Bounds(panel, (RectTransform)icon.transform)).ToArray();
        Require(portraits.Length > 0, "Portrait fixture is empty: " + panel.name);
        for (var index = 0; index < portraits.Length; index++)
        {
            var portrait = portraits[index];
            Require(Contains(rowBounds, portrait), "A portrait escapes its row: " + panel.name);
            Require(Mathf.Abs(portrait.width - portrait.height) < 0.5f, "A portrait is stretched.");
            Require(Mathf.Abs(portrait.center.y - portraits[0].center.y) < 0.5f && Mathf.Abs(portrait.width - portraits[0].width) < 0.5f,
                "Portraits differ in size or vertical alignment.");
            if (index > 0) Require(!portrait.Overlaps(portraits[index - 1]), "Portraits overlap.");
            if (index > 1) Require(Mathf.Abs((portrait.xMin - portraits[index - 1].xMax) - (portraits[1].xMin - portraits[0].xMax)) < 0.5f,
                "Portrait spacing is uneven.");
        }
        Require(Mathf.Abs(portraits[0].xMin - rowBounds.xMin) < 0.5f,
            "Portrait row does not align with its panel header.");
    }

    static void DestroyPreparation(GameObject instance)
    {
        if (instance == null) return;
        foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) LanguageConverterManger.List.Remove(converter);
        UnityEngine.Object.DestroyImmediate(instance);
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

    static T Field<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object
    {
        var type = owner.GetType();
        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) return (T)field.GetValue(owner);
            type = type.BaseType;
        }
        throw new MissingFieldException(owner.GetType().Name, name);
    }

    static Rect Bounds(RectTransform root, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }

    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 0.5f && inner.yMin >= outer.yMin - 0.5f
        && inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;

    static Sprite CreateLocalModelPreview(out Texture2D texture)
    {
        // A local prefab is instantiated in an isolated preview scene with every
        // behaviour disabled. Never call ShowModel or any account/asset loaders.
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Static Model Fixture");
        SceneManager.MoveGameObjectToScene(rig, scene);
        rig.SetActive(false);
        var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        texture = null;
        var previous = RenderTexture.active;
        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/Unit/human/haruka.prefab");
            Require(asset != null, "Missing local character model fixture.");
            var model = UnityEngine.Object.Instantiate(asset, rig.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, -20, 0);
            model.transform.localScale = Vector3.one;
            foreach (var behaviour in model.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/haruka/idle.anim");
            Require(idle != null, "Missing local character idle clip.");
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) idle.SampleAnimation(animator.gameObject, 0);
            model.transform.localRotation = Quaternion.Euler(0, -20, 0);
            var cameraNode = new GameObject("Static Model Camera", typeof(Camera));
            cameraNode.transform.SetParent(rig.transform, false);
            var camera = cameraNode.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100;
            var lightNode = new GameObject("Static Model Key Light", typeof(Light));
            lightNode.transform.SetParent(rig.transform, false);
            var light = lightNode.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(0.92f, 0.97f, 1);
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            rig.SetActive(true);
            var renderers = model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled
                && !(renderer is ParticleSystemRenderer)).ToArray();
            Require(renderers.Length > 0, "Local model fixture has no visible mesh.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.08f;
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.targetTexture = target;
            target.Create();
            RenderCamera(camera, target);
            RenderTexture.active = target;
            texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(rig);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Render(Camera camera, string path, Viewport viewport, RectTransform safe)
    {
        var width = viewport.width / 2;
        var height = viewport.height / 2;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        GameObject navigation = null;
        try
        {
            navigation = AddNavigationPreview(safe);
            target.Create();
            camera.targetTexture = target;
            RenderCamera(camera, target);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            DestroyPreparation(navigation);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void RenderCamera(Camera camera, RenderTexture target)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
