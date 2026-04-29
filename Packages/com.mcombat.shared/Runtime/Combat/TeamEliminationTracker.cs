using System.Collections.Generic;
using System.Linq;

namespace MCombat.Shared.Combat
{
    public sealed class TeamEliminationTracker<TUnit>
        where TUnit : class
    {
        readonly List<Team> _registeredTeams = new List<Team>();
        readonly Dictionary<Team, string> _teamIds = new Dictionary<Team, string>();
        readonly Dictionary<Team, int> _teamUnitCounts = new Dictionary<Team, int>();
        readonly Dictionary<Team, HashSet<TUnit>> _deadUnitsByTeam = new Dictionary<Team, HashSet<TUnit>>();
        readonly HashSet<Team> _deadTeams = new HashSet<Team>();

        public Team WinnerTeam { get; private set; } = Team.none;
        public bool IsGameOver { get; private set; }

        public void Clear()
        {
            _registeredTeams.Clear();
            _teamIds.Clear();
            _teamUnitCounts.Clear();
            _deadUnitsByTeam.Clear();
            _deadTeams.Clear();
            WinnerTeam = Team.none;
            IsGameOver = false;
        }

        public void RegisterTeam(Team team, string id, int unitCount)
        {
            if (!_teamUnitCounts.ContainsKey(team))
            {
                _registeredTeams.Add(team);
            }

            _teamIds[team] = id;
            _teamUnitCounts[team] = unitCount;
            if (!_deadUnitsByTeam.ContainsKey(team))
            {
                _deadUnitsByTeam.Add(team, new HashSet<TUnit>());
            }
        }

        public string GetWinnerId()
        {
            return _teamIds.TryGetValue(WinnerTeam, out var id) ? id : null;
        }

        public void MarkDead(Team team, TUnit unit)
        {
            if (!_deadUnitsByTeam.TryGetValue(team, out var deadUnits))
            {
                deadUnits = new HashSet<TUnit>();
                _deadUnitsByTeam.Add(team, deadUnits);
            }

            if (unit != null)
            {
                deadUnits.Add(unit);
            }

            if (_teamUnitCounts.TryGetValue(team, out var unitCount)
                && unitCount > 0
                && deadUnits.Count >= unitCount)
            {
                _deadTeams.Add(team);
            }

            if (!IsGameOver && _teamUnitCounts.Count > 0 && _teamUnitCounts.Count <= _deadTeams.Count + 1)
            {
                IsGameOver = true;
                var winner = _registeredTeams.Except(_deadTeams).ToList();
                WinnerTeam = winner.Count > 0 ? winner[0] : Team.none;
            }
        }
    }
}
