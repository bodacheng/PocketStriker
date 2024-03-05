using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Log;

namespace HittingDetection
{
    public partial class HitBoxManager : MonoBehaviour
    {
        #region settings
        [Tooltip("Should the Markers be active upon the Start of this weapon?")]
        [SerializeField] float ActivateAfterTime;
        [Tooltip("特定针对")]
        [SerializeField] SpecificTarget SpecificTarget = SpecificTarget.both;
        [Tooltip("damageTypeOfTheWeapon")]
        public DamageType damage_type = DamageType.light_damage_forward;
        [Tooltip("damageTypeOfTheWeapon")]
        public WeaponMode _WeaponMode;
        [Tooltip("AT Weights")]
        public float AT_weight = 1;
        [Tooltip("weaponHP, when below 0, is not an energy")]
        public int weaponHP = -1;
        [Tooltip("heavyLevel 关系对其他武器形成的损耗力。详见WpHpCost")]
        public int heavyLevel = 1;
        [Tooltip("如果是特效类攻击，是否为贴地魔法")]
        public bool onGroundMagic;//这个是和其他模块联动的。确实不得不放这儿。
        [Tooltip("特效是否有粘身视效")]
        public bool effectSpreadOnBody;
        [Tooltip("魔法特效路径(blueMagic redMagic)")]
        public Element element;
        [Tooltip("启动特效")]
        public string muzzle;
        [Tooltip("打击特效")] 
        public string hitEffect;// 空为默认
        [Tooltip("特殊能量消耗特效")]
        public string ExplosionEffect = "energy_resolve";
        [Tooltip("A weapon can work in two ways: Manual and Continuous. Manual bases on an idea that each attack (like a sword swing) can hit each target only once - it then needs to be manualy reloaded through a function call or an Animation Event (for example by ClearTargets() - explained in the Info Bool, at the bottom of this Inspector window). This method is very precise, as you can be certain that each Target will get damage and  Hurt animation (on BS_Main_Health script) triggered only once with each swing. Manual should be used in most situations, mostly for animation-driven combat (like Dark Souls or God of War). The Continuous mode deals damage constantly, in a given time interval, giving you a constant damage-dealing weapon. It's better for VR and games with free-moving weapons (like when the Player can swing his sword by moving his mouse in any desired direction) - the downside of Continuous damage is that it takes away the precise control of the damage dealt (as each Target may be hit more than once depending on how fast the weapon is being driven around). Continuous should be used with blades which can move freely, independent from animations. [INFO:] Continuous damage is not fit for working with shields (BS_Shield script objects).")]
        public bool ContinuousDamage = false;
        [Tooltip("If you choose the Continuous damage, what should the interval of damage dealing be? (In seconds)")]
        public float ContinuousDamageInterval = 0.2f;
        #endregion

        #region realtime param
        bool _enabled;
        public bool Enabled => _enabled;
        public float CurrentHP { get; set; }
        FightParamsReference _attackerRef;
        TeamConfig teamConfig = TeamConfig.DefaultSet;
        Transform _WeaponHolderCenter;//角色几何中心，如果是能量道具则为能量道具的几何中心，用于防御判断。
        bool HitFlesh;
        bool HitShield;
        private List<Marker> _markers = new List<Marker>();
        List<Transform> _usedTargets = new List<Transform>(); // 就是每一帧所碰撞到的所有collider的母体。所有collider。不论是否包含mainhealth什么的。是以武器启动周期为处理单位。处理过的单位才会加入至其中
        readonly List<Transform> _Targets_Raw_Hit = new List<Transform>(); //Targets initialy hit by the blade (pre-check 这个是以一帧为单位处理，为了避免多个marker重复处理击中的bodyhealth。
        private readonly List<Transform> _shieldsHit = new List<Transform>();
        private readonly List<Vector3> _shieldHitPos = new List<Vector3>();
        private readonly List<V_Damage> hitsOnHealthBody = new List<V_Damage>();
        readonly bool _traditionalDefendMode = false;
        
        public string GeneratedByStateKey { get; set; }
        HitBoxLifeEnding hitBoxLifeEnding = HitBoxLifeEnding.untouched;
        public HitBoxLifeEnding HitBoxLifeEnding
        {
            get => hitBoxLifeEnding;
            set
            {
                switch (value)
                {
                    case HitBoxLifeEnding.untouched:
                    case HitBoxLifeEnding.successed:
                        hitBoxLifeEnding = value;
                        break;
                    case HitBoxLifeEnding.touched:
                        if (value != HitBoxLifeEnding.successed)
                            hitBoxLifeEnding = value;
                        break;
                }
            }
        }
        #endregion
        
        float AT;
        public float GetDamageAmount()
        {
            return weaponHP > 0 ? AT / weaponHP : AT;
        }

        void Awake()
        {
            Transform _MarkersParent = transform;
            Transform[] children = new Transform[_MarkersParent.childCount];
            var bms = new List<Marker>();
            for (var i = 0; i < children.Length; i++)
            {
                var bO_Marker = _MarkersParent.GetChild(i).gameObject.GetComponent<BO_Marker>();
                if (bO_Marker != null)
                {
                    bO_Marker.LocalAwake();
                    bms.Add(bO_Marker);
                    bO_Marker.SetOwner(this);
                    var c = _MarkersParent.GetChild(i).gameObject.GetComponent<Collider>();
                    HitBoxesProcesser.AddToColliderHitBoxDic(c, this);
                }
            }
            _markers = bms;
        }
        
        Coroutine _delayEnableMarkers;
        public void MarkersEnablingStarts()
        {
            if (System.Math.Abs(ActivateAfterTime) == 0)
            {
                EnableMarkers();
            }
            else
            {
                IEnumerator Execution(float seconds)
                {
                    yield return new WaitForSeconds(seconds);
                    _Raw_Target_Instance = null;
                    ClearTargets();
                    EnableMarkers();
                }
                _delayEnableMarkers = StartCoroutine (Execution(ActivateAfterTime));
            }
        }
        
        public void Local_OnDisable()
        {
            if (_delayEnableMarkers != null)
                StopCoroutine(_delayEnableMarkers);
            DisableMarkers();
            SetTeamConfig(TeamConfig.DefaultSet);
        }

        public void SetReferenceTransformInfo(Transform centerT)
        {
            _WeaponHolderCenter = centerT;
        }

        public void SetOwnerFACR(FightParamsReference value)
        {
            _attackerRef = value;
            AT = _attackerRef == null ? 0 : _attackerRef.AT * AT_weight;
        }
        public FightParamsReference GetOwnerFACR()
        {
            return _attackerRef;
        }
        public void SetDetectionTargetsUnion(List<Transform> usedTargets)
        {
            _usedTargets = usedTargets;
        }

        public void SetTeamConfig(TeamConfig teamConfig)
        {
            this.teamConfig = teamConfig;
            for (var i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                marker._layers = teamConfig.mySensorAndWeaponTargetLayerMask;
                marker.enemyShieldLayer = teamConfig.enemyShieldLayerMask;
                marker.gameObject.layer = teamConfig.myWeaponLayer;
            }
        }

        public void EnableMarkers()
        {
            _usedTargets?.Clear();
            _shieldsHit.Clear();
            for (var i = 0; i < _markers.Count; i++)
            {
                _markers[i].EnableMarkerProcess(teamConfig.myWeaponLayer);
            }
            _enabled = true;
        }

        public void DisableMarkers()
        {
            _enabled = false;
            _usedTargets?.Clear();
            _shieldsHit.Clear();
            for (var i = 0; i < _markers.Count; i++)
            {
                _markers[i].DisableMarkerProcess();
            }
        }
        
        public void ClearTargets()
        {
            _usedTargets?.Clear();
            _shieldsHit.Clear();
            for (var i = 0; i < _markers.Count; i++)
            {
                _markers[i].ClearMarkerProcess();
            }
        }

        public void SetDamageType(DamageType damageType)
        {
            damage_type = damageType;
        }

        public void LocalUpdate()
        {
            if (_enabled)
            {
                HitFlesh = false;
                HitShield = false;
                DetectProcess();
            }
        }
        
        void ClearMarkersDetections()
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                _markers[i].ClearMarkerProcess();
            }
        }

        public void LocalLateUpdate()
        {
            if (_enabled)
            {
                TreatProcess();
                ClearMarkersDetections();
            }
        }
    }
}