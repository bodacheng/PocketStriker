using System;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using HittingDetection;
using Log;

public partial class FightParamsReference
{
    public Data_Center Center;
    
    public readonly ComboHitCount _comboHitCount = new ComboHitCount();
    readonly KnockOffCount _knockOffCount = new KnockOffCount();
    readonly BeHitCount _beHitCount = new BeHitCount();
    readonly List<BO_Limb> _myLimbs = new List<BO_Limb>();
    readonly List<V_Damage> _causeDamages = new List<V_Damage>();
    readonly List<Collider> _myColliders = new List<Collider>();
    readonly List<E_Damage> _eventDamageList = new List<E_Damage>();
    readonly List<E_Damage> _eventAttackSuccessList = new List<E_Damage>();
    
    // [Tooltip("与健康体同级的那个collider作不作为伤害判断?")]
    // public bool collider_on_health = false; //固定值 虽然这个值本身没有在本脚本中进行任何计算，但由于BO_Health会频繁访问BO_Health，所以如果需要这样一个参数，放在这里仍然合适
    
    public ReactiveProperty<bool> IsDead { get; set; } = new ReactiveProperty<bool>(false);
    public ReactiveProperty<float> CurrentHp { get; set; } = new ReactiveProperty<float>();
    private readonly ReactiveProperty<int> _resistance = new ReactiveProperty<int>(0);

    public ReactiveProperty<int> Resistance
    {
        get => _resistance;
        set => _resistance.Value = value.Value >= 0 ? value.Value : 0;
    }
    
    public float AT { get; set; }
    public CriticalGaugeMode CriticalGaugeMode{ get; set; }
    public bool Invincible { get; set; }
    public bool GettingDamage { get; set; }
    
    public void HealthBodyFixedUpdate()
    {
        _knockOffCount.Update();
        _comboHitCount.Update();
        _beHitCount.Update();
    }

    public KnockOffCount GetKnockOffCount()
    {
        return _knockOffCount;
    }

    public void RegisterLimb(BO_Limb limb)
    {
        if (!_myLimbs.Contains(limb))
        {
            _myLimbs.Add(limb);
        }
    }
    
    public void EnableAllLimbs(bool on)
    {
        foreach (var limb in _myLimbs)
        {
            if (limb.myColliderMustEquip != null)
                limb.myColliderMustEquip.isTrigger = !on;
        }
    }
        
    public void ChangeLayerForLimbs(int layer)
    {
        for (var i = 0; i < _myLimbs.Count; i++)
        {
            var limb = _myLimbs[i];
            limb.gameObject.layer = layer;
        }
        Center.geometryCenter.gameObject.layer = layer;
    }
    
    public bool IfMyBody(Collider collider) => _myColliders.Contains(collider);
    
    public void FindAllSelfCollidersAndIgnoreCollision()
    {
        _myColliders.Clear();
        if (_myLimbs != null)
        {
            foreach (var limb in _myLimbs)
            {
                limb.INI();
                if (limb.myColliderMustEquip != null && !_myColliders.Contains(limb.myColliderMustEquip))
                {
                    limb.myColliderMustEquip.material = FightGlobalSetting.PhysicMaterial;
                    _myColliders.Add(limb.myColliderMustEquip);
                }
            }
        }
        if (_shield != null)
        {
            if (_shield._shieldCollider != null)           
                _myColliders.Add(_shield._shieldCollider);
        }
        for (var i = 0; i < _myColliders.Count; i++)
        {
            for (var y = i + 1; y < _myColliders.Count; y++)
            {
                Physics.IgnoreCollision(_myColliders[i], _myColliders[y]);
            }
        }
    }
    
    public void MyDamageCount(V_Damage dmg)
    {
        _causeDamages?.Add(dmg);
    }
    
    string _temp;
    void HitEffect(V_Damage damage)
    {
        if (Center.FightDataRef.Resistance.Value > 0)
        {
            EffectsManager.GenerateEffect("shield_hit", 
                FightGlobalSetting.EffectPathDefine(damage.from_weapon.element),
                damage.DamageEffectPoint,
                damage.CutRotation,
            null).Forget();
        }
        else
        {
            if (!string.IsNullOrEmpty(damage.from_weapon.hitEffect))
            {
                _temp = damage.from_weapon.hitEffect;
            }
            else
            {
                switch (damage.from_weapon.damage_type)
                {
                    case DamageType.heavy_damage_forward:
                        _temp = "heavy_hit";
                        break;
                    case DamageType.supper_damage_forward:
                        _temp = "super_hit";
                        break;
                    default:
                        _temp = "light_hit";
                        break;
                }
            }
            
            EffectsManager.GenerateEffect(_temp, FightGlobalSetting.EffectPathDefine(damage.from_weapon.element),
            damage.DamageEffectPoint,
            damage.CutRotation,
            damage.from_weapon.effectSpreadOnBody ? Center.geometryCenter : null).Forget();
        }
    }

    public class GetHitTriggerEvent
    {
        public readonly string EventKey;
        public readonly Action EventProcess;

        public GetHitTriggerEvent(string eventKey, Action eventProcess)
        {
            this.EventKey = eventKey;
            this.EventProcess = eventProcess;
        }
    }
    
    private readonly List<GetHitTriggerEvent> _getHitTriggerEvent = new List<GetHitTriggerEvent>();
    public List<GetHitTriggerEvent> GetHitTriggerEvents => _getHitTriggerEvent;

    public void AddGetHitTriggerEvent(string eventKey, Action process)
    {
        _getHitTriggerEvent.Add(new GetHitTriggerEvent(eventKey, process));
    }
    
    public void RemoveEventKey(string eventKey)
    {
        _getHitTriggerEvent.RemoveAll(x=> x.EventKey == eventKey);
    }

    // 受攻击方运行
    float _d;
    public void ApplyDamage(V_Damage dmg)
	{
        HitEffect(dmg);

        if (_getHitTriggerEvent.Count > 0)
        {
            foreach (var kv in _getHitTriggerEvent)
            {
                kv.EventProcess.Invoke();
            }
            _getHitTriggerEvent.Clear();
        }
        
        if (Center.FightDataRef.Resistance.Value > 0)
        {
            Center.FightDataRef.Resistance.Value -= 1;
            return;
        }
        
        dmg.attacker.HitCountPlus(this);
        _comboHitCount.HitCountInterrupt();
        _beHitCount.BeHitCountPlus();
        
        _d = dmg.from_weapon.GetDamageAmount();
        if (!Invincible)
            CurrentHp.Value -= _d;
        if (CurrentHp.Value <= 0)
        {
            Center._MyBehaviorRunner.ChangeState("Death", dmg);
            return;
        }
        
        Center._MyBehaviorRunner.SingleFightLog.WriteLog(
            new Soul.SingleFightLog.NegativeRecord
            {
                //被打了，属于负面效益
            }
        );
        Center._MyBehaviorRunner.SingleFightLog.AnalysisLog(Center._MyBehaviorRunner.ConditionAndRespondPriority);
        Center._MyBehaviorRunner.ChangeState("Hit", dmg);
        dmg.from_weapon.HitBoxLifeEnding = HitBoxLifeEnding.successed;
    }
    
    // 打别人计数
    void HitCountPlus(FightParamsReference victim)
    {
        _comboHitCount.HitCountPlus(_beHitCount);
        Center._MyBehaviorRunner.SingleFightLog.WriteLog(
            new Soul.SingleFightLog.PositiveRecord
            {
                //打了别人，属于正面效益
            }
        );
        Center._MyBehaviorRunner.SingleFightLog.AnalysisLog(Center._MyBehaviorRunner.ConditionAndRespondPriority);
        Center._MyBehaviorRunner.GetNowState().EnergyAbsorb(CriticalGaugeMode, victim);
    }
    
    public int GetBeHitCount() => _beHitCount.GetBeHitCount(); //自己被揍计数
    
    public void DreamComboStart()
    {
        UnityEngine.Events.UnityAction eventStart = () =>
        {
            Center._ShaderManager.RimEffectsUp(CommonSetting.DreamColor, 0.2f);
            Resistance.Value += FightGlobalSetting._dreamComboResistUpCount;
        };
        UnityEngine.Events.UnityAction eventEnd = () =>
        {
            Resistance.Value -= FightGlobalSetting._dreamComboResistUpCount;
            Center._ShaderManager.RimEffectsClear(0.2f);
        };
        CustomCoroutine eventCoroutine = new CustomCoroutine(eventStart, FightGlobalSetting._dreamComboResistTime,
            () => Center._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.Hit, eventEnd);
        Center.buffsRunner.RunSubCoroutineOfState(eventCoroutine);
    }

    E_Damage _managingEDamage;
    // event 攻击系列。暂时不再使用
    public void SetManagingEventDamage(E_Damage e)
    {
        _managingEDamage = e;
    }
    public E_Damage GetManagingEventDamage()
    {
        return _managingEDamage;
    }
    public void EventAttackHitApprove(E_Damage e)
    {
        _eventAttackSuccessList.Add(e);
    }
    public void AddEventDamageList(E_Damage e)
    {
        e.SetDamagedHealthBody(this);
        _eventDamageList.Add(e);
    }
    public List<E_Damage> ReturnEventDamageList()
    {
        return _eventDamageList;
    }
    public List<E_Damage> ReturnApprovedEventAttackAttempts()
    {
        return _eventAttackSuccessList;
    }
    
    Color _damageColor;
    public void RunShaderChangeProcess(Element element, float time)
    {
        if (Center._ShaderManager != null)
        {
            switch (element)
            {
                case Element.redMagic:
                    _damageColor = Color.red;
                    break;
                case Element.blueMagic:
                    _damageColor = Color.blue;
                    break;
                case Element.greenMagic:
                    _damageColor = Color.green;
                    break;
                case Element.darkMagic:
                    _damageColor = new Color(0.8f, 0f, 1f);
                    break;
                case Element.lightMagic:
                    _damageColor = new Color(1f, 1f, 0f);
                    break;
                default:
                    _damageColor = Color.white;
                    break;
            }
            _damageColor.a = 0.5f;
            Center._ShaderManager.RimEffectsForAShortTime(_damageColor, time);
        }
    }
    
    // 旧防御盾系列函数。已经基本不用
    BO_Shield _shield;
    public void SetShield(BO_Shield shield)
    {
        var fightParamsReference = this;
        fightParamsReference._shield = shield;
    }
    public BO_Shield GetShield()
    {
        return _shield;
    }
}