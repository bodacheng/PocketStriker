using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using FightScene;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local Play-mode smoke using the production fight scene, loaders, HUD and camera.</summary>
[InitializeOnLoad]
public static partial class PocketStrikerBattleCameraSmoke
{
    const string Key = "PocketStriker.CameraSmoke";
    static string Output => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POCKETSTRIKER_IMPACT_REVIEW"))
        ? "Logs/CombatImpact/" + Environment.GetEnvironmentVariable("POCKETSTRIKER_IMPACT_REVIEW")
        : string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POCKETSTRIKER_CAMERA_REVIEW"))
        ? "Logs/CameraFraming/Playmode"
        : "Logs/CameraFraming/Transitions/" + Environment.GetEnvironmentVariable("POCKETSTRIKER_CAMERA_REVIEW");
    static bool finishing;
    static Report report;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string utcTime;
        public string scope = "Play mode in the actual FightScene through Addressables Fast Mode and FightLoad.Go. Production Preparing/CountDown/Fighting, RTFightManager, live animated models, FightingStepLayer and CameraManager. Duel 1v1, Rotation 3v3 (only current fighters), MultiRaid 3v3, Group 100v100 and production in-scene Group retry. Each first CountDown view must immediately fit the final fielded models at its independent Duel32/Multi33/Group46 pitch, without inherited staging distance. Fixture then moves active models around the arena and changes ordinary focus; each sampled frame projects complete visible renderer bounds through the actual camera.";
        public string limitation = "Local Self event bypasses login, rewards, advertising and AI story requests. A fixture sceneLoaded hook disables and destroys shop IAP startup objects before Start in every scene, cancelling their login subscriptions; production IAP code remains unchanged. Group mechanics still use GangbangInfo/IsGroupBattle. This is editor Play mode, not a physical-device performance test.";
        public int isolatedIAPObjects;
        public List<string> weaponLifecycleChecks = new List<string>();
        public List<StartCase> starts = new List<StartCase>();
        public List<Case> cases = new List<Case>();
        public List<TransitionCase> transitions = new List<TransitionCase>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public sealed class Case
    {
        public string name;
        public int loadedTeam1;
        public int loadedTeam2;
        public int minimumFielded;
        public int frames;
        public int projectedCorners;
        public int focusChanges;
        public float fieldOfView;
        public float aspect;
        public float pitch;
        public Rect usableViewport;
        public string screenshot;
    }

    [Serializable]
    public sealed class StartCase
    {
        public string name;
        public string process;
        public int framedUnits;
        public float pitch;
        public float distance;
        public float requiredDistance;
        public float centerLag;
        public string screenshot;
    }

    static PocketStrikerBattleCameraSmoke()
    {
        if (SessionState.GetBool(Key, false)) Attach();
    }

    [MenuItem("PocketStriker/Validation/Battle Camera Playmode Smoke")]
    public static void StartBatch()
    {
        finishing = false;
        report = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start camera smoke from a stopped editor.");
        if (!Application.isBatchMode)
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before camera smoke.");
        PocketStrikerGroupFormationValidation.ValidateOrThrow();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings are missing.");
        int fast = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
        if (fast < 0) throw new InvalidOperationException("Addressables Fast Mode builder is missing.");
        SessionState.SetInt(Key + ".Builder", settings.ActivePlayModeDataBuilderIndex);
        SessionState.SetBool(Key + ".HadAuto", PlayerPrefs.HasKey("auto"));
        SessionState.SetInt(Key + ".Auto", PlayerPrefs.GetInt("auto", 0));
        SessionState.SetBool(Key + ".HadOrbit", PlayerPrefs.HasKey("AutoRotateCamera"));
        SessionState.SetInt(Key + ".Orbit", PlayerPrefs.GetInt("AutoRotateCamera", 1));
        PlayerPrefs.SetInt("AutoRotateCamera", 1);
        SessionState.SetString(Key + ".Start", DateTime.UtcNow.ToString("O"));
        SessionState.SetString(Key + ".Errors", "");
        SessionState.SetInt(Key + ".IAPObjects", 0);
        SessionState.SetBool(Key + ".Running", false);
        SessionState.SetBool(Key, true);
        settings.ActivePlayModeDataBuilderIndex = fast;
        ConfigurePortraitGameView();
        Attach();
        EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(scene => scene.enabled).path, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        Application.logMessageReceived -= CaptureError;
        Application.logMessageReceived += CaptureError;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        SceneManager.sceneLoaded -= IsolateLoadedSceneServices;
        SceneManager.sceneLoaded += IsolateLoadedSceneServices;
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        var errors = SessionState.GetString(Key + ".Errors", "");
        if (errors.Length < 24000) SessionState.SetString(Key + ".Errors", errors + message + "\n" + stack + "\n");
    }

    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        var errors = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(errors)) { Finish(errors); return; }
        var started = DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime();
        if ((DateTime.UtcNow - started).TotalSeconds > 600)
        {
            Finish("Camera smoke timeout: scene=" + SceneManager.GetActiveScene().name
                + ", process=" + FSceneProcessesRunner.Main.currentProcess?.GetType().Name
                + ", config=" + Starter.ConfigInitialised + ", teams="
                + RTFightManager.Target?.team1?.teamMembers?.mDict.Count + "/"
                + RTFightManager.Target?.team2?.teamMembers?.mDict.Count);
            return;
        }
        if (!EditorApplication.isPlaying || !Starter.ConfigInitialised || SessionState.GetBool(Key + ".Running", false)) return;
        var title = UnityEngine.Object.FindObjectsByType<TitleScreenLayer>(FindObjectsSortMode.None)
            .Any(layer => layer != null && layer.gameObject.activeInHierarchy && !layer.IsClosing);
        bool startupReady = title && (SceneManager.GetActiveScene().buildIndex == 0
            || FSceneProcessesRunner.Main.currentProcess is FightingProcess);
        if (!startupReady) return;
        SessionState.SetBool(Key + ".Running", true);
        Run().Forget();
    }

    static async UniTask Run()
    {
        report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var oldAccount = PlayerAccountInfo.Me;
        var oldLanguage = AppSetting.Value.Language;
        try
        {
            await IsolateLocalAccountServices();
            PlayerAccountInfo.Me = new PlayerAccountInfo
                { PlayFabId = "local-camera-smoke", tutorialProgress = "Finished", noAdsState = true };
            AppSetting.Value.Language = SystemLanguage.Chinese;
            PlayerPrefs.SetInt("auto", 1);
            Directory.CreateDirectory(Output);
            // Read the actually registered adventure asset, not a same-named file.
            var manager = new ArcadeModeManager();
            await manager.Initialize().Timeout(TimeSpan.FromSeconds(30));
            var authored = await manager.LoadStage(4).Timeout(TimeSpan.FromSeconds(30));
            Require(authored is GangbangInfo && authored.UnitsData.Count > 0, "Published Group stage 4 did not load.");
            var leader = authored.UnitsData[0].DeepCopy();
            UnityEngine.Object.Destroy(authored);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POCKETSTRIKER_IMPACT_REVIEW")))
                await ReviewImpact(leader);
            else if (Environment.GetEnvironmentVariable("POCKETSTRIKER_CAMERA_HANDOFF") == "1")
                await ReviewHandoffs(leader);
            else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POCKETSTRIKER_CAMERA_REVIEW")))
                await ReviewTransitions(leader);
            else
            {
                var duel = CreateOrdinary(leader, TeamMode.Rotation, 1);
                FightLoad.Go(duel);
                await WaitReady(1, 1, false, "duel");
                ValidateWeaponLifecycles();
                await Observe("duel", false, 1, 1);

                var rotation = CreateOrdinary(leader, TeamMode.Rotation);
                FightLoad.Go(rotation);
                await WaitReady(3, 3, false, "rotation");
                await Observe("rotation", false, 3, 3);

                var multi = CreateOrdinary(leader, TeamMode.MultiRaid);
                FightLoad.Go(multi);
                await WaitReady(3, 3, false, "multiraid");
                await Observe("multiraid", false, 3, 3);

                var group = CreateGroup(leader);
                FightLoad.Go(group);
                await WaitReady(100, 100, true, "group-200");
                await Observe("group-200", true, 100, 100);

                var before = RTFightManager.Target;
                FightLoad.Go(FightLoad.Fight, true);
                await UniTask.NextFrame();
                await WaitReady(100, 100, true, "group-200-retry");
                Require(RTFightManager.Target == before, "Production in-scene retry unexpectedly replaced the fight scene manager.");
                await Observe("group-200-retry", true, 100, 100);
                Require(report.cases.Count == 5 && report.starts.Count == 5
                    && report.cases.All(item => item.frames >= 10 && item.projectedCorners > 0),
                    "Play-mode camera checks did not exercise all five starts and live cases.");
            }
            report.passed = true;
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            PlayerAccountInfo.Me = oldAccount;
            AppSetting.Value.Language = oldLanguage;
            Finish(null);
        }
    }

    static async UniTask IsolateLocalAccountServices()
    {
        // UniRx AddTo(Component) attaches its disposable to that component's
        // GameObject destroy trigger. Disabling or destroying just IAPManager
        // would leave the account-ready callback subscribed.
        RemoveShopStartupObjects();
        await UniTask.NextFrame();
        Require(UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include).Length == 0,
            "Shop startup subscriptions were not removed before installing the local account.");
    }

    static void IsolateLoadedSceneServices(Scene scene, LoadSceneMode mode)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
            RemoveShopStartupObjects();
    }

    static void RemoveShopStartupObjects()
    {
        // sceneLoaded runs after Awake/OnEnable and before Start. Disabling here
        // prevents a newly loaded shop from starting with the fixture account.
        var shops = UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include);
        foreach (var shop in shops)
        {
            shop.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(shop.gameObject);
        }
        IAPManager.Target = null;
        int removed = SessionState.GetInt(Key + ".IAPObjects", 0) + shops.Length;
        SessionState.SetInt(Key + ".IAPObjects", removed);
        if (report != null) report.isolatedIAPObjects = removed;
    }

    static void ValidateWeaponLifecycles()
    {
        var owner = RTFightManager.Target.team1.RMode_Unit.Value;
        var poolField = typeof(HurtObjectManager).GetField("_defaultHitBoxPool", BindingFlags.Static | BindingFlags.NonPublic);
        var partsField = typeof(BO_Weapon_Animation_Events).GetField("bodyPartsHitBoxRegisterDic", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(owner != null && HurtObjectManager.GetDPool() != null && poolField != null && partsField != null,
            "Cannot validate weapon events without a fully loaded live fighter and hitbox pool.");
        var fixture = new GameObject("CameraSmokeWeaponLifecycle");
        var bone = new GameObject("LocalFoot");
        bone.transform.SetParent(fixture.transform);
        var events = fixture.AddComponent<BO_Weapon_Animation_Events>();
        var pool = HurtObjectManager.GetDPool();
        Decomposition rented = null;
        try
        {
            events.SetLeftFootMarkerManager(1);
            events.SetLeftFootMarkerManager(0);
            events.ClearTargets(); events.ClearMarkerManagers(); events.EnableMarkers(); events.DisableMarkers();
            events.SetAllBodyMarkerManagersIn(); events.SetDamageType(new AnimationEvent { intParameter = 2 });
            Require(partsField.GetValue(events) == null, "Uninitialized animation events created hitboxes.");
            report.weaponLifecycleChecks.Add("Events before weapon assignment safely ignore the uninitialized model.");

            events.hiddenMethods.AssignWeaponsFromDataCenter(owner.FightDataRef, owner.geometryCenter,
                null, null, null, bone.transform, null, null);
            var parts = (IDictionary<Transform, Decomposition>)partsField.GetValue(events);
            poolField.SetValue(null, null);
            events.SetLeftFootMarkerManager(1);
            events.SetLeftFootMarkerManager(0);
            Require(parts[bone.transform] == null, "A transition without a pool attempted to rent a hitbox.");
            poolField.SetValue(null, pool);
            report.weaponLifecycleChecks.Add("Events between cache clear and pool construction do not rent or dereference a missing hitbox.");

            events.SetLeftFootMarkerManager(2);
            rented = parts[bone.transform];
            Require(rented != null && rented._HitBox.GetOwnerFACR() == owner.FightDataRef
                && rented._HitBox.damage_type == HittingDetection.V_Damage.FormalIntToDamageType(2)
                && rented._HitBox.Enabled && rented.GetPositionConstraint().constraintActive,
                "Valid initialized animation event did not preserve hitbox owner, damage, markers and body constraint.");
            events.DisableMarkers(); Require(!rented._HitBox.Enabled, "Marker disabling stopped working.");
            events.EnableMarkers(); Require(rented._HitBox.Enabled, "Marker enabling stopped working.");
            events.SetLeftFootMarkerManager(0);
            Require(parts[bone.transform] == null && rented.Phase == -1
                && !rented.GetPositionConstraint().constraintActive, "Normal marker removal stopped working.");
            report.weaponLifecycleChecks.Add("Initialized events still rent, bind owner/damage/constraint, enable/disable markers and retire the hitbox.");

            UnityEngine.Object.DestroyImmediate(bone);
            events.SetLeftFootMarkerManager(1); events.SetLeftFootMarkerManager(0);
            report.weaponLifecycleChecks.Add("A destroyed bone safely ignores late animation callbacks.");
        }
        finally
        {
            poolField.SetValue(null, pool);
            if (rented != null)
                typeof(Decomposition).GetMethod("EnergyResolve", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(rented, null);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    static FightInfo CreateOrdinary(UnitInfo leader, TeamMode mode, int count = 3)
    {
        var fight = ScriptableObject.CreateInstance<FightInfo>();
        Setup(fight, mode);
        fight.FightMembers = Members(leader, count);
        return fight;
    }

    static GangbangInfo CreateGroup(UnitInfo leader)
    {
        var fight = ScriptableObject.CreateInstance<GangbangInfo>();
        Setup(fight, TeamMode.MultiRaid);
        fight.FightMembers = Members(leader);
        fight.ApplyTeamLimit(100);
        fight.ConvertTeamToGangbang();
        Require(fight.FightMembers.HeroSets.mDict.Count == 100 && fight.FightMembers.EnemySets.mDict.Count == 100,
            "Group fixture did not expand to 100 fighters per team.");
        return fight;
    }

    static void Setup(FightInfo fight, TeamMode mode)
    {
        fight.ID = "camera-smoke";
        fight.EventType = FightEventType.Self;
        fight.team1Mode = fight.team2Mode = mode;
        fight.Team1ID = "local-camera-smoke";
        fight.Team2ID = "local-camera-smoke-enemy";
        fight.battleGroundID = 0;
        fight.team1CGMode = fight.team2CGMode = CriticalGaugeMode.Normal;
    }

    static FightMembers Members(UnitInfo leader, int count = 3)
    {
        var members = new FightMembers();
        for (var index = 0; index < count; index++)
        {
            var hero = leader.DeepCopy(); hero.id = index.ToString();
            var enemy = leader.DeepCopy(); enemy.id = index.ToString();
            members.HeroSets.Set(0, index, hero);
            members.EnemySets.Set(0, index, enemy);
        }
        return members;
    }

    static bool IsLoaded(int team1, int team2, bool group)
        => SceneManager.GetActiveScene().name == "FightScene"
            && FightLoad.Fight != null && FightLoad.Fight.EventType == FightEventType.Self
            && FightLoad.Fight.IsGroupBattle == group
            && RTFightManager.Target?.team1?.teamMembers?.mDict.Count == team1
            && RTFightManager.Target?.team2?.teamMembers?.mDict.Count == team2
            && RTFightManager.Target.team1.TeamMode == FightLoad.Fight.team1Mode
            && RTFightManager.Target.team2.TeamMode == FightLoad.Fight.team2Mode
            && GetHUD() != null && GetHUD().Initialized;

    static async UniTask WaitReady(int team1, int team2, bool group, string name)
    {
        try
        {
            await UniTask.WaitUntil(() => IsLoaded(team1, team2, group)
                && FSceneProcessesRunner.Main.currentProcess is CountDownProcess)
            .Timeout(TimeSpan.FromSeconds(180));
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            await CheckCountDownStart(name, group, team1, team2);
            await UniTask.WaitUntil(() => IsLoaded(team1, team2, group)
                && FSceneProcessesRunner.Main.currentProcess is FightingProcess)
                .Timeout(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException exception)
        {
            var manager = RTFightManager.Target;
            var hud = GetHUD();
            throw new TimeoutException("Battle load timeout: expected=" + team1 + "/" + team2 + ", group=" + group
                + ", scene=" + SceneManager.GetActiveScene().name
                + ", actualProcess=" + FSceneProcessesRunner.Main.currentProcess?.GetType().Name
                + ", loaded=" + manager?.team1?.teamMembers?.mDict.Count + "/" + manager?.team2?.teamMembers?.mDict.Count
                + ", HUDInitialized=" + (hud != null && hud.Initialized)
                + ", loadFinished=" + FightScene.FightScene.target?.LoadStageFinished.Value, exception);
        }
        Require(UnityEngine.Object.FindObjectsByType<IAPManager>(FindObjectsInactive.Include).Length == 0,
            "A shop startup object survived a battle load or retry in the local fixture.");
        RTFightManager.Target.team1.TurnAllUnitsInvincible(true);
        RTFightManager.Target.team2.TurnAllUnitsInvincible(true);
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
    }

    static BattleCameraProfile ExpectedProfile(bool group, UnitsManger first, UnitsManger second)
    {
        var key = BattleCameraProfiles.ResolveMode(group, first.TeamMode, second.TeamMode);
        return key == C_Mode.TopDown ? BattleCameraProfiles.Group
            : key == C_Mode.CertainYAntiVibration ? BattleCameraProfiles.Duel : BattleCameraProfiles.MultiRaid;
    }

    static void CheckLiveProfile(AllUnitsBattleCamera mode, Camera camera, bool group, UnitsManger first, UnitsManger second)
    {
        var profile = ExpectedProfile(group, first, second);
        Require(mode != null && (group ? mode is GroupBattleCamera
            : first.TeamMode == TeamMode.Rotation && second.TeamMode == TeamMode.Rotation
                ? mode is DuelBattleCamera : mode is MultiRaidBattleCamera), "Wrong independent battle camera class.");
        Require(Mathf.Abs(mode.Pitch - profile.Pitch) < .001f
            && Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x, profile.Pitch)) < .01f
            && Mathf.Abs(camera.fieldOfView - profile.FieldOfView) < .001f,
            "Live battle does not apply the intended pitch/lens profile.");
    }

    static async UniTask CheckCountDownStart(string name, bool group, int expected1, int expected2)
    {
        var manager = RTFightManager.Target;
        var camera = CameraManager._camera;
        var mode = manager._CameraManager.CurrentBattleCamera;
        Require(FSceneProcessesRunner.Main.currentProcess is CountDownProcess,
            "Initial battle framing was observed after CountDown.");
        CheckLiveProfile(mode, camera, group, manager.team1, manager.team2);
        int required = manager.team1.TeamMode == TeamMode.Rotation ? 2 : expected1 + expected2;
        Require(mode.IsFramingInitialized && mode.FramedUnitCount == required,
            "CountDown did not immediately initialize its actual fielded fighters.");
        var entry = new StartCase { name = name, process = "CountDownProcess", framedUnits = mode.FramedUnitCount,
            pitch = mode.Pitch, distance = mode.CurrentPose.Distance, requiredDistance = mode.DesiredPose.Distance,
            centerLag = Vector3.Distance(mode.CurrentPose.Center, mode.DesiredPose.Center) };
        report.starts.Add(entry);
        Require(entry.distance <= entry.requiredDistance + .15f && entry.centerLag < .1f,
            "CountDown camera inherited remote staging/previous fight framing: " + name
            + ", distance=" + entry.distance + ", required=" + entry.requiredDistance + ", centerLag=" + entry.centerLag);
        var corners = new Case { name = name + "-countdown" };
        var usable = mode.GetUsableViewport(camera);
        Require(CheckTeam(manager.team1, camera, usable, corners) + CheckTeam(manager.team2, camera, usable, corners) == required,
            "CountDown camera did not show all actual fielded models.");
        entry.screenshot = Path.GetFullPath(Path.Combine(Output, name + "-countdown.png"));
        ScreenCapture.CaptureScreenshot(entry.screenshot);
        await UniTask.Delay(100, DelayType.Realtime);
        Require(File.Exists(entry.screenshot), "CountDown screenshot was not saved: " + name);
        Debug.Log("[CameraSmoke] " + name + " CountDown: pitch=" + entry.pitch + ", distance=" + entry.distance
            + ", required=" + entry.requiredDistance + ", centerLag=" + entry.centerLag + ", fighters=" + entry.framedUnits);
    }

    static async UniTask Observe(string name, bool group, int expected1, int expected2)
    {
        var manager = RTFightManager.Target;
        var layer = GetHUD();
        var camera = CameraManager._camera;
        Require(manager != null && layer != null && camera != null, "Live camera/HUD/manager is missing.");
        var item = new Case { name = name, loadedTeam1 = manager.team1.teamMembers.mDict.Count,
            loadedTeam2 = manager.team2.teamMembers.mDict.Count, minimumFielded = int.MaxValue };
        report.cases.Add(item);
        Require(item.loadedTeam1 == expected1 && item.loadedTeam2 == expected2, "Wrong live fighter count in " + name);
        var mode = manager._CameraManager.CurrentBattleCamera;
        CheckLiveProfile(mode, camera, group, manager.team1, manager.team2);
        var start = Time.realtimeSinceStartup;
        bool focused = false;
        Data_Center requestedFocus = null;
        while (Time.realtimeSinceStartup - start < 5)
        {
            Require(FSceneProcessesRunner.Main.currentProcess is FightingProcess, "Live fight ended during camera smoke.");
            float elapsed = Time.realtimeSinceStartup - start;
            MoveFielded(manager.team1, 0, elapsed, group);
            MoveFielded(manager.team2, 1, elapsed, group);
            if (!group && expected1 > 1 && !focused && elapsed > 1.5f)
            {
                requestedFocus = manager.team1.teamMembers.mDict.Values.First(unit => unit != layer.InputsManager.CurrentFocus.Value);
                if (manager.team1.TeamMode == TeamMode.Rotation)
                    manager.team1.ReadyForNextMember(requestedFocus);
                else
                {
                    layer.InputsManager.FocusUnit(requestedFocus);
                    manager.CameraAdjustment(RTFightManager.playerTeam, manager.team1.TeamMode,
                        FightLoad.Fight.EventType, requestedFocus.geometryCenter);
                }
                focused = true;
            }
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            if (requestedFocus != null && layer.InputsManager.CurrentFocus.Value == requestedFocus && item.focusChanges == 0)
            {
                Require(manager.team1.TeamMode != TeamMode.Rotation || manager.team1.RMode_Unit.Value == requestedFocus,
                    "Rotation focus changed without a production fighter switch.");
                item.focusChanges++;
            }
            var usable = mode.GetUsableViewport(camera);
            item.usableViewport = usable; item.fieldOfView = camera.fieldOfView; item.aspect = camera.aspect;
            item.pitch = mode.Pitch;
            int count = CheckTeam(manager.team1, camera, usable, item) + CheckTeam(manager.team2, camera, usable, item);
            int required = manager.team1.TeamMode == TeamMode.Rotation ? 2 : expected1 + expected2;
            Require(count == required, name + " is missing fielded fighters: " + count + " vs " + required);
            item.minimumFielded = Math.Min(item.minimumFielded, count); item.frames++;
        }
        Require(group || expected1 == 1 || item.focusChanges == 1, "Ordinary focus did not change in " + name);
        item.screenshot = Path.GetFullPath(Path.Combine(Output, name + ".png"));
        ScreenCapture.CaptureScreenshot(item.screenshot);
        await UniTask.Delay(500, DelayType.Realtime);
        Require(File.Exists(item.screenshot), "Screenshot was not saved for " + name);
        Debug.Log("[CameraSmoke] " + name + ": " + item.frames + " live frames, " + item.minimumFielded
            + " fielded fighters, " + item.projectedCorners + " complete model corners visible.");
    }

    static void MoveFielded(UnitsManger team, int teamIndex, float elapsed, bool group)
    {
        int index = 0;
        foreach (var unit in team.teamMembers.mDict.Values)
        {
            if (!Fielded(team, unit)) continue;
            float phase = index * 2.399963f + teamIndex * Mathf.PI + elapsed * 0.08f;
            float radius = group ? 3 + 13 * Mathf.Sqrt((index + 1f) / 100) : 10 + index;
            var position = unit.WholeT.position;
            position.x = Mathf.Cos(phase) * radius + Mathf.Sin(elapsed) * 0.6f;
            position.z = Mathf.Sin(phase) * radius;
            unit.WholeT.position = position;
            index++;
        }
    }

    static int CheckTeam(UnitsManger team, Camera camera, Rect usable, Case item)
    {
        int count = 0;
        foreach (var unit in team.teamMembers.mDict.Values)
        {
            if (!Fielded(team, unit)) continue;
            Require(BattleCameraFraming.TryGetModelBounds(unit.WholeT, out var bounds), "Live model has no visible mesh bounds: " + unit.name);
            for (var corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var viewport = camera.WorldToViewportPoint(point);
                const float tolerance = 0.002f;
                Require(viewport.z > camera.nearClipPlane && viewport.x >= usable.xMin - tolerance && viewport.x <= usable.xMax + tolerance
                    && viewport.y >= usable.yMin - tolerance && viewport.y <= usable.yMax + tolerance,
                    item.name + " live model corner clipped: " + unit.UnitInfo?.id + "/" + corner + " " + viewport + " vs " + usable);
                item.projectedCorners++;
            }
            count++;
        }
        return count;
    }

    static bool Fielded(UnitsManger team, Data_Center unit) => unit != null && unit.WholeT != null
        && BattleCameraFraming.ShouldIncludeUnit(team.TeamMode, unit.gameObject.activeInHierarchy
            && unit.WholeT.gameObject.activeInHierarchy, unit.FightDataRef.IsDead.Value, team.RMode_Unit.Value == unit);

    static FightingStepLayer GetHUD() => UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>();

    static void Finish(string error)
    {
        if (finishing) return;
        finishing = true;
        report ??= new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        if (!string.IsNullOrEmpty(error)) report.errors.Add(error);
        var logged = SessionState.GetString(Key + ".Errors", "");
        if (!string.IsNullOrEmpty(logged) && logged != error) report.errors.Add(logged);
        report.passed = report.passed && report.errors.Count == 0;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CaptureError;
        EditorApplication.update -= Poll;
        SceneManager.sceneLoaded -= IsolateLoadedSceneServices;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null) settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".Builder", 0);
        if (SessionState.GetBool(Key + ".HadAuto", false)) PlayerPrefs.SetInt("auto", SessionState.GetInt(Key + ".Auto", 0));
        else PlayerPrefs.DeleteKey("auto");
        if (SessionState.GetBool(Key + ".HadOrbit", false)) PlayerPrefs.SetInt("AutoRotateCamera", SessionState.GetInt(Key + ".Orbit", 1));
        else PlayerPrefs.DeleteKey("AutoRotateCamera");
        Debug.Log("[CameraSmoke] " + (report.passed ? "PASS" : "FAIL") + ": " + Path.GetFullPath(Path.Combine(Output, "report.json")));
        if (!report.passed) Debug.LogError(string.Join("\n", report.errors));
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }

    static Vector2 ReviewResolution()
    {
        var size = Environment.GetEnvironmentVariable("POCKETSTRIKER_CAMERA_SIZE");
        if (size == "375x667") return new Vector2(375, 667);
        return size == "390x844" ? new Vector2(390, 844) : new Vector2(540, 960);
    }

    static void ConfigurePortraitGameView()
    {
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        var method = gameViewType.GetMethod("SetCustomResolution", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Cannot configure a portrait Game view in this editor.");
        var view = EditorWindow.GetWindow(gameViewType);
        method.Invoke(view, new object[] { ReviewResolution(), "PocketStriker Camera Smoke" });
        view.Repaint();
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
