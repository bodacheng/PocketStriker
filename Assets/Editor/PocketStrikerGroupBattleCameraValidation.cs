using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Projects the full battlefield through the independent Group camera and exercises its pointer ownership.</summary>
public static class PocketStrikerGroupBattleCameraValidation
{
    const string Output = "Logs/CameraFraming/Group";

    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public int arenaCases, projectedPoints, gestureCases, uiCases;
        public float defaultPhoneRingWidth, expandedPhoneRingWidth, defaultPhoneDistanceReduction;
        public string scope = "Production GroupBattleCamera.CalculateArenaPose and BattleArenaOrbitGesture. Complete ground circle inside safe-area side margins, standing bodies inside screen sides, and five-unit effects inside HUD-free vertical margins. Phone/tablet safe-area and HUD rectangles, small/default/expanded battle radius, translated arena, arbitrary yaw and near-plane variation. Orbit distance and center remain fixed for a given battlefield. Phone framing is compared with the previous whole-cylinder fit. Pointer ownership checks include UI starts, entering UI, second fingers, release, cancellation, reset and screen-width-independent horizontal drag.";
        public string limitation = "Stopped-editor geometric, pointer-state and UI component ancestry validation. Live scene lifecycle, moving models, focus and production death are checked in Battle Camera Playmode Smoke. No physical device touch or performance certification.";
        public List<string> errors = new List<string>();
    }

    readonly struct Device
    {
        public readonly string Name;
        public readonly float Aspect;
        public readonly Rect Usable;
        public Device(string name, float aspect, Rect usable) { Name = name; Aspect = aspect; Usable = usable; }
    }

    [MenuItem("PocketStriker/Validation/Group Battle Camera")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_GROUP_BATTLE_CAMERA_PASSED: " + report.arenaCases + " arena poses, "
            + report.projectedPoints + " projected points, " + report.gestureCases + " gesture checks.");
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Group camera validation requires a stopped editor.");
        Directory.CreateDirectory(Output);
        var report = new Report { unityVersion = Application.unityVersion };
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Group arena camera fixture"); rig.SetActive(false);
        SceneManager.MoveGameObjectToScene(rig, scene);
        var camera = rig.AddComponent<Camera>(); camera.enabled = false; camera.farClipPlane = 10000;
        try
        {
            CheckArenaProjection(camera, report);
            CheckGestures(report);
            CheckUIIsolation(rig.transform, report);
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig); EditorSceneManager.ClosePreviewScene(scene);
            report.passed = report.errors.Count == 0 && report.arenaCases == 720 && report.projectedPoints >= 300000
                && report.gestureCases >= 20 && report.uiCases >= 7;
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckArenaProjection(Camera camera, Report report)
    {
        var devices = new[]
        {
            new Device("phone", 9f / 16, new Rect(.006f, .20f, .988f, .72f)),
            new Device("compact-phone", 375f / 667, new Rect(.006f, .20f, .988f, .72f)),
            new Device("notched-phone", 390f / 844, new Rect(.006f, .225f, .988f, .655f)),
            new Device("tablet", 3f / 4, new Rect(.006f, .16f, .988f, .78f)),
            new Device("asymmetric-safe-area", 834f / 1194, new Rect(.056f, .14f, .908f, .79f))
        };
        var headings = new[] { 0f, 17f, 45f, 89.9f, 90f, 133f, 179.5f, 225f, 270f, 315f, 359.9f, 720.5f };
        foreach (var device in devices)
        foreach (float radius in new[] { 8f, 20f, 70f })
        foreach (var center in new[] { Vector3.zero, new Vector3(980, -15, -720) })
        foreach (float nearClip in new[] { .3f, 2f })
        {
            BattleCameraFraming.Pose? first = null;
            foreach (float yaw in headings)
            {
                string label = device.Name + "/radius-" + radius + "/center-" + center + "/yaw-" + yaw + "/near-" + nearClip;
                var pose = GroupBattleCamera.CalculateArenaPose(center, radius, device.Aspect, device.Usable, yaw, nearClip);
                Require(Finite(pose.Position) && Finite(pose.Distance) && pose.Distance > nearClip,
                    label + " produced an invalid arena camera pose.");
                Require(Vector3.Distance(pose.Center, center) < .001f, label + " moved the battlefield pivot.");
                Require(Mathf.Abs(Mathf.DeltaAngle(pose.Rotation.eulerAngles.x, BattleCameraProfiles.Group.Pitch)) < .001f
                    && Mathf.Abs(Mathf.DeltaAngle(pose.Rotation.eulerAngles.y, yaw)) < .001f,
                    label + " changed the fixed pitch or requested heading.");
                if (first.HasValue)
                {
                    var expectedPosition = center + Quaternion.AngleAxis(yaw, Vector3.up) * (first.Value.Position - center);
                    Require(Vector3.Distance(pose.Position, expectedPosition) < .005f
                        && Mathf.Abs(pose.Distance - first.Value.Distance) < .005f,
                        label + " changed distance or translated the pivot while orbiting.");
                }
                else first = pose;
                camera.aspect = device.Aspect; camera.fieldOfView = BattleCameraProfiles.Group.FieldOfView;
                camera.nearClipPlane = nearClip;
                camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                // Sample the circle independently of the fit solver. Ground
                // determines tight side margins; standing bodies keep a screen
                // margin and tall effects keep the HUD-free vertical envelope.
                for (int angle = 0; angle < 144; angle++)
                foreach (float height in new[] { 0f, 2.5f, 3.4f, GroupBattleCamera.StandingHeightReserve })
                {
                    float radians = angle * Mathf.PI * 2 / 144;
                    var point = center + new Vector3(Mathf.Cos(radians) * radius, height, Mathf.Sin(radians) * radius);
                    var viewport = camera.WorldToViewportPoint(point);
                    Require(Finite(viewport) && viewport.z > camera.nearClipPlane,
                        label + " arena point crossed the near plane.");
                    Require(viewport.y >= device.Usable.yMin - .0002f && viewport.y <= device.Usable.yMax + .0002f,
                        label + " arena point escaped the HUD-free vertical viewport: " + viewport + " at " + point);
                    if (height == 0)
                        Require(viewport.x >= device.Usable.xMin - .0002f && viewport.x <= device.Usable.xMax + .0002f,
                            label + " ground circle escaped the safe-area sides: " + viewport);
                    else if (height <= GroupBattleCamera.StandingHeightReserve)
                        Require(viewport.x >= -.0002f && viewport.x <= 1.0002f,
                            label + " standing body escaped the screen sides: " + viewport);
                    report.projectedPoints++;
                }
                report.arenaCases++;
            }
        }
        var small = GroupBattleCamera.CalculateArenaPose(Vector3.zero, 20, 9f / 16, devices[0].Usable);
        var large = GroupBattleCamera.CalculateArenaPose(Vector3.zero, 70, 9f / 16, devices[0].Usable);
        Require(large.Distance > small.Distance * 2, "Expanded battle boundary does not expand the arena shot.");
        float RingWidth(BattleCameraFraming.Pose pose, float radius)
        {
            camera.aspect = devices[0].Aspect; camera.fieldOfView = BattleCameraProfiles.Group.FieldOfView;
            camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            float min = 1, max = 0;
            for (int angle = 0; angle < 1440; angle++)
            {
                float radians = angle * Mathf.PI * 2 / 1440;
                float x = camera.WorldToViewportPoint(new Vector3(Mathf.Cos(radians) * radius, 0, Mathf.Sin(radians) * radius)).x;
                min = Mathf.Min(min, x); max = Mathf.Max(max, x);
            }
            return max - min;
        }
        report.defaultPhoneRingWidth = RingWidth(small, 20);
        report.expandedPhoneRingWidth = RingWidth(large, 70);
        var priorEnvelope = new[] { new BattleCameraFraming.BodyEnvelope(new Vector3(0, 2.5f, 0), 21, 5) };
        var prior = BattleCameraFraming.CalculatePoseAtCenter(null, devices[0].Aspect,
            BattleCameraProfiles.Group.FieldOfView, new Rect(.02f, .20f, .96f, .72f), small.Rotation,
            Vector3.zero, BattleCameraProfiles.Group.MinimumDistance, .3f, priorEnvelope);
        report.defaultPhoneDistanceReduction = 1 - small.Distance / prior.Distance;
        Require(report.defaultPhoneRingWidth > .94f && report.expandedPhoneRingWidth > .97f,
            "The phone ground circle leaves excessive space at the screen sides.");
        Require(report.defaultPhoneDistanceReduction > .04f,
            "The group arena shot did not move noticeably closer than the previous cylinder framing.");
    }

    static void CheckGestures(Report report)
    {
        var gesture = new BattleArenaOrbitGesture();
        void Check(bool condition, string message) { Require(condition, message); report.gestureCases++; }
        float Move(int id, TouchPhase phase, float x, float y = 400, bool ui = false, float width = 360)
            => gesture.UpdatePointer(id, phase, new Vector2(x, y), ui, width);
        Check(Move(7, TouchPhase.Began, 90, ui: true) == 0 && !gesture.ActiveFingerId.HasValue,
            "A HUD touch acquired the arena orbit.");
        Check(Move(7, TouchPhase.Moved, 180) == 0 && !gesture.ActiveFingerId.HasValue,
            "A touch that started on UI began rotating after leaving UI.");
        Check(Move(7, TouchPhase.Began, 90) == 0 && gesture.ActiveFingerId == 7,
            "A fresh battlefield touch did not acquire the orbit.");
        Check(Mathf.Abs(Move(7, TouchPhase.Moved, 180) - 45) < .001f,
            "Horizontal battlefield drag did not rotate by its normalized screen distance.");
        Check(Move(7, TouchPhase.Moved, 180, 500) == 0 && gesture.ActiveFingerId == 7,
            "Vertical drag changed the orbit.");
        Check(Move(8, TouchPhase.Began, 270) == 0 && gesture.ActiveFingerId == 7,
            "A second finger stole the active orbit.");
        Check(Move(8, TouchPhase.Moved, 340) == 0 && gesture.ActiveFingerId == 7,
            "The second finger changed orbit yaw.");
        Check(Move(8, TouchPhase.Ended, 340) == 0 && gesture.ActiveFingerId == 7,
            "Releasing another finger cancelled the active orbit.");
        Check(Move(7, TouchPhase.Stationary, 180, 500) == 0, "A stationary touch rotated the arena.");
        Check(Mathf.Abs(Move(7, TouchPhase.Moved, 90, 500) + 45) < .001f,
            "Dragging left did not reverse the orbit.");
        Check(Move(7, TouchPhase.Ended, 90) == 0 && !gesture.ActiveFingerId.HasValue,
            "Releasing the orbit finger kept a stale gesture.");
        Check(Move(8, TouchPhase.Moved, 270) == 0 && !gesture.ActiveFingerId.HasValue,
            "Orbit ownership transferred to an already pressed second finger.");
        Move(9, TouchPhase.Began, 90);
        Check(Move(9, TouchPhase.Moved, 180, ui: true) == 0 && !gesture.ActiveFingerId.HasValue,
            "Dragging into the HUD continued to rotate the arena.");
        Check(Move(9, TouchPhase.Moved, 270) == 0, "Leaving UI resumed a cancelled gesture.");
        Move(10, TouchPhase.Began, 90);
        Check(Move(10, TouchPhase.Canceled, 180) == 0 && !gesture.ActiveFingerId.HasValue,
            "Touch cancellation left stale orbit ownership.");
        Check(Move(10, TouchPhase.Moved, 270) == 0, "Cancelled finger resumed without a new touch.");
        Move(11, TouchPhase.Began, 90); gesture.Reset();
        Check(!gesture.ActiveFingerId.HasValue && Move(11, TouchPhase.Moved, 180) == 0,
            "Camera reset retained a held touch.");
        foreach (float width in new[] { 360f, 540f, 834f, 1440f })
        {
            gesture.Reset(); Move(12, TouchPhase.Began, width * .25f, width: width);
            Check(Mathf.Abs(Move(12, TouchPhase.Moved, width * .5f, width: width) - 45) < .001f,
                "Touch orbit sensitivity changes with screen width " + width + ".");
        }
        gesture.Reset();
    }

    static void CheckUIIsolation(Transform root, Report report)
    {
        var uiRoot = new GameObject("Group UI isolation fixture");
        SceneManager.MoveGameObjectToScene(uiRoot, root.gameObject.scene);
        try
        {
            GameObject Child(string name, Transform parent)
            {
                var child = new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(parent, false);
                return child;
            }
            void Check(GameObject hit, bool blocked, string label)
            {
                Require(GroupBattleCamera.IsOrbitBlockingUI(hit) == blocked, label + " has incorrect orbit/UI isolation.");
                report.uiCases++;
            }
            // Active UI ancestors match real raycast hits. This synchronous stopped-
            // editor fixture is destroyed before any editor Update; normal HUD
            // gameplay components never enter their Play-mode lifecycle.
            var hud = Child("Passive battle HUD", uiRoot.transform); hud.AddComponent<FightingStepLayer>();
            var passive = Child("HUD background", hud.transform); passive.AddComponent<Image>();
            Check(passive, false, "Passive battle graphic");
            var button = Child("Pause button", hud.transform); button.AddComponent<Button>();
            Check(Child("Pause icon", button.transform), true, "Selectable child");
            var pointer = Child("Pointer interaction", hud.transform); pointer.AddComponent<EventTrigger>();
            var pointerGraphic = Child("Pointer graphic", pointer.transform);
            Check(pointerGraphic, true, "Pointer handler child");
            var orbit = Child("Arena touch pad", hud.transform); orbit.AddComponent<UltimateJoystick>().joystickName = "RotateCamera";
            Check(Child("Transparent orbit graphic", orbit.transform), false, "Existing full-screen orbit pad");
            var move = Child("Team movement joystick", hud.transform); move.AddComponent<UltimateJoystick>().joystickName = "TeamMove";
            Check(Child("Movement joystick graphic", move.transform), true, "Movement joystick");
            var modal = Child("Modal UI layer", uiRoot.transform); modal.AddComponent<UILayer>();
            var backdrop = Child("Modal backdrop", modal.transform); backdrop.AddComponent<Image>();
            Check(backdrop, true, "Other UI layer backdrop");
            Check(null, false, "Empty raycast");
        }
        finally { UnityEngine.Object.DestroyImmediate(uiRoot); }
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
