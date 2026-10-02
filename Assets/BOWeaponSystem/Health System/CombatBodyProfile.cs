using UnityEngine;

/// <summary>Stable world-sized body contact shape, separate from animated damage volumes.</summary>
public sealed class CombatBodyProfile : MonoBehaviour
{
    static readonly System.Collections.Generic.Dictionary<Collider, CombatBodyProfile> Bodies = new System.Collections.Generic.Dictionary<Collider, CombatBodyProfile>();
    public static bool TryGet(Collider body, out CombatBodyProfile profile) => Bodies.TryGetValue(body, out profile);
    public CapsuleCollider Body { get; private set; }
    public float Radius { get; private set; }
    public float Height { get; private set; }
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
        float headHeight = center.head_t != null ? center.head_t.position.y - root.position.y + .35f : 3.1f;
        Height = Mathf.Clamp(headHeight, 2.8f, 3.4f);
        Radius = Mathf.Clamp(Height * .22f, .62f, .74f);
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
