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

/// <summary>Render ground shadows for both teams at the production crowd fit and a distant camera.</summary>
public static class PocketStrikerShadowCoverageValidation
{
    const string Output = "Logs/Rendering/ShadowCoverage";

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string scope = "Every production quality including iPhone Ultra, Low and Very Low: 12/24/48 fighters per team at the Group camera fit, plus 48 per team at >= 250 camera distance. Compare actual shadow-only proxy pixels to a disabled-proxy control. Verify per-quality copies, fade coverage, unchanged source budgets and restoring every override, including default-pipeline fallback.";
        public string limitation = "Stopped-editor URP render fixture using primitive bodies and the production shadow material. It does not simulate combat or measure device frame time; live battle coverage is checked by startup smoke.";
        public List<Sample> samples = new List<Sample>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class Sample
    {
        public string quality, layout;
        public int fightersPerTeam, totalFighters, shadowedFighters, changedPixels, atlasResolution;
        public float cameraDistance, receiverDistance, appliedDistance, fullyShadowedDistance;
    }

    [MenuItem("PocketStriker/Validation/Battle Shadow Coverage")]
    public static void Validate()
    {
        var errors = new List<string>();
        ValidateOffscreen(errors);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("POCKETSTRIKER_SHADOW_COVERAGE_PASSED: " + Path.GetFullPath(Output));
    }

    public static void ValidateOffscreen(List<string> errors)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Shadow coverage fixture requires a stopped editor.");
        Directory.CreateDirectory(Output);
        var report = new Report { unityVersion = Application.unityVersion };
        int originalQuality = QualitySettings.GetQualityLevel();
        var originalOverrides = Enumerable.Range(0, QualitySettings.names.Length)
            .Select(QualitySettings.GetRenderPipelineAssetAt).ToArray();
        var sources = originalOverrides.Select(asset => asset != null ? asset : GraphicsSettings.defaultRenderPipeline)
            .Cast<UniversalRenderPipelineAsset>().ToArray();
        var originalDistances = sources.Select(asset => asset.shadowDistance).ToArray();
        var originalBorders = sources.Select(asset => asset.cascadeBorder).ToArray();
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Crowd shadow coverage fixture");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var coverage = new BattleShadowCoverage();
        var target = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
        target.Create();
        Material groundMaterial = null;
        try
        {
            var camera = new GameObject("Ground camera").AddComponent<Camera>();
            camera.transform.SetParent(rig.transform);
            camera.scene = scene;
            camera.enabled = false;
            camera.aspect = 9f / 16;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 1000;
            camera.fieldOfView = BattleCameraProfiles.Group.FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            camera.cullingMask = 1;
            camera.targetTexture = target;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = true;
            cameraData.renderPostProcessing = false;

            var light = new GameObject("Ground directional light").AddComponent<Light>();
            light.transform.SetParent(rig.transform);
            light.transform.rotation = Quaternion.Euler(90, 0, 0);
            light.type = LightType.Directional;
            light.intensity = 1;
            light.cullingMask = 1;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = .8f;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(rig.transform);
            ground.transform.localScale = Vector3.one * 100;
            groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.SetColor("_BaseColor", Color.gray);
            var groundRenderer = ground.GetComponent<Renderer>();
            groundRenderer.sharedMaterial = groundMaterial;
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            groundRenderer.receiveShadows = true;
            groundRenderer.lightProbeUsage = LightProbeUsage.Off;
            groundRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/OrganizedResources/InUse/ExternalAssets/shadowMaterial.mat");
            if (shadowMaterial == null) throw new InvalidOperationException("Missing production character shadow material.");
            var proxies = new List<Renderer>();
            for (int index = 0; index < 96; index++)
            {
                var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                proxy.name = "shadow_fixture_" + index;
                proxy.transform.SetParent(rig.transform);
                proxy.transform.localScale = new Vector3(1.2f, 2.4f, 1.2f);
                var renderer = proxy.GetComponent<Renderer>();
                renderer.sharedMaterial = shadowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.receiveShadows = false;
                proxies.Add(renderer);
            }

            foreach (int count in new[] { 12, 24, 48 })
            {
                var bounds = new List<Bounds>();
                int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
                for (int index = 0; index < proxies.Count; index++)
                {
                    var proxy = proxies[index];
                    proxy.gameObject.SetActive(index < count * 2);
                    if (index >= count * 2) continue;
                    int slot = index % count;
                    float side = index < count ? -1 : 1;
                    var position = new Vector3((slot % columns - (columns - 1) * .5f) * 2,
                        1.2f, side * (6 + slot / columns * 2));
                    proxy.transform.position = position;
                    bounds.Add(new Bounds(position, proxy.transform.localScale));
                }
                var fit = BattleCameraFraming.CalculatePose(bounds, camera.aspect, camera.fieldOfView,
                    new Rect(.04f, .22f, .92f, .58f), BattleCameraProfiles.Group.Pitch);
                foreach (bool distant in count == 48 ? new[] { false, true } : new[] { false })
                {
                    var pose = distant ? BattleCameraFraming.WithDistance(fit,
                        Mathf.Max(250, fit.Distance * 3), camera.aspect, camera.fieldOfView) : fit;
                    camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                    for (int quality = 0; quality < QualitySettings.names.Length; quality++)
                    {
                        QualitySettings.SetQualityLevel(quality, true);
                        var source = sources[quality];
                        string label = "team-" + count + (distant ? "-distant" : "-fitted") + "-quality-" + quality;
                        float receiverDistance = BattleShadowCoverage.ReceiverDistance(pose.Position, bounds);
                        coverage.Update(camera, receiverDistance, .05f);
                        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                        if (pipeline == null) throw new InvalidOperationException(label + ": no URP.");
                        var sample = new Sample { quality = QualitySettings.names[quality], layout = label,
                            fightersPerTeam = count, totalFighters = count * 2, cameraDistance = pose.Distance,
                            receiverDistance = receiverDistance, appliedDistance = pipeline.shadowDistance,
                            fullyShadowedDistance = pipeline.shadowDistance * (1 - pipeline.cascadeBorder),
                            atlasResolution = pipeline.mainLightShadowmapResolution };
                        report.samples.Add(sample);
                        if (pipeline == source || pipeline.shadowDistance <= 0
                            || sample.fullyShadowedDistance < receiverDistance)
                            report.errors.Add(label + ": runtime copy does not cover all casters and ground receivers before fade.");
                        if (!pipeline.supportsMainLightShadows || pipeline.mainLightRenderingMode == LightRenderingMode.Disabled)
                            report.errors.Add(label + ": quality disables the main light/shadows.");
                        if (pipeline.mainLightShadowmapResolution != source.mainLightShadowmapResolution
                            || pipeline.shadowCascadeCount != source.shadowCascadeCount)
                            report.errors.Add(label + ": runtime coverage increased the selected quality's atlas/cascade budget.");

                        foreach (var proxy in proxies) proxy.enabled = false;
                        var control = Render(camera, target);
                        foreach (var proxy in proxies) proxy.enabled = true;
                        var shadowed = Render(camera, target);
                        try
                        {
                            var controlPixels = control.GetPixels();
                            var shadowPixels = shadowed.GetPixels();
                            for (int pixel = 0; pixel < controlPixels.Length; pixel++)
                                if (controlPixels[pixel].grayscale - shadowPixels[pixel].grayscale > .025f)
                                    sample.changedPixels++;
                            foreach (var body in bounds)
                            {
                                var groundPoint = new Vector3(body.center.x, 0, body.center.z);
                                var screen = camera.WorldToScreenPoint(groundPoint);
                                bool found = false;
                                // Search a small footprint around the projected ground
                                // contact, allowing the selected tier's atlas texel size.
                                for (int y = Mathf.Max(0, (int)screen.y - 4); y <= Mathf.Min(target.height - 1, (int)screen.y + 4); y++)
                                for (int x = Mathf.Max(0, (int)screen.x - 4); x <= Mathf.Min(target.width - 1, (int)screen.x + 4); x++)
                                    if (controlPixels[y * target.width + x].grayscale
                                        - shadowPixels[y * target.width + x].grayscale > .025f) found = true;
                                if (found) sample.shadowedFighters++;
                            }
                            if (sample.shadowedFighters != sample.totalFighters)
                                report.errors.Add(label + ": visible ground shadows=" + sample.shadowedFighters + "/" + sample.totalFighters);
                            File.WriteAllBytes(Path.Combine(Output, label + "-control.png"), control.EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(Output, label + "-shadow.png"), shadowed.EncodeToPNG());
                        }
                        finally { UnityEngine.Object.DestroyImmediate(control); UnityEngine.Object.DestroyImmediate(shadowed); }
                    }
                }
            }

            coverage.Dispose();
            for (int quality = 0; quality < originalOverrides.Length; quality++)
                if (QualitySettings.GetRenderPipelineAssetAt(quality) != originalOverrides[quality])
                    report.errors.Add(QualitySettings.names[quality] + ": override was not restored after a quality switch.");

            // Exercise a quality that inherits GraphicsSettings rather than
            // explicitly selecting an asset; teardown must restore null.
            QualitySettings.SetQualityLevel(originalQuality, true);
            QualitySettings.renderPipeline = null;
            coverage.Update(camera, 300, .05f);
            if (GraphicsSettings.currentRenderPipeline == GraphicsSettings.defaultRenderPipeline)
                report.errors.Add("Default-pipeline fallback did not receive a runtime copy.");
            coverage.Dispose();
            if (QualitySettings.renderPipeline != null)
                report.errors.Add("Default-pipeline fallback did not restore the null override.");
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            coverage.Dispose();
            QualitySettings.ForEach((quality, name) => QualitySettings.renderPipeline = originalOverrides[quality]);
            QualitySettings.SetQualityLevel(originalQuality, true);
            for (int quality = 0; quality < sources.Length; quality++)
                if (sources[quality].shadowDistance != originalDistances[quality]
                    || sources[quality].cascadeBorder != originalBorders[quality])
                    report.errors.Add(QualitySettings.names[quality] + ": source asset was changed by runtime coverage.");
            UnityEngine.Object.DestroyImmediate(rig);
            if (groundMaterial != null) UnityEngine.Object.DestroyImmediate(groundMaterial);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
            report.passed = report.errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        errors.AddRange(report.errors);
    }

    static Texture2D Render(Camera camera, RenderTexture target)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (!RenderPipeline.SupportsRenderRequest(camera, request))
            throw new InvalidOperationException("URP render request unsupported.");
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
}
