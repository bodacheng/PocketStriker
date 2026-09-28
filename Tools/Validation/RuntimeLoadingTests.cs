using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Executes production loaders and shared cache utilities. Asset I/O and Unity
// object construction are substituted so each suspension can be controlled.
internal static class RuntimeLoadingTests
{
    static int checks;
    static int unobservedErrors;

    public static int Main()
    {
        UniTaskScheduler.DispatchUnityMainThread = false;
        UniTaskScheduler.UnobservedTaskException += error =>
        {
            System.Threading.Interlocked.Increment(ref unobservedErrors);
            Console.Error.WriteLine("Unobserved UniTask error: " + error);
        };
        try
        {
            Run().AsTask().GetAwaiter().GetResult();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Check(unobservedErrors == 0, "handled loader failures do not leak unobserved exceptions");
            Console.WriteLine($"PASS: {checks} runtime loading checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static async UniTask CheckBattleGroundLoading()
    {
        var controller = new BoundaryControlByGod();
        var firstLoad = controller.ChangeBackGround(0);
        var first = new GameObject("valid") { BattleGround = new BattleGround() };
        AddressablesLogic.Complete("battleGround/0", first);
        await firstLoad;
        Check(first.BattleGround.SetCalls == 1, "valid battlefield applies placement");
        await controller.ChangeBackGround(0);
        Check(AddressablesLogic.Calls("battleGround/0") == 1, "same battlefield is reused");

        var legacyLoad = controller.ChangeBackGround(1);
        Check(!first.Destroyed, "previous battlefield survives while replacement loads");
        var legacy = new GameObject("missing settings component");
        AddressablesLogic.Complete("battleGround/1", legacy);
        await legacyLoad;
        Check(first.Destroyed && !legacy.Destroyed, "missing component does not abort battle preparation");

        var nullLoad = controller.ChangeBackGround(2);
        AddressablesLogic.Complete("battleGround/2", null);
        await nullLoad;
        Check(!legacy.Destroyed, "empty load preserves current battlefield");
        var retry = controller.ChangeBackGround(2);
        Check(AddressablesLogic.Calls("battleGround/2") == 2, "empty load remains retryable");
        AddressablesLogic.Fail("battleGround/2");
        Check(await ObserveFailure(retry) && !legacy.Destroyed, "failed load preserves current battlefield");

        var staleLoad = controller.ChangeBackGround(3);
        var latestLoad = controller.ChangeBackGround(4);
        var latest = new GameObject("latest") { BattleGround = new BattleGround() };
        AddressablesLogic.Complete("battleGround/4", latest);
        await latestLoad;
        var stale = new GameObject("stale") { BattleGround = new BattleGround() };
        AddressablesLogic.Complete("battleGround/3", stale);
        await staleLoad;
        Check(stale.Destroyed && !latest.Destroyed && stale.BattleGround.SetCalls == 0,
            "late completion cannot overwrite newer battlefield");

        var abandonedLoad = controller.ChangeBackGround(5);
        await controller.ChangeBackGround(4);
        var abandoned = new GameObject("abandoned");
        AddressablesLogic.Complete("battleGround/5", abandoned);
        await abandonedLoad;
        Check(abandoned.Destroyed && !latest.Destroyed, "reselecting current battlefield cancels pending replacement");

        var exitLoad = controller.ChangeBackGround(6);
        UnityEngine.Object.Destroy(controller);
        var afterExit = new GameObject("scene already exited");
        AddressablesLogic.Complete("battleGround/6", afterExit);
        await exitLoad;
        Check(afterExit.Destroyed, "scene exit releases late Addressables instance");
    }

    static async UniTask Run()
    {
        await CheckBattleGroundLoading();
        var first = AnimationResourceLoader.LoadAnim("unit", "shared");
        var second = AnimationResourceLoader.LoadAnim("unit", "shared");
        Check(AddressablesLogic.Calls("unit/skill/shared.anim") == 1, "simultaneous clips share one I/O request");
        Check(second.Status == UniTaskStatus.Pending, "second clip caller waits for completion");
        var clip = new AnimationClip();
        AddressablesLogic.Complete("unit/skill/shared.anim", clip);
        await first;
        await second;
        Check(ReferenceEquals(AnimationResourceLoader.Instance.GetAnimationClip("unit/skill/shared"), clip), "completed clip cached");
        await AnimationResourceLoader.LoadAnim("unit", "shared");
        Check(AddressablesLogic.Calls("unit/skill/shared.anim") == 1, "completed clip avoids another request");
        AnimationResourceLoader.Instance.Clear();
        var reload = AnimationResourceLoader.LoadAnim("unit", "shared");
        Check(AddressablesLogic.Calls("unit/skill/shared.anim") == 2, "cleared clip cache reloads next fight");
        AddressablesLogic.Complete("unit/skill/shared.anim", new AnimationClip());
        await reload;

        var failingClip = ObserveFailure(AnimationResourceLoader.LoadAnim("unit", "retry"));
        var waitingClip = ObserveFailure(AnimationResourceLoader.LoadAnim("unit", "retry"));
        AddressablesLogic.Fail("unit/skill/retry.anim");
        Check(await failingClip && await waitingClip, "clip failure reaches both waiting callers");
        var retryClip = AnimationResourceLoader.LoadAnim("unit", "retry");
        Check(AddressablesLogic.Calls("unit/skill/retry.anim") == 2, "failed inflight clip entry is removed for retry");
        AddressablesLogic.Complete("unit/skill/retry.anim", new AnimationClip());
        await retryClip;

        var absentClip = AnimationResourceLoader.LoadAnim("unit", "optional");
        AddressablesLogic.Complete("unit/skill/optional.anim", null);
        await absentClip;
        Check(AnimationResourceLoader.Instance.GetAnimationClip("unit/skill/optional") == null,
            "absent optional clip does not poison the cache");
        var optionalRetry = AnimationResourceLoader.LoadAnim("unit", "optional");
        Check(AddressablesLogic.Calls("unit/skill/optional.anim") == 2, "null clip result remains retryable");
        AddressablesLogic.Complete("unit/skill/optional.anim", new AnimationClip());
        await optionalRetry;

        var audio1 = AudioResourceLoading.Instance.LoadAudioClipFromResourceAndPutItIntoDic("effect", "sound");
        var audio2 = AudioResourceLoading.Instance.LoadAudioClipFromResourceAndPutItIntoDic("effect", "sound");
        Check(AddressablesLogic.Calls("effect/sound") == 1 && audio2.Status == UniTaskStatus.Pending,
            "simultaneous audio callers share and await I/O");
        AddressablesLogic.Complete("effect/sound", new AudioClip());
        await audio1;
        await audio2;
        AudioResourceLoading.Clear();
        var audioReload = AudioResourceLoading.Instance.LoadAudioClipFromResourceAndPutItIntoDic("effect", "sound");
        Check(AddressablesLogic.Calls("effect/sound") == 2, "released audio cache can reload");
        AddressablesLogic.Complete("effect/sound", new AudioClip());
        await audioReload;

        var effect1 = EffectsManager.IniEffectsPool("glow", null, 3);
        var effect2 = EffectsManager.IniEffectsPool("glow", null, 3);
        Check(AddressablesLogic.Calls("default/glow.prefab") == 1, "simultaneous effect pools share prefab load");
        var effectPrefab = new GameObject("glow");
        effectPrefab.PreloadGate = new UniTaskCompletionSource<bool>();
        AddressablesLogic.Complete("default/glow.prefab", effectPrefab);
        Check(effect1.Status == UniTaskStatus.Pending && effect2.Status == UniTaskStatus.Pending,
            "effect callers wait until the pool is prewarmed");
        effectPrefab.PreloadGate.TrySetResult(true);
        var pool1 = await effect1;
        var pool2 = await effect2;
        Check(ReferenceEquals(pool1, pool2) && pool1.Count == 3, "one ready effect pool published");
        EffectsManager.Clear();
        Check(pool1.Cleared, "effect cache clear releases pooled instances");
        var effectReload = EffectsManager.IniEffectsPool("glow", null, 2);
        AddressablesLogic.Complete("default/glow.prefab", new GameObject("glow"));
        Check(!ReferenceEquals(pool1, await effectReload), "next fight constructs fresh effect pool");

        var effectFail1 = ObserveFailure(EffectsManager.IniEffectsPool("retry", null, 1));
        var effectFail2 = ObserveFailure(EffectsManager.IniEffectsPool("retry", null, 1));
        AddressablesLogic.Fail("default/retry.prefab");
        Check(await effectFail1 && await effectFail2, "pool load failure reaches all waiters");
        var effectRetry = EffectsManager.IniEffectsPool("retry", null, 1);
        Check(AddressablesLogic.Calls("default/retry.prefab") == 2, "failed effect load is retryable");
        AddressablesLogic.Complete("default/retry.prefab", new GameObject("retry"));
        await effectRetry;

        var preloadFail1 = ObserveFailure(EffectsManager.IniEffectsPool("preloadRetry", null, 1));
        var preloadFail2 = ObserveFailure(EffectsManager.IniEffectsPool("preloadRetry", null, 1));
        var brokenPrefab = new GameObject("preloadRetry") { PreloadGate = new UniTaskCompletionSource<bool>() };
        AddressablesLogic.Complete("default/preloadRetry.prefab", brokenPrefab);
        var failedPool = DecompositionPool.Created[DecompositionPool.Created.Count - 1];
        brokenPrefab.PreloadGate.TrySetException(new InvalidOperationException("test prewarm failure"));
        Check(await preloadFail1 && await preloadFail2, "prewarm failure reaches both pool callers");
        Check(failedPool.Cleared, "failed prewarm releases partially constructed pool");
        var preloadRetry = EffectsManager.IniEffectsPool("preloadRetry", null, 1);
        Check(AddressablesLogic.Calls("default/preloadRetry.prefab") == 2, "failed prewarm is not published or stuck inflight");
        AddressablesLogic.Complete("default/preloadRetry.prefab", new GameObject("preloadRetry"));
        Check(await preloadRetry != null, "prewarm can recover on retry");

        var weapon1 = HurtObjectManager.ConstructHurtObjectPool("weapon", Element.Normal, 2);
        var weapon2 = HurtObjectManager.ConstructHurtObjectPool("weapon", Element.Normal, 2);
        Check(AddressablesLogic.Calls("default/weapon.prefab") == 1, "simultaneous weapons share I/O");
        var weaponPrefab = new GameObject("weapon");
        weaponPrefab.Decomposition.Attachments = new[] { "attachment" };
        AddressablesLogic.Complete("default/weapon.prefab", weaponPrefab);
        Check(weapon1.Status == UniTaskStatus.Pending && weapon2.Status == UniTaskStatus.Pending,
            "original weapon waiters also wait for attachment pool");
        var attachmentPrefab = new GameObject("attachment");
        attachmentPrefab.Decomposition.Attachments = new[] { "weapon" };
        AddressablesLogic.Complete("default/attachment.prefab", attachmentPrefab);
        await weapon1;
        await weapon2;
        Check(HurtObjectManager.GetHurtObjectPool("weapon", "default") != null &&
              HurtObjectManager.GetHurtObjectPool("attachment", "default") != null,
            "cyclic attachments complete without duplicate parent loads or deadlock");
        var oldWeapon = HurtObjectManager.GetHurtObjectPool("weapon", "default");
        HurtObjectManager.Clear();
        Check(oldWeapon.Cleared && HurtObjectManager.GetHurtObjectPool("weapon", "default") == null,
            "weapon cache clear removes registry and instances");

        var weaponFail1 = ObserveFailure(HurtObjectManager.ConstructHurtObjectPool("weaponRetry", Element.Normal, 1));
        var weaponFail2 = ObserveFailure(HurtObjectManager.ConstructHurtObjectPool("weaponRetry", Element.Normal, 1));
        AddressablesLogic.Fail("default/weaponRetry.prefab");
        Check(await weaponFail1 && await weaponFail2, "weapon failure reaches all callers");
        var weaponRetry = HurtObjectManager.ConstructHurtObjectPool("weaponRetry", Element.Normal, 1);
        Check(AddressablesLogic.Calls("default/weaponRetry.prefab") == 2, "failed weapon request releases inflight entry");
        AddressablesLogic.Complete("default/weaponRetry.prefab", new GameObject("weaponRetry"));
        await weaponRetry;

        var parentFailure = ObserveFailure(HurtObjectManager.ConstructHurtObjectPool("parentRetry", Element.Normal, 1));
        var parentPrefab = new GameObject("parentRetry");
        parentPrefab.Decomposition.Attachments = new[] { "childRetry" };
        AddressablesLogic.Complete("default/parentRetry.prefab", parentPrefab);
        AddressablesLogic.Fail("default/childRetry.prefab");
        Check(await parentFailure, "attachment failure propagates to parent load");
        var parentRetry = HurtObjectManager.ConstructHurtObjectPool("parentRetry", Element.Normal, 1);
        AddressablesLogic.Complete("default/parentRetry.prefab", parentPrefab);
        Check(parentRetry.Status == UniTaskStatus.Pending && AddressablesLogic.Calls("default/childRetry.prefab") == 2,
            "constructed parent does not skip failed attachments on retry");
        AddressablesLogic.Complete("default/childRetry.prefab", new GameObject("childRetry"));
        await parentRetry;
        Check(HurtObjectManager.GetHurtObjectPool("childRetry", "default") != null, "attachment recovers on retry");

        var nestedFailure = ObserveFailure(HurtObjectManager.ConstructHurtObjectPool("nestedParent", Element.Normal, 1));
        var nestedParent = new GameObject("nestedParent");
        nestedParent.Decomposition.Attachments = new[] { "nestedChild" };
        var nestedChild = new GameObject("nestedChild");
        nestedChild.Decomposition.Attachments = new[] { "nestedGrandchild" };
        AddressablesLogic.Complete("default/nestedParent.prefab", nestedParent);
        AddressablesLogic.Complete("default/nestedChild.prefab", nestedChild);
        AddressablesLogic.Fail("default/nestedGrandchild.prefab");
        Check(await nestedFailure, "nested attachment failure reaches the parent");
        var nestedRetry = HurtObjectManager.ConstructHurtObjectPool("nestedParent", Element.Normal, 1);
        AddressablesLogic.Complete("default/nestedParent.prefab", nestedParent);
        AddressablesLogic.Complete("default/nestedChild.prefab", nestedChild);
        Check(AddressablesLogic.Calls("default/nestedGrandchild.prefab") == 2,
            "partial child pool does not skip failed grandchildren on retry");
        AddressablesLogic.Complete("default/nestedGrandchild.prefab", new GameObject("nestedGrandchild"));
        await nestedRetry;

        var oldClip = ObserveCancellation(AnimationResourceLoader.LoadAnim("unit", "epoch"));
        AnimationResourceLoader.Instance.Clear();
        var newClip = AnimationResourceLoader.LoadAnim("unit", "epoch");
        var currentClip = new AnimationClip();
        Check(AddressablesLogic.Calls("unit/skill/epoch.anim") == 2, "clear detaches inflight clip request");
        AddressablesLogic.Complete("unit/skill/epoch.anim", currentClip, 1);
        await newClip;
        AddressablesLogic.Complete("unit/skill/epoch.anim", new AnimationClip());
        Check(await oldClip && ReferenceEquals(AnimationResourceLoader.Instance.GetAnimationClip("unit/skill/epoch"), currentClip),
            "old clip completion cannot overwrite current cache generation");

        var oldAudio = ObserveCancellation(AudioResourceLoading.Instance.LoadAudioClipFromResourceAndPutItIntoDic("effect", "epoch"));
        AudioResourceLoading.Clear();
        var newAudio = AudioResourceLoading.Instance.LoadAudioClipFromResourceAndPutItIntoDic("effect", "epoch");
        var currentAudio = new AudioClip();
        Check(AddressablesLogic.Calls("effect/epoch") == 2, "clear detaches inflight audio request");
        AddressablesLogic.Complete("effect/epoch", currentAudio, 1);
        await newAudio;
        AddressablesLogic.Complete("effect/epoch", new AudioClip());
        Check(await oldAudio && ReferenceEquals(AudioResourceLoaderCore.SoundClipsDic["effect/epoch"], currentAudio),
            "old audio completion cannot overwrite current cache generation");

        var oldEffect = ObserveCancellation(EffectsManager.IniEffectsPool("epoch", null, 1));
        var stalePrefab = new GameObject("epoch") { PreloadGate = new UniTaskCompletionSource<bool>() };
        AddressablesLogic.Complete("default/epoch.prefab", stalePrefab);
        var stalePool = DecompositionPool.Created[DecompositionPool.Created.Count - 1];
        EffectsManager.Clear();
        var newEffect = EffectsManager.IniEffectsPool("epoch", null, 2);
        Check(AddressablesLogic.Calls("default/epoch.prefab") == 2, "clear during prewarm starts an independent pool load");
        AddressablesLogic.Complete("default/epoch.prefab", new GameObject("epoch"));
        var currentEffectPool = await newEffect;
        stalePrefab.PreloadGate.TrySetResult(true);
        Check(await oldEffect && stalePool.Cleared, "obsolete partial effect pool is cleaned after prewarm");
        Check(ReferenceEquals(await EffectsManager.IniEffectsPool("epoch", null, 1), currentEffectPool),
            "obsolete effect completion does not publish over the new pool");

        var oldWeaponLoad = ObserveCancellation(HurtObjectManager.ConstructHurtObjectPool("weaponEpoch", Element.Normal, 1));
        var staleWeaponPrefab = new GameObject("weaponEpoch") { PreloadGate = new UniTaskCompletionSource<bool>() };
        AddressablesLogic.Complete("default/weaponEpoch.prefab", staleWeaponPrefab);
        var staleWeaponPool = DecompositionPool.Created[DecompositionPool.Created.Count - 1];
        HurtObjectManager.Clear();
        var newWeapon = HurtObjectManager.ConstructHurtObjectPool("weaponEpoch", Element.Normal, 2);
        AddressablesLogic.Complete("default/weaponEpoch.prefab", new GameObject("weaponEpoch"));
        await newWeapon;
        var currentWeaponPool = HurtObjectManager.GetHurtObjectPool("weaponEpoch", "default");
        staleWeaponPrefab.PreloadGate.TrySetResult(true);
        Check(await oldWeaponLoad && staleWeaponPool.Cleared, "obsolete partial weapon pool is cleaned after prewarm");
        Check(ReferenceEquals(HurtObjectManager.GetHurtObjectPool("weaponEpoch", "default"), currentWeaponPool),
            "obsolete weapon completion does not publish over the new pool");

        AddressablesLogic.Missing.Add("custom/fallback.prefab");
        var fallback = EffectsManager.IniEffectsPool("fallback", "custom", 1);
        Check(AddressablesLogic.Calls("custom/fallback.prefab") == 0 &&
              AddressablesLogic.Calls("default/fallback.prefab") == 1, "missing elemental asset uses default path");
        AddressablesLogic.Complete("default/fallback.prefab", new GameObject("fallback"));
        Check(await fallback != null, "default pool is available to caller");

        // Execute the complete production basic-loader path, then its first skill
        // preload. A fresh manager must keep its basic clips in the same epoch.
        AnimationResourceLoader.Instance.Clear();
        var manager = new AnimationManger();
        var basicNames = new[] { "death", "rush", "block", "block_break", "getup", "victory" };
        var basicLocations = new List<string>();
        foreach (var name in basicNames) basicLocations.Add("fighter/BasicPack/default/" + name + ".anim");
        UnityEngine.AddressableAssets.Addressables.Labels["basic_anim"] = basicLocations;
        var basicLoading = manager.PreloadBasicPersonalAnims("fighter", "default");
        var basicClips = new Dictionary<string, AnimationClip>();
        foreach (var name in basicNames)
        {
            var basicClip = new AnimationClip { name = name };
            basicClips.Add(name, basicClip);
            AddressablesLogic.Complete("fighter/BasicPack/default/" + name + ".anim", basicClip);
        }
        await basicLoading;
        Check(manager.toLoadAnims.Count == basicNames.Length, "basic loader installs all movement and recovery clips");
        var personalLoading = manager.PreloadPersonalAnimResourceMode("fighter", "punch", Element.Normal, 1);
        AddressablesLogic.Complete("fighter/skill/punch.anim", new AnimationClip { name = "punch" });
        await personalLoading;
        foreach (var name in basicNames)
            Check(manager.toLoadAnims.TryGetValue(name, out var retained) && ReferenceEquals(retained, basicClips[name]),
                "first personal skill preload retains basic " + name);
        Check(manager.toLoadAnims.ContainsKey("punch"), "personal skill joins the initialized basic animation dictionary");

        // A skill is only ready once its dependent pools have completed, so a
        // transient dependency failure remains retryable on the same manager.
        var failedSkill = ObserveFailure(manager.PreloadPersonalAnimResourceMode("fighter", "magic", Element.Normal, 1));
        AddressablesLogic.Complete("fighter/skill/magic.anim", new AnimationClip
        {
            name = "magic", events = new[] { new AnimationEvent { functionName = "MagicForward", stringParameter = "skillRetry" } }
        });
        AddressablesLogic.Fail("default/skillRetry.prefab");
        Check(await failedSkill && !manager.toLoadAnims.ContainsKey("magic"), "failed skill dependency does not publish a ready marker");
        var recoveredSkill = manager.PreloadPersonalAnimResourceMode("fighter", "magic", Element.Normal, 1);
        Check(AddressablesLogic.Calls("default/skillRetry.prefab") == 2, "same manager retries the failed skill dependency");
        AddressablesLogic.Complete("default/skillRetry.prefab", new GameObject("skillRetry"));
        await recoveredSkill;
        Check(manager.toLoadAnims.ContainsKey("magic"), "recovered skill is published after its dependency");
    }

    static async UniTask<bool> ObserveFailure(UniTask task)
    {
        try { await task; return false; }
        catch (InvalidOperationException) { return true; }
    }
    static async UniTask<bool> ObserveFailure<T>(UniTask<T> task)
    {
        try { await task; return false; }
        catch (InvalidOperationException) { return true; }
    }
    static async UniTask<bool> ObserveCancellation(UniTask task)
    {
        try { await task; return false; }
        catch (OperationCanceledException) { return true; }
    }
    static async UniTask<bool> ObserveCancellation<T>(UniTask<T> task)
    {
        try { await task; return false; }
        catch (OperationCanceledException) { return true; }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}

public static class AddressablesLogic
{
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    static readonly Dictionary<string, List<UniTaskCompletionSource<object>>> pending = new Dictionary<string, List<UniTaskCompletionSource<object>>>();
    public static readonly HashSet<string> Missing = new HashSet<string>();
    public static int Calls(string key) => counts.TryGetValue(key, out var count) ? count : 0;
    public static bool HasIndexedTag(string tag) => true;
    public static bool CheckKeyExist(string tag, string key) => !Missing.Contains(key);
    public static UniTask<GameObject> LoadObject(string key) => LoadT<GameObject>(key);
    public static async UniTask<T> LoadT<T>(string key)
    {
        counts[key] = Calls(key) + 1;
        var source = new UniTaskCompletionSource<object>();
        if (!pending.TryGetValue(key, out var requests)) pending.Add(key, requests = new List<UniTaskCompletionSource<object>>());
        requests.Add(source);
        return (T)await source.Task;
    }
    public static void Complete(string key, object result, int requestIndex = 0)
    {
        var requests = pending[key]; var source = requests[requestIndex]; requests.RemoveAt(requestIndex); source.TrySetResult(result);
    }
    public static void Fail(string key)
    {
        var requests = pending[key]; var source = requests[0]; requests.RemoveAt(0); source.TrySetException(new InvalidOperationException("test load failure"));
    }
}
public static class AddressablesResourcePolicy
{
    public const string BasicAnimationLabel = "basic_anim", AudioLabel = "audio", EffectLabel = "effect", WeaponLabel = "weapon";
}
public partial class AnimationManger
{
    public IDictionary<string, AnimationClip> toLoadAnims = new Dictionary<string, AnimationClip>();
    public Animator Animator;
    public AnimatorOverrideController animatorOverride;
    public List<AnimationClip> knockoffAnimations, _hurtClipsBack, _hurtClipsLow, _hurtClipsHigh, _hurtClipsPress, _hurtClipsLay;
}
public enum Facial { Normal }
public class FacialAnimManager
{
    public void CasualFace() { }
    public void TriggerExpression(Facial facial) { }
    public void INI(Animator animator, AnimatorOverrideController controller) { }
}
public static class CommonSetting
{
    public const string BreakFreeEffectCode = "breakFree";
}
public enum Element { Normal }
public static class FightGlobalSetting
{
    public static string EffectPathDefine(Element element = Element.Normal) => "default";
}
public sealed class DecompositionPool
{
    public static readonly List<DecompositionPool> Created = new List<DecompositionPool>();
    readonly GameObject prefab;
    public bool Cleared;
    public int Count;
    public DecompositionPool(GameObject prefab) { this.prefab = prefab; Created.Add(this); }
    public void Clear() { Cleared = true; Count = 0; }
    public Decomposition Rent() => new Decomposition();
    public FakePreload PreloadAsync(int count, int threshold) => new FakePreload(Preload(count));
    async UniTask Preload(int count)
    {
        if (prefab.PreloadGate != null) await prefab.PreloadGate.Task;
        Count = count;
    }
}
public readonly struct FakePreload
{
    readonly UniTask task;
    public FakePreload(UniTask task) { this.task = task; }
    public UniTask ToUniTask() => task;
}
public sealed class Decomposition
{
    public string[] Attachments;
    public Transform transform = new Transform();
    public UnityEngine.Animations.PositionConstraint GetPositionConstraint() => new UnityEngine.Animations.PositionConstraint();
}
namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public class MonoBehaviour : Object { }
    public class ParticleSystem : Object { public GameObject gameObject = new GameObject("particle"); }
    public static class Random { public static int Range(int min, int max) => min; }
    public class Object
    {
        public string name;
        public bool Destroyed;
        public static void Destroy(Object value) { if (value != null) value.Destroyed = true; }
        public static bool operator ==(Object a, Object b) =>
            (ReferenceEquals(a, null) || a.Destroyed) ? (ReferenceEquals(b, null) || b.Destroyed) : ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
        public static implicit operator bool(Object value) => value != null;
    }
    public class AnimationClip : Object { public AnimationEvent[] events = Array.Empty<AnimationEvent>(); }
    public class AnimationEvent { public string functionName, stringParameter; public int intParameter; }
    public class RuntimeAnimatorController : Object { }
    public class AnimatorOverrideController : RuntimeAnimatorController
    {
        public RuntimeAnimatorController runtimeAnimatorController;
        public AnimatorOverrideController(RuntimeAnimatorController controller) { runtimeAnimatorController = controller; }
        public AnimationClip this[string key] { get => null; set { } }
    }
    public class Animator : Object
    {
        public GameObject gameObject = new GameObject("animator");
        public RuntimeAnimatorController runtimeAnimatorController;
    }
    public static class Mathf { public static float Lerp(float a, float b, float t) => a + (b - a) * t; }
    public class AudioClip { }
    public class GameObject : Object
    {
        public Decomposition Decomposition = new Decomposition();
        public UniTaskCompletionSource<bool> PreloadGate;
        public BattleGround BattleGround;
        public GameObject(string name) { this.name = name; }
        public void SetActive(bool active) { }
        public T GetComponent<T>() where T : class => BattleGround as T ?? Decomposition as T;
    }
    public class Transform { public Vector3 position; public Quaternion rotation; }
    public struct Vector3 { public static Vector3 zero => default; }
    public struct Quaternion { }
    public static class Debug { public static void Log(object value) { } public static void LogWarning(object value) { } }
}
namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { Succeeded, Failed }
}
namespace UnityEngine.AddressableAssets
{
    public class TestLocation { public string PrimaryKey; }
    public sealed class TestLocationHandle
    {
        public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus Status =>
            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded;
        public Exception OperationException => null;
        public List<TestLocation> Result = new List<TestLocation>();
        public bool IsValid() => true;
        public UniTask<List<TestLocation>>.Awaiter GetAwaiter() => UniTask.FromResult(Result).GetAwaiter();
    }
    public static class Addressables
    {
        public static readonly Dictionary<string, List<string>> Labels = new Dictionary<string, List<string>>();
        public static TestLocationHandle LoadResourceLocationsAsync(string label)
        {
            var handle = new TestLocationHandle();
            if (Labels.TryGetValue(label, out var keys))
                foreach (var key in keys) handle.Result.Add(new TestLocation { PrimaryKey = key });
            return handle;
        }
        public static void Release(TestLocationHandle handle) { }
    }
}
namespace UnityEngine.Animations
{
    public struct ConstraintSource { public Transform sourceTransform; public float weight; }
    public class PositionConstraint
    {
        public bool locked, constraintActive;
        public Vector3 translationOffset;
        public void SetSources(List<ConstraintSource> sources) { }
    }
}

// Minimal battle setup types for executing the actual boundary loader above.
public class BattleGround { public int SetCalls; public void Set() { SetCalls++; } }
public class SensorUnity { public void Setup(float radius, Vector3 center, int count) { } }
public enum FightEventType { Gangbang }
public enum TeamMode { MultiRaid }
public static class FightLoad { public static TestFightInfo Fight = new TestFightInfo(); }
public class TestFightInfo
{
    public FightEventType EventType;
    public TeamMode team1Mode;
    public TestMembers FightMembers = new TestMembers();
}
public class TestMembers { public TestSets HeroSets = new TestSets(), EnemySets = new TestSets(); }
public class TestSets { public List<object> GetValues() => new List<object>(); }
