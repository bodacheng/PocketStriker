using System;
using System.Collections.Generic;
using UnityEngine;

public class SensorUnity : MonoBehaviour
{
    [SerializeField] LayerMask _layers;

    private Collider[] _hits;//What was hit in this frame?
    private Vector3 _centerPos;
    private float _sensorRadius;
    
    int _detectionInterval = -1; // -1 会保持检测器停止
    int _detectionResultKeepFrames;
    bool _continuousDetection;
    
    public readonly List<Action> SensorDetectionResultClearProcesses = new List<Action>();
    public readonly List<Action<Collider[]>> SensorDetectionResultSortProcesses = new List<Action<Collider[]>>();
    
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
                    one.Invoke(_hits);
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
    
    public void Setup(float radius, Vector3 center, int detectColliderCount)
    {
        _sensorRadius = radius;
        _centerPos = center;
        _hits = new Collider[detectColliderCount] ; //What was hit in this frame?
    }
    
    void SensorDetectProcess()
    {
        Physics.OverlapSphereNonAlloc(_centerPos, _sensorRadius, _hits, _layers);// 这个东西消耗太大，起码可以考虑减少运行次数 // FIXUPDATE
    }
}
