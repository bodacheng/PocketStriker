using System;
using UnityEditor;
using UnityEngine;

/// <summary>Use iOS for ordinary project opens; explicit CLI targets take precedence.</summary>
[InitializeOnLoad]
public static class PocketStrikerEditorDefaults
{
    const string SessionKey = "PocketStriker.DefaultPlatformApplied";

    static PocketStrikerEditorDefaults()
    {
        if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
        foreach (var argument in Environment.GetCommandLineArgs())
        {
            if (argument.Equals("-buildTarget", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("-activeBuildProfile", StringComparison.OrdinalIgnoreCase))
                return;
        }
        EditorApplication.update += ApplyDefaultPlatform;
    }

    static void ApplyDefaultPlatform()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= ApplyDefaultPlatform;
        SessionState.SetBool(SessionKey, true);
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) return;
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            Debug.LogWarning("PocketStriker defaults to iOS. Install iOS Build Support for this Unity editor to activate it.");
            return;
        }
        if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.iOS, BuildTarget.iOS))
            Debug.LogWarning("Unable to activate PocketStriker's default iOS target. Select iOS in Build Profiles.");
    }
}
