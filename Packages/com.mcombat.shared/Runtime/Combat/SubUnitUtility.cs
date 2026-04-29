using System;

namespace MCombat.Shared.Combat
{
    public interface ISubUnitSwitchHandler<TPayload>
    {
        bool IsSubUnit { get; }
        bool HasChangedToSubUnit { get; }
        bool TryChangeToSub(string stateKey, TPayload payload);
    }

    public static class SubUnitUtility
    {
        public const string IdPrefix = "sub_";

        public static bool IsSubUnitId(string unitId)
        {
            return !string.IsNullOrEmpty(unitId)
                   && unitId.StartsWith(IdPrefix, StringComparison.Ordinal);
        }

        public static string GetSubUnitId(string ownerGuid)
        {
            return string.IsNullOrEmpty(ownerGuid) ? null : IdPrefix + ownerGuid;
        }

        public static bool SupportsSubUnitRoster(global::FightMode fightMode)
        {
            return fightMode is global::FightMode.Rotate or global::FightMode.Evolve;
        }
    }
}
