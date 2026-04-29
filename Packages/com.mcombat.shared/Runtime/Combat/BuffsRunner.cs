using System.Collections.Generic;
using UnityEngine;

public class BuffsRunner
{
    public readonly List<CustomCoroutine> MySubMissions = new List<CustomCoroutine>();
    readonly List<CustomCoroutine> _endedCustomCoroutines = new List<CustomCoroutine>();

    public bool Freezing { get; set; }

    public void RunSubCoroutineOfState(CustomCoroutine coroutine)
    {
        coroutine.CustomCoroutineTrigger();
        MySubMissions.Add(coroutine);
    }

    public void EndSubCoroutineOfState(CustomCoroutine coroutine)
    {
        if (coroutine.IfProcessing())
        {
            coroutine.EndCustomCoroutine();
        }

        if (MySubMissions.Contains(coroutine))
        {
            MySubMissions.Remove(coroutine);
        }
    }

    public void EndAllCoroutines()
    {
        foreach (var customCoroutine in MySubMissions)
        {
            customCoroutine.EndCustomCoroutine();
        }

        MySubMissions.Clear();
    }

    public void BuffsRunnerFixedUpdate()
    {
        if (MySubMissions.Count <= 0)
        {
            return;
        }

        _endedCustomCoroutines.Clear();
        foreach (var customCoroutine in MySubMissions)
        {
            customCoroutine.CustomCoroutineProcess();
            if (!customCoroutine.IfProcessing())
            {
                _endedCustomCoroutines.Add(customCoroutine);
            }
        }

        for (var i = 0; i < _endedCustomCoroutines.Count; i++)
        {
            MySubMissions.Remove(_endedCustomCoroutines[i]);
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
        _startAction = startAction;
        _processTime = processTime;
        _endAction = endAction;
        _endCondition = TimeOver;
        _processing = false;
        _timeCounter = 0;
    }

    public CustomCoroutine(
        UnityEngine.Events.UnityAction startAction,
        float processTime,
        EndConditionDelegate c,
        UnityEngine.Events.UnityAction endAction)
    {
        _startAction = startAction;
        _processTime = processTime;
        _endAction = endAction;
        _endCondition = EndConditionCombine(TimeOver, c);
        _processing = false;
        _timeCounter = 0;
    }

    EndConditionDelegate EndConditionCombine(EndConditionDelegate a, EndConditionDelegate b)
    {
        bool Combined()
        {
            return a() || b();
        }

        return Combined;
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
