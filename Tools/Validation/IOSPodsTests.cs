using System;
using System.IO;
using System.Reflection;

internal static class IOSPodsTests
{
    public static int Main(string[] args)
    {
        try
        {
            var original = File.ReadAllText(args[0]);
            var updated = PocketStrikerIOSPods.ApplyDeploymentTargetHook(original, "15.0");
            var existingBlock = original.IndexOf("# BEGIN PocketStriker iOS deployment target", StringComparison.Ordinal);
            var originalBody = existingBlock < 0 ? original : original.Substring(0, existingBlock);
            Check(updated.StartsWith(originalBody.TrimEnd() + "\n\n", StringComparison.Ordinal), "existing Podfile content is retained");
            Check(updated == PocketStrikerIOSPods.ApplyDeploymentTargetHook(updated, "15.0"), "repeated export is idempotent");
            var bumped = PocketStrikerIOSPods.ApplyDeploymentTargetHook(updated, "15.10");
            Check(bumped.Contains("Gem::Version.new('15.10')") && !bumped.Contains("Gem::Version.new('15.0')"),
                "PlayerSettings deployment-target changes replace the old hook");
            Check(bumped.Split(new[] { "post_install do" }, StringSplitOptions.None).Length == 2, "one post-install hook remains");
            var method = typeof(PocketStrikerIOSPods).GetMethod("OnPostprocessBuild", BindingFlags.NonPublic | BindingFlags.Static);
            var attribute = (UnityEditor.Callbacks.PostProcessBuildAttribute)Attribute.GetCustomAttribute(method,
                typeof(UnityEditor.Callbacks.PostProcessBuildAttribute));
            Check(attribute.Order == 45, "hook runs between EDM generation and pod install");
            method.Invoke(null, new object[] { UnityEditor.BuildTarget.Other, "unused-path" });
            var exportDirectory = Path.Combine(Path.GetDirectoryName(args[1]), "export");
            Directory.CreateDirectory(exportDirectory);
            File.WriteAllText(Path.Combine(exportDirectory, "Podfile"), original);
            method.Invoke(null, new object[] { UnityEditor.BuildTarget.iOS, exportDirectory });
            Check(File.ReadAllText(Path.Combine(exportDirectory, "Podfile")) == updated, "real callback uses PlayerSettings and exported Podfile");
            File.WriteAllText(args[1], updated);
            Console.WriteLine("PASS: 7 Podfile generation/order checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }
}

namespace UnityEditor
{
    public enum BuildTarget { iOS, Other }
    public static class PlayerSettings
    {
        public static class iOS { public static string targetOSVersionString = "15.0"; }
    }
}
namespace UnityEditor.Build
{
    public sealed class BuildFailedException : Exception { public BuildFailedException(string message) : base(message) { } }
}
namespace UnityEditor.Callbacks
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PostProcessBuildAttribute : Attribute
    {
        public int Order;
        public PostProcessBuildAttribute(int order) { Order = order; }
    }
}
