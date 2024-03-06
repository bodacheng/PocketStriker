using System;
using System.Collections.Generic;
using DummyLayerSystem;
using mainMenu;
using PlayFab.ClientModels;
using UnityEngine;
using Crosstales.BWF;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;

public partial class ArenaPage : MSceneProcess
{
    ArenaLayer arenaLayer;
    
    public ArenaPage()
    {
        Step = MainSceneStep.Arena;
    }
    
    void ItemsLoadFinished(bool value)
    {
        missionWatcher.Finish("itemsLoadFinished", value);
    }
    
    void LeaderBoardFinished(bool value)
    {
        missionWatcher.Finish("leaderBoardFinished", value);
    }
    
    void CheckSeasonRankAndEnter(Action onClickRankResetLayer)
    {
        int lastSeasonPoint = PlayerPrefs.GetInt(PlayFabSetting._arenaPointCode, -1);
        if (lastSeasonPoint > PlayerAccountInfo.Me.arenaPoint)
        {
            var arenaNewSeason = UILayerLoader.Load<ArenaNewSeason>();
            arenaNewSeason.Setup(
                Mathf.Clamp(lastSeasonPoint,0, Int32.MaxValue), 
                Mathf.Clamp(PlayerAccountInfo.Me.arenaPoint, 0, Int32.MaxValue), 
                () =>
                {
                    UILayerLoader.Remove<ArenaNewSeason>();
                    onClickRankResetLayer.Invoke();
                }
            );
            PlayerPrefs.SetInt(PlayFabSetting._arenaPointCode, PlayerAccountInfo.Me.arenaPoint);
        }
        else
        {
            onClickRankResetLayer.Invoke();
        }
        PlayerPrefs.SetInt(PlayFabSetting._arenaPointCode, PlayerAccountInfo.Me.arenaPoint);
    }
    
    void EnterProcess()
    {
        BackGroundPS.target.Void();
        missionWatcher = new MissionWatcher(
            new List<string>
            {
                "itemsLoadFinished", "leaderBoardFinished"
            },
            () =>
            {
                SetLoaded(true);
                arenaLayer = UILayerLoader.Load<ArenaLayer>();
                arenaLayer.SetUp(
                    LoadLeaderboardInfos,
                    () =>
                    {
                        PreScene.target.trySwitchToStep(MainSceneStep.Ranking);
                    },
                    () =>
                    {
                        PreScene.target.trySwitchToStep(MainSceneStep.ArenaAward);
                    },
                    UpdateOneWord,
                    PrepareForIt
                );
                arenaLayer.SetupArenaTicket();
                arenaLayer.SetSeasonCountDown(timeUntilSettlement);
                arenaLayer.ShowMyTeamByLeaderInfo(_myLeaderboardInfo);
                arenaLayer.DisplayOpponents(opponents, _myLeaderboardInfo);
                ReturnLayer.MoveBack();
            },
            ()=>
            {
                SetLoaded(true);
            });
        
        LoadLeaderboardInfos();
        PlayFabReadClient.LoadItems(ItemsLoadFinished);
    }
    
    // Checks if there is anything entered into the input field.
    async void UpdateOneWord(InputField input)
    {
        if (String.IsNullOrEmpty(input.text))
        {
            return;
        }
        
        BWFManager.Instance.Load();
        await UniTask.WaitUntil(()=> BWFManager.Instance.isReady);
        var filteredWord = BadWordFilter(input.text);
        if (filteredWord.Contains("*"))
        {
            PopupLayer.ArrangeWarnWindow(Translate.Get("illegalword"));
            input.text = _myLeaderboardInfo != null ? _myLeaderboardInfo.OneWord : string.Empty;
        }
        else
        {
            PlayFabReadClient.UpdateUserData(
                new UpdateUserDataRequest()
                {
                    Data = new Dictionary<string, string>()
                    {
                        { "OneWord", input.text }
                    }
                },
                () =>
                {
                }
            );
        }
    }
    
    string BadWordFilter(string currentTxt)
    {
        currentTxt = BWFManager.Instance.ReplaceAll(currentTxt);
        return currentTxt;
    }
    
    void PrepareForIt(FightInfo stage)
    {
        if (Currencies.ArenaTicket.Value > 0)
        {
            PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, stage, true);
        }
        else
        {
            PopupLayer.ArrangeWarnWindow(Translate.Get("NoEnoughArenaTicket"));
        }
    }
    
    public override void ProcessEnter()
    {
        if (PlayerAccountInfo.Me.TitleDisplayName == null)
        {
            SettingPage.SetNickName((_) => Enter(), false);
            SetLoaded(true);
        }
        else
        {
            Enter();
        }

        void Enter()
        {
            var settlementDay = DayOfWeek.Sunday;
            var settlementTime = new TimeSpan(15, 0, 0); // 设置竞技场结算时间为UTC时间每周日的 15:00:00，即日本时间每周日晚上12点
        
            //DayOfWeek settlementDay = DayOfWeek.Monday;
            //TimeSpan settlementTime = new TimeSpan(7, 10, 0); // 设置竞技场结算时间为UTC时间每周日的 15:00:00，即日本时间每周日晚上12点
            GetServerTime(settlementDay, settlementTime);
        }
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<ArenaNewSeason>();
        UILayerLoader.Remove<ArenaLayer>();
        UILayerLoader.Remove<NickNameLayer>();
    }
}