using UnityEngine;
using System.Collections.Generic;
using HittingDetection;

public class BO_Shield : MonoBehaviour {

	//there are animation events on Enable shield collider and disable shield collider. Info them out!
	[Space(10)]
	[Tooltip("盾牌单次寿命")]
	public int _HP = 50;

    [Tooltip("An option for more accurate shield detection on Humanoid targets. If in the same frame both a Shield and an Enemy were hit (which may happen at REALY high speeds), ticking this option True will provide additional check to see what was closer to the Attack orign - the shield or the Enemy. It's a good idea to have it turned ON by default, but it's not mandatory. The only situations when this option is not adviced is when your shield wielder is enormously big. Like, a building-big. (though it's not a rule and it may still work fine).")]
    public bool _AdvancedShieldDetection = true;

	[Tooltip("Before we start, if you plan on using this Shield with a character or object equiped with the BS_Main_Health system, please, assign it here. Otherwise the hit detection may not work properly")]
    public FightParamsReference OwnerFightParamsReference; //it's referenced in other scripts.

	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. It is a point behind the center of the shield (from the safe side, behind the shield's collider (so it's simply a point near the center of the Shield's Wielder, if it's humantoid, for scale). It's used to calculate if the attack was coming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield and place it accordingly around the shield. Then put it's reference into this variable. [INFO 2:] If you're experiencing that the shild is being hit though the attack was clearly coming from the back (like if the shield is quite big or the attack swipe is large), don't be affraid to pull this spot a bit further behind the shield wielder")]
	public Transform _ShieldBackSpot;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. It is a point around the very center of the shield (can be a tiny bit in front of the shield's collider (so from the Attacker side). It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldCenterSpot;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot1;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot2;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot3;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot4;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot5;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot6;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot7;
	[Tooltip("This fancy named GameObject is used in Advanced Shield Detection. These edge spots should be placed around the edges of your shields - will it be a simple rectangular box or any custom shape, these spots should entwine the collider. It's used to calculate if the attack was comming from the front or the back of the shield. [INFO:] Simply create an empty GameObject, set it as a child of your shield (or even better - the character that has this shield!) and place it accordingly around the shield or the character. Then put it's reference into this variable. If your Shield is not a square (so it has any custom shape), don't be affraid to place these Spots around it's edges in any different pattern - it will still work!")]
	public Transform _ShieldEdgeSpot8;

	[Space(10)]
	[Tooltip("With an Animation Event from the BS_Weapon_Animation_Events system you can call a function to turn on and off the Shields colliders. You can use the DisableShieldCollider() and EnableShieldCollider() Animation Events of the earlier mentioned system! Read more about it in the ReadMe File.")]
	public Collider _shieldCollider;

	[Tooltip("Disable shield when dying?")]
	public bool DisableShieldOnDeath;

    [Tooltip("属性")]
    public Element element;

    int _hpCounter;
    DecompositionPool _hitSparks, shieldBreakSpark;
    string personalEffectPath;

    void Awake()
    {
        personalEffectPath = FightGlobalSetting.EffectPathDefine(element);
    }

    public void PlusHP(int plus)
    {
        _hpCounter += plus;
    }

    void Update()
    {
        if (_hpCounter < 0)
        {
            ShieldBreak();
        }
    }

    Decomposition shieldbreaking;
    async void ShieldBreak()
    {
        if (this._ShieldCenterSpot != null)
        {
            if (shieldBreakSpark == null)
            {
                shieldBreakSpark = await EffectsManager.IniEffectsPool("onEnableShieldSpark", personalEffectPath, 3);
            }
            if (shieldBreakSpark != null)
            {
                shieldbreaking = shieldBreakSpark.Rent();
                shieldbreaking.transform.position = _ShieldCenterSpot.position;
                shieldbreaking.transform.rotation = Quaternion.identity;
                shieldbreaking.transform.LookAt(_ShieldCenterSpot.position - _ShieldBackSpot.position);
            }
        }
        if (OwnerFightParamsReference != null)
        {
            // 下面这些都是瞎写的
            //_ownerFightAttriCalReference.ApplyDamage(new V_Damage(0,
            //                                                    _ownerFightAttriCalReference, null,
            //                                                    DamageType.heavy_damage, WeaponPosAdjustMode.explosion, WeaponMode.EnergyFromBodyWeapon,SpecialApply.none,
            //                                                    _ShieldCenterSpot.position, _ShieldCenterSpot.rotation,
            //                                                    Vector3.zero, transform.position,Vector3.zero,
            //                                                    "defaultmagic",false));
        }
    }

    public void IniShield(TeamConfig _TeamConfig,FightParamsReference bO_Health)
    {
        if (_TeamConfig !=null)
            this.gameObject.layer = _TeamConfig.myShieldLayer;
        this.OwnerFightParamsReference = bO_Health;
        bO_Health.SetShield(this);
    }

    public async void PassHitPointsFromWeaponToShiled(List<Vector3> _ShiledHitPositions)
    {
        if (_hitSparks == null)
            _hitSparks = await EffectsManager.IniEffectsPool("shield_hit", personalEffectPath, 3);
        
        if (_hitSparks != null)
        {
            for (int i3 = 0; i3 < _ShiledHitPositions.Count; i3++)
            {
                shieldbreaking = _hitSparks.Rent();
                shieldbreaking.transform.position = this._ShieldCenterSpot.position;
                shieldbreaking.transform.rotation = Quaternion.identity;
                //_missSparksShield.transform.LookAt(2 * _missSparksShield.transform.position - _ShieldBackSpot.position);
            }
        }
    }

	void OnEnable()
	{
        this._hpCounter = _HP;
    }
}
