using System.Collections.Generic;
using MCombat.Shared.Combat;
using UnityEngine;

public partial class Sensor
{
    readonly List<Collider> _targetRangeEnemies = new List<Collider>();
    readonly List<GameObject> _deadEnemiesByDistance = new List<GameObject>();
    
    public List<Collider> GetTargetRangeEnemyCollider(float min, float max)
    {
        _targetRangeEnemies.Clear();
        if (_detectedEnemies == null || _detectedEnemies.Count == 0 || Center == null)
            return _targetRangeEnemies;

        var minSqr = min * min;
        var maxSqr = max * max;
        var center = Center.position;
        for (var i = 0; i < _detectedEnemies.Count; i++)
        {
            var enemy = _detectedEnemies[i];
            if (enemy == null)
            {
                continue;
            }

            var sqr = CombatSpatialUtility.HorizontalDistanceSqr(enemy.transform.position, center);
            if (sqr >= minSqr && sqr <= maxSqr)
            {
                _targetRangeEnemies.Add(enemy);
            }
        }

        return _targetRangeEnemies;
    }
    
    public Collider GetClosestEnemyColliderInSensorRange()
    {
        return _nearestEnemyCollider;
    }
    
    public Collider GetSuddenThreatInRange(float min,float max)
    {
        var threat = GetClosestEnemyHitBoxColliderInSensorRange();
        if (threat == null || Center == null)
        {
            return null;
        }

        var sqr = CombatSpatialUtility.HorizontalDistanceSqr(threat.transform.position, Center.position);
        var minSqr = min * min;
        var maxSqr = max * max;
        if (sqr >= minSqr && sqr <= maxSqr)
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
            FindTargetsByDistance(this._teamConfig.myEnemies.ToArray(), SharedUnitRegistry, _enemiesByDistance);
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
            FindTargetsByDistance(new [] { this._teamConfig.myTeam }, SharedUnitRegistry, _alliesByDistance);
        return _alliesByDistance;
    }
    
    public GameObject GetLastDeadEnemies()
    {
        if (_teamConfig == null)
        {
            return null;
        }

        FindTargetsByDistance(this._teamConfig.myEnemies.ToArray(), SharedDeadUnitRegistry, _deadEnemiesByDistance);
        return _deadEnemiesByDistance.Count > 0 ? _deadEnemiesByDistance[_deadEnemiesByDistance.Count - 1] : null;
    }
    
    Collider FindNearestCollider(List<Collider> colliderList)
    {
        if (colliderList == null || colliderList.Count == 0 || Center == null)
            return null;

        return CombatSpatialUtility.FindNearest(
            colliderList,
            Center.position,
            collider => collider != null ? collider.transform.position : (Vector3?)null);
    }
    
    public bool AllyBetweenSelfAndEnemy(float judgmentRange)
    {
        GetEnemiesByDistance(true);
        GetAlliesAndSelfByDistance(true);
        if (_enemiesByDistance.Count > 0 && _alliesByDistance.Count > 1)
        {
            float disToNearestEnemy2j = CombatSpatialUtility.HorizontalDistanceSqr(_enemiesByDistance[0].transform.position, Center.position);
            float disToNearestAlly2j = CombatSpatialUtility.HorizontalDistanceSqr(_alliesByDistance[1].transform.position, Center.position);
            return disToNearestEnemy2j >= disToNearestAlly2j && disToNearestEnemy2j < Mathf.Pow(judgmentRange, 2) && 
                   Vector3.Angle((_enemiesByDistance[0].transform.position - Center.position), (_alliesByDistance[1].transform.position - Center.position)) < 40;
        }
        return false;
    }
}
