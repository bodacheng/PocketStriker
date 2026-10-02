using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public partial class PlayFabReadClient
{
    const string PENDING_TUTORIAL_PROGRESS = "PENDING_TUTORIAL_PROGRESS";
    static readonly string[] TutorialProgressOrder =
    {
        "Started", "SkillEditFinished", "StageOneFinished", "GotchaFinished", "SkillEditFinished2", "Finished"
    };
    static readonly TutorialProgressSaveQueue TutorialProgressSaves = new TutorialProgressSaveQueue(
        (request, success, failure) => UpdateUserData(request, success, failure, false, false),
        (accountId, progress) => ClearPendingTutorialProgress(progress, accountId),
        (accountId, progress) => Debug.LogWarning($"Failed to save tutorial progress '{progress}'. It will be retried at the next login."));

    // Local progress can continue while its marker is offline. Never replace a later marker with an old callback.
    public static void RememberPendingTutorialProgress(string progress)
    {
        var key = PendingTutorialProgressKey(PlayerAccountInfo.Me?.PlayFabId);
        if (TutorialProgressIndex(progress) < 0 || key == null) return;
        progress = LaterTutorialProgress(progress, PlayerAccountInfo.Me.tutorialProgress);
        progress = LaterTutorialProgress(progress, PlayerPrefs.GetString(key, null));
        PlayerPrefs.SetString(key, progress);
        PlayerPrefs.Save();
    }

    // An unsynced marker from another account must never skip a fresh account's tutorial.
    static string PendingTutorialProgressKey(string playFabId) =>
        string.IsNullOrEmpty(playFabId) ? null : PENDING_TUTORIAL_PROGRESS + ":" + playFabId;

    public static string GetPendingTutorialProgress()
    {
        var key = PendingTutorialProgressKey(PlayerAccountInfo.Me?.PlayFabId);
        return key == null || !PlayerPrefs.HasKey(key) ? null : PlayerPrefs.GetString(key);
    }

    public static void ClearPendingTutorialProgress(string progress, string playFabId = null)
    {
        var key = PendingTutorialProgressKey(playFabId ?? PlayerAccountInfo.Me?.PlayFabId);
        if (key == null || PlayerPrefs.GetString(key, null) != progress) return;
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    public static void SaveTutorialProgressInBackground(string progress)
    {
        var account = PlayerAccountInfo.Me;
        if (account == null || string.IsNullOrEmpty(account.PlayFabId) || TutorialProgressIndex(progress) < 0) return;
        progress = LaterTutorialProgress(progress, account.tutorialProgress);
        progress = LaterTutorialProgress(progress, GetPendingTutorialProgress());
        progress = LaterTutorialProgress(progress, TutorialProgressSaves.GetLatestProgress(account.PlayFabId));
        account.tutorialProgress = progress;
        RememberPendingTutorialProgress(progress);

        // staticPlayer is mutated by subsequent logins. Each queued request/retry owns a credential snapshot.
        var context = new PlayFabAuthenticationContext();
        context.CopyFrom(PlayFabSettings.staticPlayer);
        if (context.PlayFabId != account.PlayFabId || !context.IsClientLoggedIn()) return;
        TutorialProgressSaves.Enqueue(account.PlayFabId, progress, context);
    }

    static bool IsTutorialProgressAhead(string candidate, string current) =>
        TutorialProgressIndex(candidate) >= 0 && TutorialProgressIndex(candidate) > TutorialProgressIndex(current);

    internal static string LaterTutorialProgress(string progress, string other) =>
        IsTutorialProgressAhead(other, progress) ? other : progress;

    static int TutorialProgressIndex(string progress) => Array.IndexOf(TutorialProgressOrder, progress);
}

// One request (including its retries) per account; intermediate stages coalesce into the latest stage.
internal sealed class TutorialProgressSaveQueue
{
    sealed class AccountSave
    {
        public string LatestProgress;
        public string SavedProgress;
        public PlayFabAuthenticationContext Context;
        public bool InFlight;
    }

    readonly Dictionary<string, AccountSave> accounts = new Dictionary<string, AccountSave>();
    readonly Action<UpdateUserDataRequest, Action, Action> dispatch;
    readonly Action<string, string> acknowledge;
    readonly Action<string, string> failed;

    public TutorialProgressSaveQueue(Action<UpdateUserDataRequest, Action, Action> dispatch,
        Action<string, string> acknowledge, Action<string, string> failed)
    {
        this.dispatch = dispatch;
        this.acknowledge = acknowledge;
        this.failed = failed;
    }

    public string GetLatestProgress(string accountId) =>
        accounts.TryGetValue(accountId, out var save) ? save.LatestProgress : null;

    public void Enqueue(string accountId, string progress, PlayFabAuthenticationContext context)
    {
        if (!accounts.TryGetValue(accountId, out var save))
        {
            save = new AccountSave();
            accounts.Add(accountId, save);
        }
        save.LatestProgress = PlayFabReadClient.LaterTutorialProgress(progress, save.LatestProgress);
        save.Context = context;
        if (save.InFlight) return;
        if (save.SavedProgress == save.LatestProgress)
        {
            acknowledge(accountId, save.LatestProgress);
            return;
        }
        SendLatest(accountId, save);
    }

    void SendLatest(string accountId, AccountSave save)
    {
        var progress = save.LatestProgress;
        var request = new UpdateUserDataRequest
        {
            AuthenticationContext = save.Context,
            Data = new Dictionary<string, string> { { "TutorialProgress", progress } }
        };
        save.InFlight = true;
        bool completed = false;
        void Complete(bool success)
        {
            if (completed) return;
            completed = true;
            save.InFlight = false;
            if (success)
            {
                save.SavedProgress = progress;
                acknowledge(accountId, progress);
            }
            else failed(accountId, progress);
            // A same-account login may have queued fresh credentials while the old ticket was failing.
            bool refreshedCredentials = !success &&
                save.Context.ClientSessionTicket != request.AuthenticationContext.ClientSessionTicket;
            if (save.LatestProgress != progress || refreshedCredentials) SendLatest(accountId, save);
        }
        dispatch(request, () => Complete(true), () => Complete(false));
    }
}
