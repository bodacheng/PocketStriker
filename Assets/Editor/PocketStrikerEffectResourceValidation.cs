using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Checks imported skill visuals and the production camera depth required by their shaders.</summary>
public static class PocketStrikerEffectResourceValidation
{
    const string Output = "Logs/EffectResources";
    const string FightScenePath = "Assets/Scene/fight_scene/FightScene.unity";
    const string ConnectorPath = "Assets/P3/DedicatedCameraConnector.prefab";
    const int EffectLayer = 7;
    static readonly string[] Roots = { "Assets/ExternalAssets/HurtObjects", "Assets/ExternalAssets/Effects" };
    static readonly string[] RegressionEffects = { "ground_huge_green_explosion", "powergazer" };
    static readonly string[] VisibilityEffects = { "stormspray" };
    // Obsolete material-upgrade metadata retained by Unity/vendor materials; not render dependencies.
    static readonly HashSet<string> UpgradeMetadata = new HashSet<string>
    {
        "639247ca83abc874e893eb93af2b5e44", "d0353a89b1f911e48b9e16bdc9f2e058", "0406db5a14f94604a8c57ccfbc9f3b46"
    };

    [Serializable] public sealed class Report
    {
        public bool passed, currentScenesUnchanged, qualityRestored;
        public string unityVersion;
        public int prefabs, dependencies, renderers, particleSystems, materials;
        public CameraConfiguration worldCamera, unitCamera, presentationCamera;
        public List<DepthDependency> sceneDepthDependencies = new List<DepthDependency>();
        public List<string> errors = new List<string>();
        public List<string> observations = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<PixelSample> frames = new List<PixelSample>();
        public string scope = "All production HurtObjects/Effects prefabs: recursive visual GUID dependencies, imported renderer materials/meshes, enabled particle shape meshes, shader compile errors and unresolved imported references. Psychic Blast's authored animation events, weapon prefab and Addressables weapon-label entry are checked. ShaderGraphs containing SceneDepth are mapped to their dependent prefabs. Actual FightScene and DedicatedCameraConnector camera depth overrides and production renderer depth-copy timing are checked. Earth Curse at 350 ms and Spell Eruption at 150 ms are rendered with seed 42 in all six configured quality tiers, using the production world-to-UnitCamera overlay settings and an explicit depth-Off negative control. Psychic Blast is rendered at 350 ms from hand height with the production camera configuration in all six tiers; active particles and visible pixels are required. Particle shaders, textures and custom vertex streams are unchanged.";
        public string limitation = "Isolated editor URP camera-stack renders; no account, battle AI, remote bundles or physical-device GPU is exercised. Pixel comparisons measure the effect against a ground-only frame. Framing is deliberately wider than gameplay and does not assert static particle-shape bounds.";
    }

    [Serializable] public sealed class CameraConfiguration
    {
        public string source, name;
        public int cullingMask, rendererIndex;
        public float fieldOfView;
        public CameraRenderType renderType;
        public CameraOverrideOption depthOption, colorOption;
        public bool clearDepth, renderPostProcessing, renderShadows;
    }

    [Serializable] public sealed class DepthDependency
    {
        public string shaderGraph;
        public List<string> prefabs = new List<string>();
    }

    [Serializable] public sealed class PixelSample
    {
        public string effect, variant, quality, pipeline;
        public float seconds;
        public int visiblePixels, brightPixels, activeParticles;
        public bool pipelineDepthEnabled;
    }

    [MenuItem("PocketStriker/Validation/Skill Effect Resources")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException("Effect resources failed:\n" + string.Join("\n", report.errors.Take(40)));
        Debug.Log($"[EffectResources] PASS: {report.prefabs} prefabs, {report.materials} materials, {report.frames.Count} quality/depth frames. " + Path.GetFullPath(Output));
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before checking effects.");
        Directory.CreateDirectory(Output);
        var report = new Report { unityVersion = Application.unityVersion };
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var dirty = scenes.Select(scene => scene.isDirty).ToArray();
        var activeScene = SceneManager.GetActiveScene();
        int originalQuality = QualitySettings.GetQualityLevel();
        var originalPipeline = GraphicsSettings.defaultRenderPipeline;
        var originalTarget = RenderTexture.active;
        try
        {
            CheckPsychicBlastResource(report);
            CheckResources(report);
            ReadProductionCameras(report);
            if (QualitySettings.names.Length != 6) report.errors.Add("Expected the six production quality tiers.");
            for (int quality = 0; quality < QualitySettings.names.Length; quality++)
            {
                QualitySettings.SetQualityLevel(quality, true);
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline))
                {
                    report.errors.Add(QualitySettings.names[quality] + ": no active URP asset.");
                    continue;
                }
                report.observations.Add(QualitySettings.names[quality] + " | " + AssetDatabase.GetAssetPath(pipeline)
                    + " | pipelineDepth=" + pipeline.supportsCameraDepthTexture + " | UnitCamera override=" + report.unitCamera?.depthOption);
                if (report.worldCamera == null || report.unitCamera == null) continue;
                CheckDepthTiming(pipeline, report, quality);
                foreach (string effect in RegressionEffects.Concat(VisibilityEffects))
                    try { RenderEffect(effect, quality, pipeline, report); }
                    catch (Exception error) { report.errors.Add(QualitySettings.names[quality] + "/" + effect + ": " + error); }
            }
        }
        catch (Exception error) { report.errors.Add(error.ToString()); }
        finally
        {
            QualitySettings.SetQualityLevel(originalQuality, true);
            GraphicsSettings.defaultRenderPipeline = originalPipeline;
            RenderTexture.active = originalTarget;
            if (activeScene.IsValid() && activeScene.isLoaded && SceneManager.GetActiveScene() != activeScene)
                SceneManager.SetActiveScene(activeScene);
            report.qualityRestored = QualitySettings.GetQualityLevel() == originalQuality
                && GraphicsSettings.defaultRenderPipeline == originalPipeline && RenderTexture.active == originalTarget;
            report.currentScenesUnchanged = SceneManager.GetActiveScene() == activeScene && SceneManager.sceneCount == scenes.Length
                && scenes.Select((scene, index) => scene.IsValid() && scene.isLoaded && scene.isDirty == dirty[index]).All(value => value);
            if (!report.qualityRestored) report.errors.Add("Quality, graphics pipeline or render target was not restored.");
            if (!report.currentScenesUnchanged) report.errors.Add("The open scene state changed during validation.");
            int expectedFrames = QualitySettings.names.Length * (RegressionEffects.Length * 2 + VisibilityEffects.Length);
            report.passed = report.errors.Count == 0 && report.prefabs > 0
                && report.frames.Count == expectedFrames && report.screenshots.Count == expectedFrames;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckPsychicBlastResource(Report report)
    {
        const string animationPath = "Assets/ExternalAssets/Animations/human/skill/handfiref.anim";
        const string effectName = "stormspray";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationPath);
        if (clip == null || !clip.events.Any(e => e.functionName == "PrepareOneMagic" && e.stringParameter == effectName)
            || !clip.events.Any(e => e.functionName == "ReleasePreparedMagicToAir" && e.stringParameter == "left_hand"))
            report.errors.Add("Psychic Blast (164): missing authored prepare/release animation events.");

        string path = EffectPath(effectName);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null || prefab.GetComponent<Decomposition>()?._HitBox == null)
            report.errors.Add("Psychic Blast (164): missing stormspray weapon prefab or hitbox.");

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            report.errors.Add("Psychic Blast (164): Addressables settings are missing.");
            return;
        }
        var entries = new List<AddressableAssetEntry>();
        foreach (var group in settings.groups.Where(group => group != null))
            group.GatherAllAssets(entries, true, true, false);
        string address = EffectResourceKeyUtility.PrefabAddress("defaultmagic", effectName);
        if (!entries.Any(entry => entry.AssetPath == path && entry.address == address
            && entry.labels.Contains(EffectResourceKeyUtility.WeaponLabel)))
            report.errors.Add("Psychic Blast (164): stormspray is not reachable through the weapon Addressables label at " + address);
        else
            report.observations.Add("Psychic Blast (164): handfiref prepare/release events resolve to " + address + "; defaultmagic provides the fallback for all elements.");
    }

    static void CheckResources(Report report)
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", Roots).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        report.prefabs = paths.Length;
        var dependencies = AssetDatabase.GetDependencies(paths, true);
        report.dependencies = dependencies.Length;
        var depthGraphs = new HashSet<string>(dependencies.Where(path => path.EndsWith(".shadergraph", StringComparison.Ordinal)
            && File.Exists(path) && File.ReadAllText(path).Contains("UnityEditor.ShaderGraph.SceneDepthNode")), StringComparer.Ordinal);
        foreach (string graph in depthGraphs.OrderBy(path => path))
            report.sceneDepthDependencies.Add(new DepthDependency { shaderGraph = graph });
        foreach (var path in dependencies)
        {
            if (path.EndsWith(".prefab") || path.EndsWith(".mat")) CheckGuids(path, report);
            if (!path.EndsWith(".mat")) continue;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { report.errors.Add(path + ": material could not be imported."); continue; }
            report.materials++;
            CheckReferences(material, path, report);
            if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                report.errors.Add(path + ": missing/error shader.");
            else
            {
                if (!material.shader.isSupported) report.errors.Add(path + ": unsupported shader " + material.shader.name);
                foreach (var error in ShaderUtil.GetShaderMessages(material.shader).Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                    report.errors.Add(path + ": " + error.message);
            }
        }
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { report.errors.Add(path + ": prefab could not be imported."); continue; }
            var prefabDependencies = new HashSet<string>(AssetDatabase.GetDependencies(path, true), StringComparer.Ordinal);
            foreach (var dependency in report.sceneDepthDependencies)
                if (prefabDependencies.Contains(dependency.shaderGraph)) dependency.prefabs.Add(path);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                report.renderers++;
                CheckReferences(renderer, path + "/" + renderer.name, report);
                if (!renderer.enabled) continue;
                if (renderer is ParticleSystemRenderer particleRenderer && particleRenderer.renderMode == ParticleSystemRenderMode.None) continue;
                // Particle renderer slot 1 is optional only when trails are disabled.
                if (renderer.sharedMaterial == null)
                {
                    var controller = renderer is ParticleSystemRenderer ? renderer.GetComponent<ParticleSystem>() : null;
                    // MCombat uses emission-disabled root systems to control visible child systems.
                    if (controller != null && !controller.emission.enabled && !controller.trails.enabled
                        && controller.GetComponentsInChildren<ParticleSystem>(true).Length > 1)
                        report.observations.Add(path + "/" + renderer.name + ": non-emitting particle container has no material, as authored in MCombat.");
                    else report.errors.Add(path + "/" + renderer.name + ": missing renderer material.");
                }
                if (renderer is ParticleSystemRenderer trailRenderer && trailRenderer.GetComponent<ParticleSystem>().trails.enabled
                    && (trailRenderer.sharedMaterials.Length < 2 || trailRenderer.sharedMaterials[1] == null))
                    report.errors.Add(path + "/" + renderer.name + ": enabled trails have no material.");
                if (renderer is ParticleSystemRenderer meshParticles && meshParticles.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    var meshes = new Mesh[Mathf.Max(1, meshParticles.meshCount)];
                    int count = meshParticles.GetMeshes(meshes);
                    if (count == 0 || meshes.Take(count).Any(mesh => mesh == null)) report.errors.Add(path + "/" + renderer.name + ": missing particle render mesh.");
                }
            }
            foreach (var particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                report.particleSystems++;
                CheckReferences(particles, path + "/" + particles.name, report);
                var shape = particles.shape;
                if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Mesh && shape.mesh == null)
                    report.errors.Add(path + "/" + particles.name + ": enabled particle shape has no mesh.");
            }
            foreach (var mesh in prefab.GetComponentsInChildren<MeshFilter>(true)) CheckReferences(mesh, path + "/" + mesh.name, report);
        }
        foreach (string effect in RegressionEffects)
            if (!report.sceneDepthDependencies.Any(dependency => dependency.prefabs.Contains(EffectPath(effect))))
                report.errors.Add(effect + ": expected SceneDepth shader dependency was not identified.");
    }

    static void ReadProductionCameras(Report report)
    {
        var scene = EditorSceneManager.OpenPreviewScene(FightScenePath);
        try
        {
            var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
            var world = cameras.Single(camera => camera.name == "Main Camera");
            var unit = cameras.Single(camera => camera.name == "UnitCamera");
            report.worldCamera = ReadCamera(world, FightScenePath);
            report.unitCamera = ReadCamera(unit, FightScenePath);
            if (report.worldCamera.renderType != CameraRenderType.Base || report.unitCamera.renderType != CameraRenderType.Overlay)
                report.errors.Add("FightScene must render the world Base camera followed by UnitCamera Overlay.");
            var worldData = world.GetComponent<UniversalAdditionalCameraData>();
            if (!worldData.cameraStack.Contains(unit)) report.errors.Add("FightScene Main Camera stack does not include UnitCamera.");
            if (report.unitCamera.depthOption != CameraOverrideOption.On) report.errors.Add("FightScene UnitCamera must explicitly request a depth texture.");
            if ((report.unitCamera.cullingMask & (1 << EffectLayer)) == 0) report.errors.Add("FightScene UnitCamera does not render the skill effect layer.");
            if ((report.worldCamera.cullingMask & (1 << EffectLayer)) != 0) report.errors.Add("FightScene world camera unexpectedly renders the skill effect layer.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        var connector = AssetDatabase.LoadAssetAtPath<GameObject>(ConnectorPath);
        if (connector == null) { report.errors.Add("Missing presentation camera prefab: " + ConnectorPath); return; }
        var cameraSource = connector.GetComponentInChildren<Camera>(true);
        if (cameraSource == null) { report.errors.Add("Presentation prefab has no camera."); return; }
        report.presentationCamera = ReadCamera(cameraSource, ConnectorPath);
        if (report.presentationCamera.depthOption != CameraOverrideOption.On)
            report.errors.Add("DedicatedCameraConnector camera must explicitly request a depth texture.");
    }

    static CameraConfiguration ReadCamera(Camera camera, string source)
    {
        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) throw new InvalidOperationException(source + "/" + camera.name + ": missing URP camera data.");
        using var serialized = new SerializedObject(data);
        return new CameraConfiguration
        {
            source = source, name = camera.name, cullingMask = camera.cullingMask, fieldOfView = camera.fieldOfView,
            rendererIndex = serialized.FindProperty("m_RendererIndex").intValue, renderType = data.renderType,
            depthOption = data.requiresDepthOption, colorOption = data.requiresColorOption, clearDepth = data.clearDepth,
            renderPostProcessing = data.renderPostProcessing, renderShadows = data.renderShadows
        };
    }

    static void ConfigureCamera(Camera camera, CameraConfiguration source)
    {
        camera.cullingMask = source.cullingMask;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderType = source.renderType;
        data.requiresDepthOption = source.depthOption;
        data.requiresColorOption = source.colorOption;
        using (var serialized = new SerializedObject(data))
        {
            serialized.FindProperty("m_ClearDepth").boolValue = source.clearDepth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        data.renderPostProcessing = source.renderPostProcessing;
        data.renderShadows = source.renderShadows;
        data.SetRenderer(source.rendererIndex);
    }

    static void CheckDepthTiming(UniversalRenderPipelineAsset pipeline, Report report, int quality)
    {
        using var serializedPipeline = new SerializedObject(pipeline);
        var renderers = serializedPipeline.FindProperty("m_RendererDataList");
        int defaultRenderer = serializedPipeline.FindProperty("m_DefaultRendererIndex").intValue;
        foreach (int index in new[] { report.worldCamera.rendererIndex, report.unitCamera.rendererIndex }
                     .Select(index => index < 0 ? defaultRenderer : index).Distinct())
        {
            var renderer = renderers.GetArrayElementAtIndex(index).objectReferenceValue;
            using var serializedRenderer = new SerializedObject(renderer);
            var timing = (CopyDepthMode)serializedRenderer.FindProperty("m_CopyDepthMode").intValue;
            report.observations.Add(QualitySettings.names[quality] + " | " + AssetDatabase.GetAssetPath(renderer) + " | depthCopy=" + timing);
            if (timing == CopyDepthMode.AfterTransparents)
                report.errors.Add(QualitySettings.names[quality] + ": depth is copied after the transparent particles that need it.");
        }
    }

    static void CheckGuids(string path, Report report)
    {
        if (!File.Exists(path)) return;
        foreach (string guid in Regex.Matches(File.ReadAllText(path), @"guid: ([a-fA-F0-9]{32})").Cast<Match>().Select(match => match.Groups[1].Value).Distinct())
        {
            if (guid.StartsWith("0000000000000000")) continue;
            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) continue;
            if (UpgradeMetadata.Contains(guid)) { report.observations.Add(path + ": legacy material upgrade metadata " + guid); continue; }
            report.errors.Add(path + ": missing visual dependency GUID " + guid);
        }
    }

    static void CheckReferences(UnityEngine.Object asset, string label, Report report)
    {
        using var serialized = new SerializedObject(asset);
        var property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null
                && property.objectReferenceEntityIdValue != default)
                report.errors.Add(label + ": unresolved imported reference " + property.propertyPath);
    }

    static string EffectPath(string name) => Roots[0] + "/defaultmagic/" + name + ".prefab";

    static void RenderEffect(string name, int quality, UniversalRenderPipelineAsset pipeline, Report report)
    {
        bool compareDepth = RegressionEffects.Contains(name);
        // Eruption's authored bright column lasts only 170 ms; sample it while
        // it is alive rather than testing the later black-smoke aftermath.
        float sampleTime = name == "powergazer" ? .15f : .35f;
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Skill effect rendering validation");
        rig.SetActive(false);
        SceneManager.MoveGameObjectToScene(rig, scene);
        var texture = new RenderTexture(720, 720, 24, RenderTextureFormat.ARGB32);
        Material groundMaterial = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath(name));
            if (prefab == null) throw new InvalidOperationException("Missing production effect prefab.");
            var effect = UnityEngine.Object.Instantiate(prefab, rig.transform, false);
            if (name == "stormspray")
            {
                // Gameplay sets the prepared weapon's world position/rotation
                // from the left hand, replacing its source-editor placement.
                effect.transform.localPosition = new Vector3(0, 1, 0);
                effect.transform.localRotation = Quaternion.identity;
            }
            foreach (var script in effect.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var animator in effect.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var light in effect.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var node in effect.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = EffectLayer;
            var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particles in systems) { particles.useAutoRandomSeed = false; particles.randomSeed = 42; }

            var worldObject = new GameObject("World camera", typeof(Camera));
            worldObject.transform.SetParent(rig.transform, false);
            var world = worldObject.GetComponent<Camera>();
            world.scene = scene;
            world.enabled = false;
            world.clearFlags = CameraClearFlags.SolidColor;
            world.backgroundColor = new Color(.035f, .045f, .06f, 1);
            world.orthographic = false;
            world.fieldOfView = Mathf.Max(45, report.worldCamera.fieldOfView);
            world.nearClipPlane = .1f;
            world.farClipPlane = 100;
            world.transform.rotation = Quaternion.Euler(32, 0, 0);
            world.transform.position = new Vector3(0, 2.5f, 0) - world.transform.forward * 30;
            world.targetTexture = texture;
            ConfigureCamera(world, report.worldCamera);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(rig.transform, false);
            ground.transform.localScale = Vector3.one * 5;
            groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            groundMaterial.SetColor("_BaseColor", new Color(.1f, .12f, .14f, 1));
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            var overlayObject = new GameObject("UnitCamera overlay", typeof(Camera));
            overlayObject.transform.SetParent(rig.transform, false);
            var overlay = overlayObject.GetComponent<Camera>();
            overlay.CopyFrom(world);
            overlay.transform.SetPositionAndRotation(world.transform.position, world.transform.rotation);
            overlay.scene = scene;
            overlay.targetTexture = null;
            overlay.enabled = true;
            ConfigureCamera(overlay, report.unitCamera);
            world.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

            effect.SetActive(false);
            rig.SetActive(true);
            var baseline = ReadFrame(world, texture);
            effect.SetActive(true);
            foreach (var particles in systems) particles.Simulate(sampleTime, false, true, true);
            int activeParticles = systems.Sum(particles => particles.particleCount);
            if (activeParticles == 0) report.errors.Add(QualitySettings.names[quality] + "/" + name + ": the rendering fixture emitted no particles.");

            var overlayData = overlay.GetUniversalAdditionalCameraData();
            var worldData = world.GetUniversalAdditionalCameraData();
            PixelSample beforeOff = null, production = null;
            foreach (string variant in compareDepth ? new[] { "beforeOff", "productionOn" } : new[] { "productionOn" })
            {
                worldData.requiresDepthOption = variant == "beforeOff" ? CameraOverrideOption.Off : report.worldCamera.depthOption;
                overlayData.requiresDepthOption = variant == "beforeOff" ? CameraOverrideOption.Off : report.unitCamera.depthOption;
                string path = Path.Combine(Output, name + "-quality-" + quality + "-" + variant + "-" + Mathf.RoundToInt(sampleTime * 1000) + "ms.png");
                var colors = ReadFrame(world, texture, path);
                var sample = new PixelSample
                {
                    effect = name, variant = variant, quality = QualitySettings.names[quality], pipeline = AssetDatabase.GetAssetPath(pipeline),
                    pipelineDepthEnabled = pipeline.supportsCameraDepthTexture, seconds = sampleTime, activeParticles = activeParticles
                };
                for (int pixel = 0; pixel < colors.Length; pixel++)
                {
                    var actual = colors[pixel];
                    var previous = baseline[pixel];
                    if (Mathf.Abs(actual.r - previous.r) + Mathf.Abs(actual.g - previous.g) + Mathf.Abs(actual.b - previous.b) <= .04f) continue;
                    sample.visiblePixels++;
                    if (actual.maxColorComponent > .35f) sample.brightPixels++;
                }
                report.frames.Add(sample);
                report.screenshots.Add(path);
                if (variant == "beforeOff") beforeOff = sample;
                else production = sample;
            }
            if (production.visiblePixels < 500 || production.brightPixels < 25)
                report.errors.Add(QualitySettings.names[quality] + "/" + name + ": the production camera did not render the visible, illuminated effect.");
            if (compareDepth && production.brightPixels <= beforeOff.brightPixels)
                report.errors.Add(QualitySettings.names[quality] + "/" + name + ": depth-On did not restore more bright effect pixels than the depth-Off negative control.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig);
            if (groundMaterial != null) UnityEngine.Object.DestroyImmediate(groundMaterial);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static Color[] ReadFrame(Camera camera, RenderTexture texture, string path = null)
    {
        var originalTarget = RenderTexture.active;
        var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        try
        {
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            pixels.Apply();
            if (path != null) File.WriteAllBytes(path, pixels.EncodeToPNG());
            return pixels.GetPixels();
        }
        finally { RenderTexture.active = originalTarget; UnityEngine.Object.DestroyImmediate(pixels); }
    }
}
