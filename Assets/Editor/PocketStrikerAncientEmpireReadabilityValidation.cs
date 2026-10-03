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

/// <summary>Actual castle geometry and local fighter models under identical battle lights.</summary>
public static class PocketStrikerAncientEmpireReadabilityValidation
{
    const string Output = "Logs/Rendering/AncientEmpireReadability";
    const string GroundPath = "Assets/ExternalAssets/battleGround/AncientEmpire.prefab";
    const string FightScenePath = "Assets/Scene/fight_scene/FightScene.unity";

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string scope = "Actual battleGround/1 geometry, haruka and robot3 local prefab meshes with authored materials and idle poses; four actual FightScene lights copied without changing exposure. Compare close front, close side and full-field views, environment luminance and isolated fighter pixels. Check shared source materials, idempotence, restore and per-source clone reuse.";
        public string limitation = "Stopped-editor URP preview with flat .1 ambient, no combat/network initialization, HUD or post processing. Camera composition is a visual fixture; production Group camera and Addressables loading are exercised separately by Battle Camera Playmode Smoke.";
        public int runtimeMaterials, authoredMaterials, changedRendererSlots;
        public List<string> initializationChanges = new List<string>();
        public List<string> checks = new List<string>();
        public List<Sample> samples = new List<Sample>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class Sample
    {
        public string view, before, after;
        public float groundBefore, groundAfter, fighterMaximumDifference;
        public int brightBefore, brightAfter;
    }

    [MenuItem("PocketStriker/Validation/Ancient Empire Readability")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before the Ancient Empire rendering fixture.");
        Directory.CreateDirectory(Output);
        var report = new Report { unityVersion = Application.unityVersion };
        var preview = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Ancient Empire Readability Fixture");
        SceneManager.MoveGameObjectToScene(rig, preview);
        var sceneLights = default(Scene);
        var target = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
        target.Create();
        var oldAmbientMode = RenderSettings.ambientMode;
        var oldAmbient = RenderSettings.ambientLight;
        var oldSun = RenderSettings.sun;
        AncientEmpireReadability readability = null;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GroundPath);
            Require(source != null && source.GetComponent<AncientEmpireReadability>() != null,
                "Registered castle prefab has no readability component.");
            foreach (string other in new[] { "Building", "Graveyard" })
                Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/battleGround/" + other + ".prefab")
                    .GetComponent<AncientEmpireReadability>() == null, other + " unexpectedly uses the castle override.");
            var ground = UnityEngine.Object.Instantiate(source, rig.transform);
            ground.GetComponent<BattleGround>().Set();
            readability = ground.GetComponent<AncientEmpireReadability>();
            readability.Restore();
            var renderers = ground.GetComponentsInChildren<MeshRenderer>(true);
            var originals = renderers.ToDictionary(renderer => renderer, renderer => renderer.sharedMaterials);
            var coldMaterialState = originals.Values.SelectMany(value => value).Where(value => value != null).Distinct()
                .ToDictionary(material => material, material => EditorJsonUtility.ToJson(material));
            var sourceFiles = coldMaterialState.Keys.Select(AssetDatabase.GetAssetPath).Distinct()
                .ToDictionary(path => path, File.ReadAllText);
            report.authoredMaterials = coldMaterialState.Count;

            sceneLights = EditorSceneManager.OpenPreviewScene(FightScenePath);
            foreach (var light in sceneLights.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>())
                .Where(light => light.enabled && light.gameObject.activeInHierarchy))
            {
                var node = new GameObject(light.name);
                node.transform.SetParent(rig.transform);
                node.transform.rotation = light.transform.rotation;
                var copied = node.AddComponent<Light>();
                EditorUtility.CopySerialized(light, copied);
                if (light.name == "Directional Light2") RenderSettings.sun = copied;
            }
            EditorSceneManager.ClosePreviewScene(sceneLights);
            sceneLights = default;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.1f, .1f, .1f);
            var fighterRenderers = new List<Renderer>();
            CreateFighter(rig.transform, "haruka", new Vector3(-2, 0, 0), fighterRenderers);
            CreateFighter(rig.transform, "robot3", new Vector3(2, 0, 0), fighterRenderers);
            var fighterMaterials = fighterRenderers.ToDictionary(renderer => renderer, renderer => renderer.sharedMaterials);

            var camera = new GameObject("Fixture Camera").AddComponent<Camera>();
            camera.transform.SetParent(rig.transform);
            camera.scene = preview;
            camera.enabled = false;
            camera.aspect = 9f / 16;
            camera.fieldOfView = 45;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f, .12f, .15f, 1);
            camera.targetTexture = target;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            var views = new[] { "close-front", "close-side", "whole-field" };
            // The first URP draw initializes source material keyword/CRC state.
            // Some authored materials are outside all three camera views. Draw
            // every source on a separate layer before comparing full raw JSON.
            WarmMaterials(camera, target, rig.transform, coldMaterialState.Keys);
            // Also warm actual geometry before the visual baseline, retaining
            // cold/warm JSON and checking authored files separately below.
            foreach (var view in views)
            {
                PositionCamera(camera, view);
                for (int warmup = 0; warmup < 2; warmup++)
                    using (var image = new Image(camera, target, ~(1 << 5))) { }
            }
            var materialState = coldMaterialState.Keys.ToDictionary(material => material, material => EditorJsonUtility.ToJson(material));
            foreach (var pair in coldMaterialState)
                if (pair.Value != materialState[pair.Key])
                {
                    report.initializationChanges.Add(pair.Key.name);
                    File.WriteAllText(Path.Combine(Output, pair.Key.name + "-cold.json"), pair.Value);
                    File.WriteAllText(Path.Combine(Output, pair.Key.name + "-warm.json"), materialState[pair.Key]);
                }
            foreach (var view in views)
            {
                PositionCamera(camera, view);
                readability.Restore();
                var sample = new Sample { view = view,
                    before = Path.Combine(Output, view + "-before.png"), after = Path.Combine(Output, view + "-after.png") };
                using (var before = new Image(camera, target, ~(1 << 5)))
                using (var floorBefore = new Image(camera, target, 1 << 6))
                using (var fighterBefore = new Image(camera, target, 1 << 3))
                {
                    readability.Apply();
                    using (var after = new Image(camera, target, ~(1 << 5)))
                    using (var floorAfter = new Image(camera, target, 1 << 6))
                    using (var fighterAfter = new Image(camera, target, 1 << 3))
                    {
                        File.WriteAllBytes(sample.before, before.texture.EncodeToPNG());
                        File.WriteAllBytes(sample.after, after.texture.EncodeToPNG());
                        MeasureGround(floorBefore.texture, out sample.groundBefore, out sample.brightBefore);
                        MeasureGround(floorAfter.texture, out sample.groundAfter, out sample.brightAfter);
                        var originalPixels = fighterBefore.texture.GetPixels();
                        var adjustedPixels = fighterAfter.texture.GetPixels();
                        for (int index = 0; index < originalPixels.Length; index++)
                        {
                            var difference = originalPixels[index] - adjustedPixels[index];
                            sample.fighterMaximumDifference = Mathf.Max(sample.fighterMaximumDifference,
                                Mathf.Abs(difference.r), Mathf.Abs(difference.g), Mathf.Abs(difference.b));
                        }
                        Require(sample.fighterMaximumDifference < .01f, view + ": character pixels changed.");
                        Require(sample.groundAfter < sample.groundBefore * .98f,
                            view + ": battlefield background brightness did not decrease.");
                        Require(sample.groundAfter > .08f, view + ": battlefield background became black.");
                        report.samples.Add(sample);
                    }
                }
            }
            report.runtimeMaterials = readability.RuntimeMaterialCount;
            Require(report.runtimeMaterials > 0 && report.runtimeMaterials <= report.authoredMaterials * 2,
                "Material instances are not shared per authored material and ground/scenery treatment.");
            var applied = renderers.ToDictionary(renderer => renderer, renderer => renderer.sharedMaterials);
            readability.Apply();
            foreach (var pair in applied)
            {
                Require(pair.Value.SequenceEqual(pair.Key.sharedMaterials), "Repeated Apply changed material bindings.");
                report.changedRendererSlots += pair.Value.Where((value, index) => value != originals[pair.Key][index]).Count();
            }
            foreach (var pair in materialState)
                Require(EditorJsonUtility.ToJson(pair.Key) == pair.Value, "Authored source material was modified: " + pair.Key.name);
            foreach (var pair in sourceFiles)
                Require(File.ReadAllText(pair.Key) == pair.Value, "Authored material file was modified: " + pair.Key);
            foreach (var pair in fighterMaterials)
                Require(pair.Value.SequenceEqual(pair.Key.sharedMaterials), "Fighter material binding was modified.");
            readability.Restore();
            Require(readability.RuntimeMaterialCount == 0, "Runtime materials were not released.");
            foreach (var pair in originals)
                Require(pair.Value.SequenceEqual(pair.Key.sharedMaterials), "Original castle materials were not restored.");
            report.checks.AddRange(new[] { "same actual battle lights and exposure", "source materials and files preserved after renderer warmup",
                "fighter pixels and materials unchanged", "one clone per source/treatment", "idempotent apply and full restore",
                "three actual geometry before/after views" });
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            if (readability != null) readability.Restore();
            if (sceneLights.IsValid()) EditorSceneManager.ClosePreviewScene(sceneLights);
            UnityEngine.Object.DestroyImmediate(rig);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientLight = oldAmbient;
            RenderSettings.sun = oldSun;
            report.passed = report.errors.Count == 0 && report.samples.Count == 3;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_ANCIENT_EMPIRE_READABILITY_PASSED: " + Path.GetFullPath(Output));
    }

    static void CreateFighter(Transform parent, string name, Vector3 position, List<Renderer> renderers)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/Unit/human/" + name + ".prefab");
        Require(source != null, "Missing local fighter model: " + name);
        var model = UnityEngine.Object.Instantiate(source, parent);
        foreach (var centre in model.GetComponentsInChildren<Data_Center>(true)) UnityEngine.Object.DestroyImmediate(centre);
        foreach (var link in model.GetComponentsInChildren<OutsideDataLink>(true)) UnityEngine.Object.DestroyImmediate(link);
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
        foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/" + name + "/idle.anim");
        Require(idle != null, "Missing native idle animation: " + name);
        foreach (var animator in model.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
            if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) idle.SampleAnimation(animator.gameObject, 0);
        }
        foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 3;
        model.transform.position = position;
        model.transform.rotation = Quaternion.Euler(0, 180, 0);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            renderers.Add(renderer);
        }
    }

    static void MeasureGround(Texture2D texture, out float mean, out int bright)
    {
        var pixels = texture.GetPixels(texture.width / 4, texture.height / 3, texture.width / 2, texture.height / 3);
        mean = pixels.Average(pixel => pixel.grayscale);
        bright = pixels.Count(pixel => pixel.grayscale > .85f);
    }

    static void PositionCamera(Camera camera, string view)
    {
        float yaw = view == "close-side" ? 90 : view == "whole-field" ? 180 : 0;
        float pitch = view == "whole-field" ? 46 : 32;
        float distance = view == "whole-field" ? 48 : 20;
        var rotation = Quaternion.Euler(pitch, yaw, 0);
        camera.transform.SetPositionAndRotation(new Vector3(0, 1, 0) - rotation * Vector3.forward * distance, rotation);
    }

    static void WarmMaterials(Camera camera, RenderTexture target, Transform parent, IEnumerable<Material> materials)
    {
        var grid = new GameObject("All authored material warmup");
        grid.transform.SetParent(parent);
        camera.orthographic = true;
        camera.orthographicSize = 4;
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        try
        {
            int index = 0;
            foreach (var material in materials)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.SetParent(grid.transform);
                quad.layer = 17;
                quad.transform.localScale = Vector3.one * .5f;
                quad.transform.localPosition = new Vector3((index % 7 - 3) * .55f, (index / 7 - 2.5f) * .55f, 0);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                index++;
            }
            for (int warmup = 0; warmup < 2; warmup++)
                using (var image = new Image(camera, target, 1 << 17)) { }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(grid);
            camera.orthographic = false;
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    sealed class Image : IDisposable
    {
        public readonly Texture2D texture;
        public Image(Camera camera, RenderTexture target, int mask)
        {
            camera.cullingMask = mask;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            Require(RenderPipeline.SupportsRenderRequest(camera, request), "URP render request unsupported.");
            RenderPipeline.SubmitRenderRequest(camera, request);
            var old = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
            }
            finally { RenderTexture.active = old; }
        }
        public void Dispose() => UnityEngine.Object.DestroyImmediate(texture);
    }
}
