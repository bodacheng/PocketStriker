using System;
using System.Collections.Generic;

namespace Skill
{
    public static class SkillAttackTypeUtility
    {
        static readonly HashSet<string> CanonicalAttackTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "GR",
            "GM",
            "GI",
            "CT",
            "NONE"
        };

        static readonly Dictionary<string, string> LegacyAttackTypeAliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "GMB", "GM" },
            { "RB", "CT" }
        };

        public static string NormalizeAttackType(string attackType, bool normalizeLegacyAliases)
        {
            if (string.IsNullOrWhiteSpace(attackType))
            {
                return null;
            }

            var trimmed = attackType.Trim();
            if (normalizeLegacyAliases && LegacyAttackTypeAliases.TryGetValue(trimmed, out var normalized))
            {
                return normalized;
            }

            return IsKnownAttackType(trimmed, normalizeLegacyAliases) ? trimmed : null;
        }

        public static string NormalizeAttackTypeOrOriginal(string attackType, bool normalizeLegacyAliases)
        {
            return NormalizeAttackType(attackType, normalizeLegacyAliases) ?? attackType?.Trim();
        }

        public static bool IsKnownAttackType(string attackType, bool normalizeLegacyAliases)
        {
            if (string.IsNullOrWhiteSpace(attackType))
            {
                return false;
            }

            var trimmed = attackType.Trim();
            return CanonicalAttackTypes.Contains(trimmed)
                   || (!normalizeLegacyAliases && LegacyAttackTypeAliases.ContainsKey(trimmed))
                   || (normalizeLegacyAliases && LegacyAttackTypeAliases.ContainsKey(trimmed));
        }

        public static BehaviorType ToBehaviorType(string attackType, bool normalizeLegacyAliases)
        {
            switch (NormalizeAttackType(attackType, normalizeLegacyAliases))
            {
                case "GR":
                    return BehaviorType.GR;
                case "GI":
                    return BehaviorType.GI;
                case "GM":
                    return BehaviorType.GM;
                case "GMB":
                    return BehaviorType.GMB;
                case "CT":
                    return BehaviorType.CT;
                case "RB":
                    return BehaviorType.RB;
                default:
                    return BehaviorType.NONE;
            }
        }

        public static string ToCanonicalAttackTypeCode(BehaviorType behaviorType)
        {
            switch (behaviorType)
            {
                case BehaviorType.GR:
                    return "GR";
                case BehaviorType.GI:
                    return "GI";
                case BehaviorType.GM:
                    return "GM";
                case BehaviorType.CT:
                    return "CT";
                case BehaviorType.NONE:
                    return "NONE";
                default:
                    return null;
            }
        }
    }
}
