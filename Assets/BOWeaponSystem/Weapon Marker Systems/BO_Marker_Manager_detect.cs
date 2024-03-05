using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Log;
using UnityEngine.Events;

namespace HittingDetection
{
    public partial class HitBoxManager : MonoBehaviour
    {
        FightParamsReference _Raw_Target_Instance;//A single target which was hit.
        BO_Limb _boHitBox;
        Vector3 _TrailModeStartPoint;
        IDictionary<Collider, HitPointPara> _ballDetectHitPool;

        void DetectProcess()
        {
            hitsOnHealthBody.Clear();
            for (int i = 0; i < _markers.Count; i++)
            {
                if (_markers[i].HitCheck())
                {
                    // 射线检测目前不具备根据伤害等级大小修正自身能量消耗的能力
                    if (_markers[i] is Trail_Marker)
                    {
                        RaycastHit[] _hits = ((Trail_Marker)_markers[i])._hits;
                        if (_traditionalDefendMode)
                        {
                            for (int hit_target_index = 0; hit_target_index < _hits.Length; hit_target_index++)
                            {
                                if (_markers[i].enemyShieldLayer == (_markers[i].enemyShieldLayer | 1 << _hits[hit_target_index].collider.gameObject.layer) && !_shieldsHit.Contains(_hits[hit_target_index].collider.transform))
                                {
                                    BO_Shield TheS = _hits[hit_target_index].collider.gameObject.GetComponent<BO_Shield>();
                                    if (TheS == null || TheS.OwnerFightParamsReference == null)
                                    {
                                        Debug.Log("防御盾构造严重错误");
                                        break;
                                    }
                                    if (_shieldsHit.Contains(TheS.transform) == false // 本帧之内只要有武器上的一个mark打中了盾牌，那不再考虑其他mark是否打中盾牌
                                        && _usedTargets.Contains(TheS.transform) == false //used_target只在一轮攻击后才清空，所以这里的意思应该是：如果打中的这个盾牌物体在这一轮里已经起过一次作用，那就不再研究。
                                        && _usedTargets.Contains(TheS.OwnerFightParamsReference.Center.geometryCenter) == false) //所打中的盾牌对应的肉体已经在本轮攻击起过一次作用，那也不再详细计算 一把武器一轮enablemarkers和disablemarkers之间只可能对一个敌人进行一次伤害或进行一次“被防御”，敌人不可能在一把武器的一轮攻击期间内既受伤一次又防御成功一次
                                    {
                                        if (TheS._AdvancedShieldDetection)
                                        {
                                            dh = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldBackSpot.transform.position), 2);//这第二个参数也就是被攻击方肉体的transform
                                            ds1 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldCenterSpot.transform.position), 2);  //center
                                            ds2 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot1.transform.position), 2);  //Top
                                            ds3 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot2.transform.position), 2);  //Top Left
                                            ds4 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot3.transform.position), 2);  //Top Right
                                            ds5 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot4.transform.position), 2);  //Bottom
                                            ds6 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot5.transform.position), 2);  //Bottom Left
                                            ds7 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot6.transform.position), 2);  //Bottom Right
                                            ds8 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot7.transform.position), 2);  //Right
                                            ds9 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot8.transform.position), 2);  //Left
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldBackSpot.transform.position - _WeaponHolderCenter.position, Color.green, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldCenterSpot.transform.position - _WeaponHolderCenter.position, Color.red, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot7.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot1.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot2.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot3.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot4.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot6.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot5.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                           //Debug.DrawRay(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot8.transform.position - _WeaponHolderCenter.position, Color.blue, 5);
                                        }
                                        if (((dh > ds1) || (dh > ds2) || (dh > ds3) || (dh > ds4) || (dh > ds5) || (dh > ds6) || (dh > ds7) || (dh > ds8) || (dh > ds9)) || (TheS._AdvancedShieldDetection == false))
                                        {
                                            //飞行道具一律不需要开启_AdvancedShieldDetection功能，因为这个功能本身针对的就是近距离一方对另一方发起大幅度动作的挥舞攻击时攻击区域太广阔所造成的防御失灵，
                                            //就比如说一个弧形攻击，打中盾牌的瞬间这个弧形攻击动作还是会继续，那攻击点由于动画问题穿透了对方的盾牌打到对方身体话，就和我们对攻击防御演出的认识相违背了。
                                            _shieldsHit.Add(TheS.transform);
                                            HitShield = true;
                                            _usedTargets.Add(TheS.OwnerFightParamsReference.Center.geometryCenter);//这一行与接下来含（* *）的两行紧密对应(不管打中盾牌的主人是谁，主人都因为受盾牌保护而不会收到攻击了)
                                            _shieldHitPos.Add(_hits[hit_target_index].point);
                                            if (_shieldHitPos.Count > 0)
                                            {
                                                TheS.PassHitPointsFromWeaponToShiled(_shieldHitPos);//hit points on the shiled
                                            }
                                            _shieldHitPos.Clear();
                                        }
                                    }
                                }
                            }
                        }
                        //以上全部内容都是针对射线检测的防御判断

                        for (int hit_target_index = 0; hit_target_index < _hits.Length; hit_target_index++)
                        {
                            if (weaponHP > 0 && CurrentHP <= 0)
                            {
                                break;
                            }
                            if (SpecificTarget != SpecificTarget.flesh)
                            {
                                if ((teamConfig.enemyWeaponLayerMask == (teamConfig.enemyWeaponLayerMask | (1 << _hits[hit_target_index].collider.gameObject.layer))) && !_usedTargets.Contains(_hits[hit_target_index].collider.transform))
                                {
                                    HitBoxManager hit_hitbox = HitBoxesProcesser.Instance.GetHitBox(_hits[hit_target_index].collider);
                                    if (hit_hitbox != null && hit_hitbox._enabled)
                                    {
                                        _usedTargets.Add(_hits[hit_target_index].collider.transform);
                                        HitPointPara hitPointPara = new HitPointPara
                                        {
                                            onBodyPos = _hits[hit_target_index].point,
                                            qua = _hits[hit_target_index].collider.transform.rotation,
                                            WeaponHpCost = 1,
                                            exhaustEffect = true
                                        };
                                        AddWeaponEnergyExhaust(hitPointPara); // 因难以解决的问题已经停用
                                        HitBoxLifeEnding = HitBoxLifeEnding.touched;
                                        continue;//这里不退出循环的话就可能造成一个HP只有1的能量球既打碎了敌人的一个同血量能量球，又对敌人产生一点伤害。
                                    }
                                }
                            }

                            if (SpecificTarget == SpecificTarget.energy)
                            {
                                continue;
                            }
 
                            //_Raw_Target_Instance这个里面全是mainhealth，就是mainhealth，不是含着mainhealth的transform
                            //_Targets_Raw_Hit里面加入的全是_Raw_Target_Instance的transform，也就是mainhealth的transform
                            if (!_Targets_Raw_Hit.Contains(_hits[hit_target_index].collider.transform) && !_usedTargets.Contains(_hits[hit_target_index].collider.transform))
                            {
                                _boHitBox = _hits[hit_target_index].collider.GetComponent<BO_Limb>();
                                //方式1：mainhealth所在层级有collider //注意看这行条件，主要就是考虑到防御问题  （* *）
                                //if (_BO_Health != null && _Used_Targets.Contains(_markers[i]._hits[hit_target_index].collider.transform) == false)
                                //{
                                //    if (_BO_Health.collider_on_health)
                                //    {
                                //        HitFlesh = true;
                                //        _Raw_Target_Instance = _BO_Health;
                                //    }
                                //}
                                
                                //方式2：hitbox模式
                                if (_boHitBox != null)
                                {
                                    if (!_usedTargets.Contains(_boHitBox.Center.geometryCenter)) //注意看这行条件，主要就是考虑到防御问题 （* *）
                                    {
                                        HitFlesh = true;
                                        _Raw_Target_Instance = _boHitBox.Center.FightDataRef;//从上往下看，其实这一段表达的意思是一轮攻击只对一个main——health造成伤害
                                        _usedTargets.Add(_boHitBox.transform);
                                        _usedTargets.Add(_boHitBox.Center.geometryCenter);
                                    }
                                }
                                
                                if (_Raw_Target_Instance != null)
                                {
                                    _Targets_Raw_Hit.Add(_Raw_Target_Instance.Center.geometryCenter);
                                    _TrailModeStartPoint = _hits[hit_target_index].point;
                                    _TrailModeStartPoint = _TrailModeStartPoint + (_hits[hit_target_index].transform.position - _TrailModeStartPoint) * 0.3f;
                                    hitsOnHealthBody.Add(new V_Damage(this, _markers[i],_Raw_Target_Instance, _attackerRef, _TrailModeStartPoint,_TrailModeStartPoint, Quaternion.LookRotation(_Raw_Target_Instance.Center.geometryCenter.position-_TrailModeStartPoint,Vector3.up)));
                                    
                                    HitPointPara hitPointPara = new HitPointPara
                                    {
                                        onBodyPos = _hits[hit_target_index].point,
                                        qua = _hits[hit_target_index].collider.transform.rotation,
                                        WeaponHpCost = 1,
                                        exhaustEffect = false
                                    };
                                    
                                    AddWeaponEnergyExhaust(hitPointPara); // 因难以解决的问题已经停用
                                    HitBoxLifeEnding = HitBoxLifeEnding.touched;
                                }
                                if (HitFlesh && _Raw_Target_Instance != null)
                                {
                                    if (_Raw_Target_Instance.GetShield() != null)
                                    {
                                        _usedTargets.Add(_Raw_Target_Instance.GetShield().transform);
                                        //一把武器一轮enablemarkers和disablemarkers之间只可能对一个敌人进行一次伤害或进行一次“被防御”，敌人不可能在一把武器的一轮攻击期间内既受伤一次又防御成功一次
                                        //因此如果一轮攻击内敌人受伤了，也就再不用研究他能不能防御住所受攻击了。
                                    }
                                }
                            }
                            _Raw_Target_Instance = null;
                        }
                    }

                    if (_markers[i] is BO_Marker) //其实是针对球形检测的特殊形式把下面那个大for循环按照marker里的BallDetectHitPool重新循环跑了一次
                    {
                        _ballDetectHitPool = ((BO_Marker)_markers[i]).GetBallDetectHitPool();
                        if (_ballDetectHitPool != null)
                        {
                            if (_traditionalDefendMode)
                            {
                                foreach (var Hit_C in _ballDetectHitPool)
                                {
                                    if (_markers[i].enemyShieldLayer == (_markers[i].enemyShieldLayer | 1 << Hit_C.Key.gameObject.layer)　&&　!_shieldsHit.Contains(Hit_C.Key.transform))
                                    {
                                        BO_Shield TheS = Hit_C.Key.gameObject.GetComponent<BO_Shield>();
                                        if (TheS == null)
                                        {
                                            Debug.Log("防御盾构造严重错误");
                                            break;
                                        }

                                        if (TheS.OwnerFightParamsReference == null)
                                        {
                                            break;
                                        }

                                        if (!_shieldsHit.Contains(TheS.transform) // 本帧之内只要有武器上的一个mark打中了盾牌，那不再考虑其他mark是否打中盾牌
                                            && !_usedTargets.Contains(TheS.transform) //used_target只在一轮攻击后才清空，所以这里的意思应该是：如果打中的这个盾牌物体在这一轮里已经起过一次作用，那就不再研究。
                                            && !_usedTargets.Contains(TheS.OwnerFightParamsReference.Center.geometryCenter)) //所打中的盾牌对应的肉体已经在本轮攻击起过一次作用，那也不再详细计算
                                        {
                                            if (TheS._AdvancedShieldDetection)
                                            {
                                                dh = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldBackSpot.transform.position), 2);//这第二个参数也就是被攻击方肉体的transform
                                                ds1 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldCenterSpot.transform.position), 2);  //center
                                                ds2 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot1.transform.position), 2);  //Top
                                                ds3 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot2.transform.position), 2);  //Top Left
                                                ds4 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot3.transform.position), 2);  //Top Right
                                                ds5 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot4.transform.position), 2);  //Bottom
                                                ds6 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot5.transform.position), 2);  //Bottom Left
                                                ds7 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot6.transform.position), 2);  //Bottom Right
                                                ds8 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot7.transform.position), 2);  //Right
                                                ds9 = Mathf.Pow(Vector3.Distance(_WeaponHolderCenter.position, TheS._ShieldEdgeSpot8.transform.position), 2);  //Left
                                            }
                                            if (((dh > ds1) || (dh > ds2) || (dh > ds3) || (dh > ds4) || (dh > ds5) || (dh > ds6) || (dh > ds7) || (dh > ds8) || (dh > ds9)) || (TheS._AdvancedShieldDetection == false))
                                            {
                                                //飞行道具一律不需要开启_AdvancedShieldDetection功能，因为这个功能本身针对的就是近距离一方对另一方发起大幅度动作的挥舞攻击时攻击区域太广阔所造成的防御失灵，
                                                //就比如说一个弧形攻击，打中盾牌的瞬间这个弧形攻击动作还是会继续，那攻击点由于动画问题穿透了对方的盾牌打到对方身体话，就和我们对攻击防御演出的认识相违背了。
                                                _shieldsHit.Add(TheS.transform);
                                                HitShield = true;
                                                _usedTargets.Add(TheS.OwnerFightParamsReference.Center.geometryCenter);//这一行与接下来含（* *）的两行紧密对应(不管打中盾牌的主人是谁，主人都因为受盾牌保护而不会收到攻击了)

                                                _shieldHitPos.Add(Hit_C.Key.ClosestPoint(_markers[i].transform.position));//ClosestPointOnBounds
                                                if (_shieldHitPos.Count > 0)
                                                    TheS.PassHitPointsFromWeaponToShiled(_shieldHitPos);//hit points on the shiled
                                                _shieldHitPos.Clear();
                                            }
                                        }
                                    }
                                }
                            }

                            foreach (var hitC in _ballDetectHitPool)
                            {
                                if (weaponHP > 0 && CurrentHP <= 0)
                                {
                                    break;
                                }
                                if (SpecificTarget != SpecificTarget.flesh)
                                {
                                    if (hitC.Key == null)
                                    {
                                        Debug.Log("HitBox系统逻辑错误");
                                        continue;
                                    }
                                    if ((teamConfig.enemyWeaponLayerMask == (teamConfig.enemyWeaponLayerMask | (1 << hitC.Key.gameObject.layer)))
                                        && !_usedTargets.Contains(hitC.Key.transform))
                                    {
                                        var hitBox = HitBoxesProcesser.Instance.GetHitBox(hitC.Key);
                                        if (hitBox != null && hitBox._enabled)
                                        {
                                            _usedTargets.Add(hitC.Key.transform);
                                            hitC.Value.exhaustEffect = true;
                                            AddWeaponEnergyExhaust(hitC.Value);
                                            HitBoxLifeEnding = HitBoxLifeEnding.touched;
                                            continue;
                                        }
                                    }
                                }
                                
                                if (SpecificTarget == SpecificTarget.energy)
                                {
                                    continue;
                                }
                                
                                if (!_Targets_Raw_Hit.Contains(hitC.Key.transform) && !_usedTargets.Contains(hitC.Key.transform))
                                {
                                    _boHitBox = hitC.Key.GetComponent<BO_Limb>();
                                    //方式1：mainhealth所在层级有collider.注意看这行条件，主要就是考虑到防御问题  （* *）
                                    //if (_BO_Health != null && _Used_Targets.Contains(BallDetectHitPool[hit_target_index].transform) == false)
                                    //{
                                    //    if (_BO_Health.collider_on_health)
                                    //    {
                                    //        HitFlesh = true;
                                    //        _Raw_Target_Instance = _BO_Health;
                                    //    }
                                    //}
                                    //方式2：hitbox模式
                                    if (_boHitBox != null)
                                    {
                                        if (!_usedTargets.Contains(_boHitBox.Center.geometryCenter)) // 注意看这行条件，主要就是考虑到防御问题 （* *）
                                        {
                                            HitFlesh = true;
                                            _Raw_Target_Instance = _boHitBox.Center.FightDataRef; // 从上往下看，其实这一段表达的意思是一轮攻击只对一个main——health造成伤害
                                            _usedTargets.Add(_boHitBox.transform);
                                            _usedTargets.Add(_boHitBox.Center.geometryCenter);
                                        }
                                    }
                                    
                                    if (_Raw_Target_Instance != null)
                                    {
                                        _Targets_Raw_Hit.Add(_Raw_Target_Instance.Center.geometryCenter);
                                        hitsOnHealthBody.Add(new V_Damage(this, _markers[i],_Raw_Target_Instance, _attackerRef, hitC.Value.onBodyPos, hitC.Value.impactPos, hitC.Value.qua));
                                        hitC.Value.exhaustEffect = false;
                                        AddWeaponEnergyExhaust(hitC.Value);
                                        HitBoxLifeEnding = HitBoxLifeEnding.touched;
                                    }
                                    if (HitFlesh && _Raw_Target_Instance != null)
                                    {
                                        if (_Raw_Target_Instance.GetShield() != null)
                                        {
                                            // 一把武器一轮enablemarkers和disablemarkers之间只可能对一个敌人进行一次伤害或进行一次“被防御”，敌人不可能在一把武器的一轮攻击期间内既受伤一次又防御成功一次
                                            // 因此如果一轮攻击内敌人受伤了，也就再不用研究他能不能防御住所受攻击了。
                                            _usedTargets.Add(_Raw_Target_Instance.GetShield().transform);
                                        }
                                    }
                                }
                                _Raw_Target_Instance = null;
                            }
                        }
                    }
                }
            }
            // 防止一个武器单位的多个markers重复打中健康体
            _Targets_Raw_Hit.Clear();
        }
        
        // 而这个参数将和ContinuousDamage形成一个相互权衡的关系。如果武器不是ContinuousDamage，则一个能量系武器在打击到对象后应该立刻hp-1，并且直接cleartargets。
        // 直到hp为0时自身消灭。这样比如一个hp为2的波动技能就形成了一个类似kof99中boss那样的2连击飞行道具，这个道具打到人身上基本是形成一个很快的2连击。
        // 而如果这个武器是ContinuousDamage，事情将另当别论。ContinuousDamage类武器的cleartargets周期应该符合ContinuousDamageInterval。
        // 它在攻击到一个对象后不会立刻随着自身hp的减少而cleartargets，但如果它有着大于0的hp，它依然会随着打击到对象而掉血，并随着寿命结束而消失
        // 设想有一个地上火焰技能是ContinuousDamage，它可能有两种消失方式，一种是打击了不少对象hp为0了，一种是随着自身BO_destroyer的设置而时间已经尽。        
        // WeaponEnergyExhaust 这个函数在“与敌人武器发生接触”和“与敌人肉体产生接触”的时候是不同的处理逻辑
        private readonly List<UnityEngine.Events.UnityAction> _weaponEnergyExhaustMissions = new List<UnityAction>();
        void AddWeaponEnergyExhaust(HitPointPara hitPointPara)
        {
            if (weaponHP > 0 && CurrentHP > 0)
            {
                CurrentHP -= hitPointPara.WeaponHpCost;
                UnityEngine.Events.UnityAction wC = AttackerFreeze;
                _weaponEnergyExhaustMissions.Add(wC);
                if (!ContinuousDamage)
                {
                    _weaponEnergyExhaustMissions.Add(ClearTargets);
                }
            }
            if (weaponHP <= 0)
            {
                UnityEngine.Events.UnityAction we_C = AttackerFreeze;
                _weaponEnergyExhaustMissions.Add(we_C);
            }
            
            if (hitPointPara.exhaustEffect && !string.IsNullOrEmpty(ExplosionEffect))
                EffectsManager.GenerateEffect(ExplosionEffect, FightGlobalSetting.EffectPathDefine(element), hitPointPara.onBodyPos, hitPointPara.qua, null).Forget();
        }
        
        void AttackerFreeze()
        {
            if (_WeaponMode == WeaponMode.EnergyFromBodyWeapon)
            {
                _attackerRef.Center.AnimationManger.FrameFreeze();
            }
        }
    }
}