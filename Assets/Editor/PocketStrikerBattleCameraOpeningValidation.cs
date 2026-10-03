using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Diagonal composition and full-model geometry, without combat initialization.</summary>
public static class PocketStrikerBattleCameraOpeningValidation
{
    const string Output = "Logs/CameraFraming/Opening/Geometry";

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion, utcTime;
        public string scope = "Production diagonal yaw solver, full renderer/body-envelope fit and stabilizer for Duel/MultiRaid, plus a synthetic 46-degree crowd profile; five phone/tablet aspects, four world-axis headings, duel/near/tall/multi/200-unit formations. Every local human model prefab is paired against haruka at all five aspects. Full-model corners and pixel-space team axis are checked with Unity Camera.WorldToViewportPoint. Side-view distance is a controlled same-model geometric comparison, not a historic runtime measurement. Independent Group camera frames the battlefield and does not use this diagonal solver.";
        public string limitation = "Stopped-editor geometry using actual imported neutral model bounds; it does not animate, initialize gameplay or emulate device performance. Opening/replacement callbacks and actual battle rendering require the separate Playmode opening review.";
        public int cases, actualModelPairs, projectedCorners;
        public List<Sample> samples = new List<Sample>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class Sample
    {
        public string name, profile;
        public float aspect, yaw, pixelAxisAngle, distance, sideViewDistance, distanceRatio;
        public Vector3 playerViewport, opponentViewport;
        public int bodies, clippedCorners;
    }

    readonly struct Device
    {
        public readonly string Name;
        public readonly float Aspect;
        public readonly Rect Usable;
        public Device(string name, float aspect, Rect usable) { Name = name; Aspect = aspect; Usable = usable; }
    }

    [MenuItem("PocketStriker/Validation/Battle Camera Opening")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_CAMERA_OPENING_PASSED: " + report.cases + " cases, "
            + report.actualModelPairs + " actual model pairs, " + report.projectedCorners + " full-model corners.");
    }

    public static void ValidateBatch()
    {
        var report = Run();
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        else Debug.Log("POCKETSTRIKER_CAMERA_OPENING_PASSED: " + report.cases + " cases.");
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Camera opening validation requires a stopped editor.");
        Directory.CreateDirectory(Output);
        var result = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Diagonal battle camera geometry"); rig.SetActive(false);
        SceneManager.MoveGameObjectToScene(rig, scene);
        var camera = rig.AddComponent<Camera>(); camera.enabled = false;
        camera.nearClipPlane = .3f; camera.farClipPlane = 10000;
        var devices = new[]
        {
            new Device("375x667", 375f / 667, new Rect(.02f, .22f, .96f, .5f)),
            new Device("390x844", 390f / 844, new Rect(.02f, .24f, .96f, .5f)),
            new Device("540x960", 540f / 960, new Rect(.02f, .22f, .96f, .5f)),
            new Device("834x1194", 834f / 1194, new Rect(.02f, .18f, .96f, .63f)),
            new Device("1440x1920", 3f / 4, new Rect(.02f, .18f, .96f, .63f))
        };
        try
        {
            foreach (var device in devices)
            foreach (var profile in new[] { BattleCameraProfiles.Duel, BattleCameraProfiles.MultiRaid, BattleCameraProfiles.Group })
            foreach (float heading in new[] { 0f, 90f, 173f, 270f })
            foreach (string formation in new[] { "duel", "near", "tall", "tall-anchors", "multi-six", "group-200" })
            {
                var rotation = Quaternion.Euler(0, heading, 0);
                float gap = formation == "near" ? 1.1f : formation == "group-200" ? 22 : 3;
                var player = rotation * new Vector3(0, 0, -gap);
                var enemy = rotation * new Vector3(0, 0, gap);
                var bounds = new List<Bounds>();
                int teamCount = formation == "group-200" ? 100 : formation == "multi-six" ? 3 : 1;
                for (int team = 0; team < 2; team++)
                for (int index = 0; index < teamCount; index++)
                {
                    float height = formation.StartsWith("tall") && team == 1 ? 5.3f : 2.2f;
                    var offset = teamCount > 3 ? new Vector3(index % 10 * 1.6f - 7.2f, 0, index / 10 * 1.6f - 7.2f)
                        : new Vector3((index - (teamCount - 1) * .5f) * 1.8f, 0, 0);
                    var anchor = (team == 0 ? player : enemy) + rotation * offset;
                    bounds.Add(new Bounds(anchor + Vector3.up * height * .5f, new Vector3(1.2f, height, 1.2f)));
                }
                var playerAnchor = player + Vector3.up * (formation == "tall-anchors" ? 1.1f : 0);
                var enemyAnchor = enemy + Vector3.up * (formation == "tall-anchors" ? 2.6f : 0);
                Test(result, camera, device, profile, bounds, playerAnchor, enemyAnchor, formation + "/heading-" + heading);
            }
            TestActualModels(result, camera, scene, devices);
        }
        catch (Exception error) { result.errors.Add(error.ToString()); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig); EditorSceneManager.ClosePreviewScene(scene);
            result.passed = result.errors.Count == 0 && result.cases >= 400 && result.actualModelPairs >= 95;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(result, true));
        }
        return result;
    }

    static void Test(Report report, Camera camera, Device device, BattleCameraProfile profile,
        List<Bounds> raw, Vector3 player, Vector3 enemy, string label)
    {
        var whole = raw[0]; foreach (var bound in raw.Skip(1)) whole.Encapsulate(bound);
        var envelopes = raw.Select(b => new BattleCameraFraming.BodyEnvelope(b.center,
            new Vector2(b.extents.x, b.extents.z).magnitude + .15f, b.size.y + .5f)).ToList();
        var padded = raw.Select(b => { b.Expand(new Vector3(.25f, .5f, .25f)); return b; }).ToList();
        float yaw = BattleCameraOpening.CalculateYaw(player, enemy, profile, padded, envelopes,
            whole.center, device.Aspect, device.Usable);
        var stabilizer = new BattleCameraStabilizer(profile); stabilizer.Reset(yaw);
        var pose = stabilizer.Update(padded, whole.center, device.Aspect, profile.FieldOfView, device.Usable, .3f, 0, false, envelopes);
        camera.aspect = device.Aspect; camera.fieldOfView = profile.FieldOfView;
        camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
        var pv = camera.WorldToViewportPoint(player); var ev = camera.WorldToViewportPoint(enemy);
        var pixelDelta = new Vector2((ev.x - pv.x) * device.Aspect, ev.y - pv.y);
        var sample = new Sample { name = device.Name + "/" + label, profile = profile.Pitch.ToString(),
            aspect = device.Aspect, yaw = yaw, playerViewport = pv, opponentViewport = ev,
            pixelAxisAngle = Mathf.Atan2(pixelDelta.y, -pixelDelta.x) * Mathf.Rad2Deg,
            distance = pose.Distance, bodies = raw.Count };
        report.samples.Add(sample);
        if (pv.x <= ev.x || pv.y >= ev.y || Mathf.Abs(sample.pixelAxisAngle - 45) > .1f)
            report.errors.Add(sample.name + ": team axis must put player lower-right at 45 degrees; actual=" + sample.pixelAxisAngle);
        foreach (var b in raw)
        for (int corner = 0; corner < 8; corner++)
        {
            var vp = camera.WorldToViewportPoint(b.center + Vector3.Scale(b.extents,
                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
            report.projectedCorners++;
            if (vp.z <= .3f || vp.x < device.Usable.xMin - .0002f || vp.x > device.Usable.xMax + .0002f
                || vp.y < device.Usable.yMin - .0002f || vp.y > device.Usable.yMax + .0002f) sample.clippedCorners++;
        }
        if (sample.clippedCorners > 0) report.errors.Add(sample.name + ": clipped " + sample.clippedCorners + " full-model corners.");
        float heading = Mathf.Atan2(enemy.x - player.x, enemy.z - player.z) * Mathf.Rad2Deg;
        var baseline = BattleCameraFraming.CalculatePoseAtCenter(padded, device.Aspect, profile.FieldOfView,
            device.Usable, Quaternion.Euler(profile.Pitch, heading + 90, 0), whole.center, 6, .3f, envelopes);
        sample.sideViewDistance = baseline.Distance * (1 + profile.FramingReserve);
        sample.distanceRatio = sample.distance / sample.sideViewDistance;
        report.cases++;
    }

    static void TestActualModels(Report report, Camera camera, Scene scene, Device[] devices)
    {
        var parent = new GameObject("Actual local model opening pairs"); parent.SetActive(false);
        SceneManager.MoveGameObjectToScene(parent, scene);
        try
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ExternalAssets/Unit/human" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            foreach (var path in paths)
            {
                parent.SetActive(false);
                var first = Model("Assets/ExternalAssets/Unit/human/haruka.prefab", parent.transform);
                var second = Model(path, parent.transform);
                first.transform.SetPositionAndRotation(new Vector3(0, 0, -3), Quaternion.identity);
                second.transform.SetPositionAndRotation(new Vector3(0, 0, 3), Quaternion.Euler(0, 180, 0));
                parent.SetActive(true);
                if (!BattleCameraFraming.TryGetModelBounds(first.transform, out var one)
                    || !BattleCameraFraming.TryGetModelBounds(second.transform, out var two))
                    throw new InvalidOperationException("Missing actual full model bounds: " + path);
                foreach (var device in devices)
                {
                    Test(report, camera, device, BattleCameraProfiles.Duel, new List<Bounds> { one, two },
                        first.transform.position, second.transform.position, "actual/" + Path.GetFileNameWithoutExtension(path));
                    report.actualModelPairs++;
                }
                UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    static GameObject Model(string path, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing actual model " + path);
        var model = UnityEngine.Object.Instantiate(prefab, parent, false);
        foreach (var particle in model.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
        foreach (var behaviour in model.GetComponentsInChildren<Behaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
        return model;
    }
}
