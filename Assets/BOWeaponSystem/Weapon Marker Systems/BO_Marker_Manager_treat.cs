using UnityEngine;

namespace HittingDetection
{
    public partial class HitBoxManager : MonoBehaviour
    {
        // 下面这个结构目前为止事关三大重要的索引作用
        // 1. 武器在击中敌人时，作为攻击力参考
        // 2. 武器击中敌人时，特效种类参考
        // 3. 武器击中敌人时，对攻击方的hit combo进行加算
        // 由于BO_Marker_Manager现在全部都是对象池物件，如果我们认为一个instance返回对象池后就应该不再参与任何工作的话，
        // 原则上我们应该确保一切围绕BO_Marker_Manage的instance，最重要的是里面的myOwnerHealth进行的工作在instance返回对象池前结束
        
        float _ContinuousDamage_Timer;
        //These DH and DS variables are Distances to the shield spots. Whie the shield is active, DH ("Distance to Health", distance to the back point of the shiled) has to be less than all the other shield edge spots (DS, "Distance to Shield")
        float dh;
        float ds1;
        float ds2;
        float ds3;
        float ds4;
        float ds5;
        float ds6;
        float ds7;
        float ds8;
        float ds9;
        
        void TreatProcess()
        {
            if (HitShield && _traditionalDefendMode)//其实由上面的分析可以知道，对于来自一把武器的攻击，hitshield和hitflesh是不会同时为true的。但如果多把武器同时来攻击，如果被攻击方同时有被击中以及防御住的情况发生，肯定要先处理所受伤害，立刻转入受伤状态才对
            {
                for (int i1 = 0; i1 < _shieldsHit.Count; i1++)
                {
                    if (!_usedTargets.Contains(_shieldsHit[i1])) //无论对墙壁，盾牌，还是伤害对象，每一轮攻击只会造成一次影响
                    {
                        //collision = Attack_And_Shield_Specification.Instance.Attack_On_Shield_Cal(damage_type, TheS.damage_type);
                        //_MyOwnerCalReference._Center._BasicPhysicSupport.hiddenMethods.ITouchedThisCollider(1);
                        //switch (collision.on_weapon_holder)
                        //{
                        //    case DamageType.stagger:
                        //            V_Damage new_damage = new V_Damage(this,null,_MyOwnerCalReference,TheS._ownerFightAttriCalReference, _Shields_Hit[i1].position, Quaternion.LookRotation(_Shields_Hit[i1].position - TheS._ShieldBackSpot.transform.position));
                        //            _MyOwnerCalReference.ApplyDamage(new_damage);
                        //        break;
                        //    case DamageType.none:
                        //        break;
                        //}
                        
                        ////在此向防御方发送防御信号
                        //if (TheS._ownerFightAttriCalReference != null)
                        //{
                        //    switch (collision.on_shield_holder)
                        //    {
                        //        case DamageType.light_block:
                        //            V_Damage new_damage = new V_Damage(this,null,TheS._ownerFightAttriCalReference, _MyOwnerCalReference,_WeaponHolderCenter.position, Quaternion.LookRotation(_Shields_Hit[i1].position - TheS._ShieldBackSpot.transform.position));
                        //            TheS.PlusHP(-1);
                        //            TheS._ownerFightAttriCalReference.ApplyDamage(new_damage);
                        //            break;
                        //        case DamageType.heavy_block:
                        //                new_damage = new V_Damage(this, null, TheS._ownerFightAttriCalReference, _MyOwnerCalReference, _WeaponHolderCenter.position, Quaternion.LookRotation(_Shields_Hit[i1].position - TheS._ShieldBackSpot.transform.position));
                        //            TheS.PlusHP(-2);
                        //            TheS._ownerFightAttriCalReference.ApplyDamage(new_damage);
                        //            break;
                        //        case DamageType.supper_damage:
                        //                new_damage = new V_Damage(this, null, TheS._ownerFightAttriCalReference, _MyOwnerCalReference, _WeaponHolderCenter.position, Quaternion.LookRotation(_Shields_Hit[i1].position - TheS._ShieldBackSpot.transform.position));
                        //            TheS._ownerFightAttriCalReference.ApplyDamage(new_damage);
                        //            break;
                        //        case DamageType.none:
                        //            break;
                        //    }
                        //}
                        _usedTargets.Add(_shieldsHit[i1]);
                    }
                }
            }

            if (HitFlesh)
            {
                foreach (V_Damage _hitOnHealthBody in hitsOnHealthBody)
                {
                    _hitOnHealthBody.victim.ApplyDamage(_hitOnHealthBody);
                    _hitOnHealthBody.attacker.MyDamageCount(_hitOnHealthBody);
                }
            }

            if (ContinuousDamage)
            {
                _ContinuousDamage_Timer += Time.deltaTime;
                if (_ContinuousDamage_Timer >= ContinuousDamageInterval)
                {
                    ClearTargets();
                    _ContinuousDamage_Timer = 0;
                }
            }

            for (var i = 0; i < _weaponEnergyExhaustMissions.Count; i++)
            {
                _weaponEnergyExhaustMissions[i].Invoke();               
            }
            _weaponEnergyExhaustMissions.Clear();
        }
    }
}