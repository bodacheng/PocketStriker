using UnityEngine;

/// <summary>Stable world-sized body contact shape, separate from animated damage volumes.</summary>
public sealed class CombatBodyProfile : MonoBehaviour
{
    static readonly System.Collections.Generic.Dictionary<Collider, CombatBodyProfile> Bodies = new System.Collections.Generic.Dictionary<Collider, CombatBodyProfile>();
    public static bool TryGet(Collider body, out CombatBodyProfile profile) => Bodies.TryGetValue(body, out profile);
    public CapsuleCollider Body { get; private set; }
    public float Radius { get; private set; }
    public float Height { get; private set; }
    public Vector2 ReferenceTorsoSize { get; private set; }
    public float ReferenceVisualHeight { get; private set; }
    public float ReferenceTorsoRadius { get; private set; }
    public const float BodyClearance = .12f;
    bool dimensionsCalibrated;
    Collider torsoCollider;
    Collider registeredBody;
    Data_Center owner;
    PhysicsMaterial material;

    public static CombatBodyProfile Apply(Data_Center center)
    {
        var profile = center._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
        if (profile == null) profile = center._BasicPhysicSupport.gameObject.AddComponent<CombatBodyProfile>();
        profile.Configure(center);
        return profile;
    }

    void Configure(Data_Center center)
    {
        owner = center;
        if (Body == null)
        {
            var node = new GameObject("CombatBody");
            node.transform.SetParent(center.WholeT, false);
            Body = node.AddComponent<CapsuleCollider>();
            registeredBody = Body;
            Bodies[Body] = this;
            material = new PhysicsMaterial("PocketStriker body contact")
            {
                dynamicFriction = .05f, staticFriction = .05f, bounciness = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            Body.sharedMaterial = material;
        }
        var root = center.WholeT;
        if (!dimensionsCalibrated) CalibrateDimensions(center);
        var scale = root.lossyScale;
        float verticalScale = Mathf.Max(.001f, Mathf.Abs(scale.y));
        float horizontalScale = Mathf.Max(.001f, Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Body.direction = 1;
        Body.height = Height / verticalScale;
        Body.radius = Radius / horizontalScale;
        Body.center = new Vector3(0, Height / (2 * verticalScale), 0);
        Body.contactOffset = .015f;
        Body.enabled = true;
        SetLayer(center._TeamConfig.mylayer);
        SetColliding(true);
        var body = center._BasicPhysicSupport.Rigidbody;
        body.solverIterations = 8;
        body.solverVelocityIterations = 2;
        body.maxDepenetrationVelocity = 2f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        // Animated limbs still receive attacks, but no longer push whole bodies around.
        foreach (var limb in root.GetComponentsInChildren<BO_Limb>(true))
        {
            var collider = limb.myColliderMustEquip ?? limb.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
        }
    }

    void FixedUpdate()
    {
        if (owner == null || Body == null || !Body.enabled || Body.isTrigger
            || owner.FightDataRef.IsDead.Value || owner.FightDataRef.GettingDamage
            || FightScene.FSceneProcessesRunner.Main.currentProcess is not FightScene.FightingProcess)
            return;
        var physics = owner._BasicPhysicSupport;
        if (physics == null || physics.FollowingImpact || physics.hiddenMethods == null || !physics.hiddenMethods.Grounded)
            return;
        var state = owner._MyBehaviorRunner?.GetNowState();
        if (state == null || (state.StateKey != "Empty" && state.StateKey != "Stand" && state.StateKey != "Defend"))
            return;
        var body = physics.Rigidbody;
        if (body == null || body.isKinematic) return;
        var velocity = body.linearVelocity;
        var planar = new Vector2(velocity.x, velocity.z);
        if (planar.sqrMagnitude <= Mathf.Epsilon) return;
        // Grounded fighters disable gravity, so floor friction alone cannot shed
        // the solver's residual horizontal push. Settle only an idle body velocity;
        // moving, attacking, blocking a hit and airborne trajectories keep their owners.
        planar *= Mathf.Exp(-12f * Time.fixedDeltaTime);
        if (planar.sqrMagnitude < .01f * .01f) planar = Vector2.zero;
        body.linearVelocity = new Vector3(planar.x, velocity.y, planar.y);
    }

    void CalibrateDimensions(Data_Center center)
    {
        var root = center.WholeT;
        float headHeight = center.head_t != null ? center.head_t.position.y - root.position.y + .35f : 3.1f;
        ReferenceVisualHeight = headHeight;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (renderer.enabled && renderer.sharedMesh != null)
                ReferenceVisualHeight = Mathf.Max(ReferenceVisualHeight, renderer.bounds.max.y - root.position.y);
        Height = Mathf.Clamp(ReferenceVisualHeight, 2.8f, 3.4f);

        var torsoTransform = center.spine_hitbox_t;
        var limb = torsoTransform != null ? torsoTransform.GetComponent<BO_Limb>() : null;
        torsoCollider = limb?.myColliderMustEquip ?? (torsoTransform != null ? torsoTransform.GetComponent<Collider>() : null);
        if (TryMeasureTorso(torsoCollider, root, out var halfWidth, out var halfDepth, out var offset))
        {
            ReferenceTorsoSize = new Vector2(halfWidth * 2f, halfDepth * 2f);
            ReferenceTorsoRadius = Mathf.Max(halfWidth + Mathf.Abs(Vector3.Dot(offset, root.right)),
                halfDepth + Mathf.Abs(Vector3.Dot(offset, root.forward)));
        }
        else
        {
            ReferenceTorsoRadius = Height * .28f;
            ReferenceTorsoSize = Vector2.one * ReferenceTorsoRadius * 2f;
        }
        // A 1.24-wide body allowed the wider chest models to visibly interleave.
        // Fit the torso plus clearance, while keeping the same rounded contact shape
        // and a narrow world-size range for small and large fighters. Extended arms
        // remain hurt volumes rather than making every idle pose a wide solid body.
        Radius = Mathf.Clamp(ReferenceTorsoRadius + BodyClearance, .88f, 1.08f);
        dimensionsCalibrated = true; // Retrying a round must not recalibrate from a bent/attacking pose.
    }

    public Vector3 TorsoCenter
    {
        get
        {
            if (torsoCollider is BoxCollider box) return box.transform.TransformPoint(box.center);
            if (torsoCollider is CapsuleCollider capsule) return capsule.transform.TransformPoint(capsule.center);
            return owner.WholeT.position + Vector3.up * Height * .5f;
        }
    }

    /// <summary>Signed gap between animated torso shapes on the line joining their centers, separate from capsule penetration.</summary>
    public float PlanarTorsoGap(CombatBodyProfile other)
    {
        var delta = other.TorsoCenter - TorsoCenter;
        delta.y = 0f;
        var direction = delta.sqrMagnitude > .000001f ? delta.normalized : owner.WholeT.forward;
        return delta.magnitude - TorsoExtent(direction) - other.TorsoExtent(direction);
    }

    float TorsoExtent(Vector3 direction)
    {
        if (torsoCollider is BoxCollider box)
        {
            var t = box.transform;
            return Mathf.Abs(Vector3.Dot(t.TransformVector(Vector3.right * box.size.x * .5f), direction))
                + Mathf.Abs(Vector3.Dot(t.TransformVector(Vector3.up * box.size.y * .5f), direction))
                + Mathf.Abs(Vector3.Dot(t.TransformVector(Vector3.forward * box.size.z * .5f), direction));
        }
        if (torsoCollider is CapsuleCollider capsule)
        {
            var t = capsule.transform;
            var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            var radial1 = capsule.direction == 0 ? Vector3.up : Vector3.right;
            var radial2 = capsule.direction == 2 ? Vector3.up : Vector3.forward;
            var worldAxis = t.TransformVector(axis);
            float radius = capsule.radius * Mathf.Max(t.TransformVector(radial1).magnitude, t.TransformVector(radial2).magnitude);
            return Mathf.Abs(Vector3.Dot(worldAxis.normalized, direction)) * Mathf.Max(0f, capsule.height * worldAxis.magnitude * .5f - radius) + radius;
        }
        return ReferenceTorsoRadius;
    }

    /// <summary>World contact radius at a world Y, including the capsule's rounded shoulder/foot caps.</summary>
    public float ContactRadiusAtWorldHeight(float worldY)
    {
        float y = worldY - owner.WholeT.position.y;
        if (y < 0f || y > Height) return 0f;
        float capDelta = y < Radius ? Radius - y : y > Height - Radius ? y - (Height - Radius) : 0f;
        return Mathf.Sqrt(Mathf.Max(0f, Radius * Radius - capDelta * capDelta));
    }

    static bool TryMeasureTorso(Collider torso, Transform root, out float halfWidth, out float halfDepth, out Vector3 offset)
    {
        halfWidth = halfDepth = 0f;
        offset = Vector3.zero;
        if (torso == null) return false;
        var t = torso.transform;
        Vector3 center;
        if (torso is BoxCollider box)
        {
            center = t.TransformPoint(box.center);
            var x = t.TransformVector(Vector3.right * box.size.x * .5f);
            var y = t.TransformVector(Vector3.up * box.size.y * .5f);
            var z = t.TransformVector(Vector3.forward * box.size.z * .5f);
            halfWidth = Mathf.Abs(Vector3.Dot(x, root.right)) + Mathf.Abs(Vector3.Dot(y, root.right)) + Mathf.Abs(Vector3.Dot(z, root.right));
            halfDepth = Mathf.Abs(Vector3.Dot(x, root.forward)) + Mathf.Abs(Vector3.Dot(y, root.forward)) + Mathf.Abs(Vector3.Dot(z, root.forward));
        }
        else if (torso is CapsuleCollider capsule)
        {
            center = t.TransformPoint(capsule.center);
            var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            var radial1 = capsule.direction == 0 ? Vector3.up : Vector3.right;
            var radial2 = capsule.direction == 2 ? Vector3.up : Vector3.forward;
            var worldAxis = t.TransformVector(axis);
            float radius = capsule.radius * Mathf.Max(t.TransformVector(radial1).magnitude, t.TransformVector(radial2).magnitude);
            float halfSegment = Mathf.Max(0f, capsule.height * worldAxis.magnitude * .5f - radius);
            halfWidth = Mathf.Abs(Vector3.Dot(worldAxis.normalized, root.right)) * halfSegment + radius;
            halfDepth = Mathf.Abs(Vector3.Dot(worldAxis.normalized, root.forward)) * halfSegment + radius;
        }
        else
        {
            // Authored torso shapes are boxes/capsules. Keep an explicit fallback
            // for other shapes without reading empty bounds on inactive reserves.
            return false;
        }
        offset = center - root.position;
        offset.y = 0f;
        return halfWidth > .01f && halfDepth > .01f;
    }

    public void SetLayer(int layer) { if (Body != null) Body.gameObject.layer = layer; }
    public void SetColliding(bool colliding) { if (Body != null) Body.isTrigger = !colliding; }
    void OnDisable() { owner?._BasicPhysicSupport?.EndImpactFollow(); }
    void OnDestroy()
    {
        if (!ReferenceEquals(registeredBody, null)) Bodies.Remove(registeredBody);
        if (Body != null) Destroy(Body.gameObject);
        if (material != null) Destroy(material);
    }
}
