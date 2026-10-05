using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runs the authored Power Split prefabs through the pooled sub-object lifecycle.</summary>
public static class PocketStrikerEnergySplitValidation
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    const string FixturePath = "energy-split-validation";
    static readonly string[] Stages = { "twowayboltp_start", "twowayboltp1", "twowayboltp2", "twowayboltp3", "twowayboltp4" };
    static readonly MethodInfo Advance = typeof(Decomposition).GetMethod("Life", PrivateInstance, null, new[] { typeof(float) }, null);
    static readonly FieldInfo ClockTriggered = typeof(HitBoxSubEventManger).GetField("clockEventTriggered", PrivateInstance);

    [Serializable] public sealed class Report
    {
        public bool passed;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public int fadingSplitCallbacks;
        public string scope = "Production Power Split prefabs, DecompositionPool, BO_Ani_E and sub-object lifecycle in a preview scene. Uses deterministic time steps with no target collisions. Checks all four split generations, finite terminal stage, pool reuse, destruction-deadline delivery, suspension/invalidation and landing/HP-fade triggers.";
    }

    [MenuItem("PocketStriker/Validation/Energy Split")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before energy split validation.");
        var report = new Report();
        var scene = EditorSceneManager.NewPreviewScene();
        var rig = new GameObject("Energy split validation fixture");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var registry = (ResourcePoolRegistry<DecompositionPool>)typeof(HurtObjectManager).GetField("HurtPools", PrivateStatic).GetValue(null);
        var registryPools = (IDictionary<string, DecompositionPool>)typeof(ResourcePoolRegistry<DecompositionPool>).GetField("pools", PrivateInstance).GetValue(registry);
        var registryCounts = (IDictionary<string, int>)typeof(ResourcePoolRegistry<DecompositionPool>).GetField("preloadCounts", PrivateInstance).GetValue(registry);
        var pools = new List<DecompositionPool>();
        var rentals = new List<Decomposition>();
        var preparedLeases = new HashSet<Decomposition>();
        var initialized = new HashSet<Decomposition>();
        var births = Stages.ToDictionary(stage => stage, _ => 0);
        var containerField = typeof(DecompositionPool).GetField("Marker", PrivateStatic);
        var originalContainer = (GameObject)containerField.GetValue(null);
        var originalRentals = originalContainer != null
            ? new HashSet<Decomposition>(originalContainer.GetComponentsInChildren<Decomposition>(true))
            : new HashSet<Decomposition>();
        int originalSceneStep = FightGlobalSetting.SceneStep;
        bool originalLogger = FightGlobalSetting.HitBoxLogger;
        try
        {
            FightGlobalSetting.SceneStep = 0;
            FightGlobalSetting.HitBoxLogger = false;
            BattleEffectLifetime.Resume();
            foreach (var stage in Stages)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/HurtObjects/defaultmagic/" + stage + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing authored split prefab: " + stage);
                var pool = new DecompositionPool(prefab);
                pools.Add(pool);
                if (!registry.TryAdd(EffectResourceKeyUtility.ResourceKey(FixturePath, stage), pool))
                    throw new InvalidOperationException("Validation pool key is already occupied: " + stage);
            }
            var owner = rig.AddComponent<Data_Center>();
            owner.geometryCenter = rig.transform;
            owner.FightDataRef.AT = 10;
            var events = rig.AddComponent<BO_Ani_E>();
            events._DATA_CENTER = owner;
            events.hiddenMethods = new BO_Ani_E.HiddenMethods(events);
            typeof(BO_Ani_E).GetField("magic_path", PrivateInstance).SetValue(events, FixturePath);

            string StageOf(Decomposition rental) => rental.name.Replace("(Clone)", "");
            void CollectRentals()
            {
                var container = (GameObject)containerField.GetValue(null);
                foreach (var rental in container.GetComponentsInChildren<Decomposition>(true))
                {
                    if (originalRentals.Contains(rental) || rentals.Contains(rental)) continue;
                    rentals.Add(rental);
                    rental.transform.SetParent(rig.transform, true);
                }
                foreach (var rental in rentals)
                {
                    if (!rental.gameObject.activeSelf || rental.Phase == 0)
                    {
                        preparedLeases.Remove(rental);
                        continue;
                    }
                    if (!preparedLeases.Add(rental)) continue;
                    // Non-ExecuteAlways callbacks do not run in editor preview
                    // scenes; reproduce the callbacks Unity runs on creation/rent.
                    if (initialized.Add(rental))
                    {
                        typeof(HittingDetection.HitBoxManager).GetMethod("Awake", PrivateInstance).Invoke(rental._HitBox, null);
                        typeof(Decomposition).GetMethod("Awake", PrivateInstance).Invoke(rental, null);
                    }
                    foreach (var subEvent in rental.GetComponentsInChildren<HitBoxSubEventManger>(true))
                        typeof(HitBoxSubEventManger).GetMethod("OnEnable", PrivateInstance).Invoke(subEvent, null);
                    rental.AudioSource = null;
                    rental.OnEnableProcess();
                    rental._HitBox.MarkersEnablingStarts();
                    births[StageOf(rental)]++;
                }
            }
            Decomposition Spawn(string stage)
            {
                events.hiddenMethods.MagicForward_core(stage, new Vector3(0, 1, 0), Quaternion.identity, 3, "197");
                CollectRentals();
                return (Decomposition)typeof(BO_Ani_E).GetField("processingHitBox", PrivateInstance).GetValue(events);
            }
            void Tick(float deltaTime)
            {
                foreach (var rental in rentals.ToArray())
                {
                    if (!rental.gameObject.activeSelf || rental.Phase == 0) continue;
                    var subEvents = rental.GetComponentsInChildren<HitBoxSubEventManger>(true);
                    var pending = subEvents.Where(subEvent => !(bool)ClockTriggered.GetValue(subEvent)).ToArray();
                    int phase = rental.Phase;
                    Advance.Invoke(rental, new object[] { deltaTime });
                    if (phase == 2 && StageOf(rental) != Stages[4])
                        report.fadingSplitCallbacks += pending.Count(subEvent => (bool)ClockTriggered.GetValue(subEvent));
                }
                CollectRentals();
            }
            void RunWave()
            {
                for (int frame = 0; frame < 160; frame++) Tick(.025f);
            }
            var firstSeed = Spawn(Stages[0]);
            RunWave();
            int[] expected = { 1, 2, 4, 8, 16 };
            Check(Stages.Select((stage, i) => births[stage] == expected[i]).All(value => value),
                "One cast produces 1 → 2 → 4 → 8 → 16 authored projectiles", report);
            Check(report.fadingSplitCallbacks == 28,
                "All 28 follow-up branch callbacks execute during parent fade", report);
            Check(rentals.All(rental => rental.Phase == 0 && !rental.gameObject.activeSelf),
                "The final generation ends with no further split or active rental", report);
            int firstWaveInstances = rentals.Count;
            var secondSeed = Spawn(Stages[0]);
            RunWave();
            Check(firstSeed == secondSeed && rentals.Count == firstWaveInstances,
                "A second cast reuses the original pooled instances", report);
            Check(Stages.Select((stage, i) => births[stage] == expected[i] * 2).All(value => value),
                "Pool reuse resets every one-shot branch timer", report);

            var deadline = Spawn(Stages[1]);
            deadline.Phase = 2;
            deadline.Counter = .51f;
            foreach (var subEvent in deadline.GetComponentsInChildren<HitBoxSubEventManger>(true))
                typeof(HitBoxSubEventManger).GetField("_timeCount", PrivateInstance).SetValue(subEvent, .49f);
            int before = births[Stages[2]];
            Tick(.025f);
            Check(births[Stages[2]] == before + 2 && deadline.Phase == -1,
                "Both deadline branches dispatch before the parent is queued for pool return", report);
            Tick(.025f);
            Check(births[Stages[2]] == before + 2 && deadline.Phase == 0,
                "A retired parent cannot repeat its branch events", report);
            RunWave();

            var suspended = Spawn(Stages[1]);
            suspended.Phase = 2;
            before = births[Stages[2]];
            BattleEffectLifetime.SuspendDamage();
            Tick(.6f);
            Check(births[Stages[2]] == before, "Suspended round visuals cannot spawn another split generation", report);
            BattleEffectLifetime.InvalidateAll();
            suspended.SpecialTriggerEvent(Stages[2], suspended.GetComponentInChildren<HitBoxSubEventManger>());
            Tick(.025f);
            Check(births[Stages[2]] == before, "Invalidated and returned rentals cannot revive sub-object callbacks", report);
            BattleEffectLifetime.Resume();

            void ConfigureSingleTrigger(Decomposition parent, string trigger)
            {
                var subEvents = parent.GetComponentsInChildren<HitBoxSubEventManger>(true);
                foreach (var subEvent in subEvents)
                {
                    var timer = (HitBoxSubEventManger.EventAndTriggerTime)typeof(HitBoxSubEventManger).GetField("_event", PrivateInstance).GetValue(subEvent);
                    timer.event_name = "";
                    typeof(HitBoxSubEventManger).GetField("LandedEvent", PrivateInstance).SetValue(subEvent, "");
                    typeof(HitBoxSubEventManger).GetField("fadeEvent", PrivateInstance).SetValue(subEvent, "");
                }
                typeof(HitBoxSubEventManger).GetField(trigger, PrivateInstance).SetValue(subEvents[0], Stages[2]);
            }
            var landing = Spawn(Stages[1]);
            ConfigureSingleTrigger(landing, "LandedEvent");
            landing.transform.position = Vector3.zero;
            before = births[Stages[2]];
            Tick(.025f);
            Check(births[Stages[2]] == before + 1 && landing.Phase == 0,
                "Landing emits its follow-up once and returns its parent", report);
            RunWave();
            var fading = Spawn(Stages[1]);
            ConfigureSingleTrigger(fading, "fadeEvent");
            fading._HitBox.CurrentHP = 0;
            before = births[Stages[2]];
            Tick(.025f);
            Check(births[Stages[2]] == before + 1 && fading.Phase == 2,
                "HP exhaustion emits its follow-up before damage retirement", report);
            Tick(.025f);
            Check(births[Stages[2]] == before + 1,
                "HP-fade follow-up remains one-shot on subsequent ticks", report);
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            BattleEffectLifetime.InvalidateAll();
            foreach (var rental in rentals)
                if (rental != null && rental.gameObject.activeSelf) Advance.Invoke(rental, new object[] { 0f });
            foreach (var stage in Stages)
            {
                string key = EffectResourceKeyUtility.ResourceKey(FixturePath, stage);
                registryPools.Remove(key);
                registryCounts.Remove(key);
            }
            foreach (var rental in rentals)
                if (rental != null) UnityEngine.Object.DestroyImmediate(rental.gameObject);
            // Destroy preview objects before disposing queues: UniRx's normal
            // pool clear uses runtime Destroy, which is invalid in edit mode.
            foreach (var pool in pools) pool.Dispose();
            var createdContainer = (GameObject)containerField.GetValue(null);
            if (originalContainer == null && createdContainer != null) UnityEngine.Object.DestroyImmediate(createdContainer);
            UnityEngine.Object.DestroyImmediate(rig);
            EditorSceneManager.ClosePreviewScene(scene);
            FightGlobalSetting.SceneStep = originalSceneStep;
            FightGlobalSetting.HitBoxLogger = originalLogger;
            BattleEffectLifetime.Resume();
            report.passed = report.errors.Count == 0 && report.checks.Count == 12;
            Directory.CreateDirectory("Logs/EnergySplit");
            File.WriteAllText("Logs/EnergySplit/report.json", JsonUtility.ToJson(report, true));
        }
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_ENERGY_SPLIT_PASSED: " + report.checks.Count + " checks.");
    }

    static void Check(bool condition, string label, Report report)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.checks.Add(label);
    }
}
