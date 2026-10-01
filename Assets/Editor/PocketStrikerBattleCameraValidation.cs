using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Projects complete fighter bounds through the production battle framing pose without starting combat.</summary>
public static class PocketStrikerBattleCameraValidation
{
    const string Output = "Logs/CameraFraming";

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public int framingCases;
        public int cornersProjected;
        public int transitionCases;
        public int selectionCases;
        public int viewportCases;
        public int modelBoundsCases;
        public int centerLagCases;
        public int profileCases;
        public string scope = "Production BattleCameraFraming with perspective Camera.WorldToViewportPoint: complete AABB corners, portrait phone/tablet aspect ratios, safe-area/HUD usable rectangles, 25/45/60 degree field of view, production Duel32/Multi33/Group46 and 30/45/65/90 degree pitch, 0.5/1/2 fighter scale, narrow/split/tall/airborne/200-fighter layouts. Checks independent profiles and mixed-team routing, emergency expansion, gradual shrink, center lag, production target selection, safe-area/HUD exclusion and local model renderer bounds.";
        public string limitation = "Offline geometric validation. No account, prefab gameplay, Addressables, render pipeline or battle simulation is initialized. Device safe-area rectangles are explicit fixture inputs. Runtime manager integration and moving animation bounds require Play-mode smoke.";
        public List<string> errors = new List<string>();
        public List<string> observations = new List<string>();
        public List<string> projections = new List<string>();
    }

    readonly struct ViewportCase
    {
        public readonly string Name;
        public readonly float Aspect;
        public readonly Rect Usable;
        public ViewportCase(string name, float aspect, Rect usable) { Name = name; Aspect = aspect; Usable = usable; }
    }

    [MenuItem("PocketStriker/Validation/Battle Camera Framing")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_BATTLE_CAMERA_PASSED: " + report.framingCases + " framing cases, " + report.cornersProjected + " projected corners.");
    }

    public static void ValidateBatch()
    {
        var report = Run();
        Debug.Log("[BattleCamera] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Battle camera validation requires a stopped editor.");
        Directory.CreateDirectory(Output);
        var report = new Report { unityVersion = Application.unityVersion };
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Battle Camera Geometry Validation");
        rig.SetActive(false); SceneManager.MoveGameObjectToScene(rig, scene);
        var camera = rig.AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = false; camera.nearClipPlane = 0.3f; camera.farClipPlane = 10000;
        try
        {
            var viewports = new[]
            {
                new ViewportCase("phone", 9f / 16, new Rect(0.16f, 0.18f, 0.81f, 0.74f)),
                new ViewportCase("compact-phone", 375f / 667, new Rect(0.16f, 0.18f, 0.81f, 0.74f)),
                new ViewportCase("notched-phone", 390f / 844, new Rect(0.16f, 0.205f, 0.81f, 0.695f)),
                new ViewportCase("tablet", 3f / 4, new Rect(0.10f, 0.14f, 0.87f, 0.80f)),
                new ViewportCase("group-tablet", 834f / 1194, new Rect(0.03f, 0.06f, 0.94f, 0.87f))
            };
            foreach (var viewport in viewports)
            foreach (float pitch in new[] { BattleCameraProfiles.Duel.Pitch, BattleCameraProfiles.MultiRaid.Pitch,
                BattleCameraProfiles.Group.Pitch, 30f, 45f, 65f, 90f })
            foreach (float fov in new[] { 25f, 45f, 60f })
            foreach (float scale in new[] { 0.5f, 1f, 2f })
            foreach (var formation in Formations(scale))
            {
                var label = viewport.Name + "/pitch-" + pitch + "/fov-" + fov + "/scale-" + scale + "/" + formation.Key;
                try
                {
                    var pose = BattleCameraFraming.CalculatePose(formation.Value, viewport.Aspect, fov, viewport.Usable, pitch);
                    Configure(camera, pose, viewport.Aspect, fov);
                    CheckProjection(camera, formation.Value, viewport.Usable, label, report);
                    report.framingCases++;
                    if (scale == 1 && pitch == 45 && fov == 45 && formation.Key == "two-hundred" && (viewport.Name == "phone" || viewport.Name == "tablet"))
                    {
                        string path = Path.Combine(Output, "projected-bounds-" + viewport.Name + ".png");
                        SaveProjection(camera, formation.Value, viewport.Usable, path, viewport.Aspect); report.projections.Add(path);
                    }
                }
                catch (Exception exception) { if (report.errors.Count < 100) report.errors.Add(label + ": " + exception.GetBaseException().Message); }
            }
            CheckSpecialCases(camera, report);
            CheckProfiles(report);
            CheckTransitions(camera, report);
            CheckSelection(camera, report);
            CheckUsableViewports(report);
            CheckModelBounds(camera, scene, report);
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig); EditorSceneManager.ClosePreviewScene(scene);
            report.passed = report.errors.Count == 0 && report.framingCases == 1890 && report.profileCases >= 21 && report.transitionCases >= 6
                && report.selectionCases >= 24 && report.viewportCases >= 5 && report.modelBoundsCases >= 6 && report.centerLagCases >= 6;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckProfiles(Report report)
    {
        var modes = new AllUnitsBattleCamera[] { new DuelBattleCamera(), new MultiRaidBattleCamera(), new GroupBattleCamera() };
        var profiles = new[] { BattleCameraProfiles.Duel, BattleCameraProfiles.MultiRaid, BattleCameraProfiles.Group };
        for (int index = 0; index < modes.Length; index++)
        {
            Require(Mathf.Abs(modes[index].Pitch - profiles[index].Pitch) < .001f
                && Mathf.Abs(modes[index].fieldOfView - profiles[index].FieldOfView) < .001f,
                "Independent battle camera class does not apply its profile.");
            report.profileCases++;
        }
        Require(BattleCameraProfiles.Duel.Pitch >= 30 && BattleCameraProfiles.Duel.Pitch <= 35,
            "Duel camera must look over the fighters without becoming an overhead crowd view.");
        Require(Mathf.Abs(BattleCameraProfiles.MultiRaid.Pitch - Mathf.Atan2(15 - 2, 20) * Mathf.Rad2Deg) < .2f,
            "MultiRaid pitch no longer matches the previous camera elevation.");
        Require(BattleCameraProfiles.Group.Pitch > BattleCameraProfiles.MultiRaid.Pitch
            && BattleCameraProfiles.Group.Pitch < 50, "Group pitch is not a moderate independent elevation.");
        foreach (TeamMode first in Enum.GetValues(typeof(TeamMode)))
        foreach (TeamMode second in Enum.GetValues(typeof(TeamMode)))
        foreach (bool group in new[] { false, true })
        {
            var expected = group ? C_Mode.TopDown : first == TeamMode.Rotation && second == TeamMode.Rotation
                ? C_Mode.CertainYAntiVibration : C_Mode.WatchOver;
            Require(BattleCameraProfiles.ResolveMode(group, first, second) == expected,
                "Battle camera routing ignores the opponent's fielded mode.");
            report.profileCases++;
        }
        report.observations.Add("Duel32 raises the view over the fighters; Multi33 and Group46 retain their independent elevations. A rotation player against multiple fielded enemies uses MultiRaid framing.");
    }

    static IEnumerable<KeyValuePair<string, List<Bounds>>> Formations(float scale)
    {
        yield return Pair("duel", new[] { Fighter(new Vector3(-2, 0, 0), scale), Fighter(new Vector3(2, 0, 0), scale) });
        yield return Pair("split-teams-focus-left", new[] { Fighter(new Vector3(-20, 0, 0), scale), Fighter(new Vector3(-2, 0, -12), scale), Fighter(new Vector3(20, 0, 0), scale), Fighter(new Vector3(2, 0, 12), scale) });
        yield return Pair("tall-and-airborne", new[] { Fighter(new Vector3(-8, 0, -8), scale, 4.5f), Fighter(new Vector3(8, 7, 8), scale, 3.5f) });
        yield return Pair("same-center-wide-spread", new[] { Fighter(new Vector3(-30, 0, -20), scale), Fighter(new Vector3(30, 0, 20), scale), Fighter(new Vector3(30, 0, -20), scale), Fighter(new Vector3(-30, 0, 20), scale) });
        var crowd = new List<Bounds>();
        for (int index = 0; index < 200; index++)
        {
            float angle = index * 2.399963f, radius = 28 * Mathf.Sqrt((index + 1f) / 200);
            crowd.Add(Fighter(new Vector3(Mathf.Cos(angle) * radius, index % 17 == 0 ? 2 : 0, Mathf.Sin(angle) * radius), scale, index % 11 == 0 ? 3.5f : 2.2f));
        }
        yield return Pair("two-hundred", crowd);
        yield return Pair("translated-arena", new[] { Fighter(new Vector3(980, -15, -720), scale), Fighter(new Vector3(1020, -12, -690), scale, 4) });
    }

    static Bounds Fighter(Vector3 feet, float scale, float height = 2.2f)
        => new Bounds(feet + Vector3.up * height * scale / 2, new Vector3(0.9f, height, 0.9f) * scale);
    static KeyValuePair<string, List<Bounds>> Pair(string name, IEnumerable<Bounds> bounds) => new KeyValuePair<string, List<Bounds>>(name, bounds.ToList());

    static void Configure(Camera camera, BattleCameraFraming.Pose pose, float aspect, float fov)
    {
        Require(Finite(pose.Position) && Finite(pose.Center) && Finite(pose.Distance) && pose.Distance > 0, "Framing returns a nonfinite/invalid pose.");
        Require(Finite(pose.Rotation.x) && Finite(pose.Rotation.y) && Finite(pose.Rotation.z) && Finite(pose.Rotation.w), "Framing rotation is invalid.");
        camera.aspect = aspect; camera.fieldOfView = fov;
        camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
    }

    static void CheckProjection(Camera camera, IEnumerable<Bounds> fighters, Rect usable, string label, Report report)
    {
        foreach (var bounds in fighters)
        foreach (var point in Corners(bounds))
        {
            var viewport = camera.WorldToViewportPoint(point);
            Require(Finite(viewport) && viewport.z > camera.nearClipPlane, label + " corner is behind the near plane: " + viewport);
            const float tolerance = 0.0002f;
            Require(viewport.x >= usable.xMin - tolerance && viewport.x <= usable.xMax + tolerance
                && viewport.y >= usable.yMin - tolerance && viewport.y <= usable.yMax + tolerance,
                label + " corner escapes usable viewport: " + viewport + " vs " + usable + " at " + point);
            report.cornersProjected++;
        }
    }

    static IEnumerable<Vector3> Corners(Bounds bounds)
    {
        var min = bounds.min; var max = bounds.max;
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            yield return new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
    }

    static void CheckSpecialCases(Camera camera, Report report)
    {
        var one = new List<Bounds> { Fighter(Vector3.zero, 1) };
        var viewport = new Rect(0.22f, 0.27f, 0.73f, 0.62f);
        foreach (float yaw in new[] { 0f, 45f, 90f, 179f, 270f })
        {
            var pose = BattleCameraFraming.CalculatePose(one, 9f / 16, 45, viewport, 45, yaw);
            Configure(camera, pose, 9f / 16, 45); CheckProjection(camera, one, viewport, "single/asymmetric/yaw-" + yaw, report);
        }
        var zero = new List<Bounds> { new Bounds(new Vector3(12, 0, -4), Vector3.zero) };
        var zeroPose = BattleCameraFraming.CalculatePose(zero, 9f / 16, 45, viewport, 45);
        Configure(camera, zeroPose, 9f / 16, 45); CheckProjection(camera, zero, viewport, "zero-size", report);
        var narrow = new List<Bounds> { Fighter(new Vector3(-1, 0, 0), 1), Fighter(new Vector3(1, 0, 0), 1) };
        var wide = new List<Bounds> { Fighter(new Vector3(-25, 0, 0), 1), Fighter(new Vector3(25, 0, 0), 1) };
        var compact = BattleCameraFraming.CalculatePose(narrow, 9f / 16, 45, viewport, 45);
        var spread = BattleCameraFraming.CalculatePose(wide, 9f / 16, 45, viewport, 45);
        Require(spread.Distance > compact.Distance, "Equal average centers conceal the wider fighter spread.");
        foreach (float yaw in new[] { 0f, 90f })
        foreach (var lag in new[] { new Vector3(-18, 0, 8), new Vector3(18, 6, -8), new Vector3(0, -4, 20) })
        {
            var rotation = Quaternion.Euler(45, yaw, 0);
            var pose = BattleCameraFraming.CalculatePoseAtCenter(wide, 9f / 16, 45, viewport, rotation, spread.Center + lag);
            Configure(camera, pose, 9f / 16, 45);
            CheckProjection(camera, wide, viewport, "center-lag/yaw-" + yaw + "/" + lag, report);
            report.centerLagCases++;
        }
        report.observations.Add("Equal-center wide formations require more distance than close formations; complete body corners, including height and scaled bounds, stay in the usable viewport.");
    }

    static void CheckTransitions(Camera camera, Report report)
    {
        var viewport = new Rect(0.16f, 0.18f, 0.81f, 0.74f);
        var close = new List<Bounds> { Fighter(new Vector3(-2, 0, 0), 1), Fighter(new Vector3(2, 0, 0), 1) };
        var far = new List<Bounds> { Fighter(new Vector3(-28, 0, 0), 1), Fighter(new Vector3(28, 0, 0), 1) };
        var compact = BattleCameraFraming.CalculatePose(close, 9f / 16, 45, viewport, 45);
        var expanded = BattleCameraFraming.CalculatePose(far, 9f / 16, 45, viewport, 45);
        float outward = BattleCameraFraming.SmoothDistance(compact.Distance, expanded.Distance, 1f / 60);
        Require(outward >= expanded.Distance - 0.0001f, "Emergency expansion leaves a fighter clipped for one frame.");
        var outwardPose = PoseAtDistance(expanded, outward);
        Configure(camera, outwardPose, 9f / 16, 45); CheckProjection(camera, far, viewport, "one-frame-expansion", report); report.transitionCases++;
        float shrinking = BattleCameraFraming.SmoothDistance(outward, compact.Distance, 1f / 60);
        Require(shrinking < outward && shrinking > compact.Distance, "Removing distant/dead fighters should shrink gradually.");
        var shrinkPose = PoseAtDistance(compact, shrinking);
        Configure(camera, shrinkPose, 9f / 16, 45); CheckProjection(camera, close, viewport, "death-shrink", report); report.transitionCases++;
        float prior = shrinking;
        float initialDelta = shrinking - compact.Distance;
        for (int index = 0; index < 600; index++)
        {
            float next = BattleCameraFraming.SmoothDistance(prior, compact.Distance, 1f / 60);
            Require(next <= prior + 0.0001f && next >= compact.Distance - 0.0001f, "Shrinking camera overshoots or grows without a new fighter."); prior = next;
        }
        // Production uses a two-second exponential time constant: ten seconds
        // leaves e^-5 (about 0.67%) of the initial distance, not a fixed 0.01 unit.
        Require(prior - compact.Distance <= initialDelta * .008f + .0001f,
            "Camera does not follow the gradual two-second shrink after distant fighters leave.");
        report.transitionCases++;
        // The close pair must become readable promptly, at several frame rates,
        // without losing emergency expansion or changing crowd-camera behavior.
        foreach (int fps in new[] { 30, 60, 120 })
        {
            float duel = expanded.Distance;
            float crowd = expanded.Distance;
            for (int frame = 0; frame < fps * 2; frame++)
            {
                duel = BattleCameraFraming.SmoothDistance(duel, compact.Distance, 1f / fps, BattleCameraProfiles.Duel.DistanceSmoothTime);
                crowd = BattleCameraFraming.SmoothDistance(crowd, compact.Distance, 1f / fps, BattleCameraProfiles.MultiRaid.DistanceSmoothTime);
                Require(duel >= compact.Distance && crowd >= compact.Distance, "Camera shrink cropped the near pair.");
            }
            Require(duel - compact.Distance < (expanded.Distance - compact.Distance) * .02f,
                "Duel retains a wide shot after two seconds of close combat.");
            Require(crowd - compact.Distance > (expanded.Distance - compact.Distance) * .3f,
                "Duel tuning unexpectedly changes the slower crowd framing.");
            Require(BattleCameraFraming.SmoothDistance(duel, expanded.Distance, 1f / fps,
                BattleCameraProfiles.Duel.DistanceSmoothTime) >= expanded.Distance, "Fast duel return lost immediate separation safety.");
            report.transitionCases++;
        }
    }

    static BattleCameraFraming.Pose PoseAtDistance(BattleCameraFraming.Pose target, float distance)
        => BattleCameraFraming.WithDistance(target, distance, 9f / 16, 45);

    static void CheckSelection(Camera camera, Report report)
    {
        foreach (TeamMode mode in Enum.GetValues(typeof(TeamMode)))
        foreach (bool active in new[] { false, true })
        foreach (bool dead in new[] { false, true })
        foreach (bool fielded in new[] { false, true })
        {
            bool expected = active && !dead && (mode != TeamMode.Rotation || fielded);
            Require(BattleCameraFraming.ShouldIncludeUnit(mode, active, dead, fielded) == expected,
                "Incorrect target selection: " + mode + "/active=" + active + "/dead=" + dead + "/fielded=" + fielded);
            report.selectionCases++;
        }

        var usable = new Rect(0.16f, 0.18f, 0.81f, 0.74f);
        // These are unit bounds after the production selector, not a copied
        // camera algorithm. A focused ally must not hide a distant live ally.
        var source = new[]
        {
            Fighter(new Vector3(-2, 0, 0), 1), Fighter(new Vector3(-24, 0, 12), 1),
            Fighter(new Vector3(2, 0, 0), 1), Fighter(new Vector3(24, 0, -12), 1),
            Fighter(new Vector3(200, 0, 0), 1), Fighter(new Vector3(-200, 0, 0), 1)
        };
        var activeFlags = new[] { true, true, true, true, false, true };
        var deadFlags = new[] { false, false, false, false, false, true };
        var fieldedFlags = new[] { true, false, true, false, false, false };
        foreach (var mode in new[] { TeamMode.MultiRaid, TeamMode.Rotation })
        {
            var selected = source.Where((bounds, index) => BattleCameraFraming.ShouldIncludeUnit(mode,
                activeFlags[index], deadFlags[index], fieldedFlags[index])).ToList();
            Require(selected.Count == (mode == TeamMode.Rotation ? 2 : 4), "Bench, inactive or dead target selection is incorrect.");
            var pose = BattleCameraFraming.CalculatePose(selected, 9f / 16, 45, usable, 45);
            Configure(camera, pose, 9f / 16, 45); CheckProjection(camera, selected, usable, "selected/" + mode, report);
            report.selectionCases++;
        }
        report.observations.Add("Rotation excludes reserves; MultiRaid/Group includes every live active fighter regardless of which ally is focused. Inactive and dead targets are excluded.");
    }

    static void CheckUsableViewports(Report report)
    {
        var inputs = new[]
        {
            new[] { new Rect(0, 0, 1, 1), new Rect(0, .18f, 1, .74f), new Rect(.02f, .45f, .12f, .40f) },
            new[] { new Rect(0, .04f, 1, .92f), new Rect(0, .21f, 1, .69f), new Rect(.02f, .43f, .14f, .42f) },
            new[] { new Rect(.04f, .03f, .92f, .94f), new Rect(0, .15f, 1, .77f), new Rect(.06f, .49f, .10f, .34f) },
            new[] { new Rect(0, .02f, 1, .96f), new Rect(0, .10f, 1, .82f), new Rect(.02f, .40f, .13f, .44f) },
            new[] { new Rect(0, 0, 1, 1), new Rect(0, .18f, 1, .74f), new Rect(.02f, .02f, .12f, .05f) }
        };
        foreach (var input in inputs)
        foreach (bool exclude in new[] { false, true })
        {
            var safe = input[0]; var middle = input[1]; var rail = input[2];
            var usable = BattleCameraFraming.CalculateUsableViewport(safe, middle, rail, exclude);
            Require(usable.width > .1f && usable.height > .1f && Finite(usable.x) && Finite(usable.y), "Usable viewport is invalid.");
            Require(Contains(safe, usable) && Contains(middle, usable), "Camera viewport escapes safe area or HUD-free middle area.");
            Require(usable.xMin >= Mathf.Max(safe.xMin, middle.xMin) + .0199f
                && usable.yMin >= Mathf.Max(safe.yMin, middle.yMin) + .0199f
                && usable.xMax <= Mathf.Min(safe.xMax, middle.xMax) - .0199f
                && usable.yMax <= Mathf.Min(safe.yMax, middle.yMax) - .0199f,
                "Camera viewport omits the safe-area/HUD edge padding.");
            bool overlapsRail = rail.yMax > Mathf.Max(safe.yMin, middle.yMin) && rail.yMin < Mathf.Min(safe.yMax, middle.yMax);
            if (exclude && overlapsRail)
                Require(usable.xMin >= rail.xMax + .0199f, "Live fighters can be covered by the portrait rail.");
            if (!exclude || !overlapsRail)
                Require(Mathf.Abs(usable.xMin - (Mathf.Max(safe.xMin, middle.xMin) + .02f)) < .0001f,
                    "Hidden or vertically separate portraits still shrink the battle view.");
            report.viewportCases++;
        }
        foreach (var input in inputs)
        foreach (bool portraits in new[] { false, true })
        {
            var full = BattleCameraFraming.CalculateUsableViewport(input[0], input[1], input[2], false);
            var duel = BattleCameraFraming.CalculateDuelViewport(input[0], input[1], input[2], portraits);
            Require(Mathf.Abs(duel.center.x - full.center.x) < .0001f, "Corner portraits bias the duel toward one team.");
            Require(Contains(full, duel) && duel.width >= .2f && duel.height >= .1f, "Duel viewport escapes its safe gameplay area.");
            Require(!portraits || !duel.Overlaps(input[2]), "Centered duel remains under the corner portraits.");
            report.viewportCases++;
        }
        var tallDuel = BattleCameraFraming.CalculateDuelViewport(new Rect(0, 0, 1, 1),
            new Rect(0, .18f, 1, .76f), new Rect(.02f, .74f, .12f, .16f), true);
        Require(tallDuel.width > .95f && tallDuel.yMax <= .7201f,
            "Portrait duel wastes horizontal space beside its short portrait rail.");
        report.viewportCases++;
        foreach (var bounds in new[] { new List<Bounds>(), null })
        {
            var empty = BattleCameraFraming.CalculatePose(bounds, 9f / 16, 45, new Rect(0, 0, 1, 1), 45);
            Require(Finite(empty.Position) && Finite(empty.Distance) && empty.Distance >= 6, "No active targets produce an invalid minimum-distance pose.");
        }
        report.observations.Add("Camera usable rectangles stay inside both the device safe area and HUD-free middle, with padding and visible portrait-rail exclusion; hidden Group rails reclaim horizontal space.");
    }

    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .0001f && inner.yMin >= outer.yMin - .0001f
        && inner.xMax <= outer.xMax + .0001f && inner.yMax <= outer.yMax + .0001f;

    static void CheckModelBounds(Camera camera, Scene scene, Report report)
    {
        const string modelPath = "Assets/ExternalAssets/Unit/human/haruka.prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Require(source != null, "Missing local haruka model.");
        string before = EditorJsonUtility.ToJson(source);
        var parent = new GameObject("Local model bounds fixture"); parent.SetActive(false);
        SceneManager.MoveGameObjectToScene(parent, scene);
        try
        {
            // Instantiate beneath an inactive parent, then remove all behaviours
            // before activation so account and combat components never initialize.
            var model = UnityEngine.Object.Instantiate(source, parent.transform, false);
            foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var behaviour in model.GetComponentsInChildren<Behaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skinned.updateWhenOffscreen = true;
            parent.SetActive(true);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Any(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer), "Local model has no real mesh renderers.");
            var scales = new[] { Vector3.one * .5f, Vector3.one, Vector3.one * 2, new Vector3(.6f, 1.8f, 1.2f),
                new Vector3(-1, 1, 1), new Vector3(1, 2, .5f) };
            foreach (var scale in scales)
            {
                parent.transform.SetPositionAndRotation(new Vector3(8, 1, -7), Quaternion.Euler(0, 37, 0));
                parent.transform.localScale = scale;
                Require(BattleCameraFraming.TryGetModelBounds(model.transform, out var actual), "Local model bounds are unavailable at root scale " + scale);
                Require(Finite(actual.center) && Finite(actual.size) && actual.size.y > .1f, "Local model bounds are invalid.");
                foreach (var renderer in renderers)
                {
                    if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    foreach (var corner in Corners(renderer.bounds))
                        Require(actual.SqrDistance(corner) < .0001f, "Production bounds omit actual renderer geometry at root scale " + scale);
                }
                var usable = new Rect(.16f, .18f, .81f, .74f);
                var bounds = new List<Bounds> { actual };
                var pose = BattleCameraFraming.CalculatePose(bounds, 9f / 16, 45, usable, 45);
                Configure(camera, pose, 9f / 16, 45); CheckProjection(camera, bounds, usable, "local-model/root-scale-" + scale, report);
                report.modelBoundsCases++;
            }
            var filtered = new GameObject("Renderer filtering"); filtered.transform.SetParent(parent.transform, false);
            var near = GameObject.CreatePrimitive(PrimitiveType.Cube); near.transform.SetParent(filtered.transform, false);
            var far = GameObject.CreatePrimitive(PrimitiveType.Cube); far.transform.SetParent(filtered.transform, false);
            far.transform.localPosition = Vector3.right * 200; far.GetComponent<Renderer>().enabled = false;
            var inactive = GameObject.CreatePrimitive(PrimitiveType.Cube); inactive.transform.SetParent(filtered.transform, false);
            inactive.transform.localPosition = Vector3.left * 200; inactive.SetActive(false);
            var trail = new GameObject("Projectile trail", typeof(TrailRenderer)); trail.transform.SetParent(filtered.transform, false);
            trail.GetComponent<TrailRenderer>().AddPositions(new[] { Vector3.one * 200, Vector3.one * 220 });
            Require(BattleCameraFraming.TryGetModelBounds(filtered.transform, out var visible), "Visible mesh was omitted.");
            var expected = near.GetComponent<Renderer>().bounds;
            Require((visible.center - expected.center).sqrMagnitude < .0001f && (visible.size - expected.size).sqrMagnitude < .0001f,
                "Disabled/inactive mesh or projectile trail inflated model bounds.");
            Require(!BattleCameraFraming.TryGetModelBounds((Transform)null, out _), "Null model unexpectedly has bounds.");
            near.SetActive(false);
            Require(!BattleCameraFraming.TryGetModelBounds(filtered.transform, out _), "Invisible meshes or effects became model bounds.");
            report.modelBoundsCases++;
            Require(EditorJsonUtility.ToJson(source) == before, "Local model source asset changed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
        report.observations.Add("Actual local haruka Mesh/SkinnedMesh renderer bounds fit after uniform, nonuniform and mirrored parent scale; disabled/inactive meshes and projectile trails are excluded.");
    }

    static void SaveProjection(Camera camera, IReadOnlyList<Bounds> fighters, Rect usable, string path, float aspect)
    {
        int height = 900, width = Mathf.RoundToInt(height * aspect);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = Enumerable.Repeat(new Color(0.055f, 0.075f, 0.09f, 1), width * height).ToArray();
        void Line(int x0, int y0, int x1, int y1, Color color)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int index = 0; index <= steps; index++)
            {
                float t = steps == 0 ? 0 : (float)index / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = color;
            }
        }
        void Box(Rect rect, Color color)
        {
            int left = Mathf.RoundToInt(rect.xMin * (width - 1)), right = Mathf.RoundToInt(rect.xMax * (width - 1));
            int bottom = Mathf.RoundToInt(rect.yMin * (height - 1)), top = Mathf.RoundToInt(rect.yMax * (height - 1));
            Line(left, bottom, right, bottom, color); Line(right, bottom, right, top, color); Line(right, top, left, top, color); Line(left, top, left, bottom, color);
        }
        Box(usable, new Color(0.9f, 0.78f, 0.4f));
        for (int index = 0; index < fighters.Count; index++)
        {
            var points = Corners(fighters[index]).Select(camera.WorldToViewportPoint).ToArray();
            var rect = Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
            Box(rect, index % 2 == 0 ? new Color(0.42f, 0.8f, 0.73f) : new Color(0.84f, 0.52f, 0.45f));
        }
        try { image.SetPixels(pixels); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
