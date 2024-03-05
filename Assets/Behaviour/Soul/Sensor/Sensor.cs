using System.Collections.Generic;
using UnityEngine;

public partial class Sensor
{
    LayerMask _layers;
    LayerMask _meAndEnemyLayerMask;
    readonly Collider[] _hits = new Collider[30] ; //What was hit in this frame?
    readonly RaycastHit[] _spherecastHits= new RaycastHit[30] ;
    TeamConfig _teamConfig = TeamConfig.DefaultSet;
    
    int _detectionInterval = -1; // -1 会保持检测器停止
    int _detectionResultKeepFrames;
    bool _continuousDetection;

    int DetectionInterval
    {
        get => _detectionInterval;
        set
        {
            _detectionInterval = value;
            SensorDetectionResultClearProcess();
            if (_detectionInterval != -1)
            {
                SensorDetectProcess();//检测
                SensorDetectionResultSortProcess();//整理
                SphereCastSortProcess();
            }
        }
    }
    
    static readonly IDictionary<Team, List<Data_Center>> SharedUnitDic = new Dictionary<Team, List<Data_Center>>();
    static readonly IDictionary<Team, List<Data_Center>> SharedDeadUnitDic = new Dictionary<Team, List<Data_Center>>();
    readonly List<Collider> _detectedEnemies = new List<Collider>();
    Collider _nearestEnemyCollider;
    readonly List<Collider> _damagingWeaponAround = new List<Collider>();
    Collider _nearestDamagingWeapon;    
    Data_Center _selfDataCenter;
    Collider _jiaMateAmMate, _nearestEnemy;
    public float SensorRadius
    {
        get;
        set;
    }
    
    public Transform Center
    {
        get;
        set;
    }

    public void SetDetectLayer(TeamConfig teamConfig, Data_Center self)
    {
        _teamConfig = teamConfig;
        _layers = teamConfig.mySensorAndWeaponTargetLayerMask;
        _meAndEnemyLayerMask = teamConfig.myTeamAndMyEnemy;
        _selfDataCenter = self;
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
    
    public static void ClearFightingMember()
    {
        SharedUnitDic.Clear();
        SharedDeadUnitDic.Clear();
    }
    
    public static void AddOrRemoveSharedUnitInfo(Data_Center member, Team team, bool add) // add:true remove: false
    {
        if (!SharedUnitDic.ContainsKey(team))
            SharedUnitDic.Add(team, new List<Data_Center>());
        var fightingUnits = SharedUnitDic[team];
        if (add)
        {
            if (!fightingUnits.Contains(member))
            {
                fightingUnits.Add(member);
            }
        }
        else
        {
            if (fightingUnits.Contains(member))
            {
                fightingUnits.Remove(member);
            }
        }
        SharedUnitDic[team] = fightingUnits;
    }
    
    public static void AddOrRemoveSharedDeadUnitInfo(Data_Center member, Team team, bool add) // add:true remove: false
    {
        if (!SharedDeadUnitDic.ContainsKey(team))
            SharedDeadUnitDic.Add(team, new List<Data_Center>());
        var fightingUnits = SharedDeadUnitDic[team];
        if (add)
        {
            if (!fightingUnits.Contains(member))
            {
                fightingUnits.Add(member);
            }
        }
        else
        {
            if (fightingUnits.Contains(member))
            {
                fightingUnits.Remove(member);
            }
        }
        SharedDeadUnitDic[team] = fightingUnits;
    }
    
    public void Stop()
    {
        DetectionInterval = -1;
        _continuousDetection = false;
    }
    
    void SensorDetectionResultClearProcess()
    {
        _detectedEnemies.Clear();
        _damagingWeaponAround.Clear();
    }
    
    List<GameObject> FindTargetsByDistance(Team[] tags, IDictionary<Team, List<Data_Center>> targetDic)
    {
        var targetList = new List<GameObject>();
        if (tags != null)
        {
            for (var i = 0; i < tags.Length; i++)
            {
                if (targetDic != null)
                {
                    for (var y = 0; y < tags.Length; y++)
                    {
                        targetDic.TryGetValue(tags[y], out var searchingMembers);
                        if (searchingMembers != null)
                        {
                            for (var k = 0; k < searchingMembers.Count; k++)
                            {
                                if (searchingMembers[k] != null)
                                    targetList.Add(searchingMembers[k].WholeT.gameObject);
                                else
                                    Debug.Log("检测逻辑错误");
                            }
                        }
                    }
                }
            }
            if (targetList.Count > 1)
            {
                targetList.Sort((a, b) => HorizontalDistanceCompare(a.transform.position, b.transform.position));
                return targetList;
            }
            else
            {
                return targetList;
            }
        }
        return targetList;
    }

    void SphereCastSortProcess()
    {
        float mateToMe = SensorRadius, enemyToMe = SensorRadius;
        foreach (var raycastHit in _spherecastHits)
        {
            if (raycastHit.collider != null)
            {
                if (_teamConfig.myTeamLayerMask == (_teamConfig.myTeamLayerMask | (1 << raycastHit.collider.gameObject.layer)))
                {
                    if (!_selfDataCenter.FightDataRef.IfMyBody(raycastHit.collider))
                    {
                        var toMe = Vector3.Distance(Center.position, raycastHit.collider.transform.position);
                        if (toMe < mateToMe)
                        {
                            _jiaMateAmMate = raycastHit.collider;
                            mateToMe = toMe;
                        }
                    }
                }
                if (_teamConfig.enemyLayerMask == (_teamConfig.enemyLayerMask | (1 << raycastHit.collider.gameObject.layer)))
                {
                    var toMe = Vector3.Distance(Center.position, raycastHit.collider.transform.position);
                    if (toMe < enemyToMe)
                    {
                        _nearestEnemy = raycastHit.collider;
                        enemyToMe = toMe;
                    }
                }
            }
        }

        if (_jiaMateAmMate != null && _nearestEnemy != null)
        {
            if (Vector3.Distance(Center.position, _jiaMateAmMate.transform.position) < Vector3.Distance(Center.position, _nearestEnemy.transform.position))
                return;//意思就是说让jiamateammate和nearestenemy不为空
        }
        _jiaMateAmMate = null;
        _nearestEnemy = null;
    }

    void SensorDetectionResultSortProcess() //这个函数的调用必须要确保每次都在update函数之后
    {
        foreach (Collider hit in _hits)
        {
            if (hit != null)
            {
                if (_teamConfig.enemyLayerMask == (_teamConfig.enemyLayerMask | (1 << hit.gameObject.layer)) || _teamConfig.enemyShieldLayerMask == (_teamConfig.enemyShieldLayerMask | (1 << hit.gameObject.layer)))
                {
                    _detectedEnemies.Add(hit);
                }
                if (_teamConfig.enemyWeaponLayerMask == (_teamConfig.enemyWeaponLayerMask | (1 << hit.gameObject.layer)))
                {
                    _damagingWeaponAround.Add(hit);
                }
            }
        }
        _nearestEnemyCollider = FindNearestCollider(_detectedEnemies);
        _nearestDamagingWeapon = FindNearestCollider(_damagingWeaponAround);
    }

    float _p1ToMe, _p2ToMe;
    int HorizontalDistanceCompare(Vector3 p1, Vector3 p2)
    {
        var position = Center.position;
        p1.y = position.y;
        _p1ToMe = (p1 - position).sqrMagnitude;
        p2.y = position.y;
        _p2ToMe = (p2 - position).sqrMagnitude;
        
        return _p1ToMe > _p2ToMe ? 1 : _p1ToMe < _p2ToMe ? -1 : 0;
    }
    
    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.white;
    //    Gizmos.DrawWireSphere(Center.position, sensor_radius);
    //    //Gizmos.DrawRay(transform.position,selfDataCenter.WholeT.forward * sensor_radius);
    //}
}

//public List<Collider> getInnerRangeWallColliders() //这个函数的调用必须要确保每次都在update函数之后
//{
//    wallTs.Clear();
//    if (_hits == null)
//    {
//        return wallTs;
//    }
//    foreach (Collider hit in this._hits)
//    {
//        if (hit != null)
//        {
//            if (hit.gameObject.layer == 13)
//            {

//                //_ClosestPointOnBounds = hit.ClosestPointOnBounds(transform.position);
//                if ((hit.transform.position - transform.position).magnitude < innerSensorRadius)
//                {
//                    wallTs.Add(hit);
//                    break;
//                }

//            }
//        }
//    }
//    return wallTs;
//}

//public List<Collider> getMyTeammatesNearby()
//{
//    return teammatesC;
//}

//public void MyteamDetectionResultSortProcess()
//{
//    teammatesC = teamMatesHIts.ToList();
//    if (teammatesC.Count > 1)
//    {
//        //OutterDamagingWeapon.Sort((a, b) => horizontalDistanceCompare(a.transform.position, b.transform.position));
//        tempCForNearest = FindNearestCollider(teammatesC);
//        if (tempCForNearest != null)
//        {
//            teammatesC.Remove(tempCForNearest);
//            teammatesC.Insert(0, tempCForNearest);
//        }
//    }
//}

// What kinds of info we need from all the hits we get ?
// 1.Other characters
// 2.Weapons on damaging mode
// 3.Working shield

// 在这个函数中我们使用了大量getComponent函数，但实际上在新的分层机制下，这些东西可以回避掉。
// 我们整个AI系统，策略上的一些判定靠的是DATAcente里那些，而这个地方的判定更多的来说是针对敌人近身情况下的一些应急性动作。
// 也就是说，其实对getNearbyEnemyHealthBody这个函数的利用基本只局限于和近身敌人的距离判定一类。。。
// 既然层的目的本来就是针对打击判定系统自身，那如果角色层上的collider的确不用来做伤害hitbox，何不直接在这种情况下把角色给设置成other层？
//List<Collider> FocousingNearbyEnemyColliders;
//public List<Collider> getNearbyEnemyColliders()
//{
//    FocousingNearbyEnemyColliders = new List<Collider>();
//    foreach (Collider hit in this._hits)
//    {
//        if (hit != null)
//        {
//            if (enemyMeatLayers == (enemyMeatLayers | (1 << hit.gameObject.layer)))
//            {
//                FocousingNearbyEnemyColliders.Add(hit);
//            }
//        }
//    }
//    return FocousingNearbyEnemyColliders;
//}