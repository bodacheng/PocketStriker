using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "FightGlobalSetting", menuName = "ScriptableObjects/FightGlobalSetting", order = 1)]
public class FightGlobalSetting : ScriptableObject
{
    [SerializeField] bool hasDefend;
    [SerializeField] bool skillStoneHasExp = false;
    [SerializeField] AnimationCurve knockOffYAnimationCurve;
    [SerializeField] AnimationCurve knockOffZAnimationCurve;
    [SerializeField] AnimationCurve hDamageYAnimationCurve;
    [SerializeField] AnimationCurve hDamageZAnimationCurve;
    [SerializeField] int fighterRigidMass = 1000;
    [SerializeField] int onTouchEnemyBodyRigidDrag = 1000;
    [SerializeField] float fighterMoveSpeed = 10;
    [SerializeField] float closeDis = 2;
    [SerializeField] float AT_coefficient = 1;
    [SerializeField] float HP_coefficient = 1;
    [SerializeField] bool Team1Invincible = false;
    [SerializeField] int NormalSkillExGet = 30;
    [SerializeField] int Sp1SkillExGet = 0;
    [SerializeField] int Sp2SkillExGet = 0;
    [SerializeField] int Sp3SkillExGet = 0;
    [SerializeField] int getHurtExGet = 10;
    [SerializeField] float slightHitLastingTime = 0.2f, lightHitLastingTime = 0.3f, heavyHitLastingTime = 0.6f, superHitLastingTime = 1f, highHitLastingTime = 0.8f;
    [SerializeField] float hurtFreezeInDuration = 0.01f;
    [SerializeField] float hurtFreezeOutDuration = 0.2f;
    [SerializeField] float normalAttackPosFixingTime = 0.1f;
    [SerializeField] float knockOffExtent = 20f;
    [SerializeField] float MaxKnockoffLaidGroundTime = 2f;
    [SerializeField] float CanGetUpAfterKnockoffToGround = 0.5f;
    [SerializeField] float GetupTime = 1f;
    [SerializeField] float SureToPushForwardDis = 5f;
    [SerializeField] int defendHP = 20;
    [SerializeField] float lightBlockLastingTime = 0.3f, heavyBlockLastingTime = 0.5f;
    [SerializeField] float attackDrawingDistance = 1f;
    [SerializeField] int resistanceMax = 10;
    [SerializeField] float toEnemyNearestDis = 1;
    [SerializeField] int eXMax = 120;
    [SerializeField] int dreamComboGaugeMax = 180;
    [SerializeField] float dreamComboSpeed = 1.7f;
    [SerializeField] float dreamComboResistTime = 0.5f;
    [SerializeField] int dreamComboResistUpCount = 5;
    [SerializeField] int player1DreamComboAIRateNumM;//玩家队伍共斗模式下非控制队员DreamCombo触发率数字。
    [SerializeField] int arenaEnemyDreamComboAIRate;
    [SerializeField] int energyResolveAfterExtendBoundary = 5;
    [SerializeField] PhysicMaterial _physicMaterial;

    static string fightParamKey = "Config/fight_params";
    public static int SceneStep;//0 :mainmenu 1: fightscene
    public static bool HasDefend;
    public static bool SkillStoneHasExp;
    public static int FighterRigidMass = 1000;
    public static int OnTouchEnemyBodyRigidDrag = 1000;
    public static float _fighterMoveSpeed;
    static float AtCoefficient = 1;
    static float HpCoefficient = 1;
    public static bool _Team1Invincible;
    public static int _NormalSkillExGet;
    public static int _Sp1SkillExGet;
    public static int _Sp2SkillExGet;
    public static int _Sp3SkillExGet;
    public static int _getHurtExGet;
    public static float SlightHitLastingTime, LightHitLastingTime, HeavyHitLastingTime, SuperHitLastingTime;
    public static float HurtFreezeInDuration;
    public static float HurtFreezeOutDuration;
    public static float KnockOffExtent;
    public static float _MaxKnockoffLaidGroundTime;
    public static float _CanGetUpAfterKnockoffToGround;
    public static float _GetupTime;
    public static float _SureToPushForwardDis = 5f;
    public static float _closeDis;
    public static float LightBlockLastingTime, HeavyBlockLastingTime, HighHitLastingTime;
    public static float NormalAttackPosFixingTime;
    public static float _dreamComboResistTime;
    public static int _dreamComboResistUpCount;
    public static int _player1DreamComboAIRateNumM;
    public static int ArenaEnemyDreamComboAIRate;
    public static AnimationCurve KnockOffYAnimationCurve, KnockOffZAnimationCurve;
    public static AnimationCurve HDamageYAnimationCurve;
    public static AnimationCurve HDamageZAnimationCurve;
    public static float _attackDrawingDistance;
    public static int _ResistanceMax = 120;
    public static float ToEnemyNearestDis = 1;
    public static int _EXMax;
    public static int _DreamComboGaugeMax;
    public static float DreamComboSpeed;
    public static bool HitBoxLogger = true;
    public static int _defendHP;
    public static int _energyResolveAfterExtendBoundary;
    public static PhysicMaterial PhysicMaterial;
    
    public void Initialise()
    {
        HasDefend = hasDefend;
        SkillStoneHasExp = skillStoneHasExp;

        _closeDis = closeDis;
        _fighterMoveSpeed = fighterMoveSpeed;
        AtCoefficient = AT_coefficient;
        HpCoefficient = HP_coefficient;
        FighterRigidMass = fighterRigidMass;
        OnTouchEnemyBodyRigidDrag = onTouchEnemyBodyRigidDrag;
        _Team1Invincible = Team1Invincible;
        
        _NormalSkillExGet = NormalSkillExGet;
        _Sp1SkillExGet = Sp1SkillExGet;
        _Sp2SkillExGet = Sp2SkillExGet;
        _Sp3SkillExGet = Sp3SkillExGet;
        _getHurtExGet = getHurtExGet;
        
        SlightHitLastingTime = slightHitLastingTime;
        LightHitLastingTime = lightHitLastingTime;
        HeavyHitLastingTime = heavyHitLastingTime;
        SuperHitLastingTime = superHitLastingTime;
        HighHitLastingTime = highHitLastingTime;
        HurtFreezeInDuration = hurtFreezeInDuration;
        HurtFreezeOutDuration = hurtFreezeOutDuration;
        NormalAttackPosFixingTime = normalAttackPosFixingTime;
        
        KnockOffExtent = knockOffExtent;
        _CanGetUpAfterKnockoffToGround = CanGetUpAfterKnockoffToGround;
        _MaxKnockoffLaidGroundTime = MaxKnockoffLaidGroundTime;
        _GetupTime = GetupTime;
        
        KnockOffYAnimationCurve = knockOffYAnimationCurve;
        KnockOffZAnimationCurve = knockOffZAnimationCurve;

        HDamageYAnimationCurve = hDamageYAnimationCurve;
        HDamageZAnimationCurve = hDamageZAnimationCurve;

        _SureToPushForwardDis = SureToPushForwardDis;

        _defendHP = defendHP;
        LightBlockLastingTime = lightBlockLastingTime;
        HeavyBlockLastingTime = heavyBlockLastingTime;

        _attackDrawingDistance = attackDrawingDistance;
        _dreamComboResistTime = dreamComboResistTime;
        _dreamComboResistUpCount = dreamComboResistUpCount;
        ToEnemyNearestDis = toEnemyNearestDis;
        
        _ResistanceMax = resistanceMax;
        _EXMax = eXMax;
        _DreamComboGaugeMax = dreamComboGaugeMax;
        DreamComboSpeed = dreamComboSpeed;
        
        PhysicMaterial = _physicMaterial;
        _energyResolveAfterExtendBoundary = energyResolveAfterExtendBoundary;
        
        _player1DreamComboAIRateNumM = player1DreamComboAIRateNumM;
        ArenaEnemyDreamComboAIRate = arenaEnemyDreamComboAIRate;
    }
    
    // 900血，10攻击力，1打1的话接近40秒左右游戏结束。但如果存在大量远距离对火立回那么就不太好说这个时间。。
    // 那么level 是1的情况下，攻击力是1
    // 从而在技能定义表里，技能标准攻击值应该是1，存在超迅速多连击的情况多半应该少于1，而一些比较赌的重攻击则是大于1
    // HP和攻击力等比缩放。攻击是从1涨到10，HP是从10涨到100        
    // 1级的基准伤害
    // n：1   
    // ex1 ： 2
    // Ex2  ： 4
    // Ex3 ：  6
    // 100级的基准伤害
    // n ：10
    // ex1 ：20
    // Ex2 ：40
    // Ex3 ：60
    public static float ATCal(float originAT, float level)
    {
        return AtCoefficient * originAT * level;
    }
    public static float StoneHpCal(float originHP, float level)
    {
        return HpCoefficient * originHP * level;
    }
    
    public static string EffectPathDefine(Element element = Element.Null)
    {
        string personalEffectPath;
        switch (element)
        {
            case Element.blueMagic:
                personalEffectPath = "bluemagic";
                break;
            case Element.redMagic:
                personalEffectPath = "redmagic";
                break;
            case Element.greenMagic:
                personalEffectPath = "greenmagic";
                break;
            case Element.lightMagic:
                personalEffectPath = "lightmagic";
                break;
            case Element.darkMagic:
                personalEffectPath = "darkmagic";
                break;
            case Element.Null:
                personalEffectPath = "defaultmagic";
                break;
            default:
                personalEffectPath = "defaultmagic";
                break;
        }
        return personalEffectPath;
    }

    public static async UniTask LoadFightParams()
    {
        var data = await AddressablesLogic.LoadT<FightGlobalSetting>(fightParamKey);
        data.Initialise();
    }
}
