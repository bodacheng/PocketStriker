using System.Collections.Generic;
using System;
using MCombat.Shared.CombatGroup;
using UnityEngine;

public class GangbangInfo : FightInfo
{
    private List<SoldierGroupSet> team1GroupSet = new List<SoldierGroupSet>();
    [SerializeField] private List<SoldierGroupSet> team2GroupSet = new List<SoldierGroupSet>();

    public List<SoldierGroupSet> Team1GroupSet
    {
        get => team1GroupSet;
        set => team1GroupSet = value;
    }

    public List<string> GetNonZeroInstanceIds(int team)
    {
        return GroupUnitCountUtility.GetNonZeroIds(GetTeamGroupSets(team));
    }

    public List<SoldierGroupSet> Team2GroupSet
    {
        get => team2GroupSet;
        set => team2GroupSet = value;
    }

    public int SetTeamUnitCount(int team, string instanceID, int count, bool force = false)
    {
        return SetTeamUnitCount(team, instanceID, count, CommonSetting.GangbangModeMaxUnitPerTeam, force);
    }

    public int SetTeamUnitCount(int team, string instanceID, int count, int teamMaxCount, bool force = false)
    {
        return GroupUnitCountUtility.SetTeamUnitCount(
            GetTeamGroupSets(team),
            instanceID,
            count,
            teamMaxCount,
            force,
            CreateSoldierGroupSet);
    }

    public int GetTeamUnitCount(int team, string instanceID, bool useLocalData = false)
    {
        var set = GetSoldierGroupSet(instanceID, team, useLocalData);
        return set.Count;
    }

    // Start is called before the first frame update
    [Serializable]
    public class SoldierGroupSet : IGroupUnitCountEntry
    {
        public SoldierGroupSet(string id, int count)
        {
            this.id = id;
            this.Count = count;
            OriginCount = count;
        }

        public string id;
        public string Id => id;
        public int Count = 1;
        public int OriginCount { get; set; }
        int IGroupUnitCountEntry.Count
        {
            get => Count;
            set => Count = value;
        }
    }

    SoldierGroupSet GetTeam1GroupSet(string id)
    {
        return GetSoldierGroupSet(id, 1);
    }

    public SoldierGroupSet GetTeam2GroupSet(string id)
    {
        return GetSoldierGroupSet(id, 2);
    }

    SoldierGroupSet GetSoldierGroupSet(string id, int team, bool useLocalSet = false)
    {
        return GroupUnitCountUtility.GetOrCreate(
            GetTeamGroupSets(team),
            id,
            CreateSoldierGroupSet,
            useLocalSet ? PlayerPrefs.GetInt("gangbangPos"+ id, 8) : 8);
    }

    public int GetGroupWholeUnitCount(int team)
    {
        return GroupUnitCountUtility.GetWholeCount(GetTeamGroupSets(team));
    }

    public void ClearWholeUnitCount(int team)
    {
        GroupUnitCountUtility.ClearWholeCount(GetTeamGroupSets(team));
    }

    int GetIfGroupWholeUnitCount(int team, string instanceID, int count)
    {
        return GroupUnitCountUtility.GetWholeCountIfSet(GetTeamGroupSets(team), instanceID, count);
    }

    public void ConvertTeamToGangbang()
    {
        var id = 0;
        var newHeroSets = new MultiDic<int, int, UnitInfo>();
        foreach (var unitInfo in this.FightMembers.HeroSets.GetValues())
        {
            var soldierSet = GetTeam1GroupSet(unitInfo.id);
            for (var i = 0; i < soldierSet.Count; i++)
            {
                var newUnitInfo = unitInfo.DeepCopy();
                newUnitInfo.id = id.ToString();
                newHeroSets.Set(0,id, newUnitInfo);
                id++;
            }
        }
        this.FightMembers.HeroSets = newHeroSets;

        id = 0;
        var newEnemySets = new MultiDic<int, int, UnitInfo>();
        foreach (var unitInfo in this.FightMembers.EnemySets.GetValues())
        {
            var soldierSet = GetTeam2GroupSet(unitInfo.id);
            for (var i = 0; i < soldierSet.Count; i++)
            {
                var newUnitInfo = unitInfo.DeepCopy();
                newUnitInfo.id = id.ToString();
                newEnemySets.Set(0,id, newUnitInfo);
                id++;
            }
        }
        this.FightMembers.EnemySets = newEnemySets;
    }

    public int GangbangAutoAdjustTeamUnitByMaxCount(int team, List<UnitInfo> unitSets, int selectedMaxTeamCount, bool adaptMode = false)
    {
        return GroupUnitCountUtility.AutoAdjustTeamUnitByMaxCount(
            GetTeamGroupSets(team),
            unitSets,
            selectedMaxTeamCount,
            adaptMode,
            team == 2,
            unitInfo => unitInfo?.id,
            CreateSoldierGroupSet);
    }

    List<SoldierGroupSet> GetTeamGroupSets(int team)
    {
        return team == 1 ? team1GroupSet : team2GroupSet;
    }

    static SoldierGroupSet CreateSoldierGroupSet(string id, int count)
    {
        return new SoldierGroupSet(id, count);
    }

    public static GangbangInfo Copy(GangbangInfo source)
    {
        var stage = CreateInstance<GangbangInfo>();

        stage.ID = source.ID;
        stage.ArcadeFightMode = source.ArcadeFightMode;
        stage.FightMembers = CopyFightMembers(source.FightMembers);
        stage.battleGroundID = source.battleGroundID;
        stage.stageRefLevel = source.stageRefLevel;
        stage.fightBGM = source.fightBGM;
        stage.team1Mode = TeamMode.MultiRaid;
        stage.team2Mode = TeamMode.MultiRaid;
        stage.Team1Auto = source.Team1Auto;
        stage.Team2Auto = source.Team2Auto;
        stage.team1AIMode = source.team1AIMode;
        stage.team2AIMode = source.team2AIMode;
        stage.Team1ID = source.Team1ID;
        stage.Team2ID = source.Team2ID;
        stage.team1HpRate = source.team1HpRate;
        stage.team2HpRate = source.team2HpRate;
        stage.team1CGMode = source.team1CGMode;
        stage.team2CGMode = source.team2CGMode;
        stage.Team1LeaderboardEntry = source.Team1LeaderboardEntry;
        stage.Team2LeaderboardEntry = source.Team2LeaderboardEntry;
        stage.RunTutorial = false;
        stage.EventType = source.EventType;
        stage.UnitsData = new List<UnitInfo>(source.UnitsData);
        stage.team1GroupSet = CopyGroupSets(source.team1GroupSet);
        stage.team2GroupSet = CopyGroupSets(source.team2GroupSet);
        return stage;
    }

    public static List<SoldierGroupSet> CopyGroupSets(List<SoldierGroupSet> source)
    {
        var result = new List<SoldierGroupSet>();
        if (source == null)
        {
            return result;
        }

        foreach (var set in source)
        {
            if (set == null)
            {
                continue;
            }

            var copied = new SoldierGroupSet(set.id, set.Count)
            {
                OriginCount = set.OriginCount > 0 ? set.OriginCount : set.Count
            };
            result.Add(copied);
        }
        return result;
    }

    static FightMembers CopyFightMembers(FightMembers source)
    {
        var result = new FightMembers();
        if (source == null)
        {
            return result;
        }

        result.HeroSets = CopyUnitSet(source.HeroSets);
        result.EnemySets = CopyUnitSet(source.EnemySets);
        return result;
    }

    static MultiDic<int, int, UnitInfo> CopyUnitSet(MultiDic<int, int, UnitInfo> source)
    {
        var result = new MultiDic<int, int, UnitInfo>();
        if (source == null)
        {
            return result;
        }

        foreach (var kv in source.mDict)
        {
            result.Set(kv.Key.Item1, kv.Key.Item2, kv.Value?.DeepCopy());
        }
        return result;
    }
}
