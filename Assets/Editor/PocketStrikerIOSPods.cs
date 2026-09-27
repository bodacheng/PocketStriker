using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;

internal static class PocketStrikerIOSPods
{
    private const string BeginMarker = "# BEGIN PocketStriker iOS deployment target";
    private const string EndMarker = "# END PocketStriker iOS deployment target";

    // EDM generates the Podfile at 40 and runs pod install at 50.
    [PostProcessBuild(45)]
    private static void OnPostprocessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
            return;

        var podfilePath = Path.Combine(buildPath, "Podfile");
        if (!File.Exists(podfilePath))
            throw new BuildFailedException("The iOS resolver must generate a Podfile before applying the deployment target.");

        var original = File.ReadAllText(podfilePath);
        var updated = ApplyDeploymentTargetHook(original, PlayerSettings.iOS.targetOSVersionString);
        if (updated != original)
            File.WriteAllText(podfilePath, updated);
    }

    internal static string ApplyDeploymentTargetHook(string podfile, string deploymentTarget)
    {
        deploymentTarget = deploymentTarget?.Trim();
        if (string.IsNullOrEmpty(deploymentTarget) || !Regex.IsMatch(deploymentTarget, @"^\d+(?:\.\d+)*$"))
            throw new ArgumentException("The iOS deployment target must be a numeric version.", nameof(deploymentTarget));

        // Replace our own block when exporting into an existing build directory.
        var begin = podfile.IndexOf(BeginMarker, StringComparison.Ordinal);
        while (begin >= 0)
        {
            var end = podfile.IndexOf(EndMarker, begin, StringComparison.Ordinal);
            if (end < 0)
                throw new InvalidOperationException("The PocketStriker Podfile deployment-target block is incomplete.");
            podfile = podfile.Remove(begin, end + EndMarker.Length - begin);
            begin = podfile.IndexOf(BeginMarker, StringComparison.Ordinal);
        }

        return podfile.TrimEnd() + "\n\n" + BeginMarker + "\n" +
            "post_install do |installer|\n" +
            "  minimum_target = Gem::Version.new('" + deploymentTarget + "')\n" +
            "  installer.pods_project.targets.each do |target|\n" +
            "    target.build_configurations.each do |config|\n" +
            "      current_target = config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'].to_s\n" +
            "      if current_target.match?(/\\A\\d+(?:\\.\\d+)*\\z/) && Gem::Version.new(current_target) < minimum_target\n" +
            "        config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '" + deploymentTarget + "'\n" +
            "      end\n" +
            "    end\n" +
            "  end\n" +
            "end\n" + EndMarker + "\n";
    }
}
