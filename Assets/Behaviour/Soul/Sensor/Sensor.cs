using System.Collections.Generic;
using UnityEngine;

public partial class Sensor
{
    LayerMask _meAndEnemyLayerMask;
    TeamConfig _teamConfig = TeamConfig.DefaultSet;
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
        _meAndEnemyLayerMask = teamConfig.myTeamAndMyEnemy;
        _selfDataCenter = self;
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
    
    public void SensorDetectionResultClearProcess()
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
    
    public void SensorDetectionResultSortProcess(Collider[] hits) //这个函数的调用必须要确保每次都在update函数之后
    {
        float sensorRadiusSqr = SensorRadius * SensorRadius;  // 预计算半径的平方
        foreach (Collider hit in hits)
        {
            if (hit == null || (hit.transform.position - Center.position).sqrMagnitude > sensorRadiusSqr)
            {
                continue;
            }
            
            if (_teamConfig.enemyLayerMask == (_teamConfig.enemyLayerMask | (1 << hit.gameObject.layer)) || _teamConfig.enemyShieldLayerMask == (_teamConfig.enemyShieldLayerMask | (1 << hit.gameObject.layer)))
            {
                _detectedEnemies.Add(hit);
            }
            if (_teamConfig.enemyWeaponLayerMask == (_teamConfig.enemyWeaponLayerMask | (1 << hit.gameObject.layer)))
            {
                _damagingWeaponAround.Add(hit);
            }
        }
        _nearestEnemyCollider = FindNearestCollider(_detectedEnemies);
        _nearestDamagingWeapon = FindNearestCollider(_damagingWeaponAround);
    }
    
    float _p1ToMe, _p2ToMe;
    int HorizontalDistanceCompare(Vector3 p1, Vector3 p2)
    {
        Vector3 center = Center.position;  // 只获取一次中心位置，提高效率
        // 计算平方距离，避免使用开方
        float p1ToCenterSqr = (p1.x - center.x) * (p1.x - center.x) + (p1.z - center.z) * (p1.z - center.z);
        float p2ToCenterSqr = (p2.x - center.x) * (p2.x - center.x) + (p2.z - center.z) * (p2.z - center.z);

        // 简化比较逻辑，直接返回结果
        if (p1ToCenterSqr > p2ToCenterSqr) return 1;
        if (p1ToCenterSqr < p2ToCenterSqr) return -1;
        return 0;
    }
    
    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.white;
    //    Gizmos.DrawWireSphere(Center.position, sensor_radius);
    //    //Gizmos.DrawRay(transform.position,selfDataCenter.WholeT.forward * sensor_radius);
    //}
}