using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Stopped-editor regression checks for battle framing and skill rendering.</summary>
public static class PocketStrikerCombatVisualValidation
{
    [Serializable] public sealed class Report
    {
        public bool passed;
        public string unityVersion, utcTime;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Combat Visuals")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before combat visual validation.");
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        Check("battle-framing", PocketStrikerBattleCameraValidation.Validate, report);
        Check("countdown-opening", PocketStrikerBattleCameraOpeningValidation.Validate, report);
        Check("duel-horizontal-and-stability", PocketStrikerBattleCameraStabilityValidation.Validate, report);
        Check("skill-effect-resources-and-depth", PocketStrikerEffectResourceValidation.Validate, report);
        Check("preview-composition-and-loading", PocketStrikerCameraLoadingValidation.Validate, report);
        report.passed = report.errors.Count == 0 && report.checks.Count == 5;
        Directory.CreateDirectory("Logs/CombatVisuals");
        File.WriteAllText("Logs/CombatVisuals/report.json", JsonUtility.ToJson(report, true));
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("[CombatVisuals] PASS: all five validation suites.");
    }

    static void Check(string name, Action validate, Report report)
    {
        try { validate(); report.checks.Add(name); }
        catch (Exception error) { report.errors.Add(name + ": " + error); }
    }
}
