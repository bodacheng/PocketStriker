using System;
using System.Collections.Generic;
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
            var replace = utility.GetMethod("ReplaceProfileVersion", BindingFlags.NonPublic | BindingFlags.Static);
            var collect = utility.GetMethod("CollectProfileVersionErrors", BindingFlags.NonPublic | BindingFlags.Static);
            if (replace == null || collect == null)
                throw new InvalidOperationException("Import the upgraded project in Unity before running this test.");

            const string yaml = "BuildDev: ServerData/dev/v/3.0.0/\nUploadDev: s3://mcombat/dev/v/3.0.0\nBuildRelease: ServerData/release/v/3.0.0/\nUploadRelease: s3://mcombat/release/v/3.0.0\n";
            var replaced = (string)replace.Invoke(null, new object[] { yaml, "dev", "3.1.0", true, "test" });
            Check(replaced.Contains("UploadDev: s3://mcombat/dev/v/3.1.0\n"), "version bump includes non-final LF-terminated upload URL");
            var crlf = (string)replace.Invoke(null, new object[] { yaml.Replace("\n", "\r\n"), "dev", "3.1.0", true, "test" });
            Check(crlf.Contains("UploadDev: s3://mcombat/dev/v/3.1.0\r\n"), "version bump includes CRLF-terminated upload URL");

            var errors = new List<string>();
            var staleUploadOnly = yaml.Replace("ServerData/dev/v/3.0.0/", "ServerData/dev/v/3.1.0/");
            collect.Invoke(null, new object[] { staleUploadOnly, "dev", "3.1.0", "test", errors });
            Check(errors.Count == 1 && errors[0].Contains("3.0.0"), "validation rejects stale dev upload when build path is current");
            errors.Clear();
            collect.Invoke(null, new object[] { replaced, "dev", "3.1.0", "test", errors });
            Check(errors.Count == 0, "synchronized dev build/upload paths validate");

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
}
