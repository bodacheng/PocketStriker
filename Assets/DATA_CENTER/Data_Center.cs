using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using FightScene;
using UnityEngine;
using UniRx;
using Soul;

//Basically, Data_Center is a place where all parameter need applying to a character are initiazlized,
//Those paremeters are sent to all parts of unit from here.
[RequireComponent(typeof(BehaviorRunner))]
[RequireComponent(typeof(ShaderManager))]
[RequireComponent(typeof(BlendShapeProxy))]
public partial class Data_Center : MonoBehaviour
{
    public TeamConfig _TeamConfig = TeamConfig.DefaultSet;
    public Element element;
    public Transform geometryCenter;
    public Transform WholeT;
    public AudioSource _AudioSource;
    public readonly Sensor Sensor = new Sensor();
    public readonly AnimationManger AnimationManger = new AnimationManger();
    public readonly FightParamsReference FightDataRef = new FightParamsReference();
    public readonly BuffsRunner buffsRunner = new BuffsRunner();
    public SkillCancelFlag _SkillCancelFlag;
    public BO_Ani_E _BO_Ani_E;
    public BO_Weapon_Animation_Events bO_Weapon_Animation_Events;
    public BasicPhysicSupport _BasicPhysicSupport;
    public BehaviorRunner _MyBehaviorRunner;
    public ResistanceManager _ResistanceManager;
    public ShaderManager _ShaderManager;
    public BlendShapeProxy blendShapeProxy;
    public Personality_events Personality_events;
    
    public Transform right_hand_t, left_hand_t, right_foot_t, left_foot_t,tail_t, head_t;
    public Transform left_arm_hitbox_t, right_arm_hitbox_t, left_leg_hitbox_t, right_leg_hitbox_t, spine_hitbox_t;

    private bool Phase1Initialized;
    private bool Phase2Initialized;
    
    private FacialAnimManager _facialAnimManager;
    
    [Header("传统防御盾。可能真的用不到了")]
    public BO_Shield Shield;
    
    private UnitInfo unitInfo;
    public UnitInfo UnitInfo => unitInfo;
    
    public bool IfPreparedForBattle()
    {
        return Phase1Initialized && Phase2Initialized;
    }
    
    void Awake()
    {
        if (geometryCenter == null)
            geometryCenter = gameObject.transform; 
    }

    private void OnDestroy()
    {
        AnimationManger.AnimFreezeSequence?.Kill();
    }

    private List<Transform> posCalTrans = new List<Transform>();
    public Vector3 GetFarthestPositionFromZero()
    {
        float maxDistance = 0f;
        Vector3 returnValue = Vector3.zero;
        foreach (Transform t in posCalTrans)
        {
            var noYPos = t.position;
            noYPos.y = 0;
            float distance = Vector3.Distance(noYPos, Vector3.zero);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                returnValue = noYPos;
            }
        }
        return returnValue;
    }

    public async UniTask Step1Initialize(string type, string basicPackName)
    {
        if (!Phase1Initialized)
        {
            AnimationManger.AnimatorRef =  WholeT.GetComponent<Animator>();
            _facialAnimManager = WholeT.GetComponent<FacialAnimManager>();
            Sensor.Center = this.geometryCenter;
            Sensor.SensorRadius = 15f;
            FightDataRef.Center = this;
            _BasicPhysicSupport.Rigidbody.useGravity = false;
            _BasicPhysicSupport.Rigidbody.mass = 500f;
            BodyElementTagAndLayerSet(TeamConfig.DefaultSet);
            bO_Weapon_Animation_Events.hiddenMethods.AssignWeaponsFromDataCenter(FightDataRef,geometryCenter, right_hand_t, left_hand_t, right_foot_t, left_foot_t, head_t, tail_t);
            await AnimationManger.PreloadBasicPersonalAnims(type, basicPackName, _facialAnimManager);
            _BO_Ani_E.BasicMagicAndEffectsPathDefine(element);
            posCalTrans = new List<Transform>()
            {
                right_hand_t, left_hand_t, right_foot_t, left_foot_t, WholeT
            };
            
            //if (this.blendShapeProxy != null && this.blendShapeProxy.VRMBlendShapeProxy != null)
            //    this.blendShapeProxy.VRMBlendShapeProxy.AvaterRemerge(this.WholeT);
            //else
            //{
            //    this.blendShapeProxy = this.gameObject.GetComponent<BlendShapeProxy>();
            //    this.blendShapeProxy.VRMBlendShapeProxy.AvaterRemerge(this.WholeT);
            //}                
            Phase1Initialized = true;
        }
    }

    void BodyElementTagAndLayerSet(TeamConfig _TeamConfig)
    {
        this._TeamConfig = _TeamConfig;
        gameObject.layer = this._TeamConfig.mylayer;

        Sensor.SetDetectLayer(_TeamConfig,this);
        bO_Weapon_Animation_Events.hiddenMethods.AssignTeamFlag(_TeamConfig);
        if (Shield != null)
        {
            Shield.gameObject.SetActive(false);
            if (FightDataRef != null)
            {
                Shield.IniShield(_TeamConfig, FightDataRef);
                Shield._ShieldBackSpot = this.geometryCenter;
            }
        }
    }
    
    public async UniTask Step2Initialize(string type, Element element, SkillSet skillSet, int preloadCount)
    {
        if (!Phase2Initialized)
        {
            Phase2Initialized = true;
        }
        
        WholeT.gameObject.SetActive(true);// 动画模块的一些处理要求active状态下运行
        
        _MyBehaviorRunner.FormFightingSetsByNineAndTwo(skillSet);
        _MyBehaviorRunner.INIStates(this);
        Decomposition dreamBuffEffect = null;
        bool superDreamEnded = false;
        _MyBehaviorRunner.RegisterSequenceCommand(
            async () =>
            {
                superDreamEnded = false;
                AnimationManger.AddSpeedBuff("dreamCombo", FightGlobalSetting.DreamComboSpeed);
                this.FightDataRef.DreamComboGauge.Value = 0;
                EffectsManager.GenerateEffect("super_combo_explosion", FightGlobalSetting.EffectPathDefine(), WholeT.position, WholeT.rotation, WholeT).Forget();
                dreamBuffEffect = await EffectsManager.GenerateEffect("dream_buff", FightGlobalSetting.EffectPathDefine(), geometryCenter.position, default, geometryCenter);
                if (superDreamEnded)
                    dreamBuffEffect.Phase = -1;
                this.FightDataRef.DreamComboStart();
            },
            () =>
            {
                if (dreamBuffEffect != null)
                    dreamBuffEffect.Phase = -1;
                AnimationManger.RemoveSpeedBuff("dreamCombo");
                superDreamEnded = true;
            });
        
        _MyBehaviorRunner.SuperComboCondition = ()=> this.FightDataRef.HasPlentyDreamGauge() && !FightDataRef.IsDead.Value;
        
        var tasks = new List<UniTask>
        {
            EffectsManager.IniEffectsPool("short_effect", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("normal_effect", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("long_effect", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("Sparks", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("light_hit", FightGlobalSetting.EffectPathDefine(element), preloadCount),
            EffectsManager.IniEffectsPool("heavy_hit", FightGlobalSetting.EffectPathDefine(element), preloadCount),
            EffectsManager.IniEffectsPool("super_hit", FightGlobalSetting.EffectPathDefine(element), preloadCount),
            EffectsManager.IniEffectsPool("resistanceUp", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("on_enable_effect", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("FlashStart", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("FlashEnd", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("super_combo_explosion", FightGlobalSetting.EffectPathDefine(element), 1),
            EffectsManager.IniEffectsPool("dream_buff", FightGlobalSetting.EffectPathDefine(element), 1)
        };
        
        //这个环节之后我应该有一份列表来展示到底我一个角色一场战斗都能用上什么招
        // 上面这个环节结束后，有这样几个重要情况1. state_Transition_Dictionary的内容就正确了 2.AIStateRunner内的States_Dictionary实例内将有一份正确的skill类key的列表
        var toLoadSkillAnimsNames = _MyBehaviorRunner.PassSkillTypeKeys();
        tasks.Add(AnimationManger.PreloadPersonalAnimsResourceMode(type, toLoadSkillAnimsNames, element, preloadCount));
        await UniTask.WhenAll(tasks);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="teamConfig"></param>
    /// <param name="criticalGaugeMode"></param>
    /// <param name="aiMode"></param>
    /// <param name="aiDelayFrame"></param>
    /// <param name="teamHpRate"></param>
    /// <param name="lv"> 两种模式，如果带入-1，将按照角色等级来初始化，用于玩家账户队伍。带入其他值则按其他值来，用于关卡队伍 </param>
    public void Step3Initialize(TeamConfig teamConfig, CriticalGaugeMode criticalGaugeMode, AIMode aiMode, int aiDelayFrame, Func<bool> AITriggerDreamComboRateCondition,
        float teamHpRate, UnitInfo unitInfo)
    {
        this.unitInfo = unitInfo;
        FightDataRef.IsDead.Value = false;
        BodyElementTagAndLayerSet(teamConfig);
        _BasicPhysicSupport.Rigidbody.mass = FightGlobalSetting.FighterRigidMass;
        FightDataRef.FindAllSelfCollidersAndIgnoreCollision();
        FightDataRef.ChangeLayerForLimbs(teamConfig.mylayer);
        FightDataRef.EnableAllLimbs(true);
        FightDataRef._comboHitCount.HitCount.Value = 0;
        FightDataRef.CriticalGaugeMode = criticalGaugeMode;
        var hp = SkillSet.INI_Hp(unitInfo.set.SkillIDList(), unitInfo.level) * teamHpRate;
        FightDataRef.CurrentHp.Value = hp;
        FightDataRef.CurrentHp.Subscribe(x =>
        {
            FightDataRef.CurrentHp.Value = Mathf.Clamp(x, 0, hp);
        }).AddTo(gameObject);
        
        FightDataRef.Resistance.Subscribe(x =>
        {
            FightDataRef.Resistance.Value = Mathf.Clamp(x, 0, FightGlobalSetting._ResistanceMax);
        }).AddTo(gameObject);
        
        FightDataRef.CriticalGauge.Value = FightGlobalSetting._EXMax;
        _MyBehaviorRunner.SetAt(unitInfo.level);
        _MyBehaviorRunner.AIMode = aiMode;
        _MyBehaviorRunner.Controller.DecisionDelay = aiDelayFrame;
        _MyBehaviorRunner.AITriggerDreamComboRateCondition = () =>
            {
                if (teamConfig.myTeam == Team.player1 && 
                    RTFightManager.Target.team1.InputsManager.CurrentFocus.Value == this)
                {
                    return false;
                }
                return AITriggerDreamComboRateCondition();
            };
    }
    
    // for tutorial
    public void StartAutoModeWhenGetHurt()
    {
        var fullHp = FightDataRef.CurrentHp.Value;
        IDisposable disposeTask = null;
        disposeTask = FightDataRef.CurrentHp.Subscribe(x =>
        {
            if (x < fullHp)
            {
                disposeTask.Dispose();
                _MyBehaviorRunner.AI = true;
            }
        }).AddTo(gameObject);
    }

    //我们希望datacenter是整个角色初始化的出发点，那么这个地方应该也可以做到根据情况决定一些组件加载还是不加载。
    //关于角色初始化，我们需要把所有进行初始化的内容都拿出来进行一个归类工作。其实当下我们的系统里最不稳的一个事情在于
    //很多靠字符串表达的地址。。这种东西我们在读取角色和读取脚本的地方已经修改了，但在读取动画文件和读取脚本的地方仍然存在。
    //这些地方有没有必要给想办法去掉呢。。
    public void TurnShield(bool isShieldActive)
    {
        //if (PhotonNetwork.offlineMode || !PhotonNetwork.connected) {
        //} else
        //    photonView.RPC("turnShieldRPC", PhotonTargets.All, isShieldActive);
        if (Shield != null)
            Shield.gameObject.SetActive(isShieldActive);
            //Shield.EnableShieldCollider();   
    }

    void FixedUpdate()
    {
        if (_MyBehaviorRunner.IfRunning())
        {
            Sensor.SensorFixedUpdate();
            buffsRunner.BuffsRunnerFixedUpdate();
            FightDataRef.HealthBodyFixedUpdate();
            _SkillCancelFlag.hiddenMethods.SkillCancelFlagFixedUpdate();
        }
    }
    
    public void CleanClear()
    {
        Sensor.Stop();
        bO_Weapon_Animation_Events.ClearMarkerManagers();
        buffsRunner.EndAllCoroutines();
        _ResistanceManager.ResistanceClear();
        Personality_events.CloseAllPersonalityEffects();
        FightDataRef.GetHitTriggerEvents.Clear();
    }

    float p1_to_me, p2_to_me;
    public int HorizontalDistanceCompare(Vector3 p1, Vector3 p2)
    {
        p1.y = gameObject.transform.position.y;
        p1_to_me = (p1 - gameObject.transform.position).magnitude;

        p2.y = gameObject.transform.position.y;
        p2_to_me = (p2 - gameObject.transform.position).magnitude;

        return p1_to_me > p2_to_me ? 1 : p1_to_me < p2_to_me ? -1 : 0;
    }

    public GameObject[] MergerGameObjectArray(GameObject[] First, GameObject[] Second) //合并两个数组。不光是gameobjects，任何类型变量都可以通用。
    {
        GameObject[] result = new GameObject[First.Length + Second.Length];
        First.CopyTo(result, 0);
        Second.CopyTo(result, First.Length);
        return result;
    }
}

//4.25日的重要修改

 //现在，所有角色在创建的时候要遵循这样重要的一点：作为角色在地面支撑用的collider，其下沿边必须低于gameobject。transform。position，
 //并且高于floorcheckers中的marker。在DATACENTER中仍然会在角色陷入地面的情况下强行将角色的坐标回拉到地面高度，
 //但这样之后，应该是在战斗的进行过程中，撑地collider在各种抖动后会再次起到支撑地面的作用，这时候其与地面的摩擦力就会起到真正的作用。
 //如此一来原先的武器攻击位置校准，角色互推，以及防御状态震动调位的功能都已经重新打开。
 //GM攻击状态下，原本有一个把collider给trigger化的机制，这个我们给删掉了。因为这不利于生动的让角色的冲击被阻挡（这个事情有些微妙）

 //另外还有了个比较大的点：我们尽量在创建角色时候让角色手脚的攻击球附近也有角色自身的collider，这是因为在一些类似电钻一样的武功发动时候能尽可能的给对方一些推动作用，
 //否则角色总是会和对方互相重合，非常违和。

 //另外，我们决定Shield全部都是非trigger，一来因为这个shield也应该起一个和角色的隔绝作用，尤其是GM攻击，
 //再一个，这个盾牌既然看的见，就应该和角色互推。而我们的互推机制靠的是onentertrigger，并没有写ontriggerenter。

 //GM攻击下触碰Shield仍然会停止。因为root animation实在是很难靠collider接触给拦下来。一不小心就会穿越盾牌，攻击球在不接触盾牌情况下打到敌人（再来个cleartarget，敌人就伤着了）
 //于是此企划保留。