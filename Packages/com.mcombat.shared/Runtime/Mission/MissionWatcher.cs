using System;
using System.Collections.Generic;

public class MissionWatcher
{
    private readonly IDictionary<string, bool> _missionDic;
    private readonly Action _success, _fail;
    private int _unfinishedCount;
    private bool _successInvoked;
    
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
        var hadMission = _missionDic.TryGetValue(missionCode, out var previousValue);
        _missionDic[missionCode] = value;
        if (!value)
        {
            if (!hadMission || previousValue)
            {
                _unfinishedCount++;
            }

            // 主动报告一个通信错误的时候才直接执行错误处理,
            // 如果_success过一次，在原MissionWatcher上以value = false 运行Finish的话
            // 依然会触发_fail
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
