using System;

[Serializable]
public class PlayerAccountInfo
{
    public static PlayerAccountInfo Me;
    
    public string PlayFabId;
    public string TitleDisplayName;
    public string PlayFabUserName;// for login
    public string Email;
    public bool noAdsState;
    
    public string currentLinkedDeviceId;
    public int arenaPoint = -1; // 依靠是否为-1来判断玩家的竞技场分数和防御队伍是否已经登陆。
    public int arcadeProcess; // 已经打通的关卡，所以初始账号是从0开始 
    public int gangbangProcess; // 已经打通的关卡，所以初始账号是从0开始 
    public string tutorialProgress = string.Empty;
    
    private readonly ArcadeModeManager _arcadeModeManager = new ArcadeModeManager();
    private readonly GangbangModeManager _gangbangModeManager = new GangbangModeManager();
    public ArcadeModeManager ArcadeModeManager => _arcadeModeManager;
    public GangbangModeManager GangbangModeManager => _gangbangModeManager;
}