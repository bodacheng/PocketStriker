using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>Local main-adventure Group routing, fighter expansion and asset/catalog integrity.</summary>
public static class PocketStrikerGroupBattleValidation
{
    const string ReportPath = "Logs/Revival/group-battles-report.json";
    static readonly int[] GroupStages = { 4, 7, 14, 21, 30, 34, 36, 43, 49 };

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public int restoredStagesChecked;
        public int countCasesChecked;
        public int retryCasesChecked;
        public int legacyAssetsChecked;
        public int registeredUnitReferences;
        public int registeredSkillReferences;
        public int originalDataComparisons;
        public int[] teamLimits;
        public bool sourceAssetsUnchanged;
        public string scope = "Nine actual main-adventure Group assets and their quest Addressables entries; production ArcadeModeManager.PrepareStage, GangbangInfo count selection/expansion/retry copy, and FightLoad.ConfigureBattleControl. All three authored 24/72/100 per-team options. Legacy 75 Gangbang assets, addresses, GUIDs, labels and bundled schema. Unit/skill references are checked against local master CSV registrations.";
        public string limitation = "Pure stopped-editor data checks: no scene, account, download or combat simulation is started. Remote bundle availability is not checked. Original MComat payload comparison runs only when that local sibling checkout exists; curated stage identity/count/routing checks always run.";
        public List<string> errors = new List<string>();
        public List<string> observations = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Group Battles")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_GROUP_BATTLES_PASSED: nine stages, 27 count cases, 27 retry copies, 75 legacy assets.");
    }

    public static void ValidateBatch()
    {
        var report = Run();
        Debug.Log("[GroupBattles] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(ReportPath));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }

    public static Report Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Group battle validation requires a stopped editor.");
        var report = new Report { unityVersion = Application.unityVersion, sourceAssetsUnchanged = true };
        bool hadOption = PlayerPrefs.HasKey("gangbangCountOption"), hadAuto = PlayerPrefs.HasKey("auto");
        int oldOption = PlayerPrefs.GetInt("gangbangCountOption"), oldAuto = PlayerPrefs.GetInt("auto");
        var oldLimits = new[] { CommonSetting.GangbangModeMaxUnitPerTeam1, CommonSetting.GangbangModeMaxUnitPerTeam2, CommonSetting.GangbangModeMaxUnitPerTeam3 };
        var sourceSnapshots = new Dictionary<string, Tuple<FightInfo, string, string, bool>>();
        try
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Require(settings != null, "Addressables settings are missing.");
            var entries = settings.groups.Where(group => group != null).SelectMany(group => group.entries).ToList();
            var common = AssetDatabase.LoadAssetAtPath<CommonSetting>("Assets/Setting/CommonSetting.asset");
            Require(common != null, "Common settings asset is missing.");
            var serialized = new SerializedObject(common);
            report.teamLimits = Enumerable.Range(1, 3).Select(option => serialized.FindProperty("gangbangModeMaxUnitPerTeam" + option).intValue).ToArray();
            Require(report.teamLimits.SequenceEqual(new[] { 24, 72, 100 }), "Authored Group count options changed from 24/72/100.");
            CommonSetting.GangbangModeMaxUnitPerTeam1 = report.teamLimits[0];
            CommonSetting.GangbangModeMaxUnitPerTeam2 = report.teamLimits[1];
            CommonSetting.GangbangModeMaxUnitPerTeam3 = report.teamLimits[2];
            var unitIds = MasterIds("Assets/ExternalAssets/Config/mst_unit.csv");
            var skillIds = MasterIds("Assets/ExternalAssets/Config/mst_skill.csv");
            PlayerPrefs.SetInt("auto", 0);
            for (int stageIndex = 0; stageIndex < GroupStages.Length; stageIndex++)
            {
                int number = GroupStages[stageIndex];
                var entry = entries.SingleOrDefault(item => item.address == number.ToString() && item.labels.Contains("quest"));
                Require(entry != null, "Main Group quest address/label is missing: " + number);
                var path = entry.AssetPath;
                var source = AssetDatabase.LoadAssetAtPath<GangbangInfo>(path);
                Require(source != null, "Main Group stage does not deserialize as GangbangInfo: " + number);
                sourceSnapshots.Add(path, Tuple.Create((FightInfo)source, File.ReadAllText(path), EditorJsonUtility.ToJson(source), EditorUtility.IsDirty(source)));
                Require(entry.guid == AssetDatabase.AssetPathToGUID(path), "Main Group quest GUID is invalid: " + number);
                Require(source.UnitsData.Count == 4 && source.Team2GroupSet.Count == 4, "Main Group stage lost its four authored troop types: " + number);
                Require(source.Team2GroupSet.Sum(group => group.Count) == (stageIndex < 2 ? 24 : 32), "Main Group stage lost its authored troop distribution: " + number);
                CheckReferences(source, unitIds, skillIds, report);
                CheckOriginalPayload(number, path, report);
                for (int option = 1; option <= 3; option++)
                {
                    PlayerPrefs.SetInt("gangbangCountOption", option);
                    int limit = report.teamLimits[option - 1];
                    Require(GangbangInfo.GetConfiguredTeamLimit() == limit, "Saved count option selects the wrong team limit.");
                    var stage = ArcadeModeManager.PrepareStage(source, number, AdventureModeRules.GroupMode) as GangbangInfo;
                    GangbangInfo retry = null;
                    try
                    {
                        Require(stage != null && stage != source && !EditorUtility.IsPersistent(stage), "Prepared Group stage is not an owned clone.");
                        Require(stage.EventType == FightEventType.Quest && stage.ID == number.ToString() && stage.ArcadeFightMode == AdventureModeRules.GroupMode,
                            "Group mechanism lost adventure routing/rewards.");
                        Require(stage.IsGroupBattle && stage.FightMode == FightMode.Group && !stage.EvolutionMode
                            && stage.team1Mode == TeamMode.MultiRaid && stage.team2Mode == TeamMode.MultiRaid,
                            "Group stage initializes the wrong battle mechanism or team mode.");
                        Require(!stage.AllowsManualUnitControl && stage.ShouldForceAutoBattle && !stage.ShouldRunFirstQuestTutorial,
                            "Group stage has incorrect control/tutorial policy.");
                        Require(stage.UnitsData.All(unit => Mathf.Approximately(unit.level, stage.stageRefLevel)), "Group stage does not apply its reference level.");
                        Require(stage.Team2GroupSet.All(group => source.Team2GroupSet.All(original => !ReferenceEquals(group, original))), "Group troop counts share mutable source entries.");
                        // Supply four local legal player types without account/team-set loading.
                        stage.FightMembers.HeroSets = new MultiDic<int, int, UnitInfo>();
                        for (int index = 0; index < source.UnitsData.Count; index++)
                        {
                            var unit = source.UnitsData[index].DeepCopy(); unit.id = "fixture-player-" + index;
                            stage.FightMembers.HeroSets.Set(0, index, unit);
                        }
                        stage.ApplyTeamLimit(limit);
                        Require(stage.TeamUnitLimit == limit && stage.GetGroupWholeUnitCount(1) == limit && stage.GetGroupWholeUnitCount(2) == limit,
                            "Count option does not allocate exactly " + limit + " per team at stage " + number);
                        stage.ConvertTeamToGangbang();
                        CheckExpanded(stage, limit);
                        var heroes = stage.FightMembers.HeroSets.GetValues(); var enemies = stage.FightMembers.EnemySets.GetValues();
                        var first = enemies[0];
                        stage.ConvertTeamToGangbang();
                        Require(ReferenceEquals(first, stage.FightMembers.EnemySets.GetValues()[0]), "Repeated Group conversion replaces or expands existing fighters.");
                        CheckExpanded(stage, limit);
                        FightLoad.ConfigureBattleControl(stage);
                        Require(stage.Team1Auto && stage.Team2Auto && !stage.RunTutorial, "Saved AUTO=off defeats Group forced-auto initialization.");
                        retry = FightInfo.Copy(stage) as GangbangInfo;
                        Require(retry != null && retry != stage && retry.GroupsExpanded && retry.TeamUnitLimit == limit, "Retry copy loses Group expansion state/count limit.");
                        retry.ConvertTeamToGangbang(); CheckExpanded(retry, limit);
                        Require(retry.EventType == FightEventType.Quest && retry.team1Mode == TeamMode.MultiRaid && retry.team2Mode == TeamMode.MultiRaid
                            && retry.Team1Auto && retry.Team2Auto, "Retry copy loses Group control/adventure initialization.");
                        var retryEnemy = retry.FightMembers.EnemySets.GetValues()[0];
                        Require(!ReferenceEquals(retryEnemy, enemies[0]) && !ReferenceEquals(retryEnemy.set, enemies[0].set), "Retry fighters or skill sets share mutable objects.");
                        retryEnemy.level += 1; retryEnemy.set.a1 = "validation-only";
                        Require(!Mathf.Approximately(retryEnemy.level, enemies[0].level) && enemies[0].set.a1 != "validation-only", "Retry mutation changed the prepared fight.");
                        enemies[0].level += 1; enemies[0].set.a1 = "validation-only";
                        stage.Team2GroupSet[0].Count = 1;
                        Require(Unchanged(sourceSnapshots[path], path), "Prepared mutation changed the authored Group stage.");
                        report.countCasesChecked++; report.retryCasesChecked++;
                    }
                    finally
                    {
                        if (retry != null) UnityEngine.Object.DestroyImmediate(retry);
                        if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                    }
                }
                report.restoredStagesChecked++;
            }
            // Invalid/old preferences consistently fall back to the first option.
            foreach (int option in new[] { -1, 0, 4, 100 })
            {
                PlayerPrefs.SetInt("gangbangCountOption", option);
                Require(GangbangInfo.GetConfiguredTeamLimit() == 24, "Invalid saved count option did not fall back to 24.");
            }
            CheckLegacyAssets(settings, entries, unitIds, skillIds, report, sourceSnapshots);
        }
        catch (Exception exception) { report.errors.Add(exception.GetBaseException().ToString()); }
        finally
        {
            if (hadOption) PlayerPrefs.SetInt("gangbangCountOption", oldOption); else PlayerPrefs.DeleteKey("gangbangCountOption");
            if (hadAuto) PlayerPrefs.SetInt("auto", oldAuto); else PlayerPrefs.DeleteKey("auto");
            CommonSetting.GangbangModeMaxUnitPerTeam1 = oldLimits[0]; CommonSetting.GangbangModeMaxUnitPerTeam2 = oldLimits[1]; CommonSetting.GangbangModeMaxUnitPerTeam3 = oldLimits[2];
            foreach (var snapshot in sourceSnapshots)
                if (!Unchanged(snapshot.Value, snapshot.Key)) { report.sourceAssetsUnchanged = false; report.errors.Add("Source asset changed: " + snapshot.Key); }
            report.passed = report.errors.Count == 0 && report.sourceAssetsUnchanged && report.restoredStagesChecked == 9
                && report.countCasesChecked == 27 && report.retryCasesChecked == 27 && report.legacyAssetsChecked == 75;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        }
        return report;
    }

    static void CheckExpanded(GangbangInfo stage, int limit)
    {
        Require(stage.GroupsExpanded, "Group conversion did not mark the expansion as complete.");
        foreach (var team in new[] { stage.FightMembers.HeroSets.GetValues(), stage.FightMembers.EnemySets.GetValues() })
        {
            Require(team.Count == limit && team.Select(unit => unit.id).Distinct().Count() == limit, "Expanded Group has missing/duplicate fighter identities.");
            Require(team.All(unit => unit != null && unit.set != null), "Expanded Group has an empty fighter/skill set.");
            for (int index = 1; index < team.Count; index++)
                Require(!ReferenceEquals(team[0], team[index]) && !ReferenceEquals(team[0].set, team[index].set), "Expanded troops share mutable unit/skill-set objects.");
        }
    }

    static void CheckReferences(FightInfo source, HashSet<string> unitIds, HashSet<string> skillIds, Report report)
    {
        foreach (var unit in source.UnitsData)
        {
            Require(unit != null && unitIds.Contains(unit.r_id), "Unregistered Group unit: " + unit?.r_id); report.registeredUnitReferences++;
            Require(unit.set != null, "Group unit has no skill set.");
            foreach (var id in new[] { unit.set.a1, unit.set.a2, unit.set.a3, unit.set.b1, unit.set.b2, unit.set.b3, unit.set.c1, unit.set.c2, unit.set.c3 })
            {
                Require(skillIds.Contains(id), "Unregistered Group skill: " + id); report.registeredSkillReferences++;
            }
        }
    }

    static void CheckLegacyAssets(AddressableAssetSettings settings, IList<AddressableAssetEntry> entries, HashSet<string> units,
        HashSet<string> skills, Report report, IDictionary<string, Tuple<FightInfo, string, string, bool>> snapshots)
    {
        var group = settings.FindGroup("GangbangStage"); Require(group != null, "Legacy GangbangStage group is missing.");
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        Require(schema != null && schema.IncludeInBuild && schema.IncludeAddressInCatalog && schema.IncludeGUIDInCatalog && schema.IncludeLabelsInCatalog,
            "Legacy Group bundles/catalog are excluded from builds.");
        var published = group.entries.OrderBy(entry => int.Parse(entry.address)).ToArray();
        Require(published.Length == 75 && published.Select(entry => int.Parse(entry.address)).SequenceEqual(Enumerable.Range(1, 75)), "Legacy Group stage addresses are incomplete.");
        foreach (var entry in published)
        {
            Require(entry.labels.Contains("quest_gangbang") && entry.guid == AssetDatabase.AssetPathToGUID(entry.AssetPath), "Legacy Group label/GUID mismatch: " + entry.address);
            Require(entry.AssetPath == "Assets/ExternalAssets/Gangbang/" + entry.address + ".asset", "Legacy Group address points at the wrong resource.");
            var asset = AssetDatabase.LoadAssetAtPath<GangbangInfo>(entry.AssetPath); Require(asset != null, "Legacy Group asset is missing: " + entry.address);
            snapshots.Add(entry.AssetPath, Tuple.Create((FightInfo)asset, File.ReadAllText(entry.AssetPath), EditorJsonUtility.ToJson(asset), EditorUtility.IsDirty(asset)));
            CheckReferences(asset, units, skills, report); report.legacyAssetsChecked++;
        }
        report.observations.Add("Legacy Group source/catalog are intact. Their remote bundle load path is " + schema.LoadPath.GetValue(settings) + ".");
    }

    static HashSet<string> MasterIds(string path) => new HashSet<string>(CsvParser2.Parse(File.ReadAllText(path)).Skip(1).Where(row => row.Length > 0 && !string.IsNullOrEmpty(row[0])).Select(row => row[0]));
    static bool Unchanged(Tuple<FightInfo, string, string, bool> snapshot, string path) => snapshot.Item2 == File.ReadAllText(path)
        && snapshot.Item3 == EditorJsonUtility.ToJson(snapshot.Item1) && snapshot.Item4 == EditorUtility.IsDirty(snapshot.Item1);

    static void CheckOriginalPayload(int number, string path, Report report)
    {
        var original = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "MComat/Assets/ExternalAssets/Data/Stage/" + number + ".asset");
        if (!File.Exists(original)) return;
        string Payload(string text)
        {
            var data = text.Substring(text.IndexOf("  battleGroundID:", StringComparison.Ordinal)).Replace("\r\n", "\n");
            data = Regex.Replace(data, "^  fightMode:.*\\n", "", RegexOptions.Multiline);
            // Empty YAML scalar lines may have had whitespace removed during restoration.
            return Regex.Replace(data, "[ \\t]+$", "", RegexOptions.Multiline);
        }
        Require(Payload(File.ReadAllText(original)) == Payload(File.ReadAllText(path)), "Restored Group payload differs from MComat at stage " + number);
        report.originalDataComparisons++;
    }

    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
