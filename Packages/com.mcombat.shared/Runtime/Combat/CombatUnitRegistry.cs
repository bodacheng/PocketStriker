using System.Collections.Generic;

namespace MCombat.Shared.Combat
{
    public sealed class CombatUnitRegistry<TUnit>
        where TUnit : class
    {
        readonly IDictionary<Team, List<TUnit>> _unitsByTeam = new Dictionary<Team, List<TUnit>>();

        public void Clear()
        {
            _unitsByTeam.Clear();
        }

        public void AddOrRemove(TUnit unit, Team team, bool add)
        {
            if (!_unitsByTeam.TryGetValue(team, out var units))
            {
                units = new List<TUnit>();
                _unitsByTeam.Add(team, units);
            }

            if (add)
            {
                if (unit != null && !units.Contains(unit))
                {
                    units.Add(unit);
                }
                return;
            }

            units.Remove(unit);
        }

        public IReadOnlyList<TUnit> GetUnits(Team team)
        {
            return _unitsByTeam.TryGetValue(team, out var units) ? units : Empty;
        }

        static readonly IReadOnlyList<TUnit> Empty = new List<TUnit>(0);
    }
}
