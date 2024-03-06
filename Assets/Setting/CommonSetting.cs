using System.Collections.Generic;
using UnityEngine;

// 本模块定义的所有关键词只在运行模式下有效
[CreateAssetMenu(fileName = "CommonSetting", menuName = "ScriptableObjects/CommonSetting", order = 3)]
public class CommonSetting : ScriptableObject
{
    [SerializeField] bool devMode;
    [SerializeField] int maxStoneCount = 30;
    [SerializeField] int gangbangModeMaxUnitPerTeam = 30;
    [Tooltip("unit定义文件")]
    [SerializeField] string unitConfigFile = "mst_unit";
    [Tooltip("skill定义文件")]
    [SerializeField] string skillConfigFile = "mst_skill";
    [Tooltip("skill ai文件")]
    [SerializeField] string skillAIFile = "skill_ai_attrs";
    [Tooltip("skill name文件")]
    [SerializeField] string skillNameFile = "skill_name";
    [Tooltip("角色原生技能本地记录文件")]
    [SerializeField] string passiveSKillFile = "unit_passive";
    [Tooltip("关卡模式记录文件")]
    [SerializeField] string stageModeFile = "stage_mode";
    
    [Tooltip("SkillStaticAnalysis后不加.csv")]
    [SerializeField] string skillStaticAnalysis = "SkillStaticAnalysis";
    [Tooltip("SkillDynamicAnalysis后加.csv")]
    [SerializeField] string skillDynamicAnalysis = "SkillDynamicAnalysis.csv";
    
    [Tooltip("语言code文件")]
    [SerializeField] string languageCodeFile = "LanguageCode";
    
    [Tooltip("短故事文件")]
    [SerializeField] string shortStoryFile = "short_story";
        
    [Tooltip("GB短故事文件")]
    [SerializeField] string gbShortStoryFile = "gb_short_story";

    [Tooltip("admob interstitial ios Key")] 
    [SerializeField] string admob_interstitial_iosKey;
    [Tooltip("admob interstitial android Key")] 
    [SerializeField] string admob_interstitial_androidKey;
    
    [Tooltip("audio source key")]
    [SerializeField] string startThemeAddressKey = "music/start";
    [SerializeField] string lobbyThemeAddressKey = "music/lobby";
    [SerializeField] string fightThemeAddressKey1 = "music/fight1";
    [SerializeField] string fightThemeAddressKey2 = "music/fight2";

    [Tooltip("essential effects")] 
    [SerializeField] private string hitGroundEffectCode = "hitGround";
    [SerializeField] private string wallCrackEffectCode = "wallCrack";
    [SerializeField] private string breakFreeEffectCode = "breakFree";
    [SerializeField] private string memberShiftEffectCode = "memberShift";

    [Tooltip("sound effects")] 
    [SerializeField] AudioClip btnTapSound;
    [SerializeField] AudioClip btnConfirmSound;
    [SerializeField] AudioClip exTabSound;

    [Tooltip("角色动画平滑区间")] 
    [SerializeField] private float characterAnimDuration = 0.25f;
    
    [Tooltip("角色阴影材质")] 
    [SerializeField] Material shadowMaterial;

    [Tooltip("抵抗颜色")] 
    [SerializeField] private Color resistColor;
    
    [Tooltip("加速颜色")] 
    [SerializeField] private Color speedColor;
    
    [Tooltip("梦幻颜色")] 
    [SerializeField] private Color dreamColor;
    
    [Tooltip("downLoad labels")] 
    [SerializeField] List<string> downLoadLabels;

    [Tooltip("unit icon colors")]
    [SerializeField] private Color emptyBgColor;
    [SerializeField] private Color emptyFrameColor;
    [SerializeField] private Color lightBgColor;
    [SerializeField] private Color lightFrameColor;
    [SerializeField] private Color darkBgColor;
    [SerializeField] private Color darkFrameColor;
    [SerializeField] private Color redBgColor;
    [SerializeField] private Color redFrameColor;
    [SerializeField] private Color blueBgColor;
    [SerializeField] private Color blueFrameColor;
    [SerializeField] private Color greenBgColor;
    [SerializeField] private Color greenFrameColor;
    
    public List<string> DownLoadLabels => downLoadLabels;
    
    public static bool DevMode;
    public static int GangbangModeMaxUnitPerTeam;
    public static int MaxStoneCount;
    public static string UnitConfigFile;
    public static string SkillConfigFile;
    public static string SkillAIFile;
    public static string SkillNameFile;
    public static string SkillStaticAnalysis;
    public static string SkillDynamicAnalysis;
    public static string PassiveSKillFile;
    public static string LanguageCodeFile;
    public static string ShortStoryFile;
    public static string GBShortStoryFile;
    public static string StageModeFile;

    public static string Admob_interstitial_iosKey;
    public static string Admob_interstitial_androidKey;

    public static string StartThemeAddressKey;
    public static string LobbyThemeAddressKey;
    public static string FightThemeAddressKey1;
    public static string FightThemeAddressKey2;
    
    public static string HitGroundEffectCode;
    public static string WallCrackEffectCode;
    public static string BreakFreeEffectCode;
    public static string MemberShiftEffectCode;
    
    public static float CharacterAnimDuration;
    public static Material ShadowMaterial;
    public static AudioClip BtnTapSound;
    public static AudioClip BtnConfirmSound;
    public static AudioClip ExTabSound;
    
    public static Color _emptyBgColor;
    public static Color _emptyFrameColor;
    public static Color _lightBgColor;
    public static Color _lightFrameColor;
    public static Color _darkBgColor;
    public static Color _darkFrameColor;
    public static Color _redBgColor;
    public static Color _redFrameColor;
    public static Color _blueBgColor;
    public static Color _blueFrameColor;
    public static Color _greenBgColor;
    public static Color _greenFrameColor;
    
    public static Color ResistColor;
    public static Color DreamColor;
    public static Color SpeedColor;
    
    public void Initialise()
    {
        DevMode = devMode;
        MaxStoneCount = maxStoneCount;
        GangbangModeMaxUnitPerTeam = gangbangModeMaxUnitPerTeam;
        SkillStaticAnalysis = skillStaticAnalysis;
        SkillDynamicAnalysis = skillDynamicAnalysis;
        UnitConfigFile = unitConfigFile;
        SkillConfigFile = skillConfigFile;
        SkillAIFile = skillAIFile;
        SkillNameFile = skillNameFile;
        LanguageCodeFile = languageCodeFile;
        ShortStoryFile = shortStoryFile;
        GBShortStoryFile = gbShortStoryFile;
        StageModeFile = stageModeFile;
        PassiveSKillFile = passiveSKillFile;

        Admob_interstitial_iosKey = admob_interstitial_iosKey;
        Admob_interstitial_androidKey = admob_interstitial_androidKey;
        
        LobbyThemeAddressKey = lobbyThemeAddressKey;
        StartThemeAddressKey = startThemeAddressKey;
        FightThemeAddressKey1 = fightThemeAddressKey1;
        FightThemeAddressKey2 = fightThemeAddressKey2;
        
        HitGroundEffectCode = hitGroundEffectCode;
        WallCrackEffectCode = wallCrackEffectCode;
        BreakFreeEffectCode = breakFreeEffectCode;
        MemberShiftEffectCode = memberShiftEffectCode;
        
        CharacterAnimDuration = characterAnimDuration;
        ShadowMaterial = shadowMaterial;
        BtnTapSound = btnTapSound;
        BtnConfirmSound = btnConfirmSound;
        ExTabSound = exTabSound;
        
        _emptyBgColor = emptyBgColor;
        _emptyFrameColor = emptyFrameColor;
        _lightBgColor = lightBgColor;
        _lightFrameColor = lightFrameColor;
        _darkBgColor = darkBgColor;
        _darkFrameColor = darkFrameColor;
        _redBgColor = redBgColor;
        _redFrameColor = redFrameColor;
        _blueBgColor = blueBgColor;
        _blueFrameColor = blueFrameColor;
        _greenBgColor = greenBgColor;
        _greenFrameColor = greenFrameColor;
        
        ResistColor = resistColor;
        DreamColor = dreamColor;
        SpeedColor = speedColor;
    }
}
