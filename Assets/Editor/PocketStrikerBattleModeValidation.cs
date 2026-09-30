using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using MCombat.Shared.Combat;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Exercises local battle data without entering Play mode or accessing an account.</summary>
public static class PocketStrikerBattleModeValidation
{
    const string ReportPath = "Logs/Revival/battle-modes-report.json";
    const int SamplesPerDifficulty = 30;
    static bool running;

    [Serializable]
    public sealed class Report
    {
        public string unityVersion;
        public string utcTime;
        public bool passed;
        public bool sourceAssetsUnchanged = true;
        public bool prefabEntriesPassed;
        public int expectedStages;
        public int configuredStages;
        public int[] unpublishedConfiguredStages = Array.Empty<int>();
        public int stagesChecked;
        public int unitConfigs;
        public int skillConfigs;
        public int bossBattlesChecked;
        public int bossSkillSetsChecked;
        public int multiStages;
        public int rotationStages;
        public int evolutionStages;
        public int groupStages;
        public string addressablesBuilder;
        public List<StageResult> stages = new List<StageResult>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public sealed class StageResult
    {
        public int stage;
        public string mode;
        public int enemyCount;
        public float level;
        public bool sourceUnchanged;
    }

    sealed class SourceSnapshot
    {
        public string path;
        public string file;
        public string json;
        public bool dirty;
        public FightInfo asset;
    }

    public static async void ValidateBatch()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Battle Modes batch validation requires a stopped editor and no active validation run.");
            EditorApplication.Exit(1);
            return;
        }

        try
        {
            Validate();
            await UniTask.WaitUntil(() => !running);
            var report = File.Exists(ReportPath)
                ? JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath)) : null;
            EditorApplication.Exit(report != null && report.passed ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("PocketStriker/Validation/Battle Modes")]
    public static async void Validate()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Battle Modes validation requires a stopped editor and no active validation run.");
            return;
        }

        running = true;
        var randomState = UnityEngine.Random.state;
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var snapshots = new Dictionary<int, SourceSnapshot>();
        void CaptureError(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 200)
                report.errors.Add(message);
        }

        Application.logMessageReceived += CaptureError;
        // Stopped editors tick UniTask's runners, but Addressables dispatches
        // completion callbacks from an ExecuteInEditMode MonoBehaviour.Update.
        // Request editor player-loop ticks for the lifetime of this validation.
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        try
        {
            Debug.Log("[BattleModes] Validating local tables, 90 random Boss battles, published adventure stages, and menu prefab entries.");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Require(settings != null, "Addressables settings are missing.");
            report.addressablesBuilder = settings.ActivePlayModeDataBuilder != null
                ? settings.ActivePlayModeDataBuilder.Name : "none";
            var entries = settings.groups.Where(group => group != null).SelectMany(group => group.entries).ToList();
            await LoadLocalTables(entries, report);
            CheckRandomBosses(entries, report);
            CheckPrefabEntries(report);

            var questEntries = entries.Where(entry => entry.labels.Contains("quest")).ToList();
            report.expectedStages = questEntries.Count;
            Require(report.expectedStages > 0, "No published quest assets were found.");
            foreach (var entry in questEntries)
            {
                Require(int.TryParse(entry.address, out var number) && number > 0, "Invalid quest address: " + entry.address);
                var source = AssetDatabase.LoadAssetAtPath<FightInfo>(entry.AssetPath);
                Require(source != null, "Missing quest asset: " + entry.AssetPath);
                snapshots.Add(number, new SourceSnapshot
                {
                    path = entry.AssetPath, file = File.ReadAllText(entry.AssetPath),
                    json = EditorJsonUtility.ToJson(source), dirty = EditorUtility.IsDirty(source), asset = source
                });
            }
            Require(snapshots.Keys.OrderBy(number => number).SequenceEqual(Enumerable.Range(1, report.expectedStages)),
                "Published quest addresses must be continuous from 1 to the highest published stage.");

            var modes = new StageModeTable();
            modes.Load(LoadText(entries, "Config/" + CommonSetting.StageModeFile));
            var configuredIds = modes.GetRowList()
                .Where(row => int.TryParse(row.STAGE_ID, out var number) && number > 0)
                .Select(row => int.Parse(row.STAGE_ID)).Distinct().OrderBy(number => number).ToArray();
            report.configuredStages = configuredIds.Length;
            report.unpublishedConfiguredStages = configuredIds.Where(number => !snapshots.ContainsKey(number)).ToArray();
            foreach (var number in snapshots.Keys)
            {
                var configured = modes.FindAll_STAGE_ID(number.ToString());
                Require(configured.Count == 1 && int.TryParse(configured[0].MODE_NUM, out var mode)
                    && mode >= AdventureModeRules.MultiMode && mode <= AdventureModeRules.GroupMode,
                    "Missing, duplicate, or invalid mode configuration for published stage " + number);
                if (modes.GetModeById(number.ToString()) == AdventureModeRules.GroupMode)
                    Require(snapshots[number].asset is GangbangInfo && snapshots[number].asset.IsGroupBattle,
                        "Published Group quest points to an ordinary stage asset: " + number);
            }
            Debug.Log($"[BattleModes] Found {report.expectedStages} published quests and {report.configuredStages} configured stages; "
                + $"{report.unpublishedConfiguredStages.Length} unpublished configurations are excluded from runtime checks.");
            Debug.Log("[BattleModes] Initializing Addressables with editor player-loop updates enabled.");
            var initialization = Addressables.InitializeAsync(false);
            try
            {
                await initialization.ToUniTask().Timeout(TimeSpan.FromSeconds(10));
                Require(initialization.Status == AsyncOperationStatus.Succeeded,
                    "Addressables initialization failed: " + initialization.OperationException);
            }
            finally
            {
                if (initialization.IsValid()) Addressables.Release(initialization);
            }
            Debug.Log("[BattleModes] Addressables initialized; loading all published quests through ArcadeModeManager.");
            var manager = new ArcadeModeManager();
            await manager.Initialize().Timeout(TimeSpan.FromSeconds(45));
            Require(manager.MaxStageNum == report.expectedStages, "Adventure runtime did not discover every published quest.");
            for (var number = 1; number <= report.expectedStages; number++)
            {
                var stage = await manager.LoadStage(number).Timeout(TimeSpan.FromSeconds(20));
                try
                {
                    Require(stage != null, "Could not load adventure stage " + number);
                    Require(snapshots.TryGetValue(number, out var source), "Missing source for stage " + number);
                    Require(stage != source.asset, "Runtime returned the source asset for stage " + number);
                    var expectedMode = AdventureModeRules.ResolveMode(number.ToString(), modes.GetModeById(number.ToString()));
                    var expectedFightMode = expectedMode == AdventureModeRules.MultiMode ? FightMode.Multi
                        : expectedMode == AdventureModeRules.EvolutionMode ? FightMode.Evolve
                        : expectedMode == AdventureModeRules.GroupMode ? FightMode.Group : FightMode.Rotate;
                    Require(stage.ID == number.ToString() && stage.EventType == FightEventType.Quest,
                        "Incorrect adventure routing for stage " + number);
                    Require(stage.ArcadeFightMode == expectedMode && stage.FightMode == expectedFightMode,
                        "Incorrect battle mode for stage " + number);
                    Require(stage.UnitsData.Count > 0 && stage.UnitsData.All(unit =>
                        unit != null && Units.GetUnitConfig(unit.r_id) != null && Mathf.Approximately(unit.level, stage.stageRefLevel)),
                        "Invalid enemy or level for stage " + number);
                    Require(stage.UnitsData.All(unit => !string.IsNullOrEmpty(unit.id))
                        && stage.UnitsData.Select(unit => unit.id).Distinct().Count() == stage.UnitsData.Count,
                        "Empty or duplicate enemy identity for stage " + number);
                    if (expectedFightMode == FightMode.Multi) report.multiStages++;
                    else if (expectedFightMode == FightMode.Evolve) report.evolutionStages++;
                    else if (expectedFightMode == FightMode.Group)
                    {
                        report.groupStages++;
                        Require(stage is GangbangInfo group && group.Team2GroupSet.Count > 0
                            && group.Team2GroupSet.All(set => stage.UnitsData.Any(unit => unit.id == set.id)),
                            "Group quest is missing its authored fighter counts: " + number);
                        Require(stage.IsGroupBattle && !stage.AllowsManualUnitControl && stage.ShouldForceAutoBattle,
                            "Group quest was treated as a normal team fight: " + number);
                    }
                    else report.rotationStages++;

                    // A runtime visit may change levels and skills. Such writes must not reach the authored asset.
                    var originalLevel = stage.UnitsData[0].level;
                    var originalFirstSkill = stage.UnitsData[0].set?.a1;
                    stage.UnitsData[0].level += 0.25f;
                    if (stage.UnitsData[0].set != null)
                        stage.UnitsData[0].set.a1 = "validation-runtime-only";
                    var unchanged = IsUnchanged(source);
                    stage.UnitsData[0].level = originalLevel;
                    if (stage.UnitsData[0].set != null)
                        stage.UnitsData[0].set.a1 = originalFirstSkill;
                    if (!unchanged) report.sourceAssetsUnchanged = false;
                    Require(unchanged, "Runtime mutation changed the source of stage " + number);
                    report.stages.Add(new StageResult
                    {
                        stage = number, mode = expectedFightMode.ToString(), enemyCount = stage.UnitsData.Count,
                        level = stage.stageRefLevel, sourceUnchanged = unchanged
                    });
                    report.stagesChecked++;
                }
                catch (Exception exception)
                {
                    report.errors.Add("Stage " + number + ": " + exception.Message);
                }
                finally
                {
                    if (stage != null && !EditorUtility.IsPersistent(stage))
                        UnityEngine.Object.DestroyImmediate(stage);
                }
            }
        }
        catch (Exception exception)
        {
            report.errors.Add(exception.ToString());
        }
        finally
        {
            foreach (var source in snapshots.Values)
            {
                if (IsUnchanged(source)) continue;
                report.sourceAssetsUnchanged = false;
                report.errors.Add("Source asset changed: " + source.path);
            }
            Application.logMessageReceived -= CaptureError;
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
            UnityEngine.Random.state = randomState;
            running = false;
            report.passed = report.errors.Count == 0 && report.expectedStages > 0 && report.stagesChecked == report.expectedStages
                && report.bossBattlesChecked == SamplesPerDifficulty * 3 && report.prefabEntriesPassed;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            var summary = $"[BattleModes] {(report.passed ? "PASS" : "FAIL")}: adventure {report.stagesChecked}/{report.expectedStages} published "
                + $"({report.configuredStages} configured), "
                + $"(multi {report.multiStages}, rotation {report.rotationStages}, evolution {report.evolutionStages}, group {report.groupStages}), "
                + $"Boss battles {report.bossBattlesChecked}/90, skill sets {report.bossSkillSetsChecked}, "
                + $"source assets unchanged={report.sourceAssetsUnchanged}. Report: {Path.GetFullPath(ReportPath)}";
            if (report.passed) Debug.Log(summary);
            else Debug.LogError(summary + "\n" + string.Join("\n", report.errors.Take(12)));
        }
    }

    static async UniTask LoadLocalTables(List<AddressableAssetEntry> entries, Report report)
    {
        var settingsEntry = entries.FirstOrDefault(entry => entry.address == "Config/commonSetting");
        Require(settingsEntry != null, "Common settings address is missing.");
        var settings = AssetDatabase.LoadAssetAtPath<CommonSetting>(settingsEntry.AssetPath);
        Require(settings != null, "Common settings asset is missing.");
        settings.Initialise();
        Units.Load(LoadText(entries, "Config/" + CommonSetting.UnitConfigFile));
        Units.Dic.Clear();
        foreach (var config in Units.RowToConfigList(Units.rowList))
            Units.Dic.Add(config.RECORD_ID, config);
        SkillAIAttrs.Load(LoadText(entries, "Config/" + CommonSetting.SkillAIFile));
        SkillConfigTable.Load(LoadText(entries, "Config/" + CommonSetting.SkillConfigFile));
        SkillConfigTable.RefreshSkillConfigDicForReference();
        await UnitPassiveTable.Load(key => UniTask.FromResult(LoadText(entries, key)),
            id => SkillConfigTable.GetSkillConfigByRecordId(id) != null);
        report.unitConfigs = Units.Dic.Count;
        report.skillConfigs = SkillConfigTable.SkillConfigRefDic.Count;
        Require(report.unitConfigs >= 3 && report.skillConfigs >= 9, "Local battle tables are incomplete.");
    }

    static void CheckRandomBosses(List<AddressableAssetEntry> entries, Report report)
    {
        var difficulties = new[] { "easy", "normal", "hard" };
        var gauges = new[] { CriticalGaugeMode.Normal, CriticalGaugeMode.DoubleGain, CriticalGaugeMode.Unlimited };
        for (var difficulty = 0; difficulty < difficulties.Length; difficulty++)
        {
            var entry = entries.FirstOrDefault(item => item.address == difficulties[difficulty]);
            Require(entry != null, "Missing Boss difficulty template: " + difficulties[difficulty]);
            var template = AssetDatabase.LoadAssetAtPath<FightInfo>(entry.AssetPath);
            Require(template != null, "Could not read Boss template: " + entry.AssetPath);
            for (var sample = 0; sample < SamplesPerDifficulty; sample++)
            {
                FightInfo stage = null;
                try
                {
                    stage = RandomBossStageFactory.Create(difficulties[difficulty] + "_20260927",
                        gauges[difficulty], 3 - difficulty, template.stageRefLevel);
                    Require(stage.EventType == FightEventType.Event && stage.ArcadeFightMode == 0
                        && stage.FightMode == FightMode.Rotate && stage.team2CGMode == gauges[difficulty], "Incorrect Boss routing or energy mode.");
                    Require(stage.UnitsData.Count == 3 - difficulty, "Incorrect Boss enemy count.");
                    Require(stage.UnitsData.Select(unit => unit.id).Distinct().Count() == stage.UnitsData.Count, "Repeated Boss identity.");
                    foreach (var unit in stage.UnitsData)
                    {
                        Require(!string.IsNullOrEmpty(unit.id) && !SubUnitUtility.IsSubUnitId(unit.id)
                            && Units.GetUnitConfig(unit.r_id)?.TYPE == "human", "Invalid Boss enemy.");
                        Require(Mathf.Approximately(unit.level, template.stageRefLevel), "Boss level was not applied.");
                        Require(unit.set != null, "Boss skill set is missing.");
                        var skills = new[] { unit.set.a1, unit.set.a2, unit.set.a3, unit.set.b1, unit.set.b2,
                            unit.set.b3, unit.set.c1, unit.set.c2, unit.set.c3 };
                        Require(skills.All(id => SkillConfigTable.GetSkillConfigByRecordId(id) != null)
                            && skills.Distinct().Count() == 9, "Boss skill set has missing or repeated skills.");
                        var passive = UnitPassiveTable.GetUnitPassiveRecordId(unit.r_id);
                        if (SkillConfigTable.GetSkillConfigByRecordId(passive) != null)
                            Require(skills.Contains(passive), "Boss is missing its configured character passive.");
                        // Unlimited Bosses deliberately bypass player SP balance and normal-start constraints.
                        if (gauges[difficulty] != CriticalGaugeMode.Unlimited)
                            Require(unit.set.CheckEdit() == SkillSet.SkillEditError.Perfect,
                                $"{difficulties[difficulty]} sample {sample}, unit {unit.r_id}: {unit.set.CheckEdit()}.");
                        report.bossSkillSetsChecked++;
                    }
                    report.bossBattlesChecked++;
                }
                catch (Exception exception)
                {
                    report.errors.Add($"Boss {difficulties[difficulty]} sample {sample}: {exception.Message}");
                }
                finally
                {
                    if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                }
            }
        }
    }

    static void CheckPrefabEntries(Report report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/FrontLayer.prefab");
        Require(prefab != null, "FrontLayer prefab is missing.");
        var layer = prefab.GetComponent<FrontLayer>();
        Require(layer != null, "FrontLayer component is missing.");
        var serialized = new SerializedObject(layer);
        var adventure = serialized.FindProperty("ArcadeBtn")?.objectReferenceValue as Component;
        var chaos = serialized.FindProperty("GangbangBtn")?.objectReferenceValue as Component;
        var boss = serialized.FindProperty("EventFightBtn")?.objectReferenceValue as Component;
        Require(adventure != null && adventure.gameObject.activeSelf, "Adventure entry is missing or hidden.");
        Require(chaos != null && !chaos.gameObject.activeSelf, "Retired Chaos entry is still active.");
        Require(boss != null && boss.gameObject.activeSelf && boss.name == "RandomBoss", "Random Boss entry is missing.");
        Require(boss.GetComponentsInChildren<LanguageConverter>(true).Any(label => label.languageCode == "RandomBossMode"),
            "Random Boss entry has no matching localized label.");
        report.prefabEntriesPassed = true;
    }

    static TextAsset LoadText(List<AddressableAssetEntry> entries, string address)
    {
        var entry = entries.FirstOrDefault(item => item.address == address);
        var text = entry != null ? AssetDatabase.LoadAssetAtPath<TextAsset>(entry.AssetPath) : null;
        Require(text != null, "Missing local configuration: " + address);
        return text;
    }

    static bool IsUnchanged(SourceSnapshot snapshot)
    {
        return snapshot.asset != null && snapshot.json == EditorJsonUtility.ToJson(snapshot.asset)
            && snapshot.file == File.ReadAllText(snapshot.path) && snapshot.dirty == EditorUtility.IsDirty(snapshot.asset);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
