using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class PocketStrikerDownloadValidation : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => RequireSettings(AddressableAssetSettingsDefaultObject.Settings);

    public static void RequireSettings(AddressableAssetSettings settings)
    {
        var errors = FindErrors(settings);
        if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
    }

    static List<string> FindErrors(AddressableAssetSettings settings)
    {
        var errors = new List<string>();
        if (settings == null) { errors.Add("Addressables settings are missing."); return errors; }
        if (settings.MaxConcurrentWebRequests != PocketStrikerDownloadPolicy.MaxConcurrentRequests)
            errors.Add("Addressables must allow four concurrent web requests.");
        if (settings.CatalogRequestsTimeout != PocketStrikerDownloadPolicy.RequestTimeoutSeconds ||
            settings.BundleTimeout != PocketStrikerDownloadPolicy.RequestTimeoutSeconds ||
            settings.BundleRetryCount != PocketStrikerDownloadPolicy.BundleRetryCount)
            errors.Add("Addressables must use a 30-second catalog/bundle idle timeout and two bundle retries.");
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) continue;
            if (schema.Timeout != PocketStrikerDownloadPolicy.RequestTimeoutSeconds ||
                schema.RetryCount != PocketStrikerDownloadPolicy.BundleRetryCount)
                errors.Add($"Addressables group {group.Name} must use a 30-second idle timeout and two retries.");
        }
        return errors;
    }

    [Serializable]
    sealed class ValidationReport
    {
        public bool passed;
        public int maxConcurrentRequests;
        public int requestTimeoutSeconds;
        public int bundleRetryCount;
        public string[] errors;
    }

    [MenuItem("PocketStriker/Validation/Download Policy")]
    public static void Validate()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var errors = FindErrors(settings);
        var report = new ValidationReport
        {
            passed = errors.Count == 0,
            maxConcurrentRequests = settings == null ? 0 : settings.MaxConcurrentWebRequests,
            requestTimeoutSeconds = settings == null ? 0 : settings.BundleTimeout,
            bundleRetryCount = settings == null ? 0 : settings.BundleRetryCount,
            errors = errors.ToArray()
        };
        Directory.CreateDirectory("Logs/Revival");
        File.WriteAllText("Logs/Revival/download-policy-report.json", JsonUtility.ToJson(report, true));
        if (!report.passed) throw new BuildFailedException(string.Join("\n", errors));
        Debug.Log("POCKETSTRIKER_DOWNLOAD_POLICY_PASSED: four requests, bounded idle timeouts and retries for every group.");
    }
}
