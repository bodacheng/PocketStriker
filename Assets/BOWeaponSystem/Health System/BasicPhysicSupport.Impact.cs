using UnityEngine;

public partial class BasicPhysicSupport
{
    readonly System.Collections.Generic.List<BasicPhysicSupport> impactFollowers = new System.Collections.Generic.List<BasicPhysicSupport>();
    bool synchronizingImpactFollowers;
    Data_Center impactSource;
    Vector3 impactOffset;
    Collider ignoredSourceBody, ignoredVictimBody;
    public bool FollowingImpact => impactSource != null;

    public void BeginImpactFollow(Data_Center source)
    {
        EndImpactFollow();
        if (source == null || source == _DATA_CENTER) return;
        // Body-attached reactions can chain in a crowd, but must never form a movement cycle.
        for (var cursor = source; cursor != null; cursor = cursor._BasicPhysicSupport?.impactSource)
            if (cursor == _DATA_CENTER) return;
        impactSource = source;
        source._BasicPhysicSupport.impactFollowers.Add(this);
        var separation = CurrentRootPosition() - source.WholeT.position;
        separation.y = 0;
        if (separation.sqrMagnitude < .0001f)
        {
            separation = source.WholeT.forward;
            separation.y = 0;
        }
        var victimProfile = GetComponent<CombatBodyProfile>();
        var sourceProfile = source._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
        float minimum = (victimProfile?.Radius ?? .68f) + (sourceProfile?.Radius ?? .68f) + .08f;
        impactOffset = separation.normalized * Mathf.Max(minimum, Mathf.Min(separation.magnitude, 2.8f));
        ignoredSourceBody = sourceProfile?.Body;
        ignoredVictimBody = victimProfile?.Body;
        if (ignoredSourceBody != null && ignoredVictimBody != null)
        {
            // The reaction owns this pair's relative position; the solver must not shove the attacker back.
            Physics.IgnoreCollision(ignoredSourceBody, ignoredVictimBody, true);
            hiddenMethods.RemoveTouchedEnemyBody(ignoredSourceBody);
            source._BasicPhysicSupport.hiddenMethods.RemoveTouchedEnemyBody(ignoredVictimBody);
        }
        Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        SetRigidbodyVelocity(Vector3.zero, Vector3.zero);
    }

    public bool UpdateImpactFollow()
    {
        if (impactSource == null || !impactSource.WholeT.gameObject.activeInHierarchy
            || impactSource.FightDataRef.IsDead.Value)
        {
            EndImpactFollow();
            return false;
        }
        var target = impactSource.WholeT.position + impactOffset;
        target.y = CurrentRootPosition().y;
        // Match the source's owned displacement rather than lagging behind a faster charge.
        // The locked planar offset keeps the victim in front without following the rotating foot.
        ApplySkillPosition(target, true);
        return true;
    }

    public void EndImpactFollow()
    {
        if (ignoredSourceBody != null && ignoredVictimBody != null)
            Physics.IgnoreCollision(ignoredSourceBody, ignoredVictimBody, false);
        if (impactSource != null) impactSource._BasicPhysicSupport?.impactFollowers.Remove(this);
        impactSource = null;
        ignoredSourceBody = ignoredVictimBody = null;
    }

    void SynchronizeImpactFollowers()
    {
        if (synchronizingImpactFollowers) return;
        synchronizingImpactFollowers = true;
        try
        {
            for (int index = impactFollowers.Count - 1; index >= 0; index--)
                if (index < impactFollowers.Count && impactFollowers[index] != null) impactFollowers[index].UpdateImpactFollow();
        }
        finally { synchronizingImpactFollowers = false; }
    }

    void ReleaseImpactFollowers()
    {
        for (int index = impactFollowers.Count - 1; index >= 0; index--)
            if (index < impactFollowers.Count && impactFollowers[index] != null) impactFollowers[index].EndImpactFollow();
        impactFollowers.Clear();
    }

    // Physics velocity and animation root motion may move the source after the victim's FixedUpdate.
    // Synchronize this pair after those owners, without smoothing or rewriting ordinary contact motion.
    void LateUpdate()
    {
        if (impactFollowers.Count > 0)
        {
            var current = CurrentRootPosition();
            var reachable = ConstrainImpactSource(current);
            if ((reachable - current).sqrMagnitude > .000001f) SetRootAndRigidbodyPosition(reachable, true, false);
        }
        SynchronizeImpactFollowers();
    }
    void OnDisable() { EndImpactFollow(); ReleaseImpactFollowers(); }
    void OnDestroy() { EndImpactFollow(); ReleaseImpactFollowers(); }
}
