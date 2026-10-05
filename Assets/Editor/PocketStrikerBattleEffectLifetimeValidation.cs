using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HittingDetection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;

/// <summary>Tests damage suspension and visual retirement independently of the processor's per-frame queue.</summary>
public static class PocketStrikerBattleEffectLifetimeValidation
{
    [Serializable] public sealed class Report
    {
        public bool passed;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public string scope = "Production Decomposition, HitBoxManager, BO_Marker, HitBoxesProcesser and BattleEffectLifetime in a preview scene. Covers damage suspension that preserves particles/renderers until popup cleanup, a rental absent from the current Update queue, particles on a child without a root ParticleSystem, immediate collider and cached-hit retirement, stale marker activation, generation expiry and clean new rental. Real retry/evolution and delayed async prefab completion are exercised by Evolution Heal Playmode Smoke.";
    }

    [MenuItem("PocketStriker/Validation/Battle Effect Invalidation")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before battle effect validation.");
        var report = new Report();
        var scene = EditorSceneManager.NewPreviewScene();
        var originalProcessor = HitBoxesProcesser.Instance;
        var rig = new GameObject("Battle effect retirement fixture");
        SceneManager.MoveGameObjectToScene(rig, scene);
        try
        {
            BattleEffectLifetime.Resume();
            var processor = rig.AddComponent<HitBoxesProcesser>(); HitBoxesProcesser.Instance = processor;
            var effectObject = new GameObject("Rental with child particles"); effectObject.transform.SetParent(rig.transform);
            var markerObject = new GameObject("Trigger marker"); markerObject.transform.SetParent(effectObject.transform);
            var marker = markerObject.AddComponent<BO_Marker>(); marker.radius = 1;
            var hitBox = effectObject.AddComponent<HitBoxManager>();
            var effect = effectObject.AddComponent<Decomposition>();
            effect._HitBox = hitBox; effect.IsWeapon = true;
            var track = effectObject.AddComponent<TrackControl>(); track.enabled = false; effect.TrackControl = track;
            var constraint = effectObject.AddComponent<PositionConstraint>(); effect.SetPositionConstraint(constraint);
            var particleObject = new GameObject("Child particles"); particleObject.transform.SetParent(effectObject.transform);
            var particles = particleObject.AddComponent<ParticleSystem>();
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            typeof(HitBoxManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hitBox, null);
            typeof(Decomposition).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, null);
            effect.OnEnableProcess(); hitBox.EnableMarkers(); particles.Emit(10); constraint.constraintActive = true;
            Check(!track.enabled, "An authored disabled track stays disabled on its first rental", report);
            var victim = new GameObject("Cached collider"); victim.transform.SetParent(rig.transform);
            var collider = victim.AddComponent<BoxCollider>();
            marker.GetBallDetectHitPool().Add(collider, new HitPointPara());
            Check(hitBox.Enabled && markerObject.GetComponent<SphereCollider>().enabled && particles.particleCount > 0,
                "Fixture begins with a live collider and emitted particles", report);
            var generation = BattleEffectLifetime.Generation;
            BattleEffectLifetime.SuspendDamage();
            Check(BattleEffectLifetime.Suspended && !hitBox.Enabled
                && !markerObject.GetComponent<SphereCollider>().enabled && marker.GetBallDetectHitPool().Count == 0,
                "Defeat immediately suspends damage and cached collisions", report);
            Check(!effect.IsBattleEffectInvalidated && effect.Phase == 1 && particles.particleCount > 0
                && renderer.enabled && constraint.constraintActive && BattleEffectLifetime.ActiveCount == 1,
                "Defeat preserves the visible rental until popup cleanup", report);
            hitBox.EnableMarkers(); hitBox.MarkersEnablingStarts(); effect.Step1(); effect.Step2();
            Check(!hitBox.Enabled && !BattleEffectLifetime.IsCurrent(generation),
                "Defeat blocks late marker callbacks and expires pending spawns", report);
            // Never call Decomposition.Update: the old AllProcessingFade would
            // miss this valid rental because its per-frame queue is empty.
            processor.AllProcessingFade();
            Check(effect.IsBattleEffectInvalidated && effect.Phase == -1 && !hitBox.Enabled
                && !markerObject.GetComponent<SphereCollider>().enabled && marker.GetBallDetectHitPool().Count == 0,
                "Rental outside the Update queue immediately loses damage and cached collisions", report);
            Check(particles.particleCount == 0 && !particles.isPlaying && !renderer.enabled && !constraint.constraintActive,
                "Child particles, renderer and body constraint retire immediately", report);
            hitBox.EnableMarkers(); hitBox.MarkersEnablingStarts(); effect.Step1(); effect.Step2();
            Check(!hitBox.Enabled && !markerObject.GetComponent<SphereCollider>().enabled,
                "Late callbacks cannot reactivate a retired rental", report);
            Check(!BattleEffectLifetime.IsCurrent(generation) && BattleEffectLifetime.ActiveCount == 0,
                "Boundary expires the previous generation and active membership", report);
            BattleEffectLifetime.InvalidateAll(true);
            effect.OnEnableProcess(); hitBox.EnableMarkers();
            Check(effect.IsBattleEffectInvalidated && !hitBox.Enabled && !renderer.enabled,
                "A rental during the skill-choice suspension stays inactive", report);
            BattleEffectLifetime.Resume(); effect.OnEnableProcess(); hitBox.EnableMarkers();
            Check(!effect.IsBattleEffectInvalidated && hitBox.Enabled && renderer.enabled
                && markerObject.GetComponent<SphereCollider>().enabled && marker.GetBallDetectHitPool().Count == 0,
                "A new-round rental restores clean markers and its original renderer", report);
            Check(!BattleEffectLifetime.IsCurrent(generation), "Resume does not revive an earlier generation", report);
            track.enabled = true;
            BattleEffectLifetime.InvalidateAll();
            bool retiredTrack = !track.enabled;
            effect.OnEnableProcess();
            Check(retiredTrack && track.enabled, "An active track stops on invalidation and restores on the next rental", report);
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            BattleEffectLifetime.InvalidateAll();
            UnityEngine.Object.DestroyImmediate(rig); EditorSceneManager.ClosePreviewScene(scene);
            HitBoxesProcesser.Instance = originalProcessor;
            Directory.CreateDirectory("Logs/CombatEffects");
            report.passed = report.errors.Count == 0 && report.checks.Count == 13;
            File.WriteAllText("Logs/CombatEffects/report.json", JsonUtility.ToJson(report, true));
        }
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_BATTLE_EFFECT_INVALIDATION_PASSED: " + report.checks.Count + " checks.");
    }

    static void Check(bool condition, string label, Report report)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.checks.Add(label);
    }
}
