using System;
using System.Collections.Generic;

public class MissionWatcher
{
    private readonly IDictionary<string, bool> _missionDic;
    private readonly Action _success, _fail;
    private int _unfinishedCount;
    private bool _successInvoked;
    private bool _failInvoked;
    
    public MissionWatcher(IEnumerable<string> missions, Action success = null, Action fail = null)
    {
        _missionDic = new Dictionary<string, bool>();
        if (missions != null)
        {
            foreach (var missionCode in missions)
            {
                if (string.IsNullOrEmpty(missionCode) || _missionDic.ContainsKey(missionCode))
                {
                    continue;
                }

                _missionDic.Add(missionCode, false);
                _unfinishedCount++;
            }
        }

        _success = success;
        _fail = fail;
    }
    
    public void Finish(string missionCode, bool value)
    {
        if (_successInvoked || _failInvoked)
        {
            return;
        }

        var hadMission = _missionDic.TryGetValue(missionCode, out var previousValue);
        _missionDic[missionCode] = value;
        if (!value)
        {
            if (!hadMission || previousValue)
            {
                _unfinishedCount++;
            }

            // 主动报告通信错误时直接执行错误处理；成功或失败后迟到的回调会被忽略。
            _failInvoked = true;
            _fail?.Invoke();
            return;
        }

        if (hadMission && !previousValue)
        {
            _unfinishedCount--;
        }

        if (_unfinishedCount > 0 || _successInvoked)
        {
            return;
        }

        _successInvoked = true;
        _success?.Invoke();
    }
}
