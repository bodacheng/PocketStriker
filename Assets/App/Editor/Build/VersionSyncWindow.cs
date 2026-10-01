using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine;

namespace Cocone.ProjectP3
{
    public sealed class VersionSyncWindow : EditorWindow
    {
        private string versionText;

        [MenuItem("MCombat/Version Sync", priority = 5)]
        [MenuItem("P3/Version/Sync Version Settings")]
        public static void Open()
        {
            var window = GetWindow<VersionSyncWindow>("Version Sync");
            window.minSize = new Vector2(440, 180);
        }

        private void OnEnable() => versionText = PlayerSettings.bundleVersion;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("当前版本", PlayerSettings.bundleVersion);
            versionText = EditorGUILayout.TextField("版本", versionText);
            EditorGUILayout.HelpBox("程序、资源路径和 catalog 共用此版本。资源由独立 Jenkins 任务编译、发布；程序构建复用已发布的本地包和 catalog，运行时从对应地址下载远程资源。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Patch +1") && VersionSyncUtility.IsValidVersion(versionText))
                {
                    var parts = versionText.Split('.');
                    if (int.TryParse(parts[parts.Length - 1], out var patch) && patch < int.MaxValue)
                    {
                        parts[parts.Length - 1] = (patch + 1).ToString();
                        versionText = string.Join(".", parts);
                    }
                }
                using (new EditorGUI.DisabledScope(!VersionSyncUtility.IsValidVersion(versionText)))
                {
                    if (GUILayout.Button("应用"))
                    {
                        try { VersionSyncUtility.Apply(versionText); }
                        catch (Exception exception) { Debug.LogException(exception); }
                    }
                }
            }
        }
    }

    internal static class VersionSyncUtility
    {
        internal const string VersionToken = "[UnityEditor.PlayerSettings.bundleVersion]";
        private const string AppVersionJsonPath = "Assets/ExternalAssets/Config/app_version.json";
        private const string ProfileYamlPath = "Assets/App/Editor/Build/Configs/AddressablesProfileSettings.yaml";
        private static readonly string[] Profiles = { "dev", "release" };
        private static readonly string[] PathVariables = { "Remote.BuildPath", "Remote.LoadPath" };

        [Serializable]
        private sealed class AppVersionPayload { public string version; }

        public static bool IsValidVersion(string version) =>
            !string.IsNullOrEmpty(version) && Regex.IsMatch(version, @"^\d+\.\d+\.\d+$");

        public static string ReadResourceVersion()
        {
            if (!File.Exists(AppVersionJsonPath)) return string.Empty;
            return JsonUtility.FromJson<AppVersionPayload>(File.ReadAllText(AppVersionJsonPath))?.version ?? string.Empty;
        }

        public static void Apply(string version)
        {
            if (!IsValidVersion(version)) throw new ArgumentException("版本格式应为 3.0.2。", nameof(version));
            var settings = BuildAddressableAssets.GetSettings();
            if (settings == null) throw new InvalidOperationException("AddressableAssetSettings was not found.");
            // Validate all templates before changing the project version or files.
            var updates = new List<(string profileId, string variable, string value)>();
            foreach (var profile in Profiles)
            {
                var id = settings.profileSettings.GetProfileId(profile);
                if (string.IsNullOrEmpty(id)) throw new InvalidOperationException($"Missing Addressables profile: {profile}");
                foreach (var variable in PathVariables)
                    updates.Add((id, variable, NormalizePath(settings.profileSettings.GetValueByName(id, variable), profile, VersionToken)));
            }
            var yaml = File.ReadAllText(ProfileYamlPath);
            foreach (var profile in Profiles) yaml = NormalizePath(yaml, profile, "{version}");
            ValidateYaml(yaml);

            PlayerSettings.bundleVersion = version;
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            File.WriteAllText(AppVersionJsonPath, JsonUtility.ToJson(new AppVersionPayload { version = version }, true) + "\n");
            File.WriteAllText(ProfileYamlPath, yaml);
            foreach (var update in updates) settings.profileSettings.SetValue(update.profileId, update.variable, update.value);
            settings.OverridePlayerVersion = VersionToken;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            EditorUtility.SetDirty(settings);
            AssetDatabase.ImportAsset(AppVersionJsonPath);
            AssetDatabase.ImportAsset(ProfileYamlPath);
            AssetDatabase.SaveAssets();
            AssertVersionSettingsSynchronized();
            Debug.Log($"[VersionSync] Project and Addressables version: {version}");
        }

        private static string NormalizePath(string input, string profile, string token)
        {
            var pattern = $@"(/{Regex.Escape(profile)}/v/)(?:\d+\.\d+\.\d+|\[UnityEditor\.PlayerSettings\.bundleVersion\]|\{{version\}})(?=/|[\r\n]|$)";
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            if (!regex.IsMatch(input ?? string.Empty)) throw new InvalidOperationException($"Missing versioned {profile} resource path.");
            return regex.Replace(input, match => match.Groups[1].Value + token);
        }

        private static void ValidateYaml(string yaml)
        {
            foreach (var profile in Profiles)
            foreach (var kind in new[] { "Build", "Upload" })
            {
                var key = kind + (profile == "dev" ? "Dev" : "Release");
                var matches = Regex.Matches(yaml, $@"^{key}:[ \t]*([^\r\n]+)", RegexOptions.Multiline);
                if (matches.Count != 1 || !matches[0].Groups[1].Value.Contains($"/{profile}/v/{{version}}"))
                    throw new InvalidOperationException($"Jenkins {key} must contain one {{version}} path template.");
            }
        }

        public static void AssertVersionSettingsSynchronized()
        {
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            var version = PlayerSettings.bundleVersion;
            var errors = new List<string>();
            if (!IsValidVersion(version)) errors.Add($"Invalid project version: {version}");
            if (ReadResourceVersion() != version) errors.Add("app_version.json differs from the project version.");
            var settings = BuildAddressableAssets.GetSettings();
            if (settings == null) errors.Add("AddressableAssetSettings was not found.");
            else
            {
                if (settings.OverridePlayerVersion != VersionToken) errors.Add("Catalog version must derive from PlayerSettings.bundleVersion.");
                if (settings.BuildAddressablesWithPlayerBuild != AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer)
                    errors.Add("Addressables must be built independently from the player.");
                foreach (var profile in Profiles)
                {
                    var id = settings.profileSettings.GetProfileId(profile);
                    if (string.IsNullOrEmpty(id)) { errors.Add($"Missing Addressables profile: {profile}"); continue; }
                    foreach (var variable in PathVariables)
                    {
                        var value = settings.profileSettings.GetValueByName(id, variable);
                        if (!(value ?? string.Empty).Contains($"/{profile}/v/{VersionToken}/"))
                            errors.Add($"{profile} {variable} must use the project version token.");
                        else if (!settings.profileSettings.EvaluateString(id, value).Contains($"/{profile}/v/{version}/"))
                            errors.Add($"Cannot resolve project version in {profile} {variable}.");
                    }
                }
            }
            try { ValidateYaml(File.ReadAllText(ProfileYamlPath)); }
            catch (Exception exception) { errors.Add(exception.Message); }
            if (errors.Count > 0) throw new InvalidOperationException("Run MCombat/Version Sync to synchronize the project.\n" + string.Join("\n", errors));
        }
    }
}
