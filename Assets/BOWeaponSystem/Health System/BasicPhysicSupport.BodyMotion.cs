using UnityEngine;

public partial class BasicPhysicSupport
{
    static RaycastHit[] bodySweepHits = new RaycastHit[32];

    Vector3 ClampOwnedBodyMotion(Vector3 target)
    {
        if (IsPreparingBattle || Rigidbody == null) return target;
        var profile = GetComponent<CombatBodyProfile>();
        if (profile?.Body == null || !profile.Body.enabled || profile.Body.isTrigger) return target;
        var current = CurrentRootPosition();
        var delta = target - current;
        float distance = delta.magnitude;
        if (distance < .001f || new Vector2(delta.x, delta.z).sqrMagnitude < .000001f) return target;
        var direction = delta / distance;
        // Damage limbs are triggers; sweep only the stable world-sized contact capsule.
        float radius = Mathf.Max(.01f, profile.Radius - .015f);
        var lower = current + Vector3.up * profile.Radius;
        var upper = current + Vector3.up * (profile.Height - profile.Radius);
        int count;
        while (true)
        {
            count = Physics.CapsuleCastNonAlloc(lower, upper, radius, direction, bodySweepHits, distance,
                ~0, QueryTriggerInteraction.Ignore);
            if (count < bodySweepHits.Length || bodySweepHits.Length >= 1024) break;
            bodySweepHits = new RaycastHit[bodySweepHits.Length * 2];
        }
        float allowed = distance;
        for (int index = 0; index < count; index++)
        {
            var hit = bodySweepHits[index];
            if (hit.collider == null || hit.collider == profile.Body || !CombatBodyProfile.TryGet(hit.collider, out var other)
                || other == null || !other.isActiveAndEnabled
                || Physics.GetIgnoreLayerCollision(profile.Body.gameObject.layer, hit.collider.gameObject.layer)
                || Physics.GetIgnoreCollision(profile.Body, hit.collider)) continue;
            // Allow a body already in contact to move away while the solver resolves penetration.
            if (hit.distance < .02f && Vector3.Dot(delta, hit.collider.bounds.center - profile.Body.bounds.center) <= 0) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - .005f));
        }
        // Body contacts restrict planar passage, never hold a jumping/knocked-down
        // fighter above the ground by suppressing its authored vertical trajectory.
        var reachable = current + direction * allowed;
        reachable.y = target.y;
        return reachable;
    }

    Vector3 ConstrainImpactSource(Vector3 target)
    {
        // The source cannot outrun a victim blocked by the ring or another solid body.
        for (int index = impactFollowers.Count - 1; index >= 0; index--)
        {
            var follower = impactFollowers[index];
            if (follower == null || !follower.FollowingImpact) continue;
            var desired = target + follower.impactOffset;
            desired.y = follower.CurrentRootPosition().y;
            var reachable = follower.ClampOwnedBodyMotion(follower.ClampPositionToBattleRange(desired));
            var correction = reachable - desired;
            correction.y = 0;
            target += correction;
        }
        return target;
    }
}
