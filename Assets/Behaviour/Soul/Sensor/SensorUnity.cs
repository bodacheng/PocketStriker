using System;
using System.Collections.Generic;
using UnityEngine;

public class SensorUnity : MonoBehaviour
{
    [SerializeField] LayerMask _layers;

    private Collider[] _hits;//What was hit in this frame?
    private int _hitCount;
    private Vector3 _centerPos;
    private float _sensorRadius;

    int _detectionInterval = -1; // -1 会保持检测器停止
    int _detectionResultKeepFrames;
    bool _continuousDetection;

    public readonly List<Action> SensorDetectionResultClearProcesses = new List<Action>();
    public readonly List<Action<Collider[], int>> SensorDetectionResultSortProcesses = new List<Action<Collider[], int>>();

    int DetectionInterval
    {
        get => _detectionInterval;
        set
        {
            _detectionInterval = value;
            foreach (var one in SensorDetectionResultClearProcesses)
            {
                one.Invoke();
            }

            if (_detectionInterval != -1)
            {
                SensorDetectProcess();//检测
                foreach (var one in SensorDetectionResultSortProcesses)
                {
                    one.Invoke(_hits, _hitCount);
                }
            }
        }
    }

    // continuousDetectionStart(0) 的情况下。
    // round 0: (一次检测) this.DetectionResultLastFrame == 0, DetectionInterval = 1
    // round 1: DetectionInterval = 0; (上次检测结果未被清空)
    // round 2: 一次检测，DetectionInterval++; (上次检测未被清空)
    // round 3: DetectionInterval == 1, DetectionInterval = 0,(上次检测未被清空)
    // round 4: 一次检测，DetectionInterval++; (上次检测未被清空)
    //。。。。循环
    // continuousDetectionStart(-1) 的情况下。
    // round 0:  (一次检测) this.DetectionResultLastFrame == -1, DetectionInterval = 1
    // round 1: DetectionInterval = 0; (上次检测结果未被清空)
    // round 2: 一次检测 由于0 > -1, DetectionInterval = 0,
    // round 3: 一次检测 由于0 > -1, DetectionInterval = 0,
    // ... 循环
    // 结论： continuousDetectionStart(0) 让检测器隔一帧检测一次，continuousDetectionStart(-1)(任何负)，让检测器每帧检测一次

    public void DetectionStart(int detectionResultKeepFrames, bool continuous)
    {
        DetectionInterval = 3;
        _continuousDetection = continuous;
        _detectionResultKeepFrames = detectionResultKeepFrames;
    }

    public void SensorFixedUpdate()
    {
        if (DetectionInterval != -1)
        {
            if (DetectionInterval > _detectionResultKeepFrames)
            {
                DetectionInterval = 0;
                if (!_continuousDetection)
                {
                    DetectionInterval = -1;
                }
                return;//否则下面的DetectionInterval++会导致其值立刻从0变到1，无法进入上面的if (DetectionInterval == 0)部分。
            }
            _detectionInterval++;
        }
    }

    public void Stop()
    {
        DetectionInterval = -1;
        _continuousDetection = false;
        SensorDetectionResultClearProcesses.Clear();
        SensorDetectionResultSortProcesses.Clear();
    }

    public void ForceImmediateDetection()
    {
        if (_hits == null)
        {
            return;
        }

        foreach (var clearProcess in SensorDetectionResultClearProcesses)
        {
            clearProcess?.Invoke();
        }

        SensorDetectProcess();

        foreach (var sortProcess in SensorDetectionResultSortProcesses)
        {
            sortProcess?.Invoke(_hits, _hitCount);
        }

        if (_detectionInterval != -1)
        {
            _detectionInterval = 0;
        }
    }

    public void SensorSetting(float battleRingRadius, int groupFightRemainUnitCount)
    {
        var detectColliderCount = 0;

        if (FightLoad.Fight.IsGroupBattle)
        {
            detectColliderCount = groupFightRemainUnitCount * 10;
        }
        else
        {
            if (FightLoad.Fight.FightMode == FightMode.Multi)
            {
                detectColliderCount = (FightLoad.Fight.FightMembers.HeroSets.Count +
                                       FightLoad.Fight.FightMembers.EnemySets.Count) * 10;
            }
            else
            {
                detectColliderCount = 15;
            }
        }

        Setup(battleRingRadius, Vector3.zero, detectColliderCount);
    }

    public void Setup(float radius, Vector3 center, int detectColliderCount)
    {
        _sensorRadius = radius;
        _centerPos = center;
        // Body parts, shields and weapons all occupy query slots. Keep the
        // high-water capacity when casualties arrive; shrinking can hide the
        // last enemy behind an arbitrary subset of friendly colliders.
        var capacity = Mathf.Max(16, detectColliderCount);
        if (_hits == null || _hits.Length < capacity)
            _hits = new Collider[capacity];
    }

    void SensorDetectProcess()
    {
        if (_hits == null) _hits = new Collider[16];
        while (true)
        {
            _hitCount = Physics.OverlapSphereNonAlloc(_centerPos, _sensorRadius, _hits, _layers);
            if (_hitCount < _hits.Length) return;
            // A full NonAlloc buffer does not say how many results were omitted.
            // Grow and repeat until every collider is available to each sensor.
            Array.Resize(ref _hits, checked(_hits.Length * 2));
        }
    }
}
