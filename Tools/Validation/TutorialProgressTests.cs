using System;
using System.Collections.Generic;
using System.Linq;
using PlayFab;
using PlayFab.ClientModels;

// Production persistence and queue code run unchanged; only preferences and the request dispatcher are local fixtures.
internal static class TutorialProgressTests
{
    static int checks;
    static readonly string[] Progress =
    {
        "Started", "SkillEditFinished", "StageOneFinished", "GotchaFinished", "SkillEditFinished2", "Finished"
    };

    public static int Main()
    {
        try
        {
            CheckCoalescing();
            CheckFailureAndRecovery();
            CheckAccountSwitchAndRetry();
            CheckPendingIsolation();
            CheckAllArrivalOrders();
            CheckLoginRecoveryAndCallbacks();
            Console.WriteLine("PASS: " + checks + " tutorial progress checks (local dispatcher; no network)");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    static void CheckCoalescing()
    {
        Login("coalesce", "ticket-1");
        Save("SkillEditFinished");
        var first = Dispatcher.Last;
        Check(first.Progress == "SkillEditFinished", "first marker starts a request");
        Save("StageOneFinished"); Save("GotchaFinished"); Save("StageOneFinished");
        Check(Dispatcher.For("coalesce").Count == 1, "later markers wait behind the in-flight request");
        Check(PlayerAccountInfo.Me.tutorialProgress == "GotchaFinished", "a late old marker cannot regress live UI state");
        Check(Pending() == "GotchaFinished", "pending marker records the latest stage before acknowledgement");
        first.Succeed();
        var latest = Dispatcher.Last;
        Check(latest.Progress == "GotchaFinished", "intermediate stages coalesce into the newest stage");
        Check(Pending() == "GotchaFinished", "old acknowledgement leaves the latest marker pending");
        first.Succeed();
        Check(Dispatcher.For("coalesce").Count == 2, "duplicate old callback cannot dispatch another request");
        latest.Succeed();
        Check(Dispatcher.Backend["coalesce"] == "GotchaFinished" && Pending() == null, "latest confirmed marker clears pending state");
        Save("StageOneFinished");
        Check(Dispatcher.For("coalesce").Count == 2 && Pending() == null, "old marker after confirmation cannot overwrite the server");
    }

    static void CheckFailureAndRecovery()
    {
        Login("failure", "ticket-old");
        Save("SkillEditFinished"); var first = Dispatcher.Last;
        Save("StageOneFinished"); first.Fail();
        Check(Dispatcher.Last.Progress == "StageOneFinished", "failed earlier stage does not discard newer queued progress");
        Dispatcher.Last.Fail();
        Check(Pending() == "StageOneFinished", "exhausted request leaves its latest marker durable");
        int count = Dispatcher.For("failure").Count;
        Login("failure", "ticket-new", "SkillEditFinished");
        Save("SkillEditFinished");
        Check(Dispatcher.For("failure").Count == count + 1, "later login retries failed pending progress");
        Check(Dispatcher.Last.Progress == "StageOneFinished", "recovery never uploads the older server marker");
        Check(Dispatcher.Last.Request.AuthenticationContext.ClientSessionTicket == "ticket-new", "recovery uses the new login's credential snapshot");
        Dispatcher.Last.Succeed();
        Check(Pending() == null, "successful recovery clears only its matching marker");
    }

    static void CheckAccountSwitchAndRetry()
    {
        Login("old-account", "old-ticket"); Save("StageOneFinished"); var old = Dispatcher.Last;
        Check(!ReferenceEquals(old.Request.AuthenticationContext, PlayFabSettings.staticPlayer), "request context is copied from the mutable SDK context");
        Login("fresh-account", "fresh-ticket"); Save("Started"); var fresh = Dispatcher.Last;
        old.Retry();
        Check(old.RetryAccount == "old-account" && old.RetryTicket == "old-ticket", "delayed retry cannot borrow the new account's session");
        old.Succeed();
        Check(PlayerAccountInfo.Me.tutorialProgress == "Started" && Pending() == "Started", "old account acknowledgement cannot advance or clear fresh account progress");
        fresh.Succeed();
        Check(Dispatcher.Backend["fresh-account"] == "Started", "fresh account only receives its own marker");

        Login("relogin-account", "first-ticket"); Save("StageOneFinished"); var inFlight = Dispatcher.Last;
        Login("relogin-account", "second-ticket", "StageOneFinished"); Save("GotchaFinished");
        inFlight.Retry();
        Check(inFlight.RetryTicket == "first-ticket", "same-account relogin does not mutate in-flight retry credentials");
        inFlight.Succeed();
        Check(Dispatcher.Last.Progress == "GotchaFinished" && Dispatcher.Last.Request.AuthenticationContext.ClientSessionTicket == "second-ticket",
            "next queued stage uses the refreshed account credentials");
        Dispatcher.Last.Succeed();

        Login("expired-ticket", "expired-session"); Save("Finished"); var expiring = Dispatcher.Last;
        Login("expired-ticket", "valid-session", "Finished"); Save("Finished");
        Check(Dispatcher.For("expired-ticket").Count == 1, "new login waits until the old credential request finishes");
        expiring.Fail();
        Check(Dispatcher.For("expired-ticket").Count == 2 && Dispatcher.Last.Progress == "Finished" &&
            Dispatcher.Last.Request.AuthenticationContext.ClientSessionTicket == "valid-session",
            "failed old ticket immediately retries identical progress with queued fresh credentials");
        Dispatcher.Last.Fail();
        Check(Dispatcher.For("expired-ticket").Count == 2 && Pending() == "Finished",
            "same failing credentials do not create an unbounded retry loop");
        Save("Finished"); Dispatcher.Last.Succeed();
    }

    static void CheckPendingIsolation()
    {
        Login("pending", "pending-ticket");
        PlayFabReadClient.RememberPendingTutorialProgress("SkillEditFinished2");
        PlayFabReadClient.RememberPendingTutorialProgress("StageOneFinished");
        Check(Pending() == "SkillEditFinished2", "RememberPending rejects an older stage");
        PlayFabReadClient.ClearPendingTutorialProgress("StageOneFinished", "pending");
        Check(Pending() == "SkillEditFinished2", "old receipt cannot erase the newer pending marker");
        UnityEngine.PlayerPrefs.SetString("PENDING_TUTORIAL_PROGRESS", "Finished");
        Login("no-pending", "new-ticket");
        Check(Pending() == null, "unscoped legacy and other-account markers do not skip a fresh tutorial");
        Save("Started"); Dispatcher.Last.Succeed();

        Login("auth-missing", null); int count = Dispatcher.Requests.Count;
        Save("StageOneFinished");
        Check(Dispatcher.Requests.Count == count && Pending() == "StageOneFinished", "missing auth preserves progress without dispatching an unbound request");
        PlayFabSettings.staticPlayer.PlayFabId = "different-account";
        PlayFabSettings.staticPlayer.ClientSessionTicket = "different-ticket";
        Save("GotchaFinished");
        Check(Dispatcher.Requests.Count == count && Pending() == "GotchaFinished", "mismatched SDK account cannot receive a tutorial marker");
    }

    static void CheckAllArrivalOrders()
    {
        int scenario = 0;
        foreach (var order in Permutations(Progress))
        {
            string id = "order-" + scenario++;
            Login(id, "ticket");
            foreach (string progress in order) Save(progress);
            Check(PlayerAccountInfo.Me.tutorialProgress == "Finished" && Pending() == "Finished", "arrival order cannot regress local completion");
            foreach (var request in Dispatcher.For(id).ToArray()) request.Succeed();
            // Completing the first request may have dispatched the coalesced final stage.
            foreach (var request in Dispatcher.For(id).Where(request => !request.Completed).ToArray()) request.Succeed();
            Check(Dispatcher.For(id).Count <= 2, "one in-flight marker plus the latest stage suffices for every arrival order");
            Check(Dispatcher.Backend[id] == "Finished" && Pending() == null, "every arrival order persists completion without server regression");
        }
        Check(scenario == 720, "all six-stage arrival permutations exercised");
    }

    static void CheckLoginRecoveryAndCallbacks()
    {
        Login("restore-stage-one", "ticket", "SkillEditFinished");
        PlayerAccountInfo.Me.arcadeProcess = 1;
        PlayFabReadClient.ReconcileTutorialProgressAfterDataLoad();
        Check(PlayerAccountInfo.Me.tutorialProgress == "StageOneFinished", "completed first-stage reward repairs an unsynced tutorial marker");
        Dispatcher.Last.Succeed();
        Login("restore-stage-two", "ticket", "SkillEditFinished2");
        PlayerAccountInfo.Me.arcadeProcess = 2;
        PlayFabReadClient.ReconcileTutorialProgressAfterDataLoad();
        Check(PlayerAccountInfo.Me.tutorialProgress == "Finished", "completed second-stage reward repairs an unsynced final marker");
        Dispatcher.Last.Succeed();

        TestClock.Clear();
        Login("timer-old-dev", "old-ticket");
        PlayFabReadClient.LoginSuccess(new LoginResult { PlayFabId = "timer-old-dev" }, PlayFabReadClient.LoginType.dev);
        Login("timer-new-normal", "new-ticket");
        PlayFabReadClient.LoginSuccess(new LoginResult { PlayFabId = "timer-new-normal" });
        int reads = PlayFabClientAPI.Reads.Count;
        TestClock.FireOne();
        Check(PlayFabClientAPI.Reads.Count == reads, "old-account delayed login attempt is cancelled before requesting current-account data");
        TestClock.FireOne();
        Check(PlayFabClientAPI.Reads.Count == reads + 1, "current-account login timer still requests its marker");
        var current = PlayFabClientAPI.Reads.Last();
        Check(current.Request.PlayFabId == "timer-new-normal" && current.Request.AuthenticationContext.ClientSessionTicket == "new-ticket",
            "tutorial read binds both account ID and matching credential snapshot");
        current.Complete(new Dictionary<string, UserDataRecord>());
        Check(PlayerAccountInfo.Me.tutorialProgress == "Started" && CloudScript.DevGrants == 0,
            "old dev login timer cannot mark a new normal account Finished or grant dev items");
        Dispatcher.Last.Succeed();

        TestClock.Clear();
        Login("read-old", "old-ticket"); PlayFabReadClient.LoginSuccess(new LoginResult { PlayFabId = "read-old" });
        TestClock.FireOne(); var old = PlayFabClientAPI.Reads.Last();
        Login("read-new", "new-ticket"); PlayFabReadClient.LoginSuccess(new LoginResult { PlayFabId = "read-new" });
        old.Complete(new Dictionary<string, UserDataRecord> { { "TutorialProgress", new UserDataRecord { Value = "Finished" } } });
        Check(PlayerAccountInfo.Me.tutorialProgress == null, "late old-account read cannot assign its progress to the new account");
        // Old attempt also scheduled a following timer; it ends before the new account's timer executes.
        TestClock.FireOne(); TestClock.FireOne();
        PlayFabClientAPI.Reads.Last().Complete(new Dictionary<string, UserDataRecord>());
        Check(PlayerAccountInfo.Me.tutorialProgress == "Started", "new account completes tutorial initialization after a discarded old response");
        Dispatcher.Last.Succeed();
        PlayFabClientAPI.Reads.Last().Complete(new Dictionary<string, UserDataRecord> { { "TutorialProgress", new UserDataRecord { Value = "Finished" } } });
        Check(PlayerAccountInfo.Me.tutorialProgress == "Started", "duplicate delayed read does not overwrite completed login initialization");
    }

    static IEnumerable<string[]> Permutations(string[] values)
    {
        if (values.Length == 0) { yield return Array.Empty<string>(); yield break; }
        for (int index = 0; index < values.Length; index++)
            foreach (var remainder in Permutations(values.Where((value, position) => position != index).ToArray()))
                yield return new[] { values[index] }.Concat(remainder).ToArray();
    }

    static void Login(string id, string ticket, string progress = "Started")
    {
        PlayerAccountInfo.Me = new PlayerAccountInfo { PlayFabId = id, tutorialProgress = progress };
        PlayFabSettings.staticPlayer.PlayFabId = id;
        PlayFabSettings.staticPlayer.ClientSessionTicket = ticket;
    }
    static void Save(string progress) => PlayFabReadClient.SaveTutorialProgressInBackground(progress);
    static string Pending() => PlayFabReadClient.GetPendingTutorialProgress();
    internal static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
}

internal static class Dispatcher
{
    internal sealed class Call
    {
        public UpdateUserDataRequest Request;
        public Action Success, Failure;
        public bool Completed;
        public string RetryAccount, RetryTicket;
        public string Account => Request.AuthenticationContext.PlayFabId;
        public string Progress => Request.Data["TutorialProgress"];
        public void Retry()
        {
            RetryAccount = Request.AuthenticationContext.PlayFabId;
            RetryTicket = Request.AuthenticationContext.ClientSessionTicket;
        }
        public void Succeed()
        {
            if (!Completed) Backend[Account] = Progress;
            Completed = true; Success();
        }
        public void Fail() { Completed = true; Failure(); }
    }
    public static readonly List<Call> Requests = new List<Call>();
    public static readonly Dictionary<string, string> Backend = new Dictionary<string, string>();
    public static Call Last => Requests[Requests.Count - 1];
    public static List<Call> For(string id) => Requests.Where(request => request.Account == id).ToList();
    public static void Dispatch(UpdateUserDataRequest request, Action success, Action failure)
    {
        TutorialProgressTests.Check(request.AuthenticationContext != null && request.AuthenticationContext.IsClientLoggedIn(), "every dispatch has bound credentials");
        TutorialProgressTests.Check(!Requests.Any(call => !call.Completed && call.Account == request.AuthenticationContext.PlayFabId), "at most one request is in flight per account");
        Requests.Add(new Call { Request = request, Success = success, Failure = failure });
    }
}

public partial class PlayFabReadClient
{
    public static void UpdateUserData(UpdateUserDataRequest request, Action success, Action failure, bool showError, bool showLoading)
    {
        TutorialProgressTests.Check(!showError && !showLoading, "tutorial marker remains nonblocking");
        Dispatcher.Dispatch(request, success, failure);
    }
    public static void GetAccountInfo(Action<bool> success) { }
    public static void DeleteAllLocalMails() { }
    public static void ErrorReport(PlayFabError error) { throw new Exception("Unexpected fixture login error"); }
}
public sealed class PlayerAccountInfo
{
    public static PlayerAccountInfo Me;
    public string PlayFabId, tutorialProgress;
    public int arcadeProcess;
}
namespace UnityEngine
{
    public static class Debug
    {
        public static void LogWarning(object message) { }
        public static void Log(object message) { }
        public static void LogError(object message) { throw new Exception(message.ToString()); }
        public static void LogException(Exception error) { throw error; }
    }
    public static class SystemInfo { public static string deviceUniqueIdentifier => "fixture-device"; }
    public static class Mathf
    {
        public static float Pow(float value, float power) => (float)Math.Pow(value, power);
        public static float Min(float first, float second) => Math.Min(first, second);
    }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static string GetString(string key, string fallback = "") => Values.TryGetValue(key, out var value) ? value : fallback;
        public static void SetString(string key, string value) => Values[key] = value;
        public static void DeleteKey(string key) => Values.Remove(key);
        public static void Save() { }
    }
}
namespace PlayFab
{
    // The SDK settings initializer reads native Unity Application state, unavailable in this command-line fixture.
    public static class PlayFabSettings
    {
        public static readonly PlayFabAuthenticationContext staticPlayer = new PlayFabAuthenticationContext();
        public static string TitleId => "fixture-title";
    }
    public static class PlayFabClientAPI
    {
        public sealed class Read
        {
            public GetUserDataRequest Request;
            public Action<GetUserDataResult> Callback;
            public void Complete(Dictionary<string, UserDataRecord> data) => Callback(new GetUserDataResult { Data = data });
        }
        public static readonly List<Read> Reads = new List<Read>();
        public static void GetUserData(GetUserDataRequest request, Action<GetUserDataResult> success, Action<PlayFabError> error) =>
            Reads.Add(new Read { Request = request, Callback = success });
        public static void LoginWithEmailAddress(LoginWithEmailAddressRequest request, Action<LoginResult> success, Action<PlayFabError> error) =>
            throw new Exception("Fixture does not dispatch login requests");
        public static void LoginWithCustomID(LoginWithCustomIDRequest request, Action<LoginResult> success, Action<PlayFabError> error) =>
            throw new Exception("Fixture does not dispatch login requests");
    }
}
public sealed class MissionWatcher
{
    public MissionWatcher(List<string> missions, Action completed, Action failed) { }
    public void Finish(string mission, bool success) { }
}
public static class ProgressLayer { public static void Loading(string value) { } }
public static class PopupLayer { public static void ArrangeWarnWindow(string value) { throw new Exception(value); } }
public static class CloudScript
{
    public static int DevGrants;
    public static void CheckIn(Action success) { }
    public static void Common(string name, Action<ExecuteCloudScriptResult> success) { DevGrants++; }
}
namespace mainMenu
{
    public enum MainSceneStep { FrontPage }
    public static class MainMenuNote { public static MainSceneStep GoingTo; }
}
namespace UnityEngine.SceneManagement { public static class SceneManager { public static void LoadScene(int scene) { } } }
internal static class TestClock
{
    static readonly Queue<Action> Callbacks = new Queue<Action>();
    public static Wait Delay(TimeSpan duration) => new Wait();
    public static void Clear() => Callbacks.Clear();
    public static void FireOne() { if (Callbacks.Count == 0) throw new Exception("No scheduled tutorial login attempt"); Callbacks.Dequeue()(); }
    public sealed class Wait
    {
        public Cysharp.Threading.Tasks.UniTask ContinueWith(Action continuation) { Callbacks.Enqueue(continuation); return default; }
        public System.Runtime.CompilerServices.TaskAwaiter<bool> GetAwaiter() => new System.Threading.Tasks.TaskCompletionSource<bool>().Task.GetAwaiter();
    }
}
