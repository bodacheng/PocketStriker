using System.Collections.Generic;
using UnityEngine;

/// <summary>Places the player's team below and right of its opponent in the fitted portrait view.</summary>
public static class BattleCameraOpening
{
    public static float CalculatePlanarYaw(Vector3 playerAnchor, Vector3 opponentAnchor,
        BattleCameraProfile profile, float aspect, Rect usable)
    {
        var line = opponentAnchor - playerAnchor; line.y = 0;
        if (line.sqrMagnitude < .0001f) return 0;
        float pitch = profile.Pitch * Mathf.Deg2Rad;
        float tan = Mathf.Tan(profile.FieldOfView * .5f * Mathf.Deg2Rad);
        float opticalOffset = ((usable.center.x * 2 - 1) * tan * aspect
            + (usable.center.y * 2 - 1) * tan) * Mathf.Cos(pitch);
        return Mathf.Atan2(line.x, line.z) * Mathf.Rad2Deg
            + Mathf.Atan(Mathf.Sin(pitch) - opticalOffset) * Mathf.Rad2Deg;
    }

    // A screen diagonal depends on camera elevation as well as its azimuth.
    // Solve against the same full-model fit used by the camera, including the
    // HUD's displaced optical center, rather than assuming a 45-degree yaw.
    public static float CalculateYaw(Vector3 playerAnchor, Vector3 opponentAnchor,
        BattleCameraProfile profile, IReadOnlyList<Bounds> bounds,
        IReadOnlyList<BattleCameraFraming.BodyEnvelope> bodyEnvelopes,
        Vector3 trackingCenter, float aspect, Rect usable, float nearClip = .3f)
    {
        var line = opponentAnchor - playerAnchor;
        line.y = 0;
        if (line.sqrMagnitude < .0001f) return 0;
        float heading = Mathf.Atan2(line.x, line.z) * Mathf.Rad2Deg;
        float lower = .01f, upper = 89.99f;
        for (int iteration = 0; iteration < 12; iteration++)
        {
            float angle = (lower + upper) * .5f;
            var rotation = Quaternion.Euler(profile.Pitch, heading + angle, 0);
            var pose = BattleCameraFraming.CalculatePoseAtCenter(bounds, aspect, profile.FieldOfView,
                usable, rotation, trackingCenter, profile.MinimumDistance, nearClip, bodyEnvelopes);
            pose = BattleCameraFraming.WithDistance(pose, pose.Distance * (1 + profile.FramingReserve),
                aspect, profile.FieldOfView);
            var inverse = Quaternion.Inverse(rotation);
            var player = inverse * (playerAnchor - pose.Position);
            var opponent = inverse * (opponentAnchor - pose.Position);
            // Perspective X/Z and Y/Z have the same pixel scale; normalized
            // viewport X alone would incorrectly depend on the phone's aspect.
            var delta = new Vector2(opponent.x / Mathf.Max(.01f, opponent.z)
                - player.x / Mathf.Max(.01f, player.z),
                opponent.y / Mathf.Max(.01f, opponent.z) - player.y / Mathf.Max(.01f, player.z));
            float screenAngle = Mathf.Atan2(delta.y, -delta.x) * Mathf.Rad2Deg;
            if (screenAngle < 45) upper = angle;
            else lower = angle;
        }
        return heading + (lower + upper) * .5f;
    }
}
