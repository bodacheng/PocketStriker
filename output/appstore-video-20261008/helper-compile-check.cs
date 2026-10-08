// EDITOR-ONLY INTEGRATION DRAFT. This file lives outside Assets intentionally.
// Copy into an existing editor recorder only after reviewing the integration contract.
// Host contract: retain the recorder's existing REAL ACCOUNT login/navigation.
// Call plan = await PrepareAsync(layer) before recording (preloads real owned
// skill resources, never grants inventory or changes account data). Start the
// existing recorder, then await RunAsync(layer, plan); record exactly 450 frames
// at 30 fps = 15 seconds. Await the recorder, THEN call RestoreAsync(layer, plan).
// Existing battle clips 4.2 + 4.2 + 5.2 seconds produce 28.6 seconds total.
// Save Report JSON and review the encoded effect/drag frames. No Confirm button
// or cloud skill update is dispatched. Never enable the hidden combo panel.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using dataAccess;
using mainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PocketStrikerStoreSkillSequenceDraft
{
    public const int FramesPerSecond = 30;
    public const int TotalFrames = 450;
    const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly string[] Preferred = { "130", "173", "114", "154", "116", "136", "175", "140" };

    [Serializable] public sealed class StepEvidence
    {
        public string action, instanceId, skillId, realName, from, to;
        public int frame, spLevel, targetSlot;
        public string[] slotsBefore, slotsAfter;
        public bool nativeRaycast, productionDrop, identityVerified, spLevelVerified;
    }

    [Serializable] public sealed class SkillEvidence
    {
        public string skillId, realName;
        public int spLevel, clickFrame, maximumOwnedEffects, maximumVisibleOwnedEffects;
        public int maximumVisibleParticles, firstVisibleFrame = -1, lastVisibleFrame = -1;
        public int animatorStateChanges;
        public float maximumAnimatorNormalizedTime;
    }

    [Serializable] public sealed class Report
    {
        public string scope = "Actual account-owned inventory, production SkillEditLayer, real authored animated fighter, native Button/EventSystem selection, SKStoneItem drag handlers and StoneCell.OnDrop/SVCenter equip/swap. Three real animation-event VFX previews; no cloud confirmation.";
        public string limitation = "Editor Game-view recording. Pointer clone position is synchronized to synthetic PointerEventData because production OnDrag reads Input.mousePosition. No production game code or hidden feature is changed. Production inventory ScrollToCell reveals selected owned stones. Visible bounds/particles are telemetry and require encoded-frame visual review.";
        public bool passed, complete, actualAccount, resetVerified, noCloudConfirmation = true, noLoadingOverlay;
        public int frames, framesPerSecond = FramesPerSecond, inventoryEquips, occupiedSlotSwaps, pointerClicks;
        public float seconds;
        public List<StepEvidence> steps = new List<StepEvidence>();
        public List<SkillEvidence> skills = new List<SkillEvidence>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class Plan
    {
        public string[] originalSlotInstances;
        public string[] inventoryInstances = new string[2];
        public string[] previewInstances = new string[3];
        public int[] targetSlots = new int[2];
        public string thirdPreviewInstance;
        public int originalTab;
        public float originalScroll;
        public bool[] originalRanges;
    }

    // Pure planning plus resource prewarm against the existing logged-in account.
    // Never create StoneOfPlayerInfo, seed a fixture, or touch server equipment.
    public static async UniTask<Plan> PrepareAsync(SkillEditLayer layer)
    {
        Require(layer != null && layer.Initialized && layer.CamConnector.FocusingC != null, "Skill editor/model not ready.");
        Require(PlayFab.PlayFabClientAPI.IsClientLoggedIn(), "This sequence requires the existing real account.");
        var hero = PreScene.target.Focusing;
        var unit = global::Units.GetUnitConfig(hero.r_id);
        var plan = new Plan { originalSlotInstances = SlotIds(layer), originalTab = layer.stonesBox.SelectedSpLevel,
            originalScroll = layer.stonesBox.ScrollRect.verticalNormalizedPosition,
            originalRanges = new[] { "closeCheckBox", "nearCheckBox", "farCheckBox" }.Select(f => Field<Toggle>(layer.stonesBox, f).isOn).ToArray() };
        Require(plan.originalSlotInstances.All(id => !string.IsNullOrEmpty(id)), "A fully equipped original set is required.");
        var owned = Stones.TargetStonesFromAccount(new SkillStonesBox.StoneFilterForm { Type = unit.TYPE }, hero.id)
            .Select(Stones.Get).Where(info => info != null && (string.IsNullOrEmpty(info.unitInstanceId) || info.unitInstanceId == hero.id))
            .OrderBy(info => Priority(info.SkillId)).ToArray();
        var equippedRecords = layer.nineSlot.AllSlot.Select(slot => slot._cell.GetItem()._SkillConfig.RECORD_ID).ToArray();
        var candidates = owned.Where(info => !plan.originalSlotInstances.Contains(info.InstanceId)
            && !equippedRecords.Contains(info.SkillId)).OrderBy(info => HasMagicVfx(info.SkillId) ? 0 : 1).ThenBy(info => Priority(info.SkillId)).ToArray();
        Require(candidates.Length >= 2, "Actual inventory has fewer than two distinct eligible unequipped skills; do not grant fixture inventory.");
        bool found = false;
        foreach (var first in candidates)
        {
            foreach (var second in candidates.Where(info => info.SkillId != first.SkillId))
            {
                foreach (var firstSlot in layer.nineSlot.AllSlot.Where(slot => !slot._cell.GetItem().Inherent && (slot.num - 1) % 3 != 0)
                    .OrderBy(slot => slot._cell.GetItem()._SkillConfig.SP_LEVEL == Config(first.InstanceId).SP_LEVEL ? 0 : 1))
                {
                    foreach (var secondSlot in layer.nineSlot.AllSlot.Where(slot => slot != firstSlot && !slot._cell.GetItem().Inherent && (slot.num - 1) % 3 != 0)
                        .OrderBy(slot => slot._cell.GetItem()._SkillConfig.SP_LEVEL == Config(second.InstanceId).SP_LEVEL ? 0 : 1))
                    {
                        var ids = equippedRecords.ToArray(); ids[firstSlot.num - 1] = first.SkillId;
                        if (SkillSet.CheckEdit(ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], ids[6], ids[7], ids[8]) != SkillSet.SkillEditError.Perfect) continue;
                        ids[secondSlot.num - 1] = second.SkillId;
                        if (SkillSet.CheckEdit(ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], ids[6], ids[7], ids[8]) != SkillSet.SkillEditError.Perfect) continue;
                        // Swapping two non-opener slots keeps the same legal costs.
                        plan.inventoryInstances = new[] { first.InstanceId, second.InstanceId };
                        plan.targetSlots = new[] { firstSlot.num, secondSlot.num };
                        found = true; break;
                    }
                    if (found) break;
                }
                if (found) break;
            }
            if (found) break;
        }
        Require(found, "No legal two-equip plan exists with the actual owned VFX inventory.");
        plan.previewInstances = plan.inventoryInstances
            .Concat(plan.originalSlotInstances).Concat(owned.Select(info => info.InstanceId))
            .Where(instance => HasMagicVfx(Config(instance)?.RECORD_ID))
            .GroupBy(instance => Config(instance).RECORD_ID).Select(group => group.First()).Take(3).ToArray();
        Require(plan.previewInstances.Length == 3, "Actual account needs three different owned magic VFX previews.");
        plan.thirdPreviewInstance = plan.previewInstances[2];
        var actor = layer.CamConnector.FocusingC;
        await HurtObjectManager.ConstructDPool();
        foreach (string instance in plan.previewInstances)
            await actor.AnimationManger.PreloadPersonalAnimResourceMode(unit.TYPE, Config(instance).REAL_NAME, unit.element, 1);
        foreach (string field in new[] { "closeCheckBox", "nearCheckBox", "farCheckBox" })
            Field<Toggle>(layer.stonesBox, field).SetIsOnWithoutNotify(false);
        layer.stonesBox.PressTab(Config(plan.inventoryInstances[0]).SP_LEVEL);
        RevealInventoryCell(layer, plan.inventoryInstances[0]);
        Canvas.ForceUpdateCanvases();
        await UniTask.DelayFrame(20);
        Require(!Object.FindObjectsByType<ProgressLayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(), "Loading overlay survived prewarm.");
        Require(!BOButton.AnyProcess, "Button debounce not settled.");
        return plan;
    }

    // AFTER the host recording stops, click real Reset and prove the original
    // nine IDs are restored. This sends no skill confirmation to the server.
    public static async UniTask RestoreAsync(SkillEditLayer layer, Plan plan, Report report = null)
    {
        new Sequence(layer, null, null).Click(layer.nineSlot.ResetButton, "native-unsaved-reset");
        await UniTask.DelayFrame(15);
        Require(SlotIds(layer).SequenceEqual(plan.originalSlotInstances), "Native Reset did not restore all original equipped instances.");
        string[] ranges = { "closeCheckBox", "nearCheckBox", "farCheckBox" };
        for (int i = 0; i < ranges.Length; i++) Field<Toggle>(layer.stonesBox, ranges[i]).SetIsOnWithoutNotify(plan.originalRanges[i]);
        layer.stonesBox.PressTab(plan.originalTab);
        layer.stonesBox.ScrollRect.verticalNormalizedPosition = plan.originalScroll;
        if (report != null) report.resetVerified = true;
    }

    // onFrame runs once per requested frame AFTER LastPostLateUpdate. The host
    // can capture a frame here; an independent recorder loop can omit it.
    // Optional pointerVisual drives an existing touch marker; it never intercepts.
    public static async UniTask<Report> RunAsync(SkillEditLayer layer, Plan plan,
        Func<int, UniTask> onFrame = null, Action<Vector2, bool> pointerVisual = null, Action<string> noteAction = null)
    {
        var run = new Sequence(layer, onFrame, pointerVisual, noteAction);
        try
        {
            await run.WaitFrames(18);
            run.Click(Tab(layer, Config(plan.inventoryInstances[0]).SP_LEVEL), "category-first-owned-skill");
            RevealInventoryCell(layer, plan.inventoryInstances[0]);
            await run.WaitFrames(10);
            await run.Drag(InventoryCell(layer, plan.inventoryInstances[0]), layer.nineSlot.AllSlot[plan.targetSlots[0] - 1]._cell, 27, false);
            await run.WaitFrames(13);
            await run.PreviewInstance(plan.previewInstances[0], 70);

            run.Click(Tab(layer, Config(plan.inventoryInstances[1]).SP_LEVEL), "category-second-owned-skill");
            RevealInventoryCell(layer, plan.inventoryInstances[1]);
            await run.WaitFrames(10);
            await run.Drag(InventoryCell(layer, plan.inventoryInstances[1]), layer.nineSlot.AllSlot[plan.targetSlots[1] - 1]._cell, 27, false);
            await run.WaitFrames(13);
            await run.PreviewInstance(plan.previewInstances[1], 70);

            await run.Drag(layer.nineSlot.AllSlot[plan.targetSlots[0] - 1]._cell, layer.nineSlot.AllSlot[plan.targetSlots[1] - 1]._cell, 27, true);
            await run.WaitFrames(13);
            await run.PreviewInstance(plan.previewInstances[2], 80);
            Require(run.Report.frames <= TotalFrames, "Timeline overrun.");
            await run.WaitFrames(TotalFrames - run.Report.frames);

            VerifySlot(layer, plan.targetSlots[0], plan.inventoryInstances[1], Config(plan.inventoryInstances[1]).SP_LEVEL);
            VerifySlot(layer, plan.targetSlots[1], plan.inventoryInstances[0], Config(plan.inventoryInstances[0]).SP_LEVEL);
            Require(layer.nineSlot.CheckEditBasedOnCurrent(false) == SkillSet.SkillEditError.Perfect, "Final edited set is not legal.");
            Require(run.Report.frames == TotalFrames, "Timeline must produce exactly 450 frames.");
            Require(run.Report.inventoryEquips == 2 && run.Report.occupiedSlotSwaps == 1, "Required equip/swap coverage missing.");
            Require(run.Report.skills.Count == 3 && run.Report.skills.All(s => s.maximumVisibleOwnedEffects > 0), "A real skill VFX never entered the preview camera frustum.");
            Require(run.Report.noLoadingOverlay, "Loading overlay was recorded.");
            Require(PlayFab.PlayFabClientAPI.IsClientLoggedIn(), "Existing account login disappeared.");
            run.Report.actualAccount = true;
            run.Report.complete = true;
            run.Report.passed = true;
        }
        catch (Exception exception) { run.Report.errors.Add(exception.ToString()); }
        finally
        {
            pointerVisual?.Invoke(Vector2.zero, false);
            run.Report.seconds = (float)run.Report.frames / FramesPerSecond;
            // A failed drag must not leave the production global drag lock set.
            if (SKStoneItem.draggedItem != null)
                SKStoneItem.draggedItem.OnEndDrag(new PointerEventData(EventSystem.current));
        }
        return run.Report;
    }

    sealed class Sequence
    {
        readonly SkillEditLayer layer;
        readonly Func<int, UniTask> onFrame;
        readonly Action<Vector2, bool> pointerVisual;
        readonly Action<string> noteAction;
        readonly Camera camera;
        SkillEvidence observing;
        int previousState;
        public readonly Report Report = new Report { noLoadingOverlay = true };

        public Sequence(SkillEditLayer layer, Func<int, UniTask> capture, Action<Vector2, bool> pointer, Action<string> note = null)
        {
            this.layer = layer; onFrame = capture; pointerVisual = pointer; noteAction = note;
            camera = Field<Camera>(layer.CamConnector, "camera");
            Require(camera != null && camera.isActiveAndEnabled, "Real preview camera is unavailable.");
        }

        public async UniTask WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                Sample();
                if (onFrame != null) await onFrame(Report.frames);
                Report.frames++;
            }
        }

        public void Click(Button button, string purpose)
        {
            Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Unavailable button: " + purpose);
            Require(!BOButton.AnyProcess, "Native button debounce is busy: " + purpose);
            var data = Pointer(Point((RectTransform)button.transform));
            var hit = TopHit(data);
            Require(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == button.gameObject, "Pointer blocked at " + purpose);
            pointerVisual?.Invoke(data.position, true);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
            pointerVisual?.Invoke(data.position, false);
            Report.pointerClicks++;
            noteAction?.Invoke(purpose + "-frame-" + Report.frames);
            Report.steps.Add(new StepEvidence { action = purpose, frame = Report.frames, nativeRaycast = true });
        }

        public async UniTask PreviewInstance(string instanceId, int frames)
        {
            var cell = layer.nineSlot.AllSlot.Select(slot => slot._cell).FirstOrDefault(c => c.GetItem()?.instanceId == instanceId);
            if (cell == null)
            {
                Click(Tab(layer, Config(instanceId).SP_LEVEL), "category-vfx-preview");
                RevealInventoryCell(layer, instanceId);
                await WaitFrames(10);
                cell = InventoryCell(layer, instanceId);
            }
            await Preview(cell, Config(instanceId).RECORD_ID, frames);
        }

        public async UniTask Preview(StoneCell cell, string id, int frames)
        {
            Require(cell != null && cell.GetItem()?._SkillConfig.RECORD_ID == id, "Wrong preview cell: " + id);
            var config = cell.GetItem()._SkillConfig;
            observing = new SkillEvidence { skillId = id, realName = config.REAL_NAME, spLevel = config.SP_LEVEL, clickFrame = Report.frames };
            previousState = layer.CamConnector.FocusingC.AnimationManger.AnimatorRef.GetCurrentAnimatorStateInfo(1).fullPathHash;
            Report.skills.Add(observing);
            Click(cell.btn, "preview-" + id);
            await WaitFrames(frames);
            Require(observing.animatorStateChanges > 0, "Preview did not trigger a real animation: " + id);
            observing = null;
        }

        public async UniTask Drag(StoneCell from, StoneCell to, int frames, bool swap)
        {
            Require(from != null && to != null && from != to, "Invalid real drag cells.");
            var item = from.GetItem(); var displaced = to.GetItem();
            Require(item != null && !SKStoneItem.DragBlocked, "Drag source or global drag state unavailable.");
            Require(!swap || displaced != null, "Swap destination must be occupied.");
            var evidence = new StepEvidence { action = swap ? "occupied-slot-swap" : "inventory-equip",
                frame = Report.frames, from = from.name, to = to.name, instanceId = item.instanceId,
                skillId = item._SkillConfig.RECORD_ID, realName = item._SkillConfig.REAL_NAME, spLevel = item._SkillConfig.SP_LEVEL,
                targetSlot = layer.nineSlot.GetSlotByCell(to)?.num ?? -1, slotsBefore = SlotIds(layer) };
            var start = Point((RectTransform)item.transform); var end = Point((RectTransform)to.transform);
            var data = Pointer(start); var sourceHit = TopHit(data);
            Require(ExecuteEvents.GetEventHandler<IBeginDragHandler>(sourceHit) == item.gameObject, "Raycast misses draggable stone.");
            data.pointerDrag = item.gameObject;
            ExecuteEvents.ExecuteHierarchy(sourceHit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(item.gameObject, data, ExecuteEvents.beginDragHandler);
            Require(SKStoneItem.draggedItem == item && SKStoneItem.sourceCell == from && SKStoneItem.dragging != null, "Production beginDrag failed.");
            data.dragging = true;
            // Native mouse input leaves the source hit region during a drag.
            // Deliver that real pointerExit so a >0.8s drag cannot invoke the
            // BOButton hold action and navigate away to stone level-up.
            ExecuteEvents.ExecuteHierarchy(sourceHit, data, ExecuteEvents.pointerExitHandler);
            evidence.nativeRaycast = true;
            for (int i = 0; i < frames; i++)
            {
                float t = (float)(i + 1) / frames;
                float smooth = t * t * (3 - 2 * t);
                data.position = Vector2.Lerp(start, end, smooth);
                ExecuteEvents.Execute(item.gameObject, data, ExecuteEvents.dragHandler);
                // Editor-only pointer synchronization: production OnDrag uses
                // Input.mousePosition, while the synthetic EventSystem has a
                // deterministic pointer path. The real clone is moved here;
                // equipment is still performed ONLY by the production OnDrop.
                if (SKStoneItem.dragging != null) SKStoneItem.dragging.transform.position = data.position;
                pointerVisual?.Invoke(data.position, true);
                await WaitFrames(1);
            }
            var targetHit = TopHit(data);
            Require(ExecuteEvents.GetEventHandler<IDropHandler>(targetHit) == to.gameObject, "Drop raycast blocked by another UI object.");
            ExecuteEvents.ExecuteHierarchy(targetHit, data, ExecuteEvents.dropHandler);
            evidence.productionDrop = true;
            ExecuteEvents.Execute(item.gameObject, data, ExecuteEvents.endDragHandler);
            ExecuteEvents.ExecuteHierarchy(sourceHit, data, ExecuteEvents.pointerUpHandler);
            pointerVisual?.Invoke(end, false);
            Require(to.GetItem()?.instanceId == item.instanceId, "Equip/drop result identity mismatch.");
            Require(to.GetItem()?._SkillConfig.SP_LEVEL == evidence.spLevel, "Equip/drop result SP mismatch.");
            if (swap)
            {
                Require(from.GetItem()?.instanceId == displaced.instanceId, "Occupied swap did not move displaced stone back.");
                Report.occupiedSlotSwaps++;
            }
            else Report.inventoryEquips++;
            evidence.identityVerified = evidence.spLevelVerified = true;
            evidence.slotsAfter = SlotIds(layer);
            Report.steps.Add(evidence);
            noteAction?.Invoke(evidence.action + "-frame-" + Report.frames);
            Require(SKStoneItem.draggedItem == null && SKStoneItem.dragging == null, "EndDrag did not clear production drag state.");
        }

        void Sample()
        {
            if (Object.FindObjectsByType<ProgressLayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any()) Report.noLoadingOverlay = false;
            if (observing == null) return;
            var actor = layer.CamConnector.FocusingC;
            var state = actor.AnimationManger.AnimatorRef.GetCurrentAnimatorStateInfo(1);
            if (previousState != state.fullPathHash) { observing.animatorStateChanges++; previousState = state.fullPathHash; }
            observing.maximumAnimatorNormalizedTime = Mathf.Max(observing.maximumAnimatorNormalizedTime, state.normalizedTime);
            var effects = Object.FindObjectsByType<Decomposition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(effect => !effect.IsBattleEffectInvalidated && effect.GetBOAniE()?._DATA_CENTER == actor).ToArray();
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var visible = effects.Where(effect => effect.GetComponentsInChildren<Renderer>()
                .Any(renderer => renderer.enabled && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0
                    && GeometryUtility.TestPlanesAABB(planes, renderer.bounds))).ToArray();
            int particles = visible.Sum(effect => effect.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount));
            observing.maximumOwnedEffects = Mathf.Max(observing.maximumOwnedEffects, effects.Length);
            observing.maximumVisibleOwnedEffects = Mathf.Max(observing.maximumVisibleOwnedEffects, visible.Length);
            observing.maximumVisibleParticles = Mathf.Max(observing.maximumVisibleParticles, particles);
            if (visible.Length > 0)
            {
                if (observing.firstVisibleFrame < 0) observing.firstVisibleFrame = Report.frames;
                observing.lastVisibleFrame = Report.frames;
            }
        }
    }

    static StoneCell InventoryCell(SkillEditLayer layer, string instanceId) => layer.stonesBox.GetComponentsInChildren<StoneCell>()
        .FirstOrDefault(cell => cell.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell && cell.GetItem()?.instanceId == instanceId);
    static Skill.SkillConfig Config(string instanceId) => SkillConfigTable.GetSkillConfigByRecordId(Stones.Get(instanceId)?.SkillId);
    static int Priority(string id) { int i = Array.IndexOf(Preferred, id); return i >= 0 ? i : 1000; }
    static bool HasMagicVfx(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return false;
        var config = SkillConfigTable.GetSkillConfigByRecordId(skillId);
        if (config == null || config.SP_LEVEL <= 0) return false;
        if (Preferred.Contains(skillId)) return true;
        string path = "Assets/ExternalAssets/Animations/" + config.TYPE + "/skill/" + config.REAL_NAME + ".anim";
        if (!File.Exists(path)) return false;
        string source = File.ReadAllText(path);
        return source.Contains("functionName: MagicForward") || source.Contains("functionName: ReleasePreparedMagicToAir") || source.Contains("functionName: MagicToEnemy");
    }
    static BOButton Tab(SkillEditLayer layer, int sp) => Field<BOButton>(layer.stonesBox, sp == 0 ? "NormalTab" : "EX" + sp + "Tab");
    static void RevealInventoryCell(SkillEditLayer layer, string instanceId)
    {
        var cell = InventoryCell(layer, instanceId);
        Require(cell != null, "Owned stone is absent from the real category: " + instanceId);
        // Use the production inventory's existing cell reveal/scroll method.
        // The report should disclose these programmatic viewport adjustments.
        typeof(SkillStonesBox).GetMethod("ScrollToCell", Fields).Invoke(layer.stonesBox, new object[] { cell });
        Canvas.ForceUpdateCanvases();
    }
    static string[] SlotIds(SkillEditLayer layer) => layer.nineSlot.AllSlot.Select(slot => slot._cell.GetItem()?.instanceId ?? "").ToArray();
    static void VerifySlot(SkillEditLayer layer, int slot, string instanceId, int sp) =>
        Require(layer.nineSlot.AllSlot[slot - 1]._cell.GetItem()?.instanceId == instanceId
            && layer.nineSlot.AllSlot[slot - 1]._cell.GetItem()?._SkillConfig.SP_LEVEL == sp, "Wrong final slot " + slot);
    static Vector2 Point(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
    }
    static PointerEventData Pointer(Vector2 point)
    {
        Require(EventSystem.current != null, "Production EventSystem is missing.");
        return new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
    }
    static GameObject TopHit(PointerEventData data)
    {
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Require(hits.Count > 0, "No real UI raycast hit at " + data.position);
        return hits[0].gameObject;
    }
    static T Field<T>(object target, string name)
    {
        Require(target != null, "Null reflection target: " + name);
        var field = target.GetType().GetField(name, Fields);
        Require(field != null, "Missing real authored field: " + name);
        return (T)field.GetValue(target);
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
