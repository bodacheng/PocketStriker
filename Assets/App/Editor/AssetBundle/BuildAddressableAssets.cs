using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
#endif

namespace Cocone.ProjectP3
{
    public static class BuildAddressableAssets
    {
#if UNITY_EDITOR

        // バッチモード用一括ビルド
        public static void BatchBuild()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            BatchBuildInternal(args);
        }
        
        private static void BatchBuildInternal(string[] args)
        {
            // 引数取得
            string assetProfile = "dev";
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-assetVersion":
                        throw new BuildFailedException("-assetVersion is no longer supported. Set the project version in MCombat/Version Sync.");

                    case "-assetProfile":
                        assetProfile = args[i + 1];
                        i++;
                        break;
                }
            }

            SetProfile(assetProfile);
            CleanBuild();
        }

        public static void SetProfile(string assetProfile)
        {
            if (assetProfile != "dev" && assetProfile != "release")
            {
                throw new BuildFailedException("Asset builds require the dev or release Addressables profile.");
            }

            var settings = GetSettings();
            if (settings == null)
            {
                throw new BuildFailedException("AddressableAssetSettings was not found.");
            }

            var profileId = settings.profileSettings.GetProfileId(assetProfile);
            if (string.IsNullOrEmpty(profileId))
            {
                throw new BuildFailedException($"Addressables profile does not exist: {assetProfile}");
            }

            Debug.Log($"Selected Addressables profile: {assetProfile} ({profileId})");
            settings.activeProfileId = profileId;
            
            // save addressable setting
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("P3/Build/Addressable(テスト用)/iOS/Alpha")]
        public static void BuildCommandAddressableIOSBuildAlpha()
        {
            var workspace = ".";
            var unityMethod = "Cocone.ProjectP3.BuildAddressableAssets.BatchBuild";
            var buildTarget = "iOS";

            string[] args =
            {
                "-projectPath", workspace,
                "-quit", "-batchmode",
                "-executeMethod", unityMethod,
                "-buildTarget", buildTarget,
                "-assetProfile", "release"
            };
            BatchBuildInternal(args);
        }

        [MenuItem("P3/Build/Addressable(テスト用)/iOS/Test")]
        public static void BuildCommandAddressableIOSBuildTest()
        {
            var workspace = ".";
            var unityMethod = "Cocone.ProjectP3.BuildAddressableAssets.BatchBuild";
            var buildTarget = "iOS";

            string[] args =
            {
                "-projectPath", workspace,
                "-quit", "-batchmode",
                "-executeMethod", unityMethod,
                "-buildTarget", buildTarget,
                "-assetProfile", "dev"
            };
            BatchBuildInternal(args);
        }

        [MenuItem("P3/Build/Addressable(テスト用)/Android/Alpha")]
        public static void BuildCommandAddressableAndroidBuildAlpha()
        {
            var workspace = ".";
            var unityMethod = "Cocone.ProjectP3.BuildAddressableAssets.BatchBuild";
            var buildTarget = "Android";

            string[] args =
            {
                "-projectPath", workspace,
                "-quit", "-batchmode",
                "-executeMethod", unityMethod,
                "-buildTarget", buildTarget,
                "-assetProfile", "release"
            };
            BatchBuildInternal(args);
        }

        [MenuItem("P3/Build/Addressable(テスト用)/Android/Test")]
        public static void BuildCommandAddressableAndroidBuildTest()
        {
            var workspace = ".";
            var unityMethod = "Cocone.ProjectP3.BuildAddressableAssets.BatchBuild";
            var buildTarget = "Android";

            string[] args =
            {
                "-projectPath", workspace,
                "-quit", "-batchmode",
                "-executeMethod", unityMethod,
                "-buildTarget", buildTarget,
                "-assetProfile", "dev"
            };
            BatchBuildInternal(args);
        }

        // アセットバンドルをクリーンビルドします
        [MenuItem("Tools/Asset/CleanBuild")]
        public static void CleanBuild()
        {
            var settings = GetSettings();
            if (settings == null)
            {
                throw new BuildFailedException("AddressableAssetSettings was not found.");
            }
            var profile = settings.profileSettings.GetProfileName(settings.activeProfileId);
            if (profile != "dev" && profile != "release")
            {
                throw new BuildFailedException("Select the dev or release Addressables profile before building assets.");
            }
            VersionSyncUtility.AssertVersionSettingsSynchronized();
            PocketStrikerDownloadValidation.RequireSettings(settings);
            AddressableAssetSettings.CleanPlayerContent();
            BuildCache.PurgeCache(false);
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result == null || !string.IsNullOrEmpty(result.Error))
            {
                throw new BuildFailedException($"Addressables build failed: {result?.Error ?? "No build result was returned."}");
            }
        }

/*
    [MenuItem("Tools/Asset/UpdateRemotePath")]
    public static void UpdateRemotePath()
    {
        var list = AssetDatabase
                .FindAssets( "t:BundledAssetGroupSchema" )
                .Select( c => AssetDatabase.GUIDToAssetPath( c ) )
                .Select( c => AssetDatabase.LoadAssetAtPath<BundledAssetGroupSchema>( c ) );

        var settings = GetSettings();
        foreach ( var schema in list )
        {
            if (schema.Group.name == "Default Local Group")
            {
                schema.BuildPath.SetVariableByName( settings, "LocalBuildPath" );
                schema.LoadPath.SetVariableByName( settings, "LocalLoadPath" );
            }
            else
            {
                schema.BuildPath.SetVariableByName( settings, "RemoteBuildPath" );
                schema.LoadPath.SetVariableByName( settings, "RemoteLoadPath" );
            }
        }
    }
*/
        // AddressableAssetSettings を取得します
        public static AddressableAssetSettings GetSettings()
        {
            var guidList = AssetDatabase.FindAssets("t:AddressableAssetSettings");
            var guid = guidList.FirstOrDefault();
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);

            return settings;
        }
#endif
    }
}
