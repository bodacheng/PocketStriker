using UnityEngine;
using HittingDetection;

namespace Soul
{
    public class Defend_State : Behavior
    {
        readonly string defend_clip_name;
        readonly string block_break_name;
        readonly float DefendHpRefreshTime = 5f;

        float time_counter;
        bool freezed = false;
        bool temp;
        float TimeCounter
        {
            set
            {
                temp = time_counter >= 0f;
                time_counter = value;
                if (!freezed)
                {
                    if (time_counter < used_block_least_time * 0.8f)
                    {
                        _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                        freezed = true;
                    }
                }
                if (temp)
                {
                    if (time_counter < 0f)
                    {
                        AnimationManger.AnimationTrigger(defend_clip_name, true, CommonSetting.CharacterAnimDuration);
                    }
                }
            }
            get
            {
                return time_counter;
            }
        }

        float used_block_least_time;
        int DefendHP = 10;
        float lastExitTime;

        Collider threat;
        Collider nearbyenemymeat;
        Vector3 fixDesV3;

        public Defend_State(string defend_clip_name, string block_break_name)
        {
            this.defend_clip_name = defend_clip_name;
            this.block_break_name = block_break_name;
        }

        void DefendHPfade(V_Damage damage)
        {
            DefendHP -= 1;
            //if (defendHP <= 0)
            //{
            //    _FightAttriCalReference.ApplyDamage(new V_Damage(DamageType.supper_damage, WeaponPosAdjustMode.pushToMidForward, 
            //                                                        damage.damageHappenPoint, damage.CutRotation,
            //                                                            damage.AttackerT_foward,damage.AttackerT_pos, 
            //                                                                damage.fromWeapon));
            //    EffectAndHurtObjectLoading.Instance.GenerateEffect("onEnableShieldSpark", null, damage.damageHappenPoint, Quaternion.identity, null);
            //}
        }

        public override void Pre_process_before_enter()
        {
            base.Pre_process_before_enter();
            DefendHP = 10;
        }

        public override bool Capacity_enter_condition()
        {
            if ((Time.time - lastExitTime) > DefendHpRefreshTime)
            {
                DefendHP = 10;
            }
            return (DefendHP > 0);
        }

        public override bool Capacity_Exit_Condition()
        {
            return TimeCounter <= 0;
        }

        public override void AI_State_enter()
        {
            //defendHP = FightGlobalSetting._defendHP;
            base.AI_State_enter();
            freezed = false;
            FightParamsRef.Resistance.Value = DefendHP > 0 ? 10 : 0;
            _Weapon_Animation_Events.ClearMarkerManagers();
            Sensor.DetectionStart(-1, true);
            _Animator.SetFloat("speed", 0f);
            AnimationManger.AnimationTrigger(defend_clip_name, false, CommonSetting.CharacterAnimDuration);
            _Rigidbody.velocity = Vector3.zero;
            used_block_least_time = FightGlobalSetting.LightBlockLastingTime;
            TimeCounter = used_block_least_time;
            _SkillCancelFlag.turn_off_flag();
            //this.AI_DATA_CENTER.turnShield(true);
        }

        public override void AI_State_enter(V_Damage newValue)
        {
            base.AI_State_enter();
            freezed = false;
            FightParamsRef.Resistance.Value = DefendHP > 0 ? 10 : 0;
            _Weapon_Animation_Events.ClearMarkerManagers();
            Sensor.DetectionStart(-1, true);
            _Animator.SetFloat("speed", 0f);
            _SkillCancelFlag.turn_off_flag();
            //this.AI_DATA_CENTER.turnShield(true);

            fixDesV3 = CalFixPushVector(newValue.DamageEffectPoint,
                newValue.attacker.Center.WholeT.position,
                                               gameObject.transform.position,
                                                   newValue.from_weapon.damage_type, newValue.from_weapon._WeaponMode);
            switch (newValue.from_weapon.damage_type)
            {
                case DamageType.light_damage_forward:
                    AnimationManger.AnimationTrigger(block_break_name, true, 0.05f);
                    _Rigidbody.velocity = fixDesV3;
                    used_block_least_time = FightGlobalSetting.LightBlockLastingTime;
                    DefendHPfade(newValue);
                    break;
                case DamageType.heavy_damage_forward:
                    AnimationManger.AnimationTrigger(block_break_name, true, 0.05f);
                    _Rigidbody.velocity = fixDesV3;
                    used_block_least_time = FightGlobalSetting.HeavyBlockLastingTime;
                    DefendHPfade(newValue);
                    break;
                case DamageType.supper_damage_forward:
                    AnimationManger.AnimationTrigger(block_break_name, true, 0.05f);
                    _Rigidbody.velocity = fixDesV3 - gameObject.transform.position;
                    used_block_least_time = FightGlobalSetting.HeavyBlockLastingTime;
                    DefendHPfade(newValue);
                    break;
                default:
                    AnimationManger.AnimationTrigger(block_break_name, true, 0.05f);
                    _Rigidbody.velocity = fixDesV3;
                    used_block_least_time = FightGlobalSetting.LightBlockLastingTime;
                    DefendHPfade(newValue);
                    break;
            }
            TimeCounter = used_block_least_time;
        }

        public override void AI_State_exit()
        {
            //this.Animation_Manger.PlayLayerAnim(animator_layer_index.Full_Body,null);
            //注意看changeState环节，上一个状态的exit和下一个状态的enter是同一个帧执行的。
            //从这里我们曾经发现了动画播放模块一个重要问题，就是在特定情况下，
            //比如defend状态的exit里有PlayLayerAnim(_animator_layer_index, null)，防御后接攻击，
            //那么先执行PlayLayerAnim(_animator_layer_index, null) ，同一帧执行PlayLayerAnim(_animator_layer_index, clip_name);
            //就会产生bug：动画器无法正常播放攻击动画，角色会立在那里。这是我们动画模块的一个性质。
            // 我们把defend状态exit中的PlayLayerAnim(_animator_layer_index, null)删除了后就不再产生对应bug。
            // 关于动画模块的“技能动作清空”，我们是把它放在了move状态的开头，从而避免了清空函数与触发动画函数在同一帧执行。
            base.AI_State_exit();
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            lastExitTime = Time.time;
            _ResistanceManager.ResistanceClear();
            //AI_DATA_CENTER.turnShield(false);
        }

        public override void _State_FixedUpdate1()
        {
            FightParamsRef.Resistance.Value = DefendHP > 0 ? 5 : 0;
            threat = Sensor.GetSuddenThreatInRange(0, 5f);
            nearbyenemymeat = Sensor.GetClosestEnemyColliderInSensorRange();

            if (TimeCounter >= 0f)
            {
                TimeCounter -= Time.fixedDeltaTime;
            }

            if (nearbyenemymeat != null)
            {
                RotateToTarget(nearbyenemymeat.transform.position, 0.5f, true);
            }
            else
            {
                if (threat != null)
                {
                    RotateToTarget(threat.transform.position, 0.5f, true);
                }
            }
        }
    }
}