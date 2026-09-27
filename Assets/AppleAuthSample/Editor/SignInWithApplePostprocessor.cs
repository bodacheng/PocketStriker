#if UNITY_IOS || UNITY_TVOS
#define UNITY_XCODE_EXTENSIONS_AVAILABLE
#endif

using AppleAuth.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_XCODE_EXTENSIONS_AVAILABLE
using UnityEditor.iOS.Xcode;
#endif

namespace AppleAuthSample.Editor
{
    public static class SignInWithApplePostprocessor
    {
        private const int CallOrder = 1;

        [PostProcessBuild(CallOrder)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target == BuildTarget.iOS || target == BuildTarget.tvOS)
            {
                #if UNITY_XCODE_EXTENSIONS_AVAILABLE
                    var projectPath = PBXProject.GetPBXProjectPath(path);
                    #if UNITY_6000_0_OR_NEWER
                        var project = new PBXProject();
                        project.ReadFromFile(projectPath);
                        var mainTarget = project.GetUnityMainTargetGuid();
                        var entitlements = project.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");
                        if (string.IsNullOrEmpty(entitlements))
                            entitlements = "Entitlements.entitlements";

                        // Unity 6 provides a public API; the legacy compatibility helper
                        // reflects private Xcode fields that changed in Unity 6000.5.
                        var manager = new ProjectCapabilityManager(projectPath, entitlements, null, mainTarget);
                        manager.AddSignInWithApple();
                        manager.WriteToFile();

                        project.ReadFromFile(projectPath);
                        project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", true);
                        project.WriteToFile(projectPath);
                    #elif UNITY_2019_3_OR_NEWER
                        var project = new PBXProject();
                        project.ReadFromString(System.IO.File.ReadAllText(projectPath));
                        var manager = new ProjectCapabilityManager(projectPath, "Entitlements.entitlements", null, project.GetUnityMainTargetGuid());
                        manager.AddSignInWithAppleWithCompatibility(project.GetUnityFrameworkTargetGuid());
                        manager.WriteToFile();
                    #else
                        var manager = new ProjectCapabilityManager(projectPath, "Entitlements.entitlements", PBXProject.GetUnityTargetName());
                        manager.AddSignInWithAppleWithCompatibility();
                        manager.WriteToFile();
                    #endif
                #endif
            }
            else if (target == BuildTarget.StandaloneOSX)
            {
                AppleAuthMacosPostprocessorHelper.FixManagerBundleIdentifier(target, path);
            }
        }
    }
}
