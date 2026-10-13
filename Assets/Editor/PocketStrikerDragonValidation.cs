using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Checks the imported dragon against the actual game resource keys and renders its authored appearance.</summary>
public static class PocketStrikerDragonValidation
{
    const string Output = "Logs/Dragon";
    const string PrefabPath = "Assets/ExternalAssets/Unit/e_dragon/g_e_dragon.prefab";
    static bool loading;

    public static async void ValidateBatch()
    {
        try
        {
            Validate();
            ValidateLoading();
            await UniTask.WaitUntil(() => !loading);
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(Output, "loading-report.json")));
            EditorApplication.Exit(report != null && report.passed ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string scope;
        public string limitation;
        public string basicPack;
        public int animationAddresses, skillAnimations, dependencies, renderers;
        public Vector3 dragonBounds, humanBounds;
        public List<string> checks = new List<string>();
        public List<string> images = new List<string>();
        public List<Motion> motions = new List<Motion>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class Motion
    {
        public string address;
        public int transformBindings, boundTransforms, movingTransforms, ignoredExporterBindings;
        public float length;
    }

    [MenuItem("PocketStriker/Validation/Dragon Import and Appearance")]
    public static void Validate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before the dragon import fixture.");
        Directory.CreateDirectory(Output);
        var report = NewReport("Actual imported prefab, unit 17 production registration, all dragon movement/hurt/skill resource addresses, Generic rig motion, and native URP previews.",
            "Stopped-editor fixture. Images use actual FightScene lights, authored meshes/materials, a sampled idle pose, and a camera composed for review. Full battle AI and damage are outside this fixture; ValidateLoading separately exercises production animation loaders.");
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Dragon Import Validation");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var oldAmbientMode = RenderSettings.ambientMode;
        var oldAmbient = RenderSettings.ambientLight;
        var oldSun = RenderSettings.sun;
        try
        {
            var entries = Entries();
            var unit = UnitRow();
            report.basicPack = unit[5];
            Require(unit[1] == "g_e_dragon" && unit[2] == "e_dragon", "Unit 17 does not select the imported dragon model.");
            var modelEntry = UniqueEntry(entries, unit[2] + "/" + unit[1]);
            Require(modelEntry.AssetPath == PrefabPath, "Dragon model address selects a different prefab: " + modelEntry.AssetPath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(source != null, "Dragon prefab failed to import.");
            CheckPrefab(source, report);
            CheckDependencyGuids(report);
            var clips = AnimationEntries(entries, unit[5]);
            report.animationAddresses = clips.Count;
            var skillKeys = SkillKeys();
            foreach (var key in skillKeys)
                Require(clips.Any(entry => entry.address == AnimationResourceKeyUtility.SkillAnimationAddress("e_dragon", key)),
                    "Missing production skill animation address: " + key);
            report.skillAnimations = skillKeys.Count;
            Require(report.skillAnimations > 0, "No configured dragon skills.");
            var dragon = CreateModel(source, rig.transform);
            var animator = dragon.GetComponent<Animator>();
            Require(animator != null && animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman,
                "Dragon root requires a valid Generic avatar.");
            foreach (var entry in clips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.AssetPath);
                Require(clip != null && !clip.legacy && !clip.isHumanMotion && clip.length > 0,
                    "Dragon clip must be a nonempty Generic Animator clip: " + entry.address);
                report.motions.Add(CheckMotion(dragon, clip, entry.address));
            }
            var idleEntry = clips.Single(entry => IsBasic(entry, unit[5]) && LoadClip(entry).name == "idle");
            var idle = LoadClip(idleEntry);
            idle.SampleAnimation(dragon, idle.length * .15f);
            dragon.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 180, 0));

            var humanSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/Unit/human/haruka.prefab");
            Require(humanSource != null, "Missing existing fighter for size/style reference.");
            var human = CreateModel(humanSource, rig.transform);
            var humanIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/haruka/idle.anim");
            Require(humanIdle != null, "Missing existing fighter idle.");
            humanIdle.SampleAnimation(human, 0);
            human.transform.rotation = Quaternion.Euler(0, 180, 0);
            var dragonBounds = BoundsOf(dragon);
            var humanBounds = BoundsOf(human);
            report.dragonBounds = dragonBounds.size;
            report.humanBounds = humanBounds.size;
            // A quadruped's height is below the upright fighter's weapon-inclusive bounds.
            Require(dragonBounds.size.y > humanBounds.size.y * .4f && dragonBounds.size.y < humanBounds.size.y * 8,
                "Dragon scale is implausible relative to the existing human fighter.");
            human.transform.position = new Vector3(dragonBounds.min.x - humanBounds.extents.x - .65f, 0, -.35f);
            AddBattleLights(rig.transform);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.1f, .1f, .1f);
            var groundSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/battleGround/AncientEmpire.prefab");
            Require(groundSource != null, "Missing actual battlefield reference.");
            var ground = UnityEngine.Object.Instantiate(groundSource, rig.transform);
            ground.GetComponent<BattleGround>()?.Set();
            var camera = new GameObject("Dragon Review Camera").AddComponent<Camera>();
            camera.transform.SetParent(rig.transform);
            camera.scene = scene;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.095f, .07f, .065f);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 500;
            camera.fieldOfView = 36;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = true;
            var comparison = dragonBounds;
            comparison.Encapsulate(BoundsOf(human));
            RenderView(camera, comparison, new Vector2Int(540, 960), new Vector3(16, -18, 0), ~0,
                "dragon-portrait-comparison.png", report, 1.14f);
            human.SetActive(false);
            RenderView(camera, dragonBounds, new Vector2Int(1080, 1080), new Vector3(15, -30, 0), ~0,
                "dragon-hero.png", report, 1.12f);
            ground.SetActive(false);
            var head = dragon.GetComponentsInChildren<Transform>().Single(node => node.name == "Head");
            var portraitBounds = new Bounds(head.position + Vector3.down * dragonBounds.size.y * .12f,
                Vector3.one * dragonBounds.size.y * .65f);
            RenderView(camera, portraitBounds, new Vector2Int(512, 512), new Vector3(6, -20, 0), 1 << 3,
                "unit-17-icon.png", report, 1.04f);
            report.checks.AddRange(new[] { "Unit 17 resolves the actual dragon prefab", "No missing prefab scripts; required combat and skin references resolve",
                "All transitive serialized GUID references resolve", "Generic avatar and all configured animation bindings resolve",
                "Production base movement and hurt/knock labels cover each required series", "All configured dragon skill addresses resolve",
                "Actual dragon/human scale comparison and authored-material URP views" });
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig);
            EditorSceneManager.ClosePreviewScene(scene);
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientLight = oldAmbient;
            RenderSettings.sun = oldSun;
            Finish(report, "report.json");
        }
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_DRAGON_PASSED: " + Path.GetFullPath(Output));
    }

    // Run separately so import/render inspection stays synchronous and independent of the selected Addressables play mode builder.
    [MenuItem("PocketStriker/Validation/Dragon Production Animation Loading")]
    public static async void ValidateLoading()
    {
        if (loading || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Dragon loader validation needs a stopped editor and no existing run.");
        loading = true;
        var report = NewReport("Production AnimationManger.PreloadBasicPersonalAnims and AnimationResourceLoader.LoadAnim resolve the dragon movement, hit reactions and every configured skill through Addressables.",
            "Stopped-editor Addressables loader smoke with an isolated prefab clone. It does not start battle AI or change the saved editor scene.");
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Dragon Production Loader Validation");
        SceneManager.MoveGameObjectToScene(rig, scene);
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        AnimatorOverrideController runtimeController = null;
        try
        {
            var unit = UnitRow();
            report.basicPack = unit[5];
            var dragon = CreateModel(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), rig.transform);
            var animator = dragon.GetComponent<Animator>();
            Require(animator != null, "Missing dragon Animator.");
            var manager = new AnimationManger { AnimatorRef = animator };
            await manager.PreloadBasicPersonalAnims("e_dragon", unit[5]).Timeout(TimeSpan.FromSeconds(90));
            runtimeController = animator.runtimeAnimatorController as AnimatorOverrideController;
            Require(runtimeController != null, "Production loader did not install the Animator override controller.");
            foreach (var name in new[] { "idle", "walk", "run" })
            {
                var clip = runtimeController[name];
                Require(clip != null && !clip.isHumanMotion && clip.name == name,
                    "Production Generic base override failed: " + name);
                report.motions.Add(CheckMotion(dragon, clip, "runtime/base/" + name));
            }
            // The original dragon controller uses ground locomotion; its authored air clip is available for
            // full-body flight actions but does not introduce a new controller state in this import.
            Require(AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue("e_dragon/" + unit[5], out var basicClips)
                && basicClips.Any(clip => clip != null && clip.name == "air" && !clip.isHumanMotion),
                "Production loader cannot resolve the Generic air action.");
            foreach (var name in new[] { "death", "rush", "block", "block_break", "getup", "victory" })
                Require(manager.TryAnimationClip(name) != null, "Production loader cannot resolve base action: " + name);
            foreach (var series in new[] { "basic_hurts/back", "basic_hurts/high", "basic_hurts/lay", "basic_hurts/low", "basic_hurts/press", "basic_knockoffs" })
            {
                Require(AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue("e_dragon/" + series, out var clips)
                    && clips != null && clips.Count > 0 && clips.All(clip => clip != null && !clip.isHumanMotion),
                    "Production hurt/knock series is empty or not Generic: " + series);
            }
            foreach (var key in SkillKeys())
            {
                await AnimationResourceLoader.LoadAnim("e_dragon", key).Timeout(TimeSpan.FromSeconds(30));
                var clip = AnimationResourceLoader.Instance.GetAnimationClip(AnimationResourceKeyUtility.SkillClipKey("e_dragon", key));
                Require(clip != null, "Production skill loader returned no clip: " + key);
                report.motions.Add(CheckMotion(dragon, clip, "runtime/skill/" + key));
                report.skillAnimations++;
            }
            report.checks.AddRange(new[] { "Production Addressables base override installation", "All six hit/knock series nonempty",
                "All six named action clips load", "Every configured dragon skill loads and drives the actual Generic rig" });
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig);
            if (runtimeController != null) UnityEngine.Object.DestroyImmediate(runtimeController);
            EditorSceneManager.ClosePreviewScene(scene);
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
            loading = false;
            Finish(report, "loading-report.json");
        }
        if (report.passed) Debug.Log("POCKETSTRIKER_DRAGON_LOADING_PASSED: " + Path.GetFullPath(Output));
        else Debug.LogError("Dragon loading failed: " + string.Join("\n", report.errors));
    }

    static List<AddressableAssetEntry> Entries()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Require(settings != null, "Missing Addressables settings.");
        var entries = new List<AddressableAssetEntry>();
        foreach (var group in settings.groups.Where(group => group != null))
            group.GatherAllAssets(entries, true, true, false);
        return entries;
    }

    static string[] UnitRow() => CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/mst_unit.csv"))
        .Skip(1).Single(row => row.Length > 5 && row[0] == "17");

    static List<string> SkillKeys() => CsvParser2.Parse(File.ReadAllText("Assets/ExternalAssets/Config/mst_skill.csv"))
        .Skip(1).Where(row => row.Length > 2 && row[2] == "e_dragon").Select(row => row[1]).ToList();

    static AddressableAssetEntry UniqueEntry(List<AddressableAssetEntry> entries, string address)
    {
        var matches = entries.Where(entry => entry.address == address).ToList();
        Require(matches.Count == 1, "Expected exactly one resource address: " + address + "; found " + matches.Count);
        return matches[0];
    }

    static bool IsBasic(AddressableAssetEntry entry, string pack) =>
        AnimationResourceKeyUtility.IsBasicAnimationLocation(entry.address, "e_dragon", pack);

    static AnimationClip LoadClip(AddressableAssetEntry entry) => AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.AssetPath);

    static List<AddressableAssetEntry> AnimationEntries(List<AddressableAssetEntry> entries, string pack)
    {
        var clips = entries.Where(entry => entry.address.StartsWith("e_dragon/", StringComparison.Ordinal)
            && entry.address.EndsWith(".anim", StringComparison.Ordinal)).ToList();
        Require(clips.Count > 0, "No registered dragon animation clips.");
        foreach (var entry in clips) UniqueEntry(entries, entry.address);
        var basic = clips.Where(entry => IsBasic(entry, pack)).ToList();
        foreach (var name in new[] { "idle", "walk", "run", "air", "death", "rush", "block", "block_break", "getup", "victory" })
            Require(basic.Any(entry => entry.labels.Contains(AnimationResourceKeyUtility.BasicAnimationLabel)
                && LoadClip(entry) != null && LoadClip(entry).name == name), "Missing labelled base movement/action clip: " + name);
        foreach (var series in new[] { "basic_hurts/back", "basic_hurts/high", "basic_hurts/lay", "basic_hurts/low", "basic_hurts/press", "basic_knockoffs" })
            Require(clips.Any(entry => AnimationResourceKeyUtility.IsSeriesAnimationLocation(entry.address, "e_dragon/" + series)
                && entry.labels.Contains(series == "basic_knockoffs" ? "knock_anim" : "hurt_anim")), "Missing labelled hit/knock series: " + series);
        return clips;
    }

    static void CheckPrefab(GameObject prefab, Report report)
    {
        foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
        {
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "Missing script on " + node.name);
        }
        var link = prefab.GetComponent<OutsideDataLink>();
        Require(link != null && link._C != null && link._C.WholeT == prefab.transform, "Root OutsideDataLink/Data_Center model binding is invalid.");
        var centre = link._C;
        Require(centre.geometryCenter != null && centre._BO_Ani_E != null && centre.bO_Weapon_Animation_Events != null
            && centre._BasicPhysicSupport != null && centre._MyBehaviorRunner != null && centre._ShaderManager != null,
            "Dragon battle component references are incomplete.");
        Require(centre.right_hand_t != null && centre.left_hand_t != null && centre.right_foot_t != null
            && centre.left_foot_t != null && centre.head_t != null && centre.tail_t != null, "Dragon attack/effect anchor references are incomplete.");
        var animator = prefab.GetComponent<Animator>();
        Require(animator != null && animator.runtimeAnimatorController != null, "Dragon root Animator controller is missing.");
        foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Require(renderer.sharedMesh != null && renderer.rootBone != null && renderer.bones.All(bone => bone != null),
                "Dragon skin mesh or bone binding is missing: " + renderer.name);
            foreach (var material in renderer.sharedMaterials)
                Require(material != null && material.shader != null && material.shader.isSupported
                    && material.shader.name != "Hidden/InternalErrorShader", "Dragon material/shader missing or unsupported: " + renderer.name);
            report.renderers++;
        }
        Require(report.renderers > 0, "Dragon has no skinned meshes.");
    }

    static void CheckDependencyGuids(Report report)
    {
        var dependencies = AssetDatabase.GetDependencies(PrefabPath, true);
        report.dependencies = dependencies.Length;
        foreach (var path in dependencies)
        {
            if (!File.Exists(path) || !new[] { ".prefab", ".mat", ".controller", ".asset", ".anim" }.Contains(Path.GetExtension(path))) continue;
            var contents = File.ReadAllText(path);
            if (!contents.StartsWith("%YAML", StringComparison.Ordinal)) continue;
            foreach (Match match in Regex.Matches(contents, @"guid: ([0-9a-f]{32})"))
            {
                var guid = match.Groups[1].Value;
                if (guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                Require(!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)), "Unresolved dependency GUID " + guid + " in " + path);
            }
        }
    }

    static GameObject CreateModel(GameObject source, Transform parent)
    {
        Require(source != null, "Missing model prefab.");
        var model = UnityEngine.Object.Instantiate(source, parent);
        foreach (var centre in model.GetComponentsInChildren<Data_Center>(true)) UnityEngine.Object.DestroyImmediate(centre);
        foreach (var link in model.GetComponentsInChildren<OutsideDataLink>(true)) UnityEngine.Object.DestroyImmediate(link);
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
        foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
        foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 3;
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
        return model;
    }

    static Motion CheckMotion(GameObject model, AnimationClip clip, string address)
    {
        var result = new Motion { address = address, length = clip.length };
        var bindings = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)).ToArray();
        result.transformBindings = bindings.Length;
        Require(bindings.Length > 0, "Generic clip has no transform curves: " + address);
        // The upstream rush/getup exports contain an extra "Root" track, while their actual skin rig starts at CG.
        // Ignore only that known exporter track; all real bone paths must resolve on the imported prefab.
        var paths = bindings.Select(binding => binding.path).Distinct().ToArray();
        result.ignoredExporterBindings = bindings.Count(binding => binding.path == "Root" && model.transform.Find("Root") == null);
        var nodes = paths.Where(path => path != "Root" || model.transform.Find(path) != null)
            .Select(path => string.IsNullOrEmpty(path) ? model.transform : model.transform.Find(path)).ToArray();
        result.boundTransforms = nodes.Count(node => node != null);
        Require(nodes.Length > 0 && nodes.All(node => node != null),
            "Generic animation contains skeleton paths absent from the actual dragon: " + address);
        clip.SampleAnimation(model, 0);
        var positions = nodes.Select(node => node.localPosition).ToArray();
        var rotations = nodes.Select(node => node.localRotation).ToArray();
        clip.SampleAnimation(model, clip.length * .37f);
        result.movingTransforms = nodes.Where((node, index) => Vector3.Distance(node.localPosition, positions[index]) > .0001f
            || Quaternion.Angle(node.localRotation, rotations[index]) > .01f).Count();
        // "air" can deliberately hold a single flight pose. Other imported actions must animate the rig.
        if (clip.name != "air") Require(result.movingTransforms > 0, "Animation does not move the actual Generic rig: " + address);
        return result;
    }

    static Bounds BoundsOf(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled
            && !(renderer is ParticleSystemRenderer) && renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly).ToArray();
        Require(renderers.Length > 0, "Model has no visible renderers: " + model.name);
        // Skin bounds include every authored animation. Frame the actual sampled pose,
        // otherwise folded wings leave the review and small HUD icon mostly empty.
        var bounds = new Bounds();
        bool initialized = false;
        foreach (var renderer in renderers)
        {
            if (renderer is SkinnedMeshRenderer skin)
            {
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked);
                    foreach (var vertex in baked.vertices)
                    {
                        var point = skin.transform.TransformPoint(vertex);
                        if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
            }
            else
            {
                if (!initialized) { bounds = renderer.bounds; initialized = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }

    static void AddBattleLights(Transform parent)
    {
        var sourceScene = EditorSceneManager.OpenPreviewScene("Assets/Scene/fight_scene/FightScene.unity");
        try
        {
            foreach (var light in sourceScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>())
                .Where(light => light.enabled && light.gameObject.activeInHierarchy))
            {
                var node = new GameObject(light.name);
                node.transform.SetParent(parent);
                node.transform.SetPositionAndRotation(light.transform.position, light.transform.rotation);
                var copy = node.AddComponent<Light>();
                EditorUtility.CopySerialized(light, copy);
                if (light.name == "Directional Light2") RenderSettings.sun = copy;
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(sourceScene); }
    }

    static void RenderView(Camera camera, Bounds bounds, Vector2Int size, Vector3 angles, int mask, string file, Report report, float margin)
    {
        camera.aspect = (float)size.x / size.y;
        camera.cullingMask = mask;
        var rotation = Quaternion.Euler(angles);
        var inverse = Quaternion.Inverse(rotation);
        float distance = 0;
        var tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            var corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
            distance = Mathf.Max(distance, Mathf.Abs(corner.x) * margin / (tangent * camera.aspect) - corner.z,
                Mathf.Abs(corner.y) * margin / tangent - corner.z);
        }
        camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
        var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
        var old = RenderTexture.active;
        Texture2D image = null;
        try
        {
            target.Create();
            camera.targetTexture = target;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            Require(RenderPipeline.SupportsRenderRequest(camera, request), "URP render request is unsupported.");
            for (int warmup = 0; warmup < 3; warmup++) RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            Require(image.GetPixels().Count(pixel => pixel.r > .75f && pixel.b > .75f && pixel.g < .1f) < size.x * size.y / 100,
                "Preview contains unsupported-shader magenta pixels: " + file);
            var path = Path.Combine(Output, file);
            File.WriteAllBytes(path, image.EncodeToPNG());
            report.images.Add(path);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = old;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static Report NewReport(string scope, string limitation) => new Report {
        unityVersion = Application.unityVersion, scope = scope, limitation = limitation
    };

    static void Finish(Report report, string name)
    {
        report.passed = report.errors.Count == 0;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, name), JsonUtility.ToJson(report, true));
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
