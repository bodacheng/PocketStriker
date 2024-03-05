using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class Sensor
{
    public List<Collider> GetTargetRangeEnemyCollider(float min, float max)
    {
        var returnValue = new List<Collider>();
        for (var i = 0; i < _detectedEnemies.Count; i++)
        {
            var to_me = Vector3.Distance(Center.position, _detectedEnemies[i].transform.position);
            if (to_me >= min && to_me <= max)
            {
                returnValue.Add(_detectedEnemies[i]);
            }
        }
        return returnValue;
    }
    
    public Collider GetClosestEnemyColliderInSensorRange()
    {
        return _nearestEnemyCollider;
    }
    
    public Collider GetSuddenThreatInRange(float min,float max)
    {
        var threat = GetClosestEnemyHitBoxColliderInSensorRange();
        if (threat == null)
        {
            return null;
        }
        var toMe = Vector3.Distance(Center.position, threat.transform.position);
        if (toMe >= min && toMe <= max)
        {
            return threat;
        }
        return null;
    }
    
    Collider GetClosestEnemyHitBoxColliderInSensorRange()
    {
        if (_nearestDamagingWeapon != null)
        {
            var returnValue = _nearestDamagingWeapon;
            _nearestDamagingWeapon = null;
            return returnValue;
        }
        return null;
    }
    
    public Collider[] EnemyAndTeammateBetweenMeAndEnemy()
    {
        return _jiaMateAmMate != null && _nearestEnemy != null ? (new Collider[2] { _jiaMateAmMate, _nearestEnemy }) : null;
    }
    
    List<GameObject> _enemiesByDistance = new List<GameObject>();
    public List<GameObject> GetEnemiesByDistance(bool refresh)
    {
        if (_teamConfig == null)
        {
            _enemiesByDistance.Clear();
            return _enemiesByDistance;
        }
        if (refresh)
            _enemiesByDistance = FindTargetsByDistance(this._teamConfig.myEnemies.ToArray(), SharedUnitDic);
        return _enemiesByDistance;
    }
    
    List<GameObject> _alliesByDistance = new List<GameObject>();
    List<GameObject> GetAlliesAndSelfByDistance(bool refresh)
    {
        if (_teamConfig == null)
        {
            _alliesByDistance.Clear();
            return _alliesByDistance;
        }
        if (refresh)
            _alliesByDistance = this.FindTargetsByDistance(new Team[] { this._teamConfig.myTeam }, SharedUnitDic);
        return _alliesByDistance;
    }
    
    public GameObject GetLastDeadEnemies()
    {
        var _enemiesByDistance = FindTargetsByDistance(this._teamConfig.myEnemies.ToArray(), SharedDeadUnitDic);
        return _enemiesByDistance.LastOrDefault();
    }
    
    Collider FindNearestCollider(List<Collider> list)
    {
        if (list.Count == 0)
        {
            return null;
        }
        if (list[0] == null)
        {
            return null;
        }
        if (list.Count == 1)
            return list[0];
        
        Collider target = list[0];
        for (var i = 1; i < list.Count; i++)
        {
            if (list[i] == null)
                continue;
            if (HorizontalDistanceCompare(target.transform.position, list[i].transform.position) == 1)
            {
                target = list[i];
            }
        }
        return target;
    }
    
    public bool AllyBetweenSelfAndEnemy(float judgmentRange)
    {
        GetEnemiesByDistance(true);
        GetAlliesAndSelfByDistance(true);
        if (_enemiesByDistance.Count > 0 && _alliesByDistance.Count > 1)
        {
            float disToNearestEnemy2j = HorizontalDistanceCompare(_enemiesByDistance[0].transform.position, Center.position);
            float disToNearestAlly2j = HorizontalDistanceCompare(_alliesByDistance[1].transform.position, Center.position);
            return disToNearestEnemy2j >= disToNearestAlly2j && disToNearestEnemy2j < Mathf.Pow(judgmentRange, 2) && 
                   Vector3.Angle((_enemiesByDistance[0].transform.position - Center.position), (_alliesByDistance[1].transform.position - Center.position)) < 40;
        }
        return false;
    }
}
