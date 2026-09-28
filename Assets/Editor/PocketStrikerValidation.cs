using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FightScene;

/// <summary>Repeatable local checks; does not log in, purchase, or publish content.</summary>
[InitializeOnLoad]
public static class PocketStrikerValidation
{
    const string SmokeKey = "PocketStriker.Validation.Smoke";
    const string ErrorKey = SmokeKey + ".Errors";
    const string StartKey = SmokeKey + ".Started";
    const string ReadyKey = SmokeKey + ".Ready";
    const string RunsKey = SmokeKey + ".Runs";
    const string ReadyFrameKey = SmokeKey + ".ReadyFrame";
    const string ReentryKey = SmokeKey + ".ReentryObserved";
    const string PoolCheckKey = SmokeKey + ".PoolChecked";
    const string DownloadCheckKey = SmokeKey + ".DownloadUiChecked";
    const string PlayModeBuilderKey = SmokeKey + ".PlayModeBuilder";
    const string ReportDirectory = "Logs/Revival";

    [Serializable]
    public class FightRunReport
    {
        public int run;
        public int team1;
        public int team2;
        public int startFrame;
        public int endFrame;
        public double stableSeconds;
    }

    [Serializable]
    class FightRuns
    {
        public List<FightRunReport> items = new List<FightRunReport>();
    }

    [Serializable]
    public class ValidationReport
    {
        public string unityVersion;
        public string expectedUnityVersion;
        public string activeBuildTarget;
        public string defaultOrientation;
        public string runtimeOrientation;
        public int screenWidth;
        public int screenHeight;
        public int defaultScreenWidth;
        public int defaultScreenHeight;
        public bool portraitOnly;
        public string outputPath;
        public string check;
        public bool passed;
        public int scenes;
        public int addressableEntries;
        public string[] errors;
        public FightRunReport[] fightRuns;
        public bool reentryObserved;
        public bool poolLifecyclePassed;
        public bool downloadPresentationPassed;
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
        var errors = EnvironmentErrors();
        try { Cocone.ProjectP3.VersionSyncUtility.AssertVersionSettingsSynchronized(); }
        catch (Exception exception) { errors.Add(exception.Message); }
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
                    if (entry.address.StartsWith("battleGround/", StringComparison.Ordinal))
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                        if (prefab == null || prefab.GetComponent<BattleGround>() == null)
                            errors.Add("BattleGround component missing at prefab root: " + entry.address);
                    }
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
            CheckDownloadProgressAssets(errors);
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

    static void CheckDownloadProgressAssets(List<string> errors)
    {
        const string path = "Assets/Resources/DummyLayerSystem/ProgressLayer.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var progress = prefab != null ? prefab.GetComponent<ProgressLayer>() : null;
        if (progress == null)
        {
            errors.Add(path + ": bundled download progress layer is missing.");
            return;
        }

        var serialized = new SerializedObject(progress);
        foreach (var field in new[] { "progressBar", "percentage", "info", "bigCurtain" })
        {
            if (serialized.FindProperty(field)?.objectReferenceValue == null)
                errors.Add(path + ": missing download UI reference: " + field);
        }
    }

    static void CheckDownloadPresentation()
    {
        const string description = "Downloading resources...";
        try
        {
            // AssetDatabase play mode skips downloading. Exercise its real UI entry
            // separately, including reuse while a previous progress tween is active.
            ProgressLayer.Downloading(description);
            var layer = UnityEngine.Object.FindFirstObjectByType<ProgressLayer>();
            if (layer == null) throw new InvalidOperationException("Download progress did not open.");
            ProgressLayer.LoadingPercent("Previous download", 0.75f);
            ProgressLayer.Downloading(description);

            var serialized = new SerializedObject(layer);
            var bar = (Slider)serialized.FindProperty("progressBar").objectReferenceValue;
            var percentage = (Text)serialized.FindProperty("percentage").objectReferenceValue;
            var info = (Text)serialized.FindProperty("info").objectReferenceValue;
            var curtain = (Image)serialized.FindProperty("bigCurtain").objectReferenceValue;
            if (layer.transform.parent != PosCal.Canvas.transform ||
                !bar.gameObject.activeInHierarchy || bar.value != 0 || percentage.text != "0%" ||
                info.text != description || curtain.color != Color.black || !curtain.raycastTarget ||
                curtain.transform.GetSiblingIndex() != 0 ||
                UnityEngine.Object.FindObjectsByType<ProgressLayer>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Download UI must show a single full-screen progress layer at 0% with an opaque background.");

            ProgressLayer.Close();
            if (layer.gameObject.activeInHierarchy || !layer.IsClosing)
                throw new InvalidOperationException("Download progress remained active after closing.");
            Debug.Log("POCKETSTRIKER_DOWNLOAD_UI_PASSED: initialized, reset and closed without character backgrounds.");
        }
        finally { ProgressLayer.Close(); }
    }

    public static void PrepareEditor()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("PrepareEditor requires a stopped interactive editor.");
        RequireSavedScenes();
        RequireEnvironment();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            throw new InvalidOperationException("Open Unity with -buildTarget iOS before preparing the editor.");
        EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(scene => scene.enabled).path, OpenSceneMode.Single);
        ConfigurePortraitGameView();
        WriteReport("editor-ready", Array.Empty<string>(), 1, 0);
        Debug.Log("PocketStriker editor ready: iOS, Portrait 540x960, startup scene open. Press Play to run.");
    }

    // Run without -quit; completion and the 180-second deadline exit batch mode.
    [MenuItem("PocketStriker/Validation/Startup Smoke")]
    public static void SmokeStartup()
    {
        var startupScene = EditorBuildSettings.scenes.First(s => s.enabled).path;
        var useOpenStartup = SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == startupScene;
        // Entering Play preserves the existing edit-mode scene, including unsaved
        // work. Only require saved scenes when the test actually has to switch.
        if (!useOpenStartup) RequireSavedScenes();
        RequireEnvironment();
        ConfigurePortraitGameView();
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
        SessionState.SetString(RunsKey, JsonUtility.ToJson(new FightRuns()));
        SessionState.SetBool(ReentryKey, false);
        SessionState.SetBool(PoolCheckKey, false);
        SessionState.SetBool(DownloadCheckKey, false);
        Application.logMessageReceived -= CaptureError;
        Application.logMessageReceived += CaptureError;
        EditorApplication.update -= PollSmoke;
        EditorApplication.update += PollSmoke;
        try
        {
            if (!useOpenStartup) EditorSceneManager.OpenScene(startupScene);
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

    static string ExpectedUnityVersion => File.ReadLines("ProjectSettings/ProjectVersion.txt")
        .First(line => line.StartsWith("m_EditorVersion: ")).Substring("m_EditorVersion: ".Length).Trim();

    static bool PortraitOnly => PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait
        && !PlayerSettings.allowedAutorotateToLandscapeLeft
        && !PlayerSettings.allowedAutorotateToLandscapeRight
        && !PlayerSettings.allowedAutorotateToPortraitUpsideDown;

    static List<string> EnvironmentErrors()
    {
        var errors = new List<string>();
        if (Application.unityVersion != ExpectedUnityVersion)
            errors.Add($"Expected Unity {ExpectedUnityVersion}; running {Application.unityVersion}.");
        if (!PortraitOnly)
            errors.Add("PocketStriker requires Portrait orientation with landscape/upside-down autorotation disabled.");
        if (PlayerSettings.defaultScreenWidth >= PlayerSettings.defaultScreenHeight)
            errors.Add("The default player window must be portrait.");
        return errors;
    }

    static void RequireEnvironment()
    {
        var errors = EnvironmentErrors();
        if (errors.Count == 0) return;
        WriteReport("environment", errors, 0, 0);
        throw new InvalidOperationException(string.Join("\n", errors));
    }

    static void ConfigurePortraitGameView()
    {
        // Unity 6000.5 exposes this operation internally on GameView. Use its own
        // resolution setter so the rendered viewport, not just the window, is 9:16.
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var setResolution = gameViewType.GetMethod("SetCustomResolution", BindingFlags.Instance | BindingFlags.NonPublic);
        if (setResolution == null)
            throw new InvalidOperationException("This Unity editor cannot configure the portrait validation Game view.");
        var view = EditorWindow.GetWindow(gameViewType);
        setResolution.Invoke(view, new object[] { new Vector2(540, 960), "PocketStriker Portrait" });
        view.Repaint();
    }

    static void PollSmoke()
    {
        if (!SessionState.GetBool(SmokeKey, false)) return;
        var elapsed = DateTime.UtcNow - DateTime.Parse(SessionState.GetString(StartKey, DateTime.UtcNow.ToString("O"))).ToUniversalTime();
        var errors = SessionState.GetString(ErrorKey, "");
        if (!string.IsNullOrEmpty(errors)) { FinishSmoke(new[] { errors }); return; }
        if (elapsed.TotalSeconds > 180)
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
            var titleLayer = FindActiveTitle();
            var title = titleLayer != null && titleLayer.gameObject.activeInHierarchy && !titleLayer.IsClosing;
            var runs = ReadFightRuns();
            if (runs.items.Count == 1 && !fight && !title) SessionState.SetBool(ReentryKey, true);
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
                if (PosCal.Canvas == null || PosCal.SafeAreaRect == null ||
                    PosCal.SafeAreaRect == PosCal.Canvas.transform ||
                    PosCal.SafeAreaRect.parent != PosCal.Canvas.transform)
                {
                    FinishSmoke(new[] { "Battle UI must use a distinct safe-area child of the canvas." });
                    return;
                }
                // EditorApplication.update can expose the editor panel size via
                // Screen; the live overlay canvas tracks the rendered viewport.
                var viewport = PosCal.Canvas.pixelRect;
                if (viewport.width <= 0 || viewport.width >= viewport.height)
                {
                    FinishSmoke(new[] { $"Startup viewport must remain portrait; found {viewport.width}x{viewport.height}." });
                    return;
                }
                var ready = SessionState.GetString(ReadyKey, "");
                if (ready == "")
                {
                    if (runs.items.Count == 1 && !SessionState.GetBool(ReentryKey, false))
                    {
                        FinishSmoke(new[] { "Fight reload did not leave the original battle/title process." });
                        return;
                    }
                    if (!SessionState.GetBool(PoolCheckKey, false))
                    {
                        try { CheckPoolLifecycle(); SessionState.SetBool(PoolCheckKey, true); }
                        catch (Exception exception) { FinishSmoke(new[] { exception.ToString() }); return; }
                    }
                    if (!SessionState.GetBool(DownloadCheckKey, false))
                    {
                        try { CheckDownloadPresentation(); SessionState.SetBool(DownloadCheckKey, true); }
                        catch (Exception exception) { FinishSmoke(new[] { exception.ToString() }); return; }
                    }
                    SessionState.SetString(ReadyKey, DateTime.UtcNow.ToString("O"));
                    SessionState.SetInt(ReadyFrameKey, Time.frameCount);
                    Debug.Log($"POCKETSTRIKER_SMOKE_READY run={runs.items.Count + 1}, frame={Time.frameCount}");
                }
                else if ((DateTime.UtcNow - DateTime.Parse(ready).ToUniversalTime()).TotalSeconds >= 20)
                {
                    var firstFrame = SessionState.GetInt(ReadyFrameKey, Time.frameCount);
                    if (Time.frameCount <= firstFrame || Time.timeScale <= 0)
                    {
                        FinishSmoke(new[] { "Battle frames did not advance during smoke validation." });
                        return;
                    }
                    runs.items.Add(new FightRunReport
                    {
                        run = runs.items.Count + 1,
                        team1 = RTFightManager.Target.team1.teamMembers.GetValues().Count,
                        team2 = RTFightManager.Target.team2.teamMembers.GetValues().Count,
                        startFrame = firstFrame,
                        endFrame = Time.frameCount,
                        stableSeconds = (DateTime.UtcNow - DateTime.Parse(ready).ToUniversalTime()).TotalSeconds
                    });
                    SessionState.SetString(RunsKey, JsonUtility.ToJson(runs));
                    if (runs.items.Count >= 2) FinishSmoke(Array.Empty<string>());
                    else
                    {
                        SessionState.SetString(ReadyKey, "");
                        Debug.Log("POCKETSTRIKER_SMOKE_REENTER requesting another screensaver battle.");
                        FightLoad.Go(FightLoad.Fight, inSceneLoad: true);
                        // Preparing can finish before the next editor update when assets are cached.
                        var leftBattle = !(FSceneProcessesRunner.Main.currentProcess is FightingProcess);
                        var previousTitle = FindActiveTitle();
                        SessionState.SetBool(ReentryKey, leftBattle && (previousTitle == null || previousTitle.IsClosing));
                    }
                    return;
                }
            }
            else SessionState.SetString(ReadyKey, "");
        }
    }

    static TitleScreenLayer FindActiveTitle() => UnityEngine.Object
        .FindObjectsByType<TitleScreenLayer>(FindObjectsSortMode.None)
        .FirstOrDefault(layer => layer != null && !layer.IsClosing && layer.gameObject.activeInHierarchy);

    static FightRuns ReadFightRuns() => JsonUtility.FromJson<FightRuns>(SessionState.GetString(RunsKey, "{}")) ?? new FightRuns();

    static void CheckPoolLifecycle()
    {
        var prefab = new GameObject("PocketStriker pool lifecycle fixture");
        var prototype = prefab.AddComponent<Decomposition>();
        prototype.to_be_faded_renderers = new List<MeshRenderer>();
        prefab.SetActive(false);
        var pool = new DecompositionPool(prefab);
        Decomposition first = null, second = null;
        try
        {
            first = pool.Rent();
            var resolve = typeof(Decomposition).GetMethod("EnergyResolve", BindingFlags.Instance | BindingFlags.NonPublic);
            if (resolve == null) throw new InvalidOperationException("Missing pooled effect lifecycle method.");
            resolve.Invoke(first, null);
            resolve.Invoke(first, null);
            if (pool.Count != 1) throw new InvalidOperationException("Returning an effect twice duplicated its pool entry.");
            first = pool.Rent();
            second = pool.Rent();
            if (first == second) throw new InvalidOperationException("Two active pool rentals share the same effect instance.");
            if (first.Phase != 1 || second.Phase != 1)
                throw new InvalidOperationException("A rented effect did not restart its lifetime.");
        }
        finally
        {
            // Fixtures never reach Update, so they never register with the live effect processor.
            pool.Dispose();
            if (first != null) UnityEngine.Object.DestroyImmediate(first.gameObject);
            if (second != null && second != first) UnityEngine.Object.DestroyImmediate(second.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
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
        BuildLocalPlayer(BuildTarget.StandaloneOSX, "Builds/Revival/PocketStriker.app", "build-mac");
    }

    public static void BuildIOS()
    {
        BuildLocalPlayer(BuildTarget.iOS, "Builds/Revival/iOS", "build-ios");
    }

    static void BuildLocalPlayer(BuildTarget target, string path, string check)
    {
        RequireSavedScenes();
        RequireEnvironment();
        if (EditorUserBuildSettings.activeBuildTarget != target)
            throw new InvalidOperationException($"Start Unity with -buildTarget {target} before building this player.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings are missing.");
        var originalProfile = settings.activeProfileId;
        var originalRemoteCatalog = settings.BuildRemoteCatalog;
        var originalBuildOption = settings.BuildAddressablesWithPlayerBuild;
        var localProfile = settings.profileSettings.AddProfile("Local validation " + Guid.NewGuid(), originalProfile);
        var errors = new List<string>();
        var buildErrors = new List<string>();
        void CaptureBuildError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            lock (buildErrors)
            {
                if (buildErrors.Count < 100) buildErrors.Add(type + ": " + message + "\n" + stack);
            }
        }
        Application.logMessageReceivedThreaded += CaptureBuildError;
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
                target = target,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Player build failed: " + report.summary.result);
            if (target == BuildTarget.iOS) ValidateUnsignedIOSExport(path);
        }
        catch (Exception exception)
        {
            errors.Add(exception.ToString());
            throw;
        }
        finally
        {
            Application.logMessageReceivedThreaded -= CaptureBuildError;
            lock (buildErrors) errors.AddRange(buildErrors);
            settings.activeProfileId = originalProfile;
            settings.BuildRemoteCatalog = originalRemoteCatalog;
            settings.BuildAddressablesWithPlayerBuild = originalBuildOption;
            settings.profileSettings.RemoveProfile(localProfile);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            WriteReport(check, errors, EditorBuildSettings.scenes.Count(scene => scene.enabled), 0, Path.GetFullPath(path));
        }
        if (errors.Count > 0)
            throw new InvalidOperationException($"Unity logged {errors.Count} build error(s), even though BuildReport returned Succeeded. See the validation report.");
        Debug.Log("PocketStriker player build succeeded: " + Path.GetFullPath(path));
    }

    static void ValidateUnsignedIOSExport(string path)
    {
#if UNITY_IOS
        var projectPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(path);
        var plistPath = Path.Combine(path, "Info.plist");
        if (!File.Exists(projectPath) || !File.Exists(plistPath) || !Directory.Exists(Path.Combine(path, "Data")))
            throw new InvalidOperationException("The iOS export is missing its Xcode project, Info.plist, or player data.");
        var plist = new UnityEditor.iOS.Xcode.PlistDocument();
        plist.ReadFromFile(plistPath);
        foreach (var key in new[] { "UISupportedInterfaceOrientations", "UISupportedInterfaceOrientations~ipad" })
        {
            if (!plist.root.values.TryGetValue(key, out var element))
            {
                if (key == "UISupportedInterfaceOrientations")
                    throw new InvalidOperationException("The exported iOS app has no supported screen orientation.");
                continue;
            }
            var orientations = element.AsArray().values;
            if (orientations.Count == 0 || orientations.Any(value => value.AsString() != "UIInterfaceOrientationPortrait"))
                throw new InvalidOperationException("The exported iOS app must support only Portrait: " + key);
        }
        // This verification produces an unsigned export. It does not invoke Xcode,
        // request provisioning profiles, archive an IPA, or change signing settings.
        var project = new UnityEditor.iOS.Xcode.PBXProject();
        project.ReadFromFile(projectPath);
        ValidateAppleSignInExport(path, project);
        foreach (var target in new[] { project.GetUnityMainTargetGuid(), project.GetUnityFrameworkTargetGuid() })
        {
            project.SetBuildProperty(target, "CODE_SIGNING_ALLOWED", "NO");
            project.SetBuildProperty(target, "CODE_SIGNING_REQUIRED", "NO");
            project.SetBuildProperty(target, "CODE_SIGN_IDENTITY", "");
            project.SetBuildProperty(target, "DEVELOPMENT_TEAM", "");
            project.SetBuildProperty(target, "PROVISIONING_PROFILE_SPECIFIER", "");
            project.SetBuildProperty(target, "PROVISIONING_PROFILE", "");
        }
        project.WriteToFile(projectPath);
#else
        throw new InvalidOperationException("The iOS export check must run with iOS as the active build target.");
#endif
    }

#if UNITY_IOS
    static void ValidateAppleSignInExport(string path, UnityEditor.iOS.Xcode.PBXProject project)
    {
        var mainTarget = project.GetUnityMainTargetGuid();
        var entitlementFile = project.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");
        if (string.IsNullOrWhiteSpace(entitlementFile))
            throw new InvalidOperationException("The iOS app target has no Sign in with Apple entitlements file.");
        var entitlementPath = Path.Combine(path, entitlementFile.Trim('"'));
        if (!File.Exists(entitlementPath))
            throw new InvalidOperationException("The exported Apple entitlements file is missing: " + entitlementFile);
        var entitlements = new UnityEditor.iOS.Xcode.PlistDocument();
        entitlements.ReadFromFile(entitlementPath);
        if (!entitlements.root.values.TryGetValue("com.apple.developer.applesignin", out var signIn) ||
            !signIn.AsArray().values.Any(value => value.AsString() == "Default"))
            throw new InvalidOperationException("The exported app lacks the Default Sign in with Apple entitlement.");
        if (!project.ContainsFramework(project.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework"))
            throw new InvalidOperationException("UnityFramework does not link AuthenticationServices.framework.");

        // Unity's public AddSignInWithApple helper binds this entitlement without
        // emitting a SystemCapabilities display marker in current Xcode projects.
    }
#endif

    public static void CompilePlayer()
    {
        RequireEnvironment();
        var target = EditorUserBuildSettings.activeBuildTarget;
        var errors = new List<string>();
        var outputPath = "Library/RevivalPlayerScripts/" + target;
        try
        {
            var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
            {
                target = target,
                group = BuildPipeline.GetBuildTargetGroup(target),
                options = ScriptCompilationOptions.DevelopmentBuild
            }, outputPath);
            if (result.assemblies == null || !result.assemblies.Any())
                throw new InvalidOperationException("Player script compilation produced no assemblies.");
            Debug.Log($"PocketStriker {target} player script compilation passed.");
        }
        catch (Exception exception)
        {
            errors.Add(exception.ToString());
            throw;
        }
        finally
        {
            WriteReport("compile", errors, 0, 0, Path.GetFullPath(outputPath));
        }
    }

    static void WriteReport(string check, IEnumerable<string> errors, int scenes, int entries, string outputPath = null)
    {
        var errorArray = errors.ToArray();
        Directory.CreateDirectory(ReportDirectory);
        var json = JsonUtility.ToJson(new ValidationReport
        {
            unityVersion = Application.unityVersion,
            expectedUnityVersion = ExpectedUnityVersion,
            activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
            defaultOrientation = PlayerSettings.defaultInterfaceOrientation.ToString(),
            runtimeOrientation = Screen.orientation.ToString(),
            screenWidth = EditorApplication.isPlaying && PosCal.Canvas != null
                ? Mathf.RoundToInt(PosCal.Canvas.pixelRect.width) : Screen.width,
            screenHeight = EditorApplication.isPlaying && PosCal.Canvas != null
                ? Mathf.RoundToInt(PosCal.Canvas.pixelRect.height) : Screen.height,
            defaultScreenWidth = PlayerSettings.defaultScreenWidth,
            defaultScreenHeight = PlayerSettings.defaultScreenHeight,
            portraitOnly = PortraitOnly,
            outputPath = outputPath,
            check = check,
            passed = errorArray.Length == 0,
            scenes = scenes,
            addressableEntries = entries,
            errors = errorArray,
            fightRuns = check == "startup" ? ReadFightRuns().items.ToArray() : Array.Empty<FightRunReport>(),
            reentryObserved = check == "startup" && SessionState.GetBool(ReentryKey, false),
            poolLifecyclePassed = check == "startup" && SessionState.GetBool(PoolCheckKey, false),
            downloadPresentationPassed = check == "startup" && SessionState.GetBool(DownloadCheckKey, false)
        }, true);
        File.WriteAllText(Path.Combine(ReportDirectory, check + "-report.json"), json);
        File.WriteAllText(Path.Combine(ReportDirectory, check + "-" + EditorUserBuildSettings.activeBuildTarget + "-report.json"), json);
    }
}
