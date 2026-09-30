using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class BoundaryControlByGod : MonoBehaviour {
    
    [SerializeField] List<ParticleSystem> BattleRingPSs;
    [SerializeField] float BattleRingRadius = 20f;

    [SerializeField] SensorUnity sensorUnity;
    
    ParticleSystem BattleRingPS;
    GameObject battleGround;
    public static float _BattleRingRadius;
    public static BoundaryControlByGod target;
    public SensorUnity SensorUnity => sensorUnity;
    public float DefaultBattleRadius => BattleRingRadius;
    public float EffectiveBattleRadius => currentRadius > 0 ? currentRadius : BattleRingRadius;
    public float ArenaScale => EffectiveBattleRadius / Mathf.Max(0.1f, BattleRingRadius);
    public GameObject CurrentBattleGround => battleGround;
    float currentRadius;
    Vector3 battleGroundDefaultScale;
    readonly Dictionary<ParticleSystem, Vector3> ringDefaultScales = new Dictionary<ParticleSystem, Vector3>();
    readonly Dictionary<ParticleSystem, Vector3> ringDefaultParticleSizes = new Dictionary<ParticleSystem, Vector3>();

    public void ConfigureBattleRadius(bool groupBattle, float requiredRadius = 0f)
    {
        float previousRadius = EffectiveBattleRadius;
        currentRadius = groupBattle ? Mathf.Max(BattleRingRadius, requiredRadius) : BattleRingRadius;
        _BattleRingRadius = currentRadius;
        int count = FightLoad.Fight?.FightMembers != null
            ? FightLoad.Fight.FightMembers.HeroSets.Count + FightLoad.Fight.FightMembers.EnemySets.Count : 2;
        sensorUnity?.Setup(currentRadius, Vector3.zero, Mathf.Max(20, count * 10));
        if (BattleRingPSs != null)
            foreach (var ring in BattleRingPSs)
            {
                if (ring == null) continue;
                if (!ringDefaultScales.TryGetValue(ring, out var scale))
                {
                    scale = ring.transform.localScale;
                    ringDefaultScales.Add(ring, scale);
                }
                ring.transform.localScale = scale * ArenaScale;
                foreach (var particles in ring.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particles.main;
                    if (main.scalingMode == ParticleSystemScalingMode.Hierarchy) continue;
                    if (!ringDefaultParticleSizes.TryGetValue(particles, out var sizes))
                    {
                        sizes = new Vector3(main.startSizeXMultiplier, main.startSizeYMultiplier, main.startSizeZMultiplier);
                        ringDefaultParticleSizes.Add(particles, sizes);
                    }
                    // Shape/Local scaling ignores the parent's scale for particle sizes.
                    // Keep the authored mode and size at the ordinary radius.
                    main.startSizeXMultiplier = sizes.x * ArenaScale;
                    if (main.startSize3D)
                    {
                        main.startSizeYMultiplier = sizes.y * ArenaScale;
                        main.startSizeZMultiplier = sizes.z * ArenaScale;
                    }
                }
                if (!Mathf.Approximately(previousRadius, currentRadius) && ring.gameObject.activeInHierarchy)
                {
                    // World-space particles retain their old size until they are emitted again.
                    ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ring.Play(true);
                }
            }
        ApplyGroundScale();
    }

    void ApplyGroundScale()
    {
        if (battleGround != null)
            battleGround.transform.localScale = new Vector3(battleGroundDefaultScale.x * ArenaScale,
                battleGroundDefaultScale.y, battleGroundDefaultScale.z * ArenaScale);
    }
    
    void Awake()
    {
        target = this;
        _BattleRingRadius = BattleRingRadius;
    }
    
    void Start()
    {
        if (BattleRingPSs != null && BattleRingPSs.Count > 0)
        {
            int choose = Random.Range(0, BattleRingPSs.Count);
            for (int i = 0; i < BattleRingPSs.Count; i++)
            {
                if (i == choose)
                {
                    BattleRingPS = BattleRingPSs[i];
                    BattleRingPS.gameObject.SetActive(true);
                }
                else
                {
                    BattleRingPSs[i].gameObject.SetActive(false);
                }
            }
        }

        var detectColliderCount = 0;
        // 我们这里是大致的认为每个角色的所有hit box加上可能放出来的武器collider一共10个
        switch (FightLoad.Fight.EventType)
        {
            case FightEventType.Gangbang:
                detectColliderCount = (FightLoad.Fight.FightMembers.HeroSets.GetValues().Count +
                                    FightLoad.Fight.FightMembers.EnemySets.GetValues().Count) * 10;
                break;
            default:
                if (FightLoad.Fight.team1Mode == TeamMode.MultiRaid)
                {
                    detectColliderCount = (FightLoad.Fight.FightMembers.HeroSets.GetValues().Count +
                                           FightLoad.Fight.FightMembers.EnemySets.GetValues().Count) * 10;
                }
                else
                {
                    detectColliderCount = 20;
                }
                break;
        }
        
        SensorUnity.Setup(EffectiveBattleRadius, Vector3.zero, detectColliderCount);
    }
    
    private int _currentBackGroundNum = -1;
    private int _backgroundRequest;
    public async UniTask ChangeBackGround(int number)
    {
        var request = ++_backgroundRequest;
        if (_currentBackGroundNum == number && battleGround != null)
            return;

        // Keep the current environment until its replacement has finished loading.
        var loaded = await AddressablesLogic.LoadObject("battleGround/" + number);
        if (loaded == null)
            return;

        // A scene exit or a newer selection can occur while Addressables is loading.
        if (this == null || request != _backgroundRequest)
        {
            Destroy(loaded);
            return;
        }

        var settings = loaded.GetComponent<BattleGround>();
        if (settings != null)
            settings.Set();
        else
            Debug.LogWarning($"[BattleGround] battleGround/{number} has no BattleGround component. " +
                "Keeping the instantiated transform. Rebuild and publish Addressables with the current player scripts.");

        if (battleGround != null)
            Destroy(battleGround);
        battleGround = loaded;
        battleGroundDefaultScale = loaded.transform.localScale;
        ApplyGroundScale();
        _currentBackGroundNum = number;
    }

    void OnDestroy()
    {
        if (target == this)
            target = null;
    }
}

//public void SUOQUANER(int aliveMemberCount)
//{
//    float targetBattleGroundRingRadius = 30;
//    switch (aliveMemberCount)
//    {
//        case 7:
//            targetBattleGroundRingRadius = 20;
//            break;
//        case 6:
//            targetBattleGroundRingRadius = 15;
//            break;
//        case 5:
//            targetBattleGroundRingRadius = 10;
//            break;
//        case 4:
//            targetBattleGroundRingRadius = 7;
//            break;
//        case 3:
//            targetBattleGroundRingRadius = 7;
//            break;
//        case 2:
//            break;
//        default:
//            break;
//    }
//    ChangeMagicRingRadius(targetBattleGroundRingRadius);
//}

//public IDictionary<Team, List<Data_Center>> AllMembers;//双方队伍人员字典，和netfightscene模块里同名变量统一。
//float distanceFromCharToCenter;
//public void RoundBattleFieldNormalControl(Vector3 battleRingCenter)
//{
//    if (AllMembers == null)
//        return;
//    foreach (KeyValuePair<Team, List<Data_Center>> pair in AllMembers)
//    {
//        foreach (Data_Center oneBoy in pair.Value)
//        {
//            if (!oneBoy.IsDead.Value)
//            {
//                battleRingCenter.y = oneBoy.WholeT.position.y;
//                distanceFromCharToCenter = (oneBoy.WholeT.position - battleRingCenter).magnitude;
//                if (distanceFromCharToCenter > BattleRingRadius)
//                {
//                    oneBoy._BasicPhysicSupport.hiddenMethods.onBattleGroundBundary = true;
//                    oneBoy.WholeT.position = Vector3.Lerp(oneBoy.WholeT.position, battleRingCenter,Time.deltaTime); //Vector3.Lerp(oneBoy.WholeT.position, battleRingCenter,Time.deltaTime * (distanceFromCharToCenter - BattleRingRadius) * 0.4f);
//                    oneBoy._BasicPhysicSupport.hiddenMethods.antiWallDirection = battleRingCenter - oneBoy.WholeT.position;
//                }
//                else
//                {
//                    oneBoy._BasicPhysicSupport.hiddenMethods.onBattleGroundBundary = false;
//                }
//            }
//            else
//            {
//                oneBoy._BasicPhysicSupport.hiddenMethods.onBattleGroundBundary = false;
//            }
//        }
//    }
//}
