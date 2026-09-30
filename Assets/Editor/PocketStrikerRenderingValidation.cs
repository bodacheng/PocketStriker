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

public static class PocketStrikerRenderingValidation
{
    const string OutputDirectory = "Logs/Rendering";
    const string PalettePath = "Assets/OrganizedResources/InUse/D_Resources/PolygonFantasyHeroCharacters/Materials/Standard/PolygonFantasyHero_Texture_01_A.mat";

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string quality;
        public string pipeline;
        public string scope;
        public List<string> observations = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Check Character Rendering")]
    public static void Validate()
    {
        var errors = new List<string>();
        ValidateOffscreen(errors);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("POCKETSTRIKER_RENDERING_FIXTURE_PASSED: colored material clone, independent realtime lighting and shadow-only proxy. " + Path.GetFullPath(OutputDirectory));
    }

    // Called from startup smoke after the fighters and the battleground are ready.
    public static void ValidateLoadedFight(List<string> errors)
    {
        var report = NewReport("Live loaded battle: actual materials, ground camera, light masks, shadow proxy and receiver.");
        ValidateIOSDefault(report);
        var fighters = UnityEngine.Object.FindObjectsByType<Data_Center>(FindObjectsSortMode.None)
            .Where(fighter => fighter.gameObject.scene.name == "FightScene").ToArray();
        if (fighters.Length == 0) report.errors.Add("No loaded battle fighters.");
        foreach (var fighter in fighters)
        {
            var renderers = fighter.WholeT.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers.Where(item => item.gameObject.layer == 3 && !(item is ParticleSystemRenderer)))
            {
                foreach (var material in renderer.sharedMaterials.Where(item => item != null))
                {
                    report.observations.Add(renderer.name + " | " + material.name + " | " + material.shader.name
                        + " | texture=" + (material.mainTexture != null ? material.mainTexture.name : "none")
                        + " | keywords=" + string.Join(",", material.shaderKeywords));
                    if (!material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")
                        report.errors.Add(renderer.name + ": unsupported character shader " + material.shader.name);
                    if (material.shader.name == "PocketStriker/Character Shadow")
                        report.errors.Add(renderer.name + ": visible unit uses shadow material.");
                    if (material.shader.name == "OmniShade/Standard URP" && material.GetTexture("_MainTex") == null)
                        report.errors.Add(renderer.name + ": character palette texture is missing.");
                }
            }
            var shadows = renderers.Where(renderer => renderer.name.StartsWith("shadow_", StringComparison.Ordinal)).ToArray();
            if (shadows.Length == 0) report.errors.Add(fighter.name + ": missing ground-camera shadow proxy.");
            foreach (var shadow in shadows)
            {
                if (shadow.gameObject.layer != 0 || shadow.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                    report.errors.Add(shadow.name + ": proxy must be on Default and cast shadows only.");
                if (!shadow.sharedMaterials.All(material => material != null && material.FindPass("ShadowCaster") >= 0))
                    report.errors.Add(shadow.name + ": proxy material lacks a ShadowCaster pass.");
            }
        }
        var scene = SceneManager.GetSceneByName("FightScene");
        if (scene.IsValid())
        {
            var roots = scene.GetRootGameObjects();
            var lights = roots.SelectMany(root => root.GetComponentsInChildren<Light>()).Where(light => light.enabled).ToArray();
            report.observations.AddRange(lights.Select(light => $"light={light.name} intensity={light.intensity} mask={light.cullingMask} shadows={light.shadows}"));
            if (!lights.Any(light => light.type == LightType.Directional && light.intensity > 0
                && light.shadows != LightShadows.None && (light.cullingMask & 1) != 0))
                report.errors.Add("Battle has no shadow-casting directional light illuminating Default-layer receivers.");
            if (!lights.Any(light => light.intensity > 0 && (light.cullingMask & (1 << 3)) != 0))
                report.errors.Add("Battle has no active light illuminating the unit layer.");
            var ground = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>())
                .Where(renderer => renderer.enabled && !(renderer is ParticleSystemRenderer) && renderer.receiveShadows
                    && renderer.GetComponentInParent<Data_Center>() == null && renderer.gameObject.layer != 5).ToArray();
            if (ground.Length == 0) report.errors.Add("Battle has no active shadow receiver.");
            report.observations.AddRange(ground.Take(8).Select(renderer => "receiver=" + renderer.name
                + " shader=" + string.Join(",", renderer.sharedMaterials.Where(material => material != null).Select(material => material.shader.name))));
            var groundCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>())
                .FirstOrDefault(camera => camera.enabled && (camera.cullingMask & 1) != 0);
            if (groundCamera == null || !groundCamera.GetUniversalAdditionalCameraData().renderShadows)
                report.errors.Add("Ground camera does not render shadows.");
        }
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline && !pipeline.supportsMainLightShadows)
            report.errors.Add("Active URP quality disables the ground's directional shadows.");
        FinishReport(report, "live-fight.json");
        errors.AddRange(report.errors);
    }

    public static void ValidateOffscreen(List<string> errors)
    {
        var report = NewReport("Isolated URP render fixture with the actual fighter palette, no baked probes, material tint API and shadow proxy visibility.");
        ValidateIOSDefault(report);
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Rendering Validation");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        target.Create();
        var originalQuality = QualitySettings.GetQualityLevel();
        Material material = null;
        Texture2D actualPalette = null, colored = null, withProxy = null;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(PalettePath);
            if (source == null) throw new InvalidOperationException("Missing actual character palette material.");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(rig.transform);
            cube.layer = 3;
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = source;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var dummy = cube.AddComponent<DummyMesh>();
            typeof(AbstractShaderMesh).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dummy, null);
            material = renderer.sharedMaterial;
            if (material == source || material.GetTexture("_MainTex") != source.GetTexture("_MainTex")
                || !source.shaderKeywords.All(keyword => material.IsKeywordEnabled(keyword)))
                report.errors.Add("Runtime material clone lost the authored palette texture or shader keywords.");
            var lightObject = new GameObject("Realtime Light");
            lightObject.transform.SetParent(rig.transform);
            lightObject.transform.rotation = Quaternion.identity;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            light.cullingMask = ~0;
            var cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(rig.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -3);
            camera.orthographic = true;
            camera.orthographicSize = 0.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.15f, 0.2f, 1);
            camera.cullingMask = ~0;
            camera.targetTexture = target;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            actualPalette = Render(camera, target);
            var texturedPixels = actualPalette.GetPixels(56, 56, 144, 144);
            var colorPixels = texturedPixels.Count(pixel => Mathf.Max(pixel.r, pixel.g, pixel.b)
                - Mathf.Min(pixel.r, pixel.g, pixel.b) > 0.08f);
            report.observations.Add("Actual authored palette colored pixels=" + colorPixels);
            if (colorPixels < 500) report.errors.Add("Actual fighter palette rendered gray or black.");
            dummy.BaseColor = new Color(1, 0.1f, 0.05f, 1);
            if (material.GetColor("_Color") != dummy.BaseColor || dummy.BaseColor.r < 0.9f)
                report.errors.Add("Character tint does not reach OmniShade's _Color property.");
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.DisableKeyword("AMBIENT");
            material.DisableKeyword("FOG");
            material.EnableKeyword("DIFFUSE");
            colored = Render(camera, target);
            var center = colored.GetPixel(128, 128);
            report.observations.Add($"Unbaked realtime character center={center}");
            if (center.r < 0.3f || center.r < center.g * 2 || center.r < center.b * 2)
                report.errors.Add("Realtime character with no baked probes is black or lacks its authored tint.");

            // An explicit CustomProvided probe value separates a shader
            // regression from assumptions about a device's live probe data.
            renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
            var probeProperties = new MaterialPropertyBlock();
            probeProperties.CopyProbeOcclusionArrayFrom(new[] { Vector4.zero });
            renderer.SetPropertyBlock(probeProperties);
            var zeroProbeImage = Render(camera, target);
            try
            {
                var zeroProbeCenter = zeroProbeImage.GetPixel(128, 128);
                report.observations.Add("CustomProvided zero baked-probe realtime center=" + zeroProbeCenter);
                if (Mathf.Abs(zeroProbeCenter.r - center.r) > 0.05f || zeroProbeCenter.r < 0.3f)
                    report.errors.Add("Unshadowed realtime light incorrectly depends on baked probe occlusion.");
                Directory.CreateDirectory(OutputDirectory);
                File.WriteAllBytes(Path.Combine(OutputDirectory, "character-zero-baked-occlusion.png"), zeroProbeImage.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(zeroProbeImage); }
            renderer.SetPropertyBlock(null);
            renderer.lightProbeUsage = LightProbeUsage.Off;

            var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxy.transform.SetParent(rig.transform);
            proxy.transform.localScale = Vector3.one * 0.9f;
            var proxyRenderer = proxy.GetComponent<Renderer>();
            proxyRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/OrganizedResources/InUse/ExternalAssets/shadowMaterial.mat");
            proxyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            withProxy = Render(camera, target);
            var proxyCenter = withProxy.GetPixel(128, 128);
            if (Mathf.Abs(proxyCenter.r - center.r) > 0.05f || Mathf.Abs(proxyCenter.g - center.g) > 0.05f)
                report.errors.Add("Shadow proxy covered the colored preview with an opaque silhouette.");
            proxy.SetActive(false);

            // Low mobile tiers use PerVertex, higher tiers PerPixel. Both must
            // draw the same colored character when only an additional light is
            // present; a missing vertex keyword rendered this fixture black.
            light.enabled = false;
            var fillObject = new GameObject("Additional Point Light");
            fillObject.transform.SetParent(rig.transform);
            fillObject.transform.position = new Vector3(0, 0, -2);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 3;
            fill.range = 10;
            for (var quality = 0; quality < QualitySettings.names.Length; quality++)
            {
                QualitySettings.SetQualityLevel(quality, true);
                var image = Render(camera, target);
                try
                {
                    var fillCenter = image.GetPixel(128, 128);
                    report.observations.Add(QualitySettings.names[quality] + " additional-light center=" + fillCenter);
                    if (fillCenter.r < 0.2f || fillCenter.r < fillCenter.g * 2)
                        report.errors.Add(QualitySettings.names[quality] + ": additional-light character rendered black or gray.");
                    Directory.CreateDirectory(OutputDirectory);
                    File.WriteAllBytes(Path.Combine(OutputDirectory, "character-fill-quality-" + quality + ".png"), image.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllBytes(Path.Combine(OutputDirectory, "character-actual-palette.png"), actualPalette.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(OutputDirectory, "character-unbaked-light.png"), colored.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(OutputDirectory, "character-shadow-proxy.png"), withProxy.EncodeToPNG());
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            QualitySettings.SetQualityLevel(originalQuality, true);
            if (actualPalette != null) UnityEngine.Object.DestroyImmediate(actualPalette);
            if (colored != null) UnityEngine.Object.DestroyImmediate(colored);
            if (withProxy != null) UnityEngine.Object.DestroyImmediate(withProxy);
            UnityEngine.Object.DestroyImmediate(rig);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
            FinishReport(report, "offscreen.json");
        }
        errors.AddRange(report.errors);
    }

    static Texture2D Render(Camera camera, RenderTexture target)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP camera render request unsupported.");
        RenderPipeline.SubmitRenderRequest(camera, request);
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            return image;
        }
        finally { RenderTexture.active = previous; }
    }

    static Report NewReport(string scope) => new Report {
        unityVersion = Application.unityVersion,
        quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
        pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "built-in",
        scope = scope
    };

    static void ValidateIOSDefault(Report report)
    {
        // Platform defaults are Editor configuration. Unity does not expose
        // that serialized map/native lookup reliably while running a player
        // session in Play mode. Validate its actual active pipeline instead;
        // the stopped-Editor fixture separately verifies the iPhone default.
        if (Application.isPlaying)
        {
            var activeQuality = QualitySettings.GetQualityLevel();
            var activePipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            report.observations.Add($"Live runtime quality={activeQuality} ({QualitySettings.names[activeQuality]}) pipeline={activePipeline?.name}");
            if (activePipeline == null || activePipeline.mainLightRenderingMode == LightRenderingMode.Disabled
                || !activePipeline.supportsMainLightShadows)
                report.errors.Add("Active runtime URP must render a main light and ground shadows.");
            return;
        }
        var qualitySettings = new SerializedObject(QualitySettings.GetQualitySettings());
        var defaults = qualitySettings.FindProperty("m_PerPlatformDefaultQuality");
        var configured = false;
        for (var index = 0; index < defaults.arraySize; index++)
        {
            var entry = defaults.GetArrayElementAtIndex(index);
            if (entry.FindPropertyRelative("first").stringValue == "iPhone") configured = true;
        }
        if (!configured) report.errors.Add("iOS has no explicit default quality; mobile can select a tier without ground shadows.");
        var method = typeof(QualitySettings).GetMethod("GetDefaultQualityForPlatform", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
        {
            report.errors.Add("Cannot inspect Unity's native iPhone default quality.");
            return;
        }
        var quality = (int)method.Invoke(null, new object[] { "iPhone" });
        var levels = qualitySettings.FindProperty("m_QualitySettings");
        if (quality < 0 || quality >= levels.arraySize)
        {
            report.errors.Add("iPhone default quality index is invalid: " + quality);
            return;
        }
        var pipeline = levels.GetArrayElementAtIndex(quality).FindPropertyRelative("customRenderPipeline").objectReferenceValue
            as UniversalRenderPipelineAsset;
        if (pipeline == null) pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        report.observations.Add($"Native iPhone default quality={quality} ({QualitySettings.names[quality]}) pipeline={pipeline?.name}");
        if (pipeline == null || pipeline.mainLightRenderingMode == LightRenderingMode.Disabled || !pipeline.supportsMainLightShadows)
            report.errors.Add("iPhone default URP must render a main light and ground shadows.");
    }

    static void FinishReport(Report report, string name)
    {
        report.passed = report.errors.Count == 0;
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, name), JsonUtility.ToJson(report, true));
    }
}
