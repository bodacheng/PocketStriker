using System.Collections.Generic;
using System;

public class MissionWatcher
{
    private readonly IDictionary<string, bool> _missionDic;
    private readonly Action _success, _fail;
    
    public MissionWatcher(List<string> missions, Action success = null, Action fail = null)
    {
        _missionDic = new Dictionary<string, bool>();
        foreach (var missionCode in missions)
        {
            _missionDic.Add(missionCode, false);
        }
        _success = success;
        _fail = fail;
    }
    
    public void Finish(string missionCode, bool value)
    {
        _missionDic[missionCode] = value;
        if (!value)
        {
            // 主动报告一个通信错误的时候才直接执行错误处理,
            // 如果_success过一次，在原MissionWatcher上以value = false 运行Finish的话
            // 依然会触发_fail
            _fail?.Invoke();
            return;
        }
        
        foreach (var kv in _missionDic)
        {
            if (!kv.Value)
            {
                return;
            }
        }
        _success?.Invoke();
    }
}
