using System;
using System.Globalization;
using System.Reflection;

internal static class UpgradeRegressionTests
{
    private static int checks;

    public static int Main(string[] args)
    {
        try
        {
            var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
            Check(TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T11:00:00Z", "2026-09-27T13:00:00Z", now, out var end) &&
                  end == now.AddHours(1) && end.Kind == DateTimeKind.Utc, "UTC sale window");
            Check(TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T20:00:00+09:00", "2026-09-27T22:00:00+09:00", now, out end) &&
                  end == now.AddHours(1), "explicit timezone is normalized to UTC");
            Check(TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T11:00:00", "2026-09-27T13:00:00", now, out end) &&
                  end == now.AddHours(1), "backend timestamps without an offset are UTC");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T11:00:00Z", "2026-09-27T13:00:00Z", now, out end),
                  "sale parsing is independent of device culture");
            Check(!TimeLimitedSaleWindow.TryGetActiveEndUtc(null, "2026-09-27T13:00:00Z", now, out end), "absent start hides sale");
            Check(!TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T11:00:00Z", "invalid", now, out end), "invalid end hides sale");
            Check(!TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T13:00:00Z", "2026-09-27T14:00:00Z", now, out end), "future sale is hidden");
            Check(!TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T11:00:00Z", "2026-09-27T12:00:00Z", now, out end), "sale expires exactly at its end");
            Check(!TimeLimitedSaleWindow.TryGetActiveEndUtc("2026-09-27T13:00:00Z", "2026-09-27T11:00:00Z", now, out end), "reversed window is hidden");

            if (args.Length == 0)
                throw new InvalidOperationException("Pass the imported Assembly-CSharp-Editor.dll to test the actual VersionSyncUtility.");
            var editorAssembly = Assembly.LoadFrom(args[0]);
            var utility = editorAssembly.GetType("Cocone.ProjectP3.VersionSyncUtility", true);
            var normalize = utility.GetMethod("NormalizePath", BindingFlags.NonPublic | BindingFlags.Static);
            var validateYaml = utility.GetMethod("ValidateYaml", BindingFlags.NonPublic | BindingFlags.Static);
            var isValidVersion = utility.GetMethod("IsValidVersion", BindingFlags.Public | BindingFlags.Static);
            var client = editorAssembly.GetType("Cocone.ProjectP3.Client", true);
            var profileForBuild = client.GetMethod("GetAssetProfileForBuildKind", BindingFlags.NonPublic | BindingFlags.Static);
            if (normalize == null || validateYaml == null || isValidVersion == null || profileForBuild == null)
                throw new InvalidOperationException("Import the upgraded project in Unity before running this test.");

            const string versionToken = "[UnityEditor.PlayerSettings.bundleVersion]";
            const string yaml = "ProfileDev: dev\nBuildDev: ServerData/dev/v/3.0.0/\nUploadDev: s3://mcombat/dev/v/3.0.0\nProfileRelease: release\nBuildRelease: ServerData/release/v/3.0.0/\nUploadRelease: s3://mcombat/release/v/3.0.0\n";
            foreach (var newline in new[] { "\n", "\r\n" })
            {
                var input = yaml.Replace("\n", newline);
                var migrated = (string)normalize.Invoke(null, new object[] { input, "dev", "{version}" });
                migrated = (string)normalize.Invoke(null, new object[] { migrated, "release", "{version}" });
                Check(migrated == input.Replace("3.0.0", "{version}"), "all four YAML paths migrate without changing their endpoints or line endings");
                validateYaml.Invoke(null, new object[] { migrated });
                Check(true, "migrated YAML validates");
                Check((string)normalize.Invoke(null, new object[] { migrated, "dev", "{version}" }) == migrated,
                    "migration is idempotent for existing YAML templates");
                foreach (var key in new[] { "BuildDev", "UploadDev", "BuildRelease", "UploadRelease" })
                {
                    var lines = migrated.Split(new[] { newline }, StringSplitOptions.None);
                    var line = Array.Find(lines, value => value.StartsWith(key + ":", StringComparison.Ordinal));
                    ExpectRejected(validateYaml, new object[] { migrated.Replace(line + newline, "") }, typeof(InvalidOperationException), "missing YAML path " + key);
                    ExpectRejected(validateYaml, new object[] { migrated + line + newline }, typeof(InvalidOperationException), "duplicate YAML path " + key);
                    ExpectRejected(validateYaml, new object[] { migrated.Replace(line, key + ":") }, typeof(InvalidOperationException), "empty YAML path " + key);
                }
                ExpectRejected(validateYaml, new object[] { input }, typeof(InvalidOperationException), "literal YAML versions are rejected");
            }

            foreach (var profile in new[] { "dev", "release" })
            foreach (var prefix in new[] { "ServerData", "https://mcombat.s3.ap-northeast-1.amazonaws.com", "s3://mcombat" })
            {
                var address = prefix + "/" + profile + "/v/3.0.0/[BuildTarget]";
                var templated = (string)normalize.Invoke(null, new object[] { address, profile, versionToken });
                Check(templated == address.Replace("3.0.0", versionToken), "asset address preserves its endpoint while adopting the project version token");
                Check((string)normalize.Invoke(null, new object[] { templated, profile, versionToken }) == templated,
                    "profile token migration is idempotent");
                Check((string)normalize.Invoke(null, new object[] { address.Replace("3.0.0", "{version}"), profile, versionToken }) == templated,
                    "existing YAML tokens migrate to profile tokens");
            }
            ExpectRejected(normalize, new object[] { "ServerData/release/v/3.0.0/iOS", "dev", versionToken }, typeof(InvalidOperationException), "mismatched endpoint profile");
            ExpectRejected(normalize, new object[] { "ServerData/dev/iOS", "dev", versionToken }, typeof(InvalidOperationException), "missing version path");
            ExpectRejected(normalize, new object[] { null, "dev", versionToken }, typeof(InvalidOperationException), "missing input path");
            foreach (var version in new[] { "0.0.0", "3.0.2", "12.34.56" })
                Check((bool)isValidVersion.Invoke(null, new object[] { version }), "valid project version " + version);
            foreach (var version in new[] { null, "", "3.0", "3.0.2.1", "v3.0.2", "3.-1.2", "3.0.2-beta", "3.0.2 " })
                Check(!(bool)isValidVersion.Invoke(null, new object[] { version }), "invalid project version is rejected");
            Check((string)profileForBuild.Invoke(null, new object[] { "Dev" }) == "dev", "Dev clients use dev assets");
            Check((string)profileForBuild.Invoke(null, new object[] { "Release" }) == "release", "Release clients use release assets");
            foreach (var kind in new[] { null, "", "Beta", "invalid" })
                ExpectRejected(profileForBuild, new object[] { kind }, null, "unsupported client build kind");

            Console.WriteLine("PASS: " + checks + " time-window and version-sync regression checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("FAILED: " + message);
        checks++;
    }

    private static void ExpectRejected(MethodInfo method, object[] arguments, Type exceptionType, string message)
    {
        try { method.Invoke(null, arguments); }
        catch (TargetInvocationException exception)
        {
            Check(exceptionType == null
                    ? exception.InnerException?.GetType().FullName == "UnityEditor.Build.BuildFailedException"
                    : exceptionType.IsInstanceOfType(exception.InnerException), message);
            return;
        }
        throw new InvalidOperationException("FAILED: accepted " + message);
    }
}
