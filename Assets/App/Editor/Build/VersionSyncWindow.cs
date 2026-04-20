using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Cocone.ProjectP3
{
    public sealed class VersionSyncWindow : EditorWindow
    {
        private const string P3MenuPath = "P3/Version/Sync Version Settings";
        private const string MCombatMenuPath = "MCombat/Version Sync";
        private const string WindowTitle = "Version Sync";

        private string versionText;
        private string buildNumberText;
        private string resourceVersionText;

        [MenuItem(P3MenuPath)]
        [MenuItem(MCombatMenuPath, priority = 5)]
        public static void Open()
        {
            var window = GetWindow<VersionSyncWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(520f, 360f);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Current", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Program Version", PlayerSettings.bundleVersion);
            EditorGUILayout.LabelField("Resource Version", string.IsNullOrEmpty(resourceVersionText) ? "(missing)" : resourceVersionText);
            EditorGUILayout.LabelField("Android Version Code", PlayerSettings.Android.bundleVersionCode.ToString());
            EditorGUILayout.LabelField("iOS Build Number", PlayerSettings.iOS.buildNumber);

            if (string.IsNullOrEmpty(resourceVersionText))
            {
                EditorGUILayout.HelpBox("当前资源版本文件不存在，或 version 字段为空。", MessageType.Warning);
            }
            else if (!string.Equals(PlayerSettings.bundleVersion, resourceVersionText, StringComparison.Ordinal))
            {
                EditorGUILayout.HelpBox("当前程序版本和资源版本不同步。", MessageType.Warning);
            }

            if (PlayerSettings.Android.bundleVersionCode.ToString() != PlayerSettings.iOS.buildNumber)
            {
                EditorGUILayout.HelpBox("当前 Android Version Code 和 iOS Build Number 不同。", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apply", EditorStyles.boldLabel);
            versionText = EditorGUILayout.TextField("Version", versionText);
            buildNumberText = EditorGUILayout.TextField("Build Number", buildNumberText);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Patch +1"))
                {
                    BumpVersion(VersionBumpKind.Patch);
                }

                if (GUILayout.Button("Minor +1"))
                {
                    BumpVersion(VersionBumpKind.Minor);
                }

                if (GUILayout.Button("Major +1"))
                {
                    BumpVersion(VersionBumpKind.Major);
                }

                if (GUILayout.Button("Build +1"))
                {
                    buildNumberText = VersionSyncUtility.GetNextBuildNumber(buildNumberText).ToString();
                }
            }

            EditorGUILayout.HelpBox(
                "会同时更新 PlayerSettings、app_version.json、Addressables Profile，以及 AddressablesProfileSettings.yaml。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload"))
                {
                    Refresh();
                }

                using (new EditorGUI.DisabledScope(!VersionSyncUtility.TryValidate(versionText, buildNumberText, out var buildNumber, out _)))
                {
                    if (GUILayout.Button("Apply"))
                    {
                        try
                        {
                            VersionSyncUtility.Apply(versionText, buildNumber);
                            Refresh();
                            EditorUtility.DisplayDialog(WindowTitle, "版本同步完成。", "OK");
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                            EditorUtility.DisplayDialog(WindowTitle, exception.Message, "OK");
                        }
                    }
                }
            }

            if (!VersionSyncUtility.TryValidate(versionText, buildNumberText, out _, out var validationError))
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Error);
            }
        }

        private void Refresh()
        {
            versionText = PlayerSettings.bundleVersion;
            buildNumberText = VersionSyncUtility.GetSuggestedBuildNumber().ToString();
            resourceVersionText = VersionSyncUtility.ReadResourceVersion();
        }

        private void BumpVersion(VersionBumpKind bumpKind)
        {
            if (VersionSyncUtility.TryBumpVersion(versionText, bumpKind, out var bumpedVersion, out var errorMessage))
            {
                versionText = bumpedVersion;
                return;
            }

            EditorUtility.DisplayDialog(WindowTitle, errorMessage, "OK");
        }
    }

    internal enum VersionBumpKind
    {
        Patch,
        Minor,
        Major
    }

    internal static class VersionSyncUtility
    {
        private const string AppVersionJsonPath = "Assets/ExternalAssets/Config/app_version.json";
        private const string AddressablesProfileSettingsPath = "Assets/App/Editor/Build/Configs/AddressablesProfileSettings.yaml";
        private const string DevProfileName = "dev";
        private const string ReleaseProfileName = "release";
        private const string RemoteBuildPathName = "Remote.BuildPath";
        private const string RemoteLoadPathName = "Remote.LoadPath";

        [Serializable]
        private sealed class AppVersionPayload
        {
            public string version;
        }

        public static int GetSuggestedBuildNumber()
        {
            var androidBuildNumber = PlayerSettings.Android.bundleVersionCode;
            var iosBuildNumber = 0;
            int.TryParse(PlayerSettings.iOS.buildNumber, out iosBuildNumber);
            return Math.Max(androidBuildNumber, iosBuildNumber);
        }

        public static int GetNextBuildNumber(string buildNumberText)
        {
            if (int.TryParse(buildNumberText, out var buildNumber) && buildNumber >= 0)
            {
                return buildNumber + 1;
            }

            return GetSuggestedBuildNumber() + 1;
        }

        public static string ReadResourceVersion()
        {
            if (!File.Exists(AppVersionJsonPath))
            {
                return string.Empty;
            }

            var payload = JsonUtility.FromJson<AppVersionPayload>(File.ReadAllText(AppVersionJsonPath));
            return payload == null ? string.Empty : payload.version ?? string.Empty;
        }

        public static bool TryValidate(string versionText, string buildNumberText, out int buildNumber, out string errorMessage)
        {
            buildNumber = 0;

            if (string.IsNullOrWhiteSpace(versionText))
            {
                errorMessage = "Version 不能为空。";
                return false;
            }

            if (!Regex.IsMatch(versionText, @"^\d+(?:\.\d+)+$"))
            {
                errorMessage = "Version 需要是纯数字点分格式，例如 2.3.4。";
                return false;
            }

            if (!int.TryParse(buildNumberText, out buildNumber) || buildNumber < 0)
            {
                errorMessage = "Build Number 需要是大于等于 0 的整数。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool TryBumpVersion(string versionText, VersionBumpKind bumpKind, out string bumpedVersion, out string errorMessage)
        {
            bumpedVersion = string.Empty;

            if (string.IsNullOrWhiteSpace(versionText))
            {
                errorMessage = "Version 不能为空。";
                return false;
            }

            var segments = versionText.Split('.');
            if (segments.Length == 0)
            {
                errorMessage = "Version 不能为空。";
                return false;
            }

            var normalizedSegments = new int[Math.Max(segments.Length, 3)];
            for (var i = 0; i < normalizedSegments.Length; i++)
            {
                if (i >= segments.Length)
                {
                    normalizedSegments[i] = 0;
                    continue;
                }

                if (!int.TryParse(segments[i], out normalizedSegments[i]) || normalizedSegments[i] < 0)
                {
                    errorMessage = "Version 需要是纯数字点分格式，例如 2.3.4。";
                    return false;
                }
            }

            switch (bumpKind)
            {
                case VersionBumpKind.Major:
                    normalizedSegments[0] += 1;
                    for (var i = 1; i < normalizedSegments.Length; i++)
                    {
                        normalizedSegments[i] = 0;
                    }
                    break;

                case VersionBumpKind.Minor:
                    normalizedSegments[1] += 1;
                    for (var i = 2; i < normalizedSegments.Length; i++)
                    {
                        normalizedSegments[i] = 0;
                    }
                    break;

                default:
                    normalizedSegments[normalizedSegments.Length - 1] += 1;
                    break;
            }

            bumpedVersion = string.Join(".", normalizedSegments);
            errorMessage = string.Empty;
            return true;
        }

        public static void Apply(string version, int buildNumber)
        {
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = buildNumber;
            PlayerSettings.iOS.buildNumber = buildNumber.ToString();

            WriteAppVersionJson(version);
            UpdateAddressablesProfileSettings(version);
            UpdateAddressablesProfileYaml(version);

            AssetDatabase.ImportAsset(AppVersionJsonPath);
            AssetDatabase.ImportAsset(AddressablesProfileSettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VersionSync] Version={version}, BuildNumber={buildNumber}");
        }

        private static void WriteAppVersionJson(string version)
        {
            var directoryPath = Path.GetDirectoryName(AppVersionJsonPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var content = "{\n" +
                          $"    \"version\": \"{version}\"\n" +
                          "}\n";
            File.WriteAllText(AppVersionJsonPath, content);
        }

        private static void UpdateAddressablesProfileSettings(string version)
        {
            var settings = BuildAddressableAssets.GetSettings();
            if (settings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettings が見つかりません。");
            }

            UpdateAddressableProfileValue(settings, DevProfileName, RemoteBuildPathName, version);
            UpdateAddressableProfileValue(settings, DevProfileName, RemoteLoadPathName, version);
            UpdateAddressableProfileValue(settings, ReleaseProfileName, RemoteBuildPathName, version);
            UpdateAddressableProfileValue(settings, ReleaseProfileName, RemoteLoadPathName, version);

            EditorUtility.SetDirty(settings);
        }

        private static void UpdateAddressableProfileValue(AddressableAssetSettings settings, string profileName, string variableName, string version)
        {
            var profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                throw new InvalidOperationException($"Addressables Profile 不存在: {profileName}");
            }

            var currentValue = settings.profileSettings.GetValueByName(profileId, variableName);
            var updatedValue = ReplaceProfileVersion(currentValue, profileName, version, false, $"Addressables {profileName} {variableName}");
            settings.profileSettings.SetValue(profileId, variableName, updatedValue);
        }

        private static void UpdateAddressablesProfileYaml(string version)
        {
            if (!File.Exists(AddressablesProfileSettingsPath))
            {
                throw new FileNotFoundException("AddressablesProfileSettings.yaml 不存在。", AddressablesProfileSettingsPath);
            }

            var content = File.ReadAllText(AddressablesProfileSettingsPath);
            content = ReplaceProfileVersion(content, DevProfileName, version, true, AddressablesProfileSettingsPath);
            content = ReplaceProfileVersion(content, ReleaseProfileName, version, true, AddressablesProfileSettingsPath);
            File.WriteAllText(AddressablesProfileSettingsPath, content);
        }

        private static string ReplaceProfileVersion(string input, string profileName, string version, bool replaceAll, string context)
        {
            var regex = new Regex($@"(/{Regex.Escape(profileName)}/(?:v/)?)\d+(?:\.\d+)+(?=(/|$))", RegexOptions.CultureInvariant);
            var replaceCount = 0;

            var output = regex.Replace(input, match =>
            {
                if (!replaceAll && replaceCount > 0)
                {
                    return match.Value;
                }

                replaceCount += 1;
                return match.Groups[1].Value + version;
            });

            if (replaceCount == 0)
            {
                throw new InvalidOperationException($"{context} 中没有找到 {profileName} 的版本号片段。");
            }

            return output;
        }
    }
}
