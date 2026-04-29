using UnityEngine;

namespace MCombat.Shared.Combat
{
    public static class CombatLayerUtility
    {
        public static bool ContainsLayer(LayerMask mask, int layer)
        {
            return (mask.value & (1 << layer)) != 0;
        }
    }
}
