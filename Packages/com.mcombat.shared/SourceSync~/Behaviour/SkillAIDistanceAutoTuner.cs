using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Log;
using MCombat.Shared.Behaviour;
using Skill;
using UnityEngine;

public static class SkillAIDistanceAutoTuner
{
    const string TuneStateFileName = "SkillAIDistanceAutoTuneState.csv";
    const string TuneReportFileName = "SkillAIDistanceAutoTuneReport.csv";
    const float MinRangeWidth = 0.8f;

    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    sealed class TuneStateRow
    {
        public string RecordId;
        public float OverrideMin;
        public float OverrideMax;
        public int LastTriggered;
        public int LastAttempts;
        public string LastAction;
        public string LastReason;
        public string UpdatedUtc;
    }

    sealed class TuneReportRow
    {
        public string RecordId;
        public string RealName;
        public string StateType;
        public int TriggeredTimes;
        public int Attempts;
        public string HitRate;
        public string InterruptRate;
        public string UntouchedRate;
        public string OldMin;
        public string OldMax;
        public string NewMin;
        public string NewMax;
        public string Action;
        public string Reason;
    }

    struct TuneMetrics
    {
        public int TriggeredTimes;
        public int Attempts;
        public int Untouched;
        public int Touched;
        public int Successed;
        public int InterruptedTimes;
        public float HitRate;
        public float InterruptRate;
        public float UntouchedRate;
        public float SuccessPerTrigger;
    }

    static string TuneStatePath => Path.Combine(Application.persistentDataPath, TuneStateFileName);
    static string TuneReportPath => Path.Combine(Application.persistentDataPath, TuneReportFileName);
    public static string TuneStateFilePath => TuneStatePath;
    public static string TuneReportFilePath => TuneReportPath;

    public static void ApplyOverrides(IList<SkillAIAttrs.Row> rows)
    {
        if (rows == null || rows.Count == 0)
            return;

        var stateMap = LoadStateMap();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || string.IsNullOrEmpty(row.RECORD_ID))
                continue;

            if (!stateMap.TryGetValue(row.RECORD_ID, out var state))
                continue;

            row.TRIGGER_DIS_MIN = FormatFloat(state.OverrideMin);
            row.TRIGGER_DIS_MAX = FormatFloat(state.OverrideMax);
        }
    }

    public static void AutoTune(IList<HitBoxLogTable.Row> logRows)
    {
        if (!FightGlobalSetting.AutoTuneSkillAIDistance || logRows == null || logRows.Count == 0)
            return;

        var stateMap = LoadStateMap();
        var reports = new List<TuneReportRow>(logRows.Count);
        var changed = false;

        for (var i = 0; i < logRows.Count; i++)
        {
            var logRow = logRows[i];
            if (logRow == null || string.IsNullOrEmpty(logRow.RECORD_ID))
                continue;

            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(logRow.RECORD_ID);
            if (skillConfig == null || !BehaviorTypeUtility.IsTunableSkillState(skillConfig.STATE_TYPE))
                continue;

            var aiRow = SkillAIAttrs.Find_RECORD_ID(logRow.RECORD_ID);
            if (aiRow == null)
                continue;

            if (!TryBuildMetrics(logRow, out var metrics))
                continue;

            var currentMin = ParseFloat(aiRow.TRIGGER_DIS_MIN, 0f);
            var currentMax = Mathf.Max(currentMin + MinRangeWidth, ParseFloat(aiRow.TRIGGER_DIS_MAX, currentMin + MinRangeWidth));
            currentMax = Mathf.Min(currentMax, FightGlobalSetting.SkillAIAutoTuneMaxDistance);

            if (!stateMap.TryGetValue(logRow.RECORD_ID, out var state))
            {
                state = new TuneStateRow
                {
                    RecordId = logRow.RECORD_ID,
                    OverrideMin = currentMin,
                    OverrideMax = currentMax
                };
            }

            state.OverrideMin = currentMin;
            state.OverrideMax = currentMax;

            if (metrics.TriggeredTimes < state.LastTriggered || metrics.Attempts < state.LastAttempts)
            {
                state.LastTriggered = 0;
                state.LastAttempts = 0;
                state.LastAction = "Reset";
                state.LastReason = "LogCounterReset";
                state.UpdatedUtc = DateTime.UtcNow.ToString("o", Invariant);
            }

            var report = new TuneReportRow
            {
                RecordId = logRow.RECORD_ID,
                RealName = logRow.REAL_NAME ?? string.Empty,
                StateType = skillConfig.STATE_TYPE.ToString(),
                TriggeredTimes = metrics.TriggeredTimes,
                Attempts = metrics.Attempts,
                HitRate = (metrics.HitRate * 100f).ToString("0.0", Invariant),
                InterruptRate = (metrics.InterruptRate * 100f).ToString("0.0", Invariant),
                UntouchedRate = (metrics.UntouchedRate * 100f).ToString("0.0", Invariant),
                OldMin = FormatFloat(currentMin),
                OldMax = FormatFloat(currentMax),
                NewMin = FormatFloat(currentMin),
                NewMax = FormatFloat(currentMax),
                Action = "Hold",
                Reason = "NotEnoughData"
            };

            if (metrics.TriggeredTimes < FightGlobalSetting.SkillAIAutoTuneMinTriggeredTimes)
            {
                reports.Add(report);
                stateMap[logRow.RECORD_ID] = state;
                continue;
            }

            var newTriggers = metrics.TriggeredTimes - state.LastTriggered;
            if (newTriggers < FightGlobalSetting.SkillAIAutoTuneRetuneTriggerStep)
            {
                report.Reason = "WaitingMoreSamples";
                reports.Add(report);
                stateMap[logRow.RECORD_ID] = state;
                continue;
            }

            var newMin = currentMin;
            var newMax = currentMax;
            var action = "Hold";
            var reason = "MetricsStable";

            if (TryAdjustWindow(skillConfig.STATE_TYPE, metrics, currentMin, currentMax, out newMin, out newMax, out action, out reason))
            {
                if (!Mathf.Approximately(newMin, currentMin) || !Mathf.Approximately(newMax, currentMax))
                {
                    aiRow.TRIGGER_DIS_MIN = FormatFloat(newMin);
                    aiRow.TRIGGER_DIS_MAX = FormatFloat(newMax);
                    state.OverrideMin = newMin;
                    state.OverrideMax = newMax;
                    changed = true;
                }
            }

            state.LastTriggered = metrics.TriggeredTimes;
            state.LastAttempts = metrics.Attempts;
            state.LastAction = action;
            state.LastReason = reason;
            state.UpdatedUtc = DateTime.UtcNow.ToString("o", Invariant);
            stateMap[logRow.RECORD_ID] = state;

            report.NewMin = FormatFloat(state.OverrideMin);
            report.NewMax = FormatFloat(state.OverrideMax);
            report.Action = action;
            report.Reason = reason;
            reports.Add(report);
        }

        SaveStateMap(stateMap);
        SaveReport(reports);

        if (changed)
        {
            SkillConfigTable.RefreshSkillConfigDicForReference();
        }
    }

    static bool TryBuildMetrics(HitBoxLogTable.Row row, out TuneMetrics metrics)
    {
        metrics = default;
        if (row == null)
            return false;

        metrics.TriggeredTimes = ParseInt(row.TriggeredTimes);
        metrics.Attempts = ParseInt(row.Attempts);
        metrics.Untouched = ParseInt(row.Untouched);
        metrics.Touched = ParseInt(row.Touched);
        metrics.Successed = ParseInt(row.Succeeded);
        metrics.InterruptedTimes = ParseInt(row.InterruptedTimes);

        if (metrics.Attempts > 0)
        {
            metrics.HitRate = metrics.Successed / (float)metrics.Attempts;
            metrics.UntouchedRate = metrics.Untouched / (float)metrics.Attempts;
        }

        if (metrics.TriggeredTimes > 0)
        {
            metrics.InterruptRate = metrics.InterruptedTimes / (float)metrics.TriggeredTimes;
            metrics.SuccessPerTrigger = metrics.Successed / (float)metrics.TriggeredTimes;
        }

        return metrics.TriggeredTimes > 0 || metrics.Attempts > 0;
    }

    static bool TryAdjustWindow(
        BehaviorType stateType,
        TuneMetrics metrics,
        float currentMin,
        float currentMax,
        out float newMin,
        out float newMax,
        out string action,
        out string reason)
    {
        newMin = currentMin;
        newMax = currentMax;
        action = "Hold";
        reason = "MetricsStable";

        var width = Mathf.Max(MinRangeWidth, currentMax - currentMin);
        var stepBase = Mathf.Clamp(width * 0.08f, FightGlobalSetting.SkillAIAutoTuneStepMin, FightGlobalSetting.SkillAIAutoTuneStepMax);
        var stepStrong = Mathf.Clamp(width * 0.12f, FightGlobalSetting.SkillAIAutoTuneStepMin, FightGlobalSetting.SkillAIAutoTuneStepMax);
        var isCloseRange = BehaviorTypeUtility.IsCloseRangeSkillState(stateType);
        var isRanged = BehaviorTypeUtility.IsRangedSkillState(stateType);

        if (metrics.InterruptRate >= 0.5f)
        {
            newMin += isCloseRange ? stepBase * 0.5f : stepBase * 0.3f;
            newMax += isCloseRange ? stepBase : stepBase * 0.7f;
            action = "ShiftOut";
            reason = "HighInterruptRate";
            ClampWindow(ref newMin, ref newMax);
            return true;
        }

        if (metrics.HitRate <= 0.28f && metrics.UntouchedRate >= 0.55f)
        {
            newMin = Mathf.Max(0f, currentMin - (isRanged ? stepStrong * 0.4f : stepBase * 0.2f));
            newMax = currentMax - (isRanged ? stepStrong : stepBase);
            action = "ShiftIn";
            reason = "HighWhiffRate";
            ClampWindow(ref newMin, ref newMax);
            return true;
        }

        if (metrics.HitRate >= 0.7f && metrics.InterruptRate <= 0.15f && metrics.SuccessPerTrigger >= 0.7f && width < 6f)
        {
            newMax += isRanged ? stepBase * 0.8f : stepBase * 0.5f;
            action = "WidenOut";
            reason = "StablePerformance";
            ClampWindow(ref newMin, ref newMax);
            return true;
        }

        return false;
    }

    static void ClampWindow(ref float min, ref float max)
    {
        min = Mathf.Clamp(min, 0f, Mathf.Max(0f, FightGlobalSetting.SkillAIAutoTuneMaxDistance - MinRangeWidth));
        max = Mathf.Clamp(max, min + MinRangeWidth, FightGlobalSetting.SkillAIAutoTuneMaxDistance);
        if (max - min < MinRangeWidth)
            max = Mathf.Min(FightGlobalSetting.SkillAIAutoTuneMaxDistance, min + MinRangeWidth);
    }

    static Dictionary<string, TuneStateRow> LoadStateMap()
    {
        var map = new Dictionary<string, TuneStateRow>();
        if (!File.Exists(TuneStatePath))
            return map;

        var grid = CsvParser2.Parse(File.ReadAllText(TuneStatePath));
        for (var i = 1; i < grid.Length; i++)
        {
            if (grid[i].Length == 0 || string.IsNullOrEmpty(grid[i][0]))
                continue;

            var row = new TuneStateRow
            {
                RecordId = grid[i][0],
                OverrideMin = ParseFloat(grid[i].Length > 1 ? grid[i][1] : null, 0f),
                OverrideMax = ParseFloat(grid[i].Length > 2 ? grid[i][2] : null, MinRangeWidth),
                LastTriggered = ParseInt(grid[i].Length > 3 ? grid[i][3] : null),
                LastAttempts = ParseInt(grid[i].Length > 4 ? grid[i][4] : null),
                LastAction = grid[i].Length > 5 ? grid[i][5] : string.Empty,
                LastReason = grid[i].Length > 6 ? grid[i][6] : string.Empty,
                UpdatedUtc = grid[i].Length > 7 ? grid[i][7] : string.Empty
            };
            map[row.RecordId] = row;
        }
        return map;
    }

    static void SaveStateMap(IDictionary<string, TuneStateRow> stateMap)
    {
        var lines = new List<string>
        {
            "RECORD_ID,OVERRIDE_MIN,OVERRIDE_MAX,LAST_TRIGGERED,LAST_ATTEMPTS,LAST_ACTION,LAST_REASON,UPDATED_UTC"
        };

        foreach (var pair in stateMap)
        {
            var row = pair.Value;
            if (row == null || string.IsNullOrEmpty(row.RecordId))
                continue;

            lines.Add(string.Join(",",
                row.RecordId,
                FormatFloat(row.OverrideMin),
                FormatFloat(row.OverrideMax),
                row.LastTriggered.ToString(),
                row.LastAttempts.ToString(),
                SanitizeCsvValue(row.LastAction),
                SanitizeCsvValue(row.LastReason),
                SanitizeCsvValue(row.UpdatedUtc)));
        }

        File.WriteAllLines(TuneStatePath, lines);
    }

    static void SaveReport(IList<TuneReportRow> reports)
    {
        var lines = new List<string>
        {
            "RECORD_ID,REAL_NAME,STATE_TYPE,TRIGGERED_TIMES,ATTEMPTS,HIT_RATE,INTERRUPT_RATE,UNTOUCHED_RATE,OLD_MIN,OLD_MAX,NEW_MIN,NEW_MAX,ACTION,REASON"
        };

        for (var i = 0; i < reports.Count; i++)
        {
            var row = reports[i];
            if (row == null || string.IsNullOrEmpty(row.RecordId))
                continue;

            lines.Add(string.Join(",",
                row.RecordId,
                SanitizeCsvValue(row.RealName),
                row.StateType,
                row.TriggeredTimes.ToString(),
                row.Attempts.ToString(),
                row.HitRate,
                row.InterruptRate,
                row.UntouchedRate,
                row.OldMin,
                row.OldMax,
                row.NewMin,
                row.NewMax,
                row.Action,
                row.Reason));
        }

        File.WriteAllLines(TuneReportPath, lines);
    }

    static string SanitizeCsvValue(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace(",", "_").Replace("\n", " ").Replace("\r", " ");
    }

    static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, Invariant, out var parsed) ? parsed : 0;
    }

    static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, NumberStyles.Float, Invariant, out var parsed) ? parsed : fallback;
    }

    static string FormatFloat(float value)
    {
        return value.ToString("0.###", Invariant);
    }
}
