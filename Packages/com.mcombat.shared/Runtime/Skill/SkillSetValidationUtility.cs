using System;
using System.Collections.Generic;

namespace Skill
{
    public enum SkillSetEditError
    {
        Empty = 0,
        NotFull = 1,
        UnBalanced = 2,
        RepeatedSkill = 3,
        NoNormalStart = 4,
        NoAtLeastTwoEx = 5,
        Perfect = 6
    }

    public readonly struct SkillSetValidationCosts
    {
        public readonly int SkillSetCostLimit;
        public readonly int Sp0Cost;
        public readonly int Sp1Cost;
        public readonly int Sp2Cost;
        public readonly int Sp3Cost;

        public SkillSetValidationCosts(int skillSetCostLimit, int sp0Cost, int sp1Cost, int sp2Cost, int sp3Cost)
        {
            SkillSetCostLimit = skillSetCostLimit;
            Sp0Cost = sp0Cost;
            Sp1Cost = sp1Cost;
            Sp2Cost = sp2Cost;
            Sp3Cost = sp3Cost;
        }

        public int GetCost(int spLevel)
        {
            switch (spLevel)
            {
                case 0:
                    return Sp0Cost;
                case 1:
                    return Sp1Cost;
                case 2:
                    return Sp2Cost;
                case 3:
                    return Sp3Cost;
                default:
                    return 0;
            }
        }
    }

    public static class SkillSetValidationUtility
    {
        public static SkillSetEditError CheckEdit(
            string a1,
            string a2,
            string a3,
            string b1,
            string b2,
            string b3,
            string c1,
            string c2,
            string c3,
            Func<string, SkillConfig> getSkillConfig,
            IEnumerable<string> passiveSkillIds,
            SkillSetValidationCosts costs,
            bool atLeastTwoExSkill = false)
        {
            var passiveSkillSet = ToHashSet(passiveSkillIds);
            if (IsEmpty(a1, getSkillConfig, passiveSkillSet)
                && IsEmpty(a2, getSkillConfig, passiveSkillSet)
                && IsEmpty(a3, getSkillConfig, passiveSkillSet)
                && IsEmpty(b1, getSkillConfig, passiveSkillSet)
                && IsEmpty(b2, getSkillConfig, passiveSkillSet)
                && IsEmpty(b3, getSkillConfig, passiveSkillSet)
                && IsEmpty(c1, getSkillConfig, passiveSkillSet)
                && IsEmpty(c2, getSkillConfig, passiveSkillSet)
                && IsEmpty(c3, getSkillConfig, passiveSkillSet))
            {
                return SkillSetEditError.Empty;
            }

            var wholePoint = SkillBalancePoint(a1, a2, a3, b1, b2, b3, c1, c2, c3, getSkillConfig, costs);
            if (wholePoint > costs.SkillSetCostLimit)
            {
                return SkillSetEditError.UnBalanced;
            }

            if (!(HasStone(a1, getSkillConfig) && HasStone(a2, getSkillConfig) && HasStone(a3, getSkillConfig)
                  && HasStone(b1, getSkillConfig) && HasStone(b2, getSkillConfig) && HasStone(b3, getSkillConfig)
                  && HasStone(c1, getSkillConfig) && HasStone(c2, getSkillConfig) && HasStone(c3, getSkillConfig)))
            {
                return SkillSetEditError.NotFull;
            }

            if (CheckStartSkills(a1, b1, c1, getSkillConfig) == SkillSetEditError.NoNormalStart)
            {
                return SkillSetEditError.NoNormalStart;
            }

            if (atLeastTwoExSkill && !CheckAtLeastTwoEx(a1, a2, a3, b1, b2, b3, c1, c2, c3, getSkillConfig))
            {
                return SkillSetEditError.NoAtLeastTwoEx;
            }

            return SkillSetEditError.Perfect;
        }

        public static int SkillBalancePoint(
            string a1SkillId,
            string a2SkillId,
            string a3SkillId,
            string b1SkillId,
            string b2SkillId,
            string b3SkillId,
            string c1SkillId,
            string c2SkillId,
            string c3SkillId,
            Func<string, SkillConfig> getSkillConfig,
            SkillSetValidationCosts costs)
        {
            var skillConfigs = new[]
            {
                getSkillConfig(a1SkillId),
                getSkillConfig(a2SkillId),
                getSkillConfig(a3SkillId),
                getSkillConfig(b1SkillId),
                getSkillConfig(b2SkillId),
                getSkillConfig(b3SkillId),
                getSkillConfig(c1SkillId),
                getSkillConfig(c2SkillId),
                getSkillConfig(c3SkillId)
            };

            var balancePoint = 0;
            for (var i = 0; i < skillConfigs.Length; i++)
            {
                if (skillConfigs[i] != null)
                {
                    balancePoint += costs.GetCost(skillConfigs[i].SP_LEVEL);
                }
            }

            return balancePoint;
        }

        static bool HasStone(string skillId, Func<string, SkillConfig> getSkillConfig)
        {
            return getSkillConfig(skillId) != null;
        }

        static bool IsEmpty(string skillId, Func<string, SkillConfig> getSkillConfig, ISet<string> passiveSkillIds)
        {
            return !HasStone(skillId, getSkillConfig) || passiveSkillIds.Contains(skillId);
        }

        static bool CheckAtLeastTwoEx(
            string a1,
            string a2,
            string a3,
            string b1,
            string b2,
            string b3,
            string c1,
            string c2,
            string c3,
            Func<string, SkillConfig> getSkillConfig)
        {
            var ids = new[]
            {
                a1,
                a2,
                a3,
                b1,
                b2,
                b3,
                c1,
                c2,
                c3
            };

            var exCount = 0;
            for (var i = 0; i < ids.Length; i++)
            {
                var skillConfig = getSkillConfig(ids[i]);
                if (skillConfig != null && skillConfig.SP_LEVEL != 0)
                {
                    exCount++;
                }
            }

            return exCount >= 2;
        }

        static SkillSetEditError CheckStartSkills(
            string a1Skill,
            string a2Skill,
            string a3Skill,
            Func<string, SkillConfig> getSkillConfig)
        {
            var skillConfigA1 = getSkillConfig(a1Skill);
            var skillConfigB1 = getSkillConfig(a2Skill);
            var skillConfigC1 = getSkillConfig(a3Skill);

            if (skillConfigA1 != null && skillConfigA1.SP_LEVEL == 0)
            {
                return SkillSetEditError.Perfect;
            }

            if (skillConfigB1 != null && skillConfigB1.SP_LEVEL == 0)
            {
                return SkillSetEditError.Perfect;
            }

            return skillConfigC1 != null && skillConfigC1.SP_LEVEL == 0
                ? SkillSetEditError.Perfect
                : SkillSetEditError.NoNormalStart;
        }

        static ISet<string> ToHashSet(IEnumerable<string> values)
        {
            var set = new HashSet<string>();
            if (values == null)
            {
                return set;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    set.Add(value);
                }
            }

            return set;
        }
    }
}
