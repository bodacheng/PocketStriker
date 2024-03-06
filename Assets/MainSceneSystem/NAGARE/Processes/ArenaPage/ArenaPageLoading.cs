using System.Collections.Generic;
using mainMenu;

public partial class ArenaPage : MSceneProcess
{
    public LeaderboardInfo MyLeaderboardInfo => _myLeaderboardInfo;
    private LeaderboardInfo _myLeaderboardInfo;
    private List<LeaderboardInfo> opponents;
    void LoadLeaderboardInfos()
    {
        if (PlayerAccountInfo.Me.arenaPoint == -1)
        {
            // 说明玩家的防御队伍没有登陆，因为arenaPoint是首次登陆防御队伍时候顺便登陆的
            // 强制玩家登陆防御队伍
            LeaderBoardFinished(true);
            return;
        }
        
        CloudScript.GetLeaderboardAroundUser(
            leaderboardInfos =>
            {
                if (leaderboardInfos == null || leaderboardInfos.Count == 0)
                {
                    LeaderBoardFinished(true);
                    return;
                }
                
                opponents = new List<LeaderboardInfo>();
                foreach (var leaderboardInfo in leaderboardInfos)
                {
                    if (leaderboardInfo.PlayerLeaderboardEntry.PlayFabId != PlayerAccountInfo.Me.PlayFabId)
                    {
                        var info = opponents.Find(x=> x.PlayerLeaderboardEntry.PlayFabId == leaderboardInfo.PlayerLeaderboardEntry.PlayFabId);
                        if (info == null)
                            opponents.Add(leaderboardInfo);
                    }
                    else
                    {
                        _myLeaderboardInfo = leaderboardInfo;
                    }
                }
                LeaderBoardFinished(true);
            },
            () =>
            {
                LeaderBoardFinished(false);
            }
        );
    }
}
