using System.Collections.Generic;
using MCombat.Shared.Combat;
using UnityEngine;

public partial class Sensor
{
    LayerMask _meAndEnemyLayerMask;
    TeamConfig _teamConfig = TeamConfig.DefaultSet;
    static readonly CombatUnitRegistry<Data_Center> SharedUnitRegistry = new CombatUnitRegistry<Data_Center>();
    static readonly CombatUnitRegistry<Data_Center> SharedDeadUnitRegistry = new CombatUnitRegistry<Data_Center>();
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
        SharedUnitRegistry.Clear();
        SharedDeadUnitRegistry.Clear();
    }

    public static void AddOrRemoveSharedUnitInfo(Data_Center member, Team team, bool add) // add:true remove: false
    {
        SharedUnitRegistry.AddOrRemove(member, team, add);
    }

    public static void AddOrRemoveSharedDeadUnitInfo(Data_Center member, Team team, bool add) // add:true remove: false
    {
        SharedDeadUnitRegistry.AddOrRemove(member, team, add);
    }

    public void SensorDetectionResultClearProcess()
    {
        _detectedEnemies.Clear();
        _damagingWeaponAround.Clear();
    }

    List<GameObject> FindTargetsByDistance(Team[] tags, CombatUnitRegistry<Data_Center> targetRegistry)
    {
        var targetList = new List<GameObject>();
        FindTargetsByDistance(tags, targetRegistry, targetList);
        return targetList;
    }

    void FindTargetsByDistance(Team[] tags, CombatUnitRegistry<Data_Center> targetRegistry, List<GameObject> targetList)
    {
        targetList.Clear();
        if (tags == null || targetRegistry == null)
        {
            return;
        }

        for (var i = 0; i < tags.Length; i++)
        {
            var searchingMembers = targetRegistry.GetUnits(tags[i]);
            if (searchingMembers == null)
            {
                continue;
            }

            for (var k = 0; k < searchingMembers.Count; k++)
            {
                var member = searchingMembers[k];
                if (member != null && member.WholeT != null)
                {
                    targetList.Add(member.WholeT.gameObject);
                }
                else
                {
                    Debug.Log("检测逻辑错误");
                }
            }
        }

        SortByHorizontalDistance(targetList);
    }

    void SortByHorizontalDistance(List<GameObject> targetList)
    {
        if (targetList.Count < 2 || Center == null)
        {
            return;
        }

        CombatSpatialUtility.SortByHorizontalDistance(
            targetList,
            Center.position,
            target => target != null ? target.transform.position : (Vector3?)null);
    }

    public void SensorDetectionResultSortProcess(Collider[] hits) //这个函数的调用必须要确保每次都在update函数之后
    {
        float sensorRadiusSqr = SensorRadius * SensorRadius;  // 预计算半径的平方
        Vector3 centerPosition = Center.position;
        foreach (Collider hit in hits)
        {
            if (hit == null || (hit.transform.position - centerPosition).sqrMagnitude > sensorRadiusSqr)
            {
                continue;
            }

            var hitLayer = hit.gameObject.layer;
            if (CombatLayerUtility.ContainsLayer(_teamConfig.enemyLayerMask, hitLayer) || CombatLayerUtility.ContainsLayer(_teamConfig.enemyShieldLayerMask, hitLayer))
            {
                _detectedEnemies.Add(hit);
            }
            if (CombatLayerUtility.ContainsLayer(_teamConfig.enemyWeaponLayerMask, hitLayer))
            {
                _damagingWeaponAround.Add(hit);
            }
        }
        _nearestEnemyCollider = FindNearestCollider(_detectedEnemies);
        _nearestDamagingWeapon = FindNearestCollider(_damagingWeaponAround);
    }

    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.white;
    //    Gizmos.DrawWireSphere(Center.position, sensor_radius);
    //    //Gizmos.DrawRay(transform.position,selfDataCenter.WholeT.forward * sensor_radius);
    //}
}
