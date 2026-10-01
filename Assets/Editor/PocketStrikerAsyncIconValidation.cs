using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using mainMenu;
using Skill;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real presentation prefabs with controlled asynchronous completion, no account/network.</summary>
[InitializeOnLoad]
public static class PocketStrikerAsyncIconValidation
{
    const string Key = "PocketStriker.AsyncIcons";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static string Output => "Logs/UIQuality/" + (Environment.GetEnvironmentVariable("POCKETSTRIKER_ICON_REVIEW") ?? "async-icons");
    static bool finishing;
    static Report report;
    static Transform canvas;
    static GameObject view;
    static readonly List<SKStoneItem> generated = new List<SKStoneItem>();
    static string firstId, lastId;
    static Sprite firstSprite, lastSprite;
    [Serializable] public sealed class Report
    {
        public bool passed;
        public string utcTime, unityVersion;
        public string scope = "Isolated Editor Play mode: authored MiniNineSlot and InBattleEnvolve/SkillForChoose prefabs, real stoneModel and authored skill sprites. Per-view Editor-only loader gates control completion, null and failure. Production display/clear/destroy/click code runs. Not an account or remote-download test.";
        public List<string> checks = new List<string>(), errors = new List<string>();
        public string firstSkill, latestSkill;
    }
    sealed class Pending
    {
        public string id;
        public UniTaskCompletionSource<SKStoneItem> source = new UniTaskCompletionSource<SKStoneItem>();
    }
    sealed class Gate
    {
        public readonly List<Pending> requests = new List<Pending>();
        public UniTask<SKStoneItem> Load(string id)
        {
            var item = new Pending { id = id }; requests.Add(item); return item.source.Task;
        }
        public void Complete(int from = 0, int count = -1)
        {
            if (count < 0) count = requests.Count - from;
            foreach (var request in requests.Skip(from).Take(count)) request.source.TrySetResult(Icon(request.id));
        }
    }
    static PocketStrikerAsyncIconValidation()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Poll;
    }
    public static void StartBatch()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run from a stopped, separate batch Editor.");
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Running", false);
        SessionState.SetString(Key + ".Start", DateTime.UtcNow.ToString("O"));
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView", true);
        gameView.GetMethod("SetCustomResolution", Fields).Invoke(EditorWindow.GetWindow(gameView),
            new object[] { new Vector2(540, 960), "Async icon regression" });
        EditorApplication.isPlaying = true;
    }
    static void Poll()
    {
        if (finishing || !SessionState.GetBool(Key, false)) return;
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + ".Start", DateTime.UtcNow.ToString("O")))).TotalSeconds > 100)
        { report ??= new Report(); report.errors.Add("Async icon regression deadline exceeded."); Finish(); return; }
        if (!EditorApplication.isPlaying || SessionState.GetBool(Key + ".Running", false)) return;
        SessionState.SetBool(Key + ".Running", true); Run().Forget();
    }
    static void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.errors.Add(text + "\n" + stack);
    }
    static async UniTask Run()
    {
        report = new Report { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        Directory.CreateDirectory(Output); Application.logMessageReceived += Log;
        try
        {
            var entries = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g != null)
                .SelectMany(g => g.entries).Where(e => int.TryParse(e.address, out _) && AssetDatabase.LoadAssetAtPath<Sprite>(e.AssetPath) != null)
                .OrderBy(e => int.Parse(e.address)).Take(2).ToArray();
            Require(entries.Length == 2, "Two authored skill sprites are required.");
            firstId = entries[0].address; lastId = entries[1].address;
            firstSprite = AssetDatabase.LoadAssetAtPath<Sprite>(entries[0].AssetPath);
            lastSprite = AssetDatabase.LoadAssetAtPath<Sprite>(entries[1].AssetPath);
            report.firstSkill = firstId; report.latestSkill = lastId;
            var root = new GameObject("Async icon fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = root.transform; root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            var cam = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.10f,.15f,.19f,1);
            var heading = new GameObject("Fixture label", typeof(RectTransform), typeof(Text)); heading.transform.SetParent(canvas,false);
            var title = heading.GetComponent<Text>(); title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 38; title.alignment = TextAnchor.MiddleCenter; title.text = "Async presentation regression\nLatest selection: skill " + lastId;
            var rt = heading.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(.5f,.85f); rt.sizeDelta = new Vector2(1000,200);
            await Check("grid-newer-selection-wins-and-click-matches", async () =>
            {
                var nine = Grid(out var gate);
                var old = Show(nine, firstId); var latest = Show(nine, lastId);
                gate.Complete(9,9); await latest; gate.Complete(0,9); await old; await Frames();
                await Screenshot("grid-latest-selection");
                string clicked = null; nine.AddOnClickToSlots((Action<string>)(id => clicked = id)); nine.A1T.onClick.Invoke();
                Require(clicked == lastId, "The current slot click selects a superseded skill.");
                Require(nine.GetComponentsInChildren<SKStoneItem>(true).Length == 9 && Alive() == 9,
                    "Superseded grid icons survived or overlap the current selection.");
                Require(nine.AllButton().All(b => b.GetComponentInChildren<SKStoneItem>()._SkillConfig.RECORD_ID == lastId), "Grid contains stale icons.");
            });
            await Check("grid-clear-invalidates-pending", async () =>
            {
                var nine = Grid(out var gate); var loading = Show(nine, firstId); nine.ClearCurrent(); gate.Complete(); await loading; await Frames();
                Require(Alive() == 0, "A completed old request repopulated a cleared grid.");
                var retry = Show(nine, lastId); gate.Complete(9,9); await retry; await Frames();
                Require(Alive() == 9, "Grid did not recover after clear.");
            });
            await Check("grid-destroy-releases-late-icons", async () =>
            {
                var nine = Grid(out var gate); var loading = Show(nine, firstId); Object.Destroy(view); await Frames();
                gate.Complete(); await loading; await Frames(); Require(Alive() == 0, "Icons survived their destroyed grid.");
            });
            await Check("grid-partial-failure-releases-early-and-late-icons", async () =>
            {
                var nine = Grid(out var gate); var loading = Show(nine, firstId);
                gate.Complete(0,2); gate.requests[2].source.TrySetException(new InvalidOperationException("Injected icon failure"));
                bool failed = false; try { await loading; } catch (InvalidOperationException) { failed = true; }
                gate.Complete(3,6); await Frames();
                Require(failed, "The grid swallowed a load failure."); Require(Alive() == 0, "Partial failure leaked completed or late icons.");
                var retry = Show(nine,lastId); gate.Complete(9,9); await retry; await Frames(); Require(Alive() == 9, "Retry after failure failed.");
            });
            await Check("grid-empty-slots-are-valid", async () =>
            {
                var nine = Grid(out var gate); var loading = nine.ShowStones(lastId,null,null,null,null,null,null,null,null);
                gate.Complete(); await loading; await Frames(); Require(Alive() == 1 && nine.A1T.GetComponentInChildren<SKStoneItem>() != null, "Empty skills broke the grid.");
            });
            await Check("detail-newer-selection-wins", async () =>
            {
                var detail = Detail(out var gate); detail.IconForShow(firstId,180); detail.IconForShow(lastId,180);
                gate.Complete(1,1); await Frames(); gate.Complete(0,1); await Frames(); await Screenshot("detail-latest-selection");
                Require(Alive() == 1 && CurrentDetail(detail)?._SkillConfig.RECORD_ID == lastId, "Stale detail icon overwrote latest selection.");
            });
            await Check("detail-clear-invalidates-pending-and-can-retry", async () =>
            {
                var detail = Detail(out var gate); detail.IconForShow(firstId,180); detail.Clear(); gate.Complete(); await Frames();
                Require(Alive() == 0, "A late request refilled cleared detail.");
                detail.IconForShow(lastId,180); gate.Complete(1,1); await Frames(); Require(Alive() == 1, "Detail retry after clear failed.");
            });
            await Check("detail-destroy-releases-late-icon", async () =>
            {
                var detail = Detail(out var gate); detail.IconForShow(firstId,180); Object.Destroy(view); await Frames(); gate.Complete(); await Frames();
                Require(Alive() == 0, "Late detail icon survived the page.");
            });
            await Check("detail-null-result-and-recovery", async () =>
            {
                var detail = Detail(out var gate); int errors = report.errors.Count;
                detail.IconForShow(firstId,180); gate.requests[0].source.TrySetResult(null); await Frames();
                Require(report.errors.Count == errors && Alive() == 0, "Missing icon threw instead of leaving a clean detail.");
                detail.IconForShow(lastId,180); gate.Complete(1,1); await Frames(); Require(Alive() == 1, "Valid selection after missing icon failed.");
            });
        }
        catch (Exception ex) { report.errors.Add(ex.ToString()); }
        finally { Application.logMessageReceived -= Log; Finish(); }
    }
    static NineForShow Grid(out Gate gate)
    {
        view = Object.Instantiate(Resources.Load<GameObject>("DummyLayerSystem/skillEdit/MiniNineSlot"),canvas,false);
        var rt = (RectTransform)view.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(180,180); rt.localScale = Vector3.one * 4;
        var nine = view.GetComponentInChildren<NineForShow>(true); gate = Bind(nine); return nine;
    }
    static SkillStoneDetail Detail(out Gate gate)
    {
        view = Object.Instantiate(Resources.Load<GameObject>("DummyLayerSystem/InBattleEnvolve/SkillForChoose"),canvas,false);
        var rt = (RectTransform)view.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(500,800);
        foreach (var animator in view.GetComponentsInChildren<Animator>()) animator.enabled = false;
        var detail = view.GetComponentInChildren<SkillStoneDetail>(true); detail.Clear();
        Field<Text>(detail,"showName").text = "Latest skill: " + lastId; Field<Text>(detail,"skillIntro").text = "Old completion must not replace the selected icon.";
        gate = Bind(detail); return detail;
    }
    static Gate Bind(object target)
    {
        var gate = new Gate(); target.GetType().GetField("iconLoaderForValidation",Fields).SetValue(target,(Func<string,UniTask<SKStoneItem>>)gate.Load); return gate;
    }
    static SKStoneItem CurrentDetail(SkillStoneDetail detail) => Field<RectTransform>(detail,"iconShowT").GetComponentInChildren<SKStoneItem>();
    static SKStoneItem Icon(string id)
    {
        if (id == null) return null;
        var go = Object.Instantiate(Resources.Load<GameObject>("BasicSprites/stoneModel"));
        var item = go.GetComponent<SKStoneItem>(); item.enabled = false; item._SkillConfig = new SkillConfig { RECORD_ID = id };
        go.name = "Async test icon " + id; go.GetComponent<Image>().sprite = id == firstId ? firstSprite : lastSprite;
        generated.Add(item); return item;
    }
    static UniTask Show(NineForShow nine,string id) => nine.ShowStones(id,id,id,id,id,id,id,id,id);
    static int Alive() => generated.Count(x => x != null);
    static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,Fields).GetValue(target);
    static async UniTask Check(string name,Func<UniTask> action)
    {
        int errors = report.errors.Count;
        try { await action(); if (report.errors.Count == errors) report.checks.Add(name); }
        catch(Exception e) { report.errors.Add(name + ": " + e.Message); }
        Object.Destroy(view); foreach(var item in generated) if(item != null) Object.Destroy(item.gameObject);
        await Frames(); generated.Clear(); Debug.Log("[AsyncIcons] completed " + name);
    }
    static UniTask Frames() => UniTask.DelayFrame(3);
    static async UniTask Screenshot(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Output,name + ".png"))); await UniTask.Delay(150,DelayType.Realtime);
    }
    static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    static void Finish()
    {
        if(finishing) return; finishing=true;
        report.passed = report.errors.Count == 0 && report.checks.Count == 9;
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(report,true));
        SessionState.SetBool(Key,false); EditorApplication.update -= Poll;
        Debug.Log("[AsyncIcons] " + (report.passed ? "PASS" : "FAIL") + " " + report.checks.Count + "/9");
        EditorApplication.isPlaying=false; EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
