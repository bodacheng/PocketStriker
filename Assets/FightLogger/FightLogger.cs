using System;
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
    private readonly Dictionary<Team, string> IdDicRef = new Dictionary<Team, string>();
    readonly IDictionary<Data_Center, IDisposable> deathWatchers = new Dictionary<Data_Center, IDisposable>(); 
    
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
        GameOver.Value = false;
        deathWatchers.Clear();
    }
    
    public void ReadyToLog(IDictionary<TeamConfig, List<Data_Center>> teamMembers)
    {
        wholeTeamCount = 0;
        TeamDeadMemberDic.Clear();
        deadTeam.Clear();
        GameOver.Value = false;
        winnerTeam = Team.none;
        IdDicRef.Clear();
        
        foreach (var pair in teamMembers)
        {
            IdDicRef.Add(pair.Key.myTeam, pair.Key.playID);
            TeamDeadMemberDic.Add(pair.Key.myTeam, new List<Data_Center>());
            wholeTeamCount += 1;
            foreach (var dataCenter in pair.Value)
            {
                dataCenter.FightDataRef.IsDead.Dispose();
                dataCenter.FightDataRef.IsDead = new ReactiveProperty<bool>();

                void WatchDeath()
                {
                    TeamDeadMemberDic[pair.Key.myTeam].Add(dataCenter);
                    if (pair.Value.Count == TeamDeadMemberDic[pair.Key.myTeam].Count)
                    {
                        if (!deadTeam.Contains(pair.Key.myTeam))
                            deadTeam.Add(pair.Key.myTeam);
                    }
                    if (wholeTeamCount == deadTeam.Count + 1) // 胜负已决
                    {
                        GameOver.Value = true;
                        var teams = teamMembers.Keys.ToList().Select(x => x.myTeam).ToList();
                        var _winner = teams.Except(deadTeam).ToList();
                        winnerTeam = _winner[0];
                    }
                }
                
                IDisposable disposable = dataCenter.FightDataRef.IsDead.Subscribe(x =>
                {
                    if (x)
                    {
                        WatchDeath();
                        deathWatchers[dataCenter].Dispose();
                    }
                });
                deathWatchers.Add(dataCenter, disposable);
            }
        }
    }
}