using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Uses the event reward contract, but never loads the retired fixed Boss encounters.
public sealed class RandomBossPage : MSceneProcess
{
    CancellationTokenSource _enterCancellation;
    EventBattleTop _layer;

    public RandomBossPage()
    {
        Step = MainSceneStep.RandomBoss;
    }

    public override void ProcessEnter()
    {
        _enterCancellation?.Cancel();
        _enterCancellation?.Dispose();
        _enterCancellation = new CancellationTokenSource();
        SetLoaded(false);
        EnterAsync(_enterCancellation.Token).Forget();
    }

    async UniTask EnterAsync(CancellationToken token)
    {
        ProgressLayer.Loading(string.Empty);
        try
        {
            var timeSource = new UniTaskCompletionSource<DateTime>();
            PlayFabClientAPI.GetTime(new GetTimeRequest(),
                result => timeSource.TrySetResult(result.Time),
                error => timeSource.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            var serverTime = await timeSource.Task.AttachExternalCancellation(token);

            var progressSource = new UniTaskCompletionSource<ExecuteCloudScriptResult>();
            PlayFabReadClient.GetCompletedLevels(
                result => progressSource.TrySetResult(result),
                error => progressSource.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            var progress = await progressSource.Task.AttachExternalCancellation(token);
            if (!EventModeManager.Instance.TryReadCompletedLevels(progress))
                throw new InvalidOperationException("Could not load random Boss completion records.");

            await EventModeManager.Instance.InitializeRandomMode(
                serverTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).AttachExternalCancellation(token);
            token.ThrowIfCancellationRequested();
            _layer = UILayerLoader.Load<EventBattleTop>();
            _layer.SetupCommon();
            EventModeManager.Instance.SetupRandomMode(_layer);
            var representative = EventModeManager.Instance.GetRepresentativeUnit();
            if (representative != null)
                await _layer.IconButtonFeature(representative, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                Debug.LogWarning($"[RandomBoss] {exception.Message}");
                PopupLayer.ArrangeWarnWindow(Translate.Get("RandomBossLoadFailed"));
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                ProgressLayer.Close();
                LowerMainBar.Open();
                SetLoaded(true);
            }
        }
    }

    public override void ProcessEnd()
    {
        _enterCancellation?.Cancel();
        _enterCancellation?.Dispose();
        _enterCancellation = null;
        SetLoaded(false);
        ProgressLayer.Close();
        UILayerLoader.Remove<EventBattleTop>();
        _layer = null;
    }
}
