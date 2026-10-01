using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Deterministic motion regression through the production battle-camera stabilizer.</summary>
public static class PocketStrikerBattleCameraStabilityValidation
{
    const string Output = "Logs/CameraFraming/Stability";
    const float Aspect = 390f / 844;
    static readonly Rect Usable = new Rect(.16f, .205f, .81f, .695f);

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string scope = "Production BattleCameraStabilizer, Duel/MultiRaid/Group profiles, 30/60/120 fps; oscillating animation bounds, high-frequency root punch, deliberate pan and orbit, abrupt separation/airborne bounds, pause, replacement hold and reset. Fixed-root slender bodies make repeated quarter/half and continuous turns at fixed camera yaw, using production neutral cylindrical envelopes; their startup distance is compared with the prior square envelope. A real large displacement must still expand immediately. Every raw body-bound corner projects through Unity Camera.WorldToViewportPoint on every moving frame. Fixed-world-marker screen travel is compared with the previous raw-AABB-center and asymmetric-distance camera.";
        public string limitation = "Deterministic editor geometry fixtures, including a 200-body Group layout. Synthetic animation envelopes and punch motion isolate camera response; actual animator, combat callbacks, scene loading and HUD integration are covered by BattleCameraSmoke separately. This is not a device performance benchmark.";
        public int projectedCorners;
        public int motionCases;
        public int yawCases;
        public int lifecycleCases;
        public int turningCases;
        public List<MotionCase> comparisons = new List<MotionCase>();
        public List<TurningCase> turns = new List<TurningCase>();
        public List<string> observations = new List<string>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class TurningCase
    {
        public string profile;
        public int fps;
        public float cameraYaw, circleDistance, squareDistance, circleToSquareRatio;
        public float quarterHalfZoomRange, quarterHalfScreenTravelRatio;
        public float continuousScreenTravelRatio, maximumContinuousZoomStep;
        public float realDisplacementDistanceRatio;
    }

    [Serializable] public sealed class MotionCase
    {
        public string profile, scenario;
        public int fps, bodies, measuredFrames;
        public float baselineScreenTravelPixelsPerSecond, screenTravelPixelsPerSecond, screenTravelRatio;
        public float baselineCameraVelocityVariation, cameraVelocityVariation, velocityVariationRatio;
        public float baselineZoomRange, zoomRange;
    }

    sealed class MotionMeter
    {
        Vector3 previousPosition, previousVelocity;
        Vector2 previousScreen;
        bool hasPosition, hasVelocity;
        float screenTravel, velocityVariation, seconds, minimumDistance = float.PositiveInfinity, maximumDistance;
        public int Frames;
        public float ScreenTravel => screenTravel / Mathf.Max(.001f, seconds);
        public float VelocityVariation => velocityVariation / Mathf.Max(.001f, seconds);
        public float ZoomRange => maximumDistance - minimumDistance;

        public void Sample(Camera camera, float distance, Vector3 marker, float dt)
        {
            var vp = camera.WorldToViewportPoint(marker);
            var screen = new Vector2(vp.x * 390, vp.y * 844);
            var position = camera.transform.position;
            if (hasPosition)
            {
                var velocity = (position - previousPosition) / dt;
                screenTravel += Vector2.Distance(screen, previousScreen);
                if (hasVelocity) velocityVariation += Vector3.Distance(velocity, previousVelocity);
                previousVelocity = velocity; hasVelocity = true; seconds += dt;
            }
            previousPosition = position; previousScreen = screen; hasPosition = true;
            minimumDistance = Mathf.Min(minimumDistance, distance); maximumDistance = Mathf.Max(maximumDistance, distance);
            Frames++;
        }
    }

    // Historical production path retained only as a comparison fixture: it
    // follows animated renderer AABB centers and immediately fits each peak.
    sealed class PreviousCamera
    {
        readonly BattleCameraProfile profile;
        Vector3 center, velocity;
        float distance;
        bool initialized;
        public PreviousCamera(BattleCameraProfile profile) { this.profile = profile; }
        public BattleCameraFraming.Pose Update(IReadOnlyList<Bounds> bounds, float dt, float yaw = 0)
        {
            var desired = BattleCameraFraming.CalculatePose(bounds, Aspect, profile.FieldOfView, Usable,
                profile.Pitch, yaw, profile.MinimumDistance);
            if (!initialized) { center = desired.Center; distance = desired.Distance; initialized = true; }
            else center = Vector3.SmoothDamp(center, desired.Center, ref velocity,
                profile.Pitch == BattleCameraProfiles.Duel.Pitch ? .10f : .22f, Mathf.Infinity, dt);
            var fitted = BattleCameraFraming.CalculatePoseAtCenter(bounds, Aspect, profile.FieldOfView, Usable,
                desired.Rotation, center, profile.MinimumDistance);
            distance = BattleCameraFraming.SmoothDistance(distance, fitted.Distance, dt,
                profile.Pitch == BattleCameraProfiles.Duel.Pitch ? .45f : 2f);
            return BattleCameraFraming.WithDistance(fitted, distance, Aspect, profile.FieldOfView);
        }
    }

    [MenuItem("PocketStriker/Validation/Battle Camera Stability")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_BATTLE_CAMERA_STABILITY_PASSED: " + report.comparisons.Count
            + " comparisons, " + report.projectedCorners + " projected corners.");
    }

    public static void ValidateBatch()
    {
        var report = Run();
        Debug.Log("[BattleCameraStability] " + (report.passed ? "PASS" : "FAIL") + ": "
            + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Battle camera stability validation requires a stopped editor.");
        Directory.CreateDirectory(Output);
        var report = new Report { unityVersion = Application.unityVersion };
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Battle camera stability fixture"); rig.SetActive(false);
        SceneManager.MoveGameObjectToScene(rig, scene);
        var currentObject = new GameObject("Stabilized camera"); currentObject.transform.SetParent(rig.transform);
        var previousObject = new GameObject("Previous camera"); previousObject.transform.SetParent(rig.transform);
        var camera = currentObject.AddComponent<Camera>(); var previous = previousObject.AddComponent<Camera>();
        camera.enabled = previous.enabled = false;
        camera.nearClipPlane = previous.nearClipPlane = .3f;
        camera.farClipPlane = previous.farClipPlane = 10000;
        try
        {
            var profiles = new[] { BattleCameraProfiles.Duel, BattleCameraProfiles.MultiRaid, BattleCameraProfiles.Group };
            var names = new[] { "Duel", "MultiRaid", "Group" };
            for (int index = 0; index < profiles.Length; index++)
            foreach (int fps in new[] { 30, 60, 120 })
            {
                int count = index == 0 ? 2 : index == 1 ? 6 : 200;
                CheckComparison(camera, previous, profiles[index], names[index], count, fps, false, report);
                CheckComparison(camera, previous, profiles[index], names[index], count, fps, true, report);
                CheckMovement(camera, profiles[index], names[index], count, fps, report);
                CheckYaw(profiles[index], names[index], fps, report);
                CheckLifecycle(camera, profiles[index], names[index], fps, report);
                CheckTurning(camera, previous, profiles[index], names[index], fps, report);
            }
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig); EditorSceneManager.ClosePreviewScene(scene);
            report.passed = report.errors.Count == 0 && report.comparisons.Count == 18
                && report.motionCases == 9 && report.yawCases == 9 && report.lifecycleCases == 9
                && report.turningCases == 9 && report.turns.Count == 27;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckComparison(Camera camera, Camera previous, BattleCameraProfile profile,
        string name, int count, int fps, bool punch, Report report)
    {
        var current = new BattleCameraStabilizer(profile);
        var baseline = new PreviousCamera(profile);
        var currentMeter = new MotionMeter(); var baselineMeter = new MotionMeter();
        var bounds = new List<Bounds>(count);
        float dt = 1f / fps;
        var marker = new Vector3(-1.3f, .8f, -.7f);
        for (int frame = 0; frame < fps * 14; frame++)
        {
            float t = frame * dt;
            var rootMotion = punch ? new Vector3(Mathf.Sin(t * 2 * Mathf.PI * 11) * .23f,
                Mathf.Sin(t * 2 * Mathf.PI * 9) * .08f, Mathf.Sin(t * 2 * Mathf.PI * 13) * .17f) : Vector3.zero;
            FillBounds(bounds, count, t, rootMotion, true);
            var pose = current.Update(bounds, new Vector3(0, 1.1f, 0) + rootMotion,
                Aspect, profile.FieldOfView, Usable, .3f, dt);
            var oldPose = baseline.Update(bounds, dt);
            Configure(camera, pose, profile); Configure(previous, oldPose, profile);
            CheckCorners(camera, bounds, name + "/" + (punch ? "punch" : "animation") + "/" + fps, report);
            if (t < 6) continue;
            currentMeter.Sample(camera, pose.Distance, marker, dt);
            baselineMeter.Sample(previous, oldPose.Distance, marker, dt);
        }
        var item = new MotionCase { profile = name, scenario = punch ? "root-punch" : "stationary-animation",
            fps = fps, bodies = count, measuredFrames = currentMeter.Frames,
            baselineScreenTravelPixelsPerSecond = baselineMeter.ScreenTravel, screenTravelPixelsPerSecond = currentMeter.ScreenTravel,
            screenTravelRatio = currentMeter.ScreenTravel / Mathf.Max(.000001f, baselineMeter.ScreenTravel),
            baselineCameraVelocityVariation = baselineMeter.VelocityVariation, cameraVelocityVariation = currentMeter.VelocityVariation,
            velocityVariationRatio = currentMeter.VelocityVariation / Mathf.Max(.000001f, baselineMeter.VelocityVariation),
            baselineZoomRange = baselineMeter.ZoomRange, zoomRange = currentMeter.ZoomRange };
        report.comparisons.Add(item);
        Require(baselineMeter.ScreenTravel > .05f, name + " baseline fixture does not produce measurable camera jitter.");
        Require(item.screenTravelRatio < (punch ? .45f : .25f), name + "/" + item.scenario + "/" + fps
            + " camera marker jitter was not substantially reduced: ratio=" + item.screenTravelRatio);
        Require(item.velocityVariationRatio < (punch ? .50f : .30f), name + "/" + item.scenario + "/" + fps
            + " camera velocity variation was not substantially reduced: ratio=" + item.velocityVariationRatio);
    }

    static void CheckMovement(Camera camera, BattleCameraProfile profile, string name, int count, int fps, Report report)
    {
        var current = new BattleCameraStabilizer(profile);
        var bounds = new List<Bounds>(count);
        float dt = 1f / fps;
        for (int frame = 0; frame <= fps * 8; frame++)
        {
            float t = frame * dt;
            var translation = new Vector3(Mathf.Min(t, 4) * 2, 0, Mathf.Min(t, 4) * .5f);
            FillBounds(bounds, count, t, translation, false);
            var pose = current.Update(bounds, translation + Vector3.up * 1.1f,
                Aspect, profile.FieldOfView, Usable, .3f, dt);
            Configure(camera, pose, profile); CheckCorners(camera, bounds, name + "/moving/" + fps, report);
            if (t >= 2 && t <= 4)
                Require(Vector3.Distance(pose.Center, translation + Vector3.up * 1.1f) < 1.4f,
                    name + " deliberate movement has excessive pan lag at " + fps + " fps.");
        }
        // The same 2% composition window is larger in world units when a
        // 200-fighter view pulls back. Assert its screen-relative width rather
        // than demanding a duel-size residual from the crowd camera.
        float stoppedWindow = Mathf.Max(.08f, 2 * current.CurrentPose.Distance
            * Mathf.Tan(profile.FieldOfView * .5f * Mathf.Deg2Rad) * Aspect * Usable.width * .02f);
        Require(Vector3.Distance(current.CurrentPose.Center, new Vector3(8, 1.1f, 2)) < stoppedWindow + .025f,
            name + " does not settle near deliberately moved fighters at " + fps + " fps.");
        // A single-frame large separation and jump must override smoothing to
        // preserve every body corner, including the lagging camera center.
        bounds.Clear();
        bounds.Add(new Bounds(new Vector3(-28, 1.1f, -8), new Vector3(1, 2.2f, 1)));
        bounds.Add(new Bounds(new Vector3(36, 9, 12), new Vector3(1.6f, 4, 1.6f)));
        var separated = current.Update(bounds, new Vector3(4, 5.05f, 2), Aspect, profile.FieldOfView, Usable, .3f, dt);
        Configure(camera, separated, profile); CheckCorners(camera, bounds, name + "/one-frame-separation/" + fps, report);
        report.motionCases++;
    }

    static void CheckYaw(BattleCameraProfile profile, string name, int fps, Report report)
    {
        var current = new BattleCameraStabilizer(profile);
        current.Reset(179);
        float dt = 1f / fps;
        for (int frame = 0; frame < fps * 5; frame++)
            current.UpdateYaw(179 + Mathf.Sin(frame * dt * 2 * Mathf.PI * 7) * 6, dt);
        Require(Mathf.Abs(Mathf.DeltaAngle(179, current.Yaw)) < .1f,
            name + " auto orbit follows combat-line twitch within the angular dead zone at " + fps + " fps.");
        for (int frame = 0; frame < fps * 4; frame++) current.UpdateYaw(-121, dt);
        Require(Mathf.Abs(Mathf.DeltaAngle(current.Yaw, -121)) < 10,
            name + " auto orbit does not respond to a deliberate 60-degree turn at " + fps + " fps.");
        float before = current.Yaw;
        current.UpdateYaw(before + 1, dt, true);
        Require(Mathf.Abs(Mathf.DeltaAngle(before, current.Yaw)) > .001f,
            name + " manual orbit is blocked by the automatic angular dead zone.");
        float paused = current.Yaw;
        current.UpdateYaw(paused + 90, 0);
        Require(Mathf.Abs(Mathf.DeltaAngle(paused, current.Yaw)) < .0001f,
            name + " automatic yaw moved during a paused frame.");
        report.yawCases++;
    }

    static void CheckLifecycle(Camera camera, BattleCameraProfile profile, string name, int fps, Report report)
    {
        var current = new BattleCameraStabilizer(profile);
        var bounds = new List<Bounds>(); FillBounds(bounds, 2, 0, Vector3.zero, false);
        float dt = 1f / fps;
        var initial = current.Update(bounds, Vector3.up * 1.1f, Aspect, profile.FieldOfView, Usable, .3f, dt);
        Require(current.IsInitialized, name + " first fielded frame did not initialize.");
        Configure(camera, initial, profile); CheckCorners(camera, bounds, name + "/first-frame/" + fps, report);
        bounds.RemoveAt(0);
        for (int frame = 0; frame < fps; frame++)
        {
            var held = current.Update(bounds, bounds[0].center, Aspect, profile.FieldOfView, Usable, .3f, dt, true);
            Require(Vector3.Distance(held.Center, initial.Center) < .0001f && held.Distance >= initial.Distance - .0001f,
                name + " replacement hold panned inward or shrank.");
            Configure(camera, held, profile); CheckCorners(camera, bounds, name + "/hold/" + fps, report);
        }
        var before = current.CurrentPose;
        var paused = current.Update(bounds, bounds[0].center + Vector3.right, Aspect, profile.FieldOfView, Usable, .3f, 0);
        Require(Vector3.Distance(before.Center, paused.Center) < .0001f && Mathf.Abs(before.Distance - paused.Distance) < .0001f,
            name + " paused camera drifted without a new safety requirement.");
        // Time scale can be zero while layout or model visibility changes.
        // Keep composition still but preserve the immediate raw-bounds floor.
        float pausedYaw = current.Yaw;
        bounds.Clear();
        bounds.Add(new Bounds(new Vector3(-32, 1.1f, -12), new Vector3(1, 2.2f, 1)));
        bounds.Add(new Bounds(new Vector3(34, 10, 15), new Vector3(1.6f, 4, 1.6f)));
        var pausedExpansion = current.Update(bounds, new Vector3(1, 5.55f, 1.5f),
            Aspect, profile.FieldOfView, Usable, .3f, 0);
        Require(Vector3.Distance(pausedExpansion.Center, paused.Center) < .0001f
            && Mathf.Abs(Mathf.DeltaAngle(pausedYaw, current.Yaw)) < .0001f
            && pausedExpansion.Distance >= paused.Distance,
            name + " pause-time safety expansion changed composition or shrank the camera.");
        Configure(camera, pausedExpansion, profile);
        CheckCorners(camera, bounds, name + "/paused-safety-expansion/" + fps, report);
        current.Reset(-37);
        Require(!current.IsInitialized && Mathf.Abs(Mathf.DeltaAngle(current.Yaw, -37)) < .0001f,
            name + " reset did not clear camera state and preserve its assigned yaw.");
        FillBounds(bounds, 2, 0, new Vector3(200, 0, -170), false);
        var retried = current.Update(bounds, new Vector3(200, 1.1f, -170), Aspect, profile.FieldOfView, Usable, .3f, dt);
        Require(Vector3.Distance(retried.Center, new Vector3(200, 1.1f, -170)) < .0001f,
            name + " reset retained previous battle pan lag.");
        Configure(camera, retried, profile); CheckCorners(camera, bounds, name + "/reset/" + fps, report);
        report.lifecycleCases++;
    }

    static void CheckTurning(Camera camera, Camera previous, BattleCameraProfile profile,
        string name, int fps, Report report)
    {
        float dt = 1f / fps;
        var tracking = Vector3.up * 1.1f;
        var marker = new Vector3(-1.3f, .8f, -.7f);
        foreach (float cameraYaw in new[] { 0f, 45f, 90f })
        {
            var bounds = new List<Bounds>(2);
            var envelopes = new List<BattleCameraFraming.BodyEnvelope>(2);
            FillTurningBodies(bounds, envelopes, 0);
            var current = new BattleCameraStabilizer(profile); current.Reset(cameraYaw);
            var baseline = new PreviousCamera(profile);
            var initial = current.Update(bounds, tracking, Aspect, profile.FieldOfView, Usable, .3f, 0, false, envelopes);
            var circleFit = BattleCameraFraming.CalculatePoseAtCenter(null, Aspect, profile.FieldOfView, Usable,
                initial.Rotation, tracking, profile.MinimumDistance, .3f, envelopes);
            Configure(camera, circleFit, profile);
            CheckCylinderSurface(camera, envelopes, name + "/circle-surface/" + fps + "/yaw-" + cameraYaw, report);
            var squares = new List<Bounds>(2);
            foreach (var envelope in envelopes)
                squares.Add(new Bounds(envelope.Center,
                    new Vector3(envelope.Radius * 2 + .25f, envelope.Height, envelope.Radius * 2 + .25f)));
            var squarePose = BattleCameraFraming.CalculatePoseAtCenter(squares, Aspect, profile.FieldOfView,
                Usable, initial.Rotation, tracking, profile.MinimumDistance, .3f);
            float squareDistance = squarePose.Distance * (1 + profile.FramingReserve);
            var item = new TurningCase { profile = name, fps = fps, cameraYaw = cameraYaw,
                circleDistance = initial.Distance, squareDistance = squareDistance,
                circleToSquareRatio = initial.Distance / squareDistance };
            report.turns.Add(item);
            Require(item.circleToSquareRatio < .96f,
                name + "/turn/" + fps + "/yaw-" + cameraYaw + " circle did not remove square-envelope excess distance.");
            if (cameraYaw == 45)
                Require(item.circleToSquareRatio < .90f,
                    name + " oblique circle view is not meaningfully closer than the previous square envelope.");

            var meter = new MotionMeter(); var oldMeter = new MotionMeter();
            float minimum = initial.Distance, maximum = initial.Distance;
            // Quarter/half turns change the same body's world AABB, with no
            // root movement or deliberate camera orbit. The cylinder goal and
            // the established composition should remain absolutely stable.
            for (int frame = 0; frame < fps * 10; frame++)
            {
                float t = frame * dt;
                float bodyYaw = ((int)(t / .4f) % 4) * 90;
                FillTurningBodies(bounds, envelopes, bodyYaw);
                var pose = current.Update(bounds, tracking, Aspect, profile.FieldOfView, Usable, .3f, dt, false, envelopes);
                var oldPose = baseline.Update(bounds, dt, cameraYaw);
                Configure(camera, pose, profile); Configure(previous, oldPose, profile);
                CheckCorners(camera, bounds, name + "/quarter-half-turn/" + fps + "/yaw-" + cameraYaw, report);
                minimum = Mathf.Min(minimum, pose.Distance); maximum = Mathf.Max(maximum, pose.Distance);
                Require(Vector3.Distance(pose.Center, tracking) < .0001f,
                    name + " fixed-root body turn moved the composition center.");
                if (t < 3) continue;
                meter.Sample(camera, pose.Distance, marker, dt); oldMeter.Sample(previous, oldPose.Distance, marker, dt);
            }
            item.quarterHalfZoomRange = maximum - minimum;
            item.quarterHalfScreenTravelRatio = meter.ScreenTravel / Mathf.Max(.000001f, oldMeter.ScreenTravel);
            Require(item.quarterHalfZoomRange < .001f,
                name + "/quarter-half-turn/" + fps + "/yaw-" + cameraYaw + " neutral silhouette turn still pumps zoom.");
            Require(item.quarterHalfScreenTravelRatio < .15f,
                name + " quarter/half-turn screen motion was not substantially reduced.");

            current.Reset(cameraYaw); baseline = new PreviousCamera(profile);
            meter = new MotionMeter(); oldMeter = new MotionMeter();
            float priorDistance = 0;
            // Intermediate world-AABB corners can exceed the true circular
            // silhouette. Retain that raw safety floor while measuring whether
            // its turn-induced motion is substantially smaller than before.
            for (int frame = 0; frame < fps * 14; frame++)
            {
                float t = frame * dt;
                FillTurningBodies(bounds, envelopes, t * 240);
                var pose = current.Update(bounds, tracking, Aspect, profile.FieldOfView, Usable, .3f, dt, false, envelopes);
                var oldPose = baseline.Update(bounds, dt, cameraYaw);
                Configure(camera, pose, profile); Configure(previous, oldPose, profile);
                CheckCorners(camera, bounds, name + "/continuous-turn/" + fps + "/yaw-" + cameraYaw, report);
                if (t >= 6)
                {
                    meter.Sample(camera, pose.Distance, marker, dt); oldMeter.Sample(previous, oldPose.Distance, marker, dt);
                    item.maximumContinuousZoomStep = Mathf.Max(item.maximumContinuousZoomStep,
                        Mathf.Abs(pose.Distance - priorDistance));
                }
                priorDistance = pose.Distance;
            }
            item.continuousScreenTravelRatio = meter.ScreenTravel / Mathf.Max(.000001f, oldMeter.ScreenTravel);
            Require(item.continuousScreenTravelRatio < .35f,
                name + "/continuous-turn/" + fps + "/yaw-" + cameraYaw + " did not substantially reduce screen motion.");

            // The same neutral envelope must never disguise a genuine dash or
            // knockback. One body travels far and rises, so the camera needs an
            // immediate expansion even with its center still following slowly.
            float beforeDisplacement = current.CurrentPose.Distance;
            FillTurningBodies(bounds, envelopes, 90, new Vector3(32, 6, -24));
            var movedTracking = new Vector3(16, 4.1f, -12);
            var displaced = current.Update(bounds, movedTracking, Aspect, profile.FieldOfView,
                Usable, .3f, dt, false, envelopes);
            Configure(camera, displaced, profile);
            CheckCorners(camera, bounds, name + "/turn-then-real-displacement/" + fps + "/yaw-" + cameraYaw, report);
            item.realDisplacementDistanceRatio = displaced.Distance / beforeDisplacement;
            Require(item.realDisplacementDistanceRatio > 1.25f,
                name + " rotation stabilization suppressed a genuine large displacement.");
        }
        report.turningCases++;
    }

    static void FillTurningBodies(List<Bounds> bounds, List<BattleCameraFraming.BodyEnvelope> envelopes,
        float bodyYaw, Vector3 secondDisplacement = default)
    {
        bounds.Clear(); envelopes.Clear();
        var rotation = Quaternion.Euler(0, bodyYaw, 0);
        var extents = new Vector3(.35f, 1.1f, 1.45f);
        float radius = new Vector2(extents.x, extents.z).magnitude + .15f;
        for (int index = 0; index < 2; index++)
        {
            var center = new Vector3(index == 0 ? -1.5f : 1.5f, 1.1f, 0)
                + (index == 1 ? secondDisplacement : Vector3.zero);
            var box = new Bounds(center + rotation * -extents, Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                box.Encapsulate(center + rotation * Vector3.Scale(extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
            box.Expand(new Vector3(.25f, .5f, .25f));
            bounds.Add(box);
            envelopes.Add(new BattleCameraFraming.BodyEnvelope(center, radius, box.size.y));
        }
    }

    static void CheckCylinderSurface(Camera camera, IReadOnlyList<BattleCameraFraming.BodyEnvelope> envelopes,
        string label, Report report)
    {
        foreach (var envelope in envelopes)
        for (int segment = 0; segment < 64; segment++)
        for (int vertical = 0; vertical < 2; vertical++)
        {
            float angle = segment * 2 * Mathf.PI / 64;
            var point = envelope.Center + new Vector3(Mathf.Cos(angle) * envelope.Radius,
                envelope.Height * (vertical == 0 ? -.5f : .5f), Mathf.Sin(angle) * envelope.Radius);
            var vp = camera.WorldToViewportPoint(point);
            const float tolerance = .0002f;
            Require(Finite(vp) && vp.z > camera.nearClipPlane && vp.x >= Usable.xMin - tolerance
                && vp.x <= Usable.xMax + tolerance && vp.y >= Usable.yMin - tolerance && vp.y <= Usable.yMax + tolerance,
                label + " cylinder surface escaped the exact fitted viewport: " + vp);
            report.projectedCorners++;
        }
    }

    static void FillBounds(List<Bounds> output, int count, float time, Vector3 translation, bool animated)
    {
        output.Clear();
        var wave = animated ? new Vector3(Mathf.Sin(time * 2 * Mathf.PI * 4) * .32f,
            Mathf.Sin(time * 2 * Mathf.PI * 5) * .25f, Mathf.Sin(time * 2 * Mathf.PI * 3) * .2f) : Vector3.zero;
        for (int index = 0; index < count; index++)
        {
            float radius = count == 2 ? 2 : count == 6 ? 5 : 14;
            float angle = index * 2 * Mathf.PI / count;
            var root = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            float phase = time * 2 * Mathf.PI * 6 + index * .73f;
            var size = animated ? new Vector3(1 + .24f * Mathf.Sin(phase), 2.2f + .34f * Mathf.Cos(phase),
                1 + .2f * Mathf.Sin(phase + .6f)) : new Vector3(1, 2.2f, 1);
            output.Add(new Bounds(root + translation + Vector3.up * 1.1f + wave, size));
        }
    }

    static void Configure(Camera camera, BattleCameraFraming.Pose pose, BattleCameraProfile profile)
    {
        Require(Finite(pose.Position) && Finite(pose.Center) && Finite(pose.Distance) && pose.Distance > 0,
            "Stabilized camera produced a nonfinite pose.");
        camera.aspect = Aspect; camera.fieldOfView = profile.FieldOfView;
        camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
    }

    static void CheckCorners(Camera camera, IReadOnlyList<Bounds> bounds, string label, Report report)
    {
        for (int index = 0; index < bounds.Count; index++)
        for (int corner = 0; corner < 8; corner++)
        {
            var point = bounds[index].center + Vector3.Scale(bounds[index].extents,
                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            var vp = camera.WorldToViewportPoint(point);
            const float tolerance = .0002f;
            Require(Finite(vp) && vp.z > camera.nearClipPlane && vp.x >= Usable.xMin - tolerance
                && vp.x <= Usable.xMax + tolerance && vp.y >= Usable.yMin - tolerance && vp.y <= Usable.yMax + tolerance,
                label + " raw corner clipped: " + vp + " at " + point);
            report.projectedCorners++;
        }
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
