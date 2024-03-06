using UniRx;
using System.Collections.Generic;
using System.Linq;

// 用于在每一局游戏里起记录数据的作用，包括胜利判断，都应该是由本模块来执行。
public class FightLogger
{
    public static readonly FightLogger value = new FightLogger();

    public ReactiveProperty<bool> GameOver { get; set; } = new ReactiveProperty<bool>(false);
    Team winnerTeam = Team.none;
    int wholeTeamCount;
    
    readonly IDictionary<Team, List<Data_Center>> TeamDeadMemberDic = new Dictionary<Team, List<Data_Center>>();
    private readonly List<Team> deadTeam = new List<Team>();
    private readonly List<SingleAssignmentDisposable> WatchPlayers = new List<SingleAssignmentDisposable>();
    private readonly Dictionary<Team, string> IdDicRef = new Dictionary<Team, string>();
    
    public string GetWinnerId()
    {
        return IdDicRef[winnerTeam];
    }
    
    public Team GetWinnerTeam()
    {
        return winnerTeam;
    }
    
    public void WatchMissionsAbandon()
    {
        deadTeam.Clear();
        for (var i = 0; i < WatchPlayers.Count; i++)
        {
            if (!WatchPlayers[i].IsDisposed)
            {
                WatchPlayers[i].Dispose();
            }
        }
        WatchPlayers.Clear();
        GameOver.Value = false;
    }
    
    public void ReadyToLog(IDictionary<TeamConfig, List<Data_Center>> TeamMembers)
    {
        wholeTeamCount = 0;
        TeamDeadMemberDic.Clear();
        deadTeam.Clear();
        GameOver.Value = false;
        winnerTeam = Team.none;
        IdDicRef.Clear();
        
        foreach (var pair in TeamMembers)
        {
            IdDicRef.Add(pair.Key.myTeam, pair.Key.playID);
            
            TeamDeadMemberDic.Add(pair.Key.myTeam, new List<Data_Center>());
            wholeTeamCount += 1;
            foreach (var data_Center in pair.Value)
            {
                var disposable = new SingleAssignmentDisposable();
                disposable.Disposable = Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    if (data_Center.FightDataRef.IsDead.Value)
                    {
                        TeamDeadMemberDic[pair.Key.myTeam].Add(data_Center);
                        if (pair.Value.Count == TeamDeadMemberDic[pair.Key.myTeam].Count)
                        {
                            if (!deadTeam.Contains(pair.Key.myTeam))
                                deadTeam.Add(pair.Key.myTeam);
                        }
                        if (wholeTeamCount == deadTeam.Count + 1) // 胜负已决
                        {
                            GameOver.Value = true;
                            var teams = TeamMembers.Keys.ToList().Select(x => x.myTeam).ToList();
                            var _winner = teams.Except(deadTeam).ToList();
                            winnerTeam = _winner[0];
                        }
                        disposable.Dispose();
                    }
                });
                WatchPlayers.Add(disposable);
            }
        }
    }
}