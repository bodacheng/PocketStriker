using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>Read-only candidate dependency audit; never moves or deletes an asset.</summary>
public static class PocketStrikerResourceCleanupAudit
{
    [Serializable] public sealed class Candidate { public string path, meta, guid, reason; public long bytes; }
    [Serializable] public sealed class Input { public List<Candidate> candidates; }
    [Serializable] public sealed class Finding
    {
        public string path, guid;
        public bool exists, matchingGuid, runtimeDependency, referencedByRetainedAsset, resourcePath;
        public bool addressable;
    }
    [Serializable] public sealed class Report
    {
        public string unityVersion;
        public int allAssetPaths, retainedRoots, runtimeRoots, addressableEntries;
        public List<Finding> candidates = new List<Finding>();
        public List<string> unresolvedRuntimeGuids = new List<string>();
        public List<string> errors = new List<string>();
    }
    public static void Validate()
    {
        string input = Environment.GetEnvironmentVariable("POCKETSTRIKER_CLEANUP_CANDIDATES");
        string output = Environment.GetEnvironmentVariable("POCKETSTRIKER_CLEANUP_REPORT");
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
            throw new ArgumentException("Set candidate input and report output paths.");
        var list = JsonUtility.FromJson<Input>(File.ReadAllText(input)).candidates;
        var candidatePaths = new HashSet<string>(list.Select(c => c.path));
        var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).ToArray();
        var retained = paths.Where(p => !candidatePaths.Contains(p)).ToArray();
        var retainedDependencies = new HashSet<string>(AssetDatabase.GetDependencies(retained, true));
        var roots = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        foreach (var p in paths.Where(p => p.Contains("/Resources/"))) roots.Add(p);
        var entries = new List<AddressableAssetEntry>();
        foreach (var group in AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g != null))
            group.GatherAllAssets(entries, true, true, false);
        foreach (var entry in entries) if (!string.IsNullOrEmpty(entry.AssetPath)) roots.Add(entry.AssetPath);
        var runtime = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        var report = new Report { unityVersion = Application.unityVersion, allAssetPaths = paths.Length,
            retainedRoots = retained.Length, runtimeRoots = roots.Count, addressableEntries = entries.Count };
        var serialized = new HashSet<string> { ".prefab", ".unity", ".mat", ".asset", ".controller", ".overrideController", ".anim", ".spriteatlas" };
        foreach (var path in runtime.Where(p => serialized.Contains(Path.GetExtension(p)) && File.Exists(p)).OrderBy(p => p))
        {
            var text = File.ReadAllText(path);
            if (!text.StartsWith("%YAML")) continue;
            foreach (var guid in Regex.Matches(text, @"guid: ([a-fA-F0-9]{32})").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            {
                // Unity's built-in resources have reserved zero-prefixed IDs.
                if (guid.StartsWith("0000000000000000")) continue;
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    report.unresolvedRuntimeGuids.Add(path + " -> " + guid);
            }
        }
        foreach (var c in list)
        {
            var row = new Finding { path = c.path, guid = c.guid, exists = File.Exists(c.path),
                matchingGuid = AssetDatabase.AssetPathToGUID(c.path) == c.guid,
                runtimeDependency = runtime.Contains(c.path), referencedByRetainedAsset = retainedDependencies.Contains(c.path),
                resourcePath = c.path.Contains("/Resources/"), addressable = entries.Any(e => e.AssetPath == c.path) };
            report.candidates.Add(row);
            if (row.runtimeDependency || row.referencedByRetainedAsset || row.resourcePath || row.addressable)
                report.errors.Add("Retain referenced candidate: " + c.path);
        }
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log("[CleanupAudit] " + list.Count + " candidates; " + report.errors.Count + " referenced; " + paths.Length + " assets inspected.");
    }
}
