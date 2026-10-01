using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Final offline checks; live-account and battle reports are separate.</summary>
public static class PocketStrikerRound2Validation
{
    [Serializable] public sealed class Report
    {
        public bool passed;
        public string utcTime;
        public List<string> passedChecks = new List<string>();
        public List<string> errors = new List<string>();
    }
    public static void ValidateBatch()
    {
        var report = new Report { utcTime = DateTime.UtcNow.ToString("O") };
        void Check(string name, Action action)
        {
            try { action(); report.passedChecks.Add(name); Debug.Log("[Round2] PASS: " + name); }
            catch (Exception error) { report.errors.Add(name + ": " + error); Debug.LogWarning("[Round2] FAIL: " + name + ": " + error.Message); }
        }
        Check("camera geometry and centered duel viewports", PocketStrikerBattleCameraValidation.Validate);
        Check("camera motion, pause and handoff at 30/60/120 fps", PocketStrikerBattleCameraStabilityValidation.Validate);
        // Global UI uses the existing UIArtValidation isolated-scene snapshot
        // (separate report), which preserves the legacy dirty-flag finding and
        // independently checks all object/component serialization fingerprints.
        Check("stage cards", () =>
        {
            var cards = PocketStrikerStageCardValidation.ValidateCards();
            if (!cards.passed) throw new InvalidOperationException(string.Join("\n", cards.errors));
        });
        Check("project scenes, prefabs and Addressables", PocketStrikerValidation.CheckProject);
        Check("iOS player script compilation", PocketStrikerValidation.CompilePlayer);
        report.passed = report.errors.Count == 0 && report.passedChecks.Count == 5;
        Directory.CreateDirectory("Logs/UIQuality/Round2Final");
        File.WriteAllText("Logs/UIQuality/Round2Final/report.json", JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
