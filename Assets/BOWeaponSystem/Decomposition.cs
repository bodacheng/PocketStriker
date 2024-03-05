using UnityEngine;
using System.Collections.Generic;
using HittingDetection;
using UnityEngine.Animations;

public partial class Decomposition : MonoBehaviour {

    public HitBoxManager _HitBox;
    public TrackControl TrackControl;
    
    [Tooltip("物体实际销毁时间，一定要大于stop_emission_delay")]
    [SerializeField] float DestructionDelay = 1.1f;//上面的值必须要大于下面的值
    [Tooltip("武器实际失效时间，也是特效停止生成时间")]
    [SerializeField] float stop_emission_delay = 0.9f;
    
    public List<MeshRenderer> to_be_faded_renderers;

    [Tooltip("附属物体。这个只能自己把握。")]
    public string[] Attachments;

    #region realtime
    DecompositionPool pool;
    PositionConstraint positionConstraint;
    BO_Ani_E BO_Ani_E;
    ParticleSystem to_be_stop_emissions;
    public float Counter;
    public int Phase { get; set; }
    public bool IsWeapon { get; set; }
    bool hasParticle { get; set; }
    #endregion

    public AudioSource AudioSource
    {
        get;
        set;
    }

    void Awake()
    {
        to_be_stop_emissions = gameObject.GetComponent<ParticleSystem>();
        hasParticle = to_be_stop_emissions != null;
    }
    
    public void SetPool(DecompositionPool pool)
    {
        this.pool = pool;
    }

    public void SetPositionConstraint(PositionConstraint positionConstraint)
    {
        this.positionConstraint = positionConstraint;
    }
    
    public PositionConstraint GetPositionConstraint()
    {
        return positionConstraint;
    }
    
    public void SetBOAniE(BO_Ani_E _Ani_E)
    {
        BO_Ani_E = _Ani_E;
    }
    
    public BO_Ani_E GetBOAniE()
    {
        return BO_Ani_E;
    }
    
    // Local_OnEnable和Local_OnDisable，最大的一个区别是，
    // 前者没有打开marker的处理，marker的开启由各个与攻击相关的模块自行处理，因为在那之前涉及一些不太统一的参数设置
    // 而Local_OnDisable进行了关闭marker的处理（目前好像就干了这一件事）
    
    // 本函数只有在Decomposition作为对象池内物体向外取的时候才会在取出时被执行，否则不会主动执行，从而会由于Phase没有置于1而一直不消逝。
    // 如果Decomposition不基于对象池构建来生成，比如由addressable中直接创建instance，必须主动运行Local_OnEnable()
    public void OnEnableProcess()
    {
        Phase = 1;
        if (DestructionDelay >= 0)
        {
            Counter = 0;
        }
        if (IsWeapon)
            _HitBox.CurrentHP = _HitBox.weaponHP;
        if (hasParticle)
            to_be_stop_emissions.Play(true);
        
        if (AudioSource != null)
            AudioSource.Play();
        
        SetMaterialsAlpha(1f);
    }
    
    void Update()
    {
        HitBoxesProcesser.AddToDecompositionProcessorList(this);
    }

    void EnergyResolve()
    {
        CloseMarkers();
        StopEmissions(false);
        if (pool == null)
        {
            Debug.Log("严重逻辑问题"+ gameObject.name);
            Destroy(gameObject);
            return;
        }
        pool.Return(this);
    }
    
    void CloseMarkers()
    {
        if (IsWeapon)
        {
            _HitBox.Local_OnDisable();
            _HitBox.SetOwnerFACR(null);
        }
    }
    
    public void Step1()
    {
        if (Phase == 1)
        {
            if (IsWeapon)
                _HitBox.LocalUpdate();
        }
    }
    public void Step2()
    {
        if (Phase == 1)
        {
            if (IsWeapon)
                _HitBox.LocalLateUpdate();
        }
    }

    public void StopEmissions(bool clearParticles)
    {
        if (hasParticle)
        {
            if (to_be_stop_emissions.isPlaying)
            {
                if (clearParticles)
                    to_be_stop_emissions.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else
                    to_be_stop_emissions.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    public void SetMaterialsAlpha(float a)
    {
        if (to_be_faded_renderers.Count == 0)
            return;
        
        for (int i = 0; i < to_be_faded_renderers.Count; i++)
        {
            for (int y = 0; y < to_be_faded_renderers[i].materials.Length; y++)
            {
                to_be_faded_renderers[i].materials[y].SetColor("_TintColor", new Color(to_be_faded_renderers[i].materials[y] .GetColor("_TintColor").r,to_be_faded_renderers[i].materials[y] .GetColor("_TintColor").g, to_be_faded_renderers[i].materials[y] .GetColor("_TintColor").b, a));
            }
        }
    }
    
    public void SpecialTriggerEvent(string defined_event_code, HitBoxSubEventManger hitBoxSubEventManger)//这个就只能在这自定义了
    {
        if (BO_Ani_E == null)
            return;
        switch (defined_event_code)
        {
            case "expolosion":
                BO_Ani_E.hiddenMethods.BlastAttack_core(transform.position, transform.rotation, null, 2, _HitBox.GeneratedByStateKey);
                break;
            case "boltpForward":
                BO_Ani_E.hiddenMethods.MagicForward_core("boltp", hitBoxSubEventManger.transform.position, hitBoxSubEventManger.transform.rotation, 3, _HitBox.GeneratedByStateKey);
                break;
            case "c_l_bullet":
                BO_Ani_E.hiddenMethods.MagicForward_core("c_l_bullet", hitBoxSubEventManger.transform.position, hitBoxSubEventManger.transform.rotation, 3, _HitBox.GeneratedByStateKey);
                break;
            case "c_r_bullet":
                BO_Ani_E.hiddenMethods.MagicForward_core("c_r_bullet", hitBoxSubEventManger.transform.position, hitBoxSubEventManger.transform.rotation, 3, _HitBox.GeneratedByStateKey);
                break;
            case "bulletForward":
                BO_Ani_E.hiddenMethods.Bullet_shoot_from_Core(hitBoxSubEventManger.transform.position,hitBoxSubEventManger.transform.rotation, 1, 10, _HitBox.GeneratedByStateKey);
                break;
            case "bulletForward3":
                BO_Ani_E.hiddenMethods.Bullet_shoot_from_Core(hitBoxSubEventManger.transform.position,hitBoxSubEventManger.transform.rotation, 3, 10, _HitBox.GeneratedByStateKey);
                break;
            case "groundroundblast":
                BO_Ani_E.hiddenMethods.MagicForward_core("groundroundblast", hitBoxSubEventManger.transform.position,hitBoxSubEventManger.transform.rotation,0,_HitBox.GeneratedByStateKey);
                break;
            case "explosionlighteningball_big":
                BO_Ani_E.hiddenMethods.MagicForward_core("explosionlighteningball_big", hitBoxSubEventManger.transform.position,hitBoxSubEventManger.transform.rotation, 2, _HitBox.GeneratedByStateKey);
                Phase = -1;
                break;
            case "s_pillarblast":
                BO_Ani_E.hiddenMethods.MagicForward_core("s_pillarblast", hitBoxSubEventManger.transform.position, hitBoxSubEventManger.transform.rotation, 0, _HitBox.GeneratedByStateKey);
                Phase = -1;
                break;
            case "lightningspray":
                BO_Ani_E.hiddenMethods.MagicForward_core("lightningspray", hitBoxSubEventManger.transform.position, hitBoxSubEventManger.transform.rotation, 0, _HitBox.GeneratedByStateKey);
                Phase = -1;
                break;
        }
    }
}
