using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightScene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks production crowd placement against the real FightScene authored points.</summary>
public static class PocketStrikerGroupFormationValidation
{
    const string ScenePath = "Assets/Scene/fight_scene/FightScene.unity";
    const string Output = "Logs/CameraFraming/formation-report.json";
    const float Epsilon = .001f;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public int formationCases;
        public int pairedCases;
        public int arenaCases;
        public int ringParticleCases;
        public int localModels;
        public float authoredArenaRadius;
        public List<Formation> formations = new List<Formation>();
        public List<ModelFootprint> footprints = new List<ModelFootprint>();
        public List<string> ringDefinitions = new List<string>();
        public List<string> emittedRingSizes = new List<string>();
        public List<string> observations = new List<string>();
        public List<string> errors = new List<string>();
        public string scope = "Real FightScene's two 30-point formations through BattleFormationPlacement.Build: original 24/30 positions, 36/48/72/100 expanded teams, yaw/translation/scale, front edge, spacing, opposing teams, deterministic retry and unchanged source. Local human body spacing comes from native BO_Limb colliders; complete visible meshes including weapons/wings determine arena capacity. Actual BoundaryControlByGod adapts radius, sensor, ring and a local native battlefield prefab and restores ordinary size.";
        public string limitation = "Stopped-editor geometry and capacity fixture. Does not initialize combat, account, Addressables or AI. Runtime start placement, live animations and 100v100 performance are covered by the separate production Play-mode smoke.";
    }

    [Serializable]
    public sealed class Formation
    {
        public string name;
        public int count;
        public float requestedSpacing;
        public float measuredSpacing;
        public float width;
        public float depth;
        public float front;
        public float back;
        public float requiredArenaRadius;
        public int outsideAuthoredArena;
    }

    [Serializable]
    public sealed class ModelFootprint
    {
        public string model;
        public Vector3 boundsSize;
        public Vector3 bodySize;
        public float spacing;
        public float meshSpacing;
        public float radialPadding;
        public float requiredHundredUnitRadius;
        public int visibleMeshes;
        public int visibleBodyHitboxes;
    }

    [MenuItem("PocketStriker/Validation/Group Formation")]
    public static void ValidateOrThrow()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("[GroupFormation] PASS: " + report.formationCases + " formations, " + report.localModels + " local model footprints, " + report.arenaCases + " arena checks.");
    }

    public static void ValidateBatch()
    {
        var report = Run();
        Debug.Log("[GroupFormation] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(Output));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before validating formations.");
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        var report = new Report();
        var source = File.ReadAllText(ScenePath);
        var oldBoundary = BoundaryControlByGod.target;
        float oldRadius = BoundaryControlByGod._BattleRingRadius;
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            var roots = scene.GetRootGameObjects();
            var teams = roots.SelectMany(root => root.GetComponentsInChildren<UnitsManger>(true))
                .Where(team => team.name == "team1" || team.name == "team2").OrderBy(team => team.name).ToArray();
            Require(teams.Length == 2, "Real FightScene must contain exactly two team managers.");
            var boundary = roots.SelectMany(root => root.GetComponentsInChildren<BoundaryControlByGod>(true)).Single();
            report.authoredArenaRadius = new SerializedObject(boundary).FindProperty("BattleRingRadius").floatValue;
            var authored = teams.Select(team => team.TeamStandPoints.Select(point =>
            {
                Require(point != null, "Real FightScene contains an unassigned formation point.");
                return new Pose(point.position, point.rotation);
            }).ToArray()).ToArray();
            Require(authored.All(points => points.Length == 30), "Expected the actual 30 authored points for each team.");
            Require(Vector3.Dot(Forward(authored[0]), Forward(authored[1])) < -.999f, "The two teams do not face each other.");
            foreach (int team in new[] { 0, 1 })
            {
                var forward = Forward(authored[team]); var right = Vector3.Cross(Vector3.up, forward);
                var x = authored[team].Select(pose => Vector3.Dot(pose.position, right)).ToArray();
                var z = authored[team].Select(pose => Vector3.Dot(pose.position, forward)).ToArray();
                report.observations.Add(teams[team].name + " authored: width=" + (x.Max() - x.Min()) + ", depth=" + (z.Max() - z.Min())
                    + ", front=" + z.Max() + ", back=" + z.Min() + ", minimum center spacing=" + MinimumSpacing(authored[team]) + ".");
            }

            foreach (int count in new[] { 24, 30, 36, 48, 72, 100 })
            foreach (float spacing in new[] { 1f, 1.4f, 2f })
            {
                var pair = new Pose[2][];
                for (int team = 0; team < 2; team++)
                    pair[team] = CheckFormation(authored[team], count, spacing, teams[team].name, report);
                CheckOpponents(pair[0], pair[1], "count-" + count + "/spacing-" + spacing);
                report.pairedCases++;
            }

            // Transform the actual authored positions rather than inventing a second layout.
            foreach (float scale in new[] { .5f, 2f })
            foreach (float yaw in new[] { 37f, 90f })
            {
                var rotation = Quaternion.Euler(0, yaw, 0);
                var shifted = authored[0].Select(pose => new Pose(new Vector3(8, 3, -12) + rotation * (pose.position * scale), rotation * pose.rotation)).ToArray();
                CheckFormation(shifted, 100, 1.4f * scale, "transformed/scale-" + scale + "/yaw-" + yaw, report);
            }
            CheckEdgeCases(report);
            CheckLocalFootprints(scene, authored, report);
            CheckArenaCapacity(boundary, authored, report);
            Require(File.ReadAllText(ScenePath) == source, "The real FightScene source changed during validation.");
            report.observations.Add("Original 24/30-person indexed formations remain exact. Only overflow formations are rearranged; all extra rows extend behind their authored front edge. Rebuilding does not accumulate offsets or mutate source points.");
            report.observations.Add("outsideAuthoredArena is a diagnostic against the serialized ordinary radius, not a reason to compress crowd spacing. Group capacity must cover requiredArenaRadius and restore the authored ordinary radius on mode change.");
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            BoundaryControlByGod.target = oldBoundary; BoundaryControlByGod._BattleRingRadius = oldRadius;
            report.passed = report.errors.Count == 0 && report.formationCases == 40 && report.pairedCases == 18
                && report.localModels >= 3 && report.arenaCases >= 3 && report.ringParticleCases >= 8;
            File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static Pose[] CheckFormation(Pose[] authored, int count, float spacing, string label, Report report)
    {
        var original = authored.ToArray();
        var placed = BattleFormationPlacement.Build(authored, count, spacing);
        var repeated = BattleFormationPlacement.Build(authored, count, spacing);
        Require(placed.Length == count, label + " has the wrong unit count.");
        float front = authored.Max(pose => Vector3.Dot(pose.position, Forward(authored)));
        foreach (int index in Enumerable.Range(0, count))
        {
            Require(Finite(placed[index].position), label + " has a nonfinite position.");
            EqualPose(placed[index], repeated[index], label + " retry changed a point.");
            if (count <= authored.Length) EqualPose(placed[index], authored[index], label + " moved an authored point.");
            else
            {
                Require(Mathf.Abs(placed[index].position.y - authored[0].position.y) < Epsilon, label + " changed ground height.");
                Require(Quaternion.Angle(placed[index].rotation, authored[0].rotation) < Epsilon, label + " changed authored facing.");
                Require(Vector3.Dot(placed[index].position, Forward(authored)) <= front + Epsilon, label + " advances past its authored front.");
            }
        }
        float measured = MinimumSpacing(placed);
        Require(measured > .001f, label + " overlaps a formation point.");
        if (count > authored.Length) Require(measured >= spacing - Epsilon, label + " is denser than the requested footprint.");
        for (int index = 0; index < authored.Length; index++) EqualPose(authored[index], original[index], label + " changed source poses.");
        var forward = Forward(authored); var right = Vector3.Cross(Vector3.up, forward);
        var xs = placed.Select(pose => Vector3.Dot(pose.position, right)).ToArray();
        var zs = placed.Select(pose => Vector3.Dot(pose.position, forward)).ToArray();
        float radius = placed.Max(pose => new Vector2(pose.position.x, pose.position.z).magnitude);
        Require(Mathf.Abs(BattleFormationPlacement.RequiredArenaRadius(placed) - radius) < Epsilon, label + " arena radius omits a formation point.");
        report.formations.Add(new Formation { name = label, count = count, requestedSpacing = spacing, measuredSpacing = measured,
            width = xs.Max() - xs.Min(), depth = zs.Max() - zs.Min(), front = zs.Max(), back = zs.Min(),
            requiredArenaRadius = radius, outsideAuthoredArena = placed.Count(pose => new Vector2(pose.position.x, pose.position.z).magnitude > report.authoredArenaRadius + Epsilon) });
        report.formationCases++;
        return placed;
    }

    static void CheckOpponents(Pose[] first, Pose[] second, string label)
    {
        var between = second.Aggregate(Vector3.zero, (sum, pose) => sum + pose.position) / second.Length
            - first.Aggregate(Vector3.zero, (sum, pose) => sum + pose.position) / first.Length;
        Require(Vector3.Dot(first[0].rotation * Vector3.forward, between.normalized) > .99f, label + " first team faces away from the opponent.");
        Require(Vector3.Dot(second[0].rotation * Vector3.forward, between.normalized) < -.99f, label + " second team faces away from the opponent.");
        foreach (var a in first) foreach (var b in second)
            Require(Vector2.Distance(new Vector2(a.position.x, a.position.z), new Vector2(b.position.x, b.position.z)) >= 5.999f, label + " crowd fronts cross or overlap.");
    }

    static void CheckEdgeCases(Report report)
    {
        Require(BattleFormationPlacement.Build(null, 0).Length == 0 && BattleFormationPlacement.Build(null, -1).Length == 0, "Empty/negative counts are invalid.");
        foreach (float spacing in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            var placed = BattleFormationPlacement.Build(null, 3, spacing);
            Require(placed.Length == 3 && placed.All(pose => Finite(pose.position)) && MinimumSpacing(placed) > .09f, "Missing authored points or invalid spacing produce an invalid formation.");
        }
        report.observations.Add("Empty authored points, zero/negative counts and invalid spacing remain finite without allocating or placing negative counts.");
    }

    static void CheckLocalFootprints(Scene scene, Pose[][] authored, Report report)
    {
        var rows = File.ReadAllLines("Assets/ExternalAssets/Config/mst_unit.csv").Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.TrimEnd('\r').Split(',')).Where(row => row.Length >= 6 && row[2] == "human");
        var parent = new GameObject("Local formation footprint fixture"); parent.SetActive(false);
        SceneManager.MoveGameObjectToScene(parent, scene);
        try
        {
            foreach (var row in rows)
            {
                string path = "Assets/ExternalAssets/Unit/human/" + row[1] + ".prefab";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(source != null, "Missing local unit model: " + path);
                string before = File.ReadAllText(path);
                parent.SetActive(false);
                var model = UnityEngine.Object.Instantiate(source, parent.transform, false);
                try
                {
                    var centre = model.GetComponentInChildren<Data_Center>(true);
                    var bodyColliders = model.GetComponentsInChildren<BO_Limb>(true).Where(limb => limb.Center == centre)
                        .Select(limb => limb.myColliderMustEquip != null ? limb.myColliderMustEquip : limb.GetComponent<Collider>()).Where(collider => collider != null).ToArray();
                    foreach (var removedCentre in model.GetComponentsInChildren<Data_Center>(true)) UnityEngine.Object.DestroyImmediate(removedCentre);
                    foreach (var link in model.GetComponentsInChildren<OutsideDataLink>(true)) UnityEngine.Object.DestroyImmediate(link);
                    foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
                    foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
                    var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ExternalAssets/Animations/human/BasicPack/" + row[5] + "/idle.anim");
                    foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                    {
                        animator.enabled = false;
                        if (idle != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) idle.SampleAnimation(animator.gameObject, 0);
                    }
                    foreach (var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skinned.updateWhenOffscreen = true;
                    model.SetActive(true); parent.SetActive(true);
                    Physics.SyncTransforms();
                    Require(BattleCameraFraming.TryGetModelBounds(model.transform, out var box), "No visible geometry in " + path);
                    var activeBody = bodyColliders.Where(collider => collider.enabled && collider.gameObject.activeInHierarchy).ToArray();
                    var body = activeBody.Length > 0 ? activeBody[0].bounds : new Bounds();
                    foreach (var collider in activeBody.Skip(1)) body.Encapsulate(collider.bounds);
                    float spacing = Mathf.Max(1, Mathf.Max(body.size.x, body.size.z) + .2f);
                    float padding = new Vector2(box.extents.x, box.extents.z).magnitude;
                    float required = authored.Max(points => BattleFormationPlacement.RequiredArenaRadius(BattleFormationPlacement.Build(points, 100, spacing), padding));
                    report.footprints.Add(new ModelFootprint { model = row[1], boundsSize = box.size, bodySize = body.size, spacing = spacing,
                        meshSpacing = Mathf.Max(1, Mathf.Max(box.size.x, box.size.z) + .2f), radialPadding = padding,
                        visibleBodyHitboxes = activeBody.Length,
                        requiredHundredUnitRadius = required, visibleMeshes = model.GetComponentsInChildren<Renderer>(true)
                            .Count(renderer => (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.enabled && renderer.gameObject.activeInHierarchy) });
                    report.localModels++;
                    Require(File.ReadAllText(path) == before, "Unit source changed: " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    static void CheckArenaCapacity(BoundaryControlByGod boundary, Pose[][] authored, Report report)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var rings = (List<ParticleSystem>)typeof(BoundaryControlByGod).GetField("BattleRingPSs", fields).GetValue(boundary);
        var ringScales = rings.Where(ring => ring != null).ToDictionary(ring => ring, ring => ring.transform.localScale);
        Require(ringScales.Count > 0, "Actual FightScene has no boundary ring.");
        var particleDefaults = ringScales.Keys.SelectMany(root => root.GetComponentsInChildren<ParticleSystem>(true)).Distinct()
            .ToDictionary(particles => particles, particles => new ParticleDefaults(particles));
        var nativePaths = new[] { "Assets/D_Asset/Magic Circle Fx/Asset/Prefabs/MC01_Blue.prefab", "Assets/D_Asset/Magic Circle Fx/Asset/Prefabs/ring.prefab" };
        var nativeSources = nativePaths.ToDictionary(path => path, File.ReadAllText);
        boundary.ConfigureBattleRadius(false);
        foreach (var ring in ringScales.Keys) ring.Simulate(.6f, true, true, true);
        foreach (var pair in particleDefaults)
            if (pair.Value.Mode != ParticleSystemScalingMode.Hierarchy && pair.Key.gameObject.activeInHierarchy)
                pair.Value.EmittedSize = ReadParticleSize(pair.Key);
        foreach (var root in ringScales.Keys)
        foreach (var ring in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ring.GetComponent<ParticleSystemRenderer>();
            report.ringDefinitions.Add(ring.name + ": euler=" + ring.transform.eulerAngles + ", shape=" + ring.shape.shapeType
                + ", shapeRotation=" + ring.shape.rotation + ", scalingMode=" + ring.main.scalingMode
                + ", startRotation3D=" + ring.main.startRotation3D + ", renderMode=" + (renderer != null ? renderer.renderMode.ToString() : "none"));
        }
        const string groundPath = "Assets/ExternalAssets/battleGround/Building.prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(groundPath);
        Require(source != null, "Missing local battlefield prefab.");
        string before = File.ReadAllText(groundPath);
        var parent = new GameObject("Local arena capacity fixture"); parent.SetActive(false);
        SceneManager.MoveGameObjectToScene(parent, boundary.gameObject.scene);
        var ground = UnityEngine.Object.Instantiate(source, parent.transform, false);
        var settings = ground.GetComponent<BattleGround>();
        if (settings != null) settings.Set();
        var groundScale = ground.transform.localScale;
        typeof(BoundaryControlByGod).GetField("battleGround", fields).SetValue(boundary, ground);
        typeof(BoundaryControlByGod).GetField("battleGroundDefaultScale", fields).SetValue(boundary, groundScale);
        try
        {
            float maxSpacing = report.footprints.Max(model => model.spacing);
            float maxPadding = report.footprints.Max(model => model.radialPadding);
            foreach (int count in new[] { 24, 72, 100 })
            {
                // Independent corner calculation validates the capacity overload,
                // including mesh overhang, rather than comparing it to itself.
                var boxes = authored.SelectMany(points => BattleFormationPlacement.Build(points, count, maxSpacing))
                    .Select(pose => new Bounds(pose.position + Vector3.up, new Vector3(maxPadding * 2, 2, maxPadding * 2))).ToArray();
                float required = boxes.Max(box => PlanarCorners(box).Max(point => point.magnitude)) + 1f;
                Require(Mathf.Abs(BattleFormationPlacement.RequiredArenaRadius(boxes, 1) - required) < Epsilon,
                    "Full model corners are omitted from required arena capacity.");
                boundary.ConfigureBattleRadius(true, required);
                Require(Mathf.Abs(boundary.EffectiveBattleRadius - Mathf.Max(report.authoredArenaRadius, required)) < Epsilon
                    && Mathf.Abs(BoundaryControlByGod._BattleRingRadius - boundary.EffectiveBattleRadius) < Epsilon,
                    "Group capacity does not synchronize physical battle radius.");
                foreach (var box in boxes) foreach (var point in PlanarCorners(box))
                    Require(point.magnitude <= boundary.EffectiveBattleRadius - .999f, "Group arena clips a starting model corner.");
                CheckScaledCapacity(boundary, ground, groundScale, ringScales);
                CheckRingParticleSizes(boundary, ringScales.Keys, particleDefaults, report);
                float first = boundary.EffectiveBattleRadius;
                var firstScale = ground.transform.localScale;
                boundary.ConfigureBattleRadius(true, required);
                Require(Mathf.Abs(first - boundary.EffectiveBattleRadius) < Epsilon && (ground.transform.localScale - firstScale).sqrMagnitude < Epsilon * Epsilon,
                    "Repeated Group preparation multiplies arena scale.");
                CheckScaledCapacity(boundary, ground, groundScale, ringScales);
                CheckRingParticleSizes(boundary, ringScales.Keys, particleDefaults, report);
                report.arenaCases++;
            }
            boundary.ConfigureBattleRadius(false, 10000);
            Require(Mathf.Abs(boundary.EffectiveBattleRadius - report.authoredArenaRadius) < Epsilon
                && Mathf.Abs(boundary.ArenaScale - 1) < Epsilon, "Ordinary mode does not restore its authored radius.");
            CheckScaledCapacity(boundary, ground, groundScale, ringScales);
            CheckRingParticleSizes(boundary, ringScales.Keys, particleDefaults, report);
            boundary.ConfigureBattleRadius(true, report.authoredArenaRadius / 2);
            Require(Mathf.Abs(boundary.EffectiveBattleRadius - report.authoredArenaRadius) < Epsilon, "Small Group request shrinks below the authored arena.");
            CheckScaledCapacity(boundary, ground, groundScale, ringScales);
            CheckRingParticleSizes(boundary, ringScales.Keys, particleDefaults, report);
            boundary.ConfigureBattleRadius(false);
            report.arenaCases += 2;
            Require(new SerializedObject(boundary).FindProperty("BattleRingRadius").floatValue == report.authoredArenaRadius,
                "Capacity adaptation overwrites the serialized ordinary radius.");
            Require(File.ReadAllText(groundPath) == before, "Battlefield source asset changed.");
            foreach (var pair in nativeSources) Require(File.ReadAllText(pair.Key) == pair.Value, "Native ring prefab changed: " + pair.Key);
        }
        finally
        {
            boundary.ConfigureBattleRadius(false);
            typeof(BoundaryControlByGod).GetField("battleGround", fields).SetValue(boundary, null);
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    sealed class ParticleDefaults
    {
        public readonly Vector3 Multipliers;
        public readonly ParticleSystemScalingMode Mode;
        public readonly ParticleSystemSimulationSpace Space;
        public readonly bool ThreeDimensions;
        public Vector3 EmittedSize;
        public ParticleDefaults(ParticleSystem particles)
        {
            var main = particles.main;
            Multipliers = new Vector3(main.startSizeXMultiplier, main.startSizeYMultiplier, main.startSizeZMultiplier);
            Mode = main.scalingMode; Space = main.simulationSpace; ThreeDimensions = main.startSize3D;
        }
    }

    static Vector3 ReadParticleSize(ParticleSystem particles)
    {
        var emitted = new ParticleSystem.Particle[Mathf.Max(1, particles.main.maxParticles)];
        int count = particles.GetParticles(emitted);
        Require(count > 0, "Native boundary particle emitted no geometry: " + particles.name);
        return emitted[0].GetCurrentSize3D(particles);
    }

    static void CheckRingParticleSizes(BoundaryControlByGod boundary, IEnumerable<ParticleSystem> roots,
        Dictionary<ParticleSystem, ParticleDefaults> defaults, Report report)
    {
        float ratio = boundary.ArenaScale;
        foreach (var pair in defaults)
        {
            var main = pair.Key.main; var original = pair.Value;
            Require(main.scalingMode == original.Mode && main.simulationSpace == original.Space && main.startSize3D == original.ThreeDimensions,
                "Arena adaptation changed native particle scaling/simulation mode: " + pair.Key.name);
            float particleRatio = original.Mode == ParticleSystemScalingMode.Hierarchy ? 1 : ratio;
            var expected = new Vector3(original.Multipliers.x * particleRatio,
                original.Multipliers.y * (original.ThreeDimensions ? particleRatio : 1),
                original.Multipliers.z * (original.ThreeDimensions ? particleRatio : 1));
            var actual = new Vector3(main.startSizeXMultiplier, main.startSizeYMultiplier, main.startSizeZMultiplier);
            Require((actual - expected).sqrMagnitude < .001f,
                "Native particle size multipliers do not follow Group/restored radius: " + pair.Key.name + ": " + actual + " vs " + expected);
        }
        // Continue from the production state. Restart=false is intentional: an
        // omitted production clear/re-emission leaves old World particles here.
        foreach (var root in roots) root.Simulate(.6f, true, false, true);
        foreach (var pair in defaults)
        {
            if (pair.Value.Mode == ParticleSystemScalingMode.Hierarchy || !pair.Key.gameObject.activeInHierarchy) continue;
            var emitted = new ParticleSystem.Particle[Mathf.Max(1, pair.Key.main.maxParticles)];
            int count = pair.Key.GetParticles(emitted);
            Require(count > 0, "Native boundary ring has no active particles after radius change.");
            var expected = pair.Value.EmittedSize * ratio;
            for (int index = 0; index < count; index++)
            {
                var actual = emitted[index].GetCurrentSize3D(pair.Key);
                float error = pair.Value.ThreeDimensions ? (actual - expected).sqrMagnitude : Mathf.Pow(actual.x - expected.x, 2);
                Require(error <= Mathf.Max(.001f, expected.sqrMagnitude * .000001f),
                    "Old or incorrectly sized World-space ring particle survives arena change: " + pair.Key.name);
            }
            report.emittedRingSizes.Add(pair.Key.name + ": radiusRatio=" + ratio + ", actualParticleSize=" + emitted[0].GetCurrentSize3D(pair.Key) + ", activeParticles=" + count);
        }
        report.ringParticleCases++;
    }

    static void CheckScaledCapacity(BoundaryControlByGod boundary, GameObject ground, Vector3 groundScale, Dictionary<ParticleSystem, Vector3> rings)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        float sensorRadius = (float)typeof(SensorUnity).GetField("_sensorRadius", fields).GetValue(boundary.SensorUnity);
        Require(Mathf.Abs(sensorRadius - boundary.EffectiveBattleRadius) < Epsilon, "Sensor range disagrees with Group arena.");
        float ratio = boundary.ArenaScale;
        Require((ground.transform.localScale - new Vector3(groundScale.x * ratio, groundScale.y, groundScale.z * ratio)).sqrMagnitude < Epsilon * Epsilon,
            "Battlefield/floor capacity does not scale XZ while preserving height.");
        foreach (var pair in rings)
            Require((pair.Key.transform.localScale - pair.Value * ratio).sqrMagnitude < Epsilon * Epsilon,
                "Visual boundary ring does not preserve its three authored axis proportions; rotated particle meshes can become elliptical.");
    }

    static IEnumerable<Vector2> PlanarCorners(Bounds box)
    {
        yield return new Vector2(box.min.x, box.min.z); yield return new Vector2(box.max.x, box.min.z);
        yield return new Vector2(box.min.x, box.max.z); yield return new Vector2(box.max.x, box.max.z);
    }

    static Vector3 Forward(IReadOnlyList<Pose> points)
    {
        var forward = points[0].rotation * Vector3.forward; forward.y = 0; return forward.normalized;
    }
    static float MinimumSpacing(Pose[] poses)
    {
        float result = float.PositiveInfinity;
        for (int i = 0; i < poses.Length; i++) for (int j = i + 1; j < poses.Length; j++)
            result = Mathf.Min(result, Vector2.Distance(new Vector2(poses[i].position.x, poses[i].position.z), new Vector2(poses[j].position.x, poses[j].position.z)));
        return result;
    }
    static void EqualPose(Pose first, Pose second, string error)
        => Require((first.position - second.position).sqrMagnitude < Epsilon * Epsilon && Quaternion.Angle(first.rotation, second.rotation) < Epsilon, error);
    static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y)
        && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
