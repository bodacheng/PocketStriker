using System;
using System.Collections.Generic;
using Skill;

namespace MCombat.Shared.Behaviour
{
    public static class SkillSetSlotUtility
    {
        public const int SlotCount = 9;

        public static List<string> SkillIdList(
            string a1,
            string a2,
            string a3,
            string b1,
            string b2,
            string b3,
            string c1,
            string c2,
            string c3)
        {
            var ids = new List<string>();
            AddIfNotNull(ids, a1);
            AddIfNotNull(ids, a2);
            AddIfNotNull(ids, a3);
            AddIfNotNull(ids, b1);
            AddIfNotNull(ids, b2);
            AddIfNotNull(ids, b3);
            AddIfNotNull(ids, c1);
            AddIfNotNull(ids, c2);
            AddIfNotNull(ids, c3);
            return ids;
        }

        public static string GetSlot(
            int targetSlot,
            string a1,
            string a2,
            string a3,
            string b1,
            string b2,
            string b3,
            string c1,
            string c2,
            string c3)
        {
            switch (targetSlot)
            {
                case 1:
                    return a1;
                case 2:
                    return a2;
                case 3:
                    return a3;
                case 4:
                    return b1;
                case 5:
                    return b2;
                case 6:
                    return b3;
                case 7:
                    return c1;
                case 8:
                    return c2;
                case 9:
                    return c3;
                default:
                    return null;
            }
        }

        public static void SetSlot(
            int targetSlot,
            string skillId,
            ref string a1,
            ref string a2,
            ref string a3,
            ref string b1,
            ref string b2,
            ref string b3,
            ref string c1,
            ref string c2,
            ref string c3)
        {
            switch (targetSlot)
            {
                case 1:
                    a1 = skillId;
                    break;
                case 2:
                    a2 = skillId;
                    break;
                case 3:
                    a3 = skillId;
                    break;
                case 4:
                    b1 = skillId;
                    break;
                case 5:
                    b2 = skillId;
                    break;
                case 6:
                    b3 = skillId;
                    break;
                case 7:
                    c1 = skillId;
                    break;
                case 8:
                    c2 = skillId;
                    break;
                case 9:
                    c3 = skillId;
                    break;
            }
        }

        public static List<int> RemainSlotSpLevels(
            int equippedSkillCount,
            int currentPoint,
            SkillSetValidationCosts costs,
            Func<int, bool> hasAvailableStone = null)
        {
            var remainSlotCount = SlotCount - equippedSkillCount;
            var minimumFutureCost = Math.Max(0, remainSlotCount - 1) * costs.Sp0Cost;
            var maxRemain = costs.SkillSetCostLimit - (currentPoint + minimumFutureCost);
            var result = new List<int>();

            AddSpLevelIfAllowed(result, 3, maxRemain, costs.Sp3Cost, hasAvailableStone);
            AddSpLevelIfAllowed(result, 2, maxRemain, costs.Sp2Cost, hasAvailableStone);
            AddSpLevelIfAllowed(result, 1, maxRemain, costs.Sp1Cost, hasAvailableStone);
            AddSpLevelIfAllowed(result, 0, maxRemain, costs.Sp0Cost, hasAvailableStone, true);
            return result;
        }

        public static float AverageLevel(IReadOnlyList<float> levels)
        {
            if (levels == null || levels.Count == 0)
            {
                return 0f;
            }

            float sum = 0;
            for (var i = 0; i < levels.Count; i++)
            {
                sum += levels[i];
            }

            return (float)Math.Round(sum / levels.Count, 1);
        }

        public static float CalculateHp(
            IEnumerable<string> skillIds,
            float level,
            Func<string, SkillConfig> resolveSkillConfig,
            Func<float, float, float> calculateStoneHp)
        {
            if (skillIds == null)
            {
                return 0f;
            }

            float wholeHp = 0;
            foreach (var skillId in skillIds)
            {
                var skillConfig = skillId != null ? resolveSkillConfig?.Invoke(skillId) : null;
                if (skillConfig != null)
                {
                    wholeHp += calculateStoneHp(skillConfig.HP_WEIGHT, level);
                }
            }

            return wholeHp;
        }

        public static int LowestSpLevel(
            string a1,
            string a2,
            string a3,
            string b1,
            string b2,
            string b3,
            string c1,
            string c2,
            string c3,
            Func<string, SkillConfig> resolveSkillConfig)
        {
            var lowest = int.MaxValue;
            for (var slot = 1; slot <= SlotCount; slot++)
            {
                var skillId = GetSlot(slot, a1, a2, a3, b1, b2, b3, c1, c2, c3);
                var config = skillId != null ? resolveSkillConfig?.Invoke(skillId) : null;
                var spLevel = config != null ? config.SP_LEVEL : 0;
                if (spLevel < lowest)
                {
                    lowest = spLevel;
                }
            }

            return lowest == int.MaxValue ? 0 : lowest;
        }

        public static int RecommendedTargetReplaceSlot(
            IReadOnlyList<string> skillIds,
            Func<string, SkillConfig> resolveSkillConfig,
            bool mugen)
        {
            if (skillIds == null || skillIds.Count < SlotCount)
            {
                return 0;
            }

            var normalSkillCountAtFirstRow = 0;
            CountNormalStartSkill(skillIds[0], resolveSkillConfig, ref normalSkillCountAtFirstRow);
            CountNormalStartSkill(skillIds[3], resolveSkillConfig, ref normalSkillCountAtFirstRow);
            CountNormalStartSkill(skillIds[6], resolveSkillConfig, ref normalSkillCountAtFirstRow);

            var searchOrder = new[] { 0, 3, 6, 1, 4, 7, 2, 5, 8 };
            for (var order = 0; order < searchOrder.Length; order++)
            {
                var index = searchOrder[order];
                var config = resolveSkillConfig?.Invoke(skillIds[index]);
                if (config == null)
                {
                    continue;
                }

                if (config.SP_LEVEL == 0 && (mugen || !IsFirstColumnIndex(index) || normalSkillCountAtFirstRow > 1))
                {
                    return index + 1;
                }
            }

            return 0;
        }

        public static bool IsStartSlot(int targetSlot)
        {
            return targetSlot == 1 || targetSlot == 4 || targetSlot == 7;
        }

        public static List<string> StartSkillIds(string a1, string b1, string c1)
        {
            var result = new List<string>();
            AddIfNotNull(result, a1);
            AddIfNotNull(result, b1);
            AddIfNotNull(result, c1);
            return result;
        }

        static void AddIfNotNull(List<string> ids, string skillId)
        {
            if (skillId != null)
            {
                ids.Add(skillId);
            }
        }

        static void AddSpLevelIfAllowed(
            List<int> result,
            int spLevel,
            int maxRemain,
            int cost,
            Func<int, bool> hasAvailableStone,
            bool ignoreCost = false)
        {
            if (!ignoreCost && maxRemain < cost)
            {
                return;
            }

            if (hasAvailableStone != null && !hasAvailableStone(spLevel))
            {
                return;
            }

            result.Add(spLevel);
        }

        static void CountNormalStartSkill(string skillId, Func<string, SkillConfig> resolveSkillConfig, ref int count)
        {
            var config = skillId != null ? resolveSkillConfig?.Invoke(skillId) : null;
            if (config != null && config.SP_LEVEL == 0)
            {
                count++;
            }
        }

        static bool IsFirstColumnIndex(int index)
        {
            return index == 0 || index == 3 || index == 6;
        }
    }
}
