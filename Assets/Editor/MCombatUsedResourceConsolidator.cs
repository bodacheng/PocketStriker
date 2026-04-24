using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.U2D;

public static class MCombatUsedResourceConsolidator
{
    private const string OrganizedRoot = "Assets/OrganizedResources";
    private const string TargetRoot = OrganizedRoot + "/InUse";
    private const string UnusedRoot = OrganizedRoot + "/Unused";
    private const string LegacyTargetRoot = "Assets/UsedResources";
    private const string LegacyUnusedRoot = "Assets/UnusedResources";
    private const string ReportPath = TargetRoot + "/in_use_resource_report.tsv";
    private const string UnusedReportPath = UnusedRoot + "/unused_resource_report.tsv";

    private static readonly string[] ExcludedPathPrefixes =
    {
        TargetRoot + "/",
        UnusedRoot + "/",
        LegacyTargetRoot + "/",
        LegacyUnusedRoot + "/",
        "Assets/AddressableAssetsData/",
        "Assets/Editor/",
        "Assets/Editor Default Resources/",
        "Assets/Plugins/",
        "Assets/AppCenter/",
        "Assets/AppleAuth/",
        "Assets/AppleAuthSample/",
        "Assets/ExternalDependencyManager/",
        "Assets/GoogleMobileAds/",
        "Assets/IronSource/",
        "Assets/PlayFabEditorExtensions/",
        "Assets/PlayFabSDK/",
        "Assets/YamlDotNet/"
    };

    private sealed class MoveRecord
    {
        public string Kind;
        public string Guid;
        public string OldPath;
        public string NewPath;
    }

    [MenuItem("MCombat/Resources/Consolidate Used Visual Resources", priority = 80)]
    public static void ConsolidateUsedVisualResources()
    {
        Execute(dryRun: false);
    }

    public static void BatchConsolidateUsedVisualResources()
    {
        Execute(HasCommandLineArg("-dryRun"));
    }

    public static void BatchCleanupGeneratedDuplicateFolders()
    {
        var deletedCount = CleanupEmptyGeneratedDuplicateFolders();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MCombatUsedResourceConsolidator] Cleanup duplicate folders: {deletedCount}");
    }

    public static void BatchNormalizeOrganizedResourceFolders()
    {
        NormalizeOrganizedResourceFolders();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void Execute(bool dryRun)
    {
        var rootPaths = CollectRuntimeRootPaths();
        var dependencyPaths = new HashSet<string>(
            AssetDatabase.GetDependencies(rootPaths.ToArray(), true),
            StringComparer.Ordinal);

        var plannedMoves = dependencyPaths
            .Where(IsMovableUsedResource)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(BuildMoveRecord)
            .Where(record => record != null)
            .ToList();

        if (dryRun)
        {
            LogPlan("[MCombatUsedResourceConsolidator] Dry run", rootPaths.Count, dependencyPaths.Count, plannedMoves);
            return;
        }

        EnsureFolder(TargetRoot);
        foreach (var folderPath in plannedMoves
                     .Select(record => Path.GetDirectoryName(record.NewPath)?.Replace('\\', '/'))
                     .Where(path => !string.IsNullOrEmpty(path))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(path => path.Count(ch => ch == '/')))
        {
            EnsureFolder(folderPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var moved = new List<MoveRecord>();
        var errors = new List<string>();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var record in plannedMoves)
            {
                var moveError = AssetDatabase.MoveAsset(record.OldPath, record.NewPath);
                if (string.IsNullOrEmpty(moveError))
                {
                    moved.Add(record);
                    continue;
                }

                errors.Add($"{record.OldPath} -> {record.NewPath}: {moveError}");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        WriteReport(moved);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LogPlan("[MCombatUsedResourceConsolidator] Completed", rootPaths.Count, dependencyPaths.Count, moved);
        if (errors.Count > 0)
        {
            Debug.LogWarning("[MCombatUsedResourceConsolidator] Move errors:\n" + string.Join("\n", errors.Take(50)));
        }
    }

    private static HashSet<string> CollectRuntimeRootPaths()
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && IsFileAssetPath(scene.path))
            {
                roots.Add(scene.path);
            }
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            foreach (var group in settings.groups.Where(group => group != null))
            {
                foreach (var assetPath in CollectGroupAssetPaths(group))
                {
                    roots.Add(assetPath);
                }
            }
        }

        foreach (var assetPath in CollectAllAssetPaths().Where(IsUnderResourcesFolder))
        {
            roots.Add(assetPath);
        }

        return roots;
    }

    private static IEnumerable<string> CollectGroupAssetPaths(AddressableAssetGroup group)
    {
        var gatheredEntries = new List<AddressableAssetEntry>();
        group.GatherAllAssets(gatheredEntries, true, true, false);

        return gatheredEntries
            .Select(entry => entry?.AssetPath)
            .Where(IsFileAssetPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> CollectAllAssetPaths()
    {
        return AssetDatabase.FindAssets(string.Empty, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsFileAssetPath);
    }

    private static bool IsMovableUsedResource(string assetPath)
    {
        if (!IsFileAssetPath(assetPath))
        {
            return false;
        }

        if (ExcludedPathPrefixes.Any(prefix => assetPath.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (IsUnderResourcesFolder(assetPath) || IsUnderEditorFolder(assetPath))
        {
            return false;
        }

        var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        return mainType != null &&
               (typeof(Material).IsAssignableFrom(mainType) ||
                typeof(Texture).IsAssignableFrom(mainType) ||
                typeof(SpriteAtlas).IsAssignableFrom(mainType));
    }

    private static MoveRecord BuildMoveRecord(string oldPath)
    {
        var guid = AssetDatabase.AssetPathToGUID(oldPath);
        if (string.IsNullOrEmpty(guid))
        {
            return null;
        }

        var newPath = BuildTargetPath(oldPath, guid);
        if (string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            return null;
        }

        return new MoveRecord
        {
            Kind = GetResourceKind(oldPath),
            Guid = guid,
            OldPath = oldPath,
            NewPath = newPath
        };
    }

    private static string BuildTargetPath(string oldPath, string guid)
    {
        var relativePath = oldPath.Substring("Assets/".Length);
        var targetPath = $"{TargetRoot}/{relativePath}";
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            return targetPath;
        }

        if (AssetDatabase.AssetPathToGUID(targetPath) == guid)
        {
            return targetPath;
        }

        var directory = Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        return $"{directory}/{fileName}__{guid.Substring(0, 8)}{extension}";
    }

    private static string GetResourceKind(string assetPath)
    {
        var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        if (mainType != null && typeof(Material).IsAssignableFrom(mainType))
        {
            return "Material";
        }

        if (mainType != null && typeof(SpriteAtlas).IsAssignableFrom(mainType))
        {
            return "SpriteAtlas";
        }

        return "Texture";
    }

    private static bool IsFileAssetPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
               !AssetDatabase.IsValidFolder(assetPath);
    }

    private static bool IsUnderResourcesFolder(string assetPath)
    {
        return assetPath.StartsWith("Assets/Resources/", StringComparison.Ordinal) ||
               assetPath.Contains("/Resources/", StringComparison.Ordinal);
    }

    private static bool IsUnderEditorFolder(string assetPath)
    {
        return assetPath.StartsWith("Assets/Editor/", StringComparison.Ordinal) ||
               assetPath.Contains("/Editor/", StringComparison.Ordinal);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        EnsureFolder(parent);

        var folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void WriteReport(IReadOnlyCollection<MoveRecord> moved)
    {
        var lines = new List<string> { "kind\tguid\told_path\tnew_path" };
        lines.AddRange(moved.Select(record => $"{record.Kind}\t{record.Guid}\t{record.OldPath}\t{record.NewPath}"));
        File.WriteAllLines(ReportPath, lines);
    }

    private static void LogPlan(string prefix, int rootCount, int dependencyCount, IReadOnlyCollection<MoveRecord> records)
    {
        var byKind = records
            .GroupBy(record => record.Kind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {group.Count()}");

        Debug.Log($"{prefix}: roots={rootCount}, dependencies={dependencyCount}, moves={records.Count} ({string.Join(", ", byKind)})");
    }

    private static bool HasCommandLineArg(string arg)
    {
        return Environment.GetCommandLineArgs().Any(value => string.Equals(value, arg, StringComparison.Ordinal));
    }

    private static void NormalizeOrganizedResourceFolders()
    {
        EnsureFolder(OrganizedRoot);

        MoveAssetIfNeeded(LegacyTargetRoot, TargetRoot);
        MoveAssetIfNeeded(LegacyUnusedRoot, UnusedRoot);
        MoveAssetIfNeeded(TargetRoot + "/used_resource_report.tsv", ReportPath);
        MoveAssetIfNeeded(UnusedRoot + "/unused_assets_report.tsv", UnusedReportPath);

        RewriteReportPathPrefix(ReportPath, LegacyTargetRoot, TargetRoot);
        RewriteReportPathPrefix(UnusedReportPath, LegacyUnusedRoot, UnusedRoot);

        Debug.Log($"[MCombatUsedResourceConsolidator] Organized resources normalized under {OrganizedRoot}");
    }

    private static void MoveAssetIfNeeded(string oldPath, string newPath)
    {
        if (!AssetDatabase.IsValidFolder(oldPath) && !File.Exists(oldPath))
        {
            return;
        }

        if (AssetDatabase.IsValidFolder(newPath) || File.Exists(newPath))
        {
            return;
        }

        EnsureFolder(Path.GetDirectoryName(newPath)?.Replace('\\', '/'));
        var error = AssetDatabase.MoveAsset(oldPath, newPath);
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"Could not move {oldPath} to {newPath}: {error}");
        }
    }

    private static void RewriteReportPathPrefix(string reportPath, string oldPrefix, string newPrefix)
    {
        if (!File.Exists(reportPath))
        {
            return;
        }

        var text = File.ReadAllText(reportPath);
        var updated = text.Replace(oldPrefix, newPrefix);
        if (!string.Equals(text, updated, StringComparison.Ordinal))
        {
            File.WriteAllText(reportPath, updated);
        }
    }

    private static int CleanupEmptyGeneratedDuplicateFolders()
    {
        if (!Directory.Exists(TargetRoot))
        {
            return 0;
        }

        var deletedCount = 0;
        var folders = Directory.GetDirectories(TargetRoot, "*", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .OrderByDescending(path => path.Count(ch => ch == '/'))
            .ToArray();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder) ||
                !IsGeneratedDuplicateFolderName(Path.GetFileName(folder)) ||
                Directory.EnumerateFileSystemEntries(folder).Any())
            {
                continue;
            }

            var assetPath = folder;
            if (AssetDatabase.IsValidFolder(assetPath) && AssetDatabase.DeleteAsset(assetPath))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private static bool IsGeneratedDuplicateFolderName(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
        {
            return false;
        }

        var lastSpaceIndex = folderName.LastIndexOf(' ');
        return lastSpaceIndex > 0 &&
               lastSpaceIndex < folderName.Length - 1 &&
               folderName.Skip(lastSpaceIndex + 1).All(char.IsDigit);
    }
}
