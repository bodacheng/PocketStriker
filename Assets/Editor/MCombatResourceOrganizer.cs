using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.U2D;

public static class MCombatResourceOrganizer
{
    private const string CommonSettingPath = "Assets/Setting/CommonSetting.asset";
    private const string PublicGroupName = "Public";
    private const string PublicLabel = "public";
    private const string SkillConfigCsvPath = "Assets/ExternalAssets/Config/mst_skill.csv";
    private const string SkillNameCsvPath = "Assets/ExternalAssets/Config/skill_name.csv";
    private const string SkillIconRoot = "Assets/ExternalAssets/SkillIcon";
    private const string SkillAnimationRoot = "Assets/ExternalAssets/Animations";
    private const string SkillIconGroupName = "SkillIcon";
    private const string SkillIconLabel = "skill_icon";
    private const string SkillAnimGroupName = "SkillAnim";
    private const string SkillAnimLabel = "skill_anim";
    private const string RecoveredRoot = "Assets/MCombatRecovered";
    private static readonly HashSet<string> SupportedSkillIconExtensions = new HashSet<string>(
        new[] { ".png", ".jpg", ".jpeg", ".psd", ".tif", ".tiff" },
        StringComparer.OrdinalIgnoreCase);

    private enum GroupScope
    {
        Unknown,
        Local,
        Remote
    }

    private sealed class DependencyUsage
    {
        public string AssetPath;
        public readonly HashSet<string> RemoteGroups = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> LocalGroups = new HashSet<string>(StringComparer.Ordinal);
    }

    private sealed class SkillRecord
    {
        public string Id;
        public string RealName;
        public string Type;
        public string AttackType;
    }

    [MenuItem("MCombat/AddressableAssets/Organize Project Resources", priority = 60)]
    public static void OrganizeProjectResources()
    {
        ExecuteOrganization(logToConsole: true);
    }

    public static void BatchOrganizeProjectResources()
    {
        ExecuteOrganization(logToConsole: true);
    }

    private static void ExecuteOrganization(bool logToConsole)
    {
        var changes = new List<string>();

        try
        {
            SyncSharedPublicDependencies(changes);
            SyncSkillResources(changes);
            AuditRecoveredResources(changes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MCombatResourceOrganizer] Failed: {e}");
            throw;
        }

        if (!logToConsole)
        {
            return;
        }

        if (changes.Count == 0)
        {
            Debug.Log("[MCombatResourceOrganizer] No shared public dependencies required updates.");
            return;
        }

        Debug.Log("[MCombatResourceOrganizer]\n" + string.Join("\n", changes));
    }

    private static void SyncSkillResources(List<string> changes)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            throw new InvalidOperationException("AddressableAssetSettings could not be found.");
        }

        var skillRecords = LoadSkillRecords();
        if (skillRecords.Count == 0)
        {
            changes.Add("Skill resource sync skipped: mst_skill.csv has no records.");
            return;
        }

        SyncSkillIconEntries(settings, skillRecords, changes);
        SyncSkillAnimationEntries(settings, skillRecords, changes);
        AuditSkillNameCoverage(skillRecords, changes);
        AuditLegacyAttackTypes(skillRecords, changes);
    }

    private static List<SkillRecord> LoadSkillRecords()
    {
        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(SkillConfigCsvPath);
        if (csv == null)
        {
            throw new FileNotFoundException($"Skill config CSV not found: {SkillConfigCsvPath}");
        }

        var grid = CsvParser2.Parse(csv.text);
        if (grid.Length == 0)
        {
            return new List<SkillRecord>();
        }

        var header = grid[0];
        var idIndex = FindColumnIndex(header, "id", "RECORD_ID");
        var realNameIndex = FindColumnIndex(header, "REAL_NAME");
        var typeIndex = FindColumnIndex(header, "USEABLE_MONSTER_TYPE", "TYPE");
        var attackTypeIndex = FindColumnIndex(header, "ATTACK_TYPE");
        if (idIndex < 0 || realNameIndex < 0 || typeIndex < 0 || attackTypeIndex < 0)
        {
            throw new InvalidOperationException("mst_skill.csv missing one or more required columns.");
        }

        var result = new List<SkillRecord>();
        for (var i = 1; i < grid.Length; i++)
        {
            var row = grid[i];
            if (row.Length <= Math.Max(Math.Max(idIndex, realNameIndex), Math.Max(typeIndex, attackTypeIndex)))
            {
                continue;
            }

            var id = row[idIndex].Trim();
            var realName = row[realNameIndex].Trim();
            var type = row[typeIndex].Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(realName) || string.IsNullOrEmpty(type))
            {
                continue;
            }

            result.Add(new SkillRecord
            {
                Id = id,
                RealName = realName,
                Type = type,
                AttackType = row[attackTypeIndex].Trim()
            });
        }

        return result;
    }

    private static int FindColumnIndex(IReadOnlyList<string> header, params string[] names)
    {
        for (var i = 0; i < header.Count; i++)
        {
            for (var j = 0; j < names.Length; j++)
            {
                if (string.Equals(header[i], names[j], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static void SyncSkillIconEntries(
        AddressableAssetSettings settings,
        IReadOnlyCollection<SkillRecord> skillRecords,
        List<string> changes)
    {
        var group = settings.FindGroup(SkillIconGroupName);
        if (group == null)
        {
            throw new InvalidOperationException($"Addressables group not found: {SkillIconGroupName}");
        }

        settings.AddLabel(SkillIconLabel);

        var validSkillIds = new HashSet<string>(skillRecords.Select(record => record.Id), StringComparer.Ordinal);
        var iconAssetPaths = CollectSkillIconAssetPaths();

        foreach (var entry in group.entries.ToArray())
        {
            var removeEntry =
                string.IsNullOrEmpty(entry.AssetPath) ||
                !File.Exists(entry.AssetPath) ||
                !validSkillIds.Contains(entry.address);
            if (!removeEntry)
            {
                continue;
            }

            settings.RemoveAssetEntry(entry.guid, false);
            changes.Add($"Skill icon remove stale entry: {entry.address} ({entry.AssetPath})");
        }

        foreach (var record in skillRecords.OrderBy(record => int.Parse(record.Id)))
        {
            if (!iconAssetPaths.TryGetValue(record.Id, out var assetPath))
            {
                changes.Add($"Skill icon missing: {record.Id} ({record.RealName})");
                continue;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, group, false, true);
            if (!string.Equals(entry.address, record.Id, StringComparison.Ordinal))
            {
                entry.address = record.Id;
                changes.Add($"Skill icon address: {record.Id} <- {assetPath}");
            }

            entry.SetLabel(SkillIconLabel, true, true);
        }

        var extraIcons = iconAssetPaths.Keys
            .Where(id => !validSkillIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (extraIcons.Length > 0)
        {
            changes.Add($"Skill icon extra files: {extraIcons.Length} ({string.Join(", ", extraIcons.Take(8))})");
        }
    }

    private static Dictionary<string, string> CollectSkillIconAssetPaths()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { SkillIconRoot }))
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                continue;
            }

            var extension = Path.GetExtension(assetPath);
            if (!SupportedSkillIconExtensions.Contains(extension))
            {
                continue;
            }

            var id = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(id) || id.Any(ch => !char.IsDigit(ch)))
            {
                continue;
            }

            if (!result.ContainsKey(id))
            {
                result.Add(id, assetPath);
            }
        }

        return result;
    }

    private static void SyncSkillAnimationEntries(
        AddressableAssetSettings settings,
        IReadOnlyCollection<SkillRecord> skillRecords,
        List<string> changes)
    {
        var group = settings.FindGroup(SkillAnimGroupName);
        if (group == null)
        {
            throw new InvalidOperationException($"Addressables group not found: {SkillAnimGroupName}");
        }

        settings.AddLabel(SkillAnimLabel);

        var groupedByType = skillRecords
            .GroupBy(record => record.Type, StringComparer.Ordinal)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList(), StringComparer.Ordinal);

        foreach (var entry in group.entries.ToArray())
        {
            var typeFromAddress = GetSkillAnimationType(entry.address);
            var removeEntry =
                string.IsNullOrEmpty(typeFromAddress) ||
                !groupedByType.ContainsKey(typeFromAddress) ||
                string.IsNullOrEmpty(entry.AssetPath) ||
                !AssetDatabase.IsValidFolder(entry.AssetPath);
            if (!removeEntry)
            {
                continue;
            }

            settings.RemoveAssetEntry(entry.guid, false);
            changes.Add($"Skill anim remove stale entry: {entry.address} ({entry.AssetPath})");
        }

        foreach (var pair in groupedByType.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var type = pair.Key;
            var folderPath = $"{SkillAnimationRoot}/{type}/skill";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                changes.Add($"Skill anim folder missing: {type}/skill ({pair.Value.Count} skills)");
                continue;
            }

            var guid = AssetDatabase.AssetPathToGUID(folderPath);
            var entry = settings.CreateOrMoveEntry(guid, group, false, true);
            var address = $"{type}/skill";
            if (!string.Equals(entry.address, address, StringComparison.Ordinal))
            {
                entry.address = address;
                changes.Add($"Skill anim address: {address}");
            }

            entry.SetLabel(SkillAnimLabel, true, true);
            AuditSkillAnimationCoverage(type, folderPath, pair.Value, changes);
        }
    }

    private static string GetSkillAnimationType(string address)
    {
        if (string.IsNullOrEmpty(address) || !address.EndsWith("/skill", StringComparison.Ordinal))
        {
            return null;
        }

        return address.Substring(0, address.Length - "/skill".Length);
    }

    private static void AuditSkillAnimationCoverage(
        string type,
        string folderPath,
        IReadOnlyCollection<SkillRecord> skillRecords,
        List<string> changes)
    {
        var animationNames = new HashSet<string>(
            AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => Path.GetFileNameWithoutExtension(path)),
            StringComparer.Ordinal);

        var expectedNames = new HashSet<string>(
            skillRecords.Select(record => record.RealName),
            StringComparer.Ordinal);

        var missing = expectedNames
            .Where(name => !animationNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            changes.Add($"Skill anim missing clips: {type} {missing.Length} ({string.Join(", ", missing.Take(8))})");
        }

        var extra = animationNames
            .Where(name => !expectedNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (extra.Length > 0)
        {
            changes.Add($"Skill anim extra clips: {type} {extra.Length} ({string.Join(", ", extra.Take(8))})");
        }
    }

    private static void AuditSkillNameCoverage(IReadOnlyCollection<SkillRecord> skillRecords, List<string> changes)
    {
        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(SkillNameCsvPath);
        if (csv == null)
        {
            changes.Add($"Skill name CSV missing: {SkillNameCsvPath}");
            return;
        }

        var grid = CsvParser2.Parse(csv.text);
        if (grid.Length == 0)
        {
            changes.Add("Skill name CSV is empty.");
            return;
        }

        var idIndex = FindColumnIndex(grid[0], "RECORD_ID", "id");
        if (idIndex < 0)
        {
            changes.Add("Skill name CSV missing RECORD_ID column.");
            return;
        }

        var localizedIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < grid.Length; i++)
        {
            if (grid[i].Length <= idIndex)
            {
                continue;
            }

            var id = grid[i][idIndex].Trim();
            if (!string.IsNullOrEmpty(id))
            {
                localizedIds.Add(id);
            }
        }

        var missing = skillRecords
            .Where(record => !localizedIds.Contains(record.Id))
            .OrderBy(record => int.Parse(record.Id))
            .ToArray();
        if (missing.Length > 0)
        {
            changes.Add(
                $"Skill name missing rows: {missing.Length} ({string.Join(", ", missing.Take(6).Select(record => $"{record.Id}:{record.RealName}"))})");
        }
    }

    private static void AuditLegacyAttackTypes(IReadOnlyCollection<SkillRecord> skillRecords, List<string> changes)
    {
        var legacyRecords = skillRecords
            .Select(record => new
            {
                Record = record,
                Normalized = SkillConfigTable.NormalizeAttackType(record.AttackType)
            })
            .Where(x => x.Normalized == null || !string.Equals(x.Record.AttackType, x.Normalized, StringComparison.Ordinal))
            .OrderBy(x => int.Parse(x.Record.Id))
            .ToArray();
        if (legacyRecords.Length == 0)
        {
            return;
        }

        changes.Add(
            $"Skill attack-type legacy rows: {legacyRecords.Length} ({string.Join(", ", legacyRecords.Select(x => $"{x.Record.Id}:{x.Record.AttackType}->{x.Normalized ?? "INVALID"}"))})");
    }

    private static void AuditRecoveredResources(List<string> changes)
    {
        if (!AssetDatabase.IsValidFolder(RecoveredRoot))
        {
            return;
        }

        var recoveredAssetPaths = CollectFileAssetPaths(RecoveredRoot);
        if (recoveredAssetPaths.Count == 0)
        {
            changes.Add("Recovered MCombat assets: none found.");
            return;
        }

        var projectAssetPaths = CollectFileAssetPaths("Assets")
            .Where(path => !path.StartsWith(RecoveredRoot + "/", StringComparison.Ordinal))
            .ToArray();

        var assetsByComparableKey = projectAssetPaths
            .Select(path => new { Path = path, Key = BuildComparableAssetKey(path) })
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Path).ToArray(), StringComparer.Ordinal);

        var matched = new List<string>();
        var missing = new List<string>();
        var ambiguous = new List<string>();

        foreach (var recoveredAssetPath in recoveredAssetPaths)
        {
            var key = BuildComparableAssetKey(recoveredAssetPath);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!assetsByComparableKey.TryGetValue(key, out var matches))
            {
                missing.Add(recoveredAssetPath);
                continue;
            }

            if (matches.Length > 1)
            {
                ambiguous.Add($"{Path.GetFileName(recoveredAssetPath)} -> {string.Join(" | ", matches.Take(3))}");
                continue;
            }

            matched.Add($"{Path.GetFileName(recoveredAssetPath)} -> {matches[0]}");
        }

        changes.Add($"Recovered MCombat assets covered by current project: {matched.Count}/{recoveredAssetPaths.Count}");

        if (ambiguous.Count > 0)
        {
            changes.Add($"Recovered MCombat assets with ambiguous matches: {ambiguous.Count} ({string.Join(", ", ambiguous.Take(6))})");
        }

        if (missing.Count > 0)
        {
            changes.Add($"Recovered MCombat assets still unmatched: {missing.Count} ({string.Join(", ", missing.Take(8).Select(Path.GetFileName))})");
        }
    }

    private static void SyncSharedPublicDependencies(List<string> changes)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            throw new InvalidOperationException("AddressableAssetSettings could not be found.");
        }

        var groups = settings.groups
            .Where(group => group != null && !string.Equals(group.Name, PublicGroupName, StringComparison.Ordinal))
            .Where(group => group.GetSchema<BundledAssetGroupSchema>() != null)
            .ToList();

        var remoteTemplateGroup = groups.FirstOrDefault(group => GetGroupScope(settings, group) == GroupScope.Remote);
        if (remoteTemplateGroup == null)
        {
            throw new InvalidOperationException("A remote Addressables group is required to create the Public group.");
        }

        var protectedGuids = BuildProtectedGuidSet(groups);
        var dependencyUsage = CollectDependencyUsage(settings, groups, protectedGuids);
        var candidateGuids = dependencyUsage
            .Where(pair => pair.Value.RemoteGroups.Count > 1 && pair.Value.LocalGroups.Count == 0)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

        var crossScopeSharedCount = dependencyUsage.Count(pair =>
            pair.Value.RemoteGroups.Count > 1 &&
            pair.Value.LocalGroups.Count > 0);
        if (crossScopeSharedCount > 0)
        {
            changes.Add($"Skip mixed-scope shared dependencies: {crossScopeSharedCount}");
        }

        var publicGroup = settings.FindGroup(PublicGroupName);
        if (publicGroup == null && candidateGuids.Count == 0)
        {
            return;
        }

        publicGroup = EnsurePublicGroup(settings, publicGroup, remoteTemplateGroup, changes);
        RemoveStalePublicEntries(settings, publicGroup, candidateGuids, changes);

        if (candidateGuids.Count == 0)
        {
            return;
        }

        settings.AddLabel(PublicLabel);

        foreach (var pair in dependencyUsage
                     .Where(pair => candidateGuids.Contains(pair.Key))
                     .OrderBy(pair => pair.Value.AssetPath, StringComparer.OrdinalIgnoreCase))
        {
            var guid = pair.Key;
            var usage = pair.Value;
            var address = BuildPublicAddress(usage.AssetPath);

            var existingEntry = settings.FindAssetEntry(guid);
            var existedInPublic = existingEntry != null && existingEntry.parentGroup == publicGroup;
            var entry = settings.CreateOrMoveEntry(guid, publicGroup, false, true);

            if (!string.Equals(entry.address, address, StringComparison.Ordinal))
            {
                entry.address = address;
                changes.Add($"Public address: {address}");
            }

            entry.SetLabel(PublicLabel, true, true);

            if (!existedInPublic)
            {
                changes.Add(
                    $"Public add: {usage.AssetPath} <- {string.Join(", ", usage.RemoteGroups.OrderBy(name => name, StringComparer.Ordinal))}");
            }
        }

        EnsureDownloadLabel(PublicLabel, changes);
        EditorUtility.SetDirty(settings);
    }

    private static Dictionary<string, DependencyUsage> CollectDependencyUsage(
        AddressableAssetSettings settings,
        IEnumerable<AddressableAssetGroup> groups,
        ISet<string> protectedGuids)
    {
        var usageMap = new Dictionary<string, DependencyUsage>(StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var scope = GetGroupScope(settings, group);
            if (scope == GroupScope.Unknown)
            {
                continue;
            }

            foreach (var rootAssetPath in CollectGroupAssetPaths(group))
            {
                foreach (var dependencyPath in AssetDatabase.GetDependencies(rootAssetPath, true))
                {
                    if (!ShouldConsiderDependency(rootAssetPath, dependencyPath))
                    {
                        continue;
                    }

                    var dependencyGuid = AssetDatabase.AssetPathToGUID(dependencyPath);
                    if (string.IsNullOrEmpty(dependencyGuid) || protectedGuids.Contains(dependencyGuid))
                    {
                        continue;
                    }

                    if (!IsSupportedSharedDependency(dependencyPath))
                    {
                        continue;
                    }

                    if (!usageMap.TryGetValue(dependencyGuid, out var usage))
                    {
                        usage = new DependencyUsage
                        {
                            AssetPath = dependencyPath
                        };
                        usageMap.Add(dependencyGuid, usage);
                    }

                    if (scope == GroupScope.Remote)
                    {
                        usage.RemoteGroups.Add(group.Name);
                    }
                    else
                    {
                        usage.LocalGroups.Add(group.Name);
                    }
                }
            }
        }

        return usageMap;
    }

    private static HashSet<string> BuildProtectedGuidSet(IEnumerable<AddressableAssetGroup> groups)
    {
        var protectedGuids = new HashSet<string>(StringComparer.Ordinal);
        var gatheredEntries = new List<AddressableAssetEntry>();

        foreach (var group in groups)
        {
            gatheredEntries.Clear();
            group.GatherAllAssets(gatheredEntries, true, true, false);

            foreach (var entry in gatheredEntries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.guid))
                {
                    continue;
                }

                if (entry.ParentEntry != null && entry.ParentEntry.IsFolder)
                {
                    continue;
                }

                protectedGuids.Add(entry.guid);
            }
        }

        return protectedGuids;
    }

    private static IEnumerable<string> CollectGroupAssetPaths(AddressableAssetGroup group)
    {
        var gatheredEntries = new List<AddressableAssetEntry>();
        group.GatherAllAssets(gatheredEntries, true, true, false);

        return gatheredEntries
            .Select(entry => entry?.AssetPath)
            .Where(IsScannableAssetPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AddressableAssetGroup EnsurePublicGroup(
        AddressableAssetSettings settings,
        AddressableAssetGroup publicGroup,
        AddressableAssetGroup templateGroup,
        List<string> changes)
    {
        if (publicGroup == null)
        {
            var schemaTypes = templateGroup.Schemas
                .Where(schema => schema != null)
                .Select(schema => schema.GetType())
                .Distinct()
                .ToArray();

            publicGroup = settings.CreateGroup(
                PublicGroupName,
                false,
                false,
                true,
                templateGroup.Schemas.ToList(),
                schemaTypes);
            changes.Add("Create Addressables group: Public");
        }

        var publicSchema = publicGroup.GetSchema<BundledAssetGroupSchema>();
        var templateSchema = templateGroup.GetSchema<BundledAssetGroupSchema>();
        if (publicSchema == null || templateSchema == null)
        {
            return publicGroup;
        }

        var schemaChanged = false;
        if (!string.Equals(publicSchema.BuildPath.Id, templateSchema.BuildPath.Id, StringComparison.Ordinal))
        {
            publicSchema.BuildPath.SetVariableById(settings, templateSchema.BuildPath.Id);
            schemaChanged = true;
        }

        if (!string.Equals(publicSchema.LoadPath.Id, templateSchema.LoadPath.Id, StringComparison.Ordinal))
        {
            publicSchema.LoadPath.SetVariableById(settings, templateSchema.LoadPath.Id);
            schemaChanged = true;
        }

        if (publicSchema.UseUnityWebRequestForLocalBundles != templateSchema.UseUnityWebRequestForLocalBundles)
        {
            publicSchema.UseUnityWebRequestForLocalBundles = templateSchema.UseUnityWebRequestForLocalBundles;
            schemaChanged = true;
        }

        if (schemaChanged)
        {
            EditorUtility.SetDirty(publicSchema);
            changes.Add("Sync Public group schema to remote template");
        }

        return publicGroup;
    }

    private static void RemoveStalePublicEntries(
        AddressableAssetSettings settings,
        AddressableAssetGroup publicGroup,
        ISet<string> candidateGuids,
        List<string> changes)
    {
        foreach (var entry in publicGroup.entries.ToArray())
        {
            if (candidateGuids.Contains(entry.guid))
            {
                continue;
            }

            var assetPath = entry.AssetPath;
            settings.RemoveAssetEntry(entry.guid, false);
            changes.Add($"Public remove: {assetPath}");
        }
    }

    private static GroupScope GetGroupScope(AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null || !schema.IncludeInBuild)
        {
            return GroupScope.Unknown;
        }

        var loadPath = schema.LoadPath.GetValue(settings);
        return ResourceManagerConfig.IsPathRemote(loadPath) ? GroupScope.Remote : GroupScope.Local;
    }

    private static bool IsSupportedSharedDependency(string assetPath)
    {
        var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        if (mainType == null)
        {
            return false;
        }

        return typeof(Material).IsAssignableFrom(mainType) ||
               typeof(Texture).IsAssignableFrom(mainType) ||
               typeof(SpriteAtlas).IsAssignableFrom(mainType);
    }

    private static bool IsScannableAssetPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
               !AssetDatabase.IsValidFolder(assetPath);
    }

    private static bool ShouldConsiderDependency(string rootAssetPath, string dependencyPath)
    {
        return !string.Equals(rootAssetPath, dependencyPath, StringComparison.Ordinal) &&
               !string.IsNullOrEmpty(dependencyPath) &&
               dependencyPath.StartsWith("Assets/", StringComparison.Ordinal) &&
               !AssetDatabase.IsValidFolder(dependencyPath);
    }

    private static List<string> CollectFileAssetPaths(string rootPath)
    {
        return AssetDatabase.FindAssets(string.Empty, new[] { rootPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsScannableAssetPath)
            .ToList();
    }

    private static string BuildComparableAssetKey(string assetPath)
    {
        var extension = Path.GetExtension(assetPath);
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        var stem = Path.GetFileNameWithoutExtension(assetPath);
        var hashSuffixIndex = stem.IndexOf("__", StringComparison.Ordinal);
        if (hashSuffixIndex >= 0)
        {
            stem = stem.Substring(0, hashSuffixIndex);
        }

        stem = stem.Trim();
        if (string.IsNullOrEmpty(stem))
        {
            return null;
        }

        return $"{stem.ToLowerInvariant()}|{extension.ToLowerInvariant()}";
    }

    private static string BuildPublicAddress(string assetPath)
    {
        return "public/" + assetPath.Substring("Assets/".Length);
    }

    private static void EnsureDownloadLabel(string label, List<string> changes)
    {
        var commonSetting = AssetDatabase.LoadAssetAtPath<CommonSetting>(CommonSettingPath);
        if (commonSetting == null)
        {
            return;
        }

        var serializedObject = new SerializedObject(commonSetting);
        var labels = serializedObject.FindProperty("downLoadLabels");
        if (labels == null || ContainsString(labels, label))
        {
            return;
        }

        labels.InsertArrayElementAtIndex(labels.arraySize);
        labels.GetArrayElementAtIndex(labels.arraySize - 1).stringValue = label;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(commonSetting);
        changes.Add($"CommonSetting download label: {label}");
    }

    private static bool ContainsString(SerializedProperty arrayProperty, string value)
    {
        for (var i = 0; i < arrayProperty.arraySize; i++)
        {
            if (arrayProperty.GetArrayElementAtIndex(i).stringValue == value)
            {
                return true;
            }
        }

        return false;
    }
}
