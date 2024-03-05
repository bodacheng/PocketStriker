using UnityEngine;

public partial class Sensor
{
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
    
    void SensorDetectProcess()
    {
         Physics.OverlapSphereNonAlloc(Center.position, SensorRadius, _hits, _layers);// 这个东西消耗太大，起码可以考虑减少运行次数 // FIXUPDATE
         Physics.SphereCastNonAlloc(Center.position, 1f, _selfDataCenter.WholeT.forward, _spherecastHits,SensorRadius, _meAndEnemyLayerMask, QueryTriggerInteraction.Collide);
    }
}
