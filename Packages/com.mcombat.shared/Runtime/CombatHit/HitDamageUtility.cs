namespace MCombat.Shared.CombatHit
{
    public static class HitDamageUtility
    {
        public static HittingDetection.DamageType FormalIntToDamageType(int num)
        {
            return (HittingDetection.DamageType)num;
        }

        /// <summary>
        /// Calculates how much HP an energy weapon consumes when colliding with another weapon.
        /// </summary>
        public static float WeaponHpCost(int meLevel, int counterLevel)
        {
            if (meLevel > counterLevel)
            {
                switch (meLevel - counterLevel)
                {
                    case 1:
                        return 0.5f;
                    case 2:
                        return 0.25f;
                    case 3:
                        return 0.2f;
                }

                if (meLevel - counterLevel > 3)
                {
                    return 0.1f;
                }
            }

            return 1f;
        }
    }
}
