using DummyLayerSystem;
using mainMenu;

public class RankingPage : MSceneProcess
{
    private RankingLayer layer;
    private LeaderboardInfo _myLeaderboardInfo;
    
    public RankingPage()
    {
        Step = MainSceneStep.Ranking;
    }
    
    public override void ProcessEnter()
    {
        layer = UILayerLoader.Load<RankingLayer>();
        var arenaPage = (ArenaPage)ProcessesRunner.Main.GetProcess(MainSceneStep.Arena);
        _myLeaderboardInfo = arenaPage.MyLeaderboardInfo;
        layer.SetMyLeaderboardInfo(_myLeaderboardInfo);
        CloudScript.GetLeaderboard(
            obj =>
            {
                layer.DisplayOpponents(obj);
            }
        );
    }
    
    public override bool CanEnterOtherProcess()
    {
        return true;
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<RankingLayer>();
    }
}
