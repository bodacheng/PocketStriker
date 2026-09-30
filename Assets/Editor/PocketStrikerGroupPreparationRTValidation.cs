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

/// <summary>Composes the actual Group page with its actual model render-texture surfaces.</summary>
public static class PocketStrikerGroupPreparationRTValidation
{
    const string Output = "Logs/UILayout/GroupPreparationRT";
    const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
    const string PrefabPath = "Assets/Resources/DummyLayerSystem/FightPrepareLayer/FightPrepareLayer_gb.prefab";

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public int modelRenders;
        public int skillSlotsChecked;
        public int portraitsChecked;
        public bool textureCleanup;
        public bool textureBindingsStable;
        public int transparentSamplesChecked;
        public List<ModelResult> models = new List<ModelResult>();
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
        public string scope = "Actual Group preparation prefab, RefreshGroupPreparationLayout, native 3/4 GangbangHeroIcon rosters, both nine-slot stoneModel grids and shared navigation. Bundled haruka and robot3 skeletal warrior meshes keep their original shared materials and idle poses. The authored DedicatedCameraConnector cameras use Initialize(false), their authored start directions, native aspect/orthographic fit, transparent ARGB32 textures and native RawImage composition. The final 540x960 Chinese screenshot is rendered by Unity; no model sprites or diagnostic replacement materials are present.";
        public string limitation = "Stopped-editor composition fixture with local meshes and UI data. Gameplay scripts are removed before model activation; account/Addressables/model-pool loaders and live animation are omitted. A neutral directional light and ambient fill support the original materials. Private population/data helpers from the existing preparation validator are reused, but all panel, portrait, button, model and grid layout comes from production.";
    }

    [Serializable]
    public sealed class ModelResult
    {
        public string prefab;
        public Vector2 textureSize;
        public Rect modelPixelsNormalized;
        public Rect modelUIPixels;
        public float compositionBrightnessRatio;
        public float silhouetteCoverage;
        public float materialBrightnessRange;
    }

    [MenuItem("PocketStriker/Validation/Group Preparation Real Model Composition")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("[GroupPreparationRT] PASS: " + Path.GetFullPath(Path.Combine(Output, "540x960-chinese.png")));
    }

    public static void ValidateBatch()
    {
        var report = Run();
        Debug.Log("[GroupPreparationRT] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before the Group composition fixture.");
        Directory.CreateDirectory(Output);
        var report = new Report();
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Group Preparation Real Model Validation");
        rig.SetActive(false); SceneManager.MoveGameObjectToScene(rig, scene);
        var oldCanvas = PosCal.Canvas; var oldSafe = PosCal.SafeAreaRect;
        var oldAccount = PlayerAccountInfo.Me; var oldLanguage = AppSetting.Value.Language;
        var oldPreScene = mainMenu.PreScene.target;
        var oldLimits = new[] { CommonSetting.GangbangModeMaxUnitPerTeam1, CommonSetting.GangbangModeMaxUnitPerTeam2, CommonSetting.GangbangModeMaxUnitPerTeam3 };
        var rows = Translate.GetRowList(); var oldRows = rows.ToArray();
        var languageProvider = typeof(Translate).GetField("languageProvider", StaticFields);
        var oldProvider = languageProvider.GetValue(null);
        var awardsField = typeof(PlayFabReadClient).GetField("_stageAward", StaticFields);
        var gangAwardsField = typeof(PlayFabReadClient).GetField("_gangbangAward", StaticFields);
        var oldAwards = awardsField.GetValue(null); var oldGangAwards = gangAwardsField.GetValue(null);
        var dragCanvas = typeof(HeroIcon).GetField("canvas", StaticFields); var oldDragCanvas = dragCanvas.GetValue(null);
        var oldAmbientMode = RenderSettings.ambientMode; var oldAmbient = RenderSettings.ambientLight;
        var oldSun = RenderSettings.sun;
        object skills = null;
        GangbangInfo stage = null;
        Texture2D composition = null;
        Texture2D backgroundOnly = null;
        var modelImages = new List<Texture2D>();
        var nativeTextures = new List<RenderTexture>();
        var modelMaterialSets = new Dictionary<Renderer, Material[]>();
        var sourcePaths = new[] { PrefabPath, "Assets/P3/DedicatedCameraConnector.prefab",
            "Assets/ExternalAssets/Unit/human/haruka.prefab", "Assets/ExternalAssets/Unit/human/robot3.prefab" };
        var sourceFiles = sourcePaths.ToDictionary(path => path, File.ReadAllText);
        try
        {
            mainMenu.PreScene.target = null;
            AppSetting.Value.Language = SystemLanguage.Chinese;
            PlayerAccountInfo.Me = new PlayerAccountInfo { tutorialProgress = "Finished", arcadeProcess = 9999, gangbangProcess = 9999 };
            rows.Clear();
            foreach (var row in CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/LanguageCode.csv")).Skip(1))
                if (row.Length >= 4) rows.Add(new Translate.Row { RECORD_ID = row[0], EN = row[1], JP = row[2], CH = row[3] });
            languageProvider.SetValue(null, (Func<SystemLanguage>)(() => AppSetting.Value.Language));
            var skillType = typeof(PocketStrikerFightPrepareValidation).GetNestedType("LocalSkillFixture", BindingFlags.NonPublic);
            Require(skillType != null, "Preparation local skill fixture is unavailable.");
            skills = Activator.CreateInstance(skillType, true);
            Invoke(skills, "Load"); Invoke(skills, "SetLanguage", SystemLanguage.Chinese);
            var common = new SerializedObject(AssetDatabase.LoadAssetAtPath<CommonSetting>("Assets/Setting/CommonSetting.asset"));
            CommonSetting.GangbangModeMaxUnitPerTeam1 = common.FindProperty("gangbangModeMaxUnitPerTeam1").intValue;
            CommonSetting.GangbangModeMaxUnitPerTeam2 = common.FindProperty("gangbangModeMaxUnitPerTeam2").intValue;
            CommonSetting.GangbangModeMaxUnitPerTeam3 = common.FindProperty("gangbangModeMaxUnitPerTeam3").intValue;
            var awards = new Dictionary<string, Award> { ["4"] = new Award { d = 10, g = 10 } };
            awardsField.SetValue(null, awards); gangAwardsField.SetValue(null, awards);

            var cameraObject = new GameObject("Group Composition Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 960; camera.aspect = 9f / 16;
            camera.transform.position = new Vector3(0, 0, -2000);
            camera.nearClipPlane = .01f; camera.farClipPlane = 4000;
            camera.cullingMask = (1 << 0) | (1 << 5); // Default-layer menu background plus UI; characters on layer 3 appear only through RawImages.
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .06f, 1);
            var canvasObject = new GameObject("Group Composition Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 5; canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvas.transform; canvasRect.sizeDelta = new Vector2(1080, 1920);
            var safeObject = new GameObject("Group Composition Safe Area", typeof(RectTransform));
            safeObject.layer = 5; safeObject.transform.SetParent(canvas.transform, false);
            var safe = (RectTransform)safeObject.transform; Stretch(safe);
            PosCal.Canvas = canvas; PosCal.SafeAreaRect = safe; dragCanvas.SetValue(null, canvas);
            PreparationHelper("AddBackground", canvas.transform);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "Missing actual Group preparation prefab.");
            var instance = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            var layer = instance.GetComponent<FightPrepareLayer>();
            Stretch((RectTransform)instance.transform);
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var localCamera in instance.GetComponentsInChildren<Camera>(true)) localCamera.enabled = false;
            foreach (var converter in instance.GetComponentsInChildren<LanguageConverter>(true)) converter.Change();
            var sourceStage = AssetDatabase.LoadAssetAtPath<GangbangInfo>("Assets/ExternalAssets/Stage/4.asset");
            Require(sourceStage != null, "Missing restored Group stage 4.");
            stage = FightInfo.Copy(sourceStage) as GangbangInfo;
            layer.SetGangbangFeature(stage, () => { }, "4", (team, id, count, limit) => count, (team, id) => 1);
            layer.SetFightMode(1);
            PreparationHelper("PopulateGroupPreparation", layer, (Sprite)null);
            foreach (var connector in new[] { Field<DedicatedCameraConnector>(layer, "connector"), Field<DedicatedCameraConnector>(layer, "connectorE") })
            {
                // Instantiate appends "(Clone)" to the enemy helper's stand-in.
                // Delete the entire family rather than leaving its null-sprite
                // Image to render an opaque white rectangle over the real RT.
                foreach (var placeholder in connector.GetComponentsInChildren<Image>(true)
                    .Where(item => item.name.StartsWith("Static Local Character Fixture", StringComparison.Ordinal)).ToArray())
                    UnityEngine.Object.DestroyImmediate(placeholder.gameObject);
            }
            for (int option = 1; option <= 3; option++) Field<GameObject>(layer, "countSelectedFrame" + option).SetActive(option == 1);
            layer.SelectedMaxTeamCount = CommonSetting.GangbangModeMaxUnitPerTeam1;
            layer.RefreshGroupPreparationLayout();
            PreparationHelper("FitPortraits", layer);
            var hero = Field<DedicatedCameraConnector>(layer, "connector");
            var enemy = Field<DedicatedCameraConnector>(layer, "connectorE");
            var heroModel = CreateLocalModel(hero, "haruka", modelMaterialSets);
            var enemyModel = CreateLocalModel(enemy, "robot3", modelMaterialSets);
            MatchSelectedPortrait(Field<RectTransform>(layer, "myTeamShowT"), 3);
            MatchSelectedPortrait(Field<RectTransform>(layer, "enemyTeamShowT"), 12);
            AddLights(rig.transform);
            rig.SetActive(true);
            InitializeModel(hero, heroModel, scene);
            InitializeModel(enemy, enemyModel, scene);
            layer.RefreshGroupPreparationLayout();
            Canvas.ForceUpdateCanvases();
            PreparationHelper("AddNavigationPreview", safe);
            var surfaces = new[] { hero, enemy }.Select(connector => connector.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "Model UI Presentation")).ToArray();
            Require(surfaces[0].texture != surfaces[1].texture, "Both previews share one texture.");
            foreach (var connector in new[] { hero, enemy })
            {
                Invoke(connector, "CameraPositionCal");
                var modelCamera = Field<Camera>(connector, "camera");
                Require(modelCamera.targetTexture != null && modelCamera.targetTexture.format == RenderTextureFormat.ARGB32,
                    "Model is not shown through its production transparent RT.");
                nativeTextures.Add(modelCamera.targetTexture);
                RenderCamera(modelCamera, modelCamera.targetTexture);
                modelImages.Add(ReadPixels(modelCamera.targetTexture));
            }
            Save(modelImages[0], "native-haruka.png", report);
            Save(modelImages[1], "native-robot3.png", report);
            CheckSurfaceBinding(hero, surfaces[0]); CheckSurfaceBinding(enemy, surfaces[1]);
            foreach (var surface in surfaces) surface.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            backgroundOnly = RenderToPixels(camera, 540, 960);
            foreach (var surface in surfaces) surface.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            composition = RenderToPixels(camera, 540, 960);
            Save(composition, "540x960-chinese.png", report);
            CheckSurfaceBinding(hero, surfaces[0]); CheckSurfaceBinding(enemy, surfaces[1]);
            report.textureBindingsStable = true;
            CheckModel(hero, "haruka", modelImages[0], composition, backgroundOnly, canvasRect, report);
            CheckModel(enemy, "robot3", modelImages[1], composition, backgroundOnly, canvasRect, report);
            CheckVisibleUI(layer, canvasRect, composition, report);
            foreach (var pair in modelMaterialSets)
                Require(pair.Key.sharedMaterials.SequenceEqual(pair.Value), "Fixture replaced the original character materials.");
            foreach (var path in sourcePaths) Require(File.ReadAllText(path) == sourceFiles[path], "Fixture changed source asset: " + path);
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            foreach (var converter in rig.GetComponentsInChildren<LanguageConverter>(true)) LanguageConverterManger.List.Remove(converter);
            // Non-ExecuteAlways behaviours in a preview scene do not reliably
            // receive edit-mode teardown. Exercise production cleanup explicitly;
            // its null guards also make a second Unity OnDestroy harmless.
            foreach (var connector in rig.GetComponentsInChildren<DedicatedCameraConnector>(true)) Invoke(connector, "OnDestroy");
            report.textureCleanup = nativeTextures.Count == 2 && nativeTextures.All(texture => texture == null);
            if (nativeTextures.Any(texture => texture != null)) report.errors.Add("Native connector render textures leaked during editor fixture teardown.");
            UnityEngine.Object.DestroyImmediate(rig);
            foreach (var image in modelImages) if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (composition != null) UnityEngine.Object.DestroyImmediate(composition);
            if (backgroundOnly != null) UnityEngine.Object.DestroyImmediate(backgroundOnly);
            if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            (skills as IDisposable)?.Dispose();
            PlayerAccountInfo.Me = oldAccount; AppSetting.Value.Language = oldLanguage;
            mainMenu.PreScene.target = oldPreScene; PosCal.Canvas = oldCanvas; PosCal.SafeAreaRect = oldSafe;
            dragCanvas.SetValue(null, oldDragCanvas);
            CommonSetting.GangbangModeMaxUnitPerTeam1 = oldLimits[0]; CommonSetting.GangbangModeMaxUnitPerTeam2 = oldLimits[1]; CommonSetting.GangbangModeMaxUnitPerTeam3 = oldLimits[2];
            rows.Clear(); rows.AddRange(oldRows); languageProvider.SetValue(null, oldProvider);
            awardsField.SetValue(null, oldAwards); gangAwardsField.SetValue(null, oldGangAwards);
            RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientLight = oldAmbient; RenderSettings.sun = oldSun;
            EditorSceneManager.ClosePreviewScene(scene);
        }
        report.passed = report.errors.Count == 0 && report.modelRenders == 2 && report.skillSlotsChecked == 18 && report.portraitsChecked == 7
            && report.screenshots.Count == 3 && report.textureCleanup && report.textureBindingsStable && report.transparentSamplesChecked >= 16;
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        return report;
    }

    static GameObject CreateLocalModel(DedicatedCameraConnector connector, string name, IDictionary<Renderer, Material[]> materials)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/Unit/human/" + name + ".prefab");
        Require(source != null, "Missing local model: " + name);
        var model = UnityEngine.Object.Instantiate(source, connector.transform, false);
        foreach (var centre in model.GetComponentsInChildren<Data_Center>(true)) UnityEngine.Object.DestroyImmediate(centre);
        foreach (var link in model.GetComponentsInChildren<OutsideDataLink>(true)) UnityEngine.Object.DestroyImmediate(link);
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
        foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/" + name + "/idle.anim");
        Require(idle != null, "Missing native idle animation for " + name);
        foreach (var animator in model.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
            if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) idle.SampleAnimation(animator.gameObject, 0);
        }
        foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 3;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) continue;
            materials[renderer] = renderer.sharedMaterials.ToArray();
            if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }
        model.transform.position = Field<Vector3>(connector, "modelPos");
        return model;
    }

    static void InitializeModel(DedicatedCameraConnector connector, GameObject model, Scene scene)
    {
        // Same fit mode and settled starting direction as _ShowModel, without its loaders or tween.
        Invoke(connector, "Initialize", false, model.transform, connector.transform);
        connector.RotateTarget(Field<float>(connector, "directionY"), 0);
        Invoke(connector, "CameraPositionCal");
        var camera = Field<Camera>(connector, "camera"); camera.scene = scene; camera.enabled = false;
        Field<Text>(connector, "unitName").gameObject.SetActive(false);
    }

    static void MatchSelectedPortrait(RectTransform roster, int id)
    {
        var icon = roster.GetComponentsInChildren<GangbangHeroIcon>(true).First();
        var sprite = (Sprite)PreparationHelper("LocalSprite", "Assets/OrganizedResources/InUse/ExternalAssets/Unit_Icon/", id);
        Field<Image>(icon, "icon").sprite = sprite;
        icon.RefreshPresentationSize();
    }

    static void AddLights(Transform rig)
    {
        var node = new GameObject("Neutral model preview light", typeof(Light)); node.transform.SetParent(rig, false);
        var light = node.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.25f;
        light.color = new Color(.96f, .97f, 1); light.cullingMask = 1 << 3; light.shadows = LightShadows.None;
        // Authored connector cameras look from +Z toward the character. Light
        // the visible face from that side; the previous -Z key was backlighting.
        node.transform.rotation = Quaternion.Euler(35, 155, 0);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.32f, .34f, .38f);
        RenderSettings.sun = light;
    }

    static void CheckSurfaceBinding(DedicatedCameraConnector connector, RawImage surface)
    {
        var camera = Field<Camera>(connector, "camera");
        Require(surface.texture != null && surface.texture == camera.targetTexture
            && camera.targetTexture.IsCreated(), "Native RawImage lost or replaced its created camera texture: " + connector.name);
        Require(surface.color == Color.white && !surface.raycastTarget && surface.transform.GetSiblingIndex() == 0,
            "Native model surface tint, hit target or sibling order changes the composition.");
    }

    static void CheckModel(DedicatedCameraConnector connector, string name, Texture2D rendered, Texture2D composed,
        Texture2D backgroundOnly, RectTransform canvas, Report report)
    {
        var colors = rendered.GetPixels();
        var opaque = Enumerable.Range(0, colors.Length).Where(index => colors[index].a > .2f).ToArray();
        Require(opaque.Length > 100, "Original-material model renders no visible pixels: " + name);
        int left = opaque.Min(index => index % rendered.width), right = opaque.Max(index => index % rendered.width);
        int bottom = opaque.Min(index => index / rendered.width), top = opaque.Max(index => index / rendered.width);
        var body = Rect.MinMaxRect((float)left / rendered.width, (float)bottom / rendered.height,
            (right + 1f) / rendered.width, (top + 1f) / rendered.height);
        Require(left > 0 && right < rendered.width - 1 && bottom > 0 && top < rendered.height - 1, "Native model fit clips the silhouette: " + name);
        Require(body.height > .35f && body.width > .10f, "Native model preview is too small within its allocated connector: " + name + " " + body);
        for (int x = 0; x < rendered.width; x++) Require(colors[x].a < .02f && colors[(rendered.height - 1) * rendered.width + x].a < .02f,
            "Native model RT has an opaque horizontal frame edge: " + name);
        for (int y = 0; y < rendered.height; y++) Require(colors[y * rendered.width].a < .02f && colors[y * rendered.width + rendered.width - 1].a < .02f,
            "Native model RT has an opaque vertical frame edge: " + name);
        float coverage = (float)opaque.Length / ((right - left + 1) * (top - bottom + 1));
        Require(coverage > .08f && coverage < .88f, "Native model pixels form a rectangle rather than a character silhouette: " + name + " coverage=" + coverage);
        var materialPixels = opaque.Where(index => colors[index].a > .98f).Select(index => colors[index]).ToArray();
        Require(materialPixels.Length > 100, "Native mesh has too few fully opaque material pixels: " + name);
        float brightnessRange = materialPixels.Max(color => color.maxColorComponent) - materialPixels.Min(color => color.maxColorComponent);
        int colorCount = materialPixels.Select(color => new Color32((byte)Mathf.RoundToInt(color.r * 31),
            (byte)Mathf.RoundToInt(color.g * 31), (byte)Mathf.RoundToInt(color.b * 31), 255)).Distinct().Count();
        Require(brightnessRange > .08f && colorCount >= 12, "Native model lacks visible material/mesh shading detail: " + name);
        bool Interior(int index)
        {
            int x = index % rendered.width, y = index / rendered.width;
            if (x < 2 || x >= rendered.width - 2 || y < 2 || y >= rendered.height - 2) return false;
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                if (colors[(y + dy) * rendered.width + x + dx].a < .95f) return false;
            return true;
        }
        var candidates = opaque.Where(index => colors[index].a > .98f && colors[index].maxColorComponent > .12f && Interior(index)).ToArray();
        Require(candidates.Length > 0, "Original materials are black in the preview: " + name);
        int sample = candidates.OrderBy(index => Mathf.Abs(index % rendered.width - rendered.width * .5f)
            + Mathf.Abs(index / rendered.width - rendered.height * .5f)).First();
        var samplePoint = new Vector2((sample % rendered.width + .5f) / rendered.width, (sample / rendered.width + .5f) / rendered.height);
        var connectorBounds = Bounds(canvas, (RectTransform)connector.transform);
        var point = connectorBounds.min + Vector2.Scale(connectorBounds.size, samplePoint);
        var composedPixel = Pixel(composed, canvas.rect, point);
        float ratio = composedPixel.maxColorComponent / Mathf.Max(.001f, colors[sample].maxColorComponent);
        Require(ratio > .65f && ratio < 1.55f, "UI composition darkens or replaces the original-material preview: " + name + " ratio=" + ratio);
        Require(ColorDistance(composedPixel, colors[sample]) < .25f,
            "Composed model pixel differs from its native material texture: " + name);
        int transparentSamples = 0;
        foreach (float x in new[] { .03f, .10f, .90f, .97f })
        foreach (float y in new[] { .03f, .10f, .90f, .97f })
        {
            int rx = Mathf.Clamp(Mathf.FloorToInt(x * rendered.width), 0, rendered.width - 1);
            int ry = Mathf.Clamp(Mathf.FloorToInt(y * rendered.height), 0, rendered.height - 1);
            if (colors[ry * rendered.width + rx].a > .01f) continue;
            var transparentPoint = connectorBounds.min + Vector2.Scale(connectorBounds.size, new Vector2(x, y));
            Require(ColorDistance(Pixel(composed, canvas.rect, transparentPoint), Pixel(backgroundOnly, canvas.rect, transparentPoint)) < .07f,
                "Transparent model RT area becomes an opaque UI rectangle: " + name + "/" + x + "," + y);
            transparentSamples++;
        }
        Require(transparentSamples >= 8, "Native model has too few clear-area samples: " + name);
        report.transparentSamplesChecked += transparentSamples;
        var pixelBounds = Rect.MinMaxRect(connectorBounds.xMin + body.xMin * connectorBounds.width,
            connectorBounds.yMin + body.yMin * connectorBounds.height,
            connectorBounds.xMin + body.xMax * connectorBounds.width,
            connectorBounds.yMin + body.yMax * connectorBounds.height);
        pixelBounds.position = (pixelBounds.position - canvas.rect.min) * .5f; pixelBounds.size *= .5f;
        report.models.Add(new ModelResult { prefab = name, textureSize = new Vector2(rendered.width, rendered.height),
            modelPixelsNormalized = body, modelUIPixels = pixelBounds, compositionBrightnessRatio = ratio,
            silhouetteCoverage = coverage, materialBrightnessRange = brightnessRange });
        report.modelRenders++;
    }

    static void CheckVisibleUI(FightPrepareLayer layer, RectTransform canvas, Texture2D image, Report report)
    {
        foreach (var name in new[] { "team1Name", "team2Name" })
        {
            var text = Field<Text>(layer, name);
            Require(text.gameObject.activeInHierarchy && text.enabled && !string.IsNullOrWhiteSpace(text.text), "Group team heading is invisible: " + name);
        }
        var rosters = new[] { Field<RectTransform>(layer, "myTeamShowT"), Field<RectTransform>(layer, "enemyTeamShowT") };
        for (int index = 0; index < rosters.Length; index++)
        {
            var children = rosters[index].Cast<Transform>().Where(child => child.gameObject.activeSelf).ToArray();
            Require(children.Length == (index == 0 ? 3 : 4) && children.All(child => child.GetComponent<GangbangHeroIcon>() != null),
                "Group roster contains leftover or duplicate portraits.");
            report.portraitsChecked += children.Length;
        }
        var preview = Field<RectTransform>(layer, "_groupPreview");
        var models = new[] { Field<DedicatedCameraConnector>(layer, "connector"), Field<DedicatedCameraConnector>(layer, "connectorE") };
        foreach (var nine in new[] { Field<NineForShow>(layer, "nineForShow"), Field<NineForShow>(layer, "nineForShowE") })
        {
            Require(nine.gameObject.activeInHierarchy && nine.AllButton().Count == 9, "A complete Group nine-slot grid is hidden.");
            var gridBounds = Bounds(canvas, (RectTransform)nine.transform);
            Require(Contains(Bounds(canvas, preview), gridBounds), "Group skill grid escapes its model/skill region.");
            foreach (var model in models) Require(!gridBounds.Overlaps(Bounds(canvas, (RectTransform)model.transform)), "Model connector covers a nine-slot grid.");
            foreach (var button in nine.AllButton())
            {
                var stone = button.GetComponentInChildren<SKStoneItem>(true);
                Require(button.gameObject.activeInHierarchy && stone != null && stone.image.enabled && stone.image.sprite != null
                    && stone.image.gameObject.activeInHierarchy, "Skill stone is missing or hidden.");
                var bounds = Bounds(canvas, stone.image.rectTransform);
                Require(Contains(gridBounds, bounds), "Skill stone art escapes its grid.");
                Require(MaxBrightness(image, canvas.rect, bounds) > .40f, "Skill stone has no visible rendered art: " + button.name);
                report.skillSlotsChecked++;
            }
        }
        Require(layer.GetComponentsInChildren<Image>(true).All(item => !item.name.StartsWith("Static Local Character Fixture", StringComparison.Ordinal)),
            "Sprite stand-in or its Clone remains in the composition.");
        for (int option = 1; option <= 3; option++)
        {
            var text = Field<Text>(layer, "groupCount" + option);
            int expected = option == 1 ? CommonSetting.GangbangModeMaxUnitPerTeam1 : option == 2 ? CommonSetting.GangbangModeMaxUnitPerTeam2 : CommonSetting.GangbangModeMaxUnitPerTeam3;
            Require(text.gameObject.activeInHierarchy && text.enabled && text.text == expected.ToString() && expected > 0, "Group population option is missing or zero.");
        }
    }

    static float MaxBrightness(Texture2D image, Rect canvas, Rect region)
    {
        int left = Mathf.Clamp(Mathf.FloorToInt((region.xMin - canvas.xMin) / canvas.width * image.width), 0, image.width - 1);
        int right = Mathf.Clamp(Mathf.CeilToInt((region.xMax - canvas.xMin) / canvas.width * image.width), 0, image.width - 1);
        int bottom = Mathf.Clamp(Mathf.FloorToInt((region.yMin - canvas.yMin) / canvas.height * image.height), 0, image.height - 1);
        int top = Mathf.Clamp(Mathf.CeilToInt((region.yMax - canvas.yMin) / canvas.height * image.height), 0, image.height - 1);
        float value = 0;
        for (int y = bottom; y <= top; y++) for (int x = left; x <= right; x++) value = Mathf.Max(value, image.GetPixel(x, y).maxColorComponent);
        return value;
    }

    static Color Pixel(Texture2D image, Rect canvas, Vector2 point) => image.GetPixel(
        Mathf.Clamp(Mathf.FloorToInt((point.x - canvas.xMin) / canvas.width * image.width), 0, image.width - 1),
        Mathf.Clamp(Mathf.FloorToInt((point.y - canvas.yMin) / canvas.height * image.height), 0, image.height - 1));

    static float ColorDistance(Color first, Color second) => Mathf.Max(Mathf.Abs(first.r - second.r),
        Mathf.Max(Mathf.Abs(first.g - second.g), Mathf.Abs(first.b - second.b)));

    static Texture2D RenderToPixels(Camera camera, int width, int height)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
        var old = camera.targetTexture;
        try { camera.targetTexture = target; RenderCamera(camera, target); return ReadPixels(target); }
        finally { camera.targetTexture = old; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }

    static Texture2D ReadPixels(RenderTexture target)
    {
        var old = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); return image;
        }
        finally { RenderTexture.active = old; }
    }

    static void RenderCamera(Camera camera, RenderTexture target)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
    }

    static void Save(Texture2D image, string name, Report report)
    {
        string path = Path.Combine(Output, name); File.WriteAllBytes(path, image.EncodeToPNG()); report.screenshots.Add(path);
    }

    static object PreparationHelper(string name, params object[] args)
        => typeof(PocketStrikerFightPrepareValidation).GetMethod(name, StaticFields).Invoke(null, args);
    static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, InstanceFields).Invoke(owner, args);
    static T Field<T>(object owner, string name)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, InstanceFields);
            if (field != null) return (T)field.GetValue(owner);
        }
        throw new MissingFieldException(owner.GetType().Name, name);
    }
    static Rect Bounds(RectTransform root, RectTransform child)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .5f && inner.yMin >= outer.yMin - .5f
        && inner.xMax <= outer.xMax + .5f && inner.yMax <= outer.yMax + .5f;
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
