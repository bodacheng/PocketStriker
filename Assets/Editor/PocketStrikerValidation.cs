using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FightScene;

/// <summary>Repeatable local checks; does not log in, purchase, or publish content.</summary>
[InitializeOnLoad]
public static class PocketStrikerValidation
{
    const string SmokeKey = "PocketStriker.Validation.Smoke";
    const string ErrorKey = SmokeKey + ".Errors";
    const string StartKey = SmokeKey + ".Started";
    const string ReadyKey = SmokeKey + ".Ready";
    const string PlayModeBuilderKey = SmokeKey + ".PlayModeBuilder";
    const string ReportDirectory = "Logs/Revival";

    [Serializable]
    public class ValidationReport
    {
        public string unityVersion;
        public string check;
        public bool passed;
        public int scenes;
        public int addressableEntries;
        public string[] errors;
    }

    static PocketStrikerValidation()
    {
        if (!SessionState.GetBool(SmokeKey, false)) return;
        Application.logMessageReceived += CaptureError;
        EditorApplication.update += PollSmoke;
    }

    [MenuItem("PocketStriker/Validation/Check Project")]
    public static void CheckProject()
    {
        RequireSavedScenes();
        var errors = new List<string>();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var entriesCount = 0;
        try
        {
            if (scenes.Length != 3) errors.Add("Expected startup, main menu, and fight build scenes.");
            foreach (var scene in scenes)
            {
                if (!File.Exists(scene.path)) { errors.Add("Missing build scene: " + scene.path); continue; }
                var loaded = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                foreach (var root in loaded.GetRootGameObjects()) CheckScripts(root, scene.path, errors);
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) errors.Add("Addressables settings are missing.");
            else
            {
                if (settings.DataBuilders.Any(builder => builder == null))
                    errors.Add("Addressables contains an obsolete or missing data builder.");
                var addresses = new HashSet<string>();
                foreach (var group in settings.groups.Where(g => g != null))
                foreach (var entry in group.entries)
                {
                    entriesCount++;
                    addresses.Add(entry.address);
                    if (string.IsNullOrEmpty(entry.AssetPath)) errors.Add("Unresolved Addressable: " + entry.address);
                }
                foreach (var key in new[] { "app_version", "Config/commonSetting" })
                    if (!addresses.Contains(key)) errors.Add("Missing startup address: " + key);
                var labels = settings.GetLabels();
                foreach (var label in AddressablesResourcePolicy.FullCombatEssentialLabels.Concat(new[] { "config" }))
                    if (!labels.Contains(label)) errors.Add("Missing combat label: " + label);
            }

            // Prefabs are loaded as assets, so checks do not execute gameplay or network code.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) CheckScripts(prefab, path, errors);
            }
        }
        finally
        {
            if (!Application.isBatchMode && setup.Length > 0 && setup.All(scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        WriteReport("project", errors, scenes.Length, entriesCount);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("PocketStriker project validation passed.");
    }

    static void CheckScripts(GameObject root, string path, List<string> errors)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            if (count > 0) errors.Add($"{path}: {child.name} has {count} missing script(s).");
        }
    }

    // Run without -quit; completion and the 120-second deadline exit batch mode.
    public static void SmokeStartup()
    {
        RequireSavedScenes();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings are missing.");
        var fastModeIndex = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        if (fastModeIndex < 0) throw new InvalidOperationException("The AssetDatabase play mode builder is missing.");
        SessionState.SetInt(PlayModeBuilderKey, settings.ActivePlayModeDataBuilderIndex);
        settings.ActivePlayModeDataBuilderIndex = fastModeIndex;
        SessionState.SetBool(SmokeKey, true);
        SessionState.SetString(ErrorKey, "");
        SessionState.SetString(StartKey, DateTime.UtcNow.ToString("O"));
        SessionState.SetString(ReadyKey, "");
        Application.logMessageReceived -= CaptureError;
        Application.logMessageReceived += CaptureError;
        EditorApplication.update -= PollSmoke;
        EditorApplication.update += PollSmoke;
        try
        {
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s => s.enabled).path);
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            FinishSmoke(new[] { exception.ToString() });
        }
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        var existing = SessionState.GetString(ErrorKey, "");
        if (existing.Length < 24000) SessionState.SetString(ErrorKey, existing + message + "\n" + stack + "\n");
    }

    static void RequireSavedScenes()
    {
        if (Application.isBatchMode) return;
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded && (scene.isDirty || string.IsNullOrEmpty(scene.path)))
                throw new InvalidOperationException("Save all open scenes before running PocketStriker validation. Unsaved scene: " + scene.name);
        }
    }

    static void PollSmoke()
    {
        if (!SessionState.GetBool(SmokeKey, false)) return;
        var elapsed = DateTime.UtcNow - DateTime.Parse(SessionState.GetString(StartKey, DateTime.UtcNow.ToString("O"))).ToUniversalTime();
        var errors = SessionState.GetString(ErrorKey, "");
        if (!string.IsNullOrEmpty(errors)) { FinishSmoke(new[] { errors }); return; }
        if (elapsed.TotalSeconds > 120)
        {
            var manager = RTFightManager.Target;
            FinishSmoke(new[] { $"Startup timed out: scene={SceneManager.GetActiveScene().name}, " +
                $"process={FSceneProcessesRunner.Main.currentProcess?.GetType().Name}, " +
                $"event={FightLoad.Fight?.EventType}, config={Starter.ConfigInitialised}, " +
                $"teams={manager?.team1?.teamMembers.GetValues().Count}/{manager?.team2?.teamMembers.GetValues().Count}, " +
                $"frame={Time.frameCount}, timeScale={Time.timeScale}." });
            return;
        }
        if (EditorApplication.isPlaying && Starter.ConfigInitialised)
        {
            var fight = SceneManager.GetActiveScene().name == "FightScene"
                && FightLoad.Fight != null && FightLoad.Fight.EventType == FightEventType.Screensaver
                && FightScene.FSceneProcessesRunner.Main.currentProcess is FightScene.FightingProcess
                && RTFightManager.Target != null
                && RTFightManager.Target.team1.teamMembers.GetValues().Count > 0
                && RTFightManager.Target.team2.teamMembers.GetValues().Count > 0;
            var title = UnityEngine.Object.FindObjectsByType<TitleScreenLayer>(FindObjectsSortMode.None).Length > 0;
            // A player may have disabled the background fight. Exercise it directly
            // once their normal title has loaded, without changing PlayerPrefs.
            if (title && SceneManager.GetActiveScene().buildIndex == 0)
            {
                var starter = UnityEngine.Object.FindFirstObjectByType<Starter>();
                if (starter != null) starter.EnterFrontScene();
                return;
            }
            if (fight && title)
            {
                var ready = SessionState.GetString(ReadyKey, "");
                if (ready == "") SessionState.SetString(ReadyKey, DateTime.UtcNow.ToString("O"));
                else if ((DateTime.UtcNow - DateTime.Parse(ready).ToUniversalTime()).TotalSeconds >= 20)
                {
                    FinishSmoke(Array.Empty<string>());
                    return;
                }
            }
            else SessionState.SetString(ReadyKey, "");
        }
    }

    static void FinishSmoke(string[] errors)
    {
        SessionState.SetBool(SmokeKey, false);
        Application.logMessageReceived -= CaptureError;
        EditorApplication.update -= PollSmoke;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(PlayModeBuilderKey, 0);
        WriteReport("startup", errors, 0, 0);
        if (Application.isBatchMode) EditorApplication.Exit(errors.Length == 0 ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }

    public static void BuildMac()
    {
        var path = "Builds/Revival/PocketStriker.app";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings are missing.");
        var originalProfile = settings.activeProfileId;
        var originalRemoteCatalog = settings.BuildRemoteCatalog;
        var originalBuildOption = settings.BuildAddressablesWithPlayerBuild;
        var localProfile = settings.profileSettings.AddProfile("Local validation " + Guid.NewGuid(), originalProfile);
        try
        {
            // Bundle every Addressable into this local development player. Existing
            // CDN profiles remain intact and this build never uploads content.
            settings.profileSettings.SetValue(localProfile, "Remote.BuildPath",
                settings.profileSettings.GetValueByName(originalProfile, "Local.BuildPath"));
            settings.profileSettings.SetValue(localProfile, "Remote.LoadPath",
                settings.profileSettings.GetValueByName(originalProfile, "Local.LoadPath"));
            settings.activeProfileId = localProfile;
            settings.BuildRemoteCatalog = false;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Player build failed: " + report.summary.result);
            Debug.Log("PocketStriker player build succeeded: " + Path.GetFullPath(path));
        }
        finally
        {
            settings.activeProfileId = originalProfile;
            settings.BuildRemoteCatalog = originalRemoteCatalog;
            settings.BuildAddressablesWithPlayerBuild = originalBuildOption;
            settings.profileSettings.RemoveProfile(localProfile);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }

    public static void CompilePlayer()
    {
        var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
        {
            target = BuildTarget.StandaloneOSX,
            group = BuildTargetGroup.Standalone,
            options = ScriptCompilationOptions.DevelopmentBuild
        }, "Library/RevivalPlayerScripts");
        if (result.assemblies == null || !result.assemblies.Any())
            throw new InvalidOperationException("Player script compilation produced no assemblies.");
        Debug.Log("PocketStriker player script compilation passed.");
    }

    static void WriteReport(string check, IEnumerable<string> errors, int scenes, int entries)
    {
        var errorArray = errors.ToArray();
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(Path.Combine(ReportDirectory, check + "-report.json"), JsonUtility.ToJson(new ValidationReport
        {
            unityVersion = Application.unityVersion,
            check = check,
            passed = errorArray.Length == 0,
            scenes = scenes,
            addressableEntries = entries,
            errors = errorArray
        }, true));
    }
}
