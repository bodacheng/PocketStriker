using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using dataAccess;
using System.IO;
using PlayFab.ClientModels;

public class FightInfo : ScriptableObject
{
    public int battleGroundID;
    public int fightBGM = 0;
    
    // 底下这个记录的是敌人的信息
    [SerializeField] List<UnitInfo> unitsData = new List<UnitInfo>();

    public List<UnitInfo> UnitsData
    {
        get => unitsData;
        set => unitsData = value;
    }

    public string GetBGMKey()
    {
        switch (fightBGM)
        {
            case 0:
                return CommonSetting.FightThemeAddressKey1;
            case 1:
                return CommonSetting.FightThemeAddressKey2;
            default:
                return CommonSetting.FightThemeAddressKey1;
        }
    }
    
    public FightEventType EventType
    {
        set;
        get;
    }
    
    public float team1HpRate = 1f;
    public float team2HpRate = 1f;
    public CriticalGaugeMode team1CGMode = CriticalGaugeMode.Normal;
    public CriticalGaugeMode team2CGMode = CriticalGaugeMode.Normal;
    public TeamMode team1Mode = TeamMode.Rotation;
    public TeamMode team2Mode = TeamMode.Rotation;
    public AIMode team1AIMode = AIMode.Aggressive;
    public AIMode team2AIMode = AIMode.Aggressive;
    public int dumbAIDecisionDelay = 20;
    public int dreamComboAIRateNum = 5;
    public float stageRefLevel = 1;
    
    public int ArcadeFightMode
    {
        get;
        set;
    }

    public bool RunTutorial
    {
        set;
        get;
    }

    public string Team1OneWord
    {
        set;
        get;
    }
    
    public string Team2OneWord
    {
        set;
        get;
    }
    
    public void Awake()
    {
        OpenAndSetEnemyDataOnPlace();
    }

    public string ID
    {
        set;
        get;
    }
    public string Team1ID { set; get; }
    public string Team2ID { set; get; }
    
    public PlayerLeaderboardEntry Team1LeaderboardEntry {
        set;
        get;
    }
    
    public PlayerLeaderboardEntry Team2LeaderboardEntry {
        set;
        get;
    }
    
    public FightMembers FightMembers
    {
        set;
        get;
    }
    
    public bool Team1Auto
    {
        get;
        set;
    }
    
    public bool Team2Auto
    {
        get;
        set;
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// 
    /// </summary>
    /// <param name="targetTeam"></param>
    /// <param name="path">"Assets/" 开头</param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static FightInfo CreateFightInfoAsset(FightMembers targetTeam, string path, string fileName)
    {
        var fightInfo = CreateInstance<FightInfo>();
        if (!Directory.Exists(path))
        {
            //if it doesn't, create it
            Directory.CreateDirectory(path);
        }
        
        fightInfo.FightMembers = targetTeam;
        fightInfo.SaveDicToData();
        fightInfo.team1Mode = TeamMode.Rotation;
        fightInfo.team2Mode = TeamMode.Rotation;
        
        AssetDatabase.CreateAsset(fightInfo, path + "/" + fileName + ".asset");
        Debug.Log("Generated：" + path + "/" + fileName + ".asset");
        AssetDatabase.Refresh();
        return fightInfo;
    }
    
    public static GangbangInfo CreateGangbangInfoAsset(FightMembers targetTeam, string path, string fileName)
    {
        var gangbangInfo = CreateInstance<GangbangInfo>();
        if (!Directory.Exists(path))
        {
            //if it doesn't, create it
            Directory.CreateDirectory(path);
        }
        
        gangbangInfo.FightMembers = targetTeam;
        gangbangInfo.SaveDicToData();
        gangbangInfo.team1Mode = TeamMode.Rotation;
        gangbangInfo.team2Mode = TeamMode.Rotation;
        
        AssetDatabase.CreateAsset(gangbangInfo, path + "/" + fileName + ".asset");
        Debug.Log("Generated：" + path + "/" + fileName + ".asset");
        AssetDatabase.Refresh();
        return gangbangInfo;
    }
    #endif

    public void OpenAndSetEnemyDataOnPlace()
    {
        ID = this.name;
        FightMembers = new FightMembers();
        for (var i = 0; i < unitsData.Count; i++)
        {
            FightMembers.EnemySets.Set(0,i, unitsData[i]);
        }
    }

    public void SaveDicToData()
    {
        unitsData = new List<UnitInfo>();
        foreach (var info in FightMembers.EnemySets.GetValues())
        {
            if (info != default)
                unitsData.Add(info);
        }
    }
    
    public void LoadMyTeam()
    {
        PosKeySet set;
        switch (EventType)
        {
            case FightEventType.Quest:
                set = TeamSet.Default;
                break;
            case FightEventType.Arena:
                set = TeamSet.Arena3V3;
                break;
            case FightEventType.Gangbang:
                set = TeamSet.Gangbang;
                break;
            default:
                set = TeamSet.Default;
                break;
        }
        
        FightMembers.HeroSets = set.LoadTeamDic();
        Team1ID = PlayerAccountInfo.Me.PlayFabId;
    }
    
    public static FightInfo ArenaStage(FightMembers fightUnits)
    {
        var stage = CreateInstance<FightInfo>();
        stage.FightMembers = fightUnits;
        stage.battleGroundID = 0;
        stage.team1Mode = TeamMode.Rotation;
        stage.team2Mode = TeamMode.Rotation;
        stage.EventType = FightEventType.Arena;
        return stage;
    }

    public static FightInfo Copy(FightInfo source)
    {
        var stage = CreateInstance<FightInfo>();
        
        stage.ID = source.ID;
        stage.ArcadeFightMode = source.ArcadeFightMode;
        stage.FightMembers = source.FightMembers;
        stage.battleGroundID = source.battleGroundID;
        stage.stageRefLevel = source.stageRefLevel;
        stage.fightBGM = source.fightBGM;
        stage.team1Mode = source.team1Mode;
        stage.team2Mode = source.team2Mode;
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
        stage.RunTutorial = source.RunTutorial;
        stage.EventType = source.EventType;
        stage.dreamComboAIRateNum = source.dreamComboAIRateNum;
        return stage;
    }
    
    public static FightInfo RandomSkillTestStage(TeamMode teamMode)
    {
        var stage = CreateInstance<FightInfo>();
        stage.FightMembers = FightMembers.RandomSkillTest(teamMode);
        stage.battleGroundID = 0;
        stage.fightBGM = 0;
        stage.Team1Auto = true;
        stage.Team2Auto = true;
        stage.team1Mode = teamMode;
        stage.team2Mode = teamMode;
        stage.EventType = FightEventType.SkillTest;
        return stage;
    }
    
    public static FightInfo ScreenSaverStage(TeamMode teamMode)
    {
        var stage = CreateInstance<FightInfo>();
        stage.FightMembers = FightMembers.ScreenSaver(teamMode);
        stage.battleGroundID = 1;
        stage.fightBGM = 0;
        stage.Team1Auto = true;
        stage.Team2Auto = true;
        stage.team1Mode = teamMode;
        stage.team2Mode = teamMode;
        stage.EventType = FightEventType.SkillTest;
        return stage;
    }
    
    public static FightInfo RandomStage()
    {
        var stage = CreateInstance<FightInfo>();
        stage.FightMembers = FightMembers.RandomFight();
        stage.battleGroundID = 0;
        stage.fightBGM = 0;
        stage.team1Mode = TeamMode.Rotation;
        stage.team2Mode = TeamMode.Rotation;
        stage.EventType = FightEventType.Arena;
        return stage;
    }
}

public enum CriticalGaugeMode
{
    Normal,
    DoubleGain,
    Unlimited
}

public enum AIMode
{
    Aggressive,
    Dumb
}

// 系统会根据这个量来决定一场战斗结束后应该做什么。
// 比如一个剧情战斗，他结束了后应该是播放某个动画片，
// 再比如是自己打自己的一个战斗，结束后回到的应该是那个自己打自己的选人菜单。
public enum FightEventType
{
    Screensaver = 0,
    Quest = 1,
    Gangbang = 3,
    Arena = 2,
    Self = 4,
    SkillTest = 5
}

public enum TeamMode
{
    Keep = 0,
    MultiRaid = 1,
    Rotation = 2
}