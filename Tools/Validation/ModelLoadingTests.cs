using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Singleton;
using UnityEngine;

// Runs the production model and battle preparation code. Controllable model
// loads and initialization gates reproduce Unity destruction across awaits.
// The doubles model Unity's overloaded null semantics; these checks do not run
// an iOS player or verify deserialization of its bundled prefabs.
internal static class ModelLoadingTests
{
    static int checks;

    public static int Main()
    {
        UniTaskScheduler.DispatchUnityMainThread = false;
        try
        {
            Run().AsTask().GetAwaiter().GetResult();
            Console.WriteLine($"PASS: {checks} unit model loading checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static async UniTask Run()
    {
        var missingConfig = await Failure(GeneralModelPool.GetModel("missing"));
        Check(missingConfig.Message.Contains("Missing unit configuration") && missingConfig.Message.Contains("missing"),
            "missing configuration reports the requested unit before asset loading");
        var missingBattleConfig = await Failure(UnitCreator.CreateUnit(new UnitInfo { r_id = "missing" }, 1));
        Check(missingBattleConfig.Message.Contains("Missing unit configuration") && AddressablesLogic.Requests == 0,
            "battle preparation propagates configuration failure without starting a model load");
        Units.Configs["2"] = new UnitConfig { TYPE = "human", REAL_NAME = "tetsuya", BASIC_MOVEMENT_PACK = "tetsuya", element = Element.Blue };

        var noModel = GeneralModelPool.GetModel("2");
        AddressablesLogic.Complete(null);
        CheckContext(await Failure(noModel), "Could not load unit model");

        var missingLink = GeneralModelPool.GetModel("2");
        AddressablesLogic.Complete(new GameObject());
        CheckContext(await Failure(missingLink), "missing root OutsideDataLink");

        var missingCenter = GeneralModelPool.GetModel("2");
        AddressablesLogic.Complete(new GameObject { Link = new OutsideDataLink() });
        CheckContext(await Failure(missingCenter), "OutsideDataLink._C is missing");

        var model = Model();
        model.Link._C.Step1Gate = new UniTaskCompletionSource<bool>();
        var progress = new List<float>();
        var destroyedModel = GeneralModelPool.GetModel("2", onProgress: progress.Add);
        AddressablesLogic.Complete(model);
        Check(destroyedModel.Status == UniTaskStatus.Pending, "model initialization genuinely suspends");
        UnityEngine.Object.Destroy(model);
        model.Link._C.Step1Gate.TrySetResult(true);
        CheckContext(await Failure(destroyedModel), "destroyed during initialization");
        Check(model.Activations == 0 && !progress.Contains(1f), "destroyed model is neither activated nor reported ready");

        model = Model();
        model.Link._C.Step1Gate = new UniTaskCompletionSource<bool>();
        var destroyedCenter = GeneralModelPool.GetModel("2");
        AddressablesLogic.Complete(model);
        UnityEngine.Object.Destroy(model.Link._C);
        model.Link._C.Step1Gate.TrySetResult(true);
        CheckContext(await Failure(destroyedCenter), "destroyed during initialization");

        var originalError = new Exception("animation failure");
        model = Model();
        model.Link._C.Step1Error = originalError;
        var failedInitialization = GeneralModelPool.GetModel("2");
        AddressablesLogic.Complete(model);
        var error = await Failure(failedInitialization);
        CheckContext(error, "Could not initialize unit model");
        Check(ReferenceEquals(error.InnerException, originalError), "initialization failure retains the original exception");

        model = Model();
        model.Link._C.Step1Gate = new UniTaskCompletionSource<bool>();
        model.Link._C.Step1Error = originalError;
        var failedAfterDestruction = GeneralModelPool.GetModel("2");
        AddressablesLogic.Complete(model);
        UnityEngine.Object.Destroy(model);
        model.Link._C.Step1Gate.TrySetResult(true);
        error = await Failure(failedAfterDestruction);
        CheckContext(error, "destroyed during initialization");
        Check(ReferenceEquals(error.InnerException, originalError), "failure after destruction keeps cause and lifecycle context");

        var cancellation = new OperationCanceledException(new System.Threading.CancellationToken(true));
        model = Model();
        model.Link._C.Step1Gate = new UniTaskCompletionSource<bool>();
        var canceledModel = GeneralModelPool.GetModel("2");
        AddressablesLogic.Complete(model);
        model.Link._C.Step1Gate.TrySetException(cancellation);
        Check(canceledModel.Status == UniTaskStatus.Canceled, "model initialization cancellation preserves canceled task status");
        var observedCancellation = await Cancellation(canceledModel);
        Check(observedCancellation.CancellationToken == cancellation.CancellationToken && observedCancellation.InnerException == null,
            "model initialization preserves cancellation token without error wrapping");

        model = Model();
        var parent = new Transform();
        progress.Clear();
        var success = GeneralModelPool.GetModel("2", parent, onProgress: progress.Add);
        AddressablesLogic.Complete(model);
        var center = await success;
        Check(ReferenceEquals(center, model.Link._C) && ReferenceEquals(model.transform.Parent, parent),
            "valid model returns its linked data center and attaches to the requested parent");
        Check(center.element == Element.Blue && center.Step1Type == "human" && center.Step1Pack == "tetsuya"
            && model.Activations == 1 && progress[0] == 0f && progress[progress.Count - 1] == 1f,
            "valid model preserves initialization inputs and completion progress");

        var info = new UnitInfo { r_id = "2", set = new object() };
        var failedBattleModel = UnitCreator.CreateUnit(info, 3);
        AddressablesLogic.Complete(new GameObject());
        CheckContext(await Failure(failedBattleModel), "missing root OutsideDataLink");

        model = Model();
        model.Link._C.Step2Gate = new UniTaskCompletionSource<bool>();
        var destroyedBattleUnit = UnitCreator.CreateUnit(info, 3);
        AddressablesLogic.Complete(model);
        Check(destroyedBattleUnit.Status == UniTaskStatus.Pending, "battle initialization genuinely suspends");
        UnityEngine.Object.Destroy(model.Link._C);
        model.Link._C.Step2Gate.TrySetResult(true);
        CheckContext(await Failure(destroyedBattleUnit), "destroyed during battle initialization");

        model = Model();
        model.Link._C.Step2Error = originalError;
        var failedBattleInitialization = UnitCreator.CreateUnit(info, 3);
        AddressablesLogic.Complete(model);
        error = await Failure(failedBattleInitialization);
        CheckContext(error, "Could not initialize unit for battle");
        Check(ReferenceEquals(error.InnerException, originalError), "battle initialization retains original failure");

        cancellation = new OperationCanceledException(new System.Threading.CancellationToken(true));
        model = Model();
        model.Link._C.Step2Gate = new UniTaskCompletionSource<bool>();
        var canceledBattleUnit = UnitCreator.CreateUnit(info, 3);
        AddressablesLogic.Complete(model);
        model.Link._C.Step2Gate.TrySetException(cancellation);
        Check(canceledBattleUnit.Status == UniTaskStatus.Canceled, "battle initialization cancellation preserves canceled task status");
        observedCancellation = await Cancellation(canceledBattleUnit);
        Check(observedCancellation.CancellationToken == cancellation.CancellationToken && observedCancellation.InnerException == null,
            "battle initialization preserves cancellation token without error wrapping");

        model = Model();
        progress.Clear();
        var battleSuccess = UnitCreator.CreateUnit(info, 3, progress.Add);
        AddressablesLogic.Complete(model);
        center = await battleSuccess;
        Check(ReferenceEquals(center, model.Link._C) && center.Step2Type == "human"
            && center.Step2Element == Element.Blue && ReferenceEquals(center.Step2Set, info.set) && center.PreloadCount == 3,
            "valid battle unit preserves skill set, element, model type, and preload count");
        Check(progress[0] == 0f && progress[progress.Count - 1] == 1f && IsMonotonic(progress),
            "valid battle preparation keeps monotonic progress across both initialization phases");

        await CheckClosedPreviewLoads();
    }

    static async UniTask CheckClosedPreviewLoads()
    {
        var preview = new ModelView.DedicatedCameraConnector();
        var loading = preview.ShowModel("2");
        var queued = preview.ShowModel("2");
        Check(preview.TaskRunningCount == 2, "preview selection genuinely queues behind an in-flight model");
        DisablePreview(preview);
        ProgressLayer.Loading("battle loading");
        var lateModel = Model();
        AddressablesLogic.Complete(lateModel);
        await loading;
        await queued;
        Check(lateModel == null && preview.FocusingC == null && preview.InitializeCalls == 0,
            "closing preparation destroys late preview models before displaying or focusing them");
        Check(ProgressLayer.Label == "battle loading" && ProgressLayer.CloseCalls == 0,
            "late preview loads and queued selections preserve the new battle loading curtain");

        preview = new ModelView.DedicatedCameraConnector();
        loading = preview.ShowModel("2");
        queued = preview.ShowModel("2");
        DisablePreview(preview);
        preview.enabled = true;
        ProgressLayer.Loading("new page loading");
        lateModel = Model();
        var requestCount = AddressablesLogic.Requests;
        AddressablesLogic.Complete(lateModel);
        await loading;
        await queued;
        Check(lateModel == null && AddressablesLogic.Requests == requestCount && preview.FocusingC == null,
            "reopening a connector does not revive selections queued by its previous page lifetime");
        Check(ProgressLayer.Label == "new page loading" && ProgressLayer.CloseCalls == 0,
            "an old page lifetime cannot close loading after the connector is reenabled");
        await preview.ShowModel(null);
        Check(preview.TaskRunningCount == 0, "reenabled connector accepts a new selection and drains its queue");

        preview = new ModelView.DedicatedCameraConnector();
        var warmup = preview.PrepareModel("2");
        DisablePreview(preview);
        lateModel = Model();
        AddressablesLogic.Complete(lateModel);
        await warmup;
        Check(lateModel == null, "closing preparation releases models returned by an in-flight warmup");

        preview = new ModelView.DedicatedCameraConnector();
        warmup = preview.PrepareModel("2");
        DisablePreview(preview);
        preview.enabled = true;
        lateModel = Model();
        AddressablesLogic.Complete(lateModel);
        await warmup;
        Check(lateModel == null,
            "reenabling a connector cannot cache an old page lifetime's in-flight warmup model");

        DisablePreview(preview);

        requestCount = AddressablesLogic.Requests;
        await preview.ShowModel("2");
        Check(AddressablesLogic.Requests == requestCount,
            "an inactive preparation connector cannot start another model request");
        ProgressLayer.Loading("2D lobby");
        warmup = preview.PrepareModel("2");
        Check(AddressablesLogic.Requests == requestCount + 1 && warmup.Status == UniTaskStatus.Pending,
            "the 2D lobby can deliberately warm up its inactive 3D connector");
        var preparedModel = Model();
        AddressablesLogic.Complete(preparedModel);
        await warmup;
        Check(preparedModel != null && !preparedModel.activeSelf && ReferenceEquals(preparedModel.transform.Parent, preview.transform)
            && preview.FocusingC == null && preview.InitializeCalls == 0 && ProgressLayer.Label == "2D lobby",
            "inactive warmup caches a hidden model without changing the view or loading curtain");
        await preview.PrepareModel("2");
        Check(AddressablesLogic.Requests == requestCount + 1, "later warmup reuses the inactive connector's cached model");

        UnityEngine.Object.Destroy(preview);
        await preview.PrepareModel("2");
        Check(AddressablesLogic.Requests == requestCount + 1, "destroyed connectors cannot start warmup requests");
    }

    static void DisablePreview(ModelView.DedicatedCameraConnector preview)
    {
        preview.enabled = false;
        typeof(ModelView.DedicatedCameraConnector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(preview, null);
    }

    static GameObject Model()
    {
        var model = new GameObject { Link = new OutsideDataLink { _C = new Data_Center() } };
        model.transform.gameObject = model;
        model.Link._C.WholeT = model.transform;
        return model;
    }
    static async UniTask<InvalidOperationException> Failure(UniTask<Data_Center> task)
    {
        try { await task; }
        catch (InvalidOperationException error) { return error; }
        throw new Exception("Expected an explicit unit preparation failure.");
    }
    static async UniTask<OperationCanceledException> Cancellation(UniTask<Data_Center> task)
    {
        try { await task; }
        catch (OperationCanceledException error) { return error; }
        throw new Exception("Expected unit preparation cancellation.");
    }
    static void CheckContext(Exception error, string reason) => Check(error.Message.Contains(reason)
        && error.Message.Contains("unit 2") && error.Message.Contains("human/tetsuya"), reason + " identifies the unit and model key");
    static bool IsMonotonic(List<float> progress)
    {
        for (var i = 1; i < progress.Count; i++)
            if (progress[i] < progress[i - 1] || progress[i] > 1f) return false;
        return true;
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAILED: " + message);
        checks++;
    }
}

public enum Element { Blue }
public sealed class UnitInfo { public string r_id; public object set; }
public sealed class UnitConfig { public string TYPE, REAL_NAME, BASIC_MOVEMENT_PACK; public Element element; }
public static class Units
{
    public static readonly Dictionary<string, UnitConfig> Configs = new Dictionary<string, UnitConfig>();
    public static UnitConfig Find_RECORD_ID(string id) => Configs.TryGetValue(id, out var config) ? config : null;
    public static UnitConfig RowToUnitConfigInfo(UnitConfig config) => config;
    public static UnitConfig GetUnitConfig(string id) => id == null ? null : Find_RECORD_ID(id);
}
public sealed class OutsideDataLink : UnityEngine.Object { public Data_Center _C; }
public sealed class Data_Center : UnityEngine.Object
{
    public Transform WholeT;
    public AnimationManger AnimationManger = new AnimationManger();
    public ShaderManager _ShaderManager = new ShaderManager();
    public Element element, Step2Element;
    public string Step1Type, Step1Pack, Step2Type;
    public object Step2Set;
    public int PreloadCount;
    public UniTaskCompletionSource<bool> Step1Gate, Step2Gate;
    public Exception Step1Error, Step2Error;
    public async UniTask Step1Initialize(string type, string pack, Action<float> onProgress)
    {
        Step1Type = type;
        Step1Pack = pack;
        onProgress?.Invoke(0f);
        if (Step1Gate != null) await Step1Gate.Task;
        if (Step1Error != null) throw Step1Error;
        onProgress?.Invoke(1f);
    }
    public async UniTask Step2Initialize(string type, Element element, object set, int preloadCount, Action<float> onProgress)
    {
        Step2Type = type;
        Step2Element = element;
        Step2Set = set;
        PreloadCount = preloadCount;
        onProgress?.Invoke(0f);
        if (Step2Gate != null) await Step2Gate.Task;
        if (Step2Error != null) throw Step2Error;
        onProgress?.Invoke(1f);
    }
}
public static class AddressablesLogic
{
    static UniTaskCompletionSource<GameObject> pending;
    public static int Requests;
    public static UniTask<GameObject> LoadObject(string key, Vector3 pos, Action<float> onProgress)
    {
        if (key != "human/tetsuya" || pending != null) throw new Exception("Unexpected model request.");
        Requests++;
        onProgress?.Invoke(0f);
        pending = new UniTaskCompletionSource<GameObject>();
        return pending.Task;
    }
    public static void Complete(GameObject model)
    {
        var request = pending;
        pending = null;
        if (request == null || !request.TrySetResult(model)) throw new Exception("No pending model request.");
    }
}
namespace UnityEngine
{
    public class Object
    {
        bool destroyed;
        public static void Destroy(Object value) { if (value != null) value.destroyed = true; }
        public static bool operator ==(Object a, Object b) =>
            (ReferenceEquals(a, null) || a.destroyed) ? (ReferenceEquals(b, null) || b.destroyed) : ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
    }
    public sealed class GameObject : Object
    {
        public readonly Transform transform = new Transform();
        public OutsideDataLink Link;
        public int Activations;
        public bool activeSelf = true;
        public T GetComponent<T>() where T : class => Link as T;
        public void SetActive(bool active) { activeSelf = active; if (active) Activations++; }
    }
    public class MonoBehaviour : Object
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled;
        public readonly Transform transform = new Transform();
    }
    public sealed class SerializeField : Attribute { }
    public sealed class Transform
    {
        public Transform Parent;
        public Vector3 position;
        public GameObject gameObject;
        public Transform transform => this;
        public void SetParent(Transform parent) { Parent = parent; }
    }
    public struct Vector3
    {
        public static Vector3 zero => default;
        public Vector3(float x, float y, float z) { }
        public static Vector3 operator -(Vector3 a, Vector3 b) => default;
    }
    public struct Color { public static Color black => default; }
    public sealed class Animator { public bool applyRootMotion; }
    public struct AnimatorStateInfo { public float normalizedTime; }
    public static class Mathf { public static float Lerp(float a, float b, float t) => a + (b - a) * t; }
}

public enum Facial { aggressive }
public sealed class AnimationManger
{
    public Animator AnimatorRef = new Animator();
    public void CasualFace() { }
    public UniTask PreloadPersonalAnimResourceMode(string type, string skill, Element element, int count) => UniTask.CompletedTask;
    public void AnimationTrigger(string skill, float duration) { }
    public void TriggerExpression(Facial face) { }
    public bool GetBool(string key) => false;
    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => default;
}
public sealed class ShaderManager { public void FlatColorForAShortTime(Color color, float from, float to) { } }
public static class Translate { public static string Get(string key) => key; }
public static class ProgressLayer
{
    public static string Label;
    public static int CloseCalls;
    public static void Loading(string label) { Label = label; }
    public static void Close() { CloseCalls++; Label = null; }
}
public static class HurtObjectManager { public static UniTask ConstructDPool() => UniTask.CompletedTask; }
public static class DicAdd<TKey, TValue>
{
    public static void Add(IDictionary<TKey, TValue> dictionary, TKey key, TValue value) { dictionary[key] = value; }
}
namespace dataAccess
{
    public static class Units { public static UnitInfo Get(string id) => null; }
}
namespace ModelView
{
    public partial class DedicatedCameraConnector : MonoBehaviour
    {
        public int InitializeCalls;
        public DedicatedCameraConnector() { unitName = new UnityEngine.UI.Text(); }
        void Initialize(bool fix, Transform focus, Transform holder) { InitializeCalls++; }
        void ItemDetailStartDirection(float x, float y, float z) { }
    }
}
namespace UnityEngine.UI { public sealed class Text { public string text; } }
namespace UnityEngine.Events { public delegate void UnityAction(); }
namespace DG.Tweening.Plugins.Options { public struct VectorOptions { } }
namespace DG.Tweening.Core
{
    public sealed class TweenerCore<T1, T2, TOptions>
    {
        public void Kill() { }
        public TweenerCore<T1, T2, TOptions> SetLink(GameObject target) => this;
    }
}
namespace DG.Tweening
{
    public static class ModelTweenDouble
    {
        public static Core.TweenerCore<Vector3, Vector3, Plugins.Options.VectorOptions> DOMove(this Transform target, Vector3 position, float duration) => new Core.TweenerCore<Vector3, Vector3, Plugins.Options.VectorOptions>();
    }
}
