using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using dataAccess;
using mainMenu;
using UniRx;
using UnityEngine.UI;

public class ArenaLayer : UILayer
{
    [SerializeField] Text ticketCount;
    [SerializeField] RectTransform ticketChargeCountDownT;
    [SerializeField] Text ticketChargeCountDown;
    [SerializeField] Text seasonCountDown;
    
    #region 玩家队伍
    [SerializeField] HeroIcon member1, member2, member3;
    [SerializeField] Button editMyTeamBtn;
    [SerializeField] Text nickName;
    [SerializeField] Text myScore;
    [SerializeField] Text myRank; // playfab 提供的实际排名
    [SerializeField] InputField oneWord;
    [SerializeField] GameObject plsEditTeamIndicator;
    [SerializeField] ArenaRankIcon arenaRankIcon;
    #endregion
    
    [SerializeField] Button refreshBtn;
    [SerializeField] RectTransform enemiesT;
    [SerializeField] ArenaFightTeamDisplay arenaFightTeamDisplayPrefab;
    
    [SerializeField] Button rankingPageBtn;
    [SerializeField] Button rewardBtn;
    
    private Action<FightInfo> tryBeginStage;
    private int maxOpponentCount = 3;
    
    public void SetUp(Action loadData, Action openRanking, Action openAwardPage,
        Action<InputField> updateOneWord, Action<FightInfo> tryBeginStage)
    {
        refreshBtn.onClick.RemoveAllListeners();
        refreshBtn.onClick.AddListener(()=>
        {
            loadData();
        });
        
        rankingPageBtn.onClick.AddListener(()=>openRanking());
        arenaRankIcon.Set(PlayerAccountInfo.Me.arenaPoint);
        this.tryBeginStage = tryBeginStage;
        
        rewardBtn.onClick.AddListener(()=>openAwardPage());
        
        void GoToTeamEdit()
        {
            PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, "arena", true);
        }
        editMyTeamBtn.onClick.RemoveAllListeners();
        editMyTeamBtn.onClick.AddListener(GoToTeamEdit);
        
        oneWord.onSubmit.AddListener(delegate{updateOneWord(oneWord);});
    }
    
    private IDisposable _disposeSeasonCountDown;
    public void SetSeasonCountDown(TimeSpan timeUntilSettlement)
    {
        _disposeSeasonCountDown = 
            Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(
            (_) =>
            {
                timeUntilSettlement = timeUntilSettlement.Subtract(TimeSpan.FromSeconds(1));
                seasonCountDown.text = timeUntilSettlement.ToString(@"dd\:hh\:mm\:ss");
                if (timeUntilSettlement.TotalSeconds <= 0)
                {
                    _disposeSeasonCountDown.Dispose();
                    PreScene.target.ReEnterCurrent();
                }
            }).AddTo(gameObject);
    }
    
    private IDisposable _disposeCountDown;
    public void SetupArenaTicket()
    {
        Currencies.ArenaTicket.Subscribe(x=>
        {
            _disposeCountDown?.Dispose();
            ticketCount.text = x.ToString();
            if (x >= Currencies.ArenaTicketRechargeMax)
            {
                ticketChargeCountDown.text = string.Empty;
                ticketChargeCountDownT.gameObject.SetActive(false);
            }
            else
            {
                _disposeCountDown = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe((_) =>
                {
                    ticketChargeCountDownT.gameObject.SetActive(true);
                    if (Currencies.SecondsToRechargeArenaTicket > 0)
                    {
                        var minute = Currencies.SecondsToRechargeArenaTicket / 60;
                        var seconds = Currencies.SecondsToRechargeArenaTicket - minute * 60;
                        ticketChargeCountDown.text = $"{minute :00}:{seconds:00}";
                        Currencies.SecondsToRechargeArenaTicket -= 1;
                    }
                    else
                    {
                        Currencies.ArenaTicket.Value += 1;
                        Currencies.SecondsToRechargeArenaTicket = 60 * 60;
                    }
                }).AddTo(gameObject);
            }
        }).AddTo(gameObject);
    }
    
    // 挑战玩家队伍机能加载（目前规定显示在画面上的挑战组一共四个。远程获取不到的情况下就本地生成）
    public void DisplayOpponents(List<LeaderboardInfo> leaderboards, LeaderboardInfo myInfo)
    {
        foreach (Transform c in enemiesT)
        {
            Destroy(c.gameObject);
        }
        if (leaderboards == null)
            return;
        var ordered = leaderboards.OrderBy(x=> x.PlayerLeaderboardEntry.Position).ToList();
        for (var index = 0; index < Mathf.Min(ordered.Count, maxOpponentCount); index++)
        {
            var leaderInfo = ordered[index];
            var o = Instantiate(arenaFightTeamDisplayPrefab);
            o.AddFightToList(leaderInfo, myInfo, tryBeginStage);
            o.transform.SetParent(enemiesT);
            o.transform.localPosition = Vector3.zero;
            o.transform.localScale = Vector3.one;
            o.gameObject.SetActive(true);
        }
    }
    
    public void ShowMyTeamByLeaderInfo(LeaderboardInfo info)
    {
        if (info == null)
            return;
        
        nickName.text = info.PlayerLeaderboardEntry.DisplayName;
        myScore.text = Translate.Get("RankingScore") +" : " + info.PlayerLeaderboardEntry.StatValue;
        myRank.text = info.PlayerLeaderboardEntry.Position.ToString();
        
        rankingPageBtn.gameObject.SetActive(info != null);
        refreshBtn.gameObject.SetActive(info != null);
        arenaRankIcon.gameObject.SetActive(info != null);
        rewardBtn.gameObject.SetActive(info != null);
        oneWord.text = info != null ? info.OneWord : String.Empty;
        
        var posKeySet = new PosKeySet();
        if (info != null)
        {
            for (var index = 0; index < info.Team.Length; index++)
            {
                var posNum = info.Team[index].key2;
                var unitInfo = info.Team[index].value;
                HeroIcon target = null;
                switch (posNum)
                {
                    case 0:
                        target = member1;
                        break;
                    case 1:
                        target = member2;
                        break;
                    case 2:
                        target = member3;
                        break;
                }
                posKeySet.SetPosMemInfoByInstanceID(posNum, unitInfo.id);
                target.ChangeIcon(unitInfo);
            }
        }
        var teamDic = posKeySet.LoadTeamDic();
        plsEditTeamIndicator.SetActive(teamDic.mDict.Count != 3);
        TeamSet.Arena3V3 = posKeySet;
    }
}