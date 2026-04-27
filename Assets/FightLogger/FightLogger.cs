using System;
using UniRx;
using System.Collections.Generic;
using MCombat.Shared.Combat;

// 用于在每一局游戏里起记录数据的作用，包括胜利判断，都应该是由本模块来执行。
public class FightLogger
{
    public static readonly FightLogger value = new FightLogger();

    public ReactiveProperty<bool> GameOver { get; set; } = new ReactiveProperty<bool>(false);
    readonly TeamEliminationTracker<Data_Center> _eliminationTracker = new TeamEliminationTracker<Data_Center>();
    readonly IDictionary<Data_Center, IDisposable> deathWatchers = new Dictionary<Data_Center, IDisposable>();
    
    public string GetWinnerId()
    {
        return _eliminationTracker.GetWinnerId();
    }
    
    public Team GetWinnerTeam()
    {
        return _eliminationTracker.WinnerTeam;
    }
    
    public void WatchMissionsAbandon()
    {
        _eliminationTracker.Clear();
        GameOver.Value = false;
        DisposeDeathWatchers();
    }
    
    public void ReadyToLog(IDictionary<TeamConfig, List<Data_Center>> teamMembers)
    {
        DisposeDeathWatchers();
        _eliminationTracker.Clear();
        GameOver.Value = false;
        
        foreach (var pair in teamMembers)
        {
            _eliminationTracker.RegisterTeam(pair.Key.myTeam, pair.Key.playID, pair.Value.Count);
            foreach (var dataCenter in pair.Value)
            {
                dataCenter.FightDataRef.IsDead.Dispose();
                dataCenter.FightDataRef.IsDead = new ReactiveProperty<bool>();

                void WatchDeath()
                {
                    _eliminationTracker.MarkDead(pair.Key.myTeam, dataCenter);
                    if (_eliminationTracker.IsGameOver) // 胜负已决
                    {
                        GameOver.Value = true;
                    }
                }
                
                IDisposable disposable = dataCenter.FightDataRef.IsDead.Subscribe(x =>
                {
                    if (x)
                    {
                        WatchDeath();
                        if (deathWatchers.TryGetValue(dataCenter, out var watcher))
                        {
                            watcher.Dispose();
                            deathWatchers.Remove(dataCenter);
                        }
                    }
                });
                deathWatchers.Add(dataCenter, disposable);
            }
        }
    }

    void DisposeDeathWatchers()
    {
        foreach (var watcher in deathWatchers.Values)
        {
            watcher?.Dispose();
        }

        deathWatchers.Clear();
    }
}
