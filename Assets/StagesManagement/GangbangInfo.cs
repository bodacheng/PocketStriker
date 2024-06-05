using System.Collections.Generic;
using System;
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
        var sets = team == 1 ? team1GroupSet : team2GroupSet;
        List<string> returnValue = new List<string>();
        foreach (var set in sets)
        {
            if (set.Count > 0)
            {
                returnValue.Add(set.id);
            }
        }
        return returnValue;
    }
    
    public List<SoldierGroupSet> Team2GroupSet
    {
        get => team2GroupSet;
        set => team2GroupSet = value;
    }

    public int SetTeamUnitCount(int team, string instanceID, int count, bool force = false)
    {
        if (count < 0)
            count = 0;
        int ifWholeCount = GetIfGroupWholeUnitCount(team, instanceID, count);
        if (ifWholeCount <= CommonSetting.GangbangModeMaxUnitPerTeam || force)
        {
            var set = GetSoldierGroupSet(instanceID, team);
            set.Count = count;
        }
        return GetGroupWholeUnitCount(team);
    }

    public int GetTeamUnitCount(int team, string instanceID, bool useLocalData = false)
    {
        var set = GetSoldierGroupSet(instanceID, team, useLocalData);
        return set.Count;
    }
    
    // Start is called before the first frame update
    [Serializable]
    public class SoldierGroupSet
    {
        public SoldierGroupSet(string id, int count)
        {
            this.id = id;
            this.Count = count;
        }
        
        public string id;
        public int Count = 1;
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
        var sets = team == 1 ? team1GroupSet : team2GroupSet;
        var s = sets.Find(x => x.id == id);
        if (s == null)
        {
            s = new SoldierGroupSet(id, useLocalSet ? PlayerPrefs.GetInt("gangbangPos"+ id, 8) : 8);
            sets.Add(s);
            return s;
        }
        return s;
    }

    public int GetGroupWholeUnitCount(int team)
    {
        var sets = team == 1 ? team1GroupSet : team2GroupSet;
        int wholeUnitCount = 0;
        foreach (var set in sets)
        {
            wholeUnitCount += set.Count;
        }
        return wholeUnitCount;
    }

    int GetIfGroupWholeUnitCount(int team, string instanceID, int count)
    {
        var sets = team == 1 ? team1GroupSet : team2GroupSet;
        int wholeUnitCount = 0;
        foreach (var set in sets)
        {
            if (set.id != instanceID)
                wholeUnitCount += set.Count;
            else
                wholeUnitCount += count;
        }
        return wholeUnitCount;
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
    
    public static GangbangInfo Copy(GangbangInfo source)
    {
        var stage = CreateInstance<GangbangInfo>();
        
        stage.ID = source.ID;
        stage.ArcadeFightMode = source.ArcadeFightMode;
        stage.FightMembers = source.FightMembers;
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
        stage.team1GroupSet = new List<SoldierGroupSet>(source.team1GroupSet);
        stage.team2GroupSet = new List<SoldierGroupSet>(source.team2GroupSet);
        return stage;
    }
}
