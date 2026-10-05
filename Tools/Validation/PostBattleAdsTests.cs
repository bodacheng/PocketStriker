using System;
using System.Collections.Generic;
using System.Reflection;
using GoogleMobileAds.Api;

// Only the transport and Unity view plumbing are doubled. Production policy,
// readiness, fullscreen exclusion, callback handling and reward delivery run.
internal static class PostBattleAdsTests
{
    static int checks;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    public static int Main()
    {
        try
        {
            CheckEligibility();
            CheckResults();
            CheckAdLifecycle();
            Console.WriteLine("PASS: " + checks + " offline post-battle ad checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static void CheckEligibility()
    {
        for (int stage = 1; stage <= 130; stage++)
            Check(PostBattleAdSession.IsEligible(FightEventType.Quest, stage.ToString()) == (stage > 5),
                "only the first five adventure stages are exempt: " + stage);
        foreach (FightEventType type in Enum.GetValues(typeof(FightEventType)))
        {
            bool real = type == FightEventType.Quest || type == FightEventType.Arena
                || type == FightEventType.Gangbang || type == FightEventType.Event;
            Check(PostBattleAdSession.IsEligible(type, "6") == real, "real battles only: " + type);
            Check(!PostBattleAdSession.IsEligible(type, "6", true), "tutorial attempts are exempt: " + type);
        }
        foreach (string id in new[] { "1", "2", "3", "4", "5", "01", " 5 " })
            Check(!PostBattleAdSession.IsEligible(FightEventType.Quest, id), "first-five parsing: " + id);
        foreach (string id in new[] { null, "", "invalid", "0", "-1", "2147483648" })
            Check(PostBattleAdSession.IsEligible(FightEventType.Quest, id), "invalid IDs cannot silently grant an exemption");
        Check(PostBattleAdSession.IsEligible(FightEventType.Event, "easy_1"), "Boss IDs ending in 1 are not adventure tutorial levels");
        Check(!PostBattleAdSession.IsEligible((FightEventType)999, "6"), "unknown battle types are exempt");
    }

    static void CheckResults()
    {
        foreach (FightEventType type in new[] { FightEventType.Quest, FightEventType.Arena, FightEventType.Event, FightEventType.Gangbang })
        {
            foreach (string outcome in new[] { "win", "loss", "replay", "reward server failure" })
            {
                var session = new PostBattleAdSession();
                session.BeginBattle(type, "6", false);
                int shown = 0;
                Func<bool> show = () => { shown++; return true; };
                Check(!session.TryPresent(true, false, true, false, show) && shown == 0,
                    "preloading never plays an ad: " + type + " " + outcome);
                session.CompleteBattle();
                Check(session.Pending && !session.TryPresent(true, false, false, false, show), "late loading waits on results");
                Check(session.TryPresent(true, false, true, false, show) && shown == 1, "complete battle plays once");
                session.CompleteBattle();
                Check(!session.TryPresent(true, false, true, false, show) && shown == 1, "duplicate result/callback cannot repeat ad");
                session.BeginBattle(type, "6", false);
                session.CompleteBattle();
                Check(session.TryPresent(true, false, true, false, show) && shown == 2, "same-stage retry gets a fresh placement");
            }
        }
        var pending = new PostBattleAdSession();
        pending.BeginBattle(FightEventType.Arena, "", false); pending.CompleteBattle();
        Check(!pending.TryPresent(true, false, true, true, () => { throw new Exception("overlapping ads"); }) && pending.Pending,
            "rewarded fullscreen presentation defers automatic ad");
        Check(!pending.TryPresent(true, true, true, false, () => { throw new Exception("ad-free purchase violated"); }) && !pending.Pending,
            "ad-free entitlement cancels even after a preload");
        pending.BeginBattle(FightEventType.Event, "hard_1", false); pending.CompleteBattle();
        Check(!pending.TryPresent(false, false, true, false, () => { throw new Exception("late ad outside result"); }) && !pending.Pending,
            "return/next battle cancels delayed ad");
        pending.BeginBattle(FightEventType.Quest, "6", false); pending.CompleteBattle();
        Check(!pending.TryPresent(true, false, true, false, () => false) && pending.Pending, "SDK not-ready failure remains pending");
        Check(pending.TryPresent(true, false, true, false, () =>
        {
            Check(!pending.TryPresent(true, false, true, false, () => { throw new Exception("reentrant show"); }),
                "placement consumed before SDK callbacks");
            return true;
        }) && !pending.Pending, "successful retry consumes the pending placement");
        pending.BeginBattle(FightEventType.Event, "easy_1", false); pending.CompleteBattle();
        Check(!pending.TryPresent(true, false, true, false, () => { pending.Cancel(); return false; }) && !pending.Pending,
            "cancel inside failed SDK show cannot restore previous result");
        foreach (FightEventType type in new[] { FightEventType.Self, FightEventType.SkillTest, FightEventType.Screensaver, FightEventType.Quest })
        {
            pending.BeginBattle(type, "5", false); pending.CompleteBattle(); pending.CompleteBattle();
            Check(!pending.Pending, "exempt results never schedule a placement: " + type);
        }
    }

    static AdmobAdsButton Button(bool reward = false)
    {
        var button = new AdmobAdsButton();
        // Automatic battle placements have no button or graphics. Retain the
        // rewarded view here to cover shop placements using the same lifecycle.
        if (reward)
            typeof(AdmobAdsButton).GetField("_showAdButton", Fields).SetValue(button, new UnityEngine.UI.Button());
        var type = typeof(AdmobAdsButton).GetField("adType", Fields);
        type.SetValue(button, Enum.Parse(type.FieldType, reward ? "Reward" : "Interstitial"));
        if (!reward) button.UseInterstitialAd();
        button.HasTicket = true;
        return button;
    }

    static void Destroy(AdmobAdsButton button) => typeof(AdmobAdsButton).GetMethod("OnDestroy", Fields).Invoke(button, null);
    static void CheckAdLifecycle()
    {
        PlayerAccountInfo.Me = new PlayerAccountInfo();
        var passive = Button();
        AdsInitializer.IsReady = false;
        passive.LoadAd(); passive.LoadAd();
        Check(InterstitialAd.Pending.Count == 0, "preloads wait for consent/SDK ready");
        AdsInitializer.Ready();
        Check(InterstitialAd.Pending.Count == 1, "consent-ready subscribes once");
        passive.LoadAd();
        Check(InterstitialAd.Pending.Count == 1, "duplicate preload shares one SDK request");
        var ordinary = InterstitialAd.CompleteLoad();
        Check(passive.AdIsReady, "loaded interstitial reports ready");
        int bonusRewards = 0;
        passive.SetWatchedAdExtraProcess(() => bonusRewards++);
        Check(passive.TryShowAd() && AdmobAdsButton.IsFullScreenAdShowing && !passive.AdIsReady,
            "interstitial reserves fullscreen slot before opened callback");
        passive.LoadAd();
        Check(InterstitialAd.Pending.Count == 0 && ordinary.DestroyCount == 0,
            "repeated preload cannot replace the presenting SDK instance");

        var rewarded = Button(true); rewarded.LoadAd();
        var bonus = RewardedAd.CompleteLoad();
        rewarded.SetWatchedAdExtraProcess(() => bonusRewards++);
        Check(!rewarded.TryShowAd() && bonus.ShowCount == 0, "bonus ad cannot overlap automatic ad");
        ordinary.Open(); Check(AppSetting.Value.Muted, "fullscreen opening mutes music");
        ordinary.Close(); ordinary.Close();
        Check(!AdmobAdsButton.IsFullScreenAdShowing && !AppSetting.Value.Muted && ordinary.DestroyCount == 1,
            "interstitial close releases slot/audio exactly once");
        Check(InterstitialAd.Pending.Count == 1 && bonusRewards == 0, "ordinary interstitial grants no bonus and reloads once");
        var next = InterstitialAd.CompleteLoad();

        Check(rewarded.TryShowAd() && bonus.ShowCount == 1, "optional reward remains independently available");
        rewarded.SetWatchedAdExtraProcess(() => bonusRewards += 100);
        Check(!passive.TryShowAd(), "automatic ad waits for an active rewarded ad");
        bonus.Open(); bonus.GrantReward(); bonus.GrantReward();
        Check(bonusRewards == 1, "reward callback uses the show-time award exactly once");
        bonus.Close();
        Check(!AdmobAdsButton.IsFullScreenAdShowing && !AppSetting.Value.Muted, "reward close releases slot/audio");

        PlayerAccountInfo.Me.noAdsState = true;
        Check(!passive.TryShowAd() && next.ShowCount == 0, "ad-free entitlement blocks preloaded SDK ad");
        var adFree = Button(); adFree.LoadAd();
        Check(InterstitialAd.Pending.Count == 0, "ad-free entitlement skips new preload requests");
        PlayerAccountInfo.Me.noAdsState = false;
        next.ThrowOnShow = true;
        Check(!passive.TryShowAd() && !AdmobAdsButton.IsFullScreenAdShowing && !AppSetting.Value.Muted,
            "synchronous SDK error cannot retain the fullscreen lock");
        Check(next.DestroyCount == 1 && InterstitialAd.Pending.Count == 1, "failed SDK instance is destroyed and replacement loads once");
        var replacement = InterstitialAd.CompleteLoad();
        Check(passive.TryShowAd(), "replacement can show after synchronous failure");
        replacement.Open(); next.Open(); next.Fail(); next.Close();
        Check(AdmobAdsButton.IsFullScreenAdShowing && AppSetting.Value.Muted && replacement.DestroyCount == 0,
            "late callbacks from a discarded SDK instance cannot finish the replacement");
        replacement.Fail(); replacement.Close();
        Check(!AdmobAdsButton.IsFullScreenAdShowing && !AppSetting.Value.Muted && replacement.DestroyCount == 1,
            "asynchronous SDK failure and late close finish once");
        var final = InterstitialAd.CompleteLoad();
        Check(passive.TryShowAd(), "another battle can show the reloaded ad");
        final.Open(); PlayerAccountInfo.Me.noAdsState = true; final.Close();
        Check(InterstitialAd.Pending.Count == 0, "close does not reload after ad-free entitlement changes");
        PlayerAccountInfo.Me.noAdsState = false;
        passive.LoadAd(); var destroyedAd = InterstitialAd.CompleteLoad();
        Check(passive.TryShowAd(), "destroy fixture owns fullscreen slot");
        destroyedAd.Open(); Destroy(passive);
        Check(!AdmobAdsButton.IsFullScreenAdShowing && !AppSetting.Value.Muted, "destroying placement releases fullscreen/audio");
        Destroy(rewarded); Destroy(adFree);

        var pending = Button(); pending.LoadAd(); Destroy(pending);
        var late = InterstitialAd.CompleteLoad();
        Check(late.DestroyCount == 1 && !pending.AdIsReady, "late SDK load destroys ad after placement is removed");
        AdsInitializer.IsReady = false;
        var waiting = Button(); waiting.LoadAd(); Destroy(waiting); AdsInitializer.Ready();
        Check(InterstitialAd.Pending.Count == 0, "destroyed placement unsubscribes consent-ready callback");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
        checks++;
    }
}

namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public class MonoBehaviour { }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float red, float green, float blue, float alpha) { r = red; g = green; b = blue; a = alpha; }
    }
    public static class Debug
    {
        public static bool isDebugBuild = true;
        public static void Log(object value) { }
        public static void LogWarning(object value) { }
        public static void LogError(object value) { }
    }
}
namespace UnityEngine.UI
{
    public class Button { public bool interactable; }
    public class Text { public string text; }
    public class Image { public UnityEngine.Color color; }
}
public class PlayerAccountInfo { public static PlayerAccountInfo Me; public bool noAdsState; }
public class AppSetting
{
    public static readonly AppSetting Value = new AppSetting();
    public bool Muted;
    public void Mute() { Muted = true; }
    public void UnMute() { Muted = false; }
}
public static class CommonSetting
{
    public const string Admob_interstitial_androidKey = "offline-interstitial";
    public const string Admob_rewarded_androidKey = "offline-rewarded";
}
public static class AdsInitializer
{
    public static bool IsReady;
    public static event Action AdsReady;
    public static bool ShouldEnableAds() => true;
    public static void Ready() { IsReady = true; AdsReady?.Invoke(); }
}
namespace GoogleMobileAds.Common
{
    public static class MobileAdsEventExecutor { public static void ExecuteInUpdate(Action action) => action(); }
}
namespace GoogleMobileAds.Api
{
    public class AdRequest { }
    public class AdError { }
    public class LoadAdError { }
    public class AdValue { public long Value; public string CurrencyCode; }
    public class Reward { }
    public abstract class OfflineAd
    {
        public event Action<AdValue> OnAdPaid;
        public event Action OnAdImpressionRecorded;
        public event Action OnAdClicked;
        public event Action OnAdFullScreenContentOpened;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public bool Ready = true, ThrowOnShow;
        public int ShowCount, DestroyCount;
        public bool CanShowAd() => Ready;
        public string GetResponseInfo() => "offline fixture";
        public void Destroy() { DestroyCount++; Ready = false; }
        protected void StartShow()
        {
            if (ThrowOnShow) throw new InvalidOperationException("controlled show error");
            ShowCount++; Ready = false;
        }
        public void Open() => OnAdFullScreenContentOpened?.Invoke();
        public void Close() => OnAdFullScreenContentClosed?.Invoke();
        public void Fail() => OnAdFullScreenContentFailed?.Invoke(new AdError());
    }
    public class InterstitialAd : OfflineAd
    {
        public static readonly Queue<Action<InterstitialAd, LoadAdError>> Pending = new Queue<Action<InterstitialAd, LoadAdError>>();
        public static void Load(string id, AdRequest request, Action<InterstitialAd, LoadAdError> callback) => Pending.Enqueue(callback);
        public static InterstitialAd CompleteLoad() { var ad = new InterstitialAd(); Pending.Dequeue()(ad, null); return ad; }
        public void Show() => StartShow();
    }
    public class RewardedAd : OfflineAd
    {
        public static readonly Queue<Action<RewardedAd, LoadAdError>> Pending = new Queue<Action<RewardedAd, LoadAdError>>();
        Action<Reward> reward;
        public static void Load(string id, AdRequest request, Action<RewardedAd, LoadAdError> callback) => Pending.Enqueue(callback);
        public static RewardedAd CompleteLoad() { var ad = new RewardedAd(); Pending.Dequeue()(ad, null); return ad; }
        public void Show(Action<Reward> callback) { StartShow(); reward = callback; }
        public void GrantReward() => reward?.Invoke(new Reward());
    }
}
