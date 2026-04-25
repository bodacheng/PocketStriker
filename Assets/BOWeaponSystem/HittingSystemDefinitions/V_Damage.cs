using UnityEngine;
using MCombat.Shared.CombatHit;

namespace HittingDetection
{
    public class V_Damage
    {
        public readonly FightParamsReference attacker;
        public readonly FightParamsReference victim;
        public readonly HitBoxManager from_weapon;
        public readonly Marker from_weapon_marker;
        public Vector3 DamageEffectPoint;
        public Vector3 impactComingPoint;
        public Quaternion CutRotation;

        public V_Damage() { }
        public V_Damage(HitBoxManager weapon, Marker weapon_marker, FightParamsReference _victim, FightParamsReference _attacker, Vector3 damageEffectPoint, Vector3 impactComingPoint,Quaternion _CutRotation)
        {
            from_weapon = weapon;
            from_weapon_marker = weapon_marker;
            attacker = _attacker;
            victim = _victim;
            DamageEffectPoint = damageEffectPoint;
            this.impactComingPoint = impactComingPoint;
            CutRotation = _CutRotation;
        }

        public V_Damage Clone()
        {
            return (V_Damage)MemberwiseClone();
        }

        public static DamageType FormalIntToDamageType(int num)
        {
            return HitDamageUtility.FormalIntToDamageType(num);
        }

        /// <summary>
        /// heavyLevel是2的能量球，撞击1的能量球时，
        /// 自身HP只消耗0.5, 从而在HP都是1的情况下，
        /// 一个heavyLevel是2的能量球在撞击两个1的能量球时才会消失掉。
        /// </summary>
        /// <param name="meLevel"></param>
        /// <param name="counterLevel"></param>
        /// <returns></returns>
        public static float WpHpCost(int meLevel, int counterLevel)
        {
            return HitDamageUtility.WeaponHpCost(meLevel, counterLevel);
        }
    }
}
