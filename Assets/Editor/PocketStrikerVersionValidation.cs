using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cocone.ProjectP3;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
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
        "Assets/AddressableAssetsData/AddressableAssetSettings.asset",
        "Assets/AddressableAssetsData/AssetGroups/Schemas/Units_BundledAssetGroupSchema.asset"
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
        public bool stableMonoScriptBundlePrefix;
        public bool workspaceNamingRejected;
        public bool independentPlayerBuild;
        public bool remoteCatalogCheckedOnStartup;
        public bool modelsRemote;
        public bool modelBundlesContentHashed;
        public bool publishedAssetDependencyRejected;
        public bool localModelBuildPathRejected;
        public bool localModelLoadPathRejected;
        public bool unhashedModelBundlesRejected;
        public bool buildPolicySettingsRestored;
        public string monoScriptBundlePrefix;
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
            ValidateMonoScriptBundleNaming(settings, report);
            ValidateIndependentBuildPolicy(settings, report);
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
        if (report.passed) Debug.Log("Version sync validation passed: dynamic paths, independent player content, remote hashed models, stable MonoScript bundle prefix, rejected incompatible build settings and restored source files.");
        else Debug.LogError(string.Join("\n", report.errors));
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    static void ValidateMonoScriptBundleNaming(AddressableAssetSettings settings, Report report)
    {
        var prefixMethod = typeof(BuildScriptPackedMode).GetMethod("GetMonoScriptBundleNamePrefix",
            BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(AddressableAssetSettings) }, null);
        Require(prefixMethod != null, "Addressables MonoScript bundle prefix resolver was not found.");
        var originalNaming = settings.MonoScriptBundleNaming;
        try
        {
            report.monoScriptBundlePrefix = (string)prefixMethod.Invoke(null, new object[] { settings });
            Require(report.monoScriptBundlePrefix == settings.DefaultGroup.Guid,
                "MonoScript bundle prefix depends on the checkout directory instead of the default group GUID.");
            report.stableMonoScriptBundlePrefix = true;

            // Reproduce the old workspace-derived setting through the actual build
            // guard. Changing this field remains in memory and is restored below.
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.ProjectName;
            try { VersionSyncUtility.AssertVersionSettingsSynchronized(); }
            catch (InvalidOperationException exception)
            {
                report.workspaceNamingRejected = exception.Message.Contains("MonoScript bundle naming must use DefaultGroupGuid");
            }
            Require(report.workspaceNamingRejected, "Build validation accepted workspace-dependent MonoScript bundle naming.");
        }
        finally { settings.MonoScriptBundleNaming = originalNaming; }
    }

    static void ValidateIndependentBuildPolicy(AddressableAssetSettings settings, Report report)
    {
        VersionSyncUtility.AssertVersionSettingsSynchronized();
        var units = settings.FindGroup("Units")?.GetSchema<BundledAssetGroupSchema>();
        Require(units != null, "The Units bundled asset schema is missing.");
        var settingsJson = EditorJsonUtility.ToJson(settings);
        var unitsJson = EditorJsonUtility.ToJson(units);
        var settingsWasDirty = EditorUtility.IsDirty(settings);
        var unitsWereDirty = EditorUtility.IsDirty(units);
        try
        {
            report.independentPlayerBuild = settings.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            report.remoteCatalogCheckedOnStartup = settings.BuildRemoteCatalog && !settings.DisableCatalogUpdateOnStartup;
            report.modelsRemote = units.BuildPath.GetName(settings) == "Remote.BuildPath" && units.LoadPath.GetName(settings) == "Remote.LoadPath";
            report.modelBundlesContentHashed = units.BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
            Require(report.independentPlayerBuild && report.remoteCatalogCheckedOnStartup && report.modelsRemote && report.modelBundlesContentHashed,
                "Addressables content does not follow the independent player/remote model build policy.");

            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            report.publishedAssetDependencyRejected = BuildGuardRejects("Player builds must generate their own Addressables");
            Require(report.publishedAssetDependencyRejected, "Build validation accepted a player build that requires existing asset output.");
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;

            Require(units.BuildPath.SetVariableByName(settings, "Local.BuildPath"), "Could not probe local model build paths.");
            report.localModelBuildPathRejected = BuildGuardRejects("Units models must use Remote.BuildPath and Remote.LoadPath");
            Require(report.localModelBuildPathRejected, "Build validation accepted local model build paths.");
            Require(units.BuildPath.SetVariableByName(settings, "Remote.BuildPath"), "Could not restore remote model build paths.");

            Require(units.LoadPath.SetVariableByName(settings, "Local.LoadPath"), "Could not probe local model load paths.");
            report.localModelLoadPathRejected = BuildGuardRejects("Units models must use Remote.BuildPath and Remote.LoadPath");
            Require(report.localModelLoadPathRejected, "Build validation accepted local model load paths.");
            Require(units.LoadPath.SetVariableByName(settings, "Remote.LoadPath"), "Could not restore remote model load paths.");

            units.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
            report.unhashedModelBundlesRejected = BuildGuardRejects("Units model bundle names must append their content hash");
            Require(report.unhashedModelBundlesRejected, "Build validation accepted model bundle names without a content hash.");
        }
        finally
        {
            EditorJsonUtility.FromJsonOverwrite(unitsJson, units);
            EditorJsonUtility.FromJsonOverwrite(settingsJson, settings);
            if (!unitsWereDirty) EditorUtility.ClearDirty(units);
            if (!settingsWasDirty) EditorUtility.ClearDirty(settings);
            report.buildPolicySettingsRestored = EditorJsonUtility.ToJson(units) == unitsJson && EditorJsonUtility.ToJson(settings) == settingsJson;
        }
        Require(report.buildPolicySettingsRestored, "Build policy probing changed the original Addressables settings or Units schema.");
        VersionSyncUtility.AssertVersionSettingsSynchronized();
    }

    static bool BuildGuardRejects(string expectedMessage)
    {
        try { VersionSyncUtility.AssertVersionSettingsSynchronized(); }
        catch (InvalidOperationException exception) { return exception.Message.Contains(expectedMessage); }
        return false;
    }

    static bool SourcesMatch(Dictionary<string, byte[]> snapshots) =>
        snapshots.All(snapshot => File.Exists(snapshot.Key) && File.ReadAllBytes(snapshot.Key).SequenceEqual(snapshot.Value));

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
