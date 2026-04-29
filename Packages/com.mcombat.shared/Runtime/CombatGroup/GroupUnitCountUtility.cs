using System;
using System.Collections.Generic;

namespace MCombat.Shared.CombatGroup
{
    public interface IGroupUnitCountEntry
    {
        string Id { get; }
        int Count { get; set; }
        int OriginCount { get; set; }
    }

    public static class GroupUnitCountUtility
    {
        public static List<string> GetNonZeroIds<TEntry>(IEnumerable<TEntry> entries)
            where TEntry : class, IGroupUnitCountEntry
        {
            var ids = new List<string>();
            if (entries == null)
            {
                return ids;
            }

            foreach (var entry in entries)
            {
                if (entry != null && entry.Count > 0)
                {
                    ids.Add(entry.Id);
                }
            }

            return ids;
        }

        public static TEntry GetOrCreate<TEntry>(
            IList<TEntry> entries,
            string id,
            Func<string, int, TEntry> createEntry,
            int defaultCount)
            where TEntry : class, IGroupUnitCountEntry
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && entry.Id == id)
                {
                    EnsureOriginCount(entry);
                    return entry;
                }
            }

            var newEntry = createEntry(id, defaultCount);
            EnsureOriginCount(newEntry);
            entries.Add(newEntry);
            return newEntry;
        }

        public static int SetTeamUnitCount<TEntry>(
            IList<TEntry> entries,
            string id,
            int count,
            int teamMaxCount,
            bool force,
            Func<string, int, TEntry> createEntry,
            int defaultCount = 8)
            where TEntry : class, IGroupUnitCountEntry
        {
            if (count < 0)
            {
                count = 0;
            }

            var entry = GetOrCreate(entries, id, createEntry, defaultCount);
            var ifWholeCount = GetWholeCountIfSet(entries, id, count);
            entry.Count = ifWholeCount <= teamMaxCount || force
                ? count
                : Math.Max(0, count - (ifWholeCount - teamMaxCount));
            return GetWholeCount(entries);
        }

        public static int GetWholeCount<TEntry>(IEnumerable<TEntry> entries)
            where TEntry : class, IGroupUnitCountEntry
        {
            var wholeCount = 0;
            if (entries == null)
            {
                return wholeCount;
            }

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    wholeCount += entry.Count;
                }
            }

            return wholeCount;
        }

        public static void ClearWholeCount<TEntry>(IEnumerable<TEntry> entries)
            where TEntry : class, IGroupUnitCountEntry
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    entry.Count = 0;
                }
            }
        }

        public static int AutoAdjustTeamUnitByMaxCount<TEntry, TUnit>(
            IList<TEntry> entries,
            IReadOnlyList<TUnit> unitSets,
            int selectedMaxTeamCount,
            bool adaptMode,
            bool restoreOriginCountFirst,
            Func<TUnit, string> getUnitId,
            Func<string, int, TEntry> createEntry,
            int defaultCount = 8)
            where TEntry : class, IGroupUnitCountEntry
        {
            if (unitSets == null || unitSets.Count == 0)
            {
                ClearWholeCount(entries);
                return 0;
            }

            EnsureOriginCountsFromCurrent(entries);
            ClearWholeCount(entries);

            var wholeTeamCount = 0;
            foreach (var unit in unitSets)
            {
                var id = getUnitId(unit);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                wholeTeamCount += GetOrCreate(entries, id, createEntry, defaultCount).Count;
            }

            if (!adaptMode)
            {
                return wholeTeamCount;
            }

            foreach (var unit in unitSets)
            {
                var id = getUnitId(unit);
                if (!string.IsNullOrEmpty(id))
                {
                    wholeTeamCount = SetTeamUnitCount(entries, id, 0, selectedMaxTeamCount, false, createEntry, defaultCount);
                }
            }

            if (restoreOriginCountFirst)
            {
                foreach (var unit in unitSets)
                {
                    var id = getUnitId(unit);
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    var entry = GetOrCreate(entries, id, createEntry, defaultCount);
                    wholeTeamCount = SetTeamUnitCount(entries, id, entry.OriginCount, selectedMaxTeamCount, false, createEntry, defaultCount);
                }
            }

            var toBeAdd = selectedMaxTeamCount - wholeTeamCount;
            for (var index = 0; index < unitSets.Count && toBeAdd > 0; index++)
            {
                var id = getUnitId(unitSets[index]);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var addCount = index != unitSets.Count - 1 ? (int)((float)toBeAdd / unitSets.Count) : toBeAdd;
                var entry = GetOrCreate(entries, id, createEntry, defaultCount);
                wholeTeamCount = SetTeamUnitCount(entries, id, entry.Count + addCount, selectedMaxTeamCount, false, createEntry, defaultCount);
                toBeAdd = selectedMaxTeamCount - wholeTeamCount;
            }

            return wholeTeamCount;
        }

        public static int GetWholeCountIfSet<TEntry>(IEnumerable<TEntry> entries, string id, int count)
            where TEntry : class, IGroupUnitCountEntry
        {
            var wholeCount = 0;
            if (entries == null)
            {
                return wholeCount;
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                wholeCount += entry.Id == id ? count : entry.Count;
            }

            return wholeCount;
        }

        static void EnsureOriginCountsFromCurrent<TEntry>(IEnumerable<TEntry> entries)
            where TEntry : class, IGroupUnitCountEntry
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    EnsureOriginCount(entry);
                }
            }
        }

        static void EnsureOriginCount(IGroupUnitCountEntry entry)
        {
            if (entry.OriginCount <= 0)
            {
                entry.OriginCount = entry.Count;
            }
        }
    }
}
