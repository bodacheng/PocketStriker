using System;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using PlayFab;
using mainMenu;
using PlayFab.ClientModels;
using UniRx;
using UnityEngine;

public partial class ArenaPage : MSceneProcess
{
    void GetServerTime(DayOfWeek settlementDay, TimeSpan settlementTime)
    {
        PlayFabClientAPI.GetTime(new GetTimeRequest(), result => OnGetTimeSuccess(result, settlementDay, settlementTime), OnGetTimeError);
    }
    
    private void OnGetTimeSuccess(GetTimeResult result, DayOfWeek settlementDay, TimeSpan settlementTime)
    {
        DateTime serverTime = result.Time;
        Debug.Log($"Server time: {serverTime}");

        TimeSpan timeUntilNextSettlement;
        DateTime lastSettlement;

        (lastSettlement, timeUntilNextSettlement) = TimeUntilNextSettlement(serverTime, settlementDay, settlementTime);
        CheckSettlementPeriod(serverTime, lastSettlement, timeUntilNextSettlement);
    }

    private void OnGetTimeError(PlayFabError error)
    {
        Debug.LogError($"Failed to get server time: {error.GenerateErrorReport()}");
    }

    private (DateTime lastSettlement, TimeSpan timeUntilNextSettlement) TimeUntilNextSettlement(DateTime serverTime, DayOfWeek settlementDay, TimeSpan settlementTime)
    {
        DateTime nextSettlement = serverTime.Date.AddDays((int)(settlementDay - serverTime.DayOfWeek) % 7).Add(settlementTime);

        if (serverTime >= nextSettlement)
        {
            nextSettlement = nextSettlement.AddDays(7);
        }

        DateTime lastSettlement = nextSettlement.AddDays(-7);

        return (lastSettlement, nextSettlement - serverTime);
    }

    private IDisposable _disposeSettlementCountDown;
    private void CheckSettlementPeriod(DateTime serverTime, DateTime lastSettlement, TimeSpan timeUntilNextSettlement)
    {
        DateTime settlementStart = lastSettlement;
        DateTime settlementEnd = lastSettlement.AddHours(1);
        
        Debug.Log("settlementStart:"+ settlementStart);
        Debug.Log("settlementEnd:"+ settlementEnd);
        
        if (serverTime >= settlementStart && serverTime < settlementEnd)
        {
            PopupLayer.ArrangeWarnWindow(
                ReturnLayer.POP,
                Translate.Get("ArenaIsRefreshingPoint"));
            var popupLayer = UILayerLoader.Get<PopupLayer>();
            var span = settlementEnd - serverTime;
            _disposeSettlementCountDown =             
                Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(
                (_) =>
                {
                    span = span.Subtract(TimeSpan.FromSeconds(1));
                    string countDownTxt = span.ToString(@"hh\:mm\:ss");
                    PopupLayer.SetCurrentText(Translate.Get("ArenaIsRefreshingPoint") +"\n"+ countDownTxt);
                    if (span.TotalSeconds <= 0)
                    {
                        _disposeSettlementCountDown.Dispose();
                    }
                    
                }).AddTo(popupLayer.gameObject);
            SetLoaded(true);
        }
        else
        {
            CheckSeasonRankAndEnter(EnterProcess);
            this.timeUntilSettlement = timeUntilNextSettlement;
            Debug.Log($"Time until next arena settlement: {timeUntilNextSettlement}");
        }
    }
    
    private TimeSpan timeUntilSettlement;
}
