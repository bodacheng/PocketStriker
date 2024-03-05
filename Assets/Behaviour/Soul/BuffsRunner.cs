using System.Collections.Generic;
using UnityEngine;

public class BuffsRunner
{
    #region 自定义携程

    public readonly List<CustomCoroutine> MySubMissions = new List<CustomCoroutine>();
    private readonly List<CustomCoroutine> _endedCustomCoroutines = new List<CustomCoroutine>();
    
    public bool Freezing { get; set; } = false;
    #endregion
    
    public void RunSubCoroutineOfState(CustomCoroutine coroutine)
    {
        coroutine.CustomCoroutineTrigger();
        MySubMissions.Add(coroutine);
    }

    public void EndSubCoroutineOfState(CustomCoroutine coroutine)
    {
        if (coroutine.IfProcessing())
            coroutine.EndCustomCoroutine();
        if (MySubMissions.Contains(coroutine))
        {
            MySubMissions.Remove(coroutine);
        }
    }
    
    public void EndAllCoroutines()
    {
        foreach (CustomCoroutine customCoroutine in MySubMissions)
        {
            customCoroutine.EndCustomCoroutine();
        }
        MySubMissions.Clear();
    }
    
    // Update is called once per frame
    public void BuffsRunnerFixedUpdate()
    {
        if (MySubMissions.Count > 0)
        {
            _endedCustomCoroutines.Clear();
            foreach (CustomCoroutine customCoroutine in MySubMissions)
            {
                customCoroutine.CustomCoroutineProcess();
                if (!customCoroutine.IfProcessing())
                {
                    _endedCustomCoroutines.Add(customCoroutine);
                }
            }
            for (int i = 0; i < _endedCustomCoroutines.Count; i++)
            {
                MySubMissions.Remove(_endedCustomCoroutines[i]);
            }
        }
    }
}

public delegate bool EndConditionDelegate();

public class CustomCoroutine
{
    bool _processing;
    readonly UnityEngine.Events.UnityAction _startAction;
    readonly UnityEngine.Events.UnityAction _endAction;
    readonly EndConditionDelegate _endCondition;
    readonly float _processTime;
    float _timeCounter;
    
    public CustomCoroutine(UnityEngine.Events.UnityAction startAction, float processTime, UnityEngine.Events.UnityAction endAction)
    {
        this._startAction = startAction;
        this._processTime = processTime;
        this._endAction = endAction;
        _endCondition = TimeOver;
        _processing = false;
        _timeCounter = 0;
    }
    
    public CustomCoroutine(UnityEngine.Events.UnityAction startAction, float processTime, EndConditionDelegate c, UnityEngine.Events.UnityAction endAction)
    {
        this._startAction = startAction;
        this._processTime = processTime;
        this._endAction = endAction;
        _endCondition = TimeOver;
        _endCondition = EndConditionCombine(_endCondition, c);
        _processing = false;
        _timeCounter = 0;
    }
    
    EndConditionDelegate EndConditionCombine(EndConditionDelegate a, EndConditionDelegate b)
    {
        bool c()
        {
            return a() || b();
        }
        return c;
    }
    
    bool TimeOver()
    {
        return _timeCounter >= _processTime;
    }
    
    public void CustomCoroutineTrigger()
    {
        _processing = true;
        _timeCounter = 0;
        _startAction.Invoke();
    }
    
    public void EndCustomCoroutine()
    {
        _endAction.Invoke();
        _processing = false;
    }
    
    public void CustomCoroutineProcess()
    {
        if (_processing && !_endCondition())
        {
            _timeCounter += Time.fixedDeltaTime;
        }
        if (_endCondition())
        {
            _processing = false;
            _endAction.Invoke();
        }
    }
    
    public bool IfProcessing()
    {
        return _processing;
    }
}