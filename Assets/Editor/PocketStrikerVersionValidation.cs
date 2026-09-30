using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cocone.ProjectP3;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;

/// <summary>Checks version templates without building, saving assets, or publishing content.</summary>
public static class PocketStrikerVersionValidation
{
    const string ReportPath = "Logs/Revival/version-sync-report.json";
    static readonly string[] SourcePaths =
    {
        "ProjectSettings/ProjectSettings.asset",
        "Assets/ExternalAssets/Config/app_version.json",
        "Assets/App/Editor/Build/Configs/AddressablesProfileSettings.yaml",
        "Assets/AddressableAssetsData/AddressableAssetSettings.asset"
    };
    static readonly string[] Profiles = { "dev", "release" };
    static readonly string[] Variables = { "Remote.BuildPath", "Remote.LoadPath" };

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public bool sourceFilesUnchanged;
        public bool projectVersionRestored;
        public bool templatesUnchanged;
        public string unityVersion;
        public string originalVersion;
        public string[] testedVersions;
        public List<PathProbe> paths = new List<PathProbe>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public sealed class PathProbe
    {
        public string profile;
        public string variable;
        public string rawTemplate;
        public string firstResolvedPath;
        public string secondResolvedPath;
    }

    [MenuItem("PocketStriker/Validation/Version Sync")]
    public static void Validate()
    {
        var originalVersion = PlayerSettings.bundleVersion;
        var report = new Report
        {
            originalVersion = originalVersion,
            unityVersion = Application.unityVersion,
            testedVersions = new[] { "98.7.6", "98.7.7" }
        };
        var snapshots = new Dictionary<string, byte[]>();
        AddressableAssetSettings settings = null;
        var originalSettingsJson = string.Empty;
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode before validating versions.");
            foreach (var path in SourcePaths) snapshots.Add(path, File.ReadAllBytes(path));
            VersionSyncUtility.AssertVersionSettingsSynchronized();
            settings = BuildAddressableAssets.GetSettings();
            Require(settings != null, "Addressables settings are missing.");
            originalSettingsJson = EditorJsonUtility.ToJson(settings);
            Require(settings.OverridePlayerVersion == VersionSyncUtility.VersionToken, "Catalog version is not a project version template.");
            var normalize = typeof(VersionSyncUtility).GetMethod("NormalizePath", BindingFlags.Static | BindingFlags.NonPublic);
            Require(normalize != null, "NormalizePath was not found in the imported utility.");

            foreach (var profile in Profiles)
            {
                var id = settings.profileSettings.GetProfileId(profile);
                Require(!string.IsNullOrEmpty(id), "Missing profile: " + profile);
                foreach (var variable in Variables)
                {
                    var raw = settings.profileSettings.GetValueByName(id, variable);
                    Require(raw.Contains($"/{profile}/v/{VersionSyncUtility.VersionToken}/"), "Raw version token is missing: " + profile + " " + variable);
                    var legacy = raw.Replace(VersionSyncUtility.VersionToken, "3.0.1");
                    Require((string)normalize.Invoke(null, new object[] { legacy, profile, VersionSyncUtility.VersionToken }) == raw,
                        "Path migration changed its asset endpoint: " + profile + " " + variable);
                    report.paths.Add(new PathProbe { profile = profile, variable = variable, rawTemplate = raw });
                }
            }

            for (var index = 0; index < report.testedVersions.Length; index++)
            {
                PlayerSettings.bundleVersion = report.testedVersions[index];
                AddressablesRuntimeProperties.ClearCachedPropertyValues();
                foreach (var probe in report.paths)
                {
                    var id = settings.profileSettings.GetProfileId(probe.profile);
                    var raw = settings.profileSettings.GetValueByName(id, probe.variable);
                    Require(raw == probe.rawTemplate, "Testing a version changed the raw profile template.");
                    var resolved = settings.profileSettings.EvaluateString(id, raw);
                    Require(resolved.Contains($"/{probe.profile}/v/{report.testedVersions[index]}/") && !resolved.Contains(VersionSyncUtility.VersionToken),
                        "Project version did not resolve in " + probe.profile + " " + probe.variable);
                    if (index == 0) probe.firstResolvedPath = resolved;
                    else probe.secondResolvedPath = resolved;
                }
                Require(settings.PlayerBuildVersion == report.testedVersions[index], "Catalog filename resolved a different project version.");
                Require(SourcesMatch(snapshots), "A temporary test version was persisted to a source file.");
            }
            Require(report.paths.All(path => path.firstResolvedPath != path.secondResolvedPath), "Changing the project version did not change every asset path.");
            report.templatesUnchanged = settings.OverridePlayerVersion == VersionSyncUtility.VersionToken
                && EditorJsonUtility.ToJson(settings) == originalSettingsJson;
            Require(report.templatesUnchanged, "Version probing changed Addressables settings.");
        }
        catch (Exception exception)
        {
            report.errors.Add(exception.ToString());
        }
        finally
        {
            try
            {
                PlayerSettings.bundleVersion = originalVersion;
                AddressablesRuntimeProperties.ClearCachedPropertyValues();
                foreach (var snapshot in snapshots)
                {
                    if (!File.Exists(snapshot.Key) || !File.ReadAllBytes(snapshot.Key).SequenceEqual(snapshot.Value))
                        File.WriteAllBytes(snapshot.Key, snapshot.Value);
                }
                report.projectVersionRestored = PlayerSettings.bundleVersion == originalVersion;
                report.sourceFilesUnchanged = SourcesMatch(snapshots);
                Require(report.projectVersionRestored && report.sourceFilesUnchanged, "Version validation did not restore its original state.");
            }
            catch (Exception exception) { report.errors.Add("Restore failed: " + exception); }
        }
        report.passed = report.errors.Count == 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        if (report.passed) Debug.Log("Version sync validation passed: four dynamic paths, catalog version, unchanged templates and restored source files.");
        else Debug.LogError(string.Join("\n", report.errors));
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    static bool SourcesMatch(Dictionary<string, byte[]> snapshots) =>
        snapshots.All(snapshot => File.Exists(snapshot.Key) && File.ReadAllBytes(snapshot.Key).SequenceEqual(snapshot.Value));

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
