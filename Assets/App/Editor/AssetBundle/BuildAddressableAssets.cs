using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
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
            BuildTarget buildTarget = BuildTarget.iOS;
            BuildTargetGroup buildTargetGroup = BuildTargetGroup.iOS;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-assetVersion":
                        i++; // ignore assetVersion
                        break;
                    
                    case "-buildTarget":
                        buildTarget = (BuildTarget) System.Enum.Parse(typeof(BuildTarget), args[i + 1]);
                        buildTargetGroup = (BuildTargetGroup) System.Enum.Parse(typeof(BuildTargetGroup), args[i + 1]);
                        i++;
                        break;

                    case "-assetProfile":
                        assetProfile = args[i + 1];
                        i++;
                        break;
                }
            }

            var settings = GetSettings();
            Debug.Log(assetProfile);
            var profileId = settings.profileSettings.GetProfileId(assetProfile);
            Debug.Log(profileId);
            settings.activeProfileId = profileId;
            
            // save addressable setting
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            CleanBuild(); // TODO: TargetPlatform設定は要らない？
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
                "-assetProfile", "P3Dev"
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
            AddressableAssetSettings.CleanPlayerContent();
            BuildCache.PurgeCache(false);
            AddressableAssetSettings.BuildPlayerContent();
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