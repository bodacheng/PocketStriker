using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

/// <summary>Read the built remote Config bundles with the production loader; no provider/account calls.</summary>
public static class PocketStrikerRemotePromptBundleValidation
{
    [Serializable] sealed class Report
    {
        public bool passed;
        public string error, profile, catalog;
        public int pageCount;
        public List<string> localBundles = new List<string>();
    }

    public static void ValidateBatch() => Run().Forget();

    static async UniTaskVoid Run()
    {
        var report = new Report { profile = "dev" };
        var originalTransform = Addressables.InternalIdTransformFunc;
        var originalLocators = new List<IResourceLocator>();
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-assetProfile") report.profile = args[i + 1];
        try
        {
            if (report.profile != "dev" && report.profile != "release") throw new InvalidOperationException("Unknown resource profile.");
            string directory = Path.GetFullPath("ServerData/" + report.profile + "/v/" + Application.version + "/iOS");
            report.catalog = Path.Combine(directory, "catalog_" + Application.version + ".bin");
            if (!File.Exists(report.catalog)) throw new InvalidOperationException("Build the resource catalog before validating bundles.");
            await Addressables.InitializeAsync().Task;
            originalLocators.AddRange(Addressables.ResourceLocators);
            Addressables.ClearResourceLocators();
            string prefix = "https://mcombat.s3.ap-northeast-1.amazonaws.com/" + report.profile + "/v/" + Application.version + "/iOS/";
            Addressables.InternalIdTransformFunc = location =>
            {
                if (!location.InternalId.StartsWith(prefix, StringComparison.Ordinal)) return location.InternalId;
                string local = Path.Combine(directory, location.InternalId.Substring(prefix.Length));
                if (!File.Exists(local)) throw new InvalidOperationException("Remote bundle is missing from the build: " + Path.GetFileName(local));
                if (!report.localBundles.Contains(Path.GetFileName(local))) report.localBundles.Add(Path.GetFileName(local));
                return local;
            };
            await Addressables.LoadContentCatalogAsync(new Uri(report.catalog).AbsoluteUri, true).Task;
            var snapshot = await PocketStrikerRemoteStoryPrompts.Load(CancellationToken.None);
            string prompt = snapshot.BuildTextPrompt("built-bundle-verification", SystemLanguage.Chinese);
            if (snapshot.PageCount != 1 || !prompt.Contains("warm fairy-tale picture-book storytelling")
                || !prompt.Contains("2D cartoon") || prompt.Contains("Selected subject:") || report.localBundles.Count == 0)
                throw new InvalidOperationException("Built resource prompt/content settings differ from the reviewed config.");
            report.pageCount = snapshot.PageCount;
            report.passed = true;
        }
        catch (Exception error) { report.error = error.ToString(); }
        finally
        {
            Addressables.InternalIdTransformFunc = originalTransform;
            Addressables.ClearResourceLocators();
            foreach (var locator in originalLocators) Addressables.AddResourceLocator(locator);
            Directory.CreateDirectory("Logs/AIStory/RemotePrompts");
            File.WriteAllText("Logs/AIStory/RemotePrompts/bundles-" + report.profile + ".json", JsonUtility.ToJson(report, true));
            Debug.Log("[RemotePromptBundles] " + (report.passed ? "PASS" : "FAIL") + (report.error == null ? "" : ": " + report.error));
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
}
